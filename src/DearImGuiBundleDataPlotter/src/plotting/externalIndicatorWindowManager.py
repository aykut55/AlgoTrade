import math
from bisect import bisect_left, bisect_right

import numpy as np
from imgui_bundle import imgui, implot


class ExternalIndicatorWindowManager:
    """Center panel disinda lightweight indikator pencereleri yonetir - implot/
    imgui_bundle PORTU (D:\\Aykut\\Projects\\AlgoTrade\\src\\DearPyGuiDataPlotter\\
    src\\plotting\\externalIndicatorWindowManager.py). Mimari/davranis AYNEN
    korunuyor (ayni followSource/updateSource/Apply-To-Src akisi, kendi LOD/
    datetime-tick/crosshair mantigini PanelManager'inkinden BAGIMSIZ, kendi
    (xs,ys) serilerini dogrudan cizerek tekrar implemente ediyor - DPG
    orijinalinin kendisi de boyleydi, bu bilerek 'duzeltilmeyen' bir tekrar,
    backend swap mimariyi degistirmiyor) - sadece dpg.* cagrilari imgui/
    implot'a cevrildi.

    DPG retained-item modelinde pencere/plot/seri BIR KERE kurulup render()
    SADECE eksen/degerleri GUNCELLIYORDU (dpg.set_value/set_axis_limits).
    implot immediate-mode oldugu icin HER FRAME tum pencere/plot/seri yeniden
    cizilir - render() artik 'guncelle' degil 'yeniden ciz'.

    DIKKAT (bu projede panelManager.py'nin implot portunda once bulunan ayni
    hata buraya da sizmasin): implot.setup_axis_ticks/setup_axis_limits gibi
    'setup-fazi' cagrilari SADECE begin_plot sonrasi, implot.get_plot_limits/
    plot_*'ten ONCE yapilabilir (get_plot_limits ImPlot'un ic 'setup lock'unu
    tetikler). Bu yuzden hem X-eksen datetime tick'leri hem Y-eksen auto-fit
    ONCEKI TAMAMLANMIS frame'in onbellege alinmis x-limitleriyle (bir frame
    gecikmeli, goz ile fark edilmez) kuruluyor - panelManager.py'deki AYNI
    teknik."""

    LOD_ACTIVATION_THRESHOLD = 6000
    LOD_MAX_POINTS = 3000

    def __init__(self, panelManager):
        self._panelManager = panelManager
        self._windows = []
        self._nextId = 1
        self._selectorVisible = False
        self._selectorPanelIndex = 0
        self._selectorPanelId = None
        self._selectorSelectedDataIds = set()

    # ------------------------------------------------------------- acma/kapama
    def openDefaultIndicatorWindow(self, sourcePanelId=None):
        sourcePanelId = self._resolveSourcePanelId(sourcePanelId)
        if sourcePanelId is None:
            return None
        panel = self._panelManager.getPanel(sourcePanelId)
        if panel is None:
            return None
        series = self._pickDefaultSeries(panel)
        if not series:
            return None
        return self.openIndicatorWindow(sourcePanelId, series,
                                        title=f"Indicators - Panel {sourcePanelId}",
                                        followSource=True)

    def openIndicatorWindow(self, sourcePanelId, series, title=None, followSource=True):
        """DPG versiyonuyla AYNI imza. series: [{"name","xs","ys"}, ...] (dict)
        listesi. Yeni bir floating pencere state'i olusturup render() dongusune
        ekler, state'i dondurur (DPG versiyonu da state dict'ini donduruyordu)."""
        panel = self._panelManager.getPanel(sourcePanelId)
        if panel is None or not series:
            return None
        normalizedSeries = self._normalizeSeries(series)
        if not normalizedSeries:
            return None

        windowId = self._nextId
        self._nextId += 1
        state = {
            "windowId": windowId,
            "title": title or f"Indicators - Panel {sourcePanelId}",
            "open": True,
            "followSource": bool(followSource),
            "updateSource": False,
            "sourcePanelId": sourcePanelId,
            "series": normalizedSeries,
            "timestamps": self._pickTimestamps(panel),
            "isIntraday": self._pickIntraday(panel),
            "lastXLimits": None,  # (xMin,xMax) - ONCEKI TAMAMLANMIS frame'in gorunur araligi
            "lastTicks": None,
            "statusText": "",
            "axisSeeded": False,  # detached modda ilk frame'de bir kerelik Cond_.once seed
        }
        self._windows.append(state)
        return state

    def closeWindow(self, windowId):
        self._windows = [w for w in self._windows if w["windowId"] != windowId]

    def openIndicatorWindowSelector(self):
        """Panel ve line serilerini kullanicinin sectigi ikinci acma yolu."""
        panels = self._selectorPanels()
        if not panels:
            return None
        activeId = self._panelManager.getActivePanelId()
        self._selectorPanelIndex = next(
            (i for i, panel in enumerate(panels) if panel.id == activeId), 0)
        self._setSelectorPanel(panels[self._selectorPanelIndex])
        self._selectorVisible = True
        return True

    def _selectorPanels(self):
        return [
            panel for panel in self._panelManager.iterateAllPanels()
            if self._allVisibleLineSeries(panel)
        ]

    def _allVisibleLineSeries(self, panel):
        out = []
        for data in panel.iterateAllData():
            if not data.isVisible or data.dataType != "line":
                continue
            xs = list(data.xs or [])
            ys = [float(v) if self._finite(v) else math.nan for v in (data.ys or [])]
            if xs and ys:
                out.append({"dataId": data.id, "name": data.name or f"Data {data.id}",
                            "xs": xs, "ys": ys})
        return out

    def _setSelectorPanel(self, panel):
        self._selectorPanelId = panel.id
        self._selectorSelectedDataIds = {
            item["dataId"] for item in self._allVisibleLineSeries(panel)
        }

    def _normalizeSeries(self, series):
        out = []
        for item in series:
            name = str(item.get("name") or "Series")
            xs = list(item.get("xs") or [])
            ys = [float(v) if self._finite(v) else math.nan for v in (item.get("ys") or [])]
            n = min(len(xs), len(ys))
            if n <= 0:
                continue
            out.append({"name": name, "xs": xs[:n], "ys": ys[:n]})
        return out

    def _resolveSourcePanelId(self, sourcePanelId):
        if sourcePanelId is not None:
            return sourcePanelId
        activeId = self._panelManager.getActivePanelId()
        activePanel = self._panelManager.getPanel(activeId)
        if activePanel is not None and self._pickDefaultSeries(activePanel):
            return activeId
        for panel in self._panelManager.iterateAllPanels():
            panelName = str(getattr(panel, "name", "") or "").lower()
            panelCaption = str(getattr(panel, "caption", "") or "").lower()
            if "indicator" in panelName or "indicator" in panelCaption:
                if self._pickDefaultSeries(panel):
                    return panel.id
        for panel in self._panelManager.iterateAllPanels():
            if panel.name == "OHLC" and self._pickDefaultSeries(panel):
                return panel.id
        for panel in self._panelManager.iterateAllPanels():
            if self._pickDefaultSeries(panel):
                return panel.id
        return None

    def _pickDefaultSeries(self, panel):
        emaSeries = []
        otherSeries = []
        for data in panel.iterateAllData():
            if not data.isVisible or data.dataType != "line":
                continue
            name = data.name or ""
            xs = list(data.xs or [])
            ys = [float(v) if self._finite(v) else math.nan for v in (data.ys or [])]
            if xs and ys:
                target = emaSeries if name.upper().startswith("EMA") else otherSeries
                target.append({"name": name, "xs": xs, "ys": ys})
        return emaSeries or otherSeries

    def _pickTimestamps(self, panel):
        for data in panel.iterateAllData():
            if data.timestamps:
                return list(data.timestamps)
        return []

    def _pickIntraday(self, panel):
        for data in panel.iterateAllData():
            if data.timestamps:
                return bool(getattr(data, "isIntraday", True))
        return True

    def _finite(self, value):
        try:
            return math.isfinite(float(value))
        except (TypeError, ValueError):
            return False

    # ----------------------------------------------------------------- render
    def render(self):
        """GuiManager'in HER FRAME cagirmasi beklenir (DPG orijinalindeki
        render() ile ayni yerde/sirada - deferred panel slotunda)."""
        self._renderSelector()
        if not self._windows:
            return
        alive = []
        for state in self._windows:
            if self._renderWindow(state):
                alive.append(state)
        self._windows = alive

    def _renderSelector(self):
        if not self._selectorVisible:
            return
        imgui.set_next_window_size((520.0, 420.0), imgui.Cond_.first_use_ever.value)
        opened, visible = imgui.begin("Open Indicator Window 2", self._selectorVisible)
        self._selectorVisible = bool(visible)
        if not opened:
            imgui.end()
            return

        panels = self._selectorPanels()
        if not panels:
            imgui.text_disabled("Gorunur line serisi olan panel yok.")
            imgui.end()
            return

        self._selectorPanelIndex = min(self._selectorPanelIndex, len(panels) - 1)
        labels = [f"Panel {p.id}: {p.caption or p.name}" for p in panels]
        changed, newIndex = imgui.combo("Source Panel", self._selectorPanelIndex, labels)
        if changed:
            self._selectorPanelIndex = newIndex
            self._setSelectorPanel(panels[newIndex])

        panel = panels[self._selectorPanelIndex]
        series = self._allVisibleLineSeries(panel)
        imgui.separator()
        imgui.text("Series")
        for item in series:
            selected = item["dataId"] in self._selectorSelectedDataIds
            changed, selected = imgui.checkbox(
                f"{item['name']}##indicator_selector_{panel.id}_{item['dataId']}", selected)
            if changed:
                if selected:
                    self._selectorSelectedDataIds.add(item["dataId"])
                else:
                    self._selectorSelectedDataIds.discard(item["dataId"])

        imgui.separator()
        selectedSeries = [
            {"name": item["name"], "xs": item["xs"], "ys": item["ys"]}
            for item in series if item["dataId"] in self._selectorSelectedDataIds
        ]
        if len(selectedSeries) == 0:
            imgui.begin_disabled()
        if imgui.button("Open Selected", (130, 0)) and selectedSeries:
            self.openIndicatorWindow(
                panel.id, selectedSeries,
                title=f"Indicators - {panel.caption or panel.name}", followSource=True)
            self._selectorVisible = False
        if len(selectedSeries) == 0:
            imgui.end_disabled()
        imgui.same_line()
        if imgui.button("Cancel", (90, 0)):
            self._selectorVisible = False
        imgui.end()

    def _renderWindow(self, state):
        expanded, stillOpen = imgui.begin(state["title"], state.get("open", True))
        if not stillOpen:
            imgui.end()
            return False
        if expanded:
            self._renderToolbar(state)
            self._renderPlot(state)
        imgui.end()
        return True

    def _renderToolbar(self, state):
        changed, newFollow = imgui.checkbox("Follow Source", state["followSource"])
        if changed:
            state["followSource"] = newFollow
            state["lastXLimits"] = None
            state["lastTicks"] = None
            if newFollow:
                state["updateSource"] = False
        imgui.same_line()
        applyClicked = imgui.button("Apply To Src")
        imgui.same_line()
        changed2, newUpdateSrc = imgui.checkbox("Update Src", state["updateSource"])
        if changed2:
            state["updateSource"] = newUpdateSrc
            if newUpdateSrc:
                state["followSource"] = False
        imgui.same_line()
        imgui.text(f"Source Panel: {state['sourcePanelId']}")
        imgui.same_line()
        imgui.text(state.get("statusText", ""))
        state["_applyToSrcClicked"] = applyClicked

    def _renderPlot(self, state):
        plotFlags = implot.Flags_.no_mouse_text.value
        if not implot.begin_plot("##plot", size=(-1, -1), flags=plotFlags):
            return
        implot.setup_axis(implot.ImAxis_.x1.value, "")
        implot.setup_axis(implot.ImAxis_.y1.value, "y")
        implot.setup_legend(implot.Location_.south_east.value)

        prevLimits = state.get("lastXLimits")

        # --- setup-fazi (get_plot_limits/plot_*'ten ONCE, bkz. sinif docstring'i) ---
        if state["followSource"]:
            xLimits = self._panelManager.getXAxisLimits(state["sourcePanelId"])
            if xLimits is not None:
                implot.setup_axis_limits(implot.ImAxis_.x1.value, xLimits[0], xLimits[1], implot.Cond_.always.value)
        elif not state["axisSeeded"]:
            xAll = self._fullXRange(state["series"])
            if xAll is not None:
                implot.setup_axis_limits(implot.ImAxis_.x1.value, xAll[0], xAll[1], implot.Cond_.once.value)
            state["axisSeeded"] = True

        if prevLimits is not None:
            yRange = self._visibleYLimits(state["series"], prevLimits[0], prevLimits[1])
            if yRange is not None:
                implot.setup_axis_limits(implot.ImAxis_.y1.value, yRange[0], yRange[1], implot.Cond_.always.value)

        if self._panelManager.getXAxisMode() == "datetime" and state["timestamps"] and prevLimits is not None:
            self._applyDatetimeTicks(state, prevLimits[0], prevLimits[1])

        # --- okuma (setup lock burada kesinlesir) ---
        rect = implot.get_plot_limits(implot.ImAxis_.x1.value, implot.ImAxis_.y1.value)
        xMin, xMax = rect.x.min, rect.x.max
        state["lastXLimits"] = (xMin, xMax)
        state["statusText"] = f"X [{xMin:.2f} - {xMax:.2f}]"

        # --- veri cizimi ---
        for item in state["series"]:
            xs, ys = self._lodSeries(item["xs"], item["ys"], xMin, xMax)
            if xs:
                implot.plot_line(item["name"], np.asarray(xs, dtype=np.float64),
                                 np.asarray(ys, dtype=np.float64))

        self._drawCrosshair(state, xMin, xMax)

        implot.end_plot()

        # --- source senkronu (plot KAPANDIKTAN sonra - panelManager cagrilari
        # implot context'ine bagli degil, herhangi bir yerde guvenle cagrilabilir) ---
        if state.get("_applyToSrcClicked"):
            self._applyExternalXToSource(state, (xMin, xMax), forceFollowers=True)
        elif state["updateSource"]:
            self._applyExternalXToSourceIfChanged(state, (xMin, xMax))

    def _fullXRange(self, series):
        xMin = xMax = None
        for item in series:
            if not item["xs"]:
                continue
            xMin = item["xs"][0] if xMin is None else min(xMin, item["xs"][0])
            xMax = item["xs"][-1] if xMax is None else max(xMax, item["xs"][-1])
        if xMin is None:
            return None
        return xMin, xMax

    def _applyExternalXToSourceIfChanged(self, state, xLimits):
        last = state.get("lastSourceUpdateXLimits")
        if last is not None and abs(last[0] - xLimits[0]) < 1e-9 and abs(last[1] - xLimits[1]) < 1e-9:
            return
        if self._applyExternalXToSource(state, xLimits, forceFollowers=True):
            state["lastSourceUpdateXLimits"] = xLimits

    def _applyExternalXToSource(self, state, xLimits, forceFollowers=False):
        result = self._panelManager.setPanelAxisLimits(state["sourcePanelId"], xLimits=xLimits, yLimits=None)
        if result is None:
            return False
        self._panelManager.adjustYAxis(state["sourcePanelId"], xLimits=xLimits)
        if forceFollowers:
            # Source'a uygulanan X araligi zaten render()'in bir sonraki
            # dongusunde followSource=True pencerelere normal akisla yayilir
            # (xLimits her frame panelManager'dan taze okunuyor) - immediate-
            # mode'da DPG'deki gibi 'aninda force' adimina gerek yok.
            pass
        return True

    def _lodSeries(self, xs, ys, xMin, xMax):
        if not xs or not ys:
            return [], []
        start = max(0, bisect_left(xs, xMin) - 1)
        end = min(len(xs), bisect_right(xs, xMax) + 1)
        if end <= start:
            return [], []
        count = end - start
        if count <= self.LOD_ACTIVATION_THRESHOLD:
            return xs[start:end], ys[start:end]
        stride = max(1, math.ceil(count / self.LOD_MAX_POINTS))
        return xs[start:end:stride], ys[start:end:stride]

    def _visibleYLimits(self, series, xMin, xMax):
        yMin = yMax = None
        for item in series:
            _, visibleYs = self._lodSeries(item["xs"], item["ys"], xMin, xMax)
            for y in visibleYs:
                if not self._finite(y):
                    continue
                yMin = y if yMin is None else min(yMin, y)
                yMax = y if yMax is None else max(yMax, y)
        if yMin is None or yMax is None:
            return None
        span = yMax - yMin
        pad = span * 0.08 if span > 0 else max(abs(yMax) * 0.05, 1.0)
        return yMin - pad, yMax + pad

    def _drawCrosshair(self, state, xMin, xMax):
        if self._panelManager.getCrossHairMode() == "hidden":
            return
        pos = self._panelManager.getCrossHairLastPos()
        if pos is None:
            return
        _, x, _ = pos
        if x is None or not (xMin <= x <= xMax):
            return
        color = (1.0, 1.0, 0.0, 0.63)
        implot.drag_line_x(3000 + state["windowId"], x, color, 1, implot.DragToolFlags_.no_inputs.value)

    def _applyDatetimeTicks(self, state, xMin, xMax):
        n = len(state["timestamps"])
        ticks = tuple(self._buildDatetimeTicks(state["timestamps"], state["isIntraday"], xMin, xMax, n))
        if state.get("lastTicks") == ticks:
            return
        state["lastTicks"] = ticks
        if ticks:
            values = [t[1] for t in ticks]
            labels = [t[0] for t in ticks]
            implot.setup_axis_ticks(implot.ImAxis_.x1.value, values, labels)

    def _buildDatetimeTicks(self, timestamps, isIntraday, xMin, xMax, n):
        left = max(0, int(xMin))
        right = min(n - 1, int(xMax) + 1)
        if left > right or left >= n:
            return []

        fmtConfig = self._panelManager.getXAxisDateTimeFormat()
        if fmtConfig is None:
            fmt = "%d.%m.%Y\n%H:%M:%S" if isIntraday else "%d.%m.%Y"
        elif fmtConfig == "auto":
            fmt = "%H:%M" if isIntraday else "%d.%m.%Y"
        else:
            fmt = fmtConfig

        bars = max(1, right - left + 1)
        maxTicks = self._maxTicksForPlot(timestamps, fmt)
        step = 7
        while bars / step > maxTicks:
            step *= 2

        start = (left // step) * step
        if start < left:
            start += step

        ticks = []
        for i in range(start, right + 1, step):
            ts = timestamps[i]
            if hasattr(ts, "strftime"):
                ticks.append((ts.strftime(fmt), float(i)))
        return ticks

    def _maxTicksForPlot(self, timestamps, fmt):
        width = 820  # DPG versiyonunda gercek plot genisligi okunuyordu (dpg.get_item_rect_size);
        # implot'ta bu bilgi SADECE begin_plot acikken implot.get_plot_limits
        # cagirisindan SONRA (setup lock) guvenilir olurdu, ki o zaman zaten
        # tick kurulumu icin COK GEC olur (bkz. sinif docstring'i) - bu yuzden
        # sabit bir makul varsayilan kullanilir (DPG versiyonundaki 820 fallback
        # ile ayni).
        sample = timestamps[0] if timestamps else None
        text = sample.strftime(fmt) if hasattr(sample, "strftime") else fmt
        longest = max((len(line) for line in text.split("\n")), default=len(text))
        labelWidth = longest * 7 + 20
        return max(3, min(12, int(width / max(labelWidth, 1))))
