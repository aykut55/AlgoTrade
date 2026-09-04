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
    /// - buySignalModeIndex/sellSignalModeIndex: buy ve sell yöntemini AYRI AYRI seçer (asymmetric -
    ///   buy başka bir moddan, sell başka bir moddan gelebilir). Her ikisi de aynı mod kümesinden seçilir:
    ///     0: Fiyat-OTT kırılımı        (fiyat OTT'yi yukarı/aşağı kesince)
    ///     1: MA-OTT kesişimi           (MA OTT'yi yukarı/aşağı kesince)
    ///     2: OTT slope flip           (OTT'un kendi yönü dönünce)
    ///     3: OTT state                (fiyatın OTT'a göre konumu - kesişim değil, koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi   (fiyat OTT'tan %bandThreshold'dan fazla uzaklaşınca)
    ///     5: Breakout + retest         (OTT kırılıp fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars         (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: MA eğimi + OTT state     (rejim: fiyat-OTT konumu + momentum: MA N-bar eğimi)
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
    public class SimpleOTTStrategy : BaseStrategy
    {
        public override string Name => "Simple OTT Strategy";

        // barCount/openPrices/.../epochTimes artik BaseStrategy'de (protected) - LoadCommonSeries()
        // tarafindan Initialize() icinde OnInit()'ten once doldurulur, burada tekrar tanimlanmaz.

        private readonly int period;
        private readonly double percent;

        // buySignalModeIndex/sellSignalModeIndex/takeProfitExitModeIndex/stopLossExitModeIndex/flatModeIndex/skipModeIndex/ruleModeIndex artik BaseStrategy'de
        // tanimli (protected, readonly degil) - degerleri asagida constructor'da parametre olarak atanir.
        // buySignalModeIndex/sellSignalModeIndex'in dispatch mantigi (OnStep'teki if/else zincirleri) stratejiye ozgu, burada kalir.

        // OTT MA hesabı - parametreli ctor'dan gelir; verilmezse VIDYA + Close (klasik OTT ile birebir aynı).
        // priceSource hem OTT'un MA beslemesini hem OnStep sinyal kaynağını sürer.
        private readonly PriceSource priceSource = PriceSource.Close;
        private readonly MAMethod    ottMaMethod = MAMethod.VIDYA;

        // startTime/stopTime/startDay/stopDay/isTimeEnabled/isDayEnabled/triggerTime/isTriggerTimeEnabled
        // artik BaseStrategy'de tanimli (protected, readonly degil) - degerleri asagida constructor'da atanir.

        private double[]? source;   // priceSource'un çözülmüş hali - OnInit'te bir kez, OnStep bundan okur
        private double[]? ott;
        private double[]? ma;

        // runContextResolved/timeframeMinutes/isOptimizationRun/isOneMinute.../ResolveRunContext() artik
        // BaseStrategy'de (protected) - burada tekrar tanimlanmaz.

        // Parametreli constructor (data/indicators gerekli — parametresiz ctor kaldırıldı, hiç kullanılmıyordu)
        public SimpleOTTStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 2, double percent = 1.4, MAMethod ottMaMethod = MAMethod.VIDYA, PriceSource priceSource = PriceSource.Close,
            int buySignalModeIndex = 0, int sellSignalModeIndex = 0, int takeProfitExitModeIndex = 0, int stopLossExitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period                  = period;
            this.percent                 = percent;
            this.ottMaMethod             = ottMaMethod;
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
            Parameters["OttMaMethod"]             = ottMaMethod;
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

                // OTT indicator'ı hesapla (ottMaMethod / priceSource ile MA parametrik)
                var ottResult = Indicators.Trend.OTT(period, percent, ottMaMethod, priceSource);
                ott = ottResult.OTT;
                ma  = ottResult.MA;

                // ott/ma/source OnStep'te barCount ile ayni index'te okunuyor - uzunluklari
                // uyusmazsa (or. indikator filtrelenmis/kirpilmis dondurdu) IndexOutOfRange yerine
                // burada net hata ver (ortak diziler icin ayni kontrol LoadCommonSeries()'te yapildi).
                bool allSeriesLengthsMatch = true;
                allSeriesLengthsMatch &= ott.Length    == barCount;
                allSeriesLengthsMatch &= ma.Length     == barCount;
                allSeriesLengthsMatch &= source.Length == barCount;

                if (!allSeriesLengthsMatch)
                {
                    throw new InvalidOperationException(
                        $"Seri uzunlukları uyuşmuyor (barCount={barCount}): ott={ott.Length}, ma={ma.Length}, source={source.Length}");
                }

                //Log($"SimpleOTTStrategy initialized: Period={period}, Percent={percent}, BuySignalModeIndex={buySignalModeIndex}, SellSignalModeIndex={sellSignalModeIndex}");
            }
            catch (NotImplementedException)
            {
                // OTT/MA implement edilmiş durumda, bu blok normalde tetiklenmez -
                // savunma amaçlı bırakıldı, indikatör ileride kaldırılır/bozulursa sessizce crash yerine uyarı verir.
                LogWarning("OTT indicator threw NotImplementedException! Strategy will not generate signals.");
                LogWarning("Check src/Trading/Indicators/Trend/TrendIndicators.cs — OTT() implementation may be missing/broken.");

                ott    = new double[barCount];
                ma     = new double[barCount];
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
                    // 0: Fiyat-OTT kırılımı - fiyat OTT'yi yukarı kesince AL
                    if (YukarıKesti(currentIndex, source, ott)) buy = true;
                }
                else if (buySignalModeIndex == 1)
                {
                    // 1: MA-OTT kesişimi - MA, OTT'yi yukarı kesince AL
                    if (YukarıKesti(currentIndex, ma, ott)) buy = true;
                }
                else if (buySignalModeIndex == 2)
                {
                    // 2: OTT slope flip - OTT'un kendi yönü dönüyor (düşen/düz → yükselen = AL)
                    if (currentIndex >= 2)
                    {
                        double slopeNow  = ott[currentIndex]     - ott[currentIndex - 1];
                        double slopePrev = ott[currentIndex - 1] - ott[currentIndex - 2];
                        if (slopePrev <= 0.0 && slopeNow > 0.0) buy = true;
                    }
                }
                else if (buySignalModeIndex == 3)
                {
                    // 3: OTT state - fiyatın OTT'a göre konumu (kesişim değil, koşul sürdükçe her bar)
                    if (Buyuk(currentIndex, source, ott)) buy = true;
                }
                else if (buySignalModeIndex == 4)
                {
                    // 4: Band / uzaklık filtresi - fiyat OTT'tan %bandThreshold'dan fazla uzaklaşınca (trend-following)
                    const double bandThreshold = 0.01; // %1
                    if (currentOtt != 0.0)
                    {
                        double distanceRatio = (currentPrice - currentOtt) / currentOtt;
                        if (distanceRatio > bandThreshold) buy = true;
                    }
                }
                else if (buySignalModeIndex == 5)
                {
                    // 5: Breakout + retest - son retestLookback bar içinde OTT kırıldı, şimdi fiyat
                    //    OTT'a geri dokunup (retest) kırılım yönünde kapattıysa → sinyal
                    const int retestLookback = 10;
                    double barLow = Data[currentIndex].Low;

                    for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                    {
                        if (k < 1) continue;

                        if (!buy && YukarıKesti(k, source, ott)
                            && barLow <= currentOtt           // bu bar OTT'a geri dokundu (retest)
                            && currentPrice > currentOtt)     // ama üstünde kapattı (retest tuttu)
                        {
                            buy = true;
                        }
                    }
                }
                else if (buySignalModeIndex == 6)
                {
                    // 6: Confirmation bars - kırılım confirmBars bar önce oldu ve o zamandan beri fiyat
                    //    hep OTT'un aynı tarafında kaldıysa gir
                    const int confirmBars = 3;
                    if (currentIndex >= confirmBars + 1)
                    {
                        int crossBar = currentIndex - confirmBars;

                        bool stayedAbove = YukarıKesti(crossBar, source, ott);
                        for (int k = crossBar + 1; k <= currentIndex; k++)
                        {
                            stayedAbove &= source[k] > ott[k];
                        }
                        if (stayedAbove) buy = true;
                    }
                }
                else if (buySignalModeIndex == 7)
                {
                    // 7: MA eğimi + OTT state - rejim (fiyat-OTT konumu) + momentum (MA N-bar eğimi)
                    const int slopeLookback = 3;
                    if (currentIndex >= slopeLookback)
                    {
                        bool maRising = ma[currentIndex] > ma[currentIndex - slopeLookback];
                        if (Buyuk(currentIndex, source, ott) && maRising) buy = true;
                    }
                }
            }

            if (sellModeEnabled)
            {
                if (sellSignalModeIndex == 0)
                {
                    // 0: Fiyat-OTT kırılımı - fiyat OTT'yi aşağı kesince SAT
                    if (AsagiKesti(currentIndex, source, ott)) sell = true;
                }
                else if (sellSignalModeIndex == 1)
                {
                    // 1: MA-OTT kesişimi - MA, OTT'yi aşağı kesince SAT
                    if (AsagiKesti(currentIndex, ma, ott)) sell = true;
                }
                else if (sellSignalModeIndex == 2)
                {
                    // 2: OTT slope flip - OTT'un kendi yönü dönüyor (yükselen/düz → düşen = SAT)
                    if (currentIndex >= 2)
                    {
                        double slopeNow  = ott[currentIndex]     - ott[currentIndex - 1];
                        double slopePrev = ott[currentIndex - 1] - ott[currentIndex - 2];
                        if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 3)
                {
                    // 3: OTT state - fiyatın OTT'a göre konumu (kesişim değil, koşul sürdükçe her bar)
                    if (Kucuk(currentIndex, source, ott)) sell = true;
                }
                else if (sellSignalModeIndex == 4)
                {
                    // 4: Band / uzaklık filtresi - fiyat OTT'tan %bandThreshold'dan fazla uzaklaşınca (trend-following)
                    const double bandThreshold = 0.01; // %1
                    if (currentOtt != 0.0)
                    {
                        double distanceRatio = (currentPrice - currentOtt) / currentOtt;
                        if (distanceRatio < -bandThreshold) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 5)
                {
                    // 5: Breakout + retest - son retestLookback bar içinde OTT kırıldı, şimdi fiyat
                    //    OTT'a geri dokunup (retest) kırılım yönünde kapattıysa → sinyal
                    const int retestLookback = 10;
                    double barHigh = Data[currentIndex].High;

                    for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                    {
                        if (k < 1) continue;

                        if (!sell && AsagiKesti(k, source, ott)
                            && barHigh >= currentOtt
                            && currentPrice < currentOtt)
                        {
                            sell = true;
                        }
                    }
                }
                else if (sellSignalModeIndex == 6)
                {
                    // 6: Confirmation bars - kırılım confirmBars bar önce oldu ve o zamandan beri fiyat
                    //    hep OTT'un aynı tarafında kaldıysa gir
                    const int confirmBars = 3;
                    if (currentIndex >= confirmBars + 1)
                    {
                        int crossBar = currentIndex - confirmBars;

                        bool stayedBelow = AsagiKesti(crossBar, source, ott);
                        for (int k = crossBar + 1; k <= currentIndex; k++)
                        {
                            stayedBelow &= source[k] < ott[k];
                        }
                        if (stayedBelow) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 7)
                {
                    // 7: MA eğimi + OTT state - rejim (fiyat-OTT konumu) + momentum (MA N-bar eğimi)
                    const int slopeLookback = 3;
                    if (currentIndex >= slopeLookback)
                    {
                        bool maFalling = ma[currentIndex] < ma[currentIndex - slopeLookback];
                        if (Kucuk(currentIndex, source, ott) && maFalling) sell = true;
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

        // IsFirstBarOfDay/IsLastBarOfDay/IsFirstBarOfWeek/IsFirstBarOfMonth artik BaseStrategy'de
        // (protected) - burada tekrar tanimlanmaz.

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
