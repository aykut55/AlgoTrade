// Signal asamasi - globals: Data (List<StockData>), Indicators (IndicatorManager)
// return: object[] { signals, indicatorNames, indicatorValues }
//   signals        : TradeSignals[] (Data.Count uzunlugunda).
//                    Bu sablon Buy, Sell ve None uretir. TakeProfit,
//                    StopLoss, Flat ve Skip de kullanilabilir.
//   indicatorNames : string[] (OHLC panelinde cizilecek indikator adlari)
//   indicatorValues: object[] (her eleman bir double[], indicatorNames ile AYNI sirada)
// Not: pozisyon/filtre mantigi BURADA DEGIL - bkz. filter.csx (ayri asama).

var close = Indicators.Trend.ResolvePriceSource(PriceSource.Close);
var fast = Indicators.MA.Calculate(close, MAMethod.SIMPLE, 10);
var slow = Indicators.MA.Calculate(close, MAMethod.SIMPLE, 20);

var signals = new TradeSignals[close.Length];

for (int i = 1; i < close.Length; i++)
{
    if (double.IsNaN(fast[i]) || double.IsNaN(slow[i]) || double.IsNaN(fast[i - 1]) || double.IsNaN(slow[i - 1])) 
        continue;

    if (fast[i - 1] <= slow[i - 1] && fast[i] > slow[i]) 
        signals[i] = TradeSignals.Buy;
    else if (fast[i - 1] >= slow[i - 1] && fast[i] < slow[i]) 
        signals[i] = TradeSignals.Sell;
}

var indicatorNames = new[] { "Fast MA (10)", "Slow MA (20)" };
var indicatorValues = new object[] { fast, slow };

return new object[] { signals, indicatorNames, indicatorValues };
