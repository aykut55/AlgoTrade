import bisect

import numpy as np
from imgui_bundle import implot


def _toImVec4(color):
    """DPG-style 0-255 RGBA tuple -> implot'un bekledigi 0-1 float ImVec4Like tuple."""
    r, g, b, a = color
    return (r / 255.0, g / 255.0, b / 255.0, a / 255.0)


class TradeSignalRenderer:
    """Draw trade-signal overlays for one OHLC series - implot/imgui_bundle port
    of DearPyGuiDataPlotter's plotting/tradeSignalRenderer.py (the authoritative
    `TradeSignalRenderer` class only; `_DeprecatedTradeSignalRenderer` is dead
    code there too and is NOT ported).

    3 bagimsiz gorsel katman (DPG versiyonuyla AYNI, ayni flag'ler):
      1) colorBars: OHLC bar'lari sinyal durumuna (AL/SAT/FLAT) gore renklenir
         (varsayilan bull/bear yesil/kirmizi yerine).
      2) showSignals: her sinyal olayinda kucuk bir harf (A/S/F).
      3) showLevelLines: bir AL run'inin DUSUGUNDEN (veya SAT run'inin
         YUKSEGINDEN) run'in sonuna kadar yatay bir cizgi, duruma gore gruplu.

    PERFORMANS DUZELTMESI (2026-11-07): colorBars ONCEDEN kendi ayri
    renderInPlot() cizim gecisinde HER run'u (bu veri setinde 31636 run,
    ortalama run uzunlugu ~18 bar) tek tek, PanelManager'in ANA candle
    LOD'undan (PanelData.getLodSlice, en fazla LOD_MAX_POINTS=3000 bar)
    TAMAMEN BAGIMSIZ sekilde cizdiriyordu - run basina deicmation (eski
    RUN_LOD_MAX_POINTS) run'lar KISA oldugu icin HICBIR ZAMAN devreye
    girmiyordu, yani 'Full Data' gibi genis bir gorunur aralikta colorBars
    ACIKKEN panel BASINA yuz binlerce implot cizim cagrisi/frame yapiliyor,
    bu da arayuzun kasilmasina/donmasina sebep oluyordu.

    Duzeltme: src/PythonPlotter/data_plotter_img_bundle.py'nin KANITLI
    calisan teknigi benimsendi (mimari/tasarim DEGIL, salt teknik: sinyal
    rengini AYRI bir cizim gecisinde DEGIL, ANA candle cizim donguSUNUN
    ICINDE, HER BAR icin signals_array[bar_index]'ten okuyarak uygulamak) -
    artik colorBars PanelManager._drawCandleSeries'in ZATEN LOD-decimate
    edilmis (en fazla LOD_MAX_POINTS bar) ana cizim donguSUNUN icinde,
    getDenseStates()'ten okunan DENSE (her orijinal bar index'i icin tek
    durum) bir numpy dizisiyle uygulaniyor - boylece colorBars'in maliyeti
    ARTIK gorunur aralik/run sayisindan BAGIMSIZ, ana candle cizimiyle
    AYNI (zaten var olan) LOD sinirina tabi. Bu dosyada SADECE showSignals/
    showLevelLines (candle cizimi OLMAYAN, daha hafif overlay'ler) kaliyor,
    onlar da artik bisect ile SADECE gorunur araliga kirpiliyor (eskiden
    showSignals TUM gorunur bar'lari tek tek tarayip signals[i] sorguluyordu
    - 'Full Data' gorunumunde 562894 elemanlik bir Python dongusu, HER
    FRAME; artik onceden hesaplanmis siralı event-index listesinde bisect
    ile O(log n) + sadece gorunur event sayisi kadar islem).
    """

    LETTER_BY_SIGNAL = {"AL": "A", "SAT": "S", "FLAT": "F"}
    COLOR_BY_SIGNAL = {
        "AL": (40, 200, 90, 255),
        "SAT": (220, 70, 70, 255),
        "FLAT": (225, 225, 225, 255),
    }
    MAX_SIGNAL_EVENTS = 50000  # TOPLAM (veri seti capinda) bu kadar sinyal olayi varsa harf/cizgi hic cizilmez (bar boyama calismaya devam eder)
    MAX_VISIBLE_SIGNAL_LETTERS = 400  # GORUNUR pencerede bundan fazla harf-olayi varsa stride ile seyreltilir (okunamaz/yavaş olurdu)
    MAX_VISIBLE_LEVEL_RUNS = 1500  # GORUNUR pencerede bundan fazla AL/SAT run'i varsa stride ile seyreltilir

    def __init__(self, panelManager):
        self._panelManager = panelManager
        self._overlays = {}  # {(panelId, dataId): {...}} - bkz. draw()
        self._lastTarget = None  # (panelId, dataId) - bkz. setActiveOhlcVisible
        self._visible = {}  # {(panelId, dataId): bool}

    def draw(self, panelId, dataId, signals, showSignals=True, showLevelLines=True, colorBars=True):
        """panelId'deki panelin dataId'li (OHLC) serisine signals (data ile AYNI
        uzunlukta, sinyal OLMAYAN barlarda None/"" olan SEYREK liste) listesine
        gore overlay HAZIRLAR (hesaplar + saklar) - gercek cizim her frame
        renderInPlot() (showSignals/showLevelLines) + PanelManager'in candle
        donguSUnun getDenseStates() okuyan kismindan (colorBars) yapilir.
        Tekrar cagrilabilir."""
        self._lastTarget = (panelId, dataId)
        panel, data = self._getPanelData(panelId, dataId)
        if data is None:
            self._overlays.pop((panelId, dataId), None)
            return

        n = min(len(signals or []), len(data.xs), len(data.open), len(data.high),
                len(data.low), len(data.close))
        if n <= 0:
            self._overlays.pop((panelId, dataId), None)
            return

        runs = self._computeRuns(signals, n)

        # Dense per-bar durum dizisi (run'lardan DOLDURULUR) - colorBars'in
        # PanelManager'in LOD-decimate edilmis ana candle dongusu icinden
        # O(1) per-bar lookup ile okuyabilmesi icin (bkz. getDenseStates).
        denseStates = np.empty(n, dtype=object)
        for state, start, end in runs:
            denseStates[start:end] = state

        signalEvents = [i for i in range(n) if signals[i]]
        skipEvents = len(signalEvents) > self.MAX_SIGNAL_EVENTS
        if skipEvents:
            print(f"[TradeSignalRenderer] {len(signalEvents)} signals; letters/lines skipped.")

        # Seviye-cizgisi runlari (sadece AL/SAT, FLAT haric) - start'a gore SIRALI
        # (runs zaten start-sirali kuruluyor, bkz. _computeRuns), bisect icin hazir.
        levelRuns = [(state, start, end) for state, start, end in runs if state in ("AL", "SAT")]
        levelRunStarts = [r[1] for r in levelRuns]

        self._overlays[(panelId, dataId)] = {
            "signals": signals,
            "n": n,
            "denseStates": denseStates,
            "eventIndices": signalEvents,
            "levelRuns": levelRuns,
            "levelRunStarts": levelRunStarts,
            "showSignals": bool(showSignals) and not skipEvents,
            "showLevelLines": bool(showLevelLines) and not skipEvents,
            "colorBars": bool(colorBars),
        }
        self.setOhlcVisible(panelId, dataId, self._visible.get((panelId, dataId), True))
        print(f"[TradeSignalRenderer] panel={panelId} data={dataId}: "
              f"{len(signalEvents)} signals, {len(runs)} runs, colorBars={colorBars}")

    def _getPanelData(self, panelId, dataId):
        panel = self._panelManager.getPanel(panelId)
        data = panel.getData(dataId) if panel is not None else None
        if data is None or data.dataType != "candle":
            return panel, None
        return panel, data

    def _computeRuns(self, signals, n):
        """signals[0..n)'i AYNI durumda kalan ardisik [start, end) bar araliklarina
        boler (DPG versiyonuyla birebir ayni algoritma, degismedi)."""
        runs = []
        currentState = None
        runStart = 0
        for i in range(n):
            state = signals[i]
            if not state:
                continue
            if currentState is not None:
                runs.append((currentState, runStart, i))
            elif runStart < i:
                runs.append((None, runStart, i))
            currentState = state
            runStart = i
        if runStart < n:
            runs.append((currentState, runStart, n))
        return runs

    # ------------------------------------------------------- colorBars erisimi

    def isColorBarsActive(self, panelId, dataId):
        """PanelManager'in candle cizim dongusu HER FRAME, candle cizmeye
        baslamadan ONCE bunu cagirip True ise getDenseStates()'ten dense
        diziyi alir ve her LOD-decimate edilmis bar icin rengi ORADAN okur -
        artik AYRI bir cizim gecisi/suppress mekanizmasi YOK (bkz. sinif
        docstring'indeki PERFORMANS DUZELTMESI)."""
        overlay = self._overlays.get((panelId, dataId))
        return bool(overlay and overlay["colorBars"] and self._visible.get((panelId, dataId), True))

    def getDenseStates(self, panelId, dataId):
        """colorBars aktifse (bkz. isColorBarsActive) o seri icin her ORIJINAL
        bar index'ine karsilik gelen durumu ("AL"/"SAT"/"FLAT"/None) tasiyan
        numpy object dizisini dondurur - PanelManager._drawCandleSeries bunu
        SADECE kendi ZATEN LOD-decimate ettigi (en fazla LOD_MAX_POINTS) bar
        orneklerinde indexler, maliyet gorunur aralik/run sayisindan bagimsizdir.
        Aktif degilse None."""
        if not self.isColorBarsActive(panelId, dataId):
            return None
        return self._overlays[(panelId, dataId)]["denseStates"]

    def getColorBySignal(self):
        """PanelManager'in getDenseStates()'ten okudugu durum string'ini
        (AL/SAT/FLAT) gercek implot rengine (0-1 float RGBA) cevirmesi icin."""
        return {state: _toImVec4(c) for state, c in self.COLOR_BY_SIGNAL.items()}

    # ------------------------------------------------------------ per-frame çizim

    def renderInPlot(self, panelId, dataId, xMin=None, xMax=None):
        """PanelManager'in panel-cizim dongusu tarafindan, O PANELIN implot.
        begin_plot/end_plot bloğu ACIKKEN, HER FRAME cagrilmalidir. SADECE
        showSignals (harfler) + showLevelLines (seviye cizgileri) cizer -
        colorBars artik PanelManager'in kendi candle dongusunde uygulaniyor
        (bkz. getDenseStates). xMin/xMax verilirse (ONERILEN: panelin SU ANKI
        gorunur x eksen limitleri) harfler/cizgiler SADECE o bar-index
        araligina gore seyreltilir/kirpilir (bkz. sinif docstring'i)."""
        overlay = self._overlays.get((panelId, dataId))
        if overlay is None or not self._visible.get((panelId, dataId), True):
            return
        panel, data = self._getPanelData(panelId, dataId)
        if data is None:
            return

        n = overlay["n"]
        loIdx = 0 if xMin is None else max(0, int(np.floor(xMin)))
        hiIdx = (n - 1) if xMax is None else min(n - 1, int(np.ceil(xMax)))
        if hiIdx < loIdx:
            return

        if overlay["showSignals"]:
            self._drawSignalLetters(data, overlay["signals"], overlay["eventIndices"], loIdx, hiIdx)
        if overlay["showLevelLines"]:
            self._drawLevelLines(data, overlay["levelRuns"], overlay["levelRunStarts"], n, loIdx, hiIdx)

    def _drawSignalLetters(self, data, signals, eventIndices, loIdx, hiIdx):
        """eventIndices (SIRALI, TUM veri setindeki sinyal-olayi bar index'leri)
        uzerinde bisect ile SADECE [loIdx,hiIdx] GORUNUR araligina denk
        gelenleri bulur (tam-aralik taramasi DEGIL) - sonra o alt-liste
        MAX_VISIBLE_SIGNAL_LETTERS'i asarsa stride ile seyreltilir (okunabilirlik
        + performans)."""
        lo = bisect.bisect_left(eventIndices, loIdx)
        hi = bisect.bisect_right(eventIndices, hiIdx)
        visible = eventIndices[lo:hi]
        if len(visible) > self.MAX_VISIBLE_SIGNAL_LETTERS:
            step = -(-len(visible) // self.MAX_VISIBLE_SIGNAL_LETTERS)
            visible = visible[::step]
        for i in visible:
            state = signals[i]
            if not state:
                continue
            if state == "AL":
                y, offset = data.low[i], (0, 16)
            elif state == "SAT":
                y, offset = data.high[i], (0, -16)
            else:
                y, offset = data.close[i], (0, 0)
            letter = self.LETTER_BY_SIGNAL.get(state, "?")
            color = _toImVec4(self.COLOR_BY_SIGNAL.get(state, (255, 255, 255, 255)))
            implot.annotation(float(data.xs[i]), float(y), color, offset, False, letter)

    def _drawLevelLines(self, data, levelRuns, levelRunStarts, n, loIdx, hiIdx):
        """levelRunStarts (SIRALI run-baslangic index'leri) uzerinde bisect ile
        SADECE [loIdx,hiIdx] ile kesisebilecek run'lara (baslangici hiIdx'ten
        once olanlar) bakar - TUM run listesini (veri setinde onbinlerce run
        olabilir) HER FRAME taramaz. Alt-kume hala MAX_VISIBLE_LEVEL_RUNS'i
        asarsa stride ile seyreltilir."""
        hiPos = bisect.bisect_right(levelRunStarts, hiIdx)
        loPos = bisect.bisect_left(levelRunStarts, loIdx)
        # Include the run that started before the visible range but still
        # crosses its left edge. Most importantly, filter to intersecting
        # runs BEFORE applying the display cap; sampling the complete history
        # first could discard nearly every run in a zoomed viewport.
        candidates = [
            run for run in levelRuns[max(0, loPos - 1):hiPos]
            if run[2] > loIdx
        ]
        if len(candidates) > self.MAX_VISIBLE_LEVEL_RUNS:
            step = -(-len(candidates) // self.MAX_VISIBLE_LEVEL_RUNS)
            candidates = candidates[::step]

        grouped = {"AL": ([], []), "SAT": ([], [])}
        for state, start, end in candidates:
            clipStart, clipEnd = max(start, loIdx), min(end, hiIdx + 1)
            if clipEnd <= clipStart:
                continue
            level = data.low[start] if state == "AL" else data.high[start]
            xStart = data.xs[clipStart]
            xEnd = data.xs[min(clipEnd - 1, n - 1)]
            xs, ys = grouped[state]
            xs.extend([xStart, xEnd, float("nan")])
            ys.extend([level, level, float("nan")])

        for state, (xs, ys) in grouped.items():
            if not xs:
                continue
            color = _toImVec4(self.COLOR_BY_SIGNAL[state])
            implot.plot_line(f"##level_{state.lower()}", np.asarray(xs, dtype=np.float64),
                              np.asarray(ys, dtype=np.float64),
                              spec=implot.Spec(line_color=color, line_weight=2.5))

    # ------------------------------------------------------------- görünürlük

    def clearAll(self):
        self._overlays.clear()

    def setOhlcVisible(self, panelId, dataId, visible):
        """OHLC candle gorselini VE onunla birlikte cizilen HER SEYI (renk,
        harfler, seviye cizgileri) ACIKCA gosterir/gizler - DPG versiyonundaki
        ayni isimli metodun davranisi (ImPlot/DPG legend click-to-hide
        Python'a yansimiyor, bu NEDENLE ayri bir kontrol gerekiyor)."""
        self._visible[(panelId, dataId)] = bool(visible)

    def toggleOhlcVisible(self, panelId, dataId):
        self.setOhlcVisible(panelId, dataId, not self._visible.get((panelId, dataId), True))

    def setActiveOhlcVisible(self, visible):
        """setOhlcVisible'i EN SON draw() cagrisinin hedefine uygular."""
        if self._lastTarget is None:
            return
        self.setOhlcVisible(*self._lastTarget, visible)
