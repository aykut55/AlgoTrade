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
    /// Strategy001_BarAcilisi - SimpleComboStrategyRule001 desenindeki (bkz. o dosyanin doc
    /// comment'i) TURETILMIS, SABIT/immutable bir deneme. Bar'in Acilis (Open) fiyatina dayali
    /// AL/SAT kurali: gunun ilk barinda (isFirstOfDay) o barin Open fiyati "gunun acilis fiyati"
    /// olarak yakalanir (dayOpenPrice), gun boyunca sabit kalir.
    /// - buySignalModeIndex/sellSignalModeIndex: buy ve sell yöntemini AYRI AYRI seçer (asymmetric -
    ///   buy başka bir moddan, sell başka bir moddan gelebilir). Her ikisi de aynı mod kümesinden seçilir:
    ///   - 0: Siralama (level) bazli - Close, gunun acilisinin ustunde/altinda oldugu HER barda
    ///     AL/SAT tekrarlanir (Buyuk/Kucuk).
    ///   - 1: Kesisim (crossover) bazli - Close, gunun acilisini sadece YUKARI/ASAGI kestigi anda
    ///     (bir kere) sinyal uretir (YukarıKesti/AsagiKesti).
    ///
    /// buySignalModeIndex/sellSignalModeIndex/takeProfitExitModeIndex/stopLossExitModeIndex/
    /// flatModeIndex/skipModeIndex: SimpleMostStrategy/SimpleComboStrategy ile AYNI standart
    /// (2026-08-27 karari, 2026-09-04'te buy/sell ve TP/SL ayri index'lere bolundu). 0/1 disindaki
    /// degerler icin buy/sell hep false kalir, diger dispatch'ler (exit/flat/skip) standart sekilde
    /// aktif (buyModeEnabled/sellModeEnabled/takeProfitExitModeEnabled/stopLossExitModeEnabled/
    /// flatModeEnabled/skipModeEnabled BaseStrategy'deki kod-seviyesi toggle'lar - bu strateji
    /// exit/flat/skip'i constructor'da kapatiyor, bkz. asagisi).
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
    public class Strategy001_BarAcilisi : BaseStrategy
    {
        public override string Name => "Strategy001 (Bar Acilisi)";

        // buySignalModeIndex/sellSignalModeIndex/takeProfitExitModeIndex/stopLossExitModeIndex/
        // flatModeIndex/skipModeIndex artik BaseStrategy'de tanimli (protected, readonly degil) -
        // degerleri asagida constructor'da parametre olarak atanir.

        // runContextResolved/timeframeMinutes/isOptimizationRun/ResolveRunContext() artik
        // BaseStrategy'de (protected) - burada tekrar tanimlanmaz.
        // openPrices/highPrices/lowPrices/closePrices de BaseStrategy'de (protected) -
        // LoadCommonSeries() tarafindan Initialize() icinde OnInit()'ten once doldurulur.

        // Gunun acilis fiyati - isFirstOfDay barinda OnStep icinde guncellenir, gun boyunca ayni
        // kalir (o barin Open'ina gore AL/SAT kiyaslamasi yapilir). OnInit'te barCount>0 ise ilk
        // barin Open'iyla baslangic degeri verilir - currentIndex<1 guard'i yuzunden index 0
        // hicbir zaman OnStep icinde isFirstOfDay dalina girmedigi icin, o barin Open'ini erken
        // yakalamazsak ilk gunun barlarinda dayOpenPrice 0 kalirdi.
        private double dayOpenPrice;

        // ADX filtresi (buySignalModeIndex/sellSignalModeIndex==1 icin) - trend gucu adxThreshold'un altindaysa
        // kesisim sinyali uretilmez. OnInit'te Indicators.Trend.ADX(adxPeriod) ile doldurulur.
        private readonly int adxPeriod;
        private readonly double adxThreshold;
        private double[]? adx;

        public Strategy001_BarAcilisi(List<StockData> data, IndicatorManager indicators,
            int buySignalModeIndex = 0, int sellSignalModeIndex = 0, int takeProfitExitModeIndex = 0, int stopLossExitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0)
        {
            this.buySignalModeIndex      = buySignalModeIndex;
            this.sellSignalModeIndex     = sellSignalModeIndex;
            this.takeProfitExitModeIndex = takeProfitExitModeIndex;
            this.stopLossExitModeIndex   = stopLossExitModeIndex;
            this.flatModeIndex           = flatModeIndex;
            this.skipModeIndex           = skipModeIndex;

            // ADX filtresi (buySignalModeIndex/sellSignalModeIndex==1) - sabit, kod icinde atanir (startTime/stopTime ile ayni desen).
            adxPeriod    = 14;
            adxThreshold = 25;

            // BaseStrategy'deki varsayilan degerler true oldugu icin , bu stratejide exit/flat/skip modlari kapali (false) - sadece buy/sell modlari acik
            takeProfitExitModeEnabled = false;
            stopLossExitModeEnabled   = false;
            flatModeEnabled           = false;
            skipModeEnabled           = false;

            // Gun ici saat penceresi / tarih penceresi / triggerTime - alanlar BaseStrategy'de tanimli,
            // degerleri burada (sabit, kod icinde) atanir.
            startTime            = TimeOnly.ParseExact("10:05:00", "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            stopTime             = TimeOnly.ParseExact("16:45:00", "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            triggerTime          = TimeOnly.ParseExact("10:00:00", "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            startDay             = default;
            stopDay              = default;
            isTimeEnabled        = false;
            isDayEnabled         = false;
            isTriggerTimeEnabled = false;

            Parameters["BuySignalModeIndex"]      = buySignalModeIndex;
            Parameters["SellSignalModeIndex"]     = sellSignalModeIndex;
            Parameters["TakeProfitExitModeIndex"] = takeProfitExitModeIndex;
            Parameters["StopLossExitModeIndex"]   = stopLossExitModeIndex;
            Parameters["FlatModeIndex"]           = flatModeIndex;
            Parameters["SkipModeIndex"]           = skipModeIndex;
            Parameters["AdxPeriod"]               = adxPeriod;
            Parameters["AdxThreshold"]            = adxThreshold;
            Parameters["BuyModeEnabled"]          = buyModeEnabled;
            Parameters["SellModeEnabled"]         = sellModeEnabled;
            Parameters["TakeProfitExitModeEnabled"] = takeProfitExitModeEnabled;
            Parameters["StopLossExitModeEnabled"]   = stopLossExitModeEnabled;
            Parameters["FlatModeEnabled"]          = flatModeEnabled;
            Parameters["SkipModeEnabled"]          = skipModeEnabled;

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            // openPrices/closePrices BaseStrategy.LoadCommonSeries() tarafindan zaten dolduruldu.
            // dayOpenPrice baslangic degeri: ilk barin (index 0) Open'i - bkz. field yorumu.
            dayOpenPrice = openPrices != null && openPrices.Length > 0 ? openPrices[0] : 0.0;

            adx = Indicators.Trend.ADX(adxPeriod);
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

            // OnInit henuz calismamissa sinyal uretme
            if (openPrices == null || closePrices == null)
                return TradeSignals.None;
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
                    // 0: Siralama (level) - Close, gunun acilisinin ustunde oldugu HER barda AL tekrarlanir
                    if (isFirstOfDay)
                        dayOpenPrice = openPrices[currentIndex];

                    if (Buyuk(currentIndex, closePrices, dayOpenPrice)) buy = true;

                    /*buy = false;

                    bool strongTrend = adx != null && currentIndex < adx.Length && !double.IsNaN(adx[currentIndex]) && adx[currentIndex] > adxThreshold;

                    if (strongTrend && Buyuk(currentIndex, closePrices, dayOpenPrice)) buy = true;*/
                }
                else if (buySignalModeIndex == 1)
                {
                    // 1: Kesisim (crossover) - Close, gunun acilisini sadece YUKARI kestigi anda (bir kere) sinyal uretir
                    // ADX filtresi: trend gucu (adx[currentIndex]) adxThreshold'un altindaysa kesisim gormezden gelinir -
                    // amac zayif/yatay piyasada gurultu kaynakli kesisimleri elemek.
                    if (isFirstOfDay)
                        dayOpenPrice = openPrices[currentIndex];

                    /*bool strongTrend = adx != null && currentIndex < adx.Length && !double.IsNaN(adx[currentIndex]) && adx[currentIndex] > adxThreshold;

                    if (strongTrend && YukarıKesti(currentIndex, closePrices, dayOpenPrice)) buy = true;*/
                }
                else if (buySignalModeIndex == 2)
                {
                    // Gunun acilis fiyati (ayni yakalama, diger modlarla ortak)
                    if (isFirstOfDay)
                        dayOpenPrice = openPrices[currentIndex];

                    // Sadece gunun 3. barinda kesisime bakilir (BaseStrategy.IsNthBarOfDay). Acilistan
                    // itibaren hic geri donmeden (pullback yapmadan) dumduz giden gunlerde 3. barda
                    // onceki bar da zaten ayni tarafta oldugu icin YukarıKesti hicbir zaman true olmaz -
                    // yani boyle "dumduz" gunlerde bilincli olarak pozisyon acilmaz.
                    if (IsNthBarOfDay(currentIndex, 3))
                    {
                        if (YukarıKesti(currentIndex, closePrices, dayOpenPrice)) buy = true;
                    }
                }
                else if (buySignalModeIndex == 3)
                {
                    if (isTriggerTime)
                        dayOpenPrice = openPrices[currentIndex];

                    if (YukarıKesti(currentIndex, closePrices, dayOpenPrice)) buy = true;
                }
                else if (buySignalModeIndex == 4)
                {

                }
                else if (buySignalModeIndex == 5)
                {
                    // 5: High/Low ile - Close yerine bar'in High'i acilisi kesince sinyal (intrabar, Close'u beklemez)
                    if (isFirstOfDay)
                        dayOpenPrice = openPrices[currentIndex];

                    double barHigh = Data[currentIndex].High;

                    if (barHigh > dayOpenPrice) buy = true;
                }
                else if (buySignalModeIndex == 6)
                {
                    // 6: Gap bazli - gunun acilisi, bir onceki barin (dunun son bari) kapanisina gore yukari
                    //    gap yaptiysa sinyal - sadece gunun ilk barinda anlamli, diger barlarda buy false kalir
                    if (isFirstOfDay)
                    {
                        dayOpenPrice = openPrices[currentIndex];
                        double previousClose = closePrices[currentIndex - 1];

                        if (dayOpenPrice > previousClose) buy = true;
                    }
                }
                else if (buySignalModeIndex == 7)
                {
                    // 7: Zaman penceresiyle kombinasyon - mod 0'in (level) kurali sadece isTimeEnabled ile
                    //    acilan saat penceresi icindeyken calisir (isTimeEnabled false ise pencere hep "icinde" sayilir)
                    if (isFirstOfDay)
                        dayOpenPrice = openPrices[currentIndex];

                    if (isWithinTimeWindow)
                    {
                        if (Buyuk(currentIndex, closePrices, dayOpenPrice)) buy = true;
                    }
                }
            }

            if (sellModeEnabled)
            {
                if (sellSignalModeIndex == 0)
                {
                    // 0: Siralama (level) - Close, gunun acilisinin altinda oldugu HER barda SAT tekrarlanir
                    if (isFirstOfDay)
                        dayOpenPrice = openPrices[currentIndex];

                    if (Kucuk(currentIndex, closePrices, dayOpenPrice)) sell = true;

                    /*sell = false;

                    bool strongTrend = adx != null && currentIndex < adx.Length && !double.IsNaN(adx[currentIndex]) && adx[currentIndex] > adxThreshold;

                    if (strongTrend && Kucuk(currentIndex, closePrices, dayOpenPrice)) sell = true;*/
                }
                else if (sellSignalModeIndex == 1)
                {
                    // 1: Kesisim (crossover) - Close, gunun acilisini sadece ASAGI kestigi anda (bir kere) sinyal uretir
                    // ADX filtresi: trend gucu (adx[currentIndex]) adxThreshold'un altindaysa kesisim gormezden gelinir -
                    // amac zayif/yatay piyasada gurultu kaynakli kesisimleri elemek.
                    if (isFirstOfDay)
                        dayOpenPrice = openPrices[currentIndex];

                    /*bool strongTrend = adx != null && currentIndex < adx.Length && !double.IsNaN(adx[currentIndex]) && adx[currentIndex] > adxThreshold;

                    if (strongTrend && AsagiKesti(currentIndex, closePrices, dayOpenPrice)) sell = true;*/
                }
                else if (sellSignalModeIndex == 2)
                {
                    // Gunun acilis fiyati (ayni yakalama, diger modlarla ortak)
                    if (isFirstOfDay)
                        dayOpenPrice = openPrices[currentIndex];

                    // Sadece gunun 3. barinda kesisime bakilir (BaseStrategy.IsNthBarOfDay). Acilistan
                    // itibaren hic geri donmeden (pullback yapmadan) dumduz giden gunlerde 3. barda
                    // onceki bar da zaten ayni tarafta oldugu icin AsagiKesti hicbir zaman true olmaz -
                    // yani boyle "dumduz" gunlerde bilincli olarak pozisyon acilmaz.
                    if (IsNthBarOfDay(currentIndex, 3))
                    {
                        if (AsagiKesti(currentIndex, closePrices, dayOpenPrice)) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 3)
                {
                    if (isTriggerTime)
                        dayOpenPrice = openPrices[currentIndex];

                    if (AsagiKesti(currentIndex, closePrices, dayOpenPrice)) sell = true;
                }
                else if (sellSignalModeIndex == 4)
                {

                }
                else if (sellSignalModeIndex == 5)
                {
                    // 5: High/Low ile - Close yerine bar'in Low'u acilisi kesince sinyal (intrabar, Close'u beklemez)
                    if (isFirstOfDay)
                        dayOpenPrice = openPrices[currentIndex];

                    double barLow = Data[currentIndex].Low;

                    if (barLow < dayOpenPrice) sell = true;
                }
                else if (sellSignalModeIndex == 6)
                {
                    // 6: Gap bazli - gunun acilisi, bir onceki barin (dunun son bari) kapanisina gore asagi
                    //    gap yaptiysa sinyal - sadece gunun ilk barinda anlamli, diger barlarda sell false kalir
                    if (isFirstOfDay)
                    {
                        dayOpenPrice = openPrices[currentIndex];
                        double previousClose = closePrices[currentIndex - 1];

                        if (dayOpenPrice < previousClose) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 7)
                {
                    // 7: Zaman penceresiyle kombinasyon - mod 0'in (level) kurali sadece isTimeEnabled ile
                    //    acilan saat penceresi icindeyken calisir (isTimeEnabled false ise pencere hep "icinde" sayilir)
                    if (isFirstOfDay)
                        dayOpenPrice = openPrices[currentIndex];

                    if (isWithinTimeWindow)
                    {
                        if (Kucuk(currentIndex, closePrices, dayOpenPrice)) sell = true;
                    }
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
                    flat = false;
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

            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // ************************************************************************************************************************
            // Sinyal önceliklendirmesi
            // Not: 6 sinyal de yukaridaki gate blogunda Trader.signals.*Enabled ile filtrelendi -
            // flag'i kapali olan sinyal buraya hep false gelir, zincir bir sonraki gecerli sinyale duser.
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

        // ResolveRunContext() artik BaseStrategy'de (protected) - burada tekrar tanimlanmaz.

        public override Dictionary<string, double[]>? GetPlotIndicators() => null;
    }
}
