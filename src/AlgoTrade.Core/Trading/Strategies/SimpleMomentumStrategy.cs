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
    /// Momentum (ROC) İndikatörü Stratejisi
    ///
    /// Momentum Mantığı:
    /// - ROC = ((fiyat - fiyat[period ago]) / fiyat[period ago]) * 100, 0 merkezli
    ///
    /// Parametreler:
    /// - period: Momentum periyodu (varsayılan 12)
    /// - positiveThreshold/negativeThreshold: seviyeler (varsayılan 0/0)
    /// - priceSource: ROC'un beslendiği kaynak (varsayılan Close - klasik Momentum)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: Pozitif/negatif eşik kesişimi (klasik)
    ///     1: Orta hat (0) kesişimi        (Momentum 0'ı yukarı/aşağı kesince)
    ///     2: Momentum slope flip          (Momentum'un kendi yönü dönünce)
    ///     3: Momentum state               (0'a göre konum - koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi      (Momentum 0'dan %bandThreshold'dan fazla uzaklaşınca)
    ///     5: Breakout + retest            (eşik kırılıp Momentum geri yaklaşıp tutunca)
    ///     6: Confirmation bars            (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: Momentum eğimi + state combo (rejim: 0'a göre konum + momentum: N-bar eğim)
    /// - exitModeIndex: takeProfit/stopLoss yöntemini seçer (Trader.karAlZararKes üzerinden):
    ///     0: Seviye, seviyeli   1: Yüzde, seviyeli   2: Seviye, tek seviye   3: Yüzde, tek seviye
    ///     4: Anlık kar/zarar fiyat seviyesi   5: Anlık kar/zarar yüzdesi
    /// - flatModeIndex/skipModeIndex/ruleModeIndex: PLACEHOLDER, henuz okunmuyor
    /// </summary>
    public class SimpleMomentumStrategy : BaseStrategy
    {
        public override string Name => "Simple Momentum Strategy";

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
        private readonly double positiveThreshold;
        private readonly double negativeThreshold;
        private readonly int signalModeIndex;
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        private readonly PriceSource priceSource = PriceSource.Close;

        private double[]? source;
        private double[]? momentum;

        public SimpleMomentumStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 12, double positiveThreshold = 0, double negativeThreshold = 0, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period            = period;
            this.positiveThreshold = positiveThreshold;
            this.negativeThreshold = negativeThreshold;
            this.priceSource       = priceSource;
            this.ruleModeIndex     = ruleModeIndex;
            this.signalModeIndex   = signalModeIndex;
            this.exitModeIndex     = exitModeIndex;
            this.flatModeIndex     = flatModeIndex;
            this.skipModeIndex     = skipModeIndex;

            Parameters["Period"]            = period;
            Parameters["PositiveThreshold"] = positiveThreshold;
            Parameters["NegativeThreshold"] = negativeThreshold;
            Parameters["PriceSource"]       = priceSource;
            Parameters["RuleModeIndex"]     = ruleModeIndex;
            Parameters["SignalModeIndex"]   = signalModeIndex;
            Parameters["ExitModeIndex"]     = exitModeIndex;
            Parameters["FlatModeIndex"]     = flatModeIndex;
            Parameters["SkipModeIndex"]     = skipModeIndex;

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

            momentum = Indicators.Momentum.ROC(source, period);

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= momentum.Length    == barCount;
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
                    $"momentum={momentum.Length}, source={source.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                    $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                    $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false, sell = false, takeProfit = false, stopLoss = false, flat = false, skip = false;

            if (currentIndex < period + 1)
                return TradeSignals.None;

            if (momentum == null || momentum.Length == 0)
                return TradeSignals.None;

            double currentMom = momentum[currentIndex];
            if (double.IsNaN(currentMom))
                return TradeSignals.None;

            if (signalModeIndex == 0)
            {
                // 0: Pozitif/negatif eşik kesişimi (klasik)
                if (YukarıKesti(currentIndex, momentum, positiveThreshold)) buy  = true;
                if (AsagiKesti(currentIndex, momentum, negativeThreshold))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: Orta hat (0) kesişimi
                const double zero = 0.0;
                if (YukarıKesti(currentIndex, momentum, zero)) buy  = true;
                if (AsagiKesti(currentIndex, momentum, zero))  sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: Momentum slope flip
                if (currentIndex >= 2)
                {
                    double slopeNow  = momentum[currentIndex]     - momentum[currentIndex - 1];
                    double slopePrev = momentum[currentIndex - 1] - momentum[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: Momentum state
                if (Buyuk(currentIndex, momentum, 0.0)) buy  = true;
                if (Kucuk(currentIndex, momentum, 0.0)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi
                const double bandThreshold = 5.0; // %5
                if (currentMom >  bandThreshold) buy  = true;
                if (currentMom < -bandThreshold) sell = true;
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest
                const int retestLookback = 10;
                const double retestBand  = 0.5;

                for (int m = currentIndex - retestLookback; m < currentIndex; m++)
                {
                    if (m < 1) continue;

                    if (!buy && YukarıKesti(m, momentum, positiveThreshold)
                        && currentMom <= positiveThreshold + retestBand
                        && currentMom > positiveThreshold)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(m, momentum, negativeThreshold)
                        && currentMom >= negativeThreshold - retestBand
                        && currentMom < negativeThreshold)
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

                    bool stayedAbove = YukarıKesti(crossBar, momentum, positiveThreshold);
                    bool stayedBelow = AsagiKesti(crossBar, momentum, negativeThreshold);
                    for (int m = crossBar + 1; m <= currentIndex; m++)
                    {
                        stayedAbove &= momentum[m] > positiveThreshold;
                        stayedBelow &= momentum[m] < negativeThreshold;
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: Momentum eğimi + state combo
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool momRising  = momentum[currentIndex] > momentum[currentIndex - slopeLookback];
                    bool momFalling = momentum[currentIndex] < momentum[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, momentum, 0.0) && momRising)  buy  = true;
                    if (Kucuk(currentIndex, momentum, 0.0) && momFalling) sell = true;
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

        public double[]? GetMomentum() => momentum;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            if (momentum != null && momentum.Length > 0) indicators["Momentum"] = momentum;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
