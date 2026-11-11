"""PanelManager — DearPyGuiDataPlotter'daki panelManager.py'nin imgui_bundle/implot portu.

Bu bir BACKEND SWAP'tir (DearPyGui -> imgui_bundle/implot), yeniden tasarim degil - tum public
method isimleri/imzalari/davranislari DPG orijinaliyle AYNI tutulmaya calisildi. Asagidaki
noktalar DPG (retained-mode) ile implot (immediate-mode) arasindaki GERCEK API farklarindan
dogan, kacinilmaz uyarlamalardir (tasarim tercihi degil):

1) Immediate-mode: DPG'de "_drawOrUpdateSeries" bir item VARSA set_value ile guncelliyordu,
   YOKSA olusturuyordu (retained). implot'ta persistent seri objesi YOK - her panel HER FRAME
   kendi begin_plot/end_plot bloğu icinde PanelData.getLodSlice/getLodXY'den okunan guncel LOD
   dilimiyle yeniden cizilir. drawPanel()/drawPanelData() artik "cizmiyor", sadece paneli
   render() dongusune dahil ediyor / "fresh load" bayragini tetikliyor - gercek cizim HER FRAME
   render() icinde olur.

2) DPG'nin panelleri implot subplot IZGARASI DEGIL - her panel KENDI child_window'u icinde
   KENDI begin_plot/end_plot'una sahip, _panelOrder'a gore dikey olarak ISTIFLENMIS bagimsiz
   plot'lar (bkz. orijinal _buildPanelUi: panel_{id} child_window -> plot_{id}). Bu port da
   AYNI yapiyi (implot.begin_subplots KULLANMADAN, bagimsiz begin_plot'lar) korur - subplot
   izgarasi PythonPlotter'a ozgu bir teknik, burada kullanilmiyor.

3) Eksen limiti kilitleme: DPG'nin "set_axis_limits + split_frame + set_axis_limits_auto"
   (bir frame kilitle, sonra kilidi ac) dansi, implot'ta YOK/GEREKSIZ - implot zaten axis
   durumunu kendi icinde (plot ID bazinda) saklar; `setup_axis_limits(axis,min,max,Cond_.once)`
   bir KERE uygulanir, sonrasinda kullanici serbestce pan/zoom yapar. Bu yuzden DPG'nin
   lock/unlock dansi yapan her metod (panPanel/zoomPanel/setPanelAxisLimits/applyViewMode/
   resetPanelView/adjustYAxis/applySourceParamsToPanel/applySourceXAxisToPanel/cift-tikla-repad)
   yerine `_pendingAxisOverride[panelId][axis] = (min,max,sticky)` yaziyor: sticky=False ->
   bu FRAME `Cond_.always` ile uygulanip hemen dusurulur (DPG'nin bir-kerelik set+unlock'uyla
   AYNI sonuc); sticky=True -> `unlockXAxis()` cagrilana kadar HER FRAME `Cond_.always` ile
   zorlanir (DPG'nin "drag sirasinda set_axis_limits_auto hic cagirma" liveOnly davranisiyla
   AYNI).

4) Eksen limiti OKUMA: DPG'nin `get_axis_limits(tag)`'i her an cagirilabilirdi (retained item).
   implot'ta `get_plot_limits()` SADECE o plot'un kendi begin_plot/end_plot'u icindeyken
   gecerli. Bu yuzden her panelin GERCEK limitleri HER FRAME begin_plot'tan hemen sonra
   `_lastAxisLimits[panelId]`'e onbelleklenir; tum getter'lar (getXAxisLimits/getYAxisLimits/
   readPanelPlotParams/pan/zoom'un "mevcut limiti oku" adimi) bu onbellekten okur - "bir onceki
   TAMAMLANMIS frame'in durumu" (buton callback'leri zaten frame'ler ARASI calistigi icin bu
   fark pratikte gorulmez).

5) Plot piksel genisligi: DPG'nin `get_item_rect_size(plotTag)`'i -> implot plot'u icin
   begin_plot hemen SONRASI `imgui.get_item_rect_size()` ile okunup `_lastPlotRect[panelId]`'e
   onbelleklenir (ayni 800/900 fallback degerleri korunur).

6) Hover/crosshair/info-panel/mouse-pos/aktif-panel: DPG'de render() SONRASI ayri pass'ler
   (updateInfoOverlays/updateMousePosOverlays/updateCrossHairOverlays/updateActivePanel) HER
   paneli `dpg.is_item_hovered`/`dpg.get_plot_mouse_pos` ile ayri ayri sorgulayabiliyordu
   (retained item, her an sorgulanabilir). implot'ta hover/mouse-pos SADECE o plot acikken
   sorgulanabilir - bu yuzden hover durumu HER panelin begin_plot/end_plot'u icinde YAKALANIR
   (`_hoverState`), bir frame SONRA (bir onceki TAMAMLANMIS frame'in `_prevHoverState`'i)
   karar/metin-cizim mantigi icin kullanilir (bir frame gecikme, gozle fark edilmez - eksen
   onbellegiyle AYNI numara). Asil karar mantigi (mode'lar: hidden/hover/active/always,
   crosshair hidden/single/all, persist, shared index, _buildHoverLabel metin kurulumu) SIFIR
   dpg cagrisi iceriyordu zaten (saf Python/veri mantigi) - DEGISTIRILMEDEN tasindi, sadece
   metnin CIZIMI (dpg.set_value+set_item_pos+show_item) yerine panel hala acikken
   `imgui.get_window_draw_list().add_text(...)` kullanildi.

   Crosshair cizgileri: DPG'nin "hep var ama gizli" add_drag_line'i -> implot'ta immediate-mode
   oldugu icin "gizleme" yok, o FRAME gosterilmeyecekse cagrisi hic YAPILMAZ.

7) Havuz (pool) drag-drop entegrasyonu immediate-mode API'ye uyarlandi: PoolPanel
   string item id'lerini frame-yerel integer payload id'lerine cevirir; her panelin
   ImPlot item'i her frame drop target olur. Drop sonrasi DPG portuyla ayni sekilde
   drawPanelData() cagrilarak seri/LOD durumu ve eksen limitleri yenilenir.
"""

import math
import time

import numpy as np
from imgui_bundle import imgui, implot

from .panel import Panel


class PanelManager:
    COLLAPSED_PANEL_HEIGHT = 42
    LOD_ACTIVATION_THRESHOLD = 6000
    LOD_MAX_POINTS = 3000
    OVERVIEW_LOD_MAX_POINTS = 800
    LOD_RANGE_CHANGE_RATIO = 0.08
    CANDLE_HALF_WIDTH = 0.3  # bar-index biriminde govde yarim genisligi (implot'ta native candle yok - elle cizim, bkz. _drawCandleBar)

    def __init__(self):
        self._panels = {}
        self._nextId = 0
        self._panelOrder = []
        self._container = None  # artik yeniden-parent'lama icin degil, sadece bilgi/uyumluluk icin saklanir (bkz. setContainer)
        self._showCaptions = True
        self._infoPanelYWithCaptions = 40
        self._infoPanelYNoCaptions = 25
        self._mousePosYWithCaptions = 40
        self._mousePosYNoCaptions = 13
        self._infoPanelBaseX = 20
        self._drawnPanelIds = set()
        self._lastRenderPrint = 0.0
        self._xAxisMode = "bar"
        self._dateTimeFormat = None
        self._lastAxisTicksSignature = {}
        self._minTickStep = 7
        self._maxTicksOnScreen = 40
        self._axisCharPxWidth = 9
        self._axisTickPadding = 24
        self._dayChangeFormat = "%d.%m.%Y\n%H:%M:%S"
        self._dayBoundaryScanCap = 5000
        self._dayChangeMarkersEnabled = False
        self._debugAxisTicks = False
        self._infoPanelMode = "always"
        self._infoActivePanelId = None
        self._infoLastIndex = {}
        self._infoSharedIndex = None
        self._crossHairMode = "all"
        self._crossHairPersist = True
        self._crossHairLastPos = None
        self._activeUpdateMode = "click"
        self._activePanelId = None
        self._interactionManager = None
        self._poolDropHandler = None
        self._lastReadPlotParams = None
        self._defaultViewMode = "FitToScreen (Ultra)"
        self._defaultViewN = 1000
        self._defaultViewN2 = 2000
        self._fitToScreenBarWidth = {"normal": 4.0, "wide": 2.5, "ultra": 1.5}
        self._lastDrawnDataCount = {}
        self._lodLastRange = {}

        # --- implot backend'ine ozgu durum (DPG'nin retained item/axis durumunun karsiligi) ---
        self._pendingAxisOverride = {}  # {panelId: {"x": (min,max,sticky), "y": (min,max,sticky)}}
        self._autoFitAxes = {}  # {panelId: {"x": True, "y": True}} - veri yoksa surekli auto-fit (DPG'nin set_axis_limits_auto'suna karsilik)
        self._fitAxesNextFrame = {}  # legend visibility degisimi gibi tek-frame fit istekleri
        self._legendHiddenData = set()  # {(panelId, dataId)} ImPlot legend'de kapatilan seriler
        self._lastAxisLimits = {}  # {panelId: {"x": (min,max), "y": (min,max)}}
        self._lastPlotRect = {}  # {panelId: (width,height)} - begin_plot sonrasi imgui.get_item_rect_size()
        self._lastPlotScreenRect = {}  # {panelId: (x,y,width,height)} - gercek ImPlot cizim alani
        self._lastPanelRect = {}  # {panelId: (posX,posY,width,height)} - panel_{id} child_window'unun ekran dikdortgeni
        self._hoverState = None  # BU frame icinde yakalanan hover - {"panelId","mx","my"}
        self._prevHoverState = None  # ONCEKI TAMAMLANMIS frame'in hover'i - overlay karar/cizim pass'i bunu okur
        self._panelDrawHooks = {}  # {panelId: [callback(pm, panelId, panel), ...]} - TradeSignalRenderer gibi ek cizim katmanlari icin (bkz. addPanelDrawHook)
        self._rangeMeasureTool = None  # bkz. setRangeMeasureTool - A/B olcum cizgileri tum panellerde cizilir
        self._tradeSignalRenderer = None  # bkz. setTradeSignalRenderer - candle cizim dongusu her frame bunun isColorBarsActive/renderInPlot'unu cagirir
        self._detachedPanelIds = set()
        self._detachedInitialSizePending = set()
        self._panelHeightRestorePending = {}

    def setInteractionManager(self, interactionManager):
        self._interactionManager = interactionManager

    def setTradeSignalRenderer(self, tradeSignalRenderer):
        """GuiManager/script tarafindan BIR KERE baglanir (DPG'deki 'tsr' global'inin
        PanelManager tarafindan tanindigi karsiligi). Baglandiktan sonra her candle
        panelinin cizim dongusu HER FRAME tradeSignalRenderer.isColorBarsActive(panelId,
        dataId) ile varsayilan bull/bear cizimini atlayip atlamayacagina bakar, sonra
        tradeSignalRenderer.renderInPlot(panelId, dataId, xMin, xMax) cagirir (o
        (panelId,dataId) icin draw() hic cagrilmadiysa renderInPlot zaten no-op)."""
        self._tradeSignalRenderer = tradeSignalRenderer

    def getPanelHoverInfo(self, panelId):
        """InteractionManager'in implot disindan (begin_plot/end_plot kapaliyken)
        okuyabilmesi icin onceki TAMAMLANMIS frame'in hover/rect/eksen durumunu
        dondurur - implot'un kendi is_plot_hovered()/get_plot_mouse_pos()/
        get_plot_limits() cagrilari SADECE o plot acikken calisir, bu yuzden
        PanelManager kendi render() dongusunde her panel icin bunlari ONBELLEKLER
        (bkz. _lastPanelRect/_lastPlotRect/_lastAxisLimits/_prevHoverState)."""
        prevHover = self._prevHoverState
        hovered = bool(prevHover and prevHover.get("panelId") == panelId)
        rectPos = self._lastPanelRect.get(panelId)
        rectMin = (rectPos[0], rectPos[1]) if rectPos else None
        rectMax = (rectPos[0] + rectPos[2], rectPos[1] + rectPos[3]) if rectPos else None
        limits = self._lastAxisLimits.get(panelId, {})
        return {
            "hovered": hovered,
            "mouseX": prevHover.get("mx") if hovered else None,
            "mouseY": prevHover.get("my") if hovered else None,
            "rectMin": rectMin,
            "rectMax": rectMax,
            "xAxisLimits": limits.get("x"),
            "yAxisLimits": limits.get("y"),
        }

    def setPoolDropHandler(self, handler):
        """PoolPanel'den gelen drop'u uygulama katmanina ileten handler'i baglar."""
        self._poolDropHandler = handler

    # ------------------------------------------------------- ek cizim hook'lari
    def addPanelDrawHook(self, panelId, callback):
        """Bir panelin begin_plot/end_plot'u ACIKKEN, ana seri/level cizimden SONRA
        HER FRAME cagrilacak ek bir cizim fonksiyonu kaydeder: callback(self, panelId, panel).
        DPG orijinalinde YOKTU (DPG'de TradeSignalRenderer tek seferlik retained
        dpg.add_candle_series cagirip kalici birakiyordu) - immediate-mode'da "tek seferlik
        cizip kalici birakma" diye bir sey olmadigi icin, HER FRAME yeniden cizilmesi gereken
        ek katmanlar (orn. TradeSignalRenderer'in renkli run'lari) icin bu minimal, genel
        hook GEREKLI bir uyarlama (DPG mimarisine ait degil, backend farkinin dogal sonucu)."""
        self._panelDrawHooks.setdefault(panelId, []).append(callback)

    def removePanelDrawHooks(self, panelId):
        self._panelDrawHooks.pop(panelId, None)

    def setRangeMeasureTool(self, tool):
        """A/B aralik olcum aracini baglar (bkz. rangeMeasureTool.py). Hook'larin
        aksine panel bazinda degil, TUM paneller icin her frame cizilir ve
        deleteAllPanels'te silinmez (sadece reset edilir)."""
        self._rangeMeasureTool = tool

    def getRangeMeasureTool(self):
        return self._rangeMeasureTool

    def isAnyPlotHovered(self):
        """Onceki tamamlanmis frame'de fare herhangi bir panelin plot alaninin
        uzerinde miydi (klavye kisayollari sadece o zaman calisir)."""
        return self._prevHoverState is not None

    def createPanel(self, name, caption="", parent="", alignment=None):
        panelId = self._nextId
        self._nextId += 1
        return Panel(panelId, name, caption, parent, alignment)

    def addPanel(self, panel):
        self._panels[panel.id] = panel
        panel._manager = self
        if panel.id >= self._nextId:
            self._nextId = panel.id + 1
        if panel.id not in self._panelOrder:
            self._panelOrder.append(panel.id)
        return panel

    def getPanel(self, panelId):
        return self._panels.get(panelId)

    def getPanelId(self, name):
        panel = self.findPanel(name)
        return panel.id if panel else None

    def findPanel(self, key):
        if isinstance(key, str):
            return next((p for p in self._panels.values() if p.name == key), None)
        return self._panels.get(key)

    def deletePanel(self, panelId):
        panel = self._panels.get(panelId)
        if panel is None:
            return
        panel.deleteAllData()
        self._deletePanelUi(panelId)
        del self._panels[panelId]
        if panelId in self._panelOrder:
            self._panelOrder.remove(panelId)
        if self._interactionManager is not None:
            self._interactionManager.unregisterPanel(panelId)
        self._lastDrawnDataCount.pop(panelId, None)
        self._pendingAxisOverride.pop(panelId, None)
        self._autoFitAxes.pop(panelId, None)
        self._lastAxisLimits.pop(panelId, None)
        self._lastPlotRect.pop(panelId, None)
        self._lastPlotScreenRect.pop(panelId, None)
        self._lastPanelRect.pop(panelId, None)
        self._lodLastRange.pop(panelId, None)
        self.removePanelDrawHooks(panelId)
        self._detachedPanelIds.discard(panelId)
        self._detachedInitialSizePending.discard(panelId)
        self._panelHeightRestorePending.pop(panelId, None)
        if self._activePanelId == panelId:
            self._activePanelId = None

    def removePanel(self, panelId):
        self.deletePanel(panelId)

    def getAllPanels(self):
        return list(self._panels.values())

    def iterateAllPanels(self):
        for panel in self._panels.values():
            yield panel

    def deleteAllPanels(self):
        for panelId, panel in list(self._panels.items()):
            panel.deleteAllData()
            self._deletePanelUi(panelId)
        if self._interactionManager is not None:
            for panelId in self._panels.keys():
                self._interactionManager.unregisterPanel(panelId)
        self._panels.clear()
        self._panelOrder.clear()
        self._nextId = 0
        self._lastDrawnDataCount.clear()
        self._activePanelId = None
        self._lodLastRange.clear()
        self._pendingAxisOverride.clear()
        self._autoFitAxes.clear()
        self._fitAxesNextFrame.clear()
        self._legendHiddenData.clear()
        self._lastAxisLimits.clear()
        self._lastPlotRect.clear()
        self._lastPlotScreenRect.clear()
        self._lastPanelRect.clear()
        self._panelDrawHooks.clear()
        if self._rangeMeasureTool is not None:
            self._rangeMeasureTool.reset()
        self._detachedPanelIds.clear()
        self._detachedInitialSizePending.clear()
        self._panelHeightRestorePending.clear()

    def _deletePanelUi(self, panelId):
        self._drawnPanelIds.discard(panelId)

    # -------------------------------------------------------- panel sirasi
    def getPanelOrder(self):
        return list(self._panelOrder)

    def swapPanelUp(self, panelId):
        if panelId not in self._panelOrder:
            return
        idx = self._panelOrder.index(panelId)
        if idx > 0:
            self._panelOrder[idx], self._panelOrder[idx - 1] = \
                self._panelOrder[idx - 1], self._panelOrder[idx]

    def swapPanelDown(self, panelId):
        if panelId not in self._panelOrder:
            return
        idx = self._panelOrder.index(panelId)
        if idx < len(self._panelOrder) - 1:
            self._panelOrder[idx], self._panelOrder[idx + 1] = \
                self._panelOrder[idx + 1], self._panelOrder[idx]

    def applyPanelOrder(self):
        """DPG'de dpg.move_item ile container icinde yeniden diziliyordu - implot'ta
        cizim sirasi dogrudan render()'in _panelOrder'i DOLASMA sirasi oldugu icin
        baska bir seye gerek yok (no-op, API uyumlulugu icin duruyor)."""
        return

    def resetPanelOrder(self):
        self._panelOrder = list(self._panels.keys())
        self.applyPanelOrder()

    # ----------------------------------------------------------------- cizim
    def setContainer(self, tag):
        """DPG'de panellerin push_container_stack ile icine eklendigi parent tag'i.
        implot'ta reparent etme kavrami yok - cizim tamamen render()'in CAGRILDIGI
        noktadaki imgui layout akisina baglidir (GuiManager render() cagrisini
        centerCenterPanel esdegeri child_window'un icinde yapar). Sadece bilgi/
        uyumluluk icin saklanir."""
        self._container = tag

    def getContainer(self):
        return self._container

    def setShowCaptions(self, visible: bool):
        self._showCaptions = bool(visible)

    def detachPanel(self, panelId):
        if panelId not in self._panels:
            return False
        self._detachedPanelIds.add(panelId)
        self._detachedInitialSizePending.add(panelId)
        return True

    def attachPanel(self, panelId):
        self._detachedPanelIds.discard(panelId)
        self._detachedInitialSizePending.discard(panelId)
        return panelId in self._panels

    def isPanelDetached(self, panelId):
        return panelId in self._detachedPanelIds

    # ------------------------------------------------------------------ pool
    # DIKKAT: PAYLOAD_TYPE degeri PoolPanel.PAYLOAD_TYPE ile AYNI olmali -
    # circular import'tan kacinmak icin burada string sabit tekrarlanir
    # (poolPanel.py zaten panelManager.py'yi import etmiyor, tersi de
    # olmamali - iki dosya SADECE bu string sozlesmesiyle baglanir).
    POOL_DRAG_PAYLOAD_TYPE = "pool_item"

    def addPoolItemToPanel(self, panelId, poolItem):
        """PoolItem.data'yi (zaten pool'a eklenirken KLONLANMIS bir PanelData)
        hedef panele BAGIMSIZ bir ikinci klon olarak ekler - boylece ayni pool
        item'i birden fazla panele surukleyip birbirinden bagimsiz
        gizleyebilir/silebilirsin. DPG orijinaliyle AYNI mantik (bkz.
        poolDataManager.clonePanelData)."""
        from .poolDataManager import clonePanelData
        panel = self._panels.get(panelId)
        if panel is None or poolItem is None or poolItem.data is None:
            return None
        dataId = self._nextDataId(panel)
        name = self._uniqueDataName(panel, poolItem.label or poolItem.data.name)
        data = clonePanelData(poolItem.data, dataId=dataId, name=name)
        data.setParent(panel)
        data.setVisible(True)
        panel.dataList.append(data)

        # DearPyGui portuyla ayni drop-sonrasi davranis: DPG
        # addPoolItemToPanel(), plot daha once cizildiyse drawPanelData() cagirir.
        # Immediate-mode'da seri zaten bir sonraki render frame'inde dataList'ten
        # cizilir; yine de drawPanelData() LOD/fresh-load durumunu ve yeni seriyi
        # kapsayacak eksen limitlerini yenilemek icin gereklidir. Boylece Fast MA
        # gibi herhangi bir Pool serisi OHLC ya da baska bir hedef plot'a
        # birakildiginda model ve gorunum birlikte yenilenir.
        if panelId in self._drawnPanelIds:
            self.drawPanelData(panelId)
        return data

    def _handlePoolDrop(self, panelId):
        """Panelin implot.end_plot() cagrisi hemen ardindan calisir; ImGui'nin
        drag-drop target API'siyle PoolPanel'den suruklenen item'i yakalar.
        Immediate-mode'da DPG'nin plot drop_callback'inin karsiligi: her frame
        begin_drag_drop_target()/accept_drag_drop_payload_py_id() ile sorgulanir,
        native bir 'callback kaydi' yoktur."""
        if self._poolDropHandler is None:
            return
        if not imgui.begin_drag_drop_target():
            return
        try:
            payload = imgui.accept_drag_drop_payload_py_id(self.POOL_DRAG_PAYLOAD_TYPE)
            if payload is not None:
                self._poolDropHandler(panelId, payload.data_id)
        finally:
            imgui.end_drag_drop_target()

    def _nextDataId(self, panel):
        used = [int(d.id) for d in panel.dataList if isinstance(d.id, int)]
        return (max(used) + 1) if used else 1

    def _uniqueDataName(self, panel, baseName):
        baseName = baseName or "Pool Data"
        names = {d.name for d in panel.dataList}
        if baseName not in names:
            return baseName
        index = 2
        while f"{baseName} ({index})" in names:
            index += 1
        return f"{baseName} ({index})"

    # ------------------------------------------------------------ draw/data
    def drawPanel(self, panelId):
        """Paneli render() dongusune dahil eder (immediate-mode'da 'kabuk kurmak'
        sadece bir bayrak - gercek cizim HER FRAME render() icinde olur)."""
        panel = self._panels.get(panelId)
        if panel is None:
            return
        self._drawnPanelIds.add(panelId)
        if self._interactionManager is not None:
            self._interactionManager.registerPanel(
                panelId, f"plot_{panelId}", f"x_axis_{panelId}", f"y_axis_{panelId}")

    def drawPanelData(self, panelId):
        """Veri degisikligini isler - immediate-mode'da ayrica 'cizmeye' gerek yok
        (render() zaten panel.dataList'i her frame okur), ama 'fresh load' bayragini
        ve LOD onbellek-iptalini DPG orijinaliyle AYNI semantikle tetikler.

        DPG drawPanelData(), serileri guncelledikten sonra _applyAxisPadding()
        cagirir. Burada kalici seri item'i olmadigi icin yalniz model okunur, fakat
        ayni eksen yenilemesi zorunludur: sonradan eklenen Pool serisi mevcut
        ImPlot Y araliginin disinda kalabilir."""
        panel = self._panels.get(panelId)
        if panel is None:
            return
        self._applyAxisPadding(panelId, panel)
        self._maybeApplyDefaultViewOnLoad(panelId)
        self._lodLastRange.pop(panelId, None)

    # ------------------------------------------------------- candle cizimi
    def _drawCandleBar(self, x, o, h, l, c, color_bull, color_bear, label="##candle", weight=1.0):
        """implot'ta native candlestick YOK (DPG'nin kendi C++'inda da yok, kendi
        primitiflerinden cizili) - fitil + govde elle cizilir. Teknik: fitil
        plot_line, boga (c>=o) govdesi bos dikdortgen outline (plot_line, 5 nokta),
        ayi (c<o) govdesi dolu (plot_shaded). Bu YONTEM src/PythonPlotter/
        data_plotter_img_bundle.py'de (implot'un kendi sinirlamasindan dogan, tek
        calisan referans) zaten kullaniliyor - mimari/tasarim ODUNC alinmiyor,
        sadece ImPlot'un tek pratik candle-cizim PRIMITIFI tekrarlaniyor."""
        hw = self.CANDLE_HALF_WIDTH
        isBull = c >= o
        color = color_bull if isBull else color_bear
        wickXs = np.array([x, x], dtype=np.float64)
        wickYs = np.array([l, h], dtype=np.float64)
        implot.plot_line(label, wickXs, wickYs, spec=implot.Spec(line_color=color, line_weight=weight))
        bodyXs = np.array([x - hw, x + hw, x + hw, x - hw, x - hw], dtype=np.float64)
        bodyYs = np.array([o, o, c, c, o], dtype=np.float64)
        if isBull:
            implot.plot_line("##candle_body", bodyXs, bodyYs, spec=implot.Spec(line_color=color, line_weight=weight))
        else:
            topXs = np.array([x - hw, x + hw], dtype=np.float64)
            topYs = np.array([o, o], dtype=np.float64)
            botYs = np.array([c, c], dtype=np.float64)
            implot.plot_shaded("##candle_body", topXs, topYs, botYs,
                               spec=implot.Spec(fill_color=color, fill_alpha=0.8))

    def _drawCandleSeries(self, panelId, d, xMin, xMax, colorBull, colorBear,
                          denseStates=None, colorBySignal=None, showLegend=False):
        """Bir candle PanelData'yi [xMin,xMax] gorunur araligina gore LOD-decimate
        ederek, bar-bar _drawCandleBar ile cizer - en fazla LOD_MAX_POINTS bar
        (veri seti/gorunur aralik buyuklugunden BAGIMSIZ sabit bir ust sinir).

        denseStates verilirse (TradeSignalRenderer.getDenseStates() - her
        ORIJINAL bar index'i icin tek durum tasiyan numpy dizisi) HER
        decimate-edilmis bar icin kendi orijinal index'inden (xs[i] zaten
        orijinal bar index'idir, bkz. PanelData.getLodSlice/_decimateOhlc)
        O(1) ile durumu okuyup colorBySignal'daki rengi kullanir (colorBars
        ozelligi - bkz. tradeSignalRenderer.py sinif docstring'indeki
        PERFORMANS DUZELTMESI: bu artik AYRI bir cizim gecisi DEGIL, bu ayni
        ZATEN-LOD-sinirli dongu - maliyet run sayisindan/gorunur aralik
        genisliginden bagimsiz, ana candle cizimiyle AYNI sinira tabi).
        Durum yoksa/eslesme yoksa o bar normal bull/bear rengine duser."""
        if not (d.open and d.high and d.low and d.close):
            return
        useLod = d.fullCount > self.LOD_ACTIVATION_THRESHOLD
        slice_ = d.getLodSlice(xMin, xMax, self.LOD_MAX_POINTS) if useLod else None
        if slice_ is not None:
            xs, opens, closes = slice_["xs"], slice_["opens"], slice_["closes"]
            lows, highs = slice_["lows"], slice_["highs"]
        else:
            xs = d.xs if d.xs else list(range(len(d.open)))
            opens, closes, lows, highs = d.open, d.close, d.low, d.high
        denseLen = len(denseStates) if denseStates is not None else 0
        for i in range(len(xs)):
            # Mumlar ImPlot primitive'leriyle tek tek cizildigi icin normalde
            # gizli ##candle etiketi kullanilir. DPG davranisiyla ayni olarak,
            # panelde ikinci bir gorunur seri varsa ilk mum OHLC serisinin
            # legend kaydini olusturur; yalniz OHLC varken legend'e girmez.
            candleLabel = d.name if showLegend and i == 0 else "##candle"
            overrideColor = None
            if denseLen:
                idx = int(round(xs[i]))
                if 0 <= idx < denseLen:
                    state = denseStates[idx]
                    if state:
                        overrideColor = colorBySignal.get(state)
            if overrideColor is not None:
                self._drawCandleBar(xs[i], opens[i], highs[i], lows[i], closes[i],
                                    overrideColor, overrideColor, label=candleLabel)
            else:
                self._drawCandleBar(xs[i], opens[i], highs[i], lows[i], closes[i],
                                    colorBull, colorBear, label=candleLabel)

    def _drawLineOrBarSeries(self, panelId, d, xMin, xMax):
        useLod = d.fullCount > self.LOD_ACTIVATION_THRESHOLD
        slice_ = d.getLodSlice(xMin, xMax, self.LOD_MAX_POINTS) if useLod else None
        if d.dataType in ("bar", "volume") and d.volume:
            if slice_ is not None:
                xs, ys = slice_["xs"], slice_["ys"]
            else:
                xs = d.xs if d.xs else list(range(len(d.volume)))
                ys = d.volume
            spec = implot.Spec()
            implot.plot_bars(d.name, np.asarray(xs, dtype=np.float64), np.asarray(ys, dtype=np.float64),
                             bar_size=0.8, spec=spec)
            return
        if slice_ is not None:
            xs, ys = slice_["xs"], slice_["ys"]
        else:
            xs, ys = d.xs, d.ys
        spec = implot.Spec(line_color=self._colorTuple(d.color)) if d.color else implot.Spec()
        implot.plot_line(d.name, np.asarray(xs, dtype=np.float64), np.asarray(ys, dtype=np.float64), spec=spec)

    def _colorTuple(self, color):
        if color is None:
            return None
        r, g, b = color[0] / 255.0, color[1] / 255.0, color[2] / 255.0
        a = (color[3] / 255.0) if len(color) > 3 else 1.0
        return (r, g, b, a)

    def _drawLevels(self, panelId, panel):
        for lvl in panel.levels:
            v = float(lvl["value"])
            color = self._colorTuple(lvl.get("color")) or (0.47, 0.47, 0.47, 0.59)
            weight = float(lvl.get("thickness") or 1)
            label = lvl["label"] or (str(int(v)) if v == int(v) else str(v))
            if lvl["vertical"]:
                xs = np.array([v, v], dtype=np.float64)
                limits = self._lastAxisLimits.get(panelId, {}).get("y", (0.0, 1.0))
                ys = np.array([limits[0], limits[1]], dtype=np.float64)
            else:
                limits = self._lastAxisLimits.get(panelId, {}).get("x", (0.0, 1.0))
                xs = np.array([limits[0], limits[1]], dtype=np.float64)
                ys = np.array([v, v], dtype=np.float64)
            implot.plot_line(label or "##level", xs, ys, spec=implot.Spec(line_color=color, line_weight=weight))

    # --------------------------------------------------- LOD (saf numpy, aynen)
    def updateLod(self):
        """DPG surumunde ayri bir pass'ti (set_value ile mevcut seriyi guncelliyordu).
        implot'ta HER FRAME zaten yeniden ciziliyor (bkz. _renderPanelFrame) - bu
        metod artik sadece API uyumlulugu icin duruyor (no-op): asil LOD-yeniden-
        hesaplama hysteresis'i (_lodRangeChanged) _renderPanelFrame icinde, cizim
        anında dogrudan uygulanir (PanelData.getLodSlice zaten HER cagriginda
        [xMin,xMax]'a gore decimate ediyor - ayrica bir 'guncelleme' adimina gerek
        yok, immediate-mode'un kendisi bunu frame basina zaten yapiyor)."""
        return

    def _getFixedYRange(self, panel):
        if panel.ySyncMode == "fixedRange" and panel.yFixedRange:
            return panel.yFixedRange
        return None

    def _fullXRangeForPanel(self, panel):
        xMin = xMax = None
        for d in panel.dataList:
            if not d.isVisible:
                continue
            xs = d.xs if d.xs else (list(range(len(d.open))) if d.open else None)
            if xs:
                xMin = xs[0] if xMin is None else min(xMin, xs[0])
                xMax = xs[-1] if xMax is None else max(xMax, xs[-1])
        if xMin is None or xMax is None:
            return None
        return xMin, xMax

    def _isFiniteNumber(self, value):
        try:
            return math.isfinite(float(value))
        except (TypeError, ValueError):
            return False

    def _visibleYRangeForData(self, data, xMin, xMax):
        fullXs = data._fullXs
        if fullXs is None or len(fullXs) == 0:
            return self._visibleYRangeForDataSlow(data, xMin, xMax)

        if data.dataType == "candle" and data._fullLow is not None and data._fullHigh is not None:
            lows, highs = data._fullLow, data._fullHigh
        elif data.dataType in ("bar", "volume") and data._fullVolume is not None:
            highs = data._fullVolume
            lows = np.zeros_like(highs)
        else:
            values = data._fullYs
            if values is None:
                return None
            lows = highs = values

        n = min(len(fullXs), len(lows), len(highs))
        if n == 0:
            return None
        xsArr = fullXs[:n]
        startIdx = max(0, int(np.searchsorted(xsArr, xMin, side="left")))
        endIdx = min(n, int(np.searchsorted(xsArr, xMax, side="right")))
        if endIdx <= startIdx:
            return None

        lowsSlice = lows[startIdx:endIdx]
        highsSlice = highs[startIdx:endIdx]
        finiteMask = np.isfinite(lowsSlice) & np.isfinite(highsSlice)
        if not finiteMask.any():
            return None
        return float(lowsSlice[finiteMask].min()), float(highsSlice[finiteMask].max())

    def _visibleYRangeForDataSlow(self, data, xMin, xMax):
        xs = data.xs
        if data.dataType == "candle" and data.low and data.high:
            lows, highs = data.low, data.high
            if len(xs) == 0:
                xs = range(len(lows))
        elif data.dataType in ("bar", "volume") and data.volume:
            highs = data.volume
            lows = [0.0] * len(highs)
            if len(xs) == 0:
                xs = range(len(highs))
        else:
            values = data.ys
            lows = highs = values
            if len(xs) == 0:
                xs = range(len(values))

        yMin = yMax = None
        for i in range(min(len(xs), len(lows), len(highs))):
            x = xs[i]
            if x < xMin or x > xMax:
                continue
            lo, hi = lows[i], highs[i]
            if not self._isFiniteNumber(lo) or not self._isFiniteNumber(hi):
                continue
            yMin = lo if yMin is None else min(yMin, lo)
            yMax = hi if yMax is None else max(yMax, hi)
        if yMin is None or yMax is None:
            return None
        return yMin, yMax

    def _visibleYRangeForPanel(self, panelId, xLimits=None):
        panel = self._panels.get(panelId)
        xLimits = xLimits or self._lastAxisLimits.get(panelId, {}).get("x")
        if panel is None or xLimits is None:
            return None
        xMin, xMax = xLimits
        yMin = yMax = None
        for data in panel.dataList:
            if not data.isVisible:
                continue
            if (panelId, data.id) in self._legendHiddenData:
                continue
            dataRange = self._visibleYRangeForData(data, xMin, xMax)
            if dataRange is None:
                continue
            lo, hi = dataRange
            yMin = lo if yMin is None else min(yMin, lo)
            yMax = hi if yMax is None else max(yMax, hi)
        if yMin is None or yMax is None:
            return None
        return yMin, yMax

    # ------------------------------------------------- eksen override (bkz. modul docstring 3)
    def _setAxisOverride(self, panelId, axis, vMin, vMax, sticky=False):
        entry = self._pendingAxisOverride.setdefault(panelId, {})
        entry[axis] = (float(vMin), float(vMax), bool(sticky))
        self._autoFitAxes.setdefault(panelId, {})[axis] = False
        # Panel dikey scroll/collapse nedeniyle bu frame cizilmese bile model
        # yeni limiti hemen bilsin. Range Navigator sabit OHLC kaynagini
        # _lastAxisLimits'ten okur; cache sadece BeginPlot icinde guncellenirse
        # OHLC ekran disindayken scrollbar eski konuma geri sicrardi.
        self._lastAxisLimits.setdefault(panelId, {})[axis] = (
            float(vMin), float(vMax))

    def _setAxisAutoFit(self, panelId, axis):
        self._pendingAxisOverride.setdefault(panelId, {}).pop(axis, None)
        self._autoFitAxes.setdefault(panelId, {})[axis] = True

    def _applyAxisPadding(self, panelId, panel, xMarginRatio=0.02, yMarginRatio=0.08):
        xMin = xMax = yMin = yMax = None
        for d in panel.dataList:
            if not d.isVisible:
                continue
            xs = d.xs if d.xs else (list(range(len(d.open))) if d.open else None)
            if xs:
                xMin = xs[0] if xMin is None else min(xMin, xs[0])
                xMax = xs[-1] if xMax is None else max(xMax, xs[-1])
            if d.dataCount:
                yMin = d.minY if yMin is None else min(yMin, d.minY)
                yMax = d.maxY if yMax is None else max(yMax, d.maxY)

        if xMin is None or xMax is None:
            self._setAxisAutoFit(panelId, "x")
        else:
            xPad = max(1.0, (xMax - xMin) * xMarginRatio)
            self._setAxisOverride(panelId, "x", xMin - xPad, xMax + xPad, sticky=False)

        fixedY = self._getFixedYRange(panel)
        if fixedY is not None:
            self._setAxisOverride(panelId, "y", fixedY[0], fixedY[1], sticky=False)
        elif yMin is None or yMax is None:
            self._setAxisAutoFit(panelId, "y")
        else:
            yRange = yMax - yMin
            yPad = yRange * yMarginRatio if yRange > 0 else max(1.0, abs(yMax) * yMarginRatio)
            self._setAxisOverride(panelId, "y", yMin - yPad, yMax + yPad, sticky=False)

    # ---------------------------------------------------------- y adjust
    def adjustYAxis(self, panelId=None, yMarginRatio=0.08, xLimits=None):
        panelId = self.getActivePanelId() if panelId is None else panelId
        panel = self._panels.get(panelId)
        if panel is None:
            return False

        fixedY = self._getFixedYRange(panel)
        if fixedY is not None:
            self._setAxisOverride(panelId, "y", fixedY[0], fixedY[1], sticky=False)
            return True

        yRange = self._visibleYRangeForPanel(panelId, xLimits=xLimits)
        if yRange is None:
            self._setAxisAutoFit(panelId, "y")
            return True

        yMin, yMax = yRange
        ySpan = yMax - yMin
        yPad = ySpan * yMarginRatio if ySpan > 0 else max(1.0, abs(yMax) * yMarginRatio)
        self._setAxisOverride(panelId, "y", yMin - yPad, yMax + yPad, sticky=False)
        return True

    def adjustAllYAxes(self, yMarginRatio=0.08):
        count = 0
        for panel in self._panels.values():
            if not panel.getVisible():
                continue
            if self.adjustYAxis(panel.id, yMarginRatio=yMarginRatio):
                count += 1
        return count

    def adjustAllYAxesForXRange(self, xMin, xMax, yMarginRatio=0.08):
        """Tum gorunur panellerin Y limitlerini verilen yeni X araligina gore ayarlar.

        Range Navigator callback'lerinde axis-limit cache'i bir frame geriden
        gelebilecegi icin adjustAllYAxes() yerine bu varyant kullanilir.
        """
        xLimits = (xMin, xMax)
        count = 0
        for panel in self._panels.values():
            if not panel.getVisible():
                continue
            if self.adjustYAxis(panel.id, yMarginRatio=yMarginRatio, xLimits=xLimits):
                count += 1
        return count

    def resetPanelView(self, panelId=None, xMarginRatio=0.02, yMarginRatio=0.08):
        panelId = self.getActivePanelId() if panelId is None else panelId
        panel = self._panels.get(panelId)
        if panel is None:
            return False
        self._applyAxisPadding(panelId, panel, xMarginRatio=xMarginRatio, yMarginRatio=yMarginRatio)
        return True

    def resetAllPanelViews(self, xMarginRatio=0.02, yMarginRatio=0.08):
        count = 0
        for panel in self._panels.values():
            if not panel.getVisible():
                continue
            if self.resetPanelView(panel.id, xMarginRatio=xMarginRatio, yMarginRatio=yMarginRatio):
                count += 1
        return count

    # ---------------------------------------------------------- pan
    def _panStepDelta(self, mode, step, span):
        if mode == "VisibleScreenWidth":
            return span
        if mode == "UserDefined":
            return step
        try:
            return float(mode.split()[0])
        except (ValueError, IndexError):
            return span

    def panPanel(self, panelId=None, direction="left", mode="VisibleScreenWidth", step=100):
        panelId = self.getActivePanelId() if panelId is None else panelId
        panel = self._panels.get(panelId)
        if panel is None:
            return None

        limits = self._lastAxisLimits.get(panelId, {}).get("x")
        if limits is None:
            return None
        xMin, xMax = limits
        span = xMax - xMin

        if direction in ("start", "end"):
            dataRange = self._fullXRangeForPanel(panel)
            if dataRange is None:
                return None
            dataMin, dataMax = dataRange
            if direction == "start":
                newMin, newMax = dataMin, dataMin + span
            else:
                newMin, newMax = dataMax - span, dataMax
        else:
            delta = self._panStepDelta(mode, step, span)
            if direction == "left":
                newMin, newMax = xMin - delta, xMax - delta
            else:
                newMin, newMax = xMin + delta, xMax + delta
            dataRange = self._fullXRangeForPanel(panel)
            if dataRange is not None:
                dataMin, dataMax = dataRange
                if newMin < dataMin:
                    newMin, newMax = dataMin, dataMin + span
                elif newMax > dataMax:
                    newMin, newMax = dataMax - span, dataMax

        self._setAxisOverride(panelId, "x", newMin, newMax, sticky=False)
        return newMin, newMax

    def _zoomAxisLimits(self, panelId, axis, scale, clampRange=None):
        limits = self._lastAxisLimits.get(panelId, {}).get(axis)
        if limits is None:
            return None
        axisMin, axisMax = limits
        span = axisMax - axisMin
        if span <= 0:
            return None

        center = (axisMin + axisMax) / 2.0
        newSpan = max(span * scale, 1e-9)
        newMin = center - newSpan / 2.0
        newMax = center + newSpan / 2.0

        if clampRange is not None:
            dataMin, dataMax = clampRange
            dataSpan = dataMax - dataMin
            if dataSpan > 0:
                if newSpan >= dataSpan:
                    newMin, newMax = dataMin, dataMax
                elif newMin < dataMin:
                    newMin, newMax = dataMin, dataMin + newSpan
                elif newMax > dataMax:
                    newMin, newMax = dataMax - newSpan, dataMax

        self._setAxisOverride(panelId, axis, newMin, newMax, sticky=False)
        return newMin, newMax

    def zoomPanel(self, panelId=None, axes="x", direction="in", ratio=0.30):
        panelId = self.getActivePanelId() if panelId is None else panelId
        panel = self._panels.get(panelId)
        if panel is None:
            return None

        axes = (axes or "x").lower()
        direction = (direction or "in").lower()
        try:
            ratio = float(ratio)
        except (TypeError, ValueError):
            ratio = 0.30
        ratio = max(0.01, min(5.0, ratio))

        scale = 1.0 / (1.0 + ratio) if direction == "in" else (1.0 + ratio)
        result = {}

        if "x" in axes:
            xResult = self._zoomAxisLimits(panelId, "x", scale, clampRange=self._fullXRangeForPanel(panel))
            if xResult is not None:
                result["x"] = xResult

        if "y" in axes:
            yResult = self._zoomAxisLimits(panelId, "y", scale)
            if yResult is not None:
                result["y"] = yResult

        return result or None

    def setPanelAxisLimits(self, panelId=None, xLimits=None, yLimits=None):
        panelId = self.getActivePanelId() if panelId is None else panelId
        panel = self._panels.get(panelId)
        if panel is None:
            return None

        result = {}
        if xLimits is not None:
            self._setAxisOverride(panelId, "x", xLimits[0], xLimits[1], sticky=False)
            result["x"] = (xLimits[0], xLimits[1])
        if yLimits is not None:
            self._setAxisOverride(panelId, "y", yLimits[0], yLimits[1], sticky=False)
            result["y"] = (yLimits[0], yLimits[1])
        return result or None

    def panToFraction(self, panelId=None, fraction=0.0, liveOnly=False):
        panelId = self.getActivePanelId() if panelId is None else panelId
        panel = self._panels.get(panelId)
        if panel is None:
            return None

        limits = self._lastAxisLimits.get(panelId, {}).get("x")
        dataRange = self._fullXRangeForPanel(panel)
        if limits is None or dataRange is None:
            return None
        xMin, xMax = limits
        dataMin, dataMax = dataRange
        span = xMax - xMin
        total = dataMax - dataMin
        denom = max(total - span, 0.0)
        fraction = max(0.0, min(1.0, fraction))
        newMin = dataMin + fraction * denom
        newMax = newMin + span

        self._setAxisOverride(panelId, "x", newMin, newMax, sticky=bool(liveOnly))
        return newMin, newMax

    def zoomToFractionRange(self, panelId=None, startFraction=0.0, endFraction=1.0, liveOnly=False):
        panelId = self.getActivePanelId() if panelId is None else panelId
        panel = self._panels.get(panelId)
        if panel is None:
            return None

        dataRange = self._fullXRangeForPanel(panel)
        if dataRange is None:
            return None
        dataMin, dataMax = dataRange
        total = dataMax - dataMin
        startFraction = max(0.0, min(1.0, startFraction))
        endFraction = max(0.0, min(1.0, endFraction))
        if endFraction <= startFraction:
            endFraction = min(1.0, startFraction + 0.001)

        newMin = dataMin + startFraction * total
        newMax = dataMin + endFraction * total

        self._setAxisOverride(panelId, "x", newMin, newMax, sticky=bool(liveOnly))
        return newMin, newMax

    def unlockXAxis(self, panelId=None):
        """liveOnly=True ile sticky birakilan x override'i kaldirir (DPG'nin
        set_axis_limits_auto'suyla AYNI amac - serbest pan/zoom'a geri doner)."""
        panelId = self.getActivePanelId() if panelId is None else panelId
        entry = self._pendingAxisOverride.get(panelId)
        if entry and "x" in entry:
            # Tamamen silme: panel dikey scroll nedeniyle BeginPlot gormediyse
            # sticky limit henuz ImPlot'a hic uygulanmamis olabilir. Non-sticky
            # yap; panel ilk kez yeniden cizildiginde bir kere uygulanip
            # _applyAxisOverrideForFrame tarafindan kaldirilsin.
            xMin, xMax, _ = entry["x"]
            entry["x"] = (xMin, xMax, False)

    def setFitToScreenBarWidth(self, normal=None, wide=None, ultra=None):
        for key, value in (("normal", normal), ("wide", wide), ("ultra", ultra)):
            if value is None:
                continue
            try:
                parsed = float(value)
            except (TypeError, ValueError):
                continue
            if parsed > 0:
                self._fitToScreenBarWidth[key] = parsed

    # ------------------------------------------------------- view/range
    def getPanelDataCount(self, panelId=None):
        panelId = self.getActivePanelId() if panelId is None else panelId
        panel = self._panels.get(panelId)
        if panel is None:
            return 0
        count = 0
        for d in panel.dataList:
            if not d.isVisible:
                continue
            count = max(count, d.dataCount)
        return count

    def _plotPixelWidth(self, panelId):
        size = self._lastPlotRect.get(panelId)
        if size and size[0] > 0:
            return float(size[0])
        return 800.0

    def applyViewMode(self, panelId=None, mode="FitToScreen (Ultra)", n=1000, n2=2000):
        panelId = self.getActivePanelId() if panelId is None else panelId
        panel = self._panels.get(panelId)
        if panel is None:
            return None
        barCount = self.getPanelDataCount(panelId)
        if barCount <= 0:
            return None
        dataRange = self._fullXRangeForPanel(panel)
        if dataRange is None:
            return None
        dataMin, dataMax = dataRange

        if mode.startswith("FitToScreen"):
            if "Wide" in mode:
                barWidth = self._fitToScreenBarWidth["wide"]
            elif "Ultra" in mode:
                barWidth = self._fitToScreenBarWidth["ultra"]
            else:
                barWidth = self._fitToScreenBarWidth["normal"]
            plotWidth = self._plotPixelWidth(panelId)
            visible = int(plotWidth / barWidth) if barWidth > 0 else barCount
            visible = max(100, min(visible, barCount))
            offset = max(0, barCount - visible)
        elif mode == "Full Data":
            offset = 0
            visible = barCount
        elif mode == "Last N Data":
            visible = max(1, min(int(n), barCount))
            offset = max(0, barCount - visible)
        elif mode == "First N Data":
            visible = max(1, min(int(n), barCount))
            offset = 0
        elif mode == "Range":
            offset = max(0, min(int(n), barCount - 1))
            visible = max(1, min(int(n2), barCount - offset))
        else:
            return None

        span = max(1, visible)
        pad = max(1.0, span * 0.015)
        xMin = dataMin + offset - pad
        xMax = dataMin + offset + visible - 1 + pad

        self._setAxisOverride(panelId, "x", xMin, xMax, sticky=False)
        self.adjustYAxis(panelId, xLimits=(xMin, xMax))
        return xMin, xMax

    def applyViewModeToAllPanels(self, mode="FitToScreen (Ultra)", n=1000, n2=2000):
        count = 0
        for panel in self._panels.values():
            if not panel.getVisible():
                continue
            if self.applyViewMode(panel.id, mode, n=n, n2=n2) is not None:
                count += 1
        return count

    def setDefaultViewMode(self, mode, n=1000, n2=2000):
        self._defaultViewMode = mode
        self._defaultViewN = n
        self._defaultViewN2 = n2

    def _maybeApplyDefaultViewOnLoad(self, panelId):
        count = self.getPanelDataCount(panelId)
        hadData = self._lastDrawnDataCount.get(panelId, 0) > 0
        self._lastDrawnDataCount[panelId] = count
        if hadData or count <= 0:
            return
        self.applyViewMode(panelId, self._defaultViewMode, n=self._defaultViewN, n2=self._defaultViewN2)

    # -------------------------------------------------- source-params sync
    def readPanelPlotParams(self, panelId=None, plotId=None):
        panelId = self.getActivePanelId() if panelId is None else panelId
        panel = self._panels.get(panelId)
        if panel is None or panelId not in self._lastAxisLimits:
            return None
        params = {
            "panelId": panelId,
            "plotId": plotId or f"plot_{panelId}",
            "xAxisLimits": self._lastAxisLimits.get(panelId, {}).get("x"),
            "yAxisLimits": self._lastAxisLimits.get(panelId, {}).get("y"),
            "frame": imgui.get_frame_count(),
        }
        self._lastReadPlotParams = params
        return params

    def getLastReadPlotParams(self):
        return self._lastReadPlotParams

    def isPanelReadyForSourceParams(self, panelId):
        panel = self._panels.get(panelId)
        if panel is None or not panel.getVisible():
            return False
        return panelId in self._lastAxisLimits

    def applySourceParamsToPanel(self, panelId, params=None, yMarginRatio=0.08):
        params = params or self._lastReadPlotParams
        panel = self._panels.get(panelId)
        if panel is None or params is None:
            return False
        if panelId == params.get("panelId"):
            return True
        if not self.isPanelReadyForSourceParams(panelId):
            return False

        xLimits = params.get("xAxisLimits")
        if xLimits is not None:
            self._setAxisOverride(panelId, "x", xLimits[0], xLimits[1], sticky=False)
        self.adjustYAxis(panelId, yMarginRatio=yMarginRatio, xLimits=xLimits)
        return True

    def applySourceXAxisToPanel(self, panelId, params=None):
        params = params or self._lastReadPlotParams
        panel = self._panels.get(panelId)
        if panel is None or params is None:
            return False
        if panelId == params.get("panelId"):
            return True
        if not self.isPanelReadyForSourceParams(panelId):
            return False
        xLimits = params.get("xAxisLimits")
        if xLimits is None:
            return False
        self._setAxisOverride(panelId, "x", xLimits[0], xLimits[1], sticky=False)
        return True

    def getXAxisLimits(self, panelId=None):
        panelId = self.getActivePanelId() if panelId is None else panelId
        return self._lastAxisLimits.get(panelId, {}).get("x")

    def getYAxisLimits(self, panelId=None):
        panelId = self.getActivePanelId() if panelId is None else panelId
        return self._lastAxisLimits.get(panelId, {}).get("y")

    def getFullXRange(self, panelId=None):
        panelId = self.getActivePanelId() if panelId is None else panelId
        panel = self._panels.get(panelId)
        if panel is None:
            return None
        return self._fullXRangeForPanel(panel)

    def getOverviewSeries(self, panelId=None):
        panelId = self.getActivePanelId() if panelId is None else panelId
        panel = self._panels.get(panelId)
        if panel is None:
            return None
        data = self._pickInfoSourceData(panel)
        if data is None:
            return None
        if data.fullCount > self.LOD_ACTIVATION_THRESHOLD:
            dataRange = self._fullXRangeForPanel(panel)
            if dataRange is not None:
                sliceXY = data.getLodXY(dataRange[0], dataRange[1], self.OVERVIEW_LOD_MAX_POINTS)
                if sliceXY is not None:
                    return sliceXY
        xs = data._fullXs if data._fullXs is not None else data.xs
        ys = data._fullYs if data._fullYs is not None else data.ys
        if xs is None or ys is None or len(xs) == 0 or len(ys) == 0:
            return None
        return xs, ys

    # ----------------------------------------------------------- x ekseni
    def setXAxisMode(self, mode, dateTimeFormat=None):
        if mode not in ("bar", "datetime"):
            raise ValueError("xAxisMode 'bar' ya da 'datetime' olmali")
        self._xAxisMode = mode
        self._dateTimeFormat = dateTimeFormat
        self._lastAxisTicksSignature.clear()

    def setDebugAxisTicks(self, enabled: bool):
        self._debugAxisTicks = bool(enabled)

    def getXAxisMode(self):
        return self._xAxisMode

    def getXAxisDateTimeFormat(self):
        return self._dateTimeFormat

    def _pickTimestampData(self, panel):
        for d in panel.dataList:
            if d.timestamps:
                return d
        return None

    def _buildDatetimeTicks(self, panelId, data, xMin, xMax, n):
        left = max(0, int(xMin))
        right = min(n - 1, int(xMax) + 1)
        if left > right or left >= n:
            return []
        if self._dateTimeFormat is None:
            fmt = "%d.%m.%Y %H:%M:%S" if data.isIntraday else "%d.%m.%Y"
        elif self._dateTimeFormat == "auto":
            fmt = "%H:%M" if data.isIntraday else "%d.%m.%Y"
        else:
            fmt = self._dateTimeFormat
        step = self._chooseTickStep(panelId, right - left + 1, data, fmt)
        start = (left // step) * step
        if start < left:
            start += step
        indices = set(range(start, right + 1, step))

        dayIndices = set()
        if self._dayChangeMarkersEnabled and data.isIntraday and not self._fmtHasDateToken(fmt):
            dayIndices = self._dayBoundaryIndices(data, left, right, n)
            if len(dayIndices) > 20:
                dayIndices = set()
            indices |= dayIndices

        ticks = []
        for i in sorted(indices):
            if i >= n:
                continue
            ts = data.timestamps[i]
            if not hasattr(ts, "strftime"):
                continue
            curFmt = self._dayChangeFormat if i in dayIndices else fmt
            ticks.append((ts.strftime(curFmt), float(i)))
        return ticks

    def _fmtHasDateToken(self, fmt):
        return any(tok in fmt for tok in ("%d", "%m", "%Y", "%y", "%j", "%b", "%B", "%a", "%A", "%x"))

    def _dayBoundaryIndices(self, data, left, right, n):
        if right - left + 1 > self._dayBoundaryScanCap or right <= left:
            return set()
        boundaries = set()
        prevDate = None
        for i in range(max(0, left), min(n, right + 1)):
            ts = data.timestamps[i]
            if not hasattr(ts, "date"):
                continue
            d = ts.date()
            if prevDate is not None and d != prevDate:
                boundaries.add(i)
            prevDate = d
        return boundaries

    def _chooseTickStep(self, panelId, barsInRange, data, fmt):
        step = self._minTickStep
        maxTicks = self._maxTicksForWidth(panelId, data, fmt)
        while barsInRange / step > maxTicks:
            step *= 2
        return step

    def _maxTicksForWidth(self, panelId, data, fmt):
        plotWidth = self._plotWidthPx(panelId)
        labelWidth = self._estimateLabelPixelWidth(data, fmt)
        fit = int(plotWidth / labelWidth)
        return max(3, min(self._maxTicksOnScreen, fit))

    def _plotWidthPx(self, panelId):
        size = self._lastPlotRect.get(panelId)
        if size and size[0] > 0:
            return size[0]
        return 900

    def _estimateLabelPixelWidth(self, data, fmt):
        sample = data.timestamps[0] if data.timestamps else None
        text = sample.strftime(fmt) if hasattr(sample, "strftime") else fmt
        longest = max((len(line) for line in text.split("\n")), default=len(text))
        return longest * self._axisCharPxWidth + self._axisTickPadding

    def _estimateYAxisLabelWidth(self, panel):
        yMin = yMax = None
        for d in panel.dataList:
            if not d.isVisible or not d.dataCount:
                continue
            yMin = d.minY if yMin is None else min(yMin, d.minY)
            yMax = d.maxY if yMax is None else max(yMax, d.maxY)
        if yMin is None or yMax is None:
            return 0
        span = abs(yMax - yMin)
        if span < 0.1:
            decimals = 3
        elif span < 10:
            decimals = 2
        elif span < 1000:
            decimals = 1
        else:
            decimals = 0
        longest = max(len(f"{yMin:.{decimals}f}"), len(f"{yMax:.{decimals}f}"))
        return longest * self._axisCharPxWidth + self._axisTickPadding

    def updateXAxisTicks(self, panelId=None):
        """DPG surumunde ayri bir pass'ti (set_axis_ticks/reset_axis_ticks). implot'ta
        tick'ler bir plot'un begin_plot/end_plot'u ICINDE setup_axis_ticks ile
        uygulanmasi gerektigi icin bu metod artik SADECE imza/API uyumlulugu icin
        duruyor - gercek uygulama _renderPanelFrame icinde (datetime modundayken)
        yapilir, bkz. orada."""
        return

    # ------------------------------------------------------- panel hide/show
    def hidePanel(self, panelId):
        panel = self._panels.get(panelId)
        if panel is None:
            return
        panel.setVisible(False)

    def showPanel(self, panelId):
        panel = self._panels.get(panelId)
        if panel is None:
            return
        panel.setVisible(True)

    def hideAllPanels(self):
        for panelId in self._panels:
            self.hidePanel(panelId)

    def showAllPanels(self):
        for panelId in self._panels:
            self.showPanel(panelId)

    def collapseAllPanels(self):
        for panelId, panel in self._panels.items():
            if panel.getVisible():
                self.collapsePanel(panelId)

    def expandAllPanels(self):
        for panelId, panel in self._panels.items():
            if panel.getVisible():
                self.expandPanel(panelId)

    def collapsePanel(self, panelId):
        panel = self._panels.get(panelId)
        if panel is None:
            return False
        panel._collapsed = True
        return True

    def expandPanel(self, panelId):
        panel = self._panels.get(panelId)
        if panel is None:
            return False
        panel._collapsed = False
        # resize_y child kendi son (collapsed) yuksekligini saklayabilir.
        # Modelde korunan normal panel.height'i bir sonraki begin_child'da
        # acikca geri uygula.
        self._panelHeightRestorePending[panelId] = max(80, int(panel.height or 300))
        return True

    # --------------------------------------------------------- info paneli
    def setInfoPanelMode(self, mode: str):
        mode = str(mode or "").lower()
        if mode not in ("hidden", "hover", "active", "always"):
            raise ValueError("info panel mode must be one of: hidden, hover, active, always")
        self._infoPanelMode = mode

    def getInfoPanelMode(self):
        return self._infoPanelMode

    def setInfoPanelsVisible(self, visible: bool):
        self.setInfoPanelMode("always" if visible else "hover")

    def setActiveInfoPanel(self, panelId):
        self._infoActivePanelId = panelId

    def setInfoSharedIndex(self, index):
        if index is None:
            return
        self._infoSharedIndex = int(index)

    def _shouldShowInfoPanel(self, panelId, hoveredPanelId=None):
        panel = self._panels.get(panelId)
        if panel is not None and not panel.getInfoPanelVisible():
            return False
        mode = self._infoPanelMode
        if mode == "hidden":
            return False
        if mode == "always":
            return True
        if mode == "active":
            return panelId == self._infoActivePanelId
        return panelId == hoveredPanelId

    def _resolveInfoIndex(self, panelId, data, hoveredPanelId=None, hoveredIndex=None):
        idx = hoveredIndex
        if idx is None and self._infoPanelMode in ("always", "active"):
            idx = self._infoSharedIndex
        if idx is not None:
            idx = self._clampInfoIndex(data, idx)
            self._infoLastIndex[panelId] = idx
            return idx
        if panelId in self._infoLastIndex:
            return self._infoLastIndex[panelId]
        return max(0, self._infoDataLen(data) - 1)

    def _clampInfoIndex(self, data, idx):
        dataLen = self._infoDataLen(data)
        if dataLen <= 0:
            return 0
        return max(0, min(dataLen - 1, int(idx)))

    def _pickInfoSourceData(self, panel):
        for cand in panel.dataList:
            if cand.isVisible and cand.timestamps:
                return cand
        for cand in panel.dataList:
            if cand.isVisible:
                return cand
        return None

    def _infoDataLen(self, d):
        if d is None:
            return 0
        candidates = (
            getattr(d, "_fullXs", None),
            getattr(d, "_fullYs", None),
            getattr(d, "_fullClose", None),
            getattr(d, "timestamps", None),
            getattr(d, "xs", None),
            getattr(d, "ys", None),
            getattr(d, "close", None),
        )
        for seq in candidates:
            if seq is not None and len(seq) > 0:
                return len(seq)
        return 0

    def _valueAtIndex(self, d, idx):
        if d is None or idx is None:
            return None
        if d.dataType == "candle":
            fullSrc, plainSrc = getattr(d, "_fullClose", None), getattr(d, "close", None)
        else:
            fullSrc, plainSrc = getattr(d, "_fullYs", None), getattr(d, "ys", None)
        src = fullSrc if fullSrc is not None else plainSrc
        if src is None or idx < 0 or idx >= len(src):
            return None
        try:
            return float(src[idx])
        except (TypeError, ValueError):
            return None

    def _buildHoverLabel(self, panel=None, d=None, idx=None):
        dataLen = self._infoDataLen(d)
        valid = d is not None and idx is not None and 0 <= idx < dataLen
        hasTimestamp = valid and bool(getattr(d, "timestamps", None)) and idx < len(d.timestamps)
        isIntraday = getattr(d, "isIntraday", True) if d is not None else True

        def attr(name):
            return getattr(d, name, None) if d is not None else None

        def val(seqName, fullName, fmt):
            seq = attr(seqName)
            fullArr = attr(fullName) if fullName else None
            src = fullArr if fullArr is not None else seq
            if not valid or src is None or idx >= len(src):
                return "..."
            v = src[idx]
            try:
                if fmt == "d":
                    return format(int(v), fmt)
                return format(float(v), fmt)
            except (TypeError, ValueError):
                return "..."

        def num(seqName, fullName):
            seq = attr(seqName)
            fullArr = attr(fullName) if fullName else None
            src = fullArr if fullArr is not None else seq
            if not valid or src is None or idx >= len(src):
                return None
            try:
                return float(src[idx])
            except (TypeError, ValueError):
                return None

        def diffVal():
            o = num("open", "_fullOpen")
            c = num("close", "_fullClose")
            if o is None or c is None:
                return "..."
            return f"{c - o:.2f}"

        def changeVal():
            o = num("open", "_fullOpen")
            c = num("close", "_fullClose")
            if o is None or c is None:
                return "..."
            pct = ((c - o) / o * 100.0) if o else 0.0
            return f"{pct:.2f}%"

        def yVal(series):
            fullYs = getattr(series, "_fullYs", None)
            ys = getattr(series, "ys", None)
            src = fullYs if fullYs is not None else ys
            if not valid or src is None or idx >= len(src):
                return "..."
            try:
                return f"{float(src[idx]):.2f}"
            except (TypeError, ValueError):
                return "..."

        idxStr = str(idx) if valid else "..."
        dateStr = "..."
        timeStr = "..."
        if hasTimestamp:
            ts = d.timestamps[idx]
            if hasattr(ts, "strftime"):
                dateStr = ts.strftime("%d.%m.%Y")
                timeStr = ts.strftime("%H:%M")
            else:
                dateStr = str(ts)

        infoFields = panel.getInfoFields() if panel is not None else None
        if infoFields is not None:
            fieldMap = {
                "open": ("Open", ".2f", "open", "_fullOpen"),
                "high": ("High", ".2f", "high", "_fullHigh"),
                "low": ("Low", ".2f", "low", "_fullLow"),
                "close": ("Close", ".2f", "close", "_fullClose"),
                "volume": ("Volume", ".0f", "volume", "_fullVolume"),
                "size": ("Size", "d", "size", None),
            }
            rows = []
            for field in infoFields:
                if field == "index":
                    rows.append(("Index", idxStr))
                elif field == "date":
                    rows.append(("Date", dateStr))
                elif field == "time":
                    if isIntraday:
                        rows.append(("Time", timeStr))
                elif field in fieldMap:
                    label, fmt, seqName, fullName = fieldMap[field]
                    rows.append((label, val(seqName, fullName, fmt)))
                elif panel is not None:
                    for series in panel.dataList:
                        if series.name == field and series.isVisible:
                            rows.append((field, yVal(series)))
                            break
        else:
            rows = [("Index", idxStr), ("Date", dateStr)]
            if isIntraday:
                rows.append(("Time", timeStr))
            rows.append(None)

            hasOhlc = d is not None and bool(getattr(d, "open", None))
            if hasOhlc:
                rows += [
                    ("Open", val("open", "_fullOpen", ".2f")),
                    ("High", val("high", "_fullHigh", ".2f")),
                    ("Low", val("low", "_fullLow", ".2f")),
                    ("Close", val("close", "_fullClose", ".2f")),
                    ("Volume", val("volume", "_fullVolume", ".0f")),
                    ("Size", val("size", None, "d")),
                    None,
                    ("Diff", diffVal()),
                    ("Change", changeVal()),
                    None,
                ]
                if panel is not None:
                    for series in panel.dataList:
                        if series.isVisible and series.dataType == "line":
                            rows.append((series.name, yVal(series)))
            elif panel is not None:
                for series in panel.dataList:
                    if series.isVisible:
                        rows.append((series.name, yVal(series)))

        realRows = [r for r in rows if r is not None]
        if not realRows:
            return ""
        nameW = max(len(name) for name, _ in realRows)
        valW = max(len(str(value)) for _, value in realRows)
        sepLine = "-" * (nameW + 3 + valW + 1)
        return "\n".join(
            sepLine if row is None else f"{row[0]:<{nameW}} : {row[1]}"
            for row in rows
        )

    # ----------------------------------------------------------- crosshair
    def setCrossHairMode(self, mode: str):
        mode = str(mode or "").lower()
        if mode not in ("hidden", "single", "all"):
            raise ValueError("crosshair mode must be one of: hidden, single, all")
        self._crossHairMode = mode

    def getCrossHairMode(self):
        return self._crossHairMode

    def setCrossHairPersist(self, persist: bool):
        self._crossHairPersist = bool(persist)

    def getCrossHairPersist(self):
        return self._crossHairPersist

    def getCrossHairLastPos(self):
        return self._crossHairLastPos

    # ----------------------------------------------------------- aktif panel
    def setActiveUpdateMode(self, mode: str):
        mode = str(mode or "").lower()
        if mode not in ("hover", "click"):
            raise ValueError("active update mode must be 'hover' or 'click'")
        self._activeUpdateMode = mode

    def getActiveUpdateMode(self):
        return self._activeUpdateMode

    def getActivePanelId(self):
        if self._activePanelId is None and self._panelOrder:
            return self._panelOrder[0]
        return self._activePanelId

    # ----------------------------------------------------------- sync/render
    def sync(self):
        orphanIds = self._drawnPanelIds - self._panels.keys()
        for panelId in orphanIds:
            self._drawnPanelIds.discard(panelId)

    def render(self):
        """HER FRAME GuiManager tarafindan, imgui layout akisinin paneller gorunecegi
        noktasinda cagrilir (DPG'de setContainer ile 'parent'lanan centerCenterPanel'in
        karsiligi - burada basitce 'bu noktada cagir'). _panelOrder'daki GORUNUR+cizilmis
        her paneli KENDI child_window+begin_plot/end_plot'u ile cizer, sonra bir onceki
        TAMAMLANMIS frame'in hover/crosshair durumuna gore info/mouse-pos/crosshair
        overlay'lerini (asil karar mantigi degismeden) uygular."""
        self.sync()
        curHoverState = None

        for panelId in list(self._panelOrder):
            panel = self._panels.get(panelId)
            if (panel is None or panelId not in self._drawnPanelIds or
                    not panel.getVisible() or panelId in self._detachedPanelIds):
                continue
            hover = self._renderPanelFrame(panelId, panel)
            if hover is not None:
                curHoverState = hover

        # Detached paneller ayni model/cizim yolunu floating ImGui window
        # icinde kullanir; stacked listede ikinci kez render edilmez.
        for panelId in list(self._panelOrder):
            if panelId not in self._detachedPanelIds:
                continue
            panel = self._panels.get(panelId)
            if panel is None or panelId not in self._drawnPanelIds or not panel.getVisible():
                continue
            hover = self._renderPanelFrame(panelId, panel, detached=True)
            if hover is not None:
                curHoverState = hover

        self._prevHoverState = curHoverState
        if curHoverState is not None:
            self._infoSharedIndex = curHoverState.get("idx", self._infoSharedIndex)
            if self._activeUpdateMode == "hover":
                self._activePanelId = curHoverState["panelId"]
            if curHoverState.get("clicked") and self._activeUpdateMode == "click":
                self._activePanelId = curHoverState["panelId"]
            self._crossHairLastPos = (curHoverState["panelId"], curHoverState["mx"], curHoverState["my"])
        elif not self._crossHairPersist:
            self._crossHairLastPos = None

        now = time.time()
        if now - self._lastRenderPrint >= 1.0:
            self._lastRenderPrint = now
            # print(f"[PanelManager.render] tick @ {now:.1f}  panels={len(self._panels)}")

    def _renderPanelFrame(self, panelId, panel, detached=False):
        """Tek bir panelin bu-frame'lik TUM cizimini yapar: child_window + plot +
        eksenler + seriler + level'lar + draw-hook'lar + crosshair + info/mouse-pos
        overlay'leri (onceki frame'in hover karariyla). Bu frame'de hover
        yakalandiysa {"panelId","mx","my","idx","clicked"} dondurur, yoksa None."""
        w = panel.width if panel.width and panel.width > 0 else -1
        h = self.COLLAPSED_PANEL_HEIGHT if getattr(panel, "_collapsed", False) else \
            (panel.height if panel.height and panel.height > 0 else 300)

        if detached:
            if panelId in self._detachedInitialSizePending:
                imgui.set_next_window_size((900.0, float(max(300, panel.height))), imgui.Cond_.once.value)
                self._detachedInitialSizePending.discard(panelId)
            expanded, stillOpen = imgui.begin(
                f"{panel.caption}##detached_panel_{panelId}", True)
            if not stillOpen:
                self.attachPanel(panelId)
                imgui.end()
                return None
            if not expanded:
                imgui.end()
                return None
        else:
            childFlags = imgui.ChildFlags_.borders.value
            if not getattr(panel, "_collapsed", False):
                childFlags |= imgui.ChildFlags_.resize_y.value
            imgui.begin_child(f"panel_{panelId}", size=(w, h), child_flags=childFlags)

        actualPanelWidth = imgui.get_window_size().x
        restoreHeight = self._panelHeightRestorePending.pop(panelId, None)
        if not detached and restoreHeight is not None:
            imgui.set_window_size((actualPanelWidth, float(restoreHeight)), imgui.Cond_.always.value)
        elif not detached and not getattr(panel, "_collapsed", False):
            panel.height = max(80, int(imgui.get_window_size().y))
        panelRectPos = imgui.get_cursor_screen_pos()

        self._renderPanelHeader(panelId, panel, detached)
        if getattr(panel, "_collapsed", False):
            self._lastPanelRect[panelId] = (panelRectPos.x, panelRectPos.y, actualPanelWidth, h)
            if detached:
                imgui.end()
            else:
                imgui.end_child()
            return None

        if not self._showCaptions:
            imgui.dummy((1, 15))

        # Caption artik panel header'inda; ImPlot'un ikinci bir baslik cizmesi
        # engellenir.
        plotFlags = implot.Flags_.no_title.value
        plotFlags |= implot.Flags_.no_mouse_text.value
        hoverResult = None

        # ImPlot API zorunlulugu: SetNextAxisToFit, BeginPlot'tan ONCE
        # cagrilmalidir (setup_axis/setup_axis_limits ise BeginPlot'tan sonra).
        fitNext = self._fitAxesNextFrame.pop(panelId, set())
        if self._autoFitAxes.get(panelId, {}).get("x") or "x" in fitNext:
            implot.set_next_axis_to_fit(implot.ImAxis_.x1.value)
        if self._autoFitAxes.get(panelId, {}).get("y") or "y" in fitNext:
            implot.set_next_axis_to_fit(implot.ImAxis_.y1.value)

        if implot.begin_plot(panel.caption if self._showCaptions else f"##plot_{panelId}",
                             size=(-1, -1), flags=plotFlags):
            # DearPyGuiDataPlotter parity: legend plotun icinde sag altta.
            implot.setup_legend(implot.Location_.south_east.value)
            implot.setup_axis(implot.ImAxis_.x1.value, "")
            implot.setup_axis(implot.ImAxis_.y1.value, panel.getYLabel())

            self._applyAxisOverrideForFrame(panelId, "x", implot.ImAxis_.x1.value)
            self._applyAxisOverrideForFrame(panelId, "y", implot.ImAxis_.y1.value)
            # DIKKAT: implot.setup_axis_ticks (ve her 'setup-fazi' cagrisi) SADECE
            # begin_plot sonrasi, get_plot_limits/plot_*/ciziminden ONCE yapilabilir -
            # get_plot_limits'in kendisi ImPlot'un ic 'setup lock'unu tetikler (axis
            # araliklarini kesinlestirir). Bu yuzden datetime tick'leri burada, ONCEKI
            # TAMAMLANMIS frame'in onbellege alinmis gorunur araligiyla (bir frame
            # gecikmeli, goz ile fark edilmez - overlay/hover onbellekleriyle AYNI
            # teknik) kuruluyor - guncel xMin/xMax'i (get_plot_limits'ten) BEKLEMEDEN.
            if self._xAxisMode == "datetime":
                prevLimits = self._lastAxisLimits.get(panelId, {}).get("x")
                if prevLimits is not None:
                    self._applyDatetimeTicksForFrame(panelId, panel, prevLimits[0], prevLimits[1])
                else:
                    fullRange = self._fullXRangeForPanel(panel)
                    if fullRange is not None:
                        self._applyDatetimeTicksForFrame(panelId, panel, fullRange[0], fullRange[1])

            rect = implot.get_plot_limits(implot.ImAxis_.x1.value, implot.ImAxis_.y1.value)
            xMin, xMax = rect.x.min, rect.x.max
            self._lastAxisLimits[panelId] = {"x": (xMin, xMax), "y": (rect.y.min, rect.y.max)}

            # get_plot_pos/get_plot_size setup'u kilitleyen sorgulardir; tum
            # setup_axis/setup_axis_ticks cagrilarindan SONRA okunmalidir.
            plotPos = implot.get_plot_pos()
            plotSize = implot.get_plot_size()
            self._lastPlotRect[panelId] = (plotSize.x, plotSize.y)
            self._lastPlotScreenRect[panelId] = (plotPos.x, plotPos.y, plotSize.x, plotSize.y)

            colorBull = (0.0, 1.0, 0.0, 1.0)
            colorBear = (1.0, 0.0, 0.0, 1.0)
            visibleSeriesCount = sum(1 for item in panel.dataList if item.isVisible)
            for d in panel.dataList:
                if not d.isVisible:
                    continue
                if d.dataType == "candle":
                    tsr = self._tradeSignalRenderer
                    denseStates = tsr.getDenseStates(panelId, d.id) if tsr is not None else None
                    colorBySignal = tsr.getColorBySignal() if denseStates is not None else None
                    self._drawCandleSeries(panelId, d, xMin, xMax, colorBull, colorBear,
                                           denseStates=denseStates, colorBySignal=colorBySignal,
                                           showLegend=visibleSeriesCount > 1)
                    if tsr is not None:
                        tsr.renderInPlot(panelId, d.id, xMin, xMax)
                else:
                    # ImPlot'un .ini'de sakladigi legend state'i ile bizim
                    # manuel Y-fit state'imiz ayrismasin. Gorunurlugun tek
                    # kaynagi _legendHiddenData olsun ve her frame ImPlot'a
                    # acikca uygulansin.
                    implot.hide_next_item(
                        (panelId, d.id) in self._legendHiddenData,
                        implot.Cond_.always.value)
                    self._drawLineOrBarSeries(panelId, d, xMin, xMax)
                    # ImPlot legend'indeki checkbox item gorunurlugunu kendi
                    # icinde tutar; PanelData.isVisible bundan haberdar olmaz.
                    # Legend tiklaninca bir sonraki frame native ImPlot Y-fit
                    # isteyerek sadece legend'de acik kalan serileri hesaba kat.
                    if (implot.is_legend_entry_hovered(d.name) and
                            imgui.is_mouse_clicked(0)):
                        legendKey = (panelId, d.id)
                        if legendKey in self._legendHiddenData:
                            self._legendHiddenData.discard(legendKey)
                        else:
                            self._legendHiddenData.add(legendKey)
                        self._fitAxesNextFrame.setdefault(panelId, set()).add("y")
            self._drawLevels(panelId, panel)

            for hook in self._panelDrawHooks.get(panelId, []):
                hook(self, panelId, panel)

            measureTool = self._rangeMeasureTool
            if measureTool is not None:
                measureTool.drawInPlot(panelId)

            if implot.is_plot_hovered():
                mx, my = implot.get_plot_mouse_pos()
                idx = int(round(mx))
                clicked = imgui.is_mouse_clicked(0)
                if measureTool is not None:
                    measureTool.handlePlotClick(panelId, idx, clicked)
                dblClicked = imgui.is_mouse_double_clicked(0)
                if dblClicked:
                    self._applyAxisPadding(panelId, panel)
                hoverResult = {"panelId": panelId, "mx": mx, "my": my, "idx": idx, "clicked": clicked}

            self._drawCrossHairInline(panelId)

            implot.end_plot()

            # ImGui drag-drop target'i her zaman SON cizilen item'a aittir.
            # Bu nedenle hedefi begin_child() hemen sonrasinda kurmak paneli/
            # plot'u hedeflemiyordu ve Pool payload'i hic yakalanmiyordu.
            # DearPyGui portundaki plot_{id} drop_callback karsiligi burada,
            # end_plot() plot item'ini son item yaptiktan hemen sonra kurulur.
            # Drop ile eklenen seri bir sonraki immediate-mode frame'inde cizilir.
            self._handlePoolDrop(panelId)
        else:
            self._lastPlotRect[panelId] = (0.0, 0.0)
            self._lastPlotScreenRect.pop(panelId, None)

        self._lastPanelRect[panelId] = (panelRectPos.x, panelRectPos.y, actualPanelWidth, h)
        self._drawOverlayTextInline(panelId, panel)

        if detached:
            imgui.end()
        else:
            imgui.end_child()
        return hoverResult

    def _renderPanelHeader(self, panelId, panel, detached=False):
        """Stacked ve detached panellerin ortak baslik/kontrol satiri."""
        collapseLabel = "Expand" if getattr(panel, "_collapsed", False) else "Collapse"
        if imgui.small_button(f"{collapseLabel}##panel_header_collapse_{panelId}"):
            if getattr(panel, "_collapsed", False):
                self.expandPanel(panelId)
            else:
                self.collapsePanel(panelId)
        imgui.same_line()
        if imgui.small_button(f"{'Attach' if detached else 'Detach'}##panel_header_detach_{panelId}"):
            if detached:
                self.attachPanel(panelId)
            else:
                self.detachPanel(panelId)
        imgui.same_line()
        if imgui.small_button(f"Hide##panel_header_hide_{panelId}"):
            panel.setVisible(False)
            self.attachPanel(panelId)

        if self._showCaptions:
            captionWidth = imgui.calc_text_size(panel.caption).x
            captionX = max(imgui.get_cursor_pos_x() + 8.0,
                           (imgui.get_window_width() - captionWidth) * 0.5)
            imgui.same_line()
            imgui.set_cursor_pos_x(captionX)
            imgui.text_unformatted(panel.caption)
        else:
            imgui.same_line()
            imgui.dummy((1.0, imgui.get_text_line_height()))
        imgui.separator()

    def _applyAxisOverrideForFrame(self, panelId, axis, axisEnum):
        entry = self._pendingAxisOverride.get(panelId)
        if not entry or axis not in entry:
            return
        vMin, vMax, sticky = entry[axis]
        implot.setup_axis_limits(axisEnum, vMin, vMax, implot.Cond_.always.value)
        if not sticky:
            entry.pop(axis, None)

    def _applyDatetimeTicksForFrame(self, panelId, panel, xMin, xMax):
        data = self._pickTimestampData(panel)
        if data is None or not data.timestamps:
            return
        n = len(data.timestamps)
        lo, hi = max(0, xMin), min(n - 1, xMax)
        ticks = tuple(self._buildDatetimeTicks(panelId, data, lo, hi, n))
        # ImPlot is immediate-mode: setup_axis_ticks must be submitted on
        # every frame. The signature is useful for diagnostics only; skipping
        # an unchanged set makes labels disappear after their first frame and
        # the axis falls back to raw bar numbers.
        self._lastAxisTicksSignature[panelId] = ticks
        if not ticks:
            return
        if self._debugAxisTicks:
            print(f"[xAxisTicks] panel={panelId} lo={lo:.1f} hi={hi:.1f} ticks={ticks}")
        values = [t[1] for t in ticks]
        labels = [t[0] for t in ticks]
        implot.setup_axis_ticks(implot.ImAxis_.x1.value, values, labels)

    def _drawCrossHairInline(self, panelId):
        """Bu panelin begin_plot/end_plot'u ACIKKEN cagrilir - ONCEKI tamamlanmis
        frame'in hover/crosshair kararina gore (immediate-mode: gosterilmeyecekse
        cagri hic yapilmaz, DPG'nin show/hide'inin karsiligi)."""
        if self._crossHairMode == "hidden":
            return
        pos = self._crossHairLastPos
        if pos is None:
            return
        sourcePanelId, x, y = pos
        panel = self._panels.get(panelId)
        if panel is not None and not panel.getCrossHairVisible():
            return
        color = (1.0, 1.0, 0.0, 0.63)
        if panelId == sourcePanelId:
            implot.drag_line_x(1000 + panelId, x, color, 1, implot.DragToolFlags_.no_inputs.value)
            implot.drag_line_y(2000 + panelId, y, color, 1, implot.DragToolFlags_.no_inputs.value)
        elif self._crossHairMode == "all":
            implot.drag_line_x(1000 + panelId, x, color, 1, implot.DragToolFlags_.no_inputs.value)

    def _drawOverlayTextInline(self, panelId, panel):
        """ONCEKI tamamlanmis frame'in hover bilgisine gore info-panel/mouse-pos
        metnini panelin child_window'u (henuz acik, begin_child/end_child arasinda)
        icine draw-list text olarak basar - DPG'nin kalici add_text widget'inin
        immediate-mode karsiligi (bir frame gecikmeli, goz ile fark edilmez)."""
        prevHover = self._prevHoverState
        hoveredPanelId = prevHover["panelId"] if prevHover else None
        hoveredIndex = prevHover["idx"] if prevHover else None

        d = self._pickInfoSourceData(panel)
        dl = imgui.get_window_draw_list()
        originX, originY = imgui.get_item_rect_min() if False else (
            self._lastPanelRect.get(panelId, (0, 0, 0, 0))[0],
            self._lastPanelRect.get(panelId, (0, 0, 0, 0))[1],
        )

        if self._shouldShowInfoPanel(panelId, hoveredPanelId) and d is not None:
            idx = self._resolveInfoIndex(panelId, d, hoveredPanelId, hoveredIndex)
            label = self._buildHoverLabel(panel, d, idx)
            if label:
                infoPanelY = self._infoPanelYWithCaptions if self._showCaptions else self._infoPanelYNoCaptions
                infoPanelX = max(80, self._infoPanelBaseX + self._estimateYAxisLabelWidth(panel))
                # ImDrawList.add_text does not lay out embedded newlines like
                # imgui.text_unformatted does. Drawing the complete multi-line
                # label in one call placed Index/Date/Time and all following
                # rows on the same baseline. Render every row explicitly with
                # the active font's actual line spacing.
                textColor = imgui.color_convert_float4_to_u32((0.82, 0.82, 0.86, 1.0))
                separatorColor = imgui.color_convert_float4_to_u32((0.48, 0.48, 0.52, 1.0))
                lineHeight = imgui.get_text_line_height_with_spacing()
                textX = originX + infoPanelX
                textY = originY + infoPanelY
                lines = label.splitlines()
                parsedRows = []
                maxLabelWidth = 0.0
                for line in lines:
                    if " : " in line:
                        name, value = line.split(" : ", 1)
                        name = name.rstrip()
                        value = value.lstrip()
                        parsedRows.append((name, value))
                        maxLabelWidth = max(maxLabelWidth, imgui.calc_text_size(name).x)
                    else:
                        parsedRows.append(None)

                # Labels and values are separate pixel columns. Padding with
                # spaces (used by _buildHoverLabel) cannot align proportional
                # fonts and made the two parts appear on top of each other.
                # Keep a DPI-independent safety column. On some Windows font
                # scales calc_text_size under-reported the first row just
                # enough for the long Index value to touch its label.
                labelColumnWidth = max(90.0, maxLabelWidth + 24.0)
                valueX = textX + labelColumnWidth
                separatorX = valueX - 15.0
                totalWidth = labelColumnWidth + max(
                    (imgui.calc_text_size(row[1]).x for row in parsedRows if row is not None),
                    default=40.0,
                )
                for lineNo, row in enumerate(parsedRows):
                    y = textY + lineNo * lineHeight
                    if row is None:
                        dl.add_line((textX, y + lineHeight * 0.45),
                                    (textX + totalWidth, y + lineHeight * 0.45),
                                    separatorColor, 1.0)
                        continue
                    name, value = row
                    dl.add_text((textX, y), textColor, name)
                    dl.add_text((separatorX, y), textColor, ":")
                    dl.add_text((valueX, y), textColor, value)

        if d is not None:
            idx = self._resolveInfoIndex(panelId, d, hoveredPanelId, hoveredIndex)
            yValue = self._valueAtIndex(d, idx)
            if yValue is not None:
                mousePosY = self._mousePosYWithCaptions if self._showCaptions else self._mousePosYNoCaptions
                text = f"X: {idx}  Y: {yValue:.2f}"
                textSize = imgui.calc_text_size(text)
                # Mouse koordinatini plot/legend alaninin disinda, panelin
                # baslik satirinin en saginda goster. Boylece north-east
                # legend (Signals/PnL vb.) ve soldaki bilgi paneliyle ayni
                # alani paylasmaz.
                rightMargin = 36.0
                panelWidth = self._lastPanelRect.get(panelId, (0, 0, 0, 0))[2]
                x = originX + max(4.0, panelWidth - textSize.x - rightMargin)
                y = originY + (0.0 if self._showCaptions else -2.0)
                dl.add_text((x, y), imgui.color_convert_float4_to_u32((0.82, 0.82, 0.86, 1.0)), text)
