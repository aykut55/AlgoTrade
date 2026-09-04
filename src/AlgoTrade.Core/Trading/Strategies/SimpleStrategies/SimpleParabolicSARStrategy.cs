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
    /// - buySignalModeIndex/sellSignalModeIndex: buy ve sell yöntemini AYRI AYRI seçer (asymmetric -
    ///   buy başka bir moddan, sell başka bir moddan gelebilir). Her ikisi de aynı mod kümesinden seçilir:
    ///     0: Fiyat-SAR kırılımı        (fiyat SAR'ı yukarı/aşağı kesince)
    ///     1: Trend flip                (indikatörün kendi ürettiği Trend dizisi false'dan true'ya/true'dan false'a dönünce - eski choice=0 ile birebir aynı)
    ///     2: SAR slope flip            (SAR'ın kendi yönü dönünce)
    ///     3: SAR state                 (fiyatın SAR'a göre konumu - kesişim değil, koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi   (fiyat SAR'dan %bandThreshold'dan fazla uzaklaşınca)
    ///     5: Breakout + retest         (SAR kırılıp fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars         (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: Fiyat eğimi + SAR state   (rejim: fiyat-SAR konumu + momentum: fiyatın N-bar eğimi)
    /// - takeProfitExitModeIndex/stopLossExitModeIndex: takeProfit/stopLoss yöntemini AYRI AYRI seçer
    ///   (Trader.karAlZararKes üzerinden), her ikisi de aynı mod kümesinden:
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
    public class SimpleParabolicSARStrategy : BaseStrategy
    {
        public override string Name => "Simple Parabolic SAR Strategy";

        // barCount/openPrices/.../epochTimes artik BaseStrategy'de (protected) - LoadCommonSeries()
        // tarafindan Initialize() icinde OnInit()'ten once doldurulur, burada tekrar tanimlanmaz.

        private readonly double step;
        private readonly double max;

        // buySignalModeIndex/sellSignalModeIndex/takeProfitExitModeIndex/stopLossExitModeIndex/flatModeIndex/
        // skipModeIndex/ruleModeIndex artik BaseStrategy'de tanimli (protected, readonly degil) - degerleri
        // asagida constructor'da parametre olarak atanir. Dispatch mantigi (OnStep'teki if/else zincirleri)
        // stratejiye ozgu, burada kalir.

        // OnStep'teki "fiyat" tarafını besler - indikatörün kendisi (SAR) High/Low'a bağımlı,
        // priceSource'tan etkilenmez.
        private readonly PriceSource priceSource = PriceSource.Close;

        // startTime/stopTime/startDay/stopDay/isTimeEnabled/isDayEnabled/triggerTime/isTriggerTimeEnabled
        // artik BaseStrategy'de tanimli (protected, readonly degil) - degerleri asagida constructor'da atanir.

        private double[]? source;   // priceSource'un çözülmüş hali - OnInit'te bir kez, OnStep bundan okur
        private double[]? sar;
        private bool[]?    trend;

        // runContextResolved/timeframeMinutes/isOptimizationRun/isOneMinute.../ResolveRunContext() artik
        // BaseStrategy'de (protected) - burada tekrar tanimlanmaz.

        // Parametreli constructor (data/indicators gerekli — parametresiz ctor kaldırıldı, hiç kullanılmıyordu)
        public SimpleParabolicSARStrategy(List<StockData> data, IndicatorManager indicators,
            double step = 0.02, double max = 0.2, PriceSource priceSource = PriceSource.Close,
            int buySignalModeIndex = 0, int sellSignalModeIndex = 0, int takeProfitExitModeIndex = 0, int stopLossExitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.step                    = step;
            this.max                     = max;
            this.priceSource             = priceSource;
            this.buySignalModeIndex      = buySignalModeIndex;
            this.sellSignalModeIndex     = sellSignalModeIndex;
            this.takeProfitExitModeIndex = takeProfitExitModeIndex;
            this.stopLossExitModeIndex   = stopLossExitModeIndex;
            this.flatModeIndex           = flatModeIndex;
            this.skipModeIndex           = skipModeIndex;
            this.ruleModeIndex           = ruleModeIndex;

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

            // BaseStrategy'deki varsayilan degerler zaten true - takeProfit/stopLoss guard'i eskiden
            // kosulsuz (1==1) acikti, burada ayni davranisi ACIKCA true atayarak koruyoruz.
            takeProfitExitModeEnabled = true;
            stopLossExitModeEnabled   = true;

            Parameters["Step"]                    = step;
            Parameters["Max"]                     = max;
            Parameters["PriceSource"]             = priceSource;
            Parameters["BuySignalModeIndex"]      = buySignalModeIndex;
            Parameters["SellSignalModeIndex"]     = sellSignalModeIndex;
            Parameters["TakeProfitExitModeIndex"] = takeProfitExitModeIndex;
            Parameters["StopLossExitModeIndex"]   = stopLossExitModeIndex;
            Parameters["FlatModeIndex"]           = flatModeIndex;
            Parameters["SkipModeIndex"]           = skipModeIndex;
            Parameters["RuleModeIndex"]           = ruleModeIndex;
            Parameters["StartTime"]               = startTime;
            Parameters["StopTime"]                = stopTime;
            Parameters["StartDay"]                = startDay;
            Parameters["StopDay"]                 = stopDay;
            Parameters["IsTimeEnabled"]           = isTimeEnabled;
            Parameters["IsDayEnabled"]            = isDayEnabled;
            Parameters["TriggerTime"]             = triggerTime;
            Parameters["IsTriggerTimeEnabled"]    = isTriggerTimeEnabled;
            Parameters["BuyModeEnabled"]          = buyModeEnabled;
            Parameters["SellModeEnabled"]         = sellModeEnabled;
            Parameters["TakeProfitExitModeEnabled"]   = takeProfitExitModeEnabled;
            Parameters["StopLossExitModeEnabled"]     = stopLossExitModeEnabled;
            Parameters["FlatModeEnabled"]         = flatModeEnabled;
            Parameters["SkipModeEnabled"]         = skipModeEnabled;

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

            // Parabolic SAR indicator'ı hesapla (step/max ile - High/Low kullanır, priceSource'tan bağımsız)
            var sarResult = Indicators.Trend.ParabolicSAR(step, max);
            sar   = sarResult.SAR;
            trend = sarResult.Trend;

            // sar/trend/source OnStep'te barCount ile ayni index'te okunuyor - uzunluklari
            // uyusmazsa (or. indikator filtrelenmis/kirpilmis dondurdu) IndexOutOfRange yerine
            // burada net hata ver (ortak diziler icin ayni kontrol LoadCommonSeries()'te yapildi).
            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= sar.Length    == barCount;
            allSeriesLengthsMatch &= trend.Length  == barCount;
            allSeriesLengthsMatch &= source.Length == barCount;

            if (!allSeriesLengthsMatch)
            {
                throw new InvalidOperationException(
                    $"Seri uzunlukları uyuşmuyor (barCount={barCount}): sar={sar.Length}, trend={trend.Length}, source={source.Length}");
            }

            //Log($"SimpleParabolicSARStrategy initialized: Step={step}, Max={max}, BuySignalModeIndex={buySignalModeIndex}, SellSignalModeIndex={sellSignalModeIndex}");
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

            // buySignalModeIndex/sellSignalModeIndex ile buy/sell yöntemi AYRI seçilir (asymmetric) -
            // detay için sınıf başı doc comment. İki bağımsız zincir: biri sadece buy, diğeri sadece sell hesaplar.
            if (buyModeEnabled)
            {
                if (buySignalModeIndex == 0)
                {
                    // 0: Fiyat-SAR kırılımı - fiyat SAR'ı yukarı kesince AL
                    if (YukarıKesti(currentIndex, source, sar)) buy = true;
                }
                else if (buySignalModeIndex == 1)
                {
                    // 1: Trend flip - indikatörün kendi Trend dizisi false'dan true'ya dönüyor (AL)
                    if (!prevTrend && currentTrend) buy = true;
                }
                else if (buySignalModeIndex == 2)
                {
                    // 2: SAR slope flip - SAR'ın kendi yönü dönüyor (düşen/düz → yükselen = AL)
                    if (currentIndex >= 2)
                    {
                        double slopeNow  = sar[currentIndex]     - sar[currentIndex - 1];
                        double slopePrev = sar[currentIndex - 1] - sar[currentIndex - 2];
                        if (slopePrev <= 0.0 && slopeNow > 0.0) buy = true;
                    }
                }
                else if (buySignalModeIndex == 3)
                {
                    // 3: SAR state - fiyatın SAR'a göre konumu (kesişim değil, koşul sürdükçe her bar)
                    if (Buyuk(currentIndex, source, sar)) buy = true;
                }
                else if (buySignalModeIndex == 4)
                {
                    // 4: Band / uzaklık filtresi - fiyat SAR'dan %bandThreshold'dan fazla uzaklaşınca (trend-following)
                    const double bandThreshold = 0.01; // %1
                    if (currentSar != 0.0)
                    {
                        double distanceRatio = (currentPrice - currentSar) / currentSar;
                        if (distanceRatio > bandThreshold) buy = true;
                    }
                }
                else if (buySignalModeIndex == 5)
                {
                    // 5: Breakout + retest - son retestLookback bar içinde SAR kırıldı, şimdi fiyat
                    //    SAR'a geri dokunup (retest) kırılım yönünde kapattıysa → sinyal
                    const int retestLookback = 10;
                    double barLow = Data[currentIndex].Low;

                    for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                    {
                        if (k < 1) continue;

                        if (!buy && YukarıKesti(k, source, sar)
                            && barLow <= currentSar           // bu bar SAR'a geri dokundu (retest)
                            && currentPrice > currentSar)     // ama üstünde kapattı (retest tuttu)
                        {
                            buy = true;
                        }
                    }
                }
                else if (buySignalModeIndex == 6)
                {
                    // 6: Confirmation bars - kırılım confirmBars bar önce oldu ve o zamandan beri fiyat
                    //    hep SAR'ın aynı tarafında kaldıysa gir
                    const int confirmBars = 3;
                    if (currentIndex >= confirmBars + 1)
                    {
                        int crossBar = currentIndex - confirmBars;

                        bool stayedAbove = YukarıKesti(crossBar, source, sar);
                        for (int k = crossBar + 1; k <= currentIndex; k++)
                        {
                            stayedAbove &= source[k] > sar[k];
                        }
                        if (stayedAbove) buy = true;
                    }
                }
                else if (buySignalModeIndex == 7)
                {
                    // 7: Fiyat eğimi + SAR state - rejim (fiyat-SAR konumu) + momentum (fiyatın N-bar eğimi)
                    const int slopeLookback = 3;
                    if (currentIndex >= slopeLookback)
                    {
                        bool priceRising = source[currentIndex] > source[currentIndex - slopeLookback];
                        if (Buyuk(currentIndex, source, sar) && priceRising) buy = true;
                    }
                }
            }

            if (sellModeEnabled)
            {
                if (sellSignalModeIndex == 0)
                {
                    // 0: Fiyat-SAR kırılımı - fiyat SAR'ı aşağı kesince SAT
                    if (AsagiKesti(currentIndex, source, sar)) sell = true;
                }
                else if (sellSignalModeIndex == 1)
                {
                    // 1: Trend flip - indikatörün kendi Trend dizisi true'dan false'a dönüyor (SAT)
                    if (prevTrend && !currentTrend) sell = true;
                }
                else if (sellSignalModeIndex == 2)
                {
                    // 2: SAR slope flip - SAR'ın kendi yönü dönüyor (yükselen/düz → düşen = SAT)
                    if (currentIndex >= 2)
                    {
                        double slopeNow  = sar[currentIndex]     - sar[currentIndex - 1];
                        double slopePrev = sar[currentIndex - 1] - sar[currentIndex - 2];
                        if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 3)
                {
                    // 3: SAR state - fiyatın SAR'a göre konumu (kesişim değil, koşul sürdükçe her bar)
                    if (Kucuk(currentIndex, source, sar)) sell = true;
                }
                else if (sellSignalModeIndex == 4)
                {
                    // 4: Band / uzaklık filtresi - fiyat SAR'dan %bandThreshold'dan fazla uzaklaşınca (trend-following)
                    const double bandThreshold = 0.01; // %1
                    if (currentSar != 0.0)
                    {
                        double distanceRatio = (currentPrice - currentSar) / currentSar;
                        if (distanceRatio < -bandThreshold) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 5)
                {
                    // 5: Breakout + retest - son retestLookback bar içinde SAR kırıldı, şimdi fiyat
                    //    SAR'a geri dokunup (retest) kırılım yönünde kapattıysa → sinyal
                    const int retestLookback = 10;
                    double barHigh = Data[currentIndex].High;

                    for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                    {
                        if (k < 1) continue;

                        if (!sell && AsagiKesti(k, source, sar)
                            && barHigh >= currentSar
                            && currentPrice < currentSar)
                        {
                            sell = true;
                        }
                    }
                }
                else if (sellSignalModeIndex == 6)
                {
                    // 6: Confirmation bars - kırılım confirmBars bar önce oldu ve o zamandan beri fiyat
                    //    hep SAR'ın aynı tarafında kaldıysa gir
                    const int confirmBars = 3;
                    if (currentIndex >= confirmBars + 1)
                    {
                        int crossBar = currentIndex - confirmBars;

                        bool stayedBelow = AsagiKesti(crossBar, source, sar);
                        for (int k = crossBar + 1; k <= currentIndex; k++)
                        {
                            stayedBelow &= source[k] < sar[k];
                        }
                        if (stayedBelow) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 7)
                {
                    // 7: Fiyat eğimi + SAR state - rejim (fiyat-SAR konumu) + momentum (fiyatın N-bar eğimi)
                    const int slopeLookback = 3;
                    if (currentIndex >= slopeLookback)
                    {
                        bool priceFalling = source[currentIndex] < source[currentIndex - slopeLookback];
                        if (Kucuk(currentIndex, source, sar) && priceFalling) sell = true;
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

        // IsFirstBarOfDay/IsLastBarOfDay/IsFirstBarOfWeek/IsFirstBarOfMonth artik BaseStrategy'de
        // (protected) - burada tekrar tanimlanmaz.

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
