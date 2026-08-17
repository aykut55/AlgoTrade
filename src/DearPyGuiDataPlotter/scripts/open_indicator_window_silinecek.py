# Hazir: gm, pm, pool, dpg, Panel, PanelData

# External indicator window script template.
#
# Bu script son yuklenen bundle datasina gm.currentPreparedData uzerinden erisir.
# Istersen mevcut indikator listelerinden seri secebilir, istersen
# computeCustomSeries() icinde yeni custom seri hesaplayabilirsin.

TITLE = "Signal Source Indicators"
SOURCE_PANEL_ID = None       # None: aktif panel -> OHLC panel -> ilk uygun panel
FOLLOW_SOURCE = True

# Bundle indikatorlerinden secilecek seriler. Bos ise prefix kurali kullanilir.
INDICATOR_NAMES = []
INDICATOR_PREFIXES = ["EMA"]  # Ornek: ["EMA"], ["MACD"], ["RSI"], ["Stoch"]

# Signal Step gibi bundle ozel serileri.
INCLUDE_SIGNAL_STEPS = False


ctx = {}


def finite(value):
    try:
        import math
        return math.isfinite(float(value))
    except (TypeError, ValueError):
        return False


def init():
    """Script context'ini hazirlar."""
    ctx.clear()
    ctx["data"] = getPreparedData()
    ctx["sourcePanelId"] = resolveSourcePanelId()
    ctx["series"] = []
    ctx["state"] = None
    return ctx["data"] is not None and ctx["sourcePanelId"] is not None


def compute():
    """Bundle datasindan/custon hesaplardan cizilecek serileri hazirlar."""
    data = ctx.get("data")
    if data is None:
        return False
    ctx["series"] = assignSeriesToWindow(data)
    return bool(ctx["series"])


def draw():
    """Hazirlanan serileri external indicator window'da cizer."""
    sourcePanelId = ctx.get("sourcePanelId")
    series = ctx.get("series") or []
    if sourcePanelId is None or not series:
        return False

    ctx["state"] = gm.externalIndicatorWindowManager.openIndicatorWindow(
        sourcePanelId=sourcePanelId,
        series=series,
        title=TITLE,
        followSource=FOLLOW_SOURCE,
    )
    return ctx["state"] is not None


def getPreparedData():
    """default.py son bundle'i yuklediginde gm.currentPreparedData olarak expose eder."""
    data = getattr(gm, "currentPreparedData", None)
    if data is None:
        print("currentPreparedData yok. Once default.py veya pipeline scripti ile data yukle.")
    return data


def indicatorNameAccepted(name):
    if INDICATOR_NAMES:
        return name in INDICATOR_NAMES
    upper = name.upper()
    return any(upper.startswith(prefix.upper()) for prefix in INDICATOR_PREFIXES)


def getIndicatorSeriesFromBundle(data):
    """Bundle icindeki indicatorNames/indicatorValues listesinden seri uretir."""
    out = []
    if data is None:
        return out

    for name, ys in zip(data.indicatorNames, data.indicatorValues):
        if not indicatorNameAccepted(name):
            continue
        out.append({
            "name": name,
            "xs": list(data.xs),
            "ys": [float(v) if finite(v) else float("nan") for v in ys],
        })
    return out


def getSignalSeriesFromBundle(data):
    """Signal Step gibi bundle ozel serilerini burada cizilebilir hale getir."""
    out = []
    if data is None:
        return out
    if INCLUDE_SIGNAL_STEPS and data.signalSteps:
        out.append({
            "name": "Signal Step",
            "xs": list(data.xs),
            "ys": [float(v) for v in data.signalSteps],
        })
    return out


def computeCustomSeries(data):
    """Yeni custom indikator/seri hesaplama yeri.

    Ornek:
        diff = [a - b for a, b in zip(ema50, ema100)]
        return [{"name": "EMA50-EMA100", "xs": data.xs, "ys": diff}]

    Simdilik bos; ihtiyaca gore buraya hesap ekle.
    """
    return []


def assignSeriesToWindow(data):
    """Bu pencerede cizilecek tum serileri burada net olarak assign ediyoruz."""
    series = []
    series += getIndicatorSeriesFromBundle(data)
    series += getSignalSeriesFromBundle(data)
    series += computeCustomSeries(data)
    return series


def panelHasAnyData(panel):
    return panel is not None and any(True for _ in panel.iterateAllData())


def resolveSourcePanelId():
    if SOURCE_PANEL_ID is not None:
        return SOURCE_PANEL_ID

    activeId = pm.getActivePanelId()
    activePanel = pm.getPanel(activeId)
    if panelHasAnyData(activePanel):
        return activeId

    for panel in pm.iterateAllPanels():
        if panel.name == "OHLC" and panelHasAnyData(panel):
            return panel.id

    for panel in pm.iterateAllPanels():
        if panelHasAnyData(panel):
            return panel.id
    return None


def run():
    """SDK benzeri ana akis: init -> compute -> draw."""
    if not init():
        if ctx.get("data") is None:
            return
        print("Indicator Window: uygun source panel bulunamadi.")
        return

    if not compute():
        print("Indicator Window: cizilecek seri bulunamadi.")
        return

    if not draw():
        print("Indicator Window: window acilamadi.")
        return

    names = ", ".join(item["name"] for item in ctx["state"]["series"])
    print(f"Indicator Window acildi: {names}")


run()
