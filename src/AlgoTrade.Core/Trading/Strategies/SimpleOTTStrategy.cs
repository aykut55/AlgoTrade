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
    /// OTT (Optimized Trend Tracker) İndikatörü Stratejisi
    ///
    /// OTT Mantığı:
    /// - MOST'un ata algoritması - ayni "MA'ya yuzde bant koy, bandi trailing-stop gibi kaydir" mantigi
    /// - Yükseliş trendinde: OTT fiyatın altında stop loss görevi görür
    /// - Düşüş trendinde: OTT fiyatın üstünde direnç görevi görür
    ///
    /// Parametreler:
    /// - period: MA periyodu (varsayılan 2)
    /// - percent: OTT yüzde sapması (varsayılan 1.4)
    /// - ottMaMethod: MA'nın hareketli ortalama tipi (varsayılan VIDYA - klasik OTT)
    /// - priceSource: MA'nın beslendiği kaynak + OnStep sinyal serisi (varsayılan Close - klasik OTT)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: Fiyat-OTT kırılımı        (fiyat OTT'yi yukarı/aşağı kesince)
    ///     1: MA-OTT kesişimi           (MA OTT'yi yukarı/aşağı kesince)
    ///     2: OTT slope flip           (OTT'un kendi yönü dönünce)
    ///     3: OTT state                (fiyatın OTT'a göre konumu - kesişim değil, koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi   (fiyat OTT'tan %bandThreshold'dan fazla uzaklaşınca)
    ///     5: Breakout + retest         (OTT kırılıp fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars         (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: MA eğimi + OTT state     (rejim: fiyat-OTT konumu + momentum: MA N-bar eğimi)
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
    public class SimpleOTTStrategy : BaseStrategy
    {
        public override string Name => "Simple OTT Strategy";

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

        // OTT MA hesabı - parametreli ctor'dan gelir; verilmezse VIDYA + Close (klasik OTT ile birebir aynı).
        // priceSource hem OTT'un MA beslemesini hem OnStep sinyal kaynağını sürer.
        private readonly PriceSource priceSource = PriceSource.Close;
        private readonly MAMethod    ottMaMethod = MAMethod.VIDYA;

        private double[]? source;   // priceSource'un çözülmüş hali - OnInit'te bir kez, OnStep bundan okur
        private double[]? ott;
        private double[]? ma;
        // Run baglami - ILK OnStep cagrisinda Trader'dan cozulur (OnInit'te DEGIL: OnInit
        // constructor'dan calisir, SetTrader() daha sonra SetStrategy() icinde -> OnInit'te Trader null).
        private bool runContextResolved;
        private int  timeframeMinutes;    // 1, 5, 15, 60, 240 ... (0 = SymbolPeriod cozulemedi)
        private bool isOptimizationRun;   // true = opt taramasi icinde (Trader.OptimizationEnabled), false = tekli kosu


        // Parametreli constructor (data/indicators gerekli — parametresiz ctor kaldırıldı, hiç kullanılmıyordu)
        public SimpleOTTStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 2, double percent = 1.4, MAMethod ottMaMethod = MAMethod.VIDYA, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period          = period;
            this.percent         = percent;
            this.ottMaMethod     = ottMaMethod;
            this.priceSource     = priceSource;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Period"]          = period;
            Parameters["Percent"]         = percent;
            Parameters["OttMaMethod"]     = ottMaMethod;
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

                // OTT indicator'ı hesapla (ottMaMethod / priceSource ile MA parametrik)
                var ottResult = Indicators.Trend.OTT(period, percent, ottMaMethod, priceSource);
                ott = ottResult.OTT;
                ma  = ottResult.MA;

                // Tüm seriler OnStep'te aynı index ile birlikte okunuyor - uzunlukları uyuşmazsa
                // (örn. biri filtrelenmiş/kırpılmış gelirse) IndexOutOfRange yerine burada net hata ver
                bool allSeriesLengthsMatch = true;
                allSeriesLengthsMatch &= ott.Length        == barCount;
                allSeriesLengthsMatch &= ma.Length         == barCount;
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
                        $"ott={ott.Length}, ma={ma.Length}, source={source.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                        $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                        $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
                }

                //Log($"SimpleOTTStrategy initialized: Period={period}, Percent={percent}, SignalModeIndex={signalModeIndex}");
            }
            catch (NotImplementedException)
            {
                // OTT/MA implement edilmiş durumda, bu blok normalde tetiklenmez -
                // savunma amaçlı bırakıldı, indikatör ileride kaldırılır/bozulursa sessizce crash yerine uyarı verir.
                LogWarning("OTT indicator threw NotImplementedException! Strategy will not generate signals.");
                LogWarning("Check src/Trading/Indicators/Trend/TrendIndicators.cs — OTT() implementation may be missing/broken.");

                barCount    = Indicators.BarCount;
                ott         = new double[barCount];
                ma          = new double[barCount];
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
            if (ott == null || ott.Length == 0)
                return TradeSignals.None;

            if (ma == null || ma.Length == 0)
                return TradeSignals.None;

            if (source == null || source.Length == 0)
                return TradeSignals.None;
            // ************************************************************************************************************************

            // Geçerli ve önceki değerler (source = OnInit'te priceSource'tan çözülen seri)
            double currentPrice = source[currentIndex];
            double prevPrice    = source[currentIndex - 1];
            double currentOtt   = ott[currentIndex];
            double prevOtt      = ott[currentIndex - 1];
            double currentMa    = ma[currentIndex];
            double prevMa       = ma[currentIndex - 1];

            if (double.IsNaN(currentOtt) || double.IsNaN(prevOtt) || double.IsNaN(currentMa) || double.IsNaN(prevMa))
                return TradeSignals.None;
            // ************************************************************************************************************************

            // signalModeIndex ile buy/sell yöntemi seçilir - detay için sınıf başı doc comment (0-7)
            if (signalModeIndex == 0)
            {
                // 0: Fiyat-OTT kırılımı - fiyat OTT'yi yukarı kesince AL, aşağı kesince SAT
                if (YukarıKesti(currentIndex, source, ott)) buy  = true;
                if (AsagiKesti(currentIndex, source, ott))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: MA-OTT kesişimi - MA, OTT'yi yukarı kesince AL, aşağı kesince SAT
                if (YukarıKesti(currentIndex, ma, ott)) buy  = true;
                if (AsagiKesti(currentIndex, ma, ott))  sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: OTT slope flip - OTT'un kendi yönü dönüyor (düşen/düz → yükselen = AL)
                if (currentIndex >= 2)
                {
                    double slopeNow  = ott[currentIndex]     - ott[currentIndex - 1];
                    double slopePrev = ott[currentIndex - 1] - ott[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: OTT state - fiyatın OTT'a göre konumu (kesişim değil, koşul sürdükçe her bar)
                if (Buyuk(currentIndex, source, ott)) buy  = true;
                if (Kucuk(currentIndex, source, ott)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi - fiyat OTT'tan %bandThreshold'dan fazla uzaklaşınca (trend-following)
                const double bandThreshold = 0.01; // %1
                if (currentOtt != 0.0)
                {
                    double distanceRatio = (currentPrice - currentOtt) / currentOtt;
                    if (distanceRatio >  bandThreshold) buy  = true;
                    if (distanceRatio < -bandThreshold) sell = true;
                }
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest - son retestLookback bar içinde OTT kırıldı, şimdi fiyat
                //    OTT'a geri dokunup (retest) kırılım yönünde kapattıysa → sinyal
                const int retestLookback = 10;
                double barLow  = Data[currentIndex].Low;
                double barHigh = Data[currentIndex].High;

                for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                {
                    if (k < 1) continue;

                    if (!buy && YukarıKesti(k, source, ott)
                        && barLow <= currentOtt           // bu bar OTT'a geri dokundu (retest)
                        && currentPrice > currentOtt)     // ama üstünde kapattı (retest tuttu)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(k, source, ott)
                        && barHigh >= currentOtt
                        && currentPrice < currentOtt)
                    {
                        sell = true;
                    }
                }
            }
            else if (signalModeIndex == 6)
            {
                // 6: Confirmation bars - kırılım confirmBars bar önce oldu ve o zamandan beri fiyat
                //    hep OTT'un aynı tarafında kaldıysa gir
                const int confirmBars = 3;
                if (currentIndex >= confirmBars + 1)
                {
                    int crossBar = currentIndex - confirmBars;

                    bool stayedAbove = YukarıKesti(crossBar, source, ott);
                    bool stayedBelow = AsagiKesti(crossBar, source, ott);
                    for (int k = crossBar + 1; k <= currentIndex; k++)
                    {
                        stayedAbove &= source[k] > ott[k];
                        stayedBelow &= source[k] < ott[k];
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: MA eğimi + OTT state - rejim (fiyat-OTT konumu) + momentum (MA N-bar eğimi)
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool maRising  = ma[currentIndex] > ma[currentIndex - slopeLookback];
                    bool maFalling = ma[currentIndex] < ma[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, source, ott) && maRising)  buy  = true;
                    if (Kucuk(currentIndex, source, ott) && maFalling) sell = true;
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

        public override bool IsValidParameterCombination()
        {
            bool isValid = true;

            return isValid;
        }

        /// <summary>
        /// OTT değerlerini al (plotting veya analiz için)
        /// </summary>
        public double[]? GetOTT() => ott;

        /// <summary>
        /// MA değerlerini al (plotting veya analiz için)
        /// </summary>
        public double[]? GetMA() => ma;

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

            if (ott != null && ott.Length > 0)
                indicators["OTT"] = ott;

            if (ma != null && ma.Length > 0)
                indicators[$"MA ({period})"] = ma;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
