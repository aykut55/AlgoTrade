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
    /// DI (Directional Indicators) Stratejisi
    ///
    /// DI Mantığı:
    /// - +DI/-DI çifti MOST'un most/exmov çiftinin analogu - ama SimpleADXStrategy'nin aksine
    ///   ADX gücü filtresi YOK, sadece ham DI kesişimleri/konumu kullanılır
    /// - Fiyat/priceSource kavramı yok - DI'lar High/Low'a bağımlı (SuperTrend/SAR gibi)
    ///
    /// Parametreler:
    /// - period: DI periyodu (varsayılan 14)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: +DI/-DI kesişimi          (+DI, -DI'yı yukarı/aşağı kesince)
    ///     1: +DI/-DI state             (konum - kesişim değil, koşul sürdükçe her bar)
    ///     2: +DI slope flip            (+DI'nin kendi yönü dönünce)
    ///     3: -DI slope flip            (-DI'nin kendi yönü dönünce)
    ///     4: Band / uzaklık filtresi   (+DI ile -DI arasındaki fark %bandThreshold'dan fazla açılınca)
    ///     5: Breakout + retest         (DI kesişip fark geri daralıp yön korununca)
    ///     6: Confirmation bars         (kesişimden sonra confirmBars bar aynı tarafta kalınca)
    ///     7: +DI eğimi + DI state      (rejim: DI konumu + momentum: +DI N-bar eğimi)
    /// - exitModeIndex: takeProfit/stopLoss yöntemini seçer (Trader.karAlZararKes üzerinden):
    ///     0: Seviye, seviyeli   1: Yüzde, seviyeli   2: Seviye, tek seviye   3: Yüzde, tek seviye
    ///     4: Anlık kar/zarar fiyat seviyesi   5: Anlık kar/zarar yüzdesi
    /// - flatModeIndex/skipModeIndex/ruleModeIndex: PLACEHOLDER, henuz okunmuyor
    /// </summary>
    public class SimpleDIStrategy : BaseStrategy
    {
        public override string Name => "Simple DI Strategy";

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
        private readonly int signalModeIndex;
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        private double[]? plusDI;
        private double[]? minusDI;

        public SimpleDIStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 14,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period          = period;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Period"]         = period;
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

            var adxResult = Indicators.Trend.ADXWithDI(period);
            plusDI  = adxResult.PlusDI;
            minusDI = adxResult.MinusDI;

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= plusDI.Length     == barCount;
            allSeriesLengthsMatch &= minusDI.Length    == barCount;
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
                    $"plusDI={plusDI.Length}, minusDI={minusDI.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                    $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                    $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false, sell = false, takeProfit = false, stopLoss = false, flat = false, skip = false;

            if (currentIndex < period + 1)
                return TradeSignals.None;

            if (plusDI == null || minusDI == null || plusDI.Length == 0)
                return TradeSignals.None;

            double currentPlusDI = plusDI[currentIndex];
            double currentMinusDI = minusDI[currentIndex];

            if (double.IsNaN(currentPlusDI) || double.IsNaN(currentMinusDI))
                return TradeSignals.None;

            if (signalModeIndex == 0)
            {
                // 0: +DI/-DI kesişimi
                if (YukarıKesti(currentIndex, plusDI, minusDI)) buy  = true;
                if (AsagiKesti(currentIndex, plusDI, minusDI))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: +DI/-DI state - koşul sürdükçe her bar
                if (Buyuk(currentIndex, plusDI, minusDI)) buy  = true;
                if (Kucuk(currentIndex, plusDI, minusDI)) sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: +DI slope flip
                if (currentIndex >= 2)
                {
                    double slopeNow  = plusDI[currentIndex]     - plusDI[currentIndex - 1];
                    double slopePrev = plusDI[currentIndex - 1] - plusDI[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: -DI slope flip
                if (currentIndex >= 2)
                {
                    double slopeNow  = minusDI[currentIndex]     - minusDI[currentIndex - 1];
                    double slopePrev = minusDI[currentIndex - 1] - minusDI[currentIndex - 2];
                    if (slopePrev >= 0.0 && slopeNow < 0.0) buy  = true;
                    if (slopePrev <= 0.0 && slopeNow > 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi - +DI ile -DI farkı bandThreshold'dan fazla açılınca
                const double bandThreshold = 5.0; // DI puanı
                double diDiff = currentPlusDI - currentMinusDI;
                if (diDiff >  bandThreshold) buy  = true;
                if (diDiff < -bandThreshold) sell = true;
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest - son retestLookback bar içinde kesişti, hâlâ aynı yönde
                const int retestLookback = 10;
                for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                {
                    if (k < 1) continue;

                    if (!buy && YukarıKesti(k, plusDI, minusDI) && currentPlusDI > currentMinusDI)
                        buy = true;

                    if (!sell && AsagiKesti(k, plusDI, minusDI) && currentMinusDI > currentPlusDI)
                        sell = true;
                }
            }
            else if (signalModeIndex == 6)
            {
                // 6: Confirmation bars - kesişimden confirmBars sonra hâlâ aynı yönde
                const int confirmBars = 3;
                if (currentIndex >= confirmBars + 1)
                {
                    int crossBar = currentIndex - confirmBars;

                    bool stayedAbove = YukarıKesti(crossBar, plusDI, minusDI);
                    bool stayedBelow = AsagiKesti(crossBar, plusDI, minusDI);
                    for (int k = crossBar + 1; k <= currentIndex; k++)
                    {
                        stayedAbove &= plusDI[k] > minusDI[k];
                        stayedBelow &= plusDI[k] < minusDI[k];
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: +DI eğimi + DI state - rejim (DI konumu) + momentum (+DI N-bar eğimi)
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool plusDIRising = plusDI[currentIndex] > plusDI[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, plusDI, minusDI) && plusDIRising)  buy  = true;
                    if (Kucuk(currentIndex, plusDI, minusDI) && !plusDIRising) sell = true;
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

        public double[]? GetPlusDI() => plusDI;
        public double[]? GetMinusDI() => minusDI;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            if (plusDI != null && plusDI.Length > 0) indicators["+DI"] = plusDI;
            if (minusDI != null && minusDI.Length > 0) indicators["-DI"] = minusDI;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
