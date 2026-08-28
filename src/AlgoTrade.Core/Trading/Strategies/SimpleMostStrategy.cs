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
    /// MOST (Moving Stop Loss) İndikatörü Stratejisi
    ///
    /// MOST Mantığı:
    /// - Yükseliş trendinde: MOST fiyatın altında stop loss görevi görür
    /// - Düşüş trendinde: MOST fiyatın üstünde direnç görevi görür
    /// - Fiyat MOST'u yukarı kırınca AL (trend değişimi)
    /// - Fiyat MOST'u aşağı kırınca SAT (trend değişimi)
    ///
    /// Parametreler:
    /// - period: MOST periyodu (varsayılan 21)
    /// - percent: MOST yüzde sapması (varsayılan 1.0)
    /// - mostMaMethod: EXMOV'un hareketli ortalama tipi (varsayılan EMA - klasik MOST)
    /// - priceSource: EXMOV kaynağı + OnStep sinyal serisi (varsayılan Close - klasik MOST)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: Fiyat-MOST kırılımı        (fiyat MOST'u yukarı/aşağı kesince)
    ///     1: MOST-EXMOV kesişimi        (EXMOV MOST'u yukarı/aşağı kesince)
    ///     2: MOST slope flip           (MOST'un kendi yönü dönünce)
    ///     3: MOST state                (fiyatın MOST'a göre konumu - kesişim değil, koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi   (fiyat MOST'tan %bandThreshold'dan fazla uzaklaşınca)
    ///     5: Breakout + retest         (MOST kırılıp fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars         (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: EXMOV eğimi + MOST state  (rejim: fiyat-MOST konumu + momentum: EXMOV N-bar eğimi)
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
    public class SimpleMostStrategy : BaseStrategy
    {
        public override string Name => "Simple MOST Strategy";

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
        private readonly double percent;
        private readonly int signalModeIndex; // buy/sell yöntemi - bkz. sınıf başı doc comment (0-7)
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        // MOST EXMOV hesabı - parametreli ctor'dan gelir; verilmezse EMA + Close (klasik MOST ile birebir aynı).
        // priceSource hem MOST'un EXMOV beslemesini hem OnStep sinyal kaynağını sürer.
        private readonly PriceSource priceSource  = PriceSource.Close;
        private readonly MAMethod    mostMaMethod = MAMethod.EMA;

        private double[]? source;   // priceSource'un çözülmüş hali - OnInit'te bir kez, OnStep bundan okur
        private double[]? most;
        private double[]? exmov;

        // Parametresiz constructor (eski kullanımlar için)
        public SimpleMostStrategy(int period = 21, double percent = 1.0, MAMethod mostMaMethod = MAMethod.EMA, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period          = period;
            this.percent         = percent;
            this.mostMaMethod    = mostMaMethod;
            this.priceSource     = priceSource;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Period"]          = period;
            Parameters["Percent"]         = percent;
            Parameters["MostMaMethod"]    = mostMaMethod;
            Parameters["PriceSource"]     = priceSource;
            Parameters["RuleModeIndex"]   = ruleModeIndex;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"]   = exitModeIndex;
            Parameters["FlatModeIndex"]   = flatModeIndex;
            Parameters["SkipModeIndex"]   = skipModeIndex;
        }

        // Parametreli constructor (yeni kullanım)
        public SimpleMostStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 21, double percent = 1.0, MAMethod mostMaMethod = MAMethod.EMA, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period          = period;
            this.percent         = percent;
            this.mostMaMethod    = mostMaMethod;
            this.priceSource     = priceSource;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Period"]          = period;
            Parameters["Percent"]         = percent;
            Parameters["MostMaMethod"]    = mostMaMethod;
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

            try
            {
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

                // MOST indicator'ı hesapla (mostMaMethod / priceSource ile EXMOV parametrik)
                (most, exmov) = Indicators.Trend.MOST(period, percent, mostMaMethod, priceSource);

                // Tüm seriler OnStep'te aynı index ile birlikte okunuyor - uzunlukları uyuşmazsa
                // (örn. biri filtrelenmiş/kırpılmış gelirse) IndexOutOfRange yerine burada net hata ver
                bool allSeriesLengthsMatch = true;
                allSeriesLengthsMatch &= most.Length        == barCount;
                allSeriesLengthsMatch &= exmov.Length       == barCount;
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
                        $"most={most.Length}, exmov={exmov.Length}, source={source.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                        $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                        $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
                }

                //Log($"SimpleMostStrategy initialized: Period={period}, Percent={percent}, SignalModeIndex={signalModeIndex}");
            }
            catch (NotImplementedException)
            {
                // MOST implement edilmiş durumda (TrendIndicators.cs), bu blok normalde tetiklenmez -
                // savunma amaçlı bırakıldı, indikatör ileride kaldırılır/bozulursa sessizce crash yerine uyarı verir.
                LogWarning("MOST indicator threw NotImplementedException! Strategy will not generate signals.");
                LogWarning("Check src/Trading/Indicators/Trend/TrendIndicators.cs — MOST() implementation may be missing/broken.");

                barCount    = Indicators.BarCount;
                most        = new double[barCount];
                exmov       = new double[barCount];
                source      = new double[barCount];
                openPrices  = new double[barCount];
                highPrices  = new double[barCount];
                lowPrices   = new double[barCount];
                closePrices = new double[barCount];
                volumes     = new long[barCount];
                lotSizes    = new long[barCount];
                dateTimes   = new DateTime[barCount];
                dates       = new DateOnly[barCount];
                times       = new TimeOnly[barCount];
                epochTimes  = new long[barCount];
            }
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
            if (currentIndex < period)
                return TradeSignals.None;

            // OnInit'teki catch bloğu tetiklenip boş array birakmışsa sinyal üretme
            if (most == null || most.Length == 0)
                return TradeSignals.None;

            if (exmov == null || exmov.Length == 0)
                return TradeSignals.None;

            if (source == null || source.Length == 0)
                return TradeSignals.None;
            // ************************************************************************************************************************

            // Geçerli ve önceki değerler (source = OnInit'te priceSource'tan çözülen seri)
            double currentPrice = source[currentIndex];
            double prevPrice    = source[currentIndex - 1];
            double currentMost  = most[currentIndex];
            double prevMost     = most[currentIndex - 1];
            double currentExmov = exmov[currentIndex];
            double prevExmov    = exmov[currentIndex - 1];
            // ************************************************************************************************************************

            // signalModeIndex ile buy/sell yöntemi seçilir - detay için sınıf başı doc comment (0-7)
            if (signalModeIndex == 0)
            {
                // 0: Fiyat-MOST kırılımı - fiyat MOST'u yukarı kesince AL, aşağı kesince SAT
                if (YukarıKesti(currentIndex, source, most)) buy  = true;
                if (AsagiKesti(currentIndex, source, most))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: EXMOV-MOST kesişimi - EXMOV, MOST'u yukarı kesince AL, aşağı kesince SAT
                if (YukarıKesti(currentIndex, exmov, most)) buy  = true;
                if (AsagiKesti(currentIndex, exmov, most))  sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: MOST slope flip - MOST'un kendi yönü dönüyor (düşen/düz → yükselen = AL)
                if (currentIndex >= 2)
                {
                    double slopeNow  = most[currentIndex]     - most[currentIndex - 1];
                    double slopePrev = most[currentIndex - 1] - most[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: MOST state - fiyatın MOST'a göre konumu (kesişim değil, koşul sürdükçe her bar)
                if (Buyuk(currentIndex, source, most)) buy  = true;
                if (Kucuk(currentIndex, source, most)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi - fiyat MOST'tan %bandThreshold'dan fazla uzaklaşınca (trend-following)
                const double bandThreshold = 0.01; // %1
                if (currentMost != 0.0)
                {
                    double distanceRatio = (currentPrice - currentMost) / currentMost;
                    if (distanceRatio >  bandThreshold) buy  = true;
                    if (distanceRatio < -bandThreshold) sell = true;
                }
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest - son retestLookback bar içinde MOST kırıldı, şimdi fiyat
                //    MOST'a geri dokunup (retest) kırılım yönünde kapattıysa → sinyal
                const int retestLookback = 10;
                double barLow  = Data[currentIndex].Low;
                double barHigh = Data[currentIndex].High;

                for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                {
                    if (k < 1) continue;

                    if (!buy && YukarıKesti(k, source, most)
                        && barLow <= currentMost          // bu bar MOST'a geri dokundu (retest)
                        && currentPrice > currentMost)    // ama üstünde kapattı (retest tuttu)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(k, source, most)
                        && barHigh >= currentMost
                        && currentPrice < currentMost)
                    {
                        sell = true;
                    }
                }
            }
            else if (signalModeIndex == 6)
            {
                // 6: Confirmation bars - kırılım confirmBars bar önce oldu ve o zamandan beri fiyat
                //    hep MOST'un aynı tarafında kaldıysa gir
                const int confirmBars = 3;
                if (currentIndex >= confirmBars + 1)
                {
                    int crossBar = currentIndex - confirmBars;

                    bool stayedAbove = YukarıKesti(crossBar, source, most);
                    bool stayedBelow = AsagiKesti(crossBar, source, most);
                    for (int k = crossBar + 1; k <= currentIndex; k++)
                    {
                        stayedAbove &= source[k] > most[k];
                        stayedBelow &= source[k] < most[k];
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: EXMOV eğimi + MOST state - rejim (fiyat-MOST konumu) + momentum (EXMOV N-bar eğimi)
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool exmovRising  = exmov[currentIndex] > exmov[currentIndex - slopeLookback];
                    bool exmovFalling = exmov[currentIndex] < exmov[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, source, most) && exmovRising)  buy  = true;
                    if (Kucuk(currentIndex, source, most) && exmovFalling) sell = true;
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
        /// MOST değerlerini al (plotting veya analiz için)
        /// </summary>
        public double[]? GetMOST() => most;

        /// <summary>
        /// EXMOV değerlerini al (plotting veya analiz için)
        /// </summary>
        public double[]? GetEXMOV() => exmov;

        /// <summary>
        /// Period parametresini al
        /// </summary>
        public int Period => period;

        /// <summary>
        /// Percent parametresini al
        /// </summary>
        public double Percent => percent;

        /// <summary>
        /// Get indicators for plotting (IStrategy implementation)
        /// </summary>
        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (most != null && most.Length > 0)
                indicators["MOST"] = most;

            if (exmov != null && exmov.Length > 0)
                indicators["EXMOV"] = exmov;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
