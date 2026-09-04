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
    /// Tillson T3 İndikatörü Stratejisi
    ///
    /// T3 Mantığı:
    /// - Triple exponential smoothing ile düşük gecikmeli tek MA hattı (ikinci referans çizgisi yok)
    ///
    /// Parametreler:
    /// - period: T3 periyodu (varsayılan 5)
    /// - priceSource: T3'ün beslendiği kaynak (varsayılan Close - klasik T3)
    /// - buySignalModeIndex/sellSignalModeIndex: buy ve sell yöntemini AYRI AYRI seçer (asymmetric -
    ///   buy başka bir moddan, sell başka bir moddan gelebilir). Her ikisi de aynı mod kümesinden seçilir:
    ///     0: Fiyat-T3 kırılımı           (fiyat T3'ü yukarı/aşağı kesince)
    ///     1: Eğim-teyitli kırılım        (fiyat T3'ü kesince VE T3 aynı yönde eğimliyse - ikinci referans çizgisi olmadığı için T3'ün kendi eğimi teyit görevi görür)
    ///     2: T3 slope flip               (T3'ün kendi yönü dönünce)
    ///     3: T3 state                    (fiyatın T3'e göre konumu - koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi     (fiyat T3'ten %bandThreshold'dan fazla uzaklaşınca)
    ///     5: Breakout + retest           (T3 kırılıp fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars           (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: Fiyat eğimi + T3 state      (rejim: fiyat-T3 konumu + momentum: fiyatın N-bar eğimi)
    /// - takeProfitExitModeIndex/stopLossExitModeIndex: takeProfit/stopLoss yöntemini AYRI AYRI seçer
    ///   (Trader.karAlZararKes üzerinden), her ikisi de aynı mod kümesinden:
    ///     0: Seviye, seviyeli   1: Yüzde, seviyeli   2: Seviye, tek seviye   3: Yüzde, tek seviye
    ///     4: Anlık kar/zarar fiyat seviyesi   5: Anlık kar/zarar yüzdesi
    /// - flatModeIndex/skipModeIndex/ruleModeIndex: PLACEHOLDER, henuz okunmuyor
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
    public class SimpleTillsonT3Strategy : BaseStrategy
    {
        public override string Name => "Simple Tillson T3 Strategy";

        // barCount/openPrices/.../epochTimes artik BaseStrategy'de (protected) - LoadCommonSeries()
        // tarafindan Initialize() icinde OnInit()'ten once doldurulur, burada tekrar tanimlanmaz.

        private readonly int period;

        // buySignalModeIndex/sellSignalModeIndex/takeProfitExitModeIndex/stopLossExitModeIndex/flatModeIndex/skipModeIndex/ruleModeIndex artik BaseStrategy'de
        // tanimli (protected, readonly degil) - degerleri asagida constructor'da parametre olarak atanir.
        // buySignalModeIndex/sellSignalModeIndex'in dispatch mantigi (OnStep'teki if/else zincirleri) stratejiye ozgu, burada kalir.

        private readonly PriceSource priceSource = PriceSource.Close;

        // startTime/stopTime/startDay/stopDay/isTimeEnabled/isDayEnabled/triggerTime/isTriggerTimeEnabled
        // artik BaseStrategy'de tanimli (protected, readonly degil) - degerleri asagida constructor'da atanir.

        private double[]? source;
        private double[]? t3;

        // runContextResolved/timeframeMinutes/isOptimizationRun/isOneMinute.../ResolveRunContext() artik
        // BaseStrategy'de (protected) - burada tekrar tanimlanmaz.

        public SimpleTillsonT3Strategy(List<StockData> data, IndicatorManager indicators,
            int period = 5, PriceSource priceSource = PriceSource.Close,
            int buySignalModeIndex = 0, int sellSignalModeIndex = 0, int takeProfitExitModeIndex = 0, int stopLossExitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period                  = period;
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

            // Bu stratejide takeProfit/stopLoss orijinalde kosulsuz aktifti (if (1 == 1 && Trader != null)) -
            // BaseStrategy varsayilani zaten true, burada aciklik icin ayrica true set edilir.
            takeProfitExitModeEnabled = true;
            stopLossExitModeEnabled   = true;

            Parameters["Period"]                  = period;
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

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            // barCount/openPrices/.../epochTimes BaseStrategy.LoadCommonSeries() tarafindan bu
            // noktada zaten dolu (Initialize() icinde OnInit()'ten once cagrildi).
            source = Indicators.Trend.ResolvePriceSource(priceSource);

            t3 = Indicators.MA.T3(source, period);

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= t3.Length      == barCount;
            allSeriesLengthsMatch &= source.Length  == barCount;

            if (!allSeriesLengthsMatch)
            {
                throw new InvalidOperationException(
                    $"Seri uzunlukları uyuşmuyor (barCount={barCount}): t3={t3.Length}, source={source.Length}");
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false, sell = false, takeProfit = false, stopLoss = false, flat = false, skip = false;

            ResolveRunContext(currentIndex);

            TimeOnly currentTime    = times![currentIndex];
            DateOnly currentDate    = dates![currentIndex];
            bool isWithinTimeWindow = !isTimeEnabled || (currentTime >= startTime && currentTime <= stopTime);
            bool isWithinDayWindow  = !isDayEnabled  || (currentDate >= startDay  && currentDate <= stopDay);
            bool isTriggerTime      = isTriggerTimeEnabled && currentTime == triggerTime;

            if (currentIndex < period * 6 + 1) // T3 6x EMA gerektirir
                return TradeSignals.None;

            if (t3 == null || source == null || t3.Length == 0)
                return TradeSignals.None;

            double currentPrice = source[currentIndex];
            double currentT3 = t3[currentIndex];

            if (double.IsNaN(currentT3))
                return TradeSignals.None;

            // isOneMinute/isFiveMinute/isOneHour/isFourHour/isOneDay artık field - ResolveRunContext'te
            // bir kez set edilir, burada tekrar hesaplanmaz (run boyunca degismezler).
                 if (isOneMinute)   { }
            else if (isFiveMinute)  { }
            else if (isOneHour)     { }
            else if (isFourHour)    { }
            else if (isOneDay)      { }

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

            if (buyModeEnabled)
            {
                if (buySignalModeIndex == 0)
                {
                    // 0: Fiyat-T3 kırılımı (klasik) - fiyat T3'ü yukarı kesince AL
                    if (YukarıKesti(currentIndex, source, t3)) buy = true;
                }
                else if (buySignalModeIndex == 1)
                {
                    // 1: Eğim-teyitli kırılım - fiyat T3'ü kesip T3 de aynı yönde eğimliyse
                    if (currentIndex >= 1)
                    {
                        bool t3Rising = t3[currentIndex] > t3[currentIndex - 1];
                        if (YukarıKesti(currentIndex, source, t3) && t3Rising) buy = true;
                    }
                }
                else if (buySignalModeIndex == 2)
                {
                    // 2: T3 slope flip
                    if (currentIndex >= 2)
                    {
                        double slopeNow  = t3[currentIndex]     - t3[currentIndex - 1];
                        double slopePrev = t3[currentIndex - 1] - t3[currentIndex - 2];
                        if (slopePrev <= 0.0 && slopeNow > 0.0) buy = true;
                    }
                }
                else if (buySignalModeIndex == 3)
                {
                    // 3: T3 state
                    if (Buyuk(currentIndex, source, t3)) buy = true;
                }
                else if (buySignalModeIndex == 4)
                {
                    // 4: Band / uzaklık filtresi
                    const double bandThreshold = 0.01; // %1
                    if (currentT3 != 0.0)
                    {
                        double distanceRatio = (currentPrice - currentT3) / currentT3;
                        if (distanceRatio > bandThreshold) buy = true;
                    }
                }
                else if (buySignalModeIndex == 5)
                {
                    // 5: Breakout + retest
                    const int retestLookback = 10;
                    double barLow = Data[currentIndex].Low;

                    for (int m = currentIndex - retestLookback; m < currentIndex; m++)
                    {
                        if (m < 1) continue;

                        if (!buy && YukarıKesti(m, source, t3)
                            && barLow <= currentT3
                            && currentPrice > currentT3)
                        {
                            buy = true;
                        }
                    }
                }
                else if (buySignalModeIndex == 6)
                {
                    // 6: Confirmation bars
                    const int confirmBars = 3;
                    if (currentIndex >= confirmBars + 1)
                    {
                        int crossBar = currentIndex - confirmBars;

                        bool stayedAbove = YukarıKesti(crossBar, source, t3);
                        for (int m = crossBar + 1; m <= currentIndex; m++)
                        {
                            stayedAbove &= source[m] > t3[m];
                        }
                        if (stayedAbove) buy = true;
                    }
                }
                else if (buySignalModeIndex == 7)
                {
                    // 7: Fiyat eğimi + T3 state
                    const int slopeLookback = 3;
                    if (currentIndex >= slopeLookback)
                    {
                        bool priceRising = source[currentIndex] > source[currentIndex - slopeLookback];
                        if (Buyuk(currentIndex, source, t3) && priceRising) buy = true;
                    }
                }
            }

            if (sellModeEnabled)
            {
                if (sellSignalModeIndex == 0)
                {
                    // 0: Fiyat-T3 kırılımı (klasik) - fiyat T3'ü aşağı kesince SAT
                    if (AsagiKesti(currentIndex, source, t3)) sell = true;
                }
                else if (sellSignalModeIndex == 1)
                {
                    // 1: Eğim-teyitli kırılım - fiyat T3'ü kesip T3 de aynı yönde eğimliyse
                    if (currentIndex >= 1)
                    {
                        bool t3Falling = t3[currentIndex] < t3[currentIndex - 1];
                        if (AsagiKesti(currentIndex, source, t3) && t3Falling) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 2)
                {
                    // 2: T3 slope flip
                    if (currentIndex >= 2)
                    {
                        double slopeNow  = t3[currentIndex]     - t3[currentIndex - 1];
                        double slopePrev = t3[currentIndex - 1] - t3[currentIndex - 2];
                        if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 3)
                {
                    // 3: T3 state
                    if (Kucuk(currentIndex, source, t3)) sell = true;
                }
                else if (sellSignalModeIndex == 4)
                {
                    // 4: Band / uzaklık filtresi
                    const double bandThreshold = 0.01; // %1
                    if (currentT3 != 0.0)
                    {
                        double distanceRatio = (currentPrice - currentT3) / currentT3;
                        if (distanceRatio < -bandThreshold) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 5)
                {
                    // 5: Breakout + retest
                    const int retestLookback = 10;
                    double barHigh = Data[currentIndex].High;

                    for (int m = currentIndex - retestLookback; m < currentIndex; m++)
                    {
                        if (m < 1) continue;

                        if (!sell && AsagiKesti(m, source, t3)
                            && barHigh >= currentT3
                            && currentPrice < currentT3)
                        {
                            sell = true;
                        }
                    }
                }
                else if (sellSignalModeIndex == 6)
                {
                    // 6: Confirmation bars
                    const int confirmBars = 3;
                    if (currentIndex >= confirmBars + 1)
                    {
                        int crossBar = currentIndex - confirmBars;

                        bool stayedBelow = AsagiKesti(crossBar, source, t3);
                        for (int m = crossBar + 1; m <= currentIndex; m++)
                        {
                            stayedBelow &= source[m] < t3[m];
                        }
                        if (stayedBelow) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 7)
                {
                    // 7: Fiyat eğimi + T3 state
                    const int slopeLookback = 3;
                    if (currentIndex >= slopeLookback)
                    {
                        bool priceFalling = source[currentIndex] < source[currentIndex - slopeLookback];
                        if (Kucuk(currentIndex, source, t3) && priceFalling) sell = true;
                    }
                }
            }

            if (takeProfitExitModeEnabled && Trader != null)
            {
                if (takeProfitExitModeIndex == 0)
                {
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
                }
                else if (takeProfitExitModeIndex == 1)
                {
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesaplaSeviyeli(currentIndex, 2, 10, 0.01) != 0;
                }
                else if (takeProfitExitModeIndex == 2)
                {
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesapla(currentIndex, 2000.0) != 0;
                }
                else if (takeProfitExitModeIndex == 3)
                {
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesapla(currentIndex, 2.0) != 0;
                }
                else if (takeProfitExitModeIndex == 4)
                {
                    takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(currentIndex, 1000.0) != 0;
                }
                else if (takeProfitExitModeIndex == 5)
                {
                    takeProfit = Trader.karAlZararKes.KarZararYuzdesindenKarAlHesapla(currentIndex, 3.0) != 0;
                }
            }

            if (stopLossExitModeEnabled && Trader != null)
            {
                if (stopLossExitModeIndex == 0)
                {
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
                }
                else if (stopLossExitModeIndex == 1)
                {
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesaplaSeviyeli(currentIndex, -2, -10, 0.01) != 0;
                }
                else if (stopLossExitModeIndex == 2)
                {
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesapla(currentIndex, -1000.0) != 0;
                }
                else if (stopLossExitModeIndex == 3)
                {
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesapla(currentIndex, -1.0) != 0;
                }
                else if (stopLossExitModeIndex == 4)
                {
                    stopLoss = Trader.karAlZararKes.KarZararFiyatSeviyesindenZararKesHesapla(currentIndex, -500.0) != 0;
                }
                else if (stopLossExitModeIndex == 5)
                {
                    stopLoss = Trader.karAlZararKes.KarZararYuzdesindenZararKesHesapla(currentIndex, -2.0) != 0;
                }
            }

            if (flatModeEnabled) { if (flatModeIndex == 0) flat = false; }
            if (skipModeEnabled) { if (skipModeIndex == 0) skip = false; }

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

            if (skip) return TradeSignals.Skip;
            else if (flat) return TradeSignals.Flat;
            else if (takeProfit) return TradeSignals.TakeProfit;
            else if (stopLoss) return TradeSignals.StopLoss;
            else if (buy) return TradeSignals.Buy;
            else if (sell) return TradeSignals.Sell;

            return TradeSignals.None;
        }

        // ResolveRunContext() artik BaseStrategy'de (protected) - burada tekrar tanimlanmaz.

        public override bool IsValidParameterCombination()
        {
            bool isValid = true;

            return isValid;
        }

        public double[]? GetT3() => t3;

        // IsFirstBarOfDay/IsLastBarOfDay/IsFirstBarOfWeek/IsFirstBarOfMonth artik BaseStrategy'de
        // (protected) - burada tekrar tanimlanmaz.

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            if (t3 != null && t3.Length > 0) indicators["T3"] = t3;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
