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
    /// - signalModeIndex : ruleModeIndex'in urettigi koşul NE ZAMAN sinyale donusur? 
    ///                     0: siralama/seviye bazli (koşul true oldugu surece HER barda buy/sell tekrarlanir),
    ///                     1: kesisim bazli (sadece false->true GECIS aninda, bir kere). AKTIF,
    ///                     OnStep'te dallanan tek parametre bu.
    /// - exitModeIndex   : takeProfit/stopLoss yöntemini seçer (Trader.karAlZararKes üzerinden,
    ///                     diğer 21 Simple*Strategy'yle AYNI 0-5 dispatch'i - 2026-08-31'de
    ///                     tamamlandı, önceden sadece 0 uygulanıyordu):
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
    /// exitModeIndex/flatModeIndex/skipModeIndex BILEREK, henuz hicbir mantik onlari okumadigi
    /// halde eklendi - amac, ileride o kategoriye gercek bir kural yazildiginda constructor imzasini
    /// (ve dolayisiyla tum Config_01/03 script'lerini, StrategyConfig.txt kayitlarini) DEGISTIRMEK
    /// ZORUNDA KALMAMAK. Yeri/ismi bastan belli, sadece OnStep icinde ilgili yerde bu parametreye
    /// gore dallanma eklenecek (signalModeIndex'in su an yaptigi gibi).
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
    /// </summary>
    public class SimpleComboStrategy : BaseStrategy
    {
        public override string Name => "Simple Combo Strategy";

        private readonly int ruleModeIndex;
        private readonly int signalModeIndex; // 0: siralama (level) bazli - kural true oldugu surece sinyal, 1: kesisim (crossover) bazli - sadece false->true gecis aninda sinyal
        private readonly int exitModeIndex;   // takeProfit/stopLoss yöntemi (0-5, Trader.karAlZararKes üzerinden) - AKTIF
        private readonly int flatModeIndex;   // PLACEHOLDER - henuz okunmuyor, bkz. sinif basi doc comment
        private readonly int skipModeIndex;   // PLACEHOLDER - henuz okunmuyor, bkz. sinif basi doc comment
        private Dictionary<string, double[]>? series;
        private int barCount;
        private bool[]? buySignals;
        private bool[]? sellSignals;
        private bool[]? flatSignals;

        // Parametreli constructor (yeni kullanim) - field'lar underscore'suz oldugu icin, ayni
        // isimdeki constructor parametresinden ayirt etmek amaciyla burada "this." kullanmak
        // ZORUNLU (this. olmadan "ruleModeIndex = ruleModeIndex;" parametreyi kendine atar,
        // field hep 0 kalir - digger metotlarda boyle bir isim çakışması olmadigi icin "this."
        // gerekmiyor).
        public SimpleComboStrategy(List<StockData> data, IndicatorManager indicators,
            int ruleModeIndex = 0, int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0)
        {
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;
            Parameters["RuleModeIndex"]   = ruleModeIndex;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"]   = exitModeIndex;
            Parameters["FlatModeIndex"]   = flatModeIndex;
            Parameters["SkipModeIndex"]   = skipModeIndex;

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

            // Ilk barlarda yeterli veri yok
            if (currentIndex < 1)
                return TradeSignals.None;

            // OnInit henuz calismamis / kural kurulmamissa sinyal uretme
            if (series == null || buySignals == null || sellSignals == null || flatSignals == null)
                return TradeSignals.None;
            // ************************************************************************************************************************

            // Gecerli ve onceki degerler
            double currentPrice = Data[currentIndex].Close;
            double prevPrice = Data[currentIndex - 1].Close;
            // ************************************************************************************************************************

            if (signalModeIndex == 0)
            {
                // Siralama (level) bazli: sinyal true oldugu her barda tekrarlanir
                if (buySignals[currentIndex])
                    buy = true;

                if (sellSignals[currentIndex])
                    sell = true;
            }
            else
            {
                // Kesisim (crossover) bazli: sadece false->true gecis aninda (bir kere) sinyal
                if (buySignals[currentIndex] && !buySignals[currentIndex - 1])
                    buy = true;

                if (sellSignals[currentIndex] && !sellSignals[currentIndex - 1])
                    sell = true;
            }
            // ************************************************************************************************************************

            // ORNEK: Trader referansini kullanarak kar al / zarar kes hesaplama
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
                flat = flatSignals[currentIndex];
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
            // Sinyal onceliklendirmesi
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
