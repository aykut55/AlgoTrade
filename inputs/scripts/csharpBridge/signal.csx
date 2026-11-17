// Signal asamasi - globals: Data (List<StockData>), Indicators (IndicatorManager)
// return: object[] { signals, indicatorNames, indicatorValues }
//   signals        : int[] (Data.Count uzunlugunda). Kodlar: 0=yok, 1=AL(Buy),
//                    -1=SAT(Sell), 3=KarAl(TakeProfit), 4=ZararKes(StopLoss),
//                    5=FlatOl, 6=PasGec(Skip) - bu sablon sadece 1/-1/0 uretir,
//                    diger kodlari (KarAl/ZararKes/FlatOl/PasGec) burada da
//                    dogrudan uretebilirsin; genelde filter.csx'te uretilir.
//   indicatorNames : string[] (OHLC panelinde cizilecek indikator adlari)
//   indicatorValues: object[] (her eleman bir double[], indicatorNames ile AYNI sirada)
// Not: pozisyon/filtre mantigi BURADA DEGIL - bkz. filter.csx (ayri asama).
var close = Indicators.Trend.ResolvePriceSource(PriceSource.Close);
var fast = Indicators.MA.Calculate(close, MAMethod.EMA, 8);
var slow = Indicators.MA.Calculate(close, MAMethod.EMA, 21);

var signals = new int[close.Length];
for (int i = 1; i < close.Length; i++)
{
    if (double.IsNaN(fast[i]) || double.IsNaN(slow[i]) || double.IsNaN(fast[i - 1]) || double.IsNaN(slow[i - 1])) continue;
    if (fast[i - 1] <= slow[i - 1] && fast[i] > slow[i]) signals[i] = 1;   // AL
    else if (fast[i - 1] >= slow[i - 1] && fast[i] < slow[i]) signals[i] = -1; // SAT
}

var indicatorNames = new[] { "EMA Fast (8)", "EMA Slow (21)" };
var indicatorValues = new object[] { fast, slow };

return new object[] { signals, indicatorNames, indicatorValues };
