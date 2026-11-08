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
    /// WaveTrend stratejisi. Hesaplama, "WaveTrend with Crosses [LazyBear]"
    /// indikatörüne dayanır; orijinal yazar atfı bilinçli olarak korunmuştur.
    ///
    /// Sinyal modları:
    /// 0: WT1/WT2 kesişimi (orijinal LazyBear davranışı)
    /// 1: Aşırı satım/alım bölgesinde WT1/WT2 kesişimi
    /// 2: WT1 sıfır çizgisi kesişimi
    /// 3: WT1 eğim dönüşü
    /// 4: WT1/WT2 konumu (state)
    /// 5: WT1-WT2 histogram eşik filtresi
    /// </summary>
    public class SimpleWaveTrendStrategy : BaseStrategy
    {
        public override string Name => "Simple WaveTrend Strategy";

        private readonly int channelLength;
        private readonly int averageLength;
        private readonly int signalLength;
        private readonly double overboughtLevel1;
        private readonly double overboughtLevel2;
        private readonly double oversoldLevel1;
        private readonly double oversoldLevel2;

        private double[]? wt1;
        private double[]? wt2;
        private double[]? histogram;

        public SimpleWaveTrendStrategy(
            List<StockData> data,
            IndicatorManager indicators,
            int channelLength = 10,
            int averageLength = 21,
            int signalLength = 4,
            double overboughtLevel1 = 60.0,
            double overboughtLevel2 = 53.0,
            double oversoldLevel1 = -60.0,
            double oversoldLevel2 = -53.0,
            int signalModeIndex = 0,
            int buySignalModeIndex = -1,
            int sellSignalModeIndex = -1,
            int takeProfitExitModeIndex = 0,
            int stopLossExitModeIndex = 0,
            int flatModeIndex = 0,
            int skipModeIndex = 0,
            int ruleModeIndex = 0)
        {
            this.channelLength = channelLength;
            this.averageLength = averageLength;
            this.signalLength = signalLength;
            this.overboughtLevel1 = overboughtLevel1;
            this.overboughtLevel2 = overboughtLevel2;
            this.oversoldLevel1 = oversoldLevel1;
            this.oversoldLevel2 = oversoldLevel2;
            this.buySignalModeIndex = buySignalModeIndex >= 0 ? buySignalModeIndex : signalModeIndex;
            this.sellSignalModeIndex = sellSignalModeIndex >= 0 ? sellSignalModeIndex : signalModeIndex;
            this.takeProfitExitModeIndex = takeProfitExitModeIndex;
            this.stopLossExitModeIndex = stopLossExitModeIndex;
            this.flatModeIndex = flatModeIndex;
            this.skipModeIndex = skipModeIndex;
            this.ruleModeIndex = ruleModeIndex;

            startTime = TimeOnly.ParseExact("10:05:00", "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            stopTime = TimeOnly.ParseExact("16:45:00", "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            triggerTime = TimeOnly.ParseExact("14:07:00", "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            startDay = default;
            stopDay = default;
            isTimeEnabled = false;
            isDayEnabled = false;
            isTriggerTimeEnabled = false;
            takeProfitExitModeEnabled = true;
            stopLossExitModeEnabled = true;

            Parameters["ChannelLength"] = channelLength;
            Parameters["AverageLength"] = averageLength;
            Parameters["SignalLength"] = signalLength;
            Parameters["OverboughtLevel1"] = overboughtLevel1;
            Parameters["OverboughtLevel2"] = overboughtLevel2;
            Parameters["OversoldLevel1"] = oversoldLevel1;
            Parameters["OversoldLevel2"] = oversoldLevel2;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["BuySignalModeIndex"] = this.buySignalModeIndex;
            Parameters["SellSignalModeIndex"] = this.sellSignalModeIndex;
            Parameters["TakeProfitExitModeIndex"] = takeProfitExitModeIndex;
            Parameters["StopLossExitModeIndex"] = stopLossExitModeIndex;
            Parameters["FlatModeIndex"] = flatModeIndex;
            Parameters["SkipModeIndex"] = skipModeIndex;
            Parameters["RuleModeIndex"] = ruleModeIndex;

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized) return;

            var result = Indicators.Momentum.WaveTrend(
                channelLength, averageLength, signalLength,
                overboughtLevel1, overboughtLevel2, oversoldLevel1, oversoldLevel2);

            wt1 = result.WT1;
            wt2 = result.WT2;
            histogram = result.Histogram;

            if (wt1.Length != barCount || wt2.Length != barCount || histogram.Length != barCount)
                throw new InvalidOperationException(
                    $"WaveTrend seri uzunlukları uyuşmuyor (barCount={barCount}): WT1={wt1.Length}, WT2={wt2.Length}, Histogram={histogram.Length}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false, sell = false, takeProfit = false, stopLoss = false, flat = false, skip = false;

            ResolveRunContext(currentIndex);
            TimeOnly currentTime = times![currentIndex];
            DateOnly currentDate = dates![currentIndex];
            bool isWithinTimeWindow = !isTimeEnabled || (currentTime >= startTime && currentTime <= stopTime);
            bool isWithinDayWindow = !isDayEnabled || (currentDate >= startDay && currentDate <= stopDay);

            if (currentIndex < 1 || wt1 == null || wt2 == null || histogram == null)
                return TradeSignals.None;

            double currentWT1 = wt1[currentIndex];
            double currentWT2 = wt2[currentIndex];
            if (double.IsNaN(currentWT1) || double.IsNaN(currentWT2) ||
                double.IsNaN(wt1[currentIndex - 1]) || double.IsNaN(wt2[currentIndex - 1]))
                return TradeSignals.None;

            if (buyModeEnabled)
            {
                if (buySignalModeIndex == 0)
                    buy = YukarıKesti(currentIndex, wt1, wt2);
                else if (buySignalModeIndex == 1)
                    buy = YukarıKesti(currentIndex, wt1, wt2) && currentWT2 <= oversoldLevel2;
                else if (buySignalModeIndex == 2)
                    buy = YukarıKesti(currentIndex, wt1, 0.0);
                else if (buySignalModeIndex == 3 && currentIndex >= 2)
                    buy = wt1[currentIndex - 1] - wt1[currentIndex - 2] <= 0.0 && currentWT1 - wt1[currentIndex - 1] > 0.0;
                else if (buySignalModeIndex == 4)
                    buy = Buyuk(currentIndex, wt1, wt2);
                else if (buySignalModeIndex == 5)
                    buy = histogram[currentIndex] > 5.0;
            }

            if (sellModeEnabled)
            {
                if (sellSignalModeIndex == 0)
                    sell = AsagiKesti(currentIndex, wt1, wt2);
                else if (sellSignalModeIndex == 1)
                    sell = AsagiKesti(currentIndex, wt1, wt2) && currentWT2 >= overboughtLevel2;
                else if (sellSignalModeIndex == 2)
                    sell = AsagiKesti(currentIndex, wt1, 0.0);
                else if (sellSignalModeIndex == 3 && currentIndex >= 2)
                    sell = wt1[currentIndex - 1] - wt1[currentIndex - 2] >= 0.0 && currentWT1 - wt1[currentIndex - 1] < 0.0;
                else if (sellSignalModeIndex == 4)
                    sell = Kucuk(currentIndex, wt1, wt2);
                else if (sellSignalModeIndex == 5)
                    sell = histogram[currentIndex] < -5.0;
            }

            if (takeProfitExitModeEnabled && Trader != null)
            {
                if (takeProfitExitModeIndex == 0) takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
                else if (takeProfitExitModeIndex == 1) takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesaplaSeviyeli(currentIndex, 2, 10, 0.01) != 0;
                else if (takeProfitExitModeIndex == 2) takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesapla(currentIndex, 2000.0) != 0;
                else if (takeProfitExitModeIndex == 3) takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesapla(currentIndex, 2.0) != 0;
                else if (takeProfitExitModeIndex == 4) takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(currentIndex, 1000.0) != 0;
                else if (takeProfitExitModeIndex == 5) takeProfit = Trader.karAlZararKes.KarZararYuzdesindenKarAlHesapla(currentIndex, 3.0) != 0;
            }

            if (stopLossExitModeEnabled && Trader != null)
            {
                if (stopLossExitModeIndex == 0) stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
                else if (stopLossExitModeIndex == 1) stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesaplaSeviyeli(currentIndex, -2, -10, 0.01) != 0;
                else if (stopLossExitModeIndex == 2) stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesapla(currentIndex, -1000.0) != 0;
                else if (stopLossExitModeIndex == 3) stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesapla(currentIndex, -1.0) != 0;
                else if (stopLossExitModeIndex == 4) stopLoss = Trader.karAlZararKes.KarZararFiyatSeviyesindenZararKesHesapla(currentIndex, -500.0) != 0;
                else if (stopLossExitModeIndex == 5) stopLoss = Trader.karAlZararKes.KarZararYuzdesindenZararKesHesapla(currentIndex, -2.0) != 0;
            }

            if (flatModeEnabled && flatModeIndex == 0) flat = false;
            if (skipModeEnabled && skipModeIndex == 0) skip = false;

            if (Trader?.signals?.AlEnabled == false) buy = false;
            if (Trader?.signals?.SatEnabled == false) sell = false;
            if (Trader?.signals?.KarAlEnabled == false) takeProfit = false;
            if (Trader?.signals?.ZararKesEnabled == false) stopLoss = false;
            if (Trader?.signals?.FlatOlEnabled == false) flat = false;
            if (Trader?.signals?.PasGecEnabled == false) skip = false;

            if (isTimeEnabled && !isWithinTimeWindow) { buy = false; sell = false; flat = true; }
            if (isDayEnabled && !isWithinDayWindow) { buy = false; sell = false; flat = true; }

            if (skip) return TradeSignals.Skip;
            if (flat) return TradeSignals.Flat;
            if (takeProfit) return TradeSignals.TakeProfit;
            if (stopLoss) return TradeSignals.StopLoss;
            if (buy) return TradeSignals.Buy;
            if (sell) return TradeSignals.Sell;
            return TradeSignals.None;
        }

        public override bool IsValidParameterCombination()
            => channelLength > 0 && averageLength > 0 && signalLength > 0
               && overboughtLevel1 > overboughtLevel2
               && oversoldLevel1 < oversoldLevel2;

        public double[]? GetWT1() => wt1;
        public double[]? GetWT2() => wt2;
        public double[]? GetHistogram() => histogram;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var result = new Dictionary<string, double[]>();
            if (wt1 != null && wt1.Length > 0) result["WaveTrend_WT1"] = wt1;
            if (wt2 != null && wt2.Length > 0) result["WaveTrend_WT2"] = wt2;
            if (histogram != null && histogram.Length > 0) result["WaveTrend_Histogram"] = histogram;
            if (barCount > 0)
            {
                result["WaveTrend_Zero"] = CreateLevelSeries(0.0);
                result["WaveTrend_OB1"] = CreateLevelSeries(overboughtLevel1);
                result["WaveTrend_OB2"] = CreateLevelSeries(overboughtLevel2);
                result["WaveTrend_OS1"] = CreateLevelSeries(oversoldLevel1);
                result["WaveTrend_OS2"] = CreateLevelSeries(oversoldLevel2);
            }
            return result.Count > 0 ? result : null;
        }

        private double[] CreateLevelSeries(double level)
        {
            var values = new double[barCount];
            Array.Fill(values, level);
            return values;
        }
    }
}
