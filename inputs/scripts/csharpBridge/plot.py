# Plot asamasi - hazir: pm (PanelManager), tsr (TradeSignalRenderer),
# Panel, PanelData, bridge:
#   opens/highs/lows/closes/volumes/sizes/timestamps/signals/symbol/period
#   trader       - Calculate asamasindan donen AlgoTrader (henuz calismadiysa None)
#   traderSeries - trader varsa dict: pnl/pnlPct/returnFiyat/returnNet/
#                   returnPct/returnPctNet/balance/balanceNet/commission/signalStep
#
# Panel seti AlgoTrade (orijinal repo) TradeDataBundleConverter.BuildAndWriteView
# ile ayni % Change hesabini kullanir. PnL ve Return panelleri Calculate
# tamamlandiktan sonra eklenir.

import numpy as np

from src.plotting.metricGrid import attachConfiguredMetricGrid
from src.plotting.pctChangePanel import attachPctChangeUpdater

CYAN = (60, 200, 255, 255)
YELLOW = (255, 255, 0, 255)

pm.deleteAllPanels()
xs = list(range(len(bridge.closes)))

# dataId=0 ZORUNLU: TradeSignalRenderer.draw() OHLC panelinin candle serisini
# hep dataId=0 varsayarak arar (bkz. BuildAndWriteView yorumu).
ohlcPanel = pm.createPanel("OHLC", f"OHLC ({bridge.symbol})")
ohlcPanel.setHeight(380)
pm.addPanel(ohlcPanel)
ohlcPanel.setCandleData(
    opens=bridge.opens, highs=bridge.highs, lows=bridge.lows, closes=bridge.closes,
    volumes=bridge.volumes, sizes=bridge.sizes, dateTime=bridge.timestamps,
    name=bridge.symbol, dataId=0, intraday=True)

# AL/SAT sinyalini ureten MA/indikator serileri - Signal asamasindan (signal.csx
# indicatorNames/indicatorValues) - dataId=0 candle oldugu icin 1'den baslar.
for idx, (name, values) in enumerate(bridge.signalIndicators, start=1):
    ohlcPanel.addData(idx, name, "line", xs, values, timestamps=bridge.timestamps, intraday=True)

pctChangePanel = pm.createPanel("% Change", "% Change")
pctChangePanel.setHeight(220)
pctChangePanel.setYLabel("% Change")
pm.addPanel(pctChangePanel)
pctChangePanel.addData(1, "Change %", "line", [], [], intraday=True, color=YELLOW)

ts = bridge.traderSeries

if ts is not None:
    # Gercek motordan (SingleTrader) gelen signalStep - en dogru pozisyon durumu.
    signalSteps = ts["signalStep"]
else:
    # Henuz Calculate calistirilmadi - sparse event (1/-1/0) -> forward-filled state.
    signalSteps = []
    state = 0
    for value in bridge.signals:
        if value == 1:
            state = 1
        elif value == -1:
            state = -1
        signalSteps.append(state)

signalPanel = pm.createPanel("Signals", "Signals (AL/SAT/FLAT)")
signalPanel.setHeight(200)
signalPanel.setYLabel("Signals")
pm.addPanel(signalPanel)
signalPanel.addData(1, "Signal Step", "line", xs, signalSteps,
                    timestamps=bridge.timestamps, intraday=True, color=CYAN)
signalPanel.yFixedRange = (-2, 2)
signalPanel.ySyncMode = "fixedRange"

if ts is not None:
    pnlPanel = pm.createPanel("PnL Price", "PnL")
    pnlPanel.setHeight(220)
    pnlPanel.setYLabel("PnL Price")
    pm.addPanel(pnlPanel)
    pnlPanel.addData(1, "PnL", "line", xs, ts["pnl"], timestamps=bridge.timestamps, intraday=True, color=YELLOW)

    pnlPctPanel = pm.createPanel("PnL Price %", "PnL %")
    pnlPctPanel.setHeight(220)
    pnlPctPanel.setYLabel("PnL Price %")
    pm.addPanel(pnlPctPanel)
    pnlPctPanel.addData(1, "PnL %", "line", xs, ts["pnlPct"], timestamps=bridge.timestamps, intraday=True, color=YELLOW)

    # Kullanıcı tercihi: Return/Net Return ve Return %/Net Return % artık AYRI panel
    # (eskiden ikişer seri tek panelde birleşikti). Sıra: Net Return, Net Return %, Return, Return %.
    netReturnPanel = pm.createPanel("Net Return", "Net Return")
    netReturnPanel.setHeight(220)
    netReturnPanel.setYLabel("Net Return")
    pm.addPanel(netReturnPanel)
    netReturnPanel.addData(1, "Net Return", "line", xs, ts["returnNet"],
                           timestamps=bridge.timestamps, intraday=True, color=YELLOW)

    netReturnPctPanel = pm.createPanel("Net Return %", "Net Return %")
    netReturnPctPanel.setHeight(220)
    netReturnPctPanel.setYLabel("Net Return %")
    pm.addPanel(netReturnPctPanel)
    netReturnPctPanel.addData(1, "Net Return %", "line", xs, ts["returnPctNet"],
                              timestamps=bridge.timestamps, intraday=True, color=YELLOW)

    returnPanel = pm.createPanel("Return", "Return")
    returnPanel.setHeight(220)
    returnPanel.setYLabel("Return")
    pm.addPanel(returnPanel)
    returnPanel.addData(1, "Return", "line", xs, ts["returnFiyat"],
                        timestamps=bridge.timestamps, intraday=True, color=YELLOW)

    returnPctPanel = pm.createPanel("Return %", "Return %")
    returnPctPanel.setHeight(220)
    returnPctPanel.setYLabel("Return %")
    pm.addPanel(returnPctPanel)
    returnPctPanel.addData(1, "Return %", "line", xs, ts["returnPct"],
                           timestamps=bridge.timestamps, intraday=True, color=YELLOW)

# En son panel: AL/SAT uretn indikatorler (default.py'deki "Indicators" paneli
# ile ayni fikir) - SADECE kullanilan indikator serileri, AL/SAT isareti YOK
# (o isaretler OHLC panelinde zaten var, bkz. tsr.draw asagida).
if bridge.signalIndicators:
    indicatorsPanel = pm.createPanel("Indicators", "Indicators")
    indicatorsPanel.setHeight(260)
    pm.addPanel(indicatorsPanel)
    for idx, (name, values) in enumerate(bridge.signalIndicators, start=1):
        indicatorsPanel.addData(idx, name, "line", xs, values, timestamps=bridge.timestamps, intraday=True)

for panel in pm.iterateAllPanels():
    pm.drawPanel(panel.id)
    pm.drawPanelData(panel.id)

# TradeSignalRenderer sadece "AL"/"SAT"/"FLAT" tanir - KarAl(3)/ZararKes(4)/
# FlatOl(5) hepsi gorsel olarak "pozisyon kapandi" demek, FLAT'e eslenir.
signalLabels = [{1: "AL", -1: "SAT", 3: "FLAT", 4: "FLAT", 5: "FLAT"}.get(value) for value in bridge.signals]
tsr.draw(ohlcPanel.id, 0, signalLabels, showSignals=False, showLevelLines=True, colorBars=True)
pm.setXAxisMode("datetime")

# Gun ayraci - default.py'deki addSeparators("day") ile AYNI fikir: her gunun
# ilk barina, TUM panellerde dikey cyan cizgi. pm.render() bunu her frame
# otomatik ciziyor (bkz. panelManager.setSeparatorInfo docstring) - burada
# sadece gun gecis bar-index'lerini hesaplayip bildiriyoruz.
_dayStarts = []
for i in range(1, len(bridge.timestamps)):
    prevTs, curTs = bridge.timestamps[i - 1], bridge.timestamps[i]
    if curTs.date() != prevTs.date():
        _dayStarts.append(float(i))
if _dayStarts:
    pm.setSeparatorInfo("day", np.asarray(_dayStarts, dtype=np.float64),
                        color=(0.0, 1.0, 1.0, 0.6), weight=1.0, maxVisible=2000)

attachPctChangeUpdater(pm, pctChangePanel, 1, ohlcPanel)

# Metric layout and target panels: inputs/configs/OhlcMetricCells.json
stats = bridge.trader.SingleTrader.statistics if bridge.trader is not None else None
attachConfiguredMetricGrid(pm, ohlcPanel, lambda key: getattr(stats, key, None))
