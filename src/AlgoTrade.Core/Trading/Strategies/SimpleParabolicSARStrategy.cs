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
    /// Parabolic SAR (Stop and Reverse) İndikatörü Stratejisi
    ///
    /// Parabolic SAR Mantığı:
    /// - MOST/OTT/SuperTrend ailesinden, ama bant/percent yerine hızlanma faktörü (step/max) ile kuruluyor
    /// - SAR hesabı High/Low'a bağımlı olduğu için (SuperTrend'deki ATR gibi), indikatörün kendisine
    ///   priceSource verilemez - priceSource sadece OnStep'teki kırılım/state karşılaştırmasının
    ///   "fiyat" tarafını besler (indikatörü etkilemez).
    /// - Yükseliş trendinde: SAR fiyatın altında
    /// - Düşüş trendinde: SAR fiyatın üstünde
    ///
    /// Parametreler:
    /// - step: Hızlanma faktörü adımı (varsayılan 0.02)
    /// - max: Maksimum hızlanma faktörü (varsayılan 0.2)
    /// - priceSource: OnStep sinyal serisi (varsayılan Close - klasik Parabolic SAR)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: Fiyat-SAR kırılımı        (fiyat SAR'ı yukarı/aşağı kesince)
    ///     1: Trend flip                (indikatörün kendi ürettiği Trend dizisi false'dan true'ya/true'dan false'a dönünce - eski choice=0 ile birebir aynı)
    ///     2: SAR slope flip            (SAR'ın kendi yönü dönünce)
    ///     3: SAR state                 (fiyatın SAR'a göre konumu - kesişim değil, koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi   (fiyat SAR'dan %bandThreshold'dan fazla uzaklaşınca)
    ///     5: Breakout + retest         (SAR kırılıp fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars         (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: Fiyat eğimi + SAR state   (rejim: fiyat-SAR konumu + momentum: fiyatın N-bar eğimi)
    /// - exitModeIndex: takeProfit/stopLoss yöntemini seçer (Trader.karAlZararKes üzerinden):
    ///     0: Seviye, seviyeli               (SonFiyataGoreKarAl/ZararKesSeviyeHesaplaSeviyeli)
    ///     1: Yüzde, seviyeli                 (SonFiyataGoreKarAl/ZararKesYuzdeHesaplaSeviyeli)
    ///     2: Seviye, tek seviye              (SonFiyataGoreKarAl/ZararKesSeviyeHesapla)
    ///     3: Yüzde, tek seviye               (SonFiyataGoreKarAl/ZararKesYuzdeHesapla)
    ///     4: Anlık kar/zarar fiyat seviyesi  (KarZararFiyatSeviyesindenKarAl/ZararKesHesapla)
    ///     5: Anlık kar/zarar yüzdesi         (KarZararYuzdesindenKarAl/ZararKesHesapla)
    /// - flatModeIndex: flat kategorisinin dispatch parametresi - PLACEHOLDER, henuz okunmuyor
    /// - skipModeIndex: skip kategorisinin dispatch parametresi - PLACEHOLDER, henuz okunmuyor
    /// - ruleModeIndex: PLACEHOLDER, henuz okunmuyor - ileride ihtiyaç halinde kullanilacak ekstra eksen
    /// </summary>
    public class SimpleParabolicSARStrategy : BaseStrategy
    {
        public override string Name => "Simple Parabolic SAR Strategy";

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

        private readonly double step;
        private readonly double max;
        private readonly int signalModeIndex; // buy/sell yöntemi - bkz. sınıf başı doc comment (0-7)
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        // OnStep'teki "fiyat" tarafını besler - indikatörün kendisi (SAR) High/Low'a bağımlı,
        // priceSource'tan etkilenmez.
        private readonly PriceSource priceSource = PriceSource.Close;

        private double[]? source;   // priceSource'un çözülmüş hali - OnInit'te bir kez, OnStep bundan okur
        private double[]? sar;
        private bool[]?    trend;

        // Parametresiz constructor (eski kullanımlar için)
        public SimpleParabolicSARStrategy(double step = 0.02, double max = 0.2, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.step            = step;
            this.max             = max;
            this.priceSource     = priceSource;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Step"]            = step;
            Parameters["Max"]             = max;
            Parameters["PriceSource"]     = priceSource;
            Parameters["RuleModeIndex"]   = ruleModeIndex;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"]   = exitModeIndex;
            Parameters["FlatModeIndex"]   = flatModeIndex;
            Parameters["SkipModeIndex"]   = skipModeIndex;
        }

        // Parametreli constructor (yeni kullanım)
        public SimpleParabolicSARStrategy(List<StockData> data, IndicatorManager indicators,
            double step = 0.02, double max = 0.2, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.step            = step;
            this.max             = max;
            this.priceSource     = priceSource;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Step"]            = step;
            Parameters["Max"]             = max;
            Parameters["PriceSource"]     = priceSource;
            Parameters["RuleModeIndex"]   = ruleModeIndex;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"]   = exitModeIndex;
            Parameters["FlatModeIndex"]   = flatModeIndex;
            Parameters["SkipModeIndex"]   = skipModeIndex;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            // verileri oku
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

            // Parabolic SAR indicator'ı hesapla (step/max ile - High/Low kullanır, priceSource'tan bağımsız)
            var sarResult = Indicators.Trend.ParabolicSAR(step, max);
            sar   = sarResult.SAR;
            trend = sarResult.Trend;

            // Tüm seriler OnStep'te aynı index ile birlikte okunuyor - uzunlukları uyuşmazsa
            // (örn. biri filtrelenmiş/kırpılmış gelirse) IndexOutOfRange yerine burada net hata ver
            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= sar.Length        == barCount;
            allSeriesLengthsMatch &= trend.Length      == barCount;
            allSeriesLengthsMatch &= source.Length     == barCount;
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
                    $"sar={sar.Length}, trend={trend.Length}, source={source.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                    $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                    $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
            }

            //Log($"SimpleParabolicSARStrategy initialized: Step={step}, Max={max}, SignalModeIndex={signalModeIndex}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy        = false;
            bool sell       = false;
            bool takeProfit = false;
            bool stopLoss   = false;
            bool flat       = false;
            bool skip       = false;
            // ************************************************************************************************************************

            // İlk barlarda yeterli veri yok
            if (currentIndex < 2)
                return TradeSignals.None;

            if (sar == null || sar.Length == 0)
                return TradeSignals.None;

            if (trend == null || trend.Length == 0)
                return TradeSignals.None;

            if (source == null || source.Length == 0)
                return TradeSignals.None;
            // ************************************************************************************************************************

            // Geçerli ve önceki değerler (source = OnInit'te priceSource'tan çözülen seri)
            double currentPrice = source[currentIndex];
            double prevPrice    = source[currentIndex - 1];
            double currentSar   = sar[currentIndex];
            double prevSar      = sar[currentIndex - 1];
            bool   currentTrend = trend[currentIndex];
            bool   prevTrend    = trend[currentIndex - 1];
            // ************************************************************************************************************************

            // signalModeIndex ile buy/sell yöntemi seçilir - detay için sınıf başı doc comment (0-7)
            if (signalModeIndex == 0)
            {
                // 0: Fiyat-SAR kırılımı - fiyat SAR'ı yukarı kesince AL, aşağı kesince SAT
                if (YukarıKesti(currentIndex, source, sar)) buy  = true;
                if (AsagiKesti(currentIndex, source, sar))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: Trend flip - indikatörün kendi Trend dizisi false'dan true'ya (AL) / true'dan false'a (SAT) dönüyor
                if (!prevTrend && currentTrend) buy  = true;
                if (prevTrend && !currentTrend) sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: SAR slope flip - SAR'ın kendi yönü dönüyor (düşen/düz → yükselen = AL)
                if (currentIndex >= 2)
                {
                    double slopeNow  = sar[currentIndex]     - sar[currentIndex - 1];
                    double slopePrev = sar[currentIndex - 1] - sar[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: SAR state - fiyatın SAR'a göre konumu (kesişim değil, koşul sürdükçe her bar)
                if (Buyuk(currentIndex, source, sar)) buy  = true;
                if (Kucuk(currentIndex, source, sar)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi - fiyat SAR'dan %bandThreshold'dan fazla uzaklaşınca (trend-following)
                const double bandThreshold = 0.01; // %1
                if (currentSar != 0.0)
                {
                    double distanceRatio = (currentPrice - currentSar) / currentSar;
                    if (distanceRatio >  bandThreshold) buy  = true;
                    if (distanceRatio < -bandThreshold) sell = true;
                }
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest - son retestLookback bar içinde SAR kırıldı, şimdi fiyat
                //    SAR'a geri dokunup (retest) kırılım yönünde kapattıysa → sinyal
                const int retestLookback = 10;
                double barLow  = Data[currentIndex].Low;
                double barHigh = Data[currentIndex].High;

                for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                {
                    if (k < 1) continue;

                    if (!buy && YukarıKesti(k, source, sar)
                        && barLow <= currentSar           // bu bar SAR'a geri dokundu (retest)
                        && currentPrice > currentSar)     // ama üstünde kapattı (retest tuttu)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(k, source, sar)
                        && barHigh >= currentSar
                        && currentPrice < currentSar)
                    {
                        sell = true;
                    }
                }
            }
            else if (signalModeIndex == 6)
            {
                // 6: Confirmation bars - kırılım confirmBars bar önce oldu ve o zamandan beri fiyat
                //    hep SAR'ın aynı tarafında kaldıysa gir
                const int confirmBars = 3;
                if (currentIndex >= confirmBars + 1)
                {
                    int crossBar = currentIndex - confirmBars;

                    bool stayedAbove = YukarıKesti(crossBar, source, sar);
                    bool stayedBelow = AsagiKesti(crossBar, source, sar);
                    for (int k = crossBar + 1; k <= currentIndex; k++)
                    {
                        stayedAbove &= source[k] > sar[k];
                        stayedBelow &= source[k] < sar[k];
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: Fiyat eğimi + SAR state - rejim (fiyat-SAR konumu) + momentum (fiyatın N-bar eğimi)
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool priceRising  = source[currentIndex] > source[currentIndex - slopeLookback];
                    bool priceFalling = source[currentIndex] < source[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, source, sar) && priceRising)  buy  = true;
                    if (Kucuk(currentIndex, source, sar) && priceFalling) sell = true;
                }
            }
            // ************************************************************************************************************************

            // ÖRNEK: Trader referansını kullanarak kar al / zarar kes hesaplama
            // Trader property'si BaseStrategy.SetTrader() ile otomatik set edilir
            if (Trader != null)
            {
                // Trader.flags.KarAlSeviyeHesaplaEnabled kapaliysa metod iceride 0 doner(takeProfit hep false kalir)
                if (exitModeIndex == 0)
                {
                    // 0: Seviye, seviyeli
                    if (Trader.flags?.KarAlSeviyeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    // 1: Yüzde, seviyeli
                    if (Trader.flags?.KarAlYuzdeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesaplaSeviyeli(currentIndex, 2, 10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    // 2: Seviye, tek seviye
                    if (Trader.flags?.KarAlSeviyeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesapla(currentIndex, 2000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    // 3: Yüzde, tek seviye
                    if (Trader.flags?.KarAlYuzdeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesapla(currentIndex, 2.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    // 4: Anlık kar/zarar fiyat seviyesi (pozisyon bazlı)
                    if (Trader.flags?.KarAlSeviyeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(currentIndex, 1000.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
                    // 5: Anlık kar/zarar yüzdesi (pozisyon bazlı)
                    if (Trader.flags?.KarAlYuzdeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.KarZararYuzdesindenKarAlHesapla(currentIndex, 3.0) != 0;
                }
            }

            if (Trader != null)
            {
                // Trader.flags.ZararKesSeviyeHesaplaEnabled kapaliysa metod iceride 0 doner(stopLoss hep false kalir)
                if (exitModeIndex == 0)
                {
                    // 0: Seviye, seviyeli
                    if (Trader.flags?.ZararKesSeviyeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    // 1: Yüzde, seviyeli
                    if (Trader.flags?.ZararKesYuzdeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesaplaSeviyeli(currentIndex, -2, -10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    // 2: Seviye, tek seviye
                    if (Trader.flags?.ZararKesSeviyeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesapla(currentIndex, -1000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    // 3: Yüzde, tek seviye
                    if (Trader.flags?.ZararKesYuzdeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesapla(currentIndex, -1.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    // 4: Anlık kar/zarar fiyat seviyesi (pozisyon bazlı)
                    if (Trader.flags?.ZararKesSeviyeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.KarZararFiyatSeviyesindenZararKesHesapla(currentIndex, -500.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
                    // 5: Anlık kar/zarar yüzdesi (pozisyon bazlı)
                    if (Trader.flags?.ZararKesYuzdeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.KarZararYuzdesindenZararKesHesapla(currentIndex, -2.0) != 0;
                }
            }
            // ************************************************************************************************************************

            if (flatModeIndex == 0)
            {
                // Flat olma durumu burada incelenir ve flat flag'i setlenir
                flat = false;
            }
            // ************************************************************************************************************************

            if (skipModeIndex == 0)
            {
                // Skip olma durumu burada incelenir ve skip flag'i setlenir
                skip = false;
            }
            // ************************************************************************************************************************

            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // Sinyal önceliklendirmesi
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            if (skip)
            {
                return TradeSignals.Skip;
            }
            else if (flat)
            {
                return TradeSignals.Flat;
            }
            else if (takeProfit)
            {
                return TradeSignals.TakeProfit;
            }
            else if (stopLoss)
            {
                return TradeSignals.StopLoss;
            }
            else if (buy)
            {
                return TradeSignals.Buy;
            }
            else if (sell)
            {
                return TradeSignals.Sell;
            }
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // ************************************************************************************************************************

            return TradeSignals.None;
        }

        /// <summary>
        /// SAR değerlerini al (plotting veya analiz için)
        /// </summary>
        public double[]? GetSAR() => sar;

        /// <summary>
        /// Trend değerlerini al (plotting veya analiz için)
        /// </summary>
        public bool[]? GetTrend() => trend;

        /// <summary>
        /// Step parametresini al
        /// </summary>
        public double Step => step;

        /// <summary>
        /// Max parametresini al
        /// </summary>
        public double Max => max;

        /// <summary>
        /// Get indicators for plotting (IStrategy implementation)
        /// </summary>
        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (sar != null && sar.Length > 0)
                indicators["SAR"] = sar;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
