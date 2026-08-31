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
    /// Stochastic Osilatör Stratejisi
    ///
    /// Stochastic Mantığı:
    /// - %K/%D çifti MOST'un most/exmov çiftinin analogu, 0-100 arası bantlı
    /// - %K/%D High/Low/Close'a bağımlı (priceSource yok, SuperTrend/SAR gibi)
    ///
    /// Parametreler:
    /// - kPeriod/dPeriod: %K/%D periyotları (varsayılan 14/3)
    /// - centerLine: Merkez çizgi (varsayılan 50)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: %K-%D kesişimi + centerline filtresi (klasik - her ikisi de merkezin altında/üstünde olmalı)
    ///     1: %K-centerline kesişimi     (ikinci klasik Stochastic sinyali)
    ///     2: %K slope flip              (%K'nın kendi yönü dönünce)
    ///     3: %K-%D state                (konum - kesişim değil, koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi    (%K ile %D arasındaki fark %bandThreshold'dan fazla açılınca)
    ///     5: Breakout + retest          (%K %D'yi kesip geri yaklaşıp tutunca)
    ///     6: Confirmation bars          (kesişimden sonra confirmBars bar aynı tarafta kalınca)
    ///     7: %K eğimi + state combo     (rejim: %K-%D konumu + momentum: %K N-bar eğimi)
    /// - exitModeIndex: takeProfit/stopLoss yöntemini seçer (Trader.karAlZararKes üzerinden):
    ///     0: Seviye, seviyeli   1: Yüzde, seviyeli   2: Seviye, tek seviye   3: Yüzde, tek seviye
    ///     4: Anlık kar/zarar fiyat seviyesi   5: Anlık kar/zarar yüzdesi
    /// - flatModeIndex/skipModeIndex/ruleModeIndex: PLACEHOLDER, henuz okunmuyor
    /// </summary>
    public class SimpleStochasticStrategy : BaseStrategy
    {
        public override string Name => "Simple Stochastic Strategy";

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

        private readonly int kPeriod;
        private readonly int dPeriod;
        private readonly double centerLine;
        private readonly int signalModeIndex;
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        private double[]? k;
        private double[]? d;

        public SimpleStochasticStrategy(int kPeriod = 14, int dPeriod = 3, double centerLine = 50,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.kPeriod         = kPeriod;
            this.dPeriod         = dPeriod;
            this.centerLine      = centerLine;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["KPeriod"]       = kPeriod;
            Parameters["DPeriod"]       = dPeriod;
            Parameters["CenterLine"]    = centerLine;
            Parameters["RuleModeIndex"] = ruleModeIndex;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"] = exitModeIndex;
            Parameters["FlatModeIndex"] = flatModeIndex;
            Parameters["SkipModeIndex"] = skipModeIndex;
        }

        public SimpleStochasticStrategy(List<StockData> data, IndicatorManager indicators,
            int kPeriod = 14, int dPeriod = 3, double centerLine = 50,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.kPeriod         = kPeriod;
            this.dPeriod         = dPeriod;
            this.centerLine      = centerLine;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["KPeriod"]       = kPeriod;
            Parameters["DPeriod"]       = dPeriod;
            Parameters["CenterLine"]    = centerLine;
            Parameters["RuleModeIndex"] = ruleModeIndex;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"] = exitModeIndex;
            Parameters["FlatModeIndex"] = flatModeIndex;
            Parameters["SkipModeIndex"] = skipModeIndex;

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

            var stochResult = Indicators.Momentum.Stochastic(kPeriod, dPeriod);
            k = stochResult.K;
            d = stochResult.D;

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= k.Length          == barCount;
            allSeriesLengthsMatch &= d.Length          == barCount;
            allSeriesLengthsMatch &= openPrices.Length == barCount;
            allSeriesLengthsMatch &= highPrices.Length == barCount;
            allSeriesLengthsMatch &= lowPrices.Length  == barCount;
            allSeriesLengthsMatch &= closePrices.Length == barCount;
            allSeriesLengthsMatch &= volumes.Length    == barCount;
            allSeriesLengthsMatch &= lotSizes.Length   == barCount;
            allSeriesLengthsMatch &= dateTimes.Length  == barCount;
            allSeriesLengthsMatch &= dates.Length      == barCount;
            allSeriesLengthsMatch &= times.Length      == barCount;
            allSeriesLengthsMatch &= epochTimes.Length == barCount;

            if (!allSeriesLengthsMatch)
            {
                throw new InvalidOperationException(
                    $"Seri uzunlukları uyuşmuyor (barCount={barCount}): " +
                    $"k={k.Length}, d={d.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                    $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                    $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false, sell = false, takeProfit = false, stopLoss = false, flat = false, skip = false;

            if (currentIndex < kPeriod + dPeriod + 1)
                return TradeSignals.None;

            if (k == null || d == null || k.Length == 0)
                return TradeSignals.None;

            double currentK = k[currentIndex];
            double currentD = d[currentIndex];

            if (double.IsNaN(currentK) || double.IsNaN(currentD))
                return TradeSignals.None;

            if (signalModeIndex == 0)
            {
                // 0: %K-%D kesişimi + centerline filtresi (klasik)
                if (YukarıKesti(currentIndex, k, d) && currentK < centerLine && currentD < centerLine) buy  = true;
                if (AsagiKesti(currentIndex, k, d) && currentK > centerLine && currentD > centerLine)  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: %K-centerline kesişimi
                if (YukarıKesti(currentIndex, k, centerLine)) buy  = true;
                if (AsagiKesti(currentIndex, k, centerLine))  sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: %K slope flip
                if (currentIndex >= 2)
                {
                    double slopeNow  = k[currentIndex]     - k[currentIndex - 1];
                    double slopePrev = k[currentIndex - 1] - k[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: %K-%D state - koşul sürdükçe her bar
                if (Buyuk(currentIndex, k, d)) buy  = true;
                if (Kucuk(currentIndex, k, d)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi
                const double bandThreshold = 10.0; // Stochastic puanı
                double diff = currentK - currentD;
                if (diff >  bandThreshold) buy  = true;
                if (diff < -bandThreshold) sell = true;
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest
                const int retestLookback = 10;
                const double retestBand  = 3.0;

                for (int m = currentIndex - retestLookback; m < currentIndex; m++)
                {
                    if (m < 1) continue;

                    if (!buy && YukarıKesti(m, k, d)
                        && Math.Abs(currentK - currentD) <= retestBand
                        && currentK > currentD)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(m, k, d)
                        && Math.Abs(currentK - currentD) <= retestBand
                        && currentK < currentD)
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

                    bool stayedAbove = YukarıKesti(crossBar, k, d);
                    bool stayedBelow = AsagiKesti(crossBar, k, d);
                    for (int m = crossBar + 1; m <= currentIndex; m++)
                    {
                        stayedAbove &= k[m] > d[m];
                        stayedBelow &= k[m] < d[m];
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: %K eğimi + state combo
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool kRising  = k[currentIndex] > k[currentIndex - slopeLookback];
                    bool kFalling = k[currentIndex] < k[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, k, d) && kRising)  buy  = true;
                    if (Kucuk(currentIndex, k, d) && kFalling) sell = true;
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

        public double[]? GetK() => k;
        public double[]? GetD() => d;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            if (k != null && k.Length > 0) indicators["Stoch_K"] = k;
            if (d != null && d.Length > 0) indicators["Stoch_D"] = d;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
