using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;
using static AlgoTrade.Core.Trading.Utils.Utils;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// Ichimoku Cloud Stratejisi (TK Cross)
    ///
    /// Ichimoku Mantığı:
    /// - Tenkan/Kijun çifti MOST'un most/exmov çiftinin analogu (TK Cross = klasik sinyal)
    /// - Tenkan/Kijun High/Low'a bağımlı, priceSource yok
    ///
    /// Parametreler:
    /// - tenkanPeriod/kijunPeriod/senkouPeriod: periyotlar (varsayılan 9/26/52)
    /// - buySignalModeIndex/sellSignalModeIndex: buy ve sell yöntemini AYRI AYRI seçer (asymmetric -
    ///   buy başka bir moddan, sell başka bir moddan gelebilir). Her ikisi de aynı mod kümesinden seçilir:
    ///     0: Tenkan/Kijun kesişimi (TK Cross, klasik)
    ///     1: Fiyat-Kijun kesişimi         (fiyat Kijun'u yukarı/aşağı kesince - ikinci klasik sinyal)
    ///     2: Kijun slope flip             (Kijun'un kendi yönü dönünce)
    ///     3: Tenkan/Kijun state           (konum - kesişim değil, koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi      (Tenkan-Kijun farkı %bandThreshold'dan fazla açılınca)
    ///     5: Breakout + retest            (TK kesişip fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars            (kesişimden sonra confirmBars bar aynı tarafta kalınca)
    ///     7: Tenkan eğimi + state combo   (rejim: Tenkan/Kijun konumu + momentum: Tenkan N-bar eğimi)
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
    public class SimpleIchimokuStrategy : BaseStrategy
    {
        public override string Name => "Simple Ichimoku Strategy";

        // barCount/openPrices/.../epochTimes artik BaseStrategy'de (protected) - LoadCommonSeries()
        // tarafindan Initialize() icinde OnInit()'ten once doldurulur, burada tekrar tanimlanmaz.

        private readonly int tenkanPeriod;
        private readonly int kijunPeriod;
        private readonly int senkouPeriod;

        // buySignalModeIndex/sellSignalModeIndex/takeProfitExitModeIndex/stopLossExitModeIndex/flatModeIndex/skipModeIndex/ruleModeIndex artik BaseStrategy'de
        // tanimli (protected, readonly degil) - degerleri asagida constructor'da parametre olarak atanir.
        // buySignalModeIndex/sellSignalModeIndex'in dispatch mantigi (OnStep'teki if/else zincirleri) stratejiye ozgu, burada kalir.

        // startTime/stopTime/startDay/stopDay/isTimeEnabled/isDayEnabled/triggerTime/isTriggerTimeEnabled
        // artik BaseStrategy'de tanimli (protected, readonly degil) - degerleri asagida constructor'da atanir.

        private double[]? tenkan;
        private double[]? kijun;
        private double[]? senkouA;
        private double[]? senkouB;

        // runContextResolved/timeframeMinutes/isOptimizationRun/isOneMinute.../ResolveRunContext() artik
        // BaseStrategy'de (protected) - burada tekrar tanimlanmaz.

        public SimpleIchimokuStrategy(List<StockData> data, IndicatorManager indicators,
            int tenkanPeriod = 9, int kijunPeriod = 26, int senkouPeriod = 52,
            int buySignalModeIndex = 0, int sellSignalModeIndex = 0, int takeProfitExitModeIndex = 0, int stopLossExitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.tenkanPeriod            = tenkanPeriod;
            this.kijunPeriod             = kijunPeriod;
            this.senkouPeriod            = senkouPeriod;
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

            // Bu stratejide takeProfit/stopLoss eskiden kosulsuz (1 == 1) aktifti - BaseStrategy
            // varsayilani zaten true, davranis degismesin diye burada acikca da true birakiliyor.
            takeProfitExitModeEnabled = true;
            stopLossExitModeEnabled   = true;

            Parameters["TenkanPeriod"]            = tenkanPeriod;
            Parameters["KijunPeriod"]             = kijunPeriod;
            Parameters["SenkouPeriod"]            = senkouPeriod;
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
            var ichimokuResult = Indicators.Trend.Ichimoku(tenkanPeriod, kijunPeriod, senkouPeriod);
            tenkan  = ichimokuResult.Tenkan;
            kijun   = ichimokuResult.Kijun;
            senkouA = ichimokuResult.SenkouA;
            senkouB = ichimokuResult.SenkouB;

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= tenkan.Length == barCount;
            allSeriesLengthsMatch &= kijun.Length  == barCount;

            if (!allSeriesLengthsMatch)
            {
                throw new InvalidOperationException(
                    $"Seri uzunlukları uyuşmuyor (barCount={barCount}): tenkan={tenkan.Length}, kijun={kijun.Length}");
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

            if (currentIndex < kijunPeriod + 1)
                return TradeSignals.None;

            if (tenkan == null || kijun == null || tenkan.Length == 0)
                return TradeSignals.None;

            double currentTenkan = tenkan[currentIndex];
            double currentKijun = kijun[currentIndex];

            if (double.IsNaN(currentTenkan) || double.IsNaN(currentKijun))
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
                    // 0: Tenkan/Kijun kesişimi (TK Cross, klasik)
                    if (YukarıKesti(currentIndex, tenkan, kijun)) buy = true;
                }
                else if (buySignalModeIndex == 1)
                {
                    // 1: Fiyat-Kijun kesişimi
                    if (YukarıKesti(currentIndex, closePrices!, kijun)) buy = true;
                }
                else if (buySignalModeIndex == 2)
                {
                    // 2: Kijun slope flip
                    if (currentIndex >= 2)
                    {
                        double slopeNow  = kijun[currentIndex]     - kijun[currentIndex - 1];
                        double slopePrev = kijun[currentIndex - 1] - kijun[currentIndex - 2];
                        if (slopePrev <= 0.0 && slopeNow > 0.0) buy = true;
                    }
                }
                else if (buySignalModeIndex == 3)
                {
                    // 3: Tenkan/Kijun state
                    if (Buyuk(currentIndex, tenkan, kijun)) buy = true;
                }
                else if (buySignalModeIndex == 4)
                {
                    // 4: Band / uzaklık filtresi
                    const double bandThreshold = 0.01; // %1
                    if (currentKijun != 0.0)
                    {
                        double distanceRatio = (currentTenkan - currentKijun) / currentKijun;
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

                        if (!buy && YukarıKesti(m, tenkan, kijun)
                            && barLow <= currentKijun
                            && currentTenkan > currentKijun)
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

                        bool stayedAbove = YukarıKesti(crossBar, tenkan, kijun);
                        for (int m = crossBar + 1; m <= currentIndex; m++)
                        {
                            stayedAbove &= tenkan[m] > kijun[m];
                        }
                        if (stayedAbove) buy = true;
                    }
                }
                else if (buySignalModeIndex == 7)
                {
                    // 7: Tenkan eğimi + state combo
                    const int slopeLookback = 3;
                    if (currentIndex >= slopeLookback)
                    {
                        bool tenkanRising = tenkan[currentIndex] > tenkan[currentIndex - slopeLookback];
                        if (Buyuk(currentIndex, tenkan, kijun) && tenkanRising) buy = true;
                    }
                }
            }

            if (sellModeEnabled)
            {
                if (sellSignalModeIndex == 0)
                {
                    // 0: Tenkan/Kijun kesişimi (TK Cross, klasik)
                    if (AsagiKesti(currentIndex, tenkan, kijun)) sell = true;
                }
                else if (sellSignalModeIndex == 1)
                {
                    // 1: Fiyat-Kijun kesişimi
                    if (AsagiKesti(currentIndex, closePrices!, kijun)) sell = true;
                }
                else if (sellSignalModeIndex == 2)
                {
                    // 2: Kijun slope flip
                    if (currentIndex >= 2)
                    {
                        double slopeNow  = kijun[currentIndex]     - kijun[currentIndex - 1];
                        double slopePrev = kijun[currentIndex - 1] - kijun[currentIndex - 2];
                        if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 3)
                {
                    // 3: Tenkan/Kijun state
                    if (Kucuk(currentIndex, tenkan, kijun)) sell = true;
                }
                else if (sellSignalModeIndex == 4)
                {
                    // 4: Band / uzaklık filtresi
                    const double bandThreshold = 0.01; // %1
                    if (currentKijun != 0.0)
                    {
                        double distanceRatio = (currentTenkan - currentKijun) / currentKijun;
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

                        if (!sell && AsagiKesti(m, tenkan, kijun)
                            && barHigh >= currentKijun
                            && currentTenkan < currentKijun)
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

                        bool stayedBelow = AsagiKesti(crossBar, tenkan, kijun);
                        for (int m = crossBar + 1; m <= currentIndex; m++)
                        {
                            stayedBelow &= tenkan[m] < kijun[m];
                        }
                        if (stayedBelow) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 7)
                {
                    // 7: Tenkan eğimi + state combo
                    const int slopeLookback = 3;
                    if (currentIndex >= slopeLookback)
                    {
                        bool tenkanFalling = tenkan[currentIndex] < tenkan[currentIndex - slopeLookback];
                        if (Kucuk(currentIndex, tenkan, kijun) && tenkanFalling) sell = true;
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

            if (flatModeEnabled)
            {
                if (flatModeIndex == 0) flat = false;
            }

            if (skipModeEnabled)
            {
                if (skipModeIndex == 0) skip = false;
            }

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

        public double[]? GetTenkan() => tenkan;
        public double[]? GetKijun() => kijun;
        public double[]? GetSenkouA() => senkouA;
        public double[]? GetSenkouB() => senkouB;

        // IsFirstBarOfDay/IsLastBarOfDay/IsFirstBarOfWeek/IsFirstBarOfMonth artik BaseStrategy'de
        // (protected) - burada tekrar tanimlanmaz.

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            if (tenkan != null && tenkan.Length > 0) indicators["Tenkan"] = tenkan;
            if (kijun != null && kijun.Length > 0) indicators["Kijun"] = kijun;
            if (senkouA != null && senkouA.Length > 0) indicators["SenkouA"] = senkouA;
            if (senkouB != null && senkouB.Length > 0) indicators["SenkouB"] = senkouB;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
