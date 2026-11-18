// Signal asamasi - globals: Data (List<StockData>), Indicators (IndicatorManager)
// return: object[] { signals, indicatorNames, indicatorValues }
//   signals        : TradeSignals[] (Data.Count uzunlugunda).
//                    Strateji Buy, Sell veya Flat uretebilir. Kural yoksa None kalir.
//                    Bu SMA sablonu yalniz Buy ve Sell icin kural tanimlar.
//                    TakeProfit, StopLoss ve Skip filter.csx icinde degerlendirilir.
//   indicatorNames : string[] (OHLC panelinde cizilecek indikator adlari)
//   indicatorValues: object[] (her eleman bir double[], indicatorNames ile AYNI sirada)
// Not: pozisyon/filtre mantigi BURADA DEGIL - bkz. filter.csx (ayri asama).

using static AlgoTrade.Core.Trading.Utils.Utils;

var close = Indicators.Trend.ResolvePriceSource(PriceSource.Close);
var fast = Indicators.MA.Calculate(close, MAMethod.SIMPLE, 10);
var slow = Indicators.MA.Calculate(close, MAMethod.SIMPLE, 20);

var itemsCount = close.Length;
var signals = new TradeSignals[itemsCount];

for (int i = 1; i < itemsCount; i++)
{
    if (double.IsNaN(fast[i]) || double.IsNaN(slow[i]) || double.IsNaN(fast[i - 1]) || double.IsNaN(slow[i - 1]))
        continue;

    if (YukarıKesti(i, fast, slow))
        signals[i] = TradeSignals.Buy;
    else if (AsagiKesti(i, fast, slow))
        signals[i] = TradeSignals.Sell;
}

var indicatorNames = new[] { "Fast MA (10)", "Slow MA (20)" };
var indicatorValues = new object[] { fast, slow };

return new object[] { signals, indicatorNames, indicatorValues };
