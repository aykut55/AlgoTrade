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
    /// Bollinger Bands Stratejisi
    ///
    /// Bollinger Mantığı:
    /// - Middle Band: SMA, Upper/Lower: Middle ± (StdDev * multiplier)
    /// - Upper band MOST'un most'unun analogu (fiyat kırılımı), Middle bant ikinci referans çizgi
    ///
    /// Parametreler:
    /// - period: BB periyodu (varsayılan 20)
    /// - multiplier: StdDev çarpanı (varsayılan 2.0)
    /// - priceSource: BB'nin beslendiği kaynak (varsayılan Close - klasik Bollinger)
    /// - buySignalModeIndex/sellSignalModeIndex: buy ve sell yöntemini AYRI AYRI seçer (asymmetric -
    ///   buy başka bir moddan, sell başka bir moddan gelebilir). Her ikisi de aynı mod kümesinden seçilir:
    ///     0: Üst/alt bant kırılımı      (fiyat üst bandı yukarı / alt bandı aşağı kesince)
    ///     1: Orta bant (MA) kesişimi     (fiyat orta bandı yukarı/aşağı kesince)
    ///     2: Üst bant slope flip         (üst bandın kendi yönü dönünce - volatilite rejimi)
    ///     3: Bant state                  (fiyat üst/alt bandın dışında - koşul sürdükçe her bar)
    ///     4: Bant genişliği filtresi     (üst-alt bant farkı %bandWidthThreshold'dan fazla açılınca)
    ///     5: Breakout + retest           (üst/alt bant kırılıp fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars           (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: Fiyat eğimi + bant state    (rejim: bant state + momentum: fiyatın N-bar eğimi)
    /// - takeProfitExitModeIndex/stopLossExitModeIndex: takeProfit/stopLoss yöntemini AYRI AYRI seçer
    ///   (Trader.karAlZararKes üzerinden), her ikisi de aynı mod kümesinden seçilir:
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
    public class SimpleBollingerStrategy : BaseStrategy
    {
        public override string Name => "Simple Bollinger Strategy";

        // barCount/openPrices/.../epochTimes artik BaseStrategy'de (protected) - LoadCommonSeries()
        // tarafindan Initialize() icinde OnInit()'ten once doldurulur, burada tekrar tanimlanmaz.

        private readonly int period;
        private readonly double multiplier;

        // buySignalModeIndex/sellSignalModeIndex/takeProfitExitModeIndex/stopLossExitModeIndex/flatModeIndex/skipModeIndex/ruleModeIndex artik BaseStrategy'de
        // tanimli (protected, readonly degil) - degerleri asagida constructor'da parametre olarak atanir.
        // buySignalModeIndex/sellSignalModeIndex'in dispatch mantigi (OnStep'teki if/else zincirleri) stratejiye ozgu, burada kalir.

        private readonly PriceSource priceSource = PriceSource.Close;

        // startTime/stopTime/startDay/stopDay/isTimeEnabled/isDayEnabled/triggerTime/isTriggerTimeEnabled
        // artik BaseStrategy'de tanimli (protected, readonly degil) - degerleri asagida constructor'da atanir.

        private double[]? source;
        private double[]? upper;
        private double[]? middle;
        private double[]? lower;

        // runContextResolved/timeframeMinutes/isOptimizationRun/isOneMinute.../ResolveRunContext() artik
        // BaseStrategy'de (protected) - burada tekrar tanimlanmaz.

        public SimpleBollingerStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 20, double multiplier = 2.0, PriceSource priceSource = PriceSource.Close,
            int buySignalModeIndex = 0, int sellSignalModeIndex = 0, int takeProfitExitModeIndex = 0, int stopLossExitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period                   = period;
            this.multiplier               = multiplier;
            this.priceSource              = priceSource;
            this.buySignalModeIndex       = buySignalModeIndex;
            this.sellSignalModeIndex      = sellSignalModeIndex;
            this.takeProfitExitModeIndex  = takeProfitExitModeIndex;
            this.stopLossExitModeIndex    = stopLossExitModeIndex;
            this.flatModeIndex            = flatModeIndex;
            this.skipModeIndex            = skipModeIndex;
            this.ruleModeIndex            = ruleModeIndex;

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

            // BaseStrategy'deki varsayilan degerler true - bu stratejide takeProfit/stopLoss zaten
            // kosulsuz aktifti (eski "if (1 == 1 && Trader != null)" guard'i), acikca true set edilir.
            takeProfitExitModeEnabled = true;
            stopLossExitModeEnabled   = true;

            Parameters["Period"]                  = period;
            Parameters["Multiplier"]              = multiplier;
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

            var bbResult = Indicators.Volatility.BollingerBands(source, period, multiplier);
            upper  = bbResult.Upper;
            middle = bbResult.Middle;
            lower  = bbResult.Lower;

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= upper.Length  == barCount;
            allSeriesLengthsMatch &= middle.Length == barCount;
            allSeriesLengthsMatch &= lower.Length  == barCount;
            allSeriesLengthsMatch &= source.Length == barCount;

            if (!allSeriesLengthsMatch)
            {
                throw new InvalidOperationException(
                    $"Seri uzunlukları uyuşmuyor (barCount={barCount}): " +
                    $"upper={upper.Length}, middle={middle.Length}, lower={lower.Length}, source={source.Length}");
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

            if (currentIndex < period + 1)
                return TradeSignals.None;

            if (upper == null || middle == null || lower == null || source == null || upper.Length == 0)
                return TradeSignals.None;

            double currentPrice = source[currentIndex];
            double currentUpper = upper[currentIndex];
            double currentLower = lower[currentIndex];

            if (double.IsNaN(currentUpper) || double.IsNaN(currentLower))
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
                    // 0: Üst bant kırılımı (klasik)
                    if (YukarıKesti(currentIndex, source, upper)) buy = true;
                }
                else if (buySignalModeIndex == 1)
                {
                    // 1: Orta bant (MA) kesişimi
                    if (YukarıKesti(currentIndex, source, middle)) buy = true;
                }
                else if (buySignalModeIndex == 2)
                {
                    // 2: Üst bant slope flip - volatilite rejimi
                    if (currentIndex >= 2)
                    {
                        double slopeNow  = upper[currentIndex]     - upper[currentIndex - 1];
                        double slopePrev = upper[currentIndex - 1] - upper[currentIndex - 2];
                        if (slopePrev <= 0.0 && slopeNow > 0.0) buy = true;
                    }
                }
                else if (buySignalModeIndex == 3)
                {
                    // 3: Bant state - fiyat üst bandın dışında, koşul sürdükçe her bar
                    if (Buyuk(currentIndex, source, upper)) buy = true;
                }
                else if (buySignalModeIndex == 4)
                {
                    // 4: Bant genişliği filtresi - üst-alt fark %bandWidthThreshold'dan fazla açılınca
                    const double bandWidthThreshold = 0.04; // %4 (fiyata oranla)
                    double width = (currentUpper - currentLower) / currentPrice;
                    if (width > bandWidthThreshold && currentPrice > middle[currentIndex]) buy = true;
                }
                else if (buySignalModeIndex == 5)
                {
                    // 5: Breakout + retest
                    const int retestLookback = 10;
                    double barLow = Data[currentIndex].Low;

                    for (int m = currentIndex - retestLookback; m < currentIndex; m++)
                    {
                        if (m < 1) continue;

                        if (!buy && YukarıKesti(m, source, upper)
                            && barLow <= currentUpper
                            && currentPrice > currentUpper)
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

                        bool stayedAbove = YukarıKesti(crossBar, source, upper);
                        for (int m = crossBar + 1; m <= currentIndex; m++)
                        {
                            stayedAbove &= source[m] > upper[m];
                        }
                        if (stayedAbove) buy = true;
                    }
                }
                else if (buySignalModeIndex == 7)
                {
                    // 7: Fiyat eğimi + bant state
                    const int slopeLookback = 3;
                    if (currentIndex >= slopeLookback)
                    {
                        bool priceRising = source[currentIndex] > source[currentIndex - slopeLookback];
                        if (Buyuk(currentIndex, source, upper) && priceRising) buy = true;
                    }
                }
            }

            if (sellModeEnabled)
            {
                if (sellSignalModeIndex == 0)
                {
                    // 0: Alt bant kırılımı (klasik)
                    if (AsagiKesti(currentIndex, source, lower)) sell = true;
                }
                else if (sellSignalModeIndex == 1)
                {
                    // 1: Orta bant (MA) kesişimi
                    if (AsagiKesti(currentIndex, source, middle)) sell = true;
                }
                else if (sellSignalModeIndex == 2)
                {
                    // 2: Üst bant slope flip - volatilite rejimi
                    if (currentIndex >= 2)
                    {
                        double slopeNow  = upper[currentIndex]     - upper[currentIndex - 1];
                        double slopePrev = upper[currentIndex - 1] - upper[currentIndex - 2];
                        if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 3)
                {
                    // 3: Bant state - fiyat alt bandın dışında, koşul sürdükçe her bar
                    if (Kucuk(currentIndex, source, lower)) sell = true;
                }
                else if (sellSignalModeIndex == 4)
                {
                    // 4: Bant genişliği filtresi - üst-alt fark %bandWidthThreshold'dan fazla açılınca
                    const double bandWidthThreshold = 0.04; // %4 (fiyata oranla)
                    double width = (currentUpper - currentLower) / currentPrice;
                    if (width > bandWidthThreshold && currentPrice < middle[currentIndex]) sell = true;
                }
                else if (sellSignalModeIndex == 5)
                {
                    // 5: Breakout + retest
                    const int retestLookback = 10;
                    double barHigh = Data[currentIndex].High;

                    for (int m = currentIndex - retestLookback; m < currentIndex; m++)
                    {
                        if (m < 1) continue;

                        if (!sell && AsagiKesti(m, source, lower)
                            && barHigh >= currentLower
                            && currentPrice < currentLower)
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

                        bool stayedBelow = AsagiKesti(crossBar, source, lower);
                        for (int m = crossBar + 1; m <= currentIndex; m++)
                        {
                            stayedBelow &= source[m] < lower[m];
                        }
                        if (stayedBelow) sell = true;
                    }
                }
                else if (sellSignalModeIndex == 7)
                {
                    // 7: Fiyat eğimi + bant state
                    const int slopeLookback = 3;
                    if (currentIndex >= slopeLookback)
                    {
                        bool priceFalling = source[currentIndex] < source[currentIndex - slopeLookback];
                        if (Kucuk(currentIndex, source, lower) && priceFalling) sell = true;
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

        public double[]? GetUpper() => upper;
        public double[]? GetMiddle() => middle;
        public double[]? GetLower() => lower;

        // IsFirstBarOfDay/IsLastBarOfDay/IsFirstBarOfWeek/IsFirstBarOfMonth artik BaseStrategy'de
        // (protected) - burada tekrar tanimlanmaz.

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            if (upper != null && upper.Length > 0) indicators["BB_Upper"] = upper;
            if (middle != null && middle.Length > 0) indicators["BB_Middle"] = middle;
            if (lower != null && lower.Length > 0) indicators["BB_Lower"] = lower;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
