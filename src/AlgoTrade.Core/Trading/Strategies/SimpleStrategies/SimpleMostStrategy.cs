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
    /// - buySignalModeIndex/sellSignalModeIndex: buy ve sell yöntemini AYRI AYRI seçer (asymmetric -
    ///   buy başka bir moddan, sell başka bir moddan gelebilir). Her ikisi de aynı mod kümesinden seçilir:
    ///     0: Fiyat-MOST kırılımı        (fiyat MOST'u yukarı/aşağı kesince)
    ///     1: MOST-EXMOV kesişimi        (EXMOV MOST'u yukarı/aşağı kesince)
    ///     2: MOST slope flip           (MOST'un kendi yönü dönünce)
    ///     3: MOST state                (fiyatın MOST'a göre konumu - kesişim değil, koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi   (fiyat MOST'tan %bandThreshold'dan fazla uzaklaşınca)
    ///     5: Breakout + retest         (MOST kırılıp fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars         (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: EXMOV eğimi + MOST state  (rejim: fiyat-MOST konumu + momentum: EXMOV N-bar eğimi)
    /// - takeProfitExitModeIndex/stopLossExitModeIndex: takeProfit/stopLoss yöntemini AYRI AYRI seçer
    ///   (Trader.karAlZararKes üzerinden), her ikisi de aynı mod kümesinden seçilir:
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

        // barCount/openPrices/.../epochTimes artik BaseStrategy'de (protected) - LoadCommonSeries()
        // tarafindan Initialize() icinde OnInit()'ten once doldurulur, burada tekrar tanimlanmaz.

        private readonly int period;
        private readonly double percent;

        // buySignalModeIndex/sellSignalModeIndex/takeProfitExitModeIndex/stopLossExitModeIndex/flatModeIndex/skipModeIndex/ruleModeIndex artik BaseStrategy'de
        // tanimli (protected, readonly degil) - degerleri asagida constructor'da parametre olarak atanir.
        // buySignalModeIndex/sellSignalModeIndex'in dispatch mantigi (OnStep'teki if/else zincirleri) stratejiye ozgu, burada kalir.

        // MOST EXMOV hesabı - parametreli ctor'dan gelir; verilmezse EMA + Close (klasik MOST ile birebir aynı).
        // priceSource hem MOST'un EXMOV beslemesini hem OnStep sinyal kaynağını sürer.
        private readonly PriceSource priceSource  = PriceSource.Close;
        private readonly MAMethod    mostMaMethod = MAMethod.EMA;

        // startTime/stopTime/startDay/stopDay/isTimeEnabled/isDayEnabled/triggerTime/isTriggerTimeEnabled
        // artik BaseStrategy'de tanimli (protected, readonly degil) - degerleri asagida constructor'da atanir.

        private double[]? source;   // priceSource'un çözülmüş hali - OnInit'te bir kez, OnStep bundan okur
        private double[]? most;
        private double[]? exmov;

        // runContextResolved/timeframeMinutes/isOptimizationRun/isOneMinute.../ResolveRunContext() artik
        // BaseStrategy'de (protected) - burada tekrar tanimlanmaz.

        // Parametreli constructor (data/indicators gerekli — parametresiz ctor kaldırıldı, hiç kullanılmıyordu)
        public SimpleMostStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 21, double percent = 1.0, MAMethod mostMaMethod = MAMethod.EMA, PriceSource priceSource = PriceSource.Close,
            int buySignalModeIndex = 0, int sellSignalModeIndex = 0, int takeProfitExitModeIndex = 0, int stopLossExitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period                  = period;
            this.percent                 = percent;
            this.mostMaMethod            = mostMaMethod;
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

            // Orijinalde takeProfit/stopLoss guard'i kosulsuzdu ("1 == 1 && Trader != null") - ayni
            // davranisi acikca korumak icin enable flag'leri burada true olarak setleniyor.
            takeProfitExitModeEnabled = true;
            stopLossExitModeEnabled   = true;

            Parameters["Period"]                  = period;
            Parameters["Percent"]                 = percent;
            Parameters["MostMaMethod"]            = mostMaMethod;
            Parameters["PriceSource"]             = priceSource;
            Parameters["BuySignalModeIndex"]      = buySignalModeIndex;
            Parameters["SellSignalModeIndex"]     = sellSignalModeIndex;
            Parameters["TakeProfitExitModeIndex"] = takeProfitExitModeIndex;
            Parameters["StopLossExitModeIndex"]   = stopLossExitModeIndex;
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
            Parameters["BuyModeEnabled"]        = buyModeEnabled;
            Parameters["SellModeEnabled"]       = sellModeEnabled;
            Parameters["TakeProfitExitModeEnabled"] = takeProfitExitModeEnabled;
            Parameters["StopLossExitModeEnabled"]   = stopLossExitModeEnabled;
            Parameters["FlatModeEnabled"]       = flatModeEnabled;
            Parameters["SkipModeEnabled"]       = skipModeEnabled;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            try
            {
                // barCount/openPrices/.../epochTimes BaseStrategy.LoadCommonSeries() tarafindan bu
                // noktada zaten dolu (Initialize() icinde OnInit()'ten once cagrildi).
                source = Indicators.Trend.ResolvePriceSource(priceSource);

                // MOST indicator'ı hesapla (mostMaMethod / priceSource ile EXMOV parametrik)
                (most, exmov) = Indicators.Trend.MOST(period, percent, mostMaMethod, priceSource);

                // most/exmov/source OnStep'te barCount ile ayni index'te okunuyor - uzunluklari
                // uyusmazsa (or. indikator filtrelenmis/kirpilmis dondurdu) IndexOutOfRange yerine
                // burada net hata ver (ortak diziler icin ayni kontrol LoadCommonSeries()'te yapildi).
                bool allSeriesLengthsMatch = true;
                allSeriesLengthsMatch &= most.Length   == barCount;
                allSeriesLengthsMatch &= exmov.Length  == barCount;
                allSeriesLengthsMatch &= source.Length == barCount;

                if (!allSeriesLengthsMatch)
                {
                    throw new InvalidOperationException(
                        $"Seri uzunlukları uyuşmuyor (barCount={barCount}): " +
                        $"most={most.Length}, exmov={exmov.Length}, source={source.Length}");
                }

                //Log($"SimpleMostStrategy initialized: Period={period}, Percent={percent}, BuySignalModeIndex={buySignalModeIndex}, SellSignalModeIndex={sellSignalModeIndex}");
            }
            catch (NotImplementedException)
            {
                // MOST implement edilmiş durumda (TrendIndicators.cs), bu blok normalde tetiklenmez -
                // savunma amaçlı bırakıldı, indikatör ileride kaldırılır/bozulursa sessizce crash yerine uyarı verir.
                LogWarning("MOST indicator threw NotImplementedException! Strategy will not generate signals.");
                LogWarning("Check src/Trading/Indicators/Trend/TrendIndicators.cs — MOST() implementation may be missing/broken.");

                most   = new double[barCount];
                exmov  = new double[barCount];
                source = new double[barCount];
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

            if (buyModeEnabled)
            {
                if (buySignalModeIndex == 0)
                {
                    // 0: Fiyat-MOST kırılımı - fiyat MOST'u yukarı kesince AL
                    if (YukarıKesti(currentIndex, source, most)) buy = true;
                }
                else if (buySignalModeIndex == 1)
                {
                    // 1: EXMOV-MOST kesişimi - EXMOV, MOST'u yukarı kesince AL
                    if (YukarıKesti(currentIndex, exmov, most)) buy = true;
                }
                else if (buySignalModeIndex == 2)
                {
                    // 2: MOST slope flip - MOST'un kendi yönü dönüyor (düşen/düz → yükselen = AL)
                    if (currentIndex >= 2)
                    {
                        double slopeNow  = most[currentIndex]     - most[currentIndex - 1];
                        double slopePrev = most[currentIndex - 1] - most[currentIndex - 2];
                        if (slopePrev <= 0.0 && slopeNow > 0.0) buy = true;
                    }
                }
                else if (buySignalModeIndex == 3)
                {
                    // 3: MOST state - fiyatın MOST'a göre konumu (kesişim değil, koşul sürdükçe her bar)
                    if (Buyuk(currentIndex, source, most)) buy = true;
                }
                else if (buySignalModeIndex == 4)
                {
                    // 4: Band / uzaklık filtresi - fiyat MOST'tan %bandThreshold'dan fazla uzaklaşınca (trend-following)
                    const double bandThreshold = 0.01; // %1
                    if (currentMost != 0.0)
                    {
                        double distanceRatio = (currentPrice - currentMost) / currentMost;
                        if (distanceRatio > bandThreshold) buy = true;
                    }
                }
                else if (buySignalModeIndex == 5)
                {
                    // 5: Breakout + retest - son retestLookback bar içinde MOST kırıldı, şimdi fiyat
                    //    MOST'a geri dokunup (retest) kırılım yönünde kapattıysa → sinyal
                    const int retestLookback = 10;
                    double barLow = Data[currentIndex].Low;

                    for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                    {
                        if (k < 1) continue;

                        if (!buy && YukarıKesti(k, source, most)
                            && barLow <= currentMost          // bu bar MOST'a geri dokundu (retest)
                            && currentPrice > currentMost)    // ama üstünde kapattı (retest tuttu)
                        {
                            buy = true;
                        }
                    }
                }
                else if (buySignalModeIndex == 6)
                {
                    // 6: Confirmation bars - kırılım confirmBars bar önce oldu ve o zamandan beri fiyat
                    //    hep MOST'un aynı tarafında kaldıysa gir
                    const int confirmBars = 3;
                    if (currentIndex >= confirmBars + 1)
                    {
                        int crossBar = currentIndex - confirmBars;

                        bool stayedAbove = YukarıKesti(crossBar, source, most);
                        for (int k = crossBar + 1; k <= currentIndex; k++)
                        {
                            stayedAbove &= source[k] > most[k];
                        }
                        if (stayedAbove) buy = true;
                    }
                }
                else if (buySignalModeIndex == 7)
                {
                    // 7: EXMOV eğimi + MOST state - rejim (fiyat-MOST konumu) + momentum (EXMOV N-bar eğimi)
                    const int slopeLookback = 3;
                    if (currentIndex >= slopeLookback)
                    {
                        bool exmovRising = exmov[currentIndex] > exmov[currentIndex - slopeLookback];
                        if (Buyuk(currentIndex, source, most) && exmovRising) buy = true;
                    }
                }
            }

            if (sellModeEnabled)
            {
                if (sellSignalModeIndex == 0)
                {
                    // 0: Fiyat-MOST kırılımı - fiyat MOST'u aşağı kesince SAT
                    if (AsagiKesti(currentIndex, source, most)) sell = true;
                }
                else if (sellSignalModeIndex == 1)
                {
                    // 1: EXMOV-MOST kesişimi - EXMOV, MOST'u aşağı kesince SAT
                    if (AsagiKesti(currentIndex, exmov, most)) sell = true;
                }
                else if (sellSignalModeIndex == 2)
                {
                    // 2: MOST slope flip - MOST'un kendi yönü dönüyor (yükselen/düz → düşen = SAT)
                    if (currentIndex >= 2)
                    {
                        double slopeNow  = most[currentIndex]     - most[currentIndex - 1];
                        double slopePrev = most[currentIndex - 1] - most[currentIndex - 2];
                        if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 3)
                {
                    // 3: MOST state - fiyatın MOST'a göre konumu (kesişim değil, koşul sürdükçe her bar)
                    if (Kucuk(currentIndex, source, most)) sell = true;
                }
                else if (sellSignalModeIndex == 4)
                {
                    // 4: Band / uzaklık filtresi - fiyat MOST'tan %bandThreshold'dan fazla uzaklaşınca (trend-following)
                    const double bandThreshold = 0.01; // %1
                    if (currentMost != 0.0)
                    {
                        double distanceRatio = (currentPrice - currentMost) / currentMost;
                        if (distanceRatio < -bandThreshold) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 5)
                {
                    // 5: Breakout + retest - son retestLookback bar içinde MOST kırıldı, şimdi fiyat
                    //    MOST'a geri dokunup (retest) kırılım yönünde kapattıysa → sinyal
                    const int retestLookback = 10;
                    double barHigh = Data[currentIndex].High;

                    for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                    {
                        if (k < 1) continue;

                        if (!sell && AsagiKesti(k, source, most)
                            && barHigh >= currentMost
                            && currentPrice < currentMost)
                        {
                            sell = true;
                        }
                    }
                }
                else if (sellSignalModeIndex == 6)
                {
                    // 6: Confirmation bars - kırılım confirmBars bar önce oldu ve o zamandan beri fiyat
                    //    hep MOST'un aynı tarafında kaldıysa gir
                    const int confirmBars = 3;
                    if (currentIndex >= confirmBars + 1)
                    {
                        int crossBar = currentIndex - confirmBars;

                        bool stayedBelow = AsagiKesti(crossBar, source, most);
                        for (int k = crossBar + 1; k <= currentIndex; k++)
                        {
                            stayedBelow &= source[k] < most[k];
                        }
                        if (stayedBelow) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 7)
                {
                    // 7: EXMOV eğimi + MOST state - rejim (fiyat-MOST konumu) + momentum (EXMOV N-bar eğimi)
                    const int slopeLookback = 3;
                    if (currentIndex >= slopeLookback)
                    {
                        bool exmovFalling = exmov[currentIndex] < exmov[currentIndex - slopeLookback];
                        if (Kucuk(currentIndex, source, most) && exmovFalling) sell = true;
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

        // IsFirstBarOfDay/IsLastBarOfDay/IsFirstBarOfWeek/IsFirstBarOfMonth artik BaseStrategy'de
        // (protected) - burada tekrar tanimlanmaz.

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
