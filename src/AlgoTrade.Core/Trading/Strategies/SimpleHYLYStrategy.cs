using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Indicators.Base;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;
using static AlgoTrade.Core.Trading.Utils.Utils;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// HY/LY (High-Yakınlık / Low-Yakınlık) Relatif Mesafe Stratejisi
    ///
    /// HY/LY Mantığı:
    /// - HY = (fiyat-LLV)/(HHV-LLV)*100, LY = (HHV-fiyat)/(HHV-LLV)*100 - RSI'nin 0-100 mimari
    ///   analogu, ama tek seri yerine tamamlayıcı iki seri (HY yükseğe, LY düşüğe yakınlık)
    ///
    /// Parametreler:
    /// - period: HHV/LLV lookback periyodu (varsayılan 20)
    /// - threshold: sinyal eşiği (varsayılan 80)
    /// - priceSource: HY/LY hesabındaki fiyat kaynağı (varsayılan Close - klasik HY/LY)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: HY/LY eşik kesişimi (klasik) (HY threshold'u yukarı kesince AL, LY threshold'u yukarı kesince SAT)
    ///     1: Orta hat (50) kesişimi        (HY 50'yi yukarı kesince AL, LY 50'yi yukarı kesince SAT)
    ///     2: HY slope flip                 (HY'nin kendi yönü dönünce)
    ///     3: HY/LY state                   (threshold'a göre konum - koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi       (HY-LY farkı %bandThreshold'dan fazla açılınca)
    ///     5: Breakout + retest             (eşik kırılıp geri yaklaşıp tutunca)
    ///     6: Confirmation bars             (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: HY eğimi + state combo        (rejim: HY/LY konumu + momentum: HY N-bar eğimi)
    /// - exitModeIndex: takeProfit/stopLoss yöntemini seçer (Trader.karAlZararKes üzerinden):
    ///     0: Seviye, seviyeli   1: Yüzde, seviyeli   2: Seviye, tek seviye   3: Yüzde, tek seviye
    ///     4: Anlık kar/zarar fiyat seviyesi   5: Anlık kar/zarar yüzdesi
    /// - flatModeIndex/skipModeIndex/ruleModeIndex: PLACEHOLDER, henuz okunmuyor
    /// </summary>
    public class SimpleHYLYStrategy : BaseStrategy
    {
        public override string Name => "Simple HY/LY Strategy";

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

        private readonly int period;
        private readonly double threshold;
        private readonly int signalModeIndex;
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        private readonly PriceSource priceSource = PriceSource.Close;

        private double[]? source;
        private double[]? hy;
        private double[]? ly;

        public SimpleHYLYStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 20, double threshold = 80, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period          = period;
            this.threshold       = threshold;
            this.priceSource     = priceSource;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Period"]         = period;
            Parameters["Threshold"]      = threshold;
            Parameters["PriceSource"]    = priceSource;
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
            source      = Indicators.Trend.ResolvePriceSource(priceSource);

            var hhv = Indicators.Utils.HHV(highPrices, period);
            var llv = Indicators.Utils.LLV(lowPrices, period);

            hy = new double[barCount];
            ly = new double[barCount];
            for (int i = 0; i < barCount; i++)
            {
                double range = hhv[i] - llv[i];
                if (double.IsNaN(hhv[i]) || double.IsNaN(llv[i]) || range <= 0)
                {
                    hy[i] = double.NaN;
                    ly[i] = double.NaN;
                }
                else
                {
                    hy[i] = ((source[i] - llv[i]) / range) * 100;
                    ly[i] = ((hhv[i] - source[i]) / range) * 100;
                }
            }

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= hy.Length          == barCount;
            allSeriesLengthsMatch &= ly.Length          == barCount;
            allSeriesLengthsMatch &= source.Length      == barCount;
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
                    $"hy={hy.Length}, ly={ly.Length}, source={source.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                    $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                    $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false, sell = false, takeProfit = false, stopLoss = false, flat = false, skip = false;

            if (currentIndex < period + 1)
                return TradeSignals.None;

            if (hy == null || ly == null || hy.Length == 0)
                return TradeSignals.None;

            double currentHY = hy[currentIndex];
            double currentLY = ly[currentIndex];

            if (double.IsNaN(currentHY) || double.IsNaN(currentLY))
                return TradeSignals.None;

            if (signalModeIndex == 0)
            {
                // 0: HY/LY eşik kesişimi (klasik)
                if (YukarıKesti(currentIndex, hy, threshold)) buy  = true;
                if (YukarıKesti(currentIndex, ly, threshold)) sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: Orta hat (50) kesişimi
                const double midline = 50.0;
                if (YukarıKesti(currentIndex, hy, midline)) buy  = true;
                if (YukarıKesti(currentIndex, ly, midline)) sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: HY slope flip
                if (currentIndex >= 2)
                {
                    double slopeNow  = hy[currentIndex]     - hy[currentIndex - 1];
                    double slopePrev = hy[currentIndex - 1] - hy[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: HY/LY state
                if (Buyuk(currentIndex, hy, threshold)) buy  = true;
                if (Buyuk(currentIndex, ly, threshold)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi
                const double bandThreshold = 40.0;
                if (currentHY - currentLY >  bandThreshold) buy  = true;
                if (currentLY - currentHY >  bandThreshold) sell = true;
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest
                const int retestLookback = 10;
                const double retestBand  = 3.0;

                for (int m = currentIndex - retestLookback; m < currentIndex; m++)
                {
                    if (m < 1) continue;

                    // HY threshold'u kırdı, retestBand içine geri yaklaşıp tekrar üstünde kapattı
                    if (!buy && YukarıKesti(m, hy, threshold)
                        && currentHY <= threshold + retestBand
                        && currentHY > threshold)
                    {
                        buy = true;
                    }

                    if (!sell && YukarıKesti(m, ly, threshold)
                        && currentLY <= threshold + retestBand
                        && currentLY > threshold)
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

                    bool stayedAboveHY = YukarıKesti(crossBar, hy, threshold);
                    bool stayedAboveLY = YukarıKesti(crossBar, ly, threshold);
                    for (int m = crossBar + 1; m <= currentIndex; m++)
                    {
                        stayedAboveHY &= hy[m] > threshold;
                        stayedAboveLY &= ly[m] > threshold;
                    }
                    if (stayedAboveHY) buy  = true;
                    if (stayedAboveLY) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: HY eğimi + state combo
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool hyRising = hy[currentIndex] > hy[currentIndex - slopeLookback];
                    bool lyRising = ly[currentIndex] > ly[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, hy, threshold) && hyRising) buy  = true;
                    if (Buyuk(currentIndex, ly, threshold) && lyRising) sell = true;
                }
            }

            if (Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesaplaSeviyeli(currentIndex, 2, 10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesapla(currentIndex, 2000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesapla(currentIndex, 2.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(currentIndex, 1000.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
                    takeProfit = Trader.karAlZararKes.KarZararYuzdesindenKarAlHesapla(currentIndex, 3.0) != 0;
                }
            }

            if (Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesaplaSeviyeli(currentIndex, -2, -10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesapla(currentIndex, -1000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesapla(currentIndex, -1.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    stopLoss = Trader.karAlZararKes.KarZararFiyatSeviyesindenZararKesHesapla(currentIndex, -500.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
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

        public double[]? GetHY() => hy;
        public double[]? GetLY() => ly;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            if (hy != null && hy.Length > 0) indicators["HY"] = hy;
            if (ly != null && ly.Length > 0) indicators["LY"] = ly;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
