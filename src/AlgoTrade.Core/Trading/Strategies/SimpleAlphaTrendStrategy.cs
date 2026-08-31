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
    /// AlphaTrend İndikatörü Stratejisi
    ///
    /// AlphaTrend Mantığı:
    /// - ATR tabanlı dinamik destek/direnç, MFI/RSI momentum ile filtrelenir
    /// - ATR/MFI High/Low/Close/Volume'a bağımlı, priceSource yok, ikinci referans çizgisi yok
    ///
    /// Parametreler:
    /// - atrPeriod: ATR periyodu (varsayılan 14)
    /// - coefficient: ATR çarpanı (varsayılan 1.0)
    /// - momentumPeriod: MFI/RSI periyodu (varsayılan 14)
    /// - useMFI: momentum filtresi MFI mi RSI mi (varsayılan true = MFI)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: Fiyat-AlphaTrend kırılımı    (fiyat AlphaTrend'i yukarı/aşağı kesince)
    ///     1: 2-bar ötelemeli kesişim      (AlphaTrend[i-2], AlphaTrend[i-1]'i kesince - eski choice=0 ile birebir aynı)
    ///     2: AlphaTrend slope flip        (AlphaTrend'in kendi yönü dönünce)
    ///     3: AlphaTrend state             (fiyatın AlphaTrend'e göre konumu - koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi      (fiyat AlphaTrend'ten %bandThreshold'dan fazla uzaklaşınca)
    ///     5: Breakout + retest            (AlphaTrend kırılıp fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars            (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: Fiyat eğimi + state combo    (rejim: fiyat-AlphaTrend konumu + momentum: fiyatın N-bar eğimi)
    /// - exitModeIndex: takeProfit/stopLoss yöntemini seçer (Trader.karAlZararKes üzerinden):
    ///     0: Seviye, seviyeli   1: Yüzde, seviyeli   2: Seviye, tek seviye   3: Yüzde, tek seviye
    ///     4: Anlık kar/zarar fiyat seviyesi   5: Anlık kar/zarar yüzdesi
    /// - flatModeIndex/skipModeIndex/ruleModeIndex: PLACEHOLDER, henuz okunmuyor
    /// </summary>
    public class SimpleAlphaTrendStrategy : BaseStrategy
    {
        public override string Name => "Simple AlphaTrend Strategy";

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

        private readonly int atrPeriod;
        private readonly double coefficient;
        private readonly int momentumPeriod;
        private readonly bool useMFI;
        private readonly int signalModeIndex;
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        private double[]? alphaTrend;

        public SimpleAlphaTrendStrategy(List<StockData> data, IndicatorManager indicators,
            int atrPeriod = 14, double coefficient = 1.0, int momentumPeriod = 14, bool useMFI = true,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.atrPeriod       = atrPeriod;
            this.coefficient     = coefficient;
            this.momentumPeriod  = momentumPeriod;
            this.useMFI          = useMFI;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["AtrPeriod"]      = atrPeriod;
            Parameters["Coefficient"]    = coefficient;
            Parameters["MomentumPeriod"] = momentumPeriod;
            Parameters["UseMFI"]         = useMFI;
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

            var alphaTrendResult = Indicators.Trend.AlphaTrend(atrPeriod, coefficient, momentumPeriod, useMFI);
            alphaTrend = alphaTrendResult.AlphaTrend;

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= alphaTrend.Length  == barCount;
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
                    $"alphaTrend={alphaTrend.Length}, open={openPrices.Length}, high={highPrices.Length}, low={lowPrices.Length}, " +
                    $"close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, dateTime={dateTimes.Length}, " +
                    $"date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false, sell = false, takeProfit = false, stopLoss = false, flat = false, skip = false;

            if (currentIndex < atrPeriod + 2)
                return TradeSignals.None;

            if (alphaTrend == null || closePrices == null || alphaTrend.Length == 0)
                return TradeSignals.None;

            double currentPrice = closePrices[currentIndex];
            double currentAT = alphaTrend[currentIndex];

            if (double.IsNaN(currentAT))
                return TradeSignals.None;

            if (signalModeIndex == 0)
            {
                // 0: Fiyat-AlphaTrend kırılımı
                if (YukarıKesti(currentIndex, closePrices, alphaTrend)) buy  = true;
                if (AsagiKesti(currentIndex, closePrices, alphaTrend))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: 2-bar ötelemeli kesişim (eski choice=0 ile birebir aynı)
                double at0 = alphaTrend[currentIndex];
                double at1 = alphaTrend[currentIndex - 1];
                double at2 = alphaTrend[currentIndex - 2];
                if (at2 <= at1 && at0 > at1) buy  = true;
                if (at2 >= at1 && at0 < at1) sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: AlphaTrend slope flip
                if (currentIndex >= 2)
                {
                    double slopeNow  = alphaTrend[currentIndex]     - alphaTrend[currentIndex - 1];
                    double slopePrev = alphaTrend[currentIndex - 1] - alphaTrend[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: AlphaTrend state
                if (Buyuk(currentIndex, closePrices, alphaTrend)) buy  = true;
                if (Kucuk(currentIndex, closePrices, alphaTrend)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi
                const double bandThreshold = 0.01; // %1
                if (currentAT != 0.0)
                {
                    double distanceRatio = (currentPrice - currentAT) / currentAT;
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

                    if (!buy && YukarıKesti(m, closePrices, alphaTrend)
                        && barLow <= currentAT
                        && currentPrice > currentAT)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(m, closePrices, alphaTrend)
                        && barHigh >= currentAT
                        && currentPrice < currentAT)
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

                    bool stayedAbove = YukarıKesti(crossBar, closePrices, alphaTrend);
                    bool stayedBelow = AsagiKesti(crossBar, closePrices, alphaTrend);
                    for (int m = crossBar + 1; m <= currentIndex; m++)
                    {
                        stayedAbove &= closePrices[m] > alphaTrend[m];
                        stayedBelow &= closePrices[m] < alphaTrend[m];
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
                    if (Buyuk(currentIndex, closePrices, alphaTrend) && priceRising)  buy  = true;
                    if (Kucuk(currentIndex, closePrices, alphaTrend) && priceFalling) sell = true;
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

        public double[]? GetAlphaTrend() => alphaTrend;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            if (alphaTrend != null && alphaTrend.Length > 0) indicators["AlphaTrend"] = alphaTrend;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
