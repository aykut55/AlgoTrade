# ChartStudy: Indicator Dashboard
# Bu dosyayı açık plotter içindeki Script Panel'de değiştirip Run'a basabilirsiniz.
# Her Run, indikatörleri mevcut OHLC verisinden yeniden hesaplar ve panelleri yeniden kurar.

data = getattr(gm, "currentPreparedData", None)
if data is None or not data.close:
    raise RuntimeError("Önce ChartStudy OHLC bundle'ı yüklenmelidir.")

# Kullanıcının değiştirebileceği bölüm -----------------------------------------
EMA_PERIODS = [5, 8, 13, 21, 50, 100, 200]
MACD_PERIODS = (12, 26, 9)
RSI_PERIODS = [7, 14, 21]
STOCHASTIC_PERIODS = (14, 3)
MOMENTUM_PERIODS = [10, 20]

PANELS = [
    # Aynı panelde kaç seri olacağına kullanıcı karar verir.
    {"id": 0, "name": "OHLC + EMA", "height": 430},
    {"id": 1, "name": "MACD", "height": 220},
    {"id": 2, "name": "RSI 7 / 14 / 21", "height": 220, "range": (0.0, 100.0)},
    {"id": 3, "name": "Stochastic", "height": 220, "range": (0.0, 100.0)},
    {"id": 4, "name": "Momentum", "height": 220},
]
# -----------------------------------------------------------------------------

im = IndicatorManager(data.xs, data.open, data.high, data.low,
                      data.close, data.volume, data.size)
intraday = bool(data.meta.get("intraday", True))
symbol = str(data.meta.get("symbol", "OHLC"))

def full_length(values):
    # Yetersiz bar varsa plotter'a uzunluğu bozuk bir seri gönderme.
    return values if len(values) == len(data.xs) else [float("nan")] * len(data.xs)

pm.deleteAllPanels()
pool.clear()

def make_panel(spec, sync_id):
    panel = pm.createPanel(spec["name"], spec["name"])
    panel.setHeight(int(spec.get("height", 220)))
    panel.setYSyncId(sync_id)
    if spec.get("range"):
        panel.yFixedRange = tuple(spec["range"])
        panel.ySyncMode = "fixedRange"
    pm.addPanel(panel)
    return panel

panels = {spec["id"]: make_panel(spec, i) for i, spec in enumerate(PANELS)}

ohlc = panels[0]
ohlc.setCandleData(opens=data.open, highs=data.high, lows=data.low,
                   closes=data.close, volumes=data.volume, sizes=data.size,
                   dateTime=data.timestamps, name=symbol, dataId=0,
                   intraday=intraday)
for data_id, period in enumerate(EMA_PERIODS, start=1):
    ohlc.addData(data_id, f"EMA {period}", "line", data.xs, full_length(im.ema(period)),
                 timestamps=data.timestamps, intraday=intraday)

macd_line, signal_line, histogram = im.macd(*MACD_PERIODS)
macd = panels[1]
macd.addData(1, "MACD", "line", data.xs, full_length(macd_line), timestamps=data.timestamps, intraday=intraday)
macd.addData(2, "Signal", "line", data.xs, full_length(signal_line), timestamps=data.timestamps, intraday=intraday)
macd.addData(3, "Histogram", "line", data.xs, full_length(histogram), timestamps=data.timestamps, intraday=intraday)
macd.addHline(0.0, label="Zero")

rsi_panel = panels[2]
for data_id, period in enumerate(RSI_PERIODS, start=1):
    rsi_panel.addData(data_id, f"RSI {period}", "line", data.xs, full_length(im.rsi(period)),
                      timestamps=data.timestamps, intraday=intraday)
rsi_panel.addHline(30.0, label="Oversold")
rsi_panel.addHline(70.0, label="Overbought")

stoch_k, stoch_d = im.stochastic(*STOCHASTIC_PERIODS)
stochastic = panels[3]
stochastic.addData(1, "%K", "line", data.xs, full_length(stoch_k), timestamps=data.timestamps, intraday=intraday)
stochastic.addData(2, "%D", "line", data.xs, full_length(stoch_d), timestamps=data.timestamps, intraday=intraday)
stochastic.addHline(20.0, label="Oversold")
stochastic.addHline(80.0, label="Overbought")

momentum_panel = panels[4]
for data_id, period in enumerate(MOMENTUM_PERIODS, start=1):
    momentum_panel.addData(data_id, f"Momentum {period}", "line", data.xs,
                           full_length(im.momentum(period)), timestamps=data.timestamps,
                           intraday=intraday)
momentum_panel.addHline(0.0, label="Zero")

for panel in pm.iterateAllPanels():
    pm.drawPanel(panel.id)
    pm.drawPanelData(panel.id)

pm.setXAxisMode("datetime")
gm.currentView = {"mode": "chart_study", "study": "IndicatorDashboardChartStudy"}
gm.leftMenuPanel.refresh()
gm.poolPanel.refresh()
gm._refreshActivePanelCombo()
print("[ChartStudy] Indicator Dashboard yeniden hesaplandı ve çizildi.")
