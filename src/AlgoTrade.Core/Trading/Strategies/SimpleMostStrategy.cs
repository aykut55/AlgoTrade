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
        // Run baglami - ILK OnStep cagrisinda Trader'dan cozulur (OnInit'te DEGIL: OnInit
        // constructor'dan calisir, SetTrader() daha sonra SetStrategy() icinde -> OnInit'te Trader null).
        private bool runContextResolved;
        private int  timeframeMinutes;    // 1, 5, 15, 60, 240 ... (0 = SymbolPeriod cozulemedi)
        private bool isOptimizationRun;   // true = opt taramasi icinde (Trader.OptimizationEnabled), false = tekli kosu


        // Parametreli constructor (data/indicators gerekli — parametresiz ctor kaldırıldı, hiç kullanılmıyordu)
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

        // Run baglamini (timeframe + opt mu) Trader'dan bir kez cozer. OnInit'te yapilamiyor
        // (orada Trader henuz null); ilk OnStep cagrisinda cagrilir.
        private void ResolveRunContext()
        {
            if (runContextResolved)
                return;

            runContextResolved = true;

            isOptimizationRun = Trader?.OptimizationEnabled == true;

            // SymbolPeriod: intraday'de dakika sayisi string'i ("5","15","240"); A/G/H/Y = Aylik/Gunluk/Haftalik/Yillik.
            // Cozulemezse (null / "" / "N/A") timeframeMinutes = 0 -> cagiran kod "bilinmiyor" diye ele alir.
            string sp = (Trader?.SymbolPeriod ?? "").Trim().ToUpperInvariant();
            timeframeMinutes = sp switch
            {
                "G" => 1440,      // 1 gun   (takvim dk)
                "H" => 10080,     // 1 hafta
                "A" => 43200,     // ~1 ay
                "Y" => 525600,    // ~1 yil  (365 * 1440)
                _   => (int.TryParse(sp, out var tf) && tf > 0) ? tf : 0
            };

            // Opt'ta konsolu bogmasin diye sadece tekli kosuda logla
            if (!isOptimizationRun)
            {
                string tfStr = Trader?.SymbolPeriod ?? "?";
                Log($"[{Name}] timeframe={tfStr} ({timeframeMinutes}dk), optRun={isOptimizationRun}");
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            ResolveRunContext();

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

            if (Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    // 0: Seviye, seviyeli
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    // 1: Yüzde, seviyeli
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesaplaSeviyeli(currentIndex, 2, 10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    // 2: Seviye, tek seviye
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesapla(currentIndex, 2000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    // 3: Yüzde, tek seviye
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesapla(currentIndex, 2.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    // 4: Anlık kar/zarar fiyat seviyesi (pozisyon bazlı)
                    takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(currentIndex, 1000.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
                    // 5: Anlık kar/zarar yüzdesi (pozisyon bazlı)
                    takeProfit = Trader.karAlZararKes.KarZararYuzdesindenKarAlHesapla(currentIndex, 3.0) != 0;
                }
            }

            if (Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    // 0: Seviye, seviyeli
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    // 1: Yüzde, seviyeli
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesaplaSeviyeli(currentIndex, -2, -10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    // 2: Seviye, tek seviye
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesapla(currentIndex, -1000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    // 3: Yüzde, tek seviye
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesapla(currentIndex, -1.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    // 4: Anlık kar/zarar fiyat seviyesi (pozisyon bazlı)
                    stopLoss = Trader.karAlZararKes.KarZararFiyatSeviyesindenZararKesHesapla(currentIndex, -500.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
                    // 5: Anlık kar/zarar yüzdesi (pozisyon bazlı)
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
