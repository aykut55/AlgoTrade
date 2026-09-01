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
    /// PMax (Profit Maximizer) İndikatörü Stratejisi
    ///
    /// PMax Mantığı:
    /// - MOST + SuperTrend hibrit yapısı, ATR tabanlı trailing stop + MA kombinasyonu
    /// - ATR High/Low/Close'a bağımlı olduğu için priceSource yok (SuperTrend/SAR gibi)
    ///
    /// Parametreler:
    /// - atrPeriod: ATR periyodu (varsayılan 10)
    /// - multiplier: ATR çarpanı (varsayılan 3.0)
    /// - maPeriod: MA periyodu (varsayılan 10)
    /// - pmaxMaMethod: PMax'ın MA tipi (varsayılan EMA)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: Fiyat-PMax kırılımı        (fiyat PMax'ı yukarı/aşağı kesince)
    ///     1: Direction flip              (indikatörün kendi Direction dizisi -1'den 1'e/1'den -1'e dönünce - eski choice=0 ile birebir aynı)
    ///     2: PMax slope flip             (PMax'ın kendi yönü dönünce)
    ///     3: PMax state                  (fiyatın PMax'a göre konumu - koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi     (fiyat PMax'tan %bandThreshold'dan fazla uzaklaşınca)
    ///     5: Breakout + retest           (PMax kırılıp fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars           (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: Fiyat eğimi + PMax state    (rejim: fiyat-PMax konumu + momentum: fiyatın N-bar eğimi)
    /// - exitModeIndex: takeProfit/stopLoss yöntemini seçer (Trader.karAlZararKes üzerinden):
    ///     0: Seviye, seviyeli   1: Yüzde, seviyeli   2: Seviye, tek seviye   3: Yüzde, tek seviye
    ///     4: Anlık kar/zarar fiyat seviyesi   5: Anlık kar/zarar yüzdesi
    /// - flatModeIndex/skipModeIndex/ruleModeIndex: PLACEHOLDER, henuz okunmuyor
    ///
    /// Sinyal gate'i (OnStep sonu, oncelik zincirinden hemen once):
    /// 6 sinyal (buy/sell/takeProfit/stopLoss/flat/skip) once strateji tarafindan hicbir trader
    /// flag'ine bakilmadan uretilir; sonra ilgili enable flag'i ACIKCA false olan (Trader.signals.
    /// AlEnabled/SatEnabled/KarAlEnabled/ZararKesEnabled/FlatOlEnabled/PasGecEnabled) sinyaller
    /// sifirlanir. Amac: ust-oncelikli ama uygulanmayacak bir sinyal (or. KarAlEnabled=false iken
    /// takeProfit) oncelik zincirinde alttaki gecerli sinyali (or. sell) sessizce ezmesin -
    /// zincir tek TradeSignals dondurdugu icin ezilen sinyal trader'a hic ulasmaz.
    /// MapStrategyCommandsToTradeCommands() ayni flag'leri ayrica kontrol eder (defensif ikinci katman).
    /// </summary>
    public class SimplePMaxStrategy : BaseStrategy
    {
        public override string Name => "Simple PMax Strategy";

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
        private readonly double multiplier;
        private readonly int maPeriod;
        private readonly int signalModeIndex;
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        private readonly MAMethod pmaxMaMethod = MAMethod.EMA;

        private double[]? pmax;
        private double[]? pmaxMA;
        private int[]?    direction;

        public SimplePMaxStrategy(List<StockData> data, IndicatorManager indicators,
            int atrPeriod = 10, double multiplier = 3.0, int maPeriod = 10, MAMethod pmaxMaMethod = MAMethod.EMA,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.atrPeriod       = atrPeriod;
            this.multiplier      = multiplier;
            this.maPeriod        = maPeriod;
            this.pmaxMaMethod    = pmaxMaMethod;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["AtrPeriod"]      = atrPeriod;
            Parameters["Multiplier"]     = multiplier;
            Parameters["MaPeriod"]       = maPeriod;
            Parameters["PmaxMaMethod"]   = pmaxMaMethod;
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

            var pmaxResult = Indicators.Trend.PMax(atrPeriod, multiplier, maPeriod, pmaxMaMethod);
            pmax      = pmaxResult.PMax;
            pmaxMA    = pmaxResult.PMaxMA;
            direction = pmaxResult.Direction;

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= pmax.Length        == barCount;
            allSeriesLengthsMatch &= direction.Length   == barCount;
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
                    $"pmax={pmax.Length}, direction={direction.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                    $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                    $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false, sell = false, takeProfit = false, stopLoss = false, flat = false, skip = false;

            if (currentIndex < Math.Max(atrPeriod, maPeriod) + 1)
                return TradeSignals.None;

            if (pmax == null || direction == null || closePrices == null || pmax.Length == 0)
                return TradeSignals.None;

            double currentPrice = closePrices[currentIndex];
            double currentPMax = pmax[currentIndex];
            int currentDirection = direction[currentIndex];
            int prevDirection = direction[currentIndex - 1];

            if (double.IsNaN(currentPMax))
                return TradeSignals.None;

            if (signalModeIndex == 0)
            {
                // 0: Fiyat-PMax kırılımı (klasik)
                if (YukarıKesti(currentIndex, closePrices, pmax)) buy  = true;
                if (AsagiKesti(currentIndex, closePrices, pmax))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: Direction flip - indikatörün kendi Direction dizisi
                if (prevDirection == -1 && currentDirection == 1) buy  = true;
                if (prevDirection == 1  && currentDirection == -1) sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: PMax slope flip
                if (currentIndex >= 2)
                {
                    double slopeNow  = pmax[currentIndex]     - pmax[currentIndex - 1];
                    double slopePrev = pmax[currentIndex - 1] - pmax[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: PMax state
                if (Buyuk(currentIndex, closePrices, pmax)) buy  = true;
                if (Kucuk(currentIndex, closePrices, pmax)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi
                const double bandThreshold = 0.01; // %1
                if (currentPMax != 0.0)
                {
                    double distanceRatio = (currentPrice - currentPMax) / currentPMax;
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

                    if (!buy && YukarıKesti(m, closePrices, pmax)
                        && barLow <= currentPMax
                        && currentPrice > currentPMax)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(m, closePrices, pmax)
                        && barHigh >= currentPMax
                        && currentPrice < currentPMax)
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

                    bool stayedAbove = YukarıKesti(crossBar, closePrices, pmax);
                    bool stayedBelow = AsagiKesti(crossBar, closePrices, pmax);
                    for (int m = crossBar + 1; m <= currentIndex; m++)
                    {
                        stayedAbove &= closePrices[m] > pmax[m];
                        stayedBelow &= closePrices[m] < pmax[m];
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: Fiyat eğimi + PMax state
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool priceRising  = closePrices[currentIndex] > closePrices[currentIndex - slopeLookback];
                    bool priceFalling = closePrices[currentIndex] < closePrices[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, closePrices, pmax) && priceRising)  buy  = true;
                    if (Kucuk(currentIndex, closePrices, pmax) && priceFalling) sell = true;
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

            // ------------------------------------------------------------------------------------------------------------------
            // SINYAL GATE'I - nihai önceliklendirmeden hemen ÖNCE.
            // Yukarıda 6 sinyal (buy/sell/takeProfit/stopLoss/flat/skip) strateji tarafından hiçbir trader
            // flag'ine bakılmadan, tamamen kendi mantığıyla üretildi. Burada, öncelik zincirine girmeden,
            // ilgili enable flag'i AÇIKÇA false olanları sıfırlıyoruz.
            //
            // Neden burada (zincirden önce): OnStep tek bir TradeSignals döndürür ve aşağıdaki öncelik
            // zinciri (skip > flat > TP > SL > buy > sell) ilk true olanı döndürüp gerisini atar. Üst
            // öncelikli ama trader'ın uygulamayacağı bir sinyal (ör. KarAlEnabled=false iken takeProfit)
            // true kalırsa, zincir TakeProfit döndürür; MapStrategyCommandsToTradeCommands() onu flag
            // kapalı diye düşürür - ve o barın ALTTAKİ geçerli sinyali (ör. Sell) hiç döndürülmediği için
            // SESSİZCE KAYBOLUR. Güçlü trendde bu pozisyonu kilitler (poz kâra geçince her bar
            // takeProfit=true, her death-cross yutulur, poz dönmez). Gate zincirden önce olunca kapalı
            // sinyal daha zincire girmeden elenir, zincir bir sonraki geçerli sinyale düşer.
            //
            // Neden "== false" (,"!= true" değil): Trader/signals null ise (strateji trader'sız çalışırsa,
            // ör. birim test) "== false" -> false, sinyal sıfırlanmaz; strateji ham üretici gibi davranır.
            //
            // MapStrategyCommandsToTradeCommands() aynı flag'leri bir kez daha kontrol eder (çift katman)
            // ama tek başına shadowing'i çözemez - asıl düzeltme buradaki gate. Trader.flags.
            // KarAlSeviyeHesaplaEnabled de korumaz: ilk trade'den sonra koşulsuz true olur, niyeti yansıtmaz.
            // ------------------------------------------------------------------------------------------------------------------
            if (Trader?.signals?.AlEnabled      == false) buy        = false;
            if (Trader?.signals?.SatEnabled     == false) sell       = false;
            if (Trader?.signals?.KarAlEnabled   == false) takeProfit = false;
            if (Trader?.signals?.ZararKesEnabled== false) stopLoss   = false;
            if (Trader?.signals?.FlatOlEnabled  == false) flat       = false;
            if (Trader?.signals?.PasGecEnabled  == false) skip       = false;

            if (skip) return TradeSignals.Skip;
            else if (flat) return TradeSignals.Flat;
            else if (takeProfit) return TradeSignals.TakeProfit;
            else if (stopLoss) return TradeSignals.StopLoss;
            else if (buy) return TradeSignals.Buy;
            else if (sell) return TradeSignals.Sell;

            return TradeSignals.None;
        }

        public double[]? GetPMax() => pmax;
        public double[]? GetMA() => pmaxMA;
        public int[]? GetDirection() => direction;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            if (pmax != null && pmax.Length > 0) indicators["PMax"] = pmax;
            if (pmaxMA != null && pmaxMA.Length > 0) indicators["MA"] = pmaxMA;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
