import numpy as np
from imgui_bundle import imgui, implot


class RangeSliderBar:
    """DearPyGuiDataPlotter'daki RangeSliderBar'in implot/imgui_bundle portu.

    Mimari AYNI (bkz. plan: backend swap) - DPG'nin fraction-space (x in
    [0,100], y in [0,1] SABIT eksen) yaklasimi birebir korunuyor, cunku bu
    yaklasimin tum matematigi (fraction<->gercek-eksen donusumu) implot'ta da
    PanelManager.zoomToFractionRange/panToFraction uzerinden AYNEN gecerli -
    implot'un kendi drag_rect'i de ayni ImPlot C++ kutuphanesinden geliyor.

    Iki parca:
      1) Overview mini-plot: x/y SABIT [0,100]/[0,1] eksende, PanelManager.
         getOverviewSeries() ile gelen Close/temsili seriyi plot_shaded ile
         silüet olarak, ustune implot.drag_rect ile gorunur-pencere secim
         kutusunu cizer. Kutu suruklenince (govde=pan, kenar=resize/zoom)
         PanelManager.zoomToFractionRange(liveOnly=True) cagrilir.
      2) Scroll bar: imgui.slider_int - sadece PENCERENIN KONUMUNU (genisligi
         DEGIL) temsil eder, PanelManager.panToFraction(liveOnly=True) cagirir.

    liveOnly=True DPG surumunde split_frame/set_axis_limits_auto'yu ATLAMAK
    icindi (surukleme sirasinda saniyede onlarca kez senkron frame-bekleme
    kekemelige yol aciyordu). implot'ta PanelManager zaten sadece bir dict'e
    yaziyor (ucuz) - liveOnly davranissal bir fark YARATMAZ, sadece cagri-yeri/
    imza uyumlulugu icin aynen tutuluyor (bkz. panelManager.py docstring'i)."""

    OVERVIEW_HEIGHT = 110.0
    OVERVIEW_LOD_MAX_POINTS = 800
    DRAG_RECT_ID = 90001

    def __init__(self):
        self._panelManager = None
        self._viewChangeCallback = None
        self._sliderVisible = False
        self._scrollbarVisible = True
        self._rectFrac = (0.0, 0.0, 100.0, 1.0)  # x1,y1,x2,y2 - fraction-space secim kutusu
        self._rangeDragActive = False
        self._scrollDragActive = False
        self._scrollDragValue = 0

    def setPanelManager(self, panelManager):
        self._panelManager = panelManager

    def setViewChangeCallback(self, callback):
        self._viewChangeCallback = callback

    def _notifyViewChanged(self, panelId, xLimits):
        if self._viewChangeCallback is not None and xLimits is not None:
            self._viewChangeCallback(panelId, xLimits)

    def build(self, parentTag=None):
        """DPG surumunde bu metod centerTopPanel icine overview plot + scroll
        bar widget'larini KALICI olarak OLUSTURUYORDU. implot immediate-mode
        oldugu icin onceden kurulacak bir sey yok - render() zaten HER karede
        yeniden cizer. Cagri-yeri uyumlulugu icin no-op olarak tutuluyor
        (GuiManager ayni yerde cagirabilir)."""
        pass

    def setSliderVisible(self, visible: bool):
        self._sliderVisible = bool(visible)

    def setScrollbarVisible(self, visible: bool):
        self._scrollbarVisible = bool(visible)

    def getSliderVisible(self):
        return self._sliderVisible

    def getScrollbarVisible(self):
        return self._scrollbarVisible

    def getSourcePanelId(self):
        """Range Navigator her zaman OHLC/candle panelini kaynak alir."""
        pm = self._panelManager
        if pm is None:
            return None
        for panel in pm.iterateAllPanels():
            if str(panel.name or "").upper() == "OHLC":
                return panel.id
        for panel in pm.iterateAllPanels():
            if any(data.dataType == "candle" for data in panel.iterateAllData()):
                return panel.id
        # Range Navigator (SliderRange + Scrollbar) aktif panele dusmez;
        # OHLC/candle yoksa kaynak yoktur ve cizim/etkilesim atlanir.
        return None

    def sync(self):
        """DPG surumunde ayri bir 'model <-> UI' temizligi yapiyordu (orphan
        widget silme) - implot'ta retained item olmadigi icin gerek yok,
        cagri-yeri uyumlulugu icin no-op."""
        pass

    def syncScrollToView(self, panelId=None):
        """DPG surumunde manuel bir pan/zoom butonundan SONRA scrollbar'i o an
        gorunur araliga gore ANINDA guncellemek icin cagriliyordu (retained
        widget - butonun kendisi axis limit'i degistiriyor ama scrollbar
        widget'i KENDI BASINA guncellenmiyordu). Burada render() HER FRAME
        pm.getXAxisLimits/getFullXRange'i zaten TAZE okuyup scrollbar'i ona
        gore cizdigi icin (immediate-mode, retained widget state yok) bu
        'push' adimina gerek yok - bir sonraki frame otomatik guncel olur.
        GuiManager'daki cagri-yeri uyumlulugu icin no-op birakildi."""
        pass

    def syncSliderToView(self, panelId=None):
        """bkz. syncScrollToView - ayni sebeple range-slider (drag_rect) icin
        de no-op; render() her frame zaten guncel axis limit'lerinden
        yeniden cizer."""
        pass

    # ------------------------------------------------------------- render
    def render(self):
        """GuiManager'in HER karede (kendi centerTopPanel imgui.begin_child
        blogu icinden) cagirmasi beklenir. Panel/veri yoksa veya her iki
        widget da gizliyse hicbir sey cizmez (DPG surumundeki centerTopPanel
        auto-hide davranisinin karsiligi - orada child_window'un kendisi
        show=False oluyordu, burada cagiran GuiManager zaten bu durumda
        begin_child'i hic acmayabilir; biz yine de kendi icerigimizi
        atlayarak ikinci bir guvenlik katmani sagliyoruz)."""
        pm = self._panelManager
        if pm is None:
            return
        panelId = self.getSourcePanelId()
        if panelId is None:
            return
        barCount = pm.getPanelDataCount(panelId)
        if barCount <= 0:
            return
        if not self._sliderVisible and not self._scrollbarVisible:
            return

        dataRange = pm.getFullXRange(panelId)
        xLimits = pm.getXAxisLimits(panelId)

        if self._sliderVisible:
            self._renderOverview(pm, panelId, dataRange, xLimits)
        if self._scrollbarVisible:
            self._renderScrollbar(pm, panelId, barCount, dataRange, xLimits)

    def _fractionsFromLimits(self, dataRange, xLimits):
        if dataRange is None or xLimits is None:
            return None
        dataMin, dataMax = dataRange
        xMin, xMax = xLimits
        total = dataMax - dataMin
        if total <= 0:
            return None
        startFrac = max(0.0, min(100.0, (xMin - dataMin) / total * 100.0))
        endFrac = max(0.0, min(100.0, (xMax - dataMin) / total * 100.0))
        if endFrac <= startFrac:
            endFrac = min(100.0, startFrac + 0.1)
        return startFrac, endFrac

    def _renderOverview(self, pm, panelId, dataRange, xLimits):
        fracs = self._fractionsFromLimits(dataRange, xLimits)
        if fracs is not None:
            startFrac, endFrac = fracs
            self._rectFrac = (startFrac, 0.0, endFrac, 1.0)

        flags = (implot.Flags_.no_legend | implot.Flags_.no_mouse_text |
                implot.Flags_.no_inputs | implot.Flags_.no_menus)
        avail = imgui.get_content_region_avail()
        if not implot.begin_plot("##range_slider_overview", (avail.x, self.OVERVIEW_HEIGHT), flags):
            return

        implot.setup_axis(implot.ImAxis_.x1, "", implot.AxisFlags_.no_decorations)
        implot.setup_axis(implot.ImAxis_.y1, "", implot.AxisFlags_.no_decorations)
        implot.setup_axis_limits(implot.ImAxis_.x1, 0.0, 100.0, implot.Cond_.always)
        implot.setup_axis_limits(implot.ImAxis_.y1, 0.0, 1.0, implot.Cond_.always)

        overview = pm.getOverviewSeries(panelId)
        if overview is not None:
            xs, ys = overview
            fxs, fys = self._toFractionSeries(xs, ys)
            if fxs is not None:
                implot.plot_shaded("##overview_shade", fxs, fys,
                                   spec=implot.Spec(fill_color=(0.5, 0.7, 1.0, 0.35)))
                implot.plot_line("##overview_line", fxs, fys,
                                 spec=implot.Spec(line_color=(0.5, 0.7, 1.0, 1.0), line_weight=1.0))

        x1, y1, x2, y2 = self._rectFrac
        changed, x1, y1, x2, y2, *_ = implot.drag_rect(
            self.DRAG_RECT_ID, x1, y1, x2, y2, (0.9, 0.9, 1.0, 0.35))
        if changed:
            self._rangeDragActive = True
            x1 = max(0.0, min(100.0, x1))
            x2 = max(0.0, min(100.0, x2))
            if x2 <= x1:
                x2 = min(100.0, x1 + 0.1)
            self._rectFrac = (x1, 0.0, x2, 1.0)
            newRange = pm.zoomToFractionRange(
                panelId, x1 / 100.0, x2 / 100.0, liveOnly=True)
            if newRange is not None:
                pm.adjustAllYAxesForXRange(newRange[0], newRange[1])
                self._notifyViewChanged(panelId, newRange)

        # Release frame'inde drag_rect artik `changed=False` donebilir. Kilit
        # acma kontrolu changed blogunun icinde kalirsa sticky X override
        # sonsuza kadar native sag-tus zoom'u ezer.
        if self._rangeDragActive and imgui.is_mouse_released(0):
            pm.unlockXAxis(panelId)
            self._rangeDragActive = False

        implot.end_plot()

    def _toFractionSeries(self, xs, ys):
        xs = np.asarray(xs, dtype=np.float64)
        ys = np.asarray(ys, dtype=np.float64)
        if len(xs) == 0:
            return None, None
        n = len(xs)
        if n > self.OVERVIEW_LOD_MAX_POINTS:
            step = max(1, n // self.OVERVIEW_LOD_MAX_POINTS)
            xs, ys = xs[::step], ys[::step]
        xMin, xMax = float(xs[0]), float(xs[-1])
        span = max(xMax - xMin, 1e-9)
        fxs = (xs - xMin) / span * 100.0
        yMin, yMax = float(np.nanmin(ys)), float(np.nanmax(ys))
        yRange = max(yMax - yMin, 1e-9)
        fys = (ys - yMin) / yRange
        return fxs, fys

    def _renderScrollbar(self, pm, panelId, barCount, dataRange, xLimits):
        fracs = self._fractionsFromLimits(dataRange, xLimits)
        posFrac = 0.0
        if fracs is not None:
            startFrac, endFrac = fracs
            span = max(endFrac - startFrac, 1e-9)
            denom = max(100.0 - span, 1e-9)
            posFrac = max(0.0, min(1.0, startFrac / denom)) if denom > 0 else 0.0

        sliderMax = max(1, 1000)
        calculatedVal = int(round(posFrac * sliderMax))
        sliderVal = self._scrollDragValue if self._scrollDragActive else calculatedVal
        imgui.set_next_item_width(-1)
        changed, newVal = imgui.slider_int("##range_scrollbar", sliderVal, 0, sliderMax)
        if imgui.is_item_active():
            self._scrollDragActive = True
            self._scrollDragValue = newVal
        if changed:
            self._scrollDragValue = newVal
            newRange = pm.panToFraction(panelId, newVal / sliderMax, liveOnly=True)
            if newRange is not None:
                pm.adjustAllYAxesForXRange(newRange[0], newRange[1])
                self._notifyViewChanged(panelId, newRange)
        # ImGui bu bayragi degerin degistigi frame'de degil, edit bittigi
        # (mouse birakildigi) frame'de verir; bu nedenle `changed` disinda.
        if imgui.is_item_deactivated_after_edit():
            pm.unlockXAxis(panelId)
            self._scrollDragActive = False
