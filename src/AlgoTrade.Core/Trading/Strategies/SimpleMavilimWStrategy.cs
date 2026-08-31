using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;
using static AlgoTrade.Core.Trading.Utils.Utils;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// MavilimW İndikatörü Stratejisi
    ///
    /// MavilimW Mantığı:
    /// - Fibonacci tabanlı ağırlıklı MA kombinasyonu, kendi Trendline (FAMA) hattıyla birlikte gelir
    /// - MavilimW/Trendline çifti MOST'un most/exmov çiftinin analogu; indikatör Close'a bağımlı, priceSource yok
    ///
    /// Parametreler:
    /// - param1/param2: hassasiyet parametreleri (varsayılan 3/5)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: Fiyat-MavilimW kırılımı     (fiyat MavilimW'yi yukarı/aşağı kesince)
    ///     1: MavilimW-Trendline kesişimi (MavilimW, Trendline'ı yukarı/aşağı kesince)
    ///     2: MavilimW slope flip         (MavilimW'nin kendi yönü dönünce)
    ///     3: MavilimW state              (fiyatın MavilimW'ye göre konumu - koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi     (fiyat MavilimW'den %bandThreshold'dan fazla uzaklaşınca)
    ///     5: Breakout + retest           (MavilimW kırılıp fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars           (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: Fiyat eğimi + state combo   (rejim: fiyat-MavilimW konumu + momentum: fiyatın N-bar eğimi)
    /// - exitModeIndex: takeProfit/stopLoss yöntemini seçer (Trader.karAlZararKes üzerinden):
    ///     0: Seviye, seviyeli   1: Yüzde, seviyeli   2: Seviye, tek seviye   3: Yüzde, tek seviye
    ///     4: Anlık kar/zarar fiyat seviyesi   5: Anlık kar/zarar yüzdesi
    /// - flatModeIndex/skipModeIndex/ruleModeIndex: PLACEHOLDER, henuz okunmuyor
    /// </summary>
    public class SimpleMavilimWStrategy : BaseStrategy
    {
        public override string Name => "Simple MavilimW Strategy";

        private int barCount;
        private double[]? openPrices;
        private double[]? highPrices;
        private double[]? lowPrices;
        private double[]? closePrices;
        private long[]? volumes;
        private long[]? lotSizes;
        private DateTime[]? dateTimes;
        private DateOnly[]? dates;
        private TimeOnly[]? times;
        private long[]? epochTimes;

        private readonly int param1;
        private readonly int param2;
        private readonly int signalModeIndex;
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        private double[]? mavilimW;
        private double[]? trendline;

        public SimpleMavilimWStrategy(int param1 = 3, int param2 = 5,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.param1          = param1;
            this.param2          = param2;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Param1"]         = param1;
            Parameters["Param2"]         = param2;
            Parameters["RuleModeIndex"]  = ruleModeIndex;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"]  = exitModeIndex;
            Parameters["FlatModeIndex"]  = flatModeIndex;
            Parameters["SkipModeIndex"]  = skipModeIndex;
        }

        public SimpleMavilimWStrategy(List<StockData> data, IndicatorManager indicators,
            int param1 = 3, int param2 = 5,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.param1          = param1;
            this.param2          = param2;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Param1"]         = param1;
            Parameters["Param2"]         = param2;
            Parameters["RuleModeIndex"]  = ruleModeIndex;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"]  = exitModeIndex;
            Parameters["FlatModeIndex"]  = flatModeIndex;
            Parameters["SkipModeIndex"]  = skipModeIndex;

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            barCount    = Indicators.GetDataCount();
            openPrices  = Indicators.GetOpenPrices();
            highPrices  = Indicators.GetHighPrices();
            lowPrices   = Indicators.GetLowPrices();
            closePrices = Indicators.GetClosePrices();
            volumes     = Indicators.GetVolume();
            lotSizes    = Indicators.GetLotSizes();
            dateTimes   = Indicators.GetDateTimes();
            dates       = Indicators.GetDates();
            times       = Indicators.GetTimes();
            epochTimes  = Indicators.GetEpochTimes();

            var mavilimWResult = Indicators.Trend.MavilimW(param1, param2);
            mavilimW  = mavilimWResult.MavilimW;
            trendline = mavilimWResult.Trendline;

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= mavilimW.Length    == barCount;
            allSeriesLengthsMatch &= trendline.Length   == barCount;
            allSeriesLengthsMatch &= openPrices.Length  == barCount;
            allSeriesLengthsMatch &= highPrices.Length  == barCount;
            allSeriesLengthsMatch &= lowPrices.Length   == barCount;
            allSeriesLengthsMatch &= closePrices.Length == barCount;
            allSeriesLengthsMatch &= volumes.Length     == barCount;
            allSeriesLengthsMatch &= lotSizes.Length    == barCount;
            allSeriesLengthsMatch &= dateTimes.Length   == barCount;
            allSeriesLengthsMatch &= dates.Length       == barCount;
            allSeriesLengthsMatch &= times.Length       == barCount;
            allSeriesLengthsMatch &= epochTimes.Length  == barCount;

            if (!allSeriesLengthsMatch)
            {
                throw new InvalidOperationException(
                    $"Seri uzunlukları uyuşmuyor (barCount={barCount}): " +
                    $"mavilimW={mavilimW.Length}, trendline={trendline.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                    $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                    $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false, sell = false, takeProfit = false, stopLoss = false, flat = false, skip = false;

            int minPeriod = 100; // MavilimW Fibonacci periyotları 250'ye kadar çıkabiliyor
            if (currentIndex < minPeriod)
                return TradeSignals.None;

            if (mavilimW == null || trendline == null || closePrices == null || mavilimW.Length == 0)
                return TradeSignals.None;

            double currentPrice = closePrices[currentIndex];
            double currentMavilim = mavilimW[currentIndex];

            if (double.IsNaN(currentMavilim))
                return TradeSignals.None;

            if (signalModeIndex == 0)
            {
                // 0: Fiyat-MavilimW kırılımı (klasik)
                if (YukarıKesti(currentIndex, closePrices, mavilimW)) buy  = true;
                if (AsagiKesti(currentIndex, closePrices, mavilimW))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: MavilimW-Trendline kesişimi
                if (YukarıKesti(currentIndex, mavilimW, trendline)) buy  = true;
                if (AsagiKesti(currentIndex, mavilimW, trendline))  sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: MavilimW slope flip
                if (currentIndex >= 2)
                {
                    double slopeNow  = mavilimW[currentIndex]     - mavilimW[currentIndex - 1];
                    double slopePrev = mavilimW[currentIndex - 1] - mavilimW[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: MavilimW state
                if (Buyuk(currentIndex, closePrices, mavilimW)) buy  = true;
                if (Kucuk(currentIndex, closePrices, mavilimW)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi
                const double bandThreshold = 0.01; // %1
                if (currentMavilim != 0.0)
                {
                    double distanceRatio = (currentPrice - currentMavilim) / currentMavilim;
                    if (distanceRatio >  bandThreshold) buy  = true;
                    if (distanceRatio < -bandThreshold) sell = true;
                }
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest
                const int retestLookback = 10;
                double barLow  = Data[currentIndex].Low;
                double barHigh = Data[currentIndex].High;

                for (int m = currentIndex - retestLookback; m < currentIndex; m++)
                {
                    if (m < 1) continue;

                    if (!buy && YukarıKesti(m, closePrices, mavilimW)
                        && barLow <= currentMavilim
                        && currentPrice > currentMavilim)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(m, closePrices, mavilimW)
                        && barHigh >= currentMavilim
                        && currentPrice < currentMavilim)
                    {
                        sell = true;
                    }
                }
            }
            else if (signalModeIndex == 6)
            {
                // 6: Confirmation bars
                const int confirmBars = 3;
                if (currentIndex >= confirmBars + 1)
                {
                    int crossBar = currentIndex - confirmBars;

                    bool stayedAbove = YukarıKesti(crossBar, closePrices, mavilimW);
                    bool stayedBelow = AsagiKesti(crossBar, closePrices, mavilimW);
                    for (int m = crossBar + 1; m <= currentIndex; m++)
                    {
                        stayedAbove &= closePrices[m] > mavilimW[m];
                        stayedBelow &= closePrices[m] < mavilimW[m];
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: Fiyat eğimi + state combo
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool priceRising  = closePrices[currentIndex] > closePrices[currentIndex - slopeLookback];
                    bool priceFalling = closePrices[currentIndex] < closePrices[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, closePrices, mavilimW) && priceRising)  buy  = true;
                    if (Kucuk(currentIndex, closePrices, mavilimW) && priceFalling) sell = true;
                }
            }

            if (Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    if (Trader.flags?.KarAlSeviyeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    if (Trader.flags?.KarAlYuzdeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesaplaSeviyeli(currentIndex, 2, 10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    if (Trader.flags?.KarAlSeviyeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesapla(currentIndex, 2000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    if (Trader.flags?.KarAlYuzdeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesapla(currentIndex, 2.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    if (Trader.flags?.KarAlSeviyeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(currentIndex, 1000.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
                    if (Trader.flags?.KarAlYuzdeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.KarZararYuzdesindenKarAlHesapla(currentIndex, 3.0) != 0;
                }
            }

            if (Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    if (Trader.flags?.ZararKesSeviyeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    if (Trader.flags?.ZararKesYuzdeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesaplaSeviyeli(currentIndex, -2, -10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    if (Trader.flags?.ZararKesSeviyeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesapla(currentIndex, -1000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    if (Trader.flags?.ZararKesYuzdeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesapla(currentIndex, -1.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    if (Trader.flags?.ZararKesSeviyeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.KarZararFiyatSeviyesindenZararKesHesapla(currentIndex, -500.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
                    if (Trader.flags?.ZararKesYuzdeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.KarZararYuzdesindenZararKesHesapla(currentIndex, -2.0) != 0;
                }
            }

            if (flatModeIndex == 0) flat = false;
            if (skipModeIndex == 0) skip = false;

            if (skip) return TradeSignals.Skip;
            else if (flat) return TradeSignals.Flat;
            else if (takeProfit) return TradeSignals.TakeProfit;
            else if (stopLoss) return TradeSignals.StopLoss;
            else if (buy) return TradeSignals.Buy;
            else if (sell) return TradeSignals.Sell;

            return TradeSignals.None;
        }

        public double[]? GetMavilimW() => mavilimW;
        public double[]? GetTrendline() => trendline;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            if (mavilimW != null && mavilimW.Length > 0) indicators["MavilimW"] = mavilimW;
            if (trendline != null && trendline.Length > 0) indicators["Trendline"] = trendline;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
