// Signal asamasi - globals: Data (List<StockData>), Indicators (IndicatorManager)
// Varsayilan sinyal kaynagi Menu [5] ile tutarlidir:
// StrategyConfig.txt -> SimpleSuperTrendStrategy | v1 | Default
// period=10, multiplier=3.0, priceSource=Close, buy/sell mode=0, TP/SL kapali.
//
// return: object[] { signals, indicatorNames, indicatorValues }
//   signals        : TradeSignals[] (Data.Count uzunlugunda)
//   indicatorNames : string[] (OHLC panelinde cizilecek indikator adlari)
//   indicatorValues: object[] (indicatorNames ile ayni sirada double[] dizileri)
//
// Pozisyon ve ek filtre mantigi burada degildir; bkz. filter.csx.

const int period = 10;
const double multiplier = 3.0;

var close = Indicators.Trend.ResolvePriceSource(PriceSource.Close);
var superTrendResult = Indicators.Trend.SuperTrend(period, multiplier);
var superTrend = superTrendResult.SuperTrend;

if (superTrend.Length != close.Length)
    throw new InvalidOperationException(
        $"SuperTrend seri uzunlugu veriyle uyusmuyor: {superTrend.Length} != {close.Length}");

var signals = new TradeSignals[close.Length];

// SimpleSuperTrendStrategy v1'in buySignalModeIndex=0 / sellSignalModeIndex=0
// kuraliyla birebir ayni fiyat-SuperTrend kesisimleri.
for (int i = period; i < close.Length; i++)
{
    bool crossesAbove = close[i - 1] <= superTrend[i - 1] && close[i] > superTrend[i];
    bool crossesBelow = close[i - 1] >= superTrend[i - 1] && close[i] < superTrend[i];

    if (crossesAbove)
        signals[i] = TradeSignals.Buy;
    else if (crossesBelow)
        signals[i] = TradeSignals.Sell;
}

var indicatorNames = new[] { "SuperTrend (10, 3.0)" };
var indicatorValues = new object[] { superTrend };

return new object[] { signals, indicatorNames, indicatorValues };
