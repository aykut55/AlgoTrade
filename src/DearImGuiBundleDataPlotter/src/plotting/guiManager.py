import os
import time

from imgui_bundle import imgui, implot, hello_imgui

from .panel import Panel
from .panelData import PanelData
from .panelManager import PanelManager
from .interactionManager import InteractionManager
from .tradeSignalRenderer import TradeSignalRenderer
from .rangeSliderBar import RangeSliderBar
from .runtimeCommandManager import RuntimeCommandManager
from .scriptRunner import ScriptRunner
from .dataManager import DataManager
from .poolDataManager import PoolDataManager
from .poolPanel import PoolPanel
from .consolePanel import ConsolePanel
from .externalIndicatorWindowManager import ExternalIndicatorWindowManager
from .menuBar import MenuBar
from .leftMenuPanel import LeftMenuPanel
from .panelManagerWindow import PanelManagerWindow
from .panelManagerWindow2 import PanelManagerWindow2
from .reportsWindow import ReportsWindow
from ..trading.stockDataReader import StockDataReader, FilterMode
from ..trading.indicatorManager import IndicatorManager
from ..trading.indicators import Indicators


class GuiManager:
    """DearPyGuiDataPlotter'daki GuiManager'in imgui_bundle/implot PORTU.
    Mimari (geometri sabitleri, 5 kutulu toolbar, topPanel/centerPanel
    (centerTopPanel+centerCenterPanel)/bottomPanel dikey yerlesimi, render
    dongusu sirasi) AYNEN korunuyor - sadece DPG cagrilari imgui/implot'a
    cevrildi (bkz. plan C:\\Users\\User\\.claude\\plans\\silly-mapping-hopper.md §5).

    ScriptPanel (REPL UI), DataManager, PoolPanel (+PoolDataManager),
    ConsolePanel, ExternalIndicatorWindowManager, MenuBar, LeftMenuPanel,
    PanelManagerWindow ve tema toggle burada entegredir."""

    MARGIN = 7
    TOP_HEIGHT = 135
    BOTTOM_HEIGHT = 60
    PANEL_PADDING = (10, 10)
    CENTER_TOP_HEIGHT = 170

    TOP_VIEW_MODES = ["FitToScreen (Normal)", "FitToScreen (Wide)", "FitToScreen (Ultra)",
                      "Full Data", "Last N Data", "First N Data", "Range"]
    TOP_VIEW_MODE_FIELDS = {"Last N Data": ("n",), "First N Data": ("n",), "Range": ("n", "n2")}
    PAN_MODES = ["VisibleScreenWidth", "1 Bar", "10 Bar", "100 Bar", "1000 Bar", "UserDefined"]
    CROSSHAIR_MODES = ["Hidden", "Single", "All"]
    ZOOM_RATIOS = ["10%", "20%", "30%", "50%", "100%"]
    ACTIVE_UPDATE_MODES = ["Hover", "Click"]

    TOP_PANEL_DEFAULTS = {
        "activeUpdateMode": "Click",
        "viewRange": {
            "mode": "FitToScreen (Ultra)", "n": 0, "n2": 0,
            "fitToScreenBarWidth": {"normal": 4.0, "wide": 2.5, "ultra": 1.5},
        },
        "panMode": "VisibleScreenWidth",
        "panStep": 100,
        "zoomRatio": "30%",
        "showSliderRange": False,
        "crossHairMode": "All",
        "showScrollBar": True,
        "showInfoPanels": True,
        "autoSyncX": True,
        "showBarNumbers": False,
        "autoSyncY": False,
        "showOhlc": True,
        "showPlotCaptions": True,
    }

    def __init__(self, configManager):
        self.configManager = configManager
        self.topPanelConfig = self._loadTopPanelConfig()

        # ConsolePanel EN BASTA kuruluyor (DPG orijinalindeki gibi) ki
        # kalan __init__/build() boyunca yapilan print()'ler de yakalansin.
        self.consolePanel = ConsolePanel()
        self.consolePanel.attachStdout()

        self.panelManager = PanelManager()

        self.tsr = TradeSignalRenderer(self.panelManager)
        self.panelManager.setTradeSignalRenderer(self.tsr)
        self.tradeSignalRenderer = self.tsr  # DPG orijinalindeki ikinci ad (guiManager.py'de de ikisi de var)

        self.interactionManager = InteractionManager()
        self.panelManager.setInteractionManager(self.interactionManager)
        self.interactionManager.setPanelManager(self.panelManager)
        self.interactionManager.ensureHandlers()

        self.dataManager = DataManager()

        self.poolDataManager = PoolDataManager()
        self.poolPanel = PoolPanel()
        self.poolPanel.setPoolDataManager(self.poolDataManager)
        self.panelManager.setPoolDropHandler(self._onPoolItemDroppedOnPanel)

        self.externalIndicatorWindowManager = ExternalIndicatorWindowManager(self.panelManager)

        self.leftMenuPanel = LeftMenuPanel()
        self.leftMenuPanel.setPanelManager(self.panelManager)
        self.leftMenuPanel.setPoolDataManager(self.poolDataManager)

        self.panelManagerWindow = PanelManagerWindow()
        self.panelManagerWindow.setPanelManager(self.panelManager)
        self.panelManagerWindow2 = PanelManagerWindow2()
        self.panelManagerWindow2.setPanelManager(self.panelManager)
        self.reportsWindow = ReportsWindow(self)

        self.menuBar = MenuBar()
        self.menuBar.set_callback(
            on_exit=self.requestShutdown,
            on_reset_views=self._onResetViewAll,
            on_toggle_theme=self.toggleTheme,
            on_toggle_left_menu=self.leftMenuPanel.toggle,
            on_toggle_pool=self.poolPanel.toggle,
            on_toggle_script=lambda: self.scriptPanel.setVisible(not self.scriptPanel.getVisible()),
            on_toggle_console=self.consolePanel.toggle,
            on_manage_panels=self.panelManagerWindow.toggle,
            on_manage_panels2=self.panelManagerWindow2.toggle,
            on_toggle_reports=self.reportsWindow.toggle,
            on_manage_data=self.dataManager.toggle,
            on_open_all=self.panelManager.showAllPanels,
            on_collapse_all=self.panelManager.collapseAllPanels,
            on_expand_all=self.panelManager.expandAllPanels,
            on_close_all=self.panelManager.hideAllPanels,
            on_open_indicator=self.externalIndicatorWindowManager.openDefaultIndicatorWindow,
            on_open_indicator_selector=self.externalIndicatorWindowManager.openIndicatorWindowSelector,
            on_command_query=self._onCommandQuery,
        )

        projectDir = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
        scriptsDir = os.path.join(projectDir, "scripts")
        scriptRunner = ScriptRunner(scriptsDir)
        scriptRunner.setGlobals(gm=self, pm=self.panelManager, pool=self.poolDataManager,
                                pm2=self.panelManagerWindow2,
                                Panel=Panel, PanelData=PanelData,
                                TradeSignalRenderer=TradeSignalRenderer, tsr=self.tsr,
                                StockDataReader=StockDataReader, FilterMode=FilterMode,
                                IndicatorManager=IndicatorManager, Indicators=Indicators,
                                dataManager=self.dataManager)
        self.scriptPanel = scriptRunner  # bkz. scriptRunner.py - runtimeCommandManager.py (verbatim) bu adi okuyor

        self.rangeSliderBar = RangeSliderBar()
        self.rangeSliderBar.setPanelManager(self.panelManager)
        self.rangeSliderBar.setViewChangeCallback(self._onRangeNavigatorViewChanged)

        self.runtimeCommandManager = RuntimeCommandManager(self)

        self._isDarkTheme = str(self.configManager.get("theme", "dark")).lower() != "light"
        self._themeApplied = False

        self.currentPreparedData = None
        self.currentView = None
        self.currentBundlePath = None
        self.currentViewPath = None

        self._shutdownRequested = False
        self._bottomPanelVisible = False
        self._statusText = ""
        self._panStatusText = ""
        self._zoomStatusText = "(0,0)"
        self._zoomStates = {}  # {panelId: {"x0","y0","xCount","yCount"}}
        self._topViewModeValues = {}

        topCfg = self.topPanelConfig
        viewRange = topCfg["viewRange"]
        self._activeUpdateModeIdx = self.ACTIVE_UPDATE_MODES.index(topCfg["activeUpdateMode"]) \
            if topCfg["activeUpdateMode"] in self.ACTIVE_UPDATE_MODES else 1
        self._viewModeIdx = self.TOP_VIEW_MODES.index(viewRange["mode"]) \
            if viewRange["mode"] in self.TOP_VIEW_MODES else 2
        self._viewN = int(viewRange.get("n", 0))
        self._viewN2 = int(viewRange.get("n2", 0))
        self._panModeIdx = self.PAN_MODES.index(topCfg["panMode"]) if topCfg["panMode"] in self.PAN_MODES else 0
        self._panStep = int(topCfg.get("panStep", 100))
        self._zoomRatioIdx = self.ZOOM_RATIOS.index(topCfg["zoomRatio"]) if topCfg["zoomRatio"] in self.ZOOM_RATIOS else 2
        self._showSliderRange = bool(topCfg["showSliderRange"])
        self._crossHairModeIdx = self.CROSSHAIR_MODES.index(topCfg["crossHairMode"]) \
            if topCfg["crossHairMode"] in self.CROSSHAIR_MODES else 2
        self._showScrollBar = bool(topCfg["showScrollBar"])
        self._showInfoPanels = bool(topCfg["showInfoPanels"])
        self._autoSyncXEnabled = bool(topCfg["autoSyncX"])
        self._showBarNumbers = bool(topCfg["showBarNumbers"])
        self._autoSyncYEnabled = bool(topCfg["autoSyncY"])
        self._showOhlc = bool(topCfg["showOhlc"])
        self._showPlotCaptions = bool(topCfg["showPlotCaptions"])
        self._lastAutoSyncXTime = 0.0
        self._lastAutoSyncYTime = 0.0
        self._autoSyncIntervalSec = 0.15
        self._rangeNavigatorCollapsed = False
        self._rangeNavigatorDetached = False
        self._rangeNavigatorDetachedSizePending = False

    def _loadTopPanelConfig(self):
        raw = self.configManager.get("topPanel") or {}
        cfg = dict(self.TOP_PANEL_DEFAULTS)
        if isinstance(raw, dict):
            cfg.update(raw)
            viewRange = dict(self.TOP_PANEL_DEFAULTS["viewRange"])
            if isinstance(raw.get("viewRange"), dict):
                viewRange.update(raw["viewRange"])
            fitDefaults = self.TOP_PANEL_DEFAULTS["viewRange"]["fitToScreenBarWidth"]
            fit = dict(fitDefaults)
            if isinstance(viewRange.get("fitToScreenBarWidth"), dict):
                fit.update(viewRange["fitToScreenBarWidth"])
            viewRange["fitToScreenBarWidth"] = fit
            cfg["viewRange"] = viewRange
        return cfg

    def build(self):
        """DPG orijinalinde dpg.create_context/viewport kurulumu yapiyordu -
        burada immapp.run() zaten cagrilmadan ONCE main.py tarafindan
        cagrilir, GERCEK pencere/font kurulumu main.py'nin sorumlulugunda.
        build() SADECE topPanel baslangic durumunu (DPG default_value'nun
        callback tetiklemedigi icin el ile uygulanan _applyTopPanelInitialState
        karsiligi) manager'lara uygular - bu port'ta state zaten __init__'te
        kuruldugu icin ayrica bir 'uygula' adimina GEREK YOK, sadece
        fitToScreenBarWidth PanelManager'a aktarilir."""
        try:
            self.rangeSliderBar.build("centerTopPanel")
        except Exception:
            # RangeSliderBar implot portunun build() imzasi/davranisi bu
            # dosyayla PARALEL yazildi - DPG orijinalinde build() bir kerelik
            # widget kurulumuydu, immediate-mode'da render()'in kendisi zaten
            # her frame yeniden cizdigi icin build()'in no-op/farkli imzali
            # olmasi engelleyici degil (bkz. plan - entegrasyon riski).
            pass
        fit = self.topPanelConfig["viewRange"].get("fitToScreenBarWidth") or {}
        self.panelManager.setFitToScreenBarWidth(
            normal=fit.get("normal", 4.0), wide=fit.get("wide", 2.5), ultra=fit.get("ultra", 1.5))
        # DIKKAT: bu cagri eksikti - config.json'daki viewRange.mode SADECE
        # toolbar combo'sunun baslangic index'ini (_viewModeIdx) belirliyordu,
        # PanelManager._defaultViewMode kendi __init__ sabitinde ("FitToScreen
        # (Ultra)") kaliyordu; yani config'teki mod kullanici "Apply"'a
        # basmadan GERCEKTE devreye girmiyordu. setDefaultViewMode burada
        # cagrilarak _maybeApplyDefaultViewOnLoad'un (fresh bundle yuklendiginde)
        # config'teki modu (bkz. configManager.py DEFAULT_CONFIG - "Full Data")
        # GERCEKTEN kullanmasi saglanir.
        self.panelManager.setDefaultViewMode(
            self.topPanelConfig["viewRange"]["mode"], n=self._viewN, n2=self._viewN2)
        self.panelManager.setActiveUpdateMode(self.topPanelConfig["activeUpdateMode"].lower())
        self.panelManager.setCrossHairMode(self.topPanelConfig["crossHairMode"].lower())
        self.panelManager.setInfoPanelMode("always" if self._showInfoPanels else "hidden")
        self.panelManager.setXAxisMode("bar" if self._showBarNumbers else "datetime")
        self.panelManager.setShowCaptions(self._showPlotCaptions)
        self.rangeSliderBar.setSliderVisible(self._showSliderRange)
        self.rangeSliderBar.setScrollbarVisible(self._showScrollBar)
        self.tsr.setActiveOhlcVisible(self._showOhlc)
        self._applyDebugConfig()
        self._applyConfiguredPanelVisibility()

    def _applyConfiguredPanelVisibility(self):
        """config.json'daki panel görünürlük ve başlangıç geometrilerini uygular."""
        panelsCfg = self.configManager.get("panels") or {}

        def coordsOf(name, defaultVisible):
            cfg = panelsCfg.get(name) or {}
            coords = cfg.get("initialCoordinates") or {}
            visible = bool(cfg.get("visible", defaultVisible))
            return coords, visible

        _, visible = coordsOf("scriptPanel", True)
        self.scriptPanel.setVisible(visible)

        coords, visible = coordsOf("dataManager", False)
        if coords and hasattr(self.dataManager, "applyInitialState"):
            self.dataManager.applyInitialState(coords, visible)
        else:
            self.dataManager.setVisible(visible)

        coords, visible = coordsOf("poolPanel", False)
        self.poolPanel.build(coords.get("x", 1000), coords.get("y", 147),
                             coords.get("width", 420), coords.get("height", 600))
        if visible:
            self.poolPanel.show()

        coords, visible = coordsOf("consolePanel", False)
        if hasattr(self.consolePanel, "setGeometry"):
            self.consolePanel.setGeometry(coords.get("x", 1546), coords.get("y", 705),
                                          coords.get("width", 520), coords.get("height", 261))
        self.consolePanel.setVisible(visible)

        coords, visible = coordsOf("panelManagerWindow", False)
        self.panelManagerWindow.build(coords.get("x", 550), coords.get("y", 100),
                                      coords.get("width", 700), coords.get("height", 900))
        if visible:
            self.panelManagerWindow.show()

        # Panel Manager 2 ayri bir alternatif arayuzdur; ilk acilista menu
        # veya Script Panel'deki ``pm2.show()`` ile acilir.
        self.panelManagerWindow2.build(580, 120, 760, 900)

        coords, visible = coordsOf("leftMenu", True)
        self.leftMenuPanel.build(coords.get("x", 10), coords.get("y", 160),
                                 coords.get("width", 300), coords.get("height", 700))
        if visible:
            self.leftMenuPanel.show()
        else:
            self.leftMenuPanel.hide()

    def _onPoolItemDroppedOnPanel(self, panelId, dragId):
        """panelManager.setPoolDropHandler ile baglanir - panelManager'in
        drag-drop target'i yakaladigi (panelId, imgui payload int'i) ciftini
        gercek PoolItem'e cevirip panele ekler (bkz. poolPanel.resolveDragData/
        panelManager.addPoolItemToPanel)."""
        poolItem = self.poolPanel.resolveDragData(dragId)
        if poolItem is None:
            return
        self.panelManager.addPoolItemToPanel(panelId, poolItem)

    def toggleTheme(self):
        """DPG orijinali kendi elle-yazilmis duz 'light' renk setini
        bind_theme(0) (varsayilan dark) ile degistiriyordu - burada ImGui'nin
        kendi hazir StyleColorsDark/StyleColorsLight presetleri kullaniliyor
        (DPG'de bu hazir setler yoktu, custom yazmak gerekiyordu - burada
        ImGui'nin kendi ozelligi oldugu icin mekanik bir basitlestirme,
        kullaniciya sunulan 'tema degistir' yetenegi AYNI)."""
        self._isDarkTheme = not self._isDarkTheme
        if self._isDarkTheme:
            imgui.style_colors_dark()
        else:
            imgui.style_colors_light()
        self.configManager.set("theme", "dark" if self._isDarkTheme else "light")
        self.configManager.save()
        self._themeApplied = True

    def _applyDebugConfig(self):
        debug = self.configManager.get("debug") or {}
        self.interactionManager.setEventLoggingEnabled(bool(debug.get("interactionEvents", False)))
        self.interactionManager.setPrintFormatted(bool(debug.get("interactionEventsFormatted", False)))

    def requestShutdown(self):
        """RuntimeCommandManager'in 'shutdown' komutu bunu cagirir. DPG
        orijinali dpg.stop_dearpygui() cagirirdi - implot/immapp'ta pencereyi
        kapatmanin karsiligi RunnerParams.app_shall_exit=True (main.py'nin
        immapp.run() dongusu bunu bir sonraki frame kontrol eder)."""
        self._shutdownRequested = True
        try:
            hello_imgui.get_runner_params().app_shall_exit = True
        except Exception:
            pass

    # ------------------------------------------------------------- render
    def _applyThemeForFrame(self):
        """Config temasini ilk gercek ImGui frame'inde uygular."""
        if not self._themeApplied:
            if self._isDarkTheme:
                imgui.style_colors_dark()
            else:
                imgui.style_colors_light()
            self._themeApplied = True

    def renderTopDockContent(self):
        """Tam genislikteki ust dock: menu ve Top Panel."""
        if self._shutdownRequested:
            return
        self._applyThemeForFrame()
        self.menuBar.render()
        imgui.separator()
        imgui.dummy((0.0, 12.0))
        self._renderTopPanel()
        imgui.separator()
        imgui.dummy((0.0, 12.0))

    def render(self):
        """Docking disi/geriye uyumlu tek-pencere render yolu."""
        if self._shutdownRequested:
            return
        self._applyThemeForFrame()
        self.menuBar.render()
        imgui.separator()
        imgui.dummy((0.0, 10.0))
        self._renderTopPanel()
        imgui.separator()
        imgui.dummy((0.0, 10.0))
        self.renderChartsDockContent()

    def renderChartsDockContent(self):
        """Top dock'un altinda kalan merkez grafik ve arac pencereleri."""
        if self._shutdownRequested:
            return
        self._applyThemeForFrame()
        avail = imgui.get_content_region_avail()
        bottomSpace = self.BOTTOM_HEIGHT + self.MARGIN if self._bottomPanelVisible else 0.0
        centerHeight = max(100.0, avail.y - bottomSpace)
        imgui.push_style_var(imgui.StyleVar_.window_padding.value, self.PANEL_PADDING)
        imgui.begin_child("centerPanel", size=(-1, centerHeight))
        rangeNavigatorVisible = self._showSliderRange or self._showScrollBar
        if rangeNavigatorVisible and not self._rangeNavigatorDetached:
            navigatorHeight = self._getRangeNavigatorHeight()
            imgui.begin_child("centerTopPanel", size=(-1, navigatorHeight),
                              child_flags=imgui.ChildFlags_.borders.value)
            self._renderRangeNavigatorHeader(detached=False)
            if not self._rangeNavigatorCollapsed:
                self.rangeSliderBar.render()
            imgui.end_child()
        imgui.begin_child("centerCenterPanel", size=(-1, -1))
        self.panelManager.render()
        imgui.end_child()
        imgui.end_child()
        imgui.pop_style_var()

        if self._bottomPanelVisible:
            self._renderBottomPanel()

        # Bagimsiz floating pencereler (layout'taki hicbir slotu isgal etmezler,
        # kendi imgui.begin()/end() cagrilariyla her frame cizilirler - DPG
        # orijinalindeki ayri dpg.window'larin immediate-mode karsiligi).
        self.scriptPanel.render()
        self.dataManager.render()
        self.poolPanel.render()
        self.consolePanel.render()
        self.externalIndicatorWindowManager.render()
        self.leftMenuPanel.render()
        self.panelManagerWindow.render()
        self.panelManagerWindow2.render()
        self.reportsWindow.render()
        if rangeNavigatorVisible and self._rangeNavigatorDetached:
            self._renderDetachedRangeNavigator()

        self._updateAutoSyncX()
        self._updateAutoSyncY()
        self.interactionManager.onTick()
        self.runtimeCommandManager.processPendingCommands()

    def sync(self):
        self.rangeSliderBar.sync()
        self.panelManager.sync()

    # DearPyGui GuiManager public surface compatibility -----------------
    def buildLayout(self):
        return self.build()

    def destroyLayout(self):
        self.panelManager.deleteAllPanels()

    def deferFrames(self, frameCount, callback):
        # Immediate-mode callbacks are safe to execute at the next render site;
        # current callers only use this as a post-layout deferral.
        if callback:
            callback()

    def toggleScript(self):
        self.scriptPanel.setVisible(not self.scriptPanel.getVisible())

    def toggleDataManager(self): self.dataManager.toggle()
    def toggleLeftMenu(self): self.leftMenuPanel.toggle()
    def togglePanelManagerWindow(self): self.panelManagerWindow.toggle()
    def togglePanelManagerWindow2(self): self.panelManagerWindow2.toggle()
    def toggleReportsWindow(self): self.reportsWindow.toggle()
    def togglePoolPanel(self): self.poolPanel.toggle()

    def _onCommandQuery(self, query):
        """Small command palette matching the menu's most-used actions."""
        command = (query or "").strip().lower()
        actions = {
            "left menu": self.leftMenuPanel.toggle,
            "pool": self.poolPanel.toggle,
            "script": lambda: self.scriptPanel.setVisible(not self.scriptPanel.getVisible()),
            "console": self.consolePanel.toggle,
            "panel manager": self.panelManagerWindow.toggle,
            "panel manager 2": self.panelManagerWindow2.toggle,
            "reports": self.reportsWindow.toggle,
            "data manager": self.dataManager.toggle,
            "indicator": self.externalIndicatorWindowManager.openDefaultIndicatorWindow,
            "theme": self.toggleTheme,
            "reset": self._onResetViewAll,
        }
        for name, action in actions.items():
            if command == name or command in name:
                action()
                self._setStatusText(f"Command: {name}")
                return
        self._setStatusText(f"Unknown command: {query}")

    # --------------------------------------------------------- top toolbar
    def _renderTopPanel(self):
        imgui.push_style_var(imgui.StyleVar_.window_padding.value, self.PANEL_PADDING)
        imgui.begin_child("topPanel", size=(-1, self.TOP_HEIGHT),
                          child_flags=imgui.ChildFlags_.borders.value)
        imgui.push_style_var(imgui.StyleVar_.frame_padding.value, (4, 3))
        self._renderTopBox1()
        imgui.same_line()
        self._renderTopBox2()
        imgui.same_line()
        self._renderTopBox3()
        imgui.same_line()
        self._renderTopBox5()
        imgui.same_line()
        self._renderTopBox4()
        imgui.pop_style_var()
        imgui.end_child()
        imgui.pop_style_var()

    def _renderRangeNavigatorHeader(self, detached=False):
        """Range slider/scrollbar ortak arac panelinin baslik satiri."""
        collapseLabel = "Expand" if self._rangeNavigatorCollapsed else "Collapse"
        if imgui.small_button(f"{collapseLabel}##range_navigator_collapse"):
            self._rangeNavigatorCollapsed = not self._rangeNavigatorCollapsed
        imgui.same_line()
        attachLabel = "Attach" if detached else "Detach"
        if imgui.small_button(f"{attachLabel}##range_navigator_detach"):
            self._rangeNavigatorDetached = not detached
            if self._rangeNavigatorDetached:
                self._rangeNavigatorDetachedSizePending = True
        imgui.same_line()
        if imgui.small_button("Hide##range_navigator_hide"):
            self._showSliderRange = False
            self._showScrollBar = False
            self.rangeSliderBar.setSliderVisible(False)
            self.rangeSliderBar.setScrollbarVisible(False)

        caption = "Range Navigator"
        captionWidth = imgui.calc_text_size(caption).x
        imgui.same_line()
        imgui.set_cursor_pos_x(max(imgui.get_cursor_pos_x() + 8.0,
                                   (imgui.get_window_width() - captionWidth) * 0.5))
        imgui.text_unformatted(caption)
        imgui.separator()

    def _getRangeNavigatorHeight(self):
        if self._rangeNavigatorCollapsed:
            return 42
        if self._showSliderRange and self._showScrollBar:
            return self.CENTER_TOP_HEIGHT
        if self._showSliderRange:
            return 150
        if self._showScrollBar:
            return 72
        return 0

    def _renderDetachedRangeNavigator(self):
        if self._rangeNavigatorDetachedSizePending:
            imgui.set_next_window_size(
                (900.0, float(self._getRangeNavigatorHeight() + 40)),
                imgui.Cond_.once.value)
            self._rangeNavigatorDetachedSizePending = False
        expanded, stillOpen = imgui.begin("Range Navigator##detached", True)
        if not stillOpen:
            self._rangeNavigatorDetached = False
            imgui.end()
            return
        if expanded:
            self._renderRangeNavigatorHeader(detached=True)
            if not self._rangeNavigatorCollapsed:
                self.rangeSliderBar.render()
        imgui.end()

    def _renderTopBox1(self):
        imgui.begin_child("topPanelGroupBox1", size=(350, -1), child_flags=imgui.ChildFlags_.borders.value)
        imgui.text("Active Panel")
        imgui.same_line()
        imgui.set_next_item_width(90)
        changed, idx = imgui.combo("##activeUpdateMode", self._activeUpdateModeIdx, self.ACTIVE_UPDATE_MODES)
        if changed:
            self._activeUpdateModeIdx = idx
            self.panelManager.setActiveUpdateMode(self.ACTIVE_UPDATE_MODES[idx].lower())
        imgui.same_line()
        imgui.set_next_item_width(150)
        activeId = self.panelManager.getActivePanelId()
        panels = list(self.panelManager.iterateAllPanels())
        activeLabel = next((f"Panel {p.id}: {p.name}" for p in panels if p.id == activeId), "None")
        imgui.text(activeLabel)  # DPG'deki gosterge-combo'nun yerine canli metin (immediate-mode'da 'refresh' adimina gerek yok)

        imgui.text("View / Range")
        imgui.same_line()
        imgui.set_next_item_width(150)
        changed, idx = imgui.combo("##viewMode", self._viewModeIdx, self.TOP_VIEW_MODES)
        if changed:
            self._saveTopViewModeValues(self.TOP_VIEW_MODES[self._viewModeIdx])
            self._viewModeIdx = idx
            self._restoreTopViewModeValues(self.TOP_VIEW_MODES[idx])
        imgui.same_line()
        if imgui.button("Apply", (60, 0)):
            self._onTopViewApply()
        mode = self.TOP_VIEW_MODES[self._viewModeIdx]
        fields = self.TOP_VIEW_MODE_FIELDS.get(mode, ())
        if "n" in fields:
            imgui.set_next_item_width(70)
            changed, self._viewN = imgui.input_int("N", self._viewN, 0)
            imgui.same_line()
        if "n2" in fields:
            imgui.set_next_item_width(70)
            changed, self._viewN2 = imgui.input_int("N2", self._viewN2, 0)
        imgui.end_child()

    def _renderTopBox2(self):
        imgui.begin_child("topPanelGroupBox2", size=(340, -1), child_flags=imgui.ChildFlags_.borders.value)
        imgui.text("Pan Controls")
        imgui.same_line()
        imgui.set_next_item_width(150)
        changed, idx = imgui.combo("##panMode", self._panModeIdx, self.PAN_MODES)
        if changed:
            self._panModeIdx = idx
        if self.PAN_MODES[self._panModeIdx] == "UserDefined":
            imgui.same_line()
            imgui.set_next_item_width(70)
            changed, self._panStep = imgui.input_int("##panStep", self._panStep, 0)
        if imgui.button("|< Basa", (70, 0)):
            self._doPan("start")
        imgui.same_line()
        if imgui.button("<< Sol", (70, 0)):
            self._doPan("left")
        imgui.same_line()
        if imgui.button("Sag >>", (70, 0)):
            self._doPan("right")
        imgui.same_line()
        if imgui.button("Son >|", (70, 0)):
            self._doPan("end")
        imgui.set_cursor_pos_y(imgui.get_cursor_pos_y() + 8.0)
        imgui.text(self._panStatusText)
        imgui.end_child()

    def _renderTopBox3(self):
        imgui.begin_child("topPanelGroupBox3", size=(330, -1), child_flags=imgui.ChildFlags_.borders.value)
        if imgui.button("Read Params (src)", (140, 0)):
            self._onReadSrcParams()
        imgui.same_line()
        if imgui.button("Apply Params (dst)", (140, 0)):
            self._onApplySrcParams()
        if imgui.button("Adjust Y Axis (src)", (140, 0)):
            self._onAdjustYAxisSrc()
        imgui.same_line()
        if imgui.button("Adjust Y Axis (all)", (140, 0)):
            self._onAdjustYAxisAll()
        if imgui.button("Reset Src", (140, 0)):
            self._onResetViewSrc()
        imgui.same_line()
        if imgui.button("Adjust X Axes All", (140, 0)):
            self._onAdjustXAxisAll()
        if imgui.button("Reset All", (140, 0)):
            self._onResetViewAll()
        imgui.same_line()
        if imgui.button("Adjust All", (140, 0)):
            self._onAdjustAllAxes()
        imgui.end_child()

    def _renderTopBox5(self):
        imgui.begin_child("topPanelGroupBox5", size=(330, -1), child_flags=imgui.ChildFlags_.borders.value)
        imgui.text("Zoom Controls")
        imgui.same_line()
        imgui.set_next_item_width(90)
        changed, idx = imgui.combo("##zoomRatio", self._zoomRatioIdx, self.ZOOM_RATIOS)
        if changed:
            self._zoomRatioIdx = idx
        imgui.same_line()
        imgui.text(self._zoomStatusText)
        if imgui.button("Zoom In X", (95, 0)):
            self._doZoom("x", "in")
        imgui.same_line()
        if imgui.button("Zoom In Y", (95, 0)):
            self._doZoom("y", "in")
        imgui.same_line()
        if imgui.button("Zoom In XY", (95, 0)):
            self._doZoom("xy", "in")
        if imgui.button("Zoom Out X", (95, 0)):
            self._doZoom("x", "out")
        imgui.same_line()
        if imgui.button("Zoom Out Y", (95, 0)):
            self._doZoom("y", "out")
        imgui.same_line()
        if imgui.button("Zoom Out XY", (95, 0)):
            self._doZoom("xy", "out")
        if imgui.button("Reset X", (95, 0)):
            self._resetZoom("x")
        imgui.same_line()
        if imgui.button("Reset Y", (95, 0)):
            self._resetZoom("y")
        imgui.same_line()
        if imgui.button("Reset XY", (95, 0)):
            self._resetZoom("xy")
        imgui.end_child()

    def _renderTopBox4(self):
        imgui.begin_child("topPanelGroupBox4", size=(320, -1), child_flags=imgui.ChildFlags_.borders.value)
        changed, self._showSliderRange = imgui.checkbox("Show SliderRange", self._showSliderRange)
        if changed:
            self.rangeSliderBar.setSliderVisible(self._showSliderRange)
        imgui.same_line()
        imgui.text("CrossHair :")
        imgui.same_line()
        imgui.set_next_item_width(70)
        changed, idx = imgui.combo("##crossHairMode", self._crossHairModeIdx, self.CROSSHAIR_MODES)
        if changed:
            self._crossHairModeIdx = idx
            self.panelManager.setCrossHairMode(self.CROSSHAIR_MODES[idx].lower())

        changed, self._showScrollBar = imgui.checkbox("Show ScrollBar", self._showScrollBar)
        if changed:
            self.rangeSliderBar.setScrollbarVisible(self._showScrollBar)

        changed, self._showInfoPanels = imgui.checkbox("Show InfoPanel(s)", self._showInfoPanels)
        if changed:
            self.panelManager.setInfoPanelMode("always" if self._showInfoPanels else "hidden")
        imgui.same_line()
        imgui.set_cursor_pos_x(imgui.get_cursor_pos_x() + 8.0)
        changed, self._autoSyncXEnabled = imgui.checkbox("Auto Sync X", self._autoSyncXEnabled)

        changed, self._showBarNumbers = imgui.checkbox("Show BarNumbers", self._showBarNumbers)
        if changed:
            self.panelManager.setXAxisMode("bar" if self._showBarNumbers else "datetime")
        imgui.same_line()
        imgui.set_cursor_pos_x(imgui.get_cursor_pos_x() + 2.0)
        changed, self._autoSyncYEnabled = imgui.checkbox("Auto Sync Y", self._autoSyncYEnabled)

        changed, self._showOhlc = imgui.checkbox("Show OHLC", self._showOhlc)
        if changed:
            self.tsr.setActiveOhlcVisible(self._showOhlc)

        changed, self._showPlotCaptions = imgui.checkbox("Show Plot Captions", self._showPlotCaptions)
        if changed:
            self.panelManager.setShowCaptions(self._showPlotCaptions)

        if imgui.button("Script", (55, 0)):
            self.scriptPanel.setVisible(not self.scriptPanel.getVisible())
        imgui.same_line()
        if imgui.button("Data", (55, 0)):
            self.dataManager.toggle()
        imgui.same_line()
        if imgui.button("Pool", (55, 0)):
            self.poolPanel.toggle()
        imgui.same_line()
        if imgui.button("Console", (55, 0)):
            self.consolePanel.toggle()
        imgui.same_line()
        if imgui.button("Theme", (55, 0)):
            self.toggleTheme()
        imgui.end_child()

    def _renderBottomPanel(self):
        imgui.push_style_var(imgui.StyleVar_.window_padding.value, self.PANEL_PADDING)
        imgui.begin_child("bottomPanel", size=(-1, -1), child_flags=imgui.ChildFlags_.borders.value)
        imgui.text(self._statusText)
        imgui.end_child()
        imgui.pop_style_var()

    # ------------------------------------------------------------- callback'ler
    # (DPG orijinalindeki _on*/​_do* metodlarinin AYNISI - sadece dpg.get_value(tag)
    # okumalari yukardaki self._* state alanlarina, dpg.set_value("bottom_status_text",..)
    # yazmalari self._statusText'e donustu.)

    def _setStatusText(self, text):
        self._statusText = text

    def _onReadSrcParams(self):
        params = self.panelManager.readPanelPlotParams()
        if not params:
            self._setStatusText("Read Params: no active panel")
            return
        self._setStatusText(f"Read Params: panel {params['panelId']} x={params['xAxisLimits']} y={params['yAxisLimits']}")

    def _onApplySrcParams(self):
        if not self.panelManager.getLastReadPlotParams():
            self._setStatusText("Apply Params: read source params first")
            return
        pending = self.interactionManager.scheduleSyncOthers()
        self._setStatusText(f"Apply Params: pending {pending} panel(s)")

    def _onAdjustYAxisSrc(self):
        panelId = self.panelManager.getActivePanelId()
        ok = self.panelManager.adjustYAxis(panelId)
        self._setStatusText(f"Adjusted Y: panel {panelId}" if ok else "Adjusted Y: no active panel")

    def _onAdjustYAxisAll(self, updateStatus=True):
        count = self.panelManager.adjustAllYAxes()
        if updateStatus:
            self._setStatusText(f"Adjusted Y: {count} panels")

    def _onResetViewSrc(self):
        panelId = self.panelManager.getActivePanelId()
        result = self.panelManager.applyViewMode(panelId, self.TOP_VIEW_MODES[self._viewModeIdx],
                                                  n=self._viewN, n2=self._viewN2)
        if result is None:
            self._setStatusText("Reset View: no active panel")
            return
        self.rangeSliderBar.syncScrollToView(panelId)
        self._updateViewStatusText(panelId, result)

    def _onResetViewAll(self):
        count = self.panelManager.applyViewModeToAllPanels(self.TOP_VIEW_MODES[self._viewModeIdx],
                                                            n=self._viewN, n2=self._viewN2)
        self._setStatusText(f"Reset View: {count} panels ({self.TOP_VIEW_MODES[self._viewModeIdx]})")

    def _onAdjustXAxisAll(self, updateStatus=True):
        # X senkronizasyonunun sabit referansi OHLC/candle panelidir; son
        # tiklanan Signals/PnL paneli Range Navigator kaynagini degistirmez.
        sourcePanelId = self.rangeSliderBar.getSourcePanelId()
        params = self.panelManager.readPanelPlotParams(sourcePanelId)
        if not params or not params.get("xAxisLimits"):
            if updateStatus:
                self._setStatusText("Adjust X: no active source X")
            return
        pending = self.interactionManager.scheduleSyncOthers(params, mode="x")
        if updateStatus:
            self._setStatusText(f"Adjust X: pending {pending} panel(s)")

    def _onAdjustAllAxes(self):
        panelId = self.panelManager.getActivePanelId()
        self.panelManager.adjustYAxis(panelId)
        params = self.panelManager.readPanelPlotParams(panelId)
        if not params or not params.get("xAxisLimits"):
            self._setStatusText("Adjust All: no active source X")
            return
        pending = self.interactionManager.scheduleSyncOthers(params)
        self._setStatusText(f"Adjust All: pending {pending} panel(s)")

    def _onTopViewApply(self):
        panelId = self.panelManager.getActivePanelId()
        mode = self.TOP_VIEW_MODES[self._viewModeIdx]
        result = self.panelManager.applyViewMode(panelId, mode, n=self._viewN, n2=self._viewN2)
        if result is None:
            self._setStatusText("View: no active panel/data")
            return
        self.panelManager.setDefaultViewMode(mode, self._viewN, self._viewN2)
        self.rangeSliderBar.syncScrollToView(panelId)
        self._updateViewStatusText(panelId, result)

    def _doPan(self, direction):
        # Range Navigator ve Auto Sync X gibi Pan Controls da sabit olarak
        # OHLC/candle panelini kaynak alir. Baska bir panele tiklamak pan
        # kaynagini degistirmemelidir.
        panelId = self.rangeSliderBar.getSourcePanelId()
        result = self.panelManager.panPanel(panelId, direction=direction,
                                            mode=self.PAN_MODES[self._panModeIdx], step=self._panStep)
        if result is None:
            self._panStatusText = "Pan: no OHLC panel"
            return
        # X override bir sonraki frame'de uygulanir; Y hesabinda cache'teki
        # eski X limitlerini degil panPanel'in yeni dondurdugu araligi kullan.
        self.panelManager.adjustAllYAxesForXRange(result[0], result[1])
        self.rangeSliderBar.syncScrollToView(panelId)
        self._panStatusText = self._formatViewStatusText(panelId, result)

    def _onRangeNavigatorViewChanged(self, panelId, xLimits):
        self._panStatusText = self._formatViewStatusText(panelId, xLimits)

    def _doZoom(self, axes, direction):
        panelId = self.panelManager.getActivePanelId()
        self._ensureZoomState(panelId)
        ratio = float(self.ZOOM_RATIOS[self._zoomRatioIdx].rstrip("%")) / 100.0
        result = self.panelManager.zoomPanel(panelId, axes=axes, direction=direction, ratio=ratio)
        if result is None:
            self._setStatusText("Zoom: no active panel")
            return
        self._incrementZoomCounters(panelId, axes, direction)
        self._updateZoomStatusLabel(panelId)
        if "x" in result:
            self.rangeSliderBar.syncScrollToView(panelId)
            self._updateViewStatusText(panelId, result["x"])
        else:
            self._setStatusText(f"Zoom {axes.upper()} {direction}: panel {panelId}")

    def _resetZoom(self, axes):
        panelId = self.panelManager.getActivePanelId()
        state = self._zoomStates.get(panelId)
        if not state:
            self._setStatusText("Reset Zoom: no zoom baseline")
            self._updateZoomStatusLabel(panelId)
            return
        xLimits = state.get("x0") if "x" in axes else None
        yLimits = state.get("y0") if "y" in axes else None
        result = self.panelManager.setPanelAxisLimits(panelId, xLimits=xLimits, yLimits=yLimits)
        if result is None:
            self._setStatusText("Reset Zoom: no active panel")
            return
        if "x" in axes:
            state["xCount"] = 0
        if "y" in axes:
            state["yCount"] = 0
        self._updateZoomStatusLabel(panelId)
        if "x" in result:
            self.rangeSliderBar.syncScrollToView(panelId)
            self._updateViewStatusText(panelId, result["x"])
        else:
            self._setStatusText(f"Reset Zoom {axes.upper()}: panel {panelId}")

    def _ensureZoomState(self, panelId):
        if panelId in self._zoomStates:
            return self._zoomStates[panelId]
        self._zoomStates[panelId] = {
            "x0": self.panelManager.getXAxisLimits(panelId),
            "y0": self.panelManager.getYAxisLimits(panelId),
            "xCount": 0, "yCount": 0,
        }
        return self._zoomStates[panelId]

    def _incrementZoomCounters(self, panelId, axes, direction):
        state = self._ensureZoomState(panelId)
        delta = 1 if direction == "in" else -1
        if "x" in axes:
            state["xCount"] += delta
        if "y" in axes:
            state["yCount"] += delta

    def _updateZoomStatusLabel(self, panelId=None):
        panelId = self.panelManager.getActivePanelId() if panelId is None else panelId
        state = self._zoomStates.get(panelId)
        xCount = state.get("xCount", 0) if state else 0
        yCount = state.get("yCount", 0) if state else 0
        self._zoomStatusText = f"({xCount},{yCount})"

    def _updateViewStatusText(self, panelId, xLimits):
        self._setStatusText(self._formatViewStatusText(panelId, xLimits))

    def _formatViewStatusText(self, panelId, xLimits):
        xMin, xMax = xLimits
        barCount = self.panelManager.getPanelDataCount(panelId)
        lastIndex = max(0, barCount - 1)
        offset = min(lastIndex, max(0, round(xMin))) if barCount > 0 else max(0, round(xMin))
        end = min(lastIndex, max(0, round(xMax))) if barCount > 0 else round(xMax)
        visibleCount = max(0, end - offset)
        movableDistance = max(0, lastIndex - visibleCount)
        progressPct = (offset / movableDistance * 100.0) if movableDistance > 0 else 0.0
        progressPct = max(0.0, min(100.0, progressPct))
        return f"{barCount} / [{offset}-{end}] / [{progressPct:.2f}%] / {visibleCount}"

    def _saveTopViewModeValues(self, mode):
        fields = self.TOP_VIEW_MODE_FIELDS.get(mode, ())
        if not fields:
            return
        values = self._topViewModeValues.setdefault(mode, {})
        if "n" in fields:
            values["n"] = self._viewN
        if "n2" in fields:
            values["n2"] = self._viewN2

    def _restoreTopViewModeValues(self, mode):
        values = self._topViewModeValues.get(mode)
        if not values:
            return
        if "n" in values:
            self._viewN = values["n"]
        if "n2" in values:
            self._viewN2 = values["n2"]

    def seedTopViewRangeInputs(self, barCount):
        """scripts/default.py bir bundle yukledikten SONRA bir kez cagirir -
        Last/First N -> 1000, Range -> 0..barCount sensible varsayilanlarini
        yazar (bkz. DPG orijinalindeki ayni isimli metod)."""
        barCount = int(barCount or 0)
        self._topViewModeValues = {
            "Last N Data": {"n": 1000},
            "First N Data": {"n": 1000},
            "Range": {"n": 0, "n2": barCount},
        }
        self._restoreTopViewModeValues(self.TOP_VIEW_MODES[self._viewModeIdx])

    def _refreshActivePanelCombo(self):
        """DPG orijinalinde bir combo widget'inin item listesini/degerini
        guncelliyordu - burada aktif panel etiketi HER FRAME dogrudan
        _renderTopBox1 icinde canli hesaplaniyor, ayri bir 'refresh' adimina
        gerek yok (immediate-mode). scripts/default.py bunu cagirir, no-op."""
        pass

    def _updateAutoSyncX(self):
        if not self._autoSyncXEnabled:
            return
        now = time.time()
        if now - self._lastAutoSyncXTime < self._autoSyncIntervalSec:
            return
        self._lastAutoSyncXTime = now
        self._onAdjustXAxisAll(updateStatus=False)

    def _updateAutoSyncY(self):
        if not self._autoSyncYEnabled:
            return
        now = time.time()
        if now - self._lastAutoSyncYTime < self._autoSyncIntervalSec:
            return
        self._lastAutoSyncYTime = now
        self._onAdjustYAxisAll(updateStatus=False)
