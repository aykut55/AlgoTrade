import numpy as np


def attachPctChangeUpdater(pm, panel, dataId, ohlcPanel, swingController=None):
    """Update the series from the selected A/B range or the active separator segments."""
    if panel is None or dataId is None or ohlcPanel is None:
        return
    candle = next((d for d in ohlcPanel.dataList if d.dataType == "candle"), None)
    if candle is None:
        return

    def updatePctChangePanel(pm, panelId, p):
        data = p.getData(dataId)
        if data is None:
            return
        xs = candle._fullXs if candle._fullXs is not None else None
        closes = candle._fullClose if candle._fullClose is not None else None
        timestamps = candle._fullTimestamps if candle._fullTimestamps else candle.timestamps
        if xs is None or closes is None or len(xs) == 0:
            _clearPctChangeData(data)
            return

        # Swing modunda RangeMeasureTool suspend edilir (startIndex hep None) -
        # bu durumda SwingAnnotationController'in kendi A/B'si (710001/710002)
        # kaynak olur, o da yoksa separator moduna dusulur.
        measureTool = pm.getRangeMeasureTool()
        swingCtrl = swingController
        startIndex = endIndex = None
        measureEndIndex = (measureTool.getEffectiveEndIndex()
                           if measureTool is not None else None)
        if (measureTool is not None and measureTool.startIndex is not None
                and measureEndIndex is not None):
            startIndex, endIndex = measureTool.startIndex, measureEndIndex
        elif (swingCtrl is not None and swingCtrl._measureStartIndex is not None
                and swingCtrl._measureEndIndex is not None):
            startIndex, endIndex = swingCtrl._measureStartIndex, swingCtrl._measureEndIndex

        if startIndex is not None and endIndex is not None:
            pct = _pctChangeMeasureSegment(xs, closes, startIndex, endIndex)
        else:
            pct = _pctChangeSeparatorSegments(pm, xs, closes)

        if pct is None:
            _clearPctChangeData(data)
            return
        _applyPctChangeData(data, xs, pct, timestamps, candle.isIntraday)

    pm.addPanelDrawHook(panel.id, updatePctChangePanel)
    updatePctChangePanel(pm, panel.id, panel)


def _pctChangeMeasureSegment(arr, closeArr, startIndex, endIndex):
    """A/B arasindaki (A'ya gore) % serisi - TAM veri uzunlugunda, A-B
    DISI NaN. Hover/Index hizalamasi diger panellerle AYNI kalsin diye
    kismi/windowed bir dizi DEGIL - _buildHoverLabel (panelManager.py)
    hovered bar index'ini DOGRUDAN pozisyon olarak kullanir (src[idx]),
    kisaltilmis bir dizi bu hizalamayi bozardi (bkz. kullanici bulgusu:
    '% Change panelinde Change degeri fare ile degismiyor'). startIndex/
    endIndex RangeMeasureTool'dan ya da SwingAnnotationController'in kendi
    A/B'sinden (swing modunda RangeMeasureTool suspend edilir) gelebilir."""
    lo, hi = sorted((startIndex, endIndex))
    loPos = int(np.searchsorted(arr, lo))
    hiPos = min(int(np.searchsorted(arr, hi)) + 1, len(arr))
    if hiPos - loPos < 2:
        return None
    anchorPos = int(np.clip(np.searchsorted(arr, startIndex), 0, len(closeArr) - 1))
    anchorClose = float(closeArr[anchorPos])
    if not anchorClose:
        return None
    pct = np.full(len(arr), np.nan, dtype=np.float64)
    pct[loPos:hiPos] = (closeArr[loPos:hiPos] - anchorClose) / anchorClose * 100.0
    return pct

def _pctChangeSeparatorSegments(pm, arr, closeArr):
    """TUM veri boyunca, her separator segmentinin kendi ilk barina gore
    sifirlanan % serisi - tamamen vektorize (buyuk veri setinde de hizli).
    Hover hizalamasi icin TAM uzunlukta (bkz. _pctChangeMeasureSegment)."""
    _, starts = pm.getSeparatorInfo()
    if starts is None or len(starts) == 0:
        return None

    boundaryPositions = np.searchsorted(arr, starts)
    boundaryPositions = np.unique(np.clip(boundaryPositions, 0, len(arr) - 1))
    if len(boundaryPositions) == 0 or boundaryPositions[0] != 0:
        boundaryPositions = np.insert(boundaryPositions, 0, 0)

    positions = np.arange(len(arr))
    segIdx = np.searchsorted(boundaryPositions, positions, side="right") - 1
    segIdx = np.clip(segIdx, 0, len(boundaryPositions) - 1)
    anchorCloses = closeArr[boundaryPositions[segIdx]]
    with np.errstate(divide="ignore", invalid="ignore"):
        pct = np.where(anchorCloses != 0,
                      (closeArr - anchorCloses) / anchorCloses * 100.0,
                      np.nan)
    return pct

def _clearPctChangeData(data):
    """Gecerli bir segment yokken cagrilir. Panel diger paneller gibi
    SABIT/hep gorunur kalsin diye isVisible'a dokunulmaz - bos dizi
    PanelManager._drawLineOrBarSeries icindeki genel guard tarafindan
    sessizce atlanir (crash etmez, bkz. panelManager.py)."""
    data.xs, data.ys, data.timestamps = [], [], []
    data._fullXs, data._fullYs, data._fullTimestamps = None, None, []
    data.fullCount, data.lodCount = 0, 0
    data.dataCount, data.minY, data.maxY = 0, 0, 0

def _applyPctChangeData(data, xs, pct, timestamps, isIntraday):
    """xs/pct/timestamps TAM veri uzunlugunda (bkz. _pctChangeMeasureSegment/
    _pctChangeSeparatorSegments) - hover'in src[idx] ile dogrudan pozisyon
    olarak kullandigi global bar index'le hizali kalsin diye. Buyuk veri
    setinde (fullCount > LOD_ACTIVATION_THRESHOLD) cizim zaten _fullXs/
    _fullYs'ten LOD ile okunur, data.xs/data.ys (duz liste) hic okunmaz -
    bu yuzden HER FRAME 500k+ elemanli .tolist() cevirisi YAPILMAZ
    (performans). updateStats() de CAGRILMAZ - NaN icerdigi icin
    min(self.ys)/max(self.ys) guvenilmez; minY/maxY np.nanmin/nanmax ile
    hesaplanir."""
    data.isIntraday = bool(isIntraday)
    data._fullXs = xs
    data._fullYs = np.ascontiguousarray(pct, dtype=np.float64)
    data.fullCount = len(xs)
    data.lodCount = data.fullCount
    data.dataCount = data.fullCount
    data._fullTimestamps = timestamps
    data.timestamps = timestamps if timestamps is not None else []
    finite = data._fullYs[np.isfinite(data._fullYs)]
    if len(finite):
        data.minY = float(np.min(finite))
        data.maxY = float(np.max(finite))
    else:
        data.minY = data.maxY = 0.0
