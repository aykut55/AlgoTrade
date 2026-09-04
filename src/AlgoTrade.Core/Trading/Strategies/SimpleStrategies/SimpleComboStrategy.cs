using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
using static AlgoTrade.Core.Trading.Utils.Utils;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// Combo Stratejisi - Sabit bir "seri katalogu" (MA/RSI/MACD/SuperTrend...) uzerinden,
    /// AL/SAT sinyallerini ruleModeIndex ile secilen kurala gore ONCEDEN (OnInit sirasinda, bir kere)
    /// hesaplayip bool[] dizilerine (buySignals/sellSignals/flatSignals) yazar. OnStep sadece bu
    /// dizilerden okur, hicbir hesaplama yapmaz - SimpleMostStrategy'nin _most/_exmov deseniyle ayni.
    ///
    /// Karsilastirmalar AlgoTrade.Core.Trading.Utils.Utils'teki hazir fonksiyonlarla yapiliyor
    /// (Buyuk/Kucuk/BuyukEsit/KucukEsit/YukarıKesti/AsagiKesti - using static ile onek gerekmeden
    /// cagriliyor). Bu fonksiyonlar ayni bar'da IKI FARKLI diziyi kiyaslar; ayni dizinin farkli
    /// lag'lerini (orn. Rsi[-3] vs Rsi[-1]) kiyaslamak icin uygun degiller, o durumlarda ham
    /// (i-3]/[i-1] gibi) karsilastirma yapiliyor, kendi sinir kontrolumuzle.
    ///
    /// Tasarim amaci: yeni bir indikator veya yeni bir kural denemek icin ne constructor imzasi
    /// ne de yeni bir Strategy sinifi gerekiyor:
    /// - Yeni indikator eklemek -> BuildSeriesCatalog()'a bir satir eklenir.
    /// - Yeni kural denemek     -> BuildSignals()'a bir else-if dali eklenir.
    ///
    /// =====================================================================================
    /// "...ModeIndex" PARAMETRELERI (2026-08-27 karari) - OnStep'in urettigi 4 sinyal kategorisine
    /// (buy/sell, takeProfit/stopLoss, flat, skip) birebir karsilik gelen, birbirinden BAGIMSIZ
    /// 5 parametre var. Her biri farkli bir soruya cevap veriyor, biri digerinin yerini tutmuyor:
    ///
    /// - ruleModeIndex   : buy/sell koşulunun ICERIGI ne? (hangi indikatorler, hangi karsilastirma -
    ///                     BuildSignals()'daki hangi else-if dali). AKTIF.
    /// - buySignalModeIndex/sellSignalModeIndex : ruleModeIndex'in urettigi buySignals/sellSignals
    ///                     dizilerinin HER BIRI NE ZAMAN sinyale donusur - buy ve sell icin AYRI AYRI
    ///                     secilir (asymmetric - buy baska bir zamanlamadan, sell baska bir
    ///                     zamanlamadan gelebilir). Ikisi de ayni mod kumesinden secilir:
    ///                     0: siralama/seviye bazli (koşul true oldugu surece HER barda tekrarlanir),
    ///                     1: kesisim bazli (sadece false->true GECIS aninda, bir kere). AKTIF,
    ///                     OnStep'te dallanan parametreler bunlar.
    /// - takeProfitExitModeIndex/stopLossExitModeIndex : takeProfit/stopLoss yöntemini AYRI AYRI
    ///                     seçer (Trader.karAlZararKes üzerinden, diğer 21 Simple*Strategy'yle AYNI
    ///                     0-5 dispatch'i - 2026-08-31'de tamamlandı, önceden sadece 0 uygulanıyordu):
    ///                     0: Seviye, seviyeli   1: Yüzde, seviyeli   2: Seviye, tek seviye
    ///                     3: Yüzde, tek seviye  4: Anlık kar/zarar fiyat seviyesi
    ///                     5: Anlık kar/zarar yüzdesi. AKTIF.
    /// - flatModeIndex   : flat'e gecis NE ZAMAN tetiklensin. PLACEHOLDER - şu an flat icin hicbir
    ///                     kural tanimli degil (flatSignals hep false), bu parametrenin henuz
    ///                     hicbir etkisi yok.
    /// - skipModeIndex   : skip NE ZAMAN tetiklensin. PLACEHOLDER - şu an skip icin hicbir kural
    ///                     tanimli degil (OnStep'te hep false), bu parametrenin henuz hicbir
    ///                     etkisi yok.
    ///
    /// takeProfitExitModeIndex/stopLossExitModeIndex/flatModeIndex/skipModeIndex BILEREK, henuz hicbir mantik onlari okumadigi
    /// halde eklendi - amac, ileride o kategoriye gercek bir kural yazildiginda constructor imzasini
    /// (ve dolayisiyla tum Config_01/03 script'lerini, StrategyConfig.txt kayitlarini) DEGISTIRMEK
    /// ZORUNDA KALMAMAK. Yeri/ismi bastan belli, sadece OnStep icinde ilgili yerde bu parametreye
    /// gore dallanma eklenecek (buySignalModeIndex/sellSignalModeIndex'in su an yaptigi gibi).
    /// =====================================================================================
    ///
    /// VERSIYONLAMA KARARI (2026-08-27): ruleModeIndex/BuildSignals() yaklasimi (ayni sinif icinde
    /// birden fazla kural setini index ile secmek) periyot gibi surekli/sayisal parametreleri
    /// taramak icin uygun degil (opt sonuclari "hangi ruleModeIndex" bazinda anlamsiz geliyor) -
    /// onun yerine periyotlar (ma1Period/ma2Period/ma3Period/rsiPeriod gibi) dogrudan constructor
    /// parametresi yapiliyor, boylece optimizer bunlari AddOptimizationParameterRange ile
    /// gercekten tarayabiliyor.
    ///
    /// Yeni bir indikator/kural KOMBINASYONU denerken (orn. "3 MA siralamasi" -> "3 MA + RSI
    /// filtresi") bu sinifi elle degistirmek YERINE, YENI BIR SINIF acilir: ComboStrategy0001,
    /// ComboStrategy0002, ... (numaralandirilmis, immutable denemeler). Sebep: ayni sinifi
    /// surekli degistirmek onceki denemeleri bozuyor/kayboluyor; her deneme kendi dosyasinda
    /// sabit kalirsa hicbiri kaybolmaz, git gecmisi de zaten versiyon farkini tutar.
    ///
    /// StrategyRegistry.AutoRegister() yeni ComboStrategyNNNN sinifini OTOMATIK bulur (ayri bir
    /// kayit adimi gerekmez, sadece optimizationStrategyName/strategyName string'ine yeni sinif
    /// adini yazmak yeterli). OTOMATIK OLMAYAN kisim: Config_01_SingleTrader.csx /
    /// Config_03_SingleTraderOpt.csx'teki strategyChoice/optChoice bloguna, o yeni sinifa ozel
    /// parametre/range setiyle yeni bir else-if dali eklemek HALA ELLE yapiliyor - her
    /// ComboStrategyNNNN'in constructor'i farkli oldugu icin bu otomatiklesemiyor.
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
    public class SimpleComboStrategy : BaseStrategy
    {
        public override string Name => "Simple Combo Strategy";

        // buySignalModeIndex/sellSignalModeIndex/takeProfitExitModeIndex/stopLossExitModeIndex/
        // flatModeIndex/skipModeIndex/ruleModeIndex artik BaseStrategy'de tanimli (protected, readonly
        // degil) - degerleri asagida constructor'da parametre olarak atanir.
        private Dictionary<string, double[]>? series;

        // barCount artik BaseStrategy'de (protected) - LoadCommonSeries() tarafindan Initialize()
        // icinde OnInit()'ten once doldurulur, burada tekrar tanimlanmaz.
        private bool[]? buySignals;
        private bool[]? sellSignals;
        private bool[]? flatSignals;

        // runContextResolved/timeframeMinutes/isOptimizationRun/ResolveRunContext() artik
        // BaseStrategy'de (protected) - burada tekrar tanimlanmaz.


        // Parametreli constructor (yeni kullanim) - field'lar underscore'suz oldugu icin, ayni
        // isimdeki constructor parametresinden ayirt etmek amaciyla burada "this." kullanmak
        // ZORUNLU (this. olmadan "ruleModeIndex = ruleModeIndex;" parametreyi kendine atar,
        // field hep 0 kalir - digger metotlarda boyle bir isim çakışması olmadigi icin "this."
        // gerekmiyor).
        public SimpleComboStrategy(List<StockData> data, IndicatorManager indicators,
            int ruleModeIndex = 0, int buySignalModeIndex = 0, int sellSignalModeIndex = 0, int takeProfitExitModeIndex = 0, int stopLossExitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0)
        {
            this.ruleModeIndex           = ruleModeIndex;
            this.buySignalModeIndex      = buySignalModeIndex;
            this.sellSignalModeIndex     = sellSignalModeIndex;
            this.takeProfitExitModeIndex = takeProfitExitModeIndex;
            this.stopLossExitModeIndex   = stopLossExitModeIndex;
            this.flatModeIndex           = flatModeIndex;
            this.skipModeIndex           = skipModeIndex;

            // Gun ici saat penceresi / tarih penceresi / triggerTime - alanlar BaseStrategy'de tanimli,
            // degerleri burada (sabit, kod icinde) atanir.
            startTime            = TimeOnly.ParseExact("10:05:00", "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            stopTime             = TimeOnly.ParseExact("16:45:00", "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            triggerTime          = TimeOnly.ParseExact("14:07:00", "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            startDay             = default;
            stopDay              = default;
            isTimeEnabled        = false;
            isDayEnabled         = false;
            isTriggerTimeEnabled = false;

            Parameters["RuleModeIndex"]           = ruleModeIndex;
            Parameters["BuySignalModeIndex"]      = buySignalModeIndex;
            Parameters["SellSignalModeIndex"]     = sellSignalModeIndex;
            Parameters["TakeProfitExitModeIndex"] = takeProfitExitModeIndex;
            Parameters["StopLossExitModeIndex"]   = stopLossExitModeIndex;
            Parameters["FlatModeIndex"]           = flatModeIndex;
            Parameters["SkipModeIndex"]           = skipModeIndex;
            Parameters["BuyModeEnabled"]          = buyModeEnabled;
            Parameters["SellModeEnabled"]         = sellModeEnabled;
            Parameters["TakeProfitExitModeEnabled"]   = takeProfitExitModeEnabled;
            Parameters["StopLossExitModeEnabled"]     = stopLossExitModeEnabled;
            Parameters["FlatModeEnabled"]         = flatModeEnabled;
            Parameters["SkipModeEnabled"]         = skipModeEnabled;

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            series = BuildSeriesCatalog(Indicators);

            barCount    = series["Close"].Length;
            buySignals  = new bool[barCount];
            sellSignals = new bool[barCount];
            flatSignals = new bool[barCount];

            // Tüm seriler BuildSignals()/OnStep'te aynı index ile birlikte okunuyor - uzunlukları
            // uyuşmazsa (örn. biri filtrelenmiş/kırpılmış gelirse) IndexOutOfRange yerine burada
            // net hata ver. Diğer 21 stratejideki allSeriesLengthsMatch guard'ının dictionary
            // üzerinden döngüyle yazılmış hali - burada seri sayısı sabit değil (BuildSeriesCatalog
            // genişleyebilir), o yüzden isim isim AND zinciri yerine foreach kullanılıyor.
            foreach (var kvp in series)
            {
                if (kvp.Value.Length != barCount)
                {
                    throw new InvalidOperationException(
                        $"Seri uzunlukları uyuşmuyor (barCount={barCount}): {kvp.Key}={kvp.Value.Length}");
                }
            }

            BuildSignals(ruleModeIndex);
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

            ResolveRunContext(currentIndex);

            TimeOnly currentTime    = times![currentIndex];
            DateOnly currentDate    = dates![currentIndex];
            bool isWithinTimeWindow = !isTimeEnabled || (currentTime >= startTime && currentTime <= stopTime);
            bool isWithinDayWindow  = !isDayEnabled  || (currentDate >= startDay  && currentDate <= stopDay);
            bool isTriggerTime      = isTriggerTimeEnabled && currentTime == triggerTime;
            // ************************************************************************************************************************

            // Ilk barlarda yeterli veri yok
            if (currentIndex < 1)
                return TradeSignals.None;

            // OnInit henuz calismamis / kural kurulmamissa sinyal uretme
            if (series == null || buySignals == null || sellSignals == null || flatSignals == null)
                return TradeSignals.None;
            // ************************************************************************************************************************

            // Gecerli ve onceki degerler
            double currentPrice = Data[currentIndex].Close;
            double prevPrice    = Data[currentIndex - 1].Close;
            // ************************************************************************************************************************

                 if (isOneMinute)       { }
            else if (isFiveMinute)      { }
            else if (isOneHour)         { }
            else if (isFourHour)        { }
            else if (isOneDay)          { }

            if (isFirstOfDay)           { }
            if (isLastOfDay)            { }
            if (isFirstOfWeek)          { }
            if (isFirstOfMonth)         { }
                 
                 if (isSonYonA)         { }
            else if (isSonYonS)         { }
            else if (isSonYonF)         { }
            // ************************************************************************************************************************

            if (buyModeEnabled)
            {
                if (buySignalModeIndex == 0)
                {
                    // Siralama (level) bazli: sinyal true oldugu her barda tekrarlanir
                    if (buySignals[currentIndex])
                        buy = true;
                }
                else
                {
                    // Kesisim (crossover) bazli: sadece false->true gecis aninda (bir kere) sinyal
                    if (buySignals[currentIndex] && !buySignals[currentIndex - 1])
                        buy = true;
                }
            }

            if (sellModeEnabled)
            {
                if (sellSignalModeIndex == 0)
                {
                    // Siralama (level) bazli: sinyal true oldugu her barda tekrarlanir
                    if (sellSignals[currentIndex])
                        sell = true;
                }
                else
                {
                    // Kesisim (crossover) bazli: sadece false->true gecis aninda (bir kere) sinyal
                    if (sellSignals[currentIndex] && !sellSignals[currentIndex - 1])
                        sell = true;
                }
            }
            // ************************************************************************************************************************

            if (takeProfitExitModeEnabled && Trader != null)
            {
                if (takeProfitExitModeIndex == 0)
                {
                    // 0: Seviye, seviyeli
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
                }
                else if (takeProfitExitModeIndex == 1)
                {
                    // 1: Yüzde, seviyeli
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesaplaSeviyeli(currentIndex, 2, 10, 0.01) != 0;
                }
                else if (takeProfitExitModeIndex == 2)
                {
                    // 2: Seviye, tek seviye
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesapla(currentIndex, 2000.0) != 0;
                }
                else if (takeProfitExitModeIndex == 3)
                {
                    // 3: Yüzde, tek seviye
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesapla(currentIndex, 2.0) != 0;
                }
                else if (takeProfitExitModeIndex == 4)
                {
                    // 4: Anlık kar/zarar fiyat seviyesi (pozisyon bazlı)
                    takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(currentIndex, 1000.0) != 0;
                }
                else if (takeProfitExitModeIndex == 5)
                {
                    // 5: Anlık kar/zarar yüzdesi (pozisyon bazlı)
                    takeProfit = Trader.karAlZararKes.KarZararYuzdesindenKarAlHesapla(currentIndex, 3.0) != 0;
                }
            }

            if (stopLossExitModeEnabled && Trader != null)
            {
                if (stopLossExitModeIndex == 0)
                {
                    // 0: Seviye, seviyeli
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
                }
                else if (stopLossExitModeIndex == 1)
                {
                    // 1: Yüzde, seviyeli
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesaplaSeviyeli(currentIndex, -2, -10, 0.01) != 0;
                }
                else if (stopLossExitModeIndex == 2)
                {
                    // 2: Seviye, tek seviye
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesapla(currentIndex, -1000.0) != 0;
                }
                else if (stopLossExitModeIndex == 3)
                {
                    // 3: Yüzde, tek seviye
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesapla(currentIndex, -1.0) != 0;
                }
                else if (stopLossExitModeIndex == 4)
                {
                    // 4: Anlık kar/zarar fiyat seviyesi (pozisyon bazlı)
                    stopLoss = Trader.karAlZararKes.KarZararFiyatSeviyesindenZararKesHesapla(currentIndex, -500.0) != 0;
                }
                else if (stopLossExitModeIndex == 5)
                {
                    // 5: Anlık kar/zarar yüzdesi (pozisyon bazlı)
                    stopLoss = Trader.karAlZararKes.KarZararYuzdesindenZararKesHesapla(currentIndex, -2.0) != 0;
                }
            }
            // ************************************************************************************************************************

            if (flatModeEnabled)
            {
                if (flatModeIndex == 0)
                {
                    // Flat olma durumu burada incelenir ve flat flag'i setlenir
                    flat = flatSignals[currentIndex];
                }
            }
            // ************************************************************************************************************************

            if (skipModeEnabled)
            {
                if (skipModeIndex == 0)
                {
                    // Skip olma durumu burada incelenir ve skip flag'i setlenir
                    skip = false;
                }
            }
            // ************************************************************************************************************************

            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // Sinyal onceliklendirmesi
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
            // ************************************************************************************************************************

            // Saat/tarih penceresi gate'i: pencere disinda buy/sell uretilmez ve flat=true setlenir -
            // oncelik zincirinde flat buy/sell'den once geldigi icin (skip > flat > TP > SL > buy > sell)
            // kosulsuz calisir. isTimeEnabled/isDayEnabled false ise ilgili pencere hep "icinde" sayilir.
            if (isTimeEnabled && !isWithinTimeWindow) { buy = false; sell = false; flat = true; }
            if (isDayEnabled  && !isWithinDayWindow)  { buy = false; sell = false; flat = true; }
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

        // ResolveRunContext() artik BaseStrategy'de (protected) - burada tekrar tanimlanmaz.

        // =========================================================================
        // 1) SERI KATALOGU - TODO: kendi indikator setini burada tanimla
        //    (periyotlar burada SABIT kalir, optimizer bunlari degil ruleModeIndex'i tarar)
        // =========================================================================
        private static Dictionary<string, double[]> BuildSeriesCatalog(IndicatorManager ind)
        {
            var closes = ind.GetClosePrices();

            var rsi  = ind.Momentum.RSI(closes, 14);
            var macd = ind.Momentum.MACD(closes, 12, 26, 9);
            var st   = ind.Trend.SuperTrend(10, 3.0);

            return new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["MA5"]        = ind.MA.SMA(closes, 5),
                ["MA8"]        = ind.MA.SMA(closes, 8),
                ["MA13"]       = ind.MA.SMA(closes, 13),
                ["MA21"]       = ind.MA.SMA(closes, 21),
                ["MA34"]       = ind.MA.SMA(closes, 34),
                ["MA55"]       = ind.MA.SMA(closes, 55),
                ["MA86"]       = ind.MA.SMA(closes, 86),
                ["RSI"]        = rsi.Values,
                ["MACD"]       = macd.MACD,
                ["MACDSignal"] = macd.Signal,
                ["SuperTrend"] = st.SuperTrend,
                ["Close"]      = closes,
            };
        }

        // =========================================================================
        // 2) KURAL SECIMI - TODO: kendi AL/SAT kuralini burada tanimla
        //    ruleModeIndex hangi else-if dalinin calisacagini secer, her dal buySignals/sellSignals'i
        //    (butun barlar icin onceden hesaplanmis bool[]) dolduruyor.
        // =========================================================================
        private void BuildSignals(int ruleModeIndex)
        {
            if (ruleModeIndex == 0)
            {
                // Golden/Death cross (MA5 x MA8)
                var ma5 = series!["MA5"];
                var ma8 = series!["MA8"];

                for (int i = 0; i < barCount; i++)
                {
                    buySignals![i]  = Buyuk(i, ma5, ma8);
                    sellSignals![i] = Kucuk(i, ma5, ma8);
                }
            }
            else if (ruleModeIndex == 1)
            {
                // MA5 > MA8 > MA13 siralamasi + RSI[i-3] > RSI[i-1] (RSI uc bar once, bir bar
                // oncesine gore daha YUKSEKTI - yani RSI o aralikta DUSMUS, "sogumus momentum"
                // pullback filtresi). Bu kosul "rsiUp" adiyla buy tarafina, tersi (RSI[i-3] <
                // RSI[i-1], yani RSI o aralikta YUKSELMIS) "rsiDown" adiyla sell tarafina
                // ekleniyor - degisken adlari RSI'nin kendi yonunu degil, hangi sinyale (buy/sell)
                // eklendiklerini ifade ediyor, kafa karistirmasin diye burada not dusuldu.
                var ma5  = series!["MA5"];
                var ma8  = series!["MA8"];
                var ma13 = series!["MA13"];
                var rsi  = series!["RSI"];

                for (int i = 0; i < barCount; i++)
                {
                    // RSI[-3] / RSI[-1]: ayni dizinin farkli lag'leri - Buyuk/Kucuk ayni index'te
                    // iki FARKLI diziyi kiyasladigi icin burada uygun degil, ham karsilastirma yapiyoruz.
                    bool rsiUp   = i >= 3 && rsi[i - 3] > rsi[i - 1];
                    bool rsiDown = i >= 3 && rsi[i - 3] < rsi[i - 1];

                    buySignals![i]  = Buyuk(i, ma5, ma8) && Buyuk(i, ma8, ma13) && rsiUp;
                    sellSignals![i] = Kucuk(i, ma5, ma8) && Kucuk(i, ma8, ma13) && rsiDown;
                }
            }
            else if (ruleModeIndex == 2)
            {
                // MACD sinyal cizgisini kesiyor + SuperTrend fiyatin altinda/ustunde
                var macd       = series!["MACD"];
                var macdSignal = series!["MACDSignal"];
                var superTrend = series!["SuperTrend"];
                var close      = series!["Close"];

                for (int i = 0; i < barCount; i++)
                {
                    buySignals![i]  = Buyuk(i, macd, macdSignal) && Kucuk(i, superTrend, close);
                    sellSignals![i] = Kucuk(i, macd, macdSignal) && Buyuk(i, superTrend, close);
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(ruleModeIndex), $"ruleModeIndex {ruleModeIndex} gecersiz (0-2 arasinda olmali).");
            }
        }

        public override Dictionary<string, double[]>? GetPlotIndicators() => series;
    }
}
