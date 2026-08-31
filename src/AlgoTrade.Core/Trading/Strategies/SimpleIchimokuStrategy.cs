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
    /// Ichimoku Cloud Stratejisi (TK Cross)
    ///
    /// Ichimoku Mantığı:
    /// - Tenkan/Kijun çifti MOST'un most/exmov çiftinin analogu (TK Cross = klasik sinyal)
    /// - Tenkan/Kijun High/Low'a bağımlı, priceSource yok
    ///
    /// Parametreler:
    /// - tenkanPeriod/kijunPeriod/senkouPeriod: periyotlar (varsayılan 9/26/52)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: Tenkan/Kijun kesişimi (TK Cross, klasik)
    ///     1: Fiyat-Kijun kesişimi         (fiyat Kijun'u yukarı/aşağı kesince - ikinci klasik sinyal)
    ///     2: Kijun slope flip             (Kijun'un kendi yönü dönünce)
    ///     3: Tenkan/Kijun state           (konum - kesişim değil, koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi      (Tenkan-Kijun farkı %bandThreshold'dan fazla açılınca)
    ///     5: Breakout + retest            (TK kesişip fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars            (kesişimden sonra confirmBars bar aynı tarafta kalınca)
    ///     7: Tenkan eğimi + state combo   (rejim: Tenkan/Kijun konumu + momentum: Tenkan N-bar eğimi)
    /// - exitModeIndex: takeProfit/stopLoss yöntemini seçer (Trader.karAlZararKes üzerinden):
    ///     0: Seviye, seviyeli   1: Yüzde, seviyeli   2: Seviye, tek seviye   3: Yüzde, tek seviye
    ///     4: Anlık kar/zarar fiyat seviyesi   5: Anlık kar/zarar yüzdesi
    /// - flatModeIndex/skipModeIndex/ruleModeIndex: PLACEHOLDER, henuz okunmuyor
    /// </summary>
    public class SimpleIchimokuStrategy : BaseStrategy
    {
        public override string Name => "Simple Ichimoku Strategy";

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

        private readonly int tenkanPeriod;
        private readonly int kijunPeriod;
        private readonly int senkouPeriod;
        private readonly int signalModeIndex;
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        private double[]? tenkan;
        private double[]? kijun;
        private double[]? senkouA;
        private double[]? senkouB;

        public SimpleIchimokuStrategy(int tenkanPeriod = 9, int kijunPeriod = 26, int senkouPeriod = 52,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.tenkanPeriod    = tenkanPeriod;
            this.kijunPeriod     = kijunPeriod;
            this.senkouPeriod    = senkouPeriod;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["TenkanPeriod"]   = tenkanPeriod;
            Parameters["KijunPeriod"]    = kijunPeriod;
            Parameters["SenkouPeriod"]   = senkouPeriod;
            Parameters["RuleModeIndex"]  = ruleModeIndex;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"]  = exitModeIndex;
            Parameters["FlatModeIndex"]  = flatModeIndex;
            Parameters["SkipModeIndex"]  = skipModeIndex;
        }

        public SimpleIchimokuStrategy(List<StockData> data, IndicatorManager indicators,
            int tenkanPeriod = 9, int kijunPeriod = 26, int senkouPeriod = 52,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.tenkanPeriod    = tenkanPeriod;
            this.kijunPeriod     = kijunPeriod;
            this.senkouPeriod    = senkouPeriod;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["TenkanPeriod"]   = tenkanPeriod;
            Parameters["KijunPeriod"]    = kijunPeriod;
            Parameters["SenkouPeriod"]   = senkouPeriod;
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

            var ichimokuResult = Indicators.Trend.Ichimoku(tenkanPeriod, kijunPeriod, senkouPeriod);
            tenkan  = ichimokuResult.Tenkan;
            kijun   = ichimokuResult.Kijun;
            senkouA = ichimokuResult.SenkouA;
            senkouB = ichimokuResult.SenkouB;

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= tenkan.Length      == barCount;
            allSeriesLengthsMatch &= kijun.Length       == barCount;
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
                    $"tenkan={tenkan.Length}, kijun={kijun.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                    $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                    $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false, sell = false, takeProfit = false, stopLoss = false, flat = false, skip = false;

            if (currentIndex < kijunPeriod + 1)
                return TradeSignals.None;

            if (tenkan == null || kijun == null || tenkan.Length == 0)
                return TradeSignals.None;

            double currentTenkan = tenkan[currentIndex];
            double currentKijun = kijun[currentIndex];

            if (double.IsNaN(currentTenkan) || double.IsNaN(currentKijun))
                return TradeSignals.None;

            if (signalModeIndex == 0)
            {
                // 0: Tenkan/Kijun kesişimi (TK Cross, klasik)
                if (YukarıKesti(currentIndex, tenkan, kijun)) buy  = true;
                if (AsagiKesti(currentIndex, tenkan, kijun))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: Fiyat-Kijun kesişimi
                if (YukarıKesti(currentIndex, closePrices!, kijun)) buy  = true;
                if (AsagiKesti(currentIndex, closePrices!, kijun))  sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: Kijun slope flip
                if (currentIndex >= 2)
                {
                    double slopeNow  = kijun[currentIndex]     - kijun[currentIndex - 1];
                    double slopePrev = kijun[currentIndex - 1] - kijun[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: Tenkan/Kijun state
                if (Buyuk(currentIndex, tenkan, kijun)) buy  = true;
                if (Kucuk(currentIndex, tenkan, kijun)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi
                const double bandThreshold = 0.01; // %1
                if (currentKijun != 0.0)
                {
                    double distanceRatio = (currentTenkan - currentKijun) / currentKijun;
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

                    if (!buy && YukarıKesti(m, tenkan, kijun)
                        && barLow <= currentKijun
                        && currentTenkan > currentKijun)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(m, tenkan, kijun)
                        && barHigh >= currentKijun
                        && currentTenkan < currentKijun)
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

                    bool stayedAbove = YukarıKesti(crossBar, tenkan, kijun);
                    bool stayedBelow = AsagiKesti(crossBar, tenkan, kijun);
                    for (int m = crossBar + 1; m <= currentIndex; m++)
                    {
                        stayedAbove &= tenkan[m] > kijun[m];
                        stayedBelow &= tenkan[m] < kijun[m];
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: Tenkan eğimi + state combo
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool tenkanRising  = tenkan[currentIndex] > tenkan[currentIndex - slopeLookback];
                    bool tenkanFalling = tenkan[currentIndex] < tenkan[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, tenkan, kijun) && tenkanRising)  buy  = true;
                    if (Kucuk(currentIndex, tenkan, kijun) && tenkanFalling) sell = true;
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

        public double[]? GetTenkan() => tenkan;
        public double[]? GetKijun() => kijun;
        public double[]? GetSenkouA() => senkouA;
        public double[]? GetSenkouB() => senkouB;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            if (tenkan != null && tenkan.Length > 0) indicators["Tenkan"] = tenkan;
            if (kijun != null && kijun.Length > 0) indicators["Kijun"] = kijun;
            if (senkouA != null && senkouA.Length > 0) indicators["SenkouA"] = senkouA;
            if (senkouB != null && senkouB.Length > 0) indicators["SenkouB"] = senkouB;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
