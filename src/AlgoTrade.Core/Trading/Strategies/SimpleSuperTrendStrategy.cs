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
    /// SuperTrend İndikatörü Stratejisi
    ///
    /// SuperTrend Mantığı:
    /// - MOST/OTT ailesinden, ama bant percent yerine ATR*multiplier ile kuruluyor
    /// - ATR hesabı High/Low/Close'a bağımlı olduğu için (True Range), MOST/OTT'nin aksine
    ///   indikatörün kendisine priceSource verilemez - priceSource sadece OnStep'teki
    ///   kırılım/state karşılaştırmasının "fiyat" tarafını besler (indikatörü etkilemez).
    /// - Yükseliş trendinde: SuperTrend fiyatın altında destek görevi görür
    /// - Düşüş trendinde: SuperTrend fiyatın üstünde direnç görevi görür
    ///
    /// Parametreler:
    /// - period: ATR periyodu (varsayılan 10)
    /// - multiplier: ATR çarpanı (varsayılan 3.0)
    /// - priceSource: OnStep sinyal serisi (varsayılan Close - klasik SuperTrend)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: Fiyat-SuperTrend kırılımı  (fiyat SuperTrend'i yukarı/aşağı kesince)
    ///     1: Direction flip             (indikatörün kendi ürettiği Direction dizisi -1'den 1'e/1'den -1'e dönünce - eski choice=0 ile birebir aynı)
    ///     2: SuperTrend slope flip      (SuperTrend'in kendi yönü dönünce)
    ///     3: SuperTrend state           (fiyatın SuperTrend'e göre konumu - kesişim değil, koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi    (fiyat SuperTrend'ten %bandThreshold'dan fazla uzaklaşınca)
    ///     5: Breakout + retest          (SuperTrend kırılıp fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars          (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: Fiyat eğimi + SuperTrend state (rejim: fiyat-SuperTrend konumu + momentum: fiyatın N-bar eğimi)
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
    public class SimpleSuperTrendStrategy : BaseStrategy
    {
        public override string Name => "Simple SuperTrend Strategy";

        // barCount/openPrices/.../epochTimes artik BaseStrategy'de (protected) - LoadCommonSeries()
        // tarafindan Initialize() icinde OnInit()'ten once doldurulur, burada tekrar tanimlanmaz.

        private readonly int period;
        private readonly double multiplier;

        // signalModeIndex/exitModeIndex/flatModeIndex/skipModeIndex/ruleModeIndex artik BaseStrategy'de
        // tanimli (protected, readonly degil) - degerleri asagida constructor'da parametre olarak atanir.
        // signalModeIndex'in dispatch mantigi (OnStep'teki if/else zinciri) stratejiye ozgu, burada kalir.

        // OnStep'teki "fiyat" tarafını besler - indikatörün kendisi (ATR) High/Low/Close'a bağımlı,
        // priceSource'tan etkilenmez.
        private readonly PriceSource priceSource = PriceSource.Close;

        // startTime/stopTime/startDay/stopDay/isTimeEnabled/isDayEnabled/triggerTime/isTriggerTimeEnabled
        // artik BaseStrategy'de tanimli (protected, readonly degil) - degerleri asagida constructor'da atanir.

        private double[]? source;   // priceSource'un çözülmüş hali - OnInit'te bir kez, OnStep bundan okur
        private double[]? superTrend;
        private int[]?     direction;

        // runContextResolved/timeframeMinutes/isOptimizationRun/isOneMinute.../ResolveRunContext() artik
        // BaseStrategy'de (protected) - burada tekrar tanimlanmaz.

        // Parametreli constructor (data/indicators gerekli — parametresiz ctor kaldırıldı, hiç kullanılmıyordu)
        public SimpleSuperTrendStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 10, double multiplier = 3.0, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period          = period;
            this.multiplier      = multiplier;
            this.priceSource     = priceSource;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;
            this.ruleModeIndex   = ruleModeIndex;

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

            Parameters["Period"]               = period;
            Parameters["Multiplier"]           = multiplier;
            Parameters["PriceSource"]          = priceSource;
            Parameters["SignalModeIndex"]      = signalModeIndex;
            Parameters["ExitModeIndex"]        = exitModeIndex;
            Parameters["FlatModeIndex"]        = flatModeIndex;
            Parameters["SkipModeIndex"]        = skipModeIndex;
            Parameters["RuleModeIndex"]        = ruleModeIndex;
            Parameters["StartTime"]            = startTime;
            Parameters["StopTime"]             = stopTime;
            Parameters["StartDay"]             = startDay;
            Parameters["StopDay"]              = stopDay;
            Parameters["IsTimeEnabled"]        = isTimeEnabled;
            Parameters["IsDayEnabled"]         = isDayEnabled;
            Parameters["TriggerTime"]          = triggerTime;
            Parameters["IsTriggerTimeEnabled"] = isTriggerTimeEnabled;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            // barCount/openPrices/.../epochTimes BaseStrategy.LoadCommonSeries() tarafindan bu
            // noktada zaten dolu (Initialize() icinde OnInit()'ten once cagrildi).
            source = Indicators.Trend.ResolvePriceSource(priceSource);

            // SuperTrend indicator'ı hesapla (ATR tabanlı, High/Low/Close kullanır - priceSource'tan bağımsız)
            var superTrendResult = Indicators.Trend.SuperTrend(period, multiplier);
            superTrend = superTrendResult.SuperTrend;
            direction  = superTrendResult.Direction;

            // superTrend/direction/source OnStep'te barCount ile ayni index'te okunuyor - uzunluklari
            // uyusmazsa (or. indikator filtrelenmis/kirpilmis dondurdu) IndexOutOfRange yerine
            // burada net hata ver (ortak diziler icin ayni kontrol LoadCommonSeries()'te yapildi).
            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= superTrend.Length == barCount;
            allSeriesLengthsMatch &= direction.Length  == barCount;
            allSeriesLengthsMatch &= source.Length     == barCount;

            if (!allSeriesLengthsMatch)
            {
                throw new InvalidOperationException(
                    $"Seri uzunlukları uyuşmuyor (barCount={barCount}): " +
                    $"superTrend={superTrend.Length}, direction={direction.Length}, source={source.Length}");
            }

            //Log($"SimpleSuperTrendStrategy initialized: Period={period}, Multiplier={multiplier}, SignalModeIndex={signalModeIndex}");
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

            // İlk barlarda yeterli veri yok
            if (currentIndex < period)
                return TradeSignals.None;

            if (superTrend == null || superTrend.Length == 0)
                return TradeSignals.None;

            if (direction == null || direction.Length == 0)
                return TradeSignals.None;

            if (source == null || source.Length == 0)
                return TradeSignals.None;
            // ************************************************************************************************************************

            // Geçerli ve önceki değerler (source = OnInit'te priceSource'tan çözülen seri)
            double currentPrice       = source[currentIndex];
            double prevPrice          = source[currentIndex - 1];
            double currentSuperTrend  = superTrend[currentIndex];
            double prevSuperTrend     = superTrend[currentIndex - 1];
            int    currentDirection   = direction[currentIndex];
            int    prevDirection      = direction[currentIndex - 1];
            // ************************************************************************************************************************

            // isOneMinute/isFiveMinute/isOneHour/isFourHour/isOneDay artık field - ResolveRunContext'te
            // bir kez set edilir, burada tekrar hesaplanmaz (run boyunca degismezler).
                 if (isOneMinute)   { }
            else if (isFiveMinute)  { }
            else if (isOneHour)     { }
            else if (isFourHour)    { }
            else if (isOneDay)      { }
            // ************************************************************************************************************************

            // isFirstOfDay/isLastOfDay/isFirstOfWeek/isFirstOfMonth/isSonYonA/S/F - artik BaseStrategy field'i,
            // ResolveRunContext(currentIndex) tarafindan her bar tazelenir. Henüz sinyal mantığına dahil
            // değil, kullanılmasa da hazır tutuluyor.
            if (isFirstOfDay)           { }
            if (isLastOfDay)            { }
            if (isFirstOfWeek)          { }
            if (isFirstOfMonth)         { }

                 if (isSonYonA)         { }
            else if (isSonYonS)         { }
            else if (isSonYonF)         { }
            // ************************************************************************************************************************

            // signalModeIndex ile buy/sell yöntemi seçilir - detay için sınıf başı doc comment (0-7)
            if (signalModeIndex == 0)
            {
                // 0: Fiyat-SuperTrend kırılımı - fiyat SuperTrend'i yukarı kesince AL, aşağı kesince SAT
                if (YukarıKesti(currentIndex, source, superTrend)) buy  = true;
                if (AsagiKesti(currentIndex, source, superTrend))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: Direction flip - indikatörün kendi Direction dizisi -1'den 1'e (AL) / 1'den -1'e (SAT) dönüyor
                if (prevDirection == -1 && currentDirection == 1) buy  = true;
                if (prevDirection == 1  && currentDirection == -1) sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: SuperTrend slope flip - SuperTrend'in kendi yönü dönüyor (düşen/düz → yükselen = AL)
                if (currentIndex >= 2)
                {
                    double slopeNow  = superTrend[currentIndex]     - superTrend[currentIndex - 1];
                    double slopePrev = superTrend[currentIndex - 1] - superTrend[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: SuperTrend state - fiyatın SuperTrend'e göre konumu (kesişim değil, koşul sürdükçe her bar)
                if (Buyuk(currentIndex, source, superTrend)) buy  = true;
                if (Kucuk(currentIndex, source, superTrend)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi - fiyat SuperTrend'ten %bandThreshold'dan fazla uzaklaşınca (trend-following)
                const double bandThreshold = 0.01; // %1
                if (currentSuperTrend != 0.0)
                {
                    double distanceRatio = (currentPrice - currentSuperTrend) / currentSuperTrend;
                    if (distanceRatio >  bandThreshold) buy  = true;
                    if (distanceRatio < -bandThreshold) sell = true;
                }
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest - son retestLookback bar içinde SuperTrend kırıldı, şimdi fiyat
                //    SuperTrend'e geri dokunup (retest) kırılım yönünde kapattıysa → sinyal
                const int retestLookback = 10;
                double barLow  = Data[currentIndex].Low;
                double barHigh = Data[currentIndex].High;

                for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                {
                    if (k < 1) continue;

                    if (!buy && YukarıKesti(k, source, superTrend)
                        && barLow <= currentSuperTrend           // bu bar SuperTrend'e geri dokundu (retest)
                        && currentPrice > currentSuperTrend)     // ama üstünde kapattı (retest tuttu)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(k, source, superTrend)
                        && barHigh >= currentSuperTrend
                        && currentPrice < currentSuperTrend)
                    {
                        sell = true;
                    }
                }
            }
            else if (signalModeIndex == 6)
            {
                // 6: Confirmation bars - kırılım confirmBars bar önce oldu ve o zamandan beri fiyat
                //    hep SuperTrend'in aynı tarafında kaldıysa gir
                const int confirmBars = 3;
                if (currentIndex >= confirmBars + 1)
                {
                    int crossBar = currentIndex - confirmBars;

                    bool stayedAbove = YukarıKesti(crossBar, source, superTrend);
                    bool stayedBelow = AsagiKesti(crossBar, source, superTrend);
                    for (int k = crossBar + 1; k <= currentIndex; k++)
                    {
                        stayedAbove &= source[k] > superTrend[k];
                        stayedBelow &= source[k] < superTrend[k];
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: Fiyat eğimi + SuperTrend state - rejim (fiyat-SuperTrend konumu) + momentum (fiyatın N-bar eğimi)
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool priceRising  = source[currentIndex] > source[currentIndex - slopeLookback];
                    bool priceFalling = source[currentIndex] < source[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, source, superTrend) && priceRising)  buy  = true;
                    if (Kucuk(currentIndex, source, superTrend) && priceFalling) sell = true;
                }
            }
            // ************************************************************************************************************************

            if (1 == 1 && Trader != null)
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

            if (1 == 1 && Trader != null)
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

            // Saat/tarih penceresi gate'i: pencere disinda buy/sell uretilmez ve flat=true setlenir -
            // oncelik zincirinde flat buy/sell'den once geldigi icin (skip > flat > TP > SL > buy > sell)
            // kosulsuz calisir. isTimeEnabled/isDayEnabled false ise ilgili pencere hep "icinde" sayilir.
            if (isTimeEnabled && !isWithinTimeWindow) { buy = false; sell = false; flat = true; }
            if (isDayEnabled  && !isWithinDayWindow)  { buy = false; sell = false; flat = true; }

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

        public override bool IsValidParameterCombination()
        {
            bool isValid = true;

            return isValid;
        }

        /// <summary>
        /// SuperTrend değerlerini al (plotting veya analiz için)
        /// </summary>
        public double[]? GetSuperTrend() => superTrend;

        /// <summary>
        /// SuperTrend direction değerlerini al (plotting veya analiz için)
        /// </summary>
        public int[]? GetDirection() => direction;

        /// <summary>
        /// Period parametresini al
        /// </summary>
        public int Period => period;

        /// <summary>
        /// Multiplier parametresini al
        /// </summary>
        public double Multiplier => multiplier;

        // IsFirstBarOfDay/IsLastBarOfDay/IsFirstBarOfWeek/IsFirstBarOfMonth artik BaseStrategy'de
        // (protected) - burada tekrar tanimlanmaz.

        /// <summary>
        /// Get indicators for plotting (IStrategy implementation)
        /// </summary>
        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (superTrend != null && superTrend.Length > 0)
                indicators["SuperTrend"] = superTrend;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
