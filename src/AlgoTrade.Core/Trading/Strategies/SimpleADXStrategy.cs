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
    /// ADX (Average Directional Index) Stratejisi
    ///
    /// ADX Mantığı:
    /// - ADX: Trend gücünü ölçer (yön değil), +DI/-DI: yönlü hareket gücü
    /// - +DI/-DI çifti MOST'un most/exmov çiftinin analogu; ADX ise tüm modları filtreleyen
    ///   bir trend-gücü eşiği (bu, ADX Strategy'yi filtresiz DI Strategy'den ayıran özellik)
    /// - Fiyat/priceSource kavramı yok - +DI/-DI High/Low'a bağımlı (SuperTrend/SAR gibi)
    ///
    /// Parametreler:
    /// - period: ADX/DI periyodu (varsayılan 14)
    /// - adxThreshold: Minimum ADX değeri - trend gücü filtresi (varsayılan 25)
    /// - signalModeIndex: buy/sell yöntemini seçer (hepsi ADX>adxThreshold ile filtrelenir):
    ///     0: +DI/-DI kesişimi          (+DI, -DI'yı yukarı/aşağı kesince)
    ///     1: +DI/-DI state             (konum - kesişim değil, koşul sürdükçe her bar)
    ///     2: ADX slope flip            (ADX'in kendi yönü dönünce - trend gücü artışa/azalışa geçince)
    ///     3: ADX-DI kombine state      (ADX>threshold VE +DI>-DI ise AL, ADX>threshold VE -DI>+DI ise SAT, her bar)
    ///     4: Band / uzaklık filtresi   (+DI ile -DI arasındaki fark %bandThreshold'dan fazla açılınca)
    ///     5: Breakout + retest         (DI kesişip ADX geriden threshold'u geçince)
    ///     6: Confirmation bars         (kesişimden sonra confirmBars bar aynı tarafta kalınca)
    ///     7: ADX eğimi + DI state      (rejim: DI state + momentum: ADX N-bar eğimi)
    /// - exitModeIndex: takeProfit/stopLoss yöntemini seçer (Trader.karAlZararKes üzerinden):
    ///     0: Seviye, seviyeli               (SonFiyataGoreKarAl/ZararKesSeviyeHesaplaSeviyeli)
    ///     1: Yüzde, seviyeli                 (SonFiyataGoreKarAl/ZararKesYuzdeHesaplaSeviyeli)
    ///     2: Seviye, tek seviye              (SonFiyataGoreKarAl/ZararKesSeviyeHesapla)
    ///     3: Yüzde, tek seviye               (SonFiyataGoreKarAl/ZararKesYuzdeHesapla)
    ///     4: Anlık kar/zarar fiyat seviyesi  (KarZararFiyatSeviyesindenKarAl/ZararKesHesapla)
    ///     5: Anlık kar/zarar yüzdesi         (KarZararYuzdesindenKarAl/ZararKesHesapla)
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
    public class SimpleADXStrategy : BaseStrategy
    {
        public override string Name => "Simple ADX Strategy";

        // barCount/openPrices/.../epochTimes artik BaseStrategy'de (protected) - LoadCommonSeries()
        // tarafindan Initialize() icinde OnInit()'ten once doldurulur, burada tekrar tanimlanmaz.

        private readonly int period;
        private readonly double adxThreshold;

        // signalModeIndex/exitModeIndex/flatModeIndex/skipModeIndex/ruleModeIndex artik BaseStrategy'de
        // tanimli (protected, readonly degil) - degerleri asagida constructor'da parametre olarak atanir.
        // signalModeIndex'in dispatch mantigi (OnStep'teki if/else zinciri) stratejiye ozgu, burada kalir.

        // startTime/stopTime/startDay/stopDay/isTimeEnabled/isDayEnabled/triggerTime/isTriggerTimeEnabled
        // artik BaseStrategy'de tanimli (protected, readonly degil) - degerleri asagida constructor'da atanir.

        private double[]? adx;
        private double[]? plusDI;
        private double[]? minusDI;

        // runContextResolved/timeframeMinutes/isOptimizationRun/isOneMinute.../ResolveRunContext() artik
        // BaseStrategy'de (protected) - burada tekrar tanimlanmaz.

        public SimpleADXStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 14, double adxThreshold = 25,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period          = period;
            this.adxThreshold    = adxThreshold;
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
            Parameters["AdxThreshold"]         = adxThreshold;
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

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            // barCount/openPrices/.../epochTimes BaseStrategy.LoadCommonSeries() tarafindan bu
            // noktada zaten dolu (Initialize() icinde OnInit()'ten once cagrildi).
            var adxResult = Indicators.Trend.ADXWithDI(period);
            adx     = adxResult.ADX;
            plusDI  = adxResult.PlusDI;
            minusDI = adxResult.MinusDI;

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= adx.Length     == barCount;
            allSeriesLengthsMatch &= plusDI.Length  == barCount;
            allSeriesLengthsMatch &= minusDI.Length == barCount;

            if (!allSeriesLengthsMatch)
            {
                throw new InvalidOperationException(
                    $"Seri uzunlukları uyuşmuyor (barCount={barCount}): " +
                    $"adx={adx.Length}, plusDI={plusDI.Length}, minusDI={minusDI.Length}");
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

            if (currentIndex < period * 2 + 1)
                return TradeSignals.None;

            if (adx == null || adx.Length == 0 || plusDI == null || minusDI == null)
                return TradeSignals.None;

            double currentADX = adx[currentIndex];
            double currentPlusDI = plusDI[currentIndex];
            double currentMinusDI = minusDI[currentIndex];

            if (double.IsNaN(currentADX) || double.IsNaN(currentPlusDI) || double.IsNaN(currentMinusDI))
                return TradeSignals.None;

            bool strongTrend = currentADX > adxThreshold;

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

            if (signalModeIndex == 0)
            {
                // 0: +DI/-DI kesişimi + ADX>threshold filtresi (klasik)
                if (YukarıKesti(currentIndex, plusDI, minusDI) && strongTrend) buy  = true;
                if (AsagiKesti(currentIndex, plusDI, minusDI) && strongTrend)  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: +DI/-DI state - filtresiz, koşul sürdükçe her bar
                if (Buyuk(currentIndex, plusDI, minusDI)) buy  = true;
                if (Kucuk(currentIndex, plusDI, minusDI)) sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: ADX slope flip - trend gücü artışa/azalışa geçiyor, yön DI'dan
                if (currentIndex >= 2)
                {
                    double slopeNow  = adx[currentIndex]     - adx[currentIndex - 1];
                    double slopePrev = adx[currentIndex - 1] - adx[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0 && currentPlusDI > currentMinusDI) buy  = true;
                    if (slopePrev <= 0.0 && slopeNow > 0.0 && currentMinusDI > currentPlusDI) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: ADX-DI kombine state - her bar, ADX filtreli konum
                if (strongTrend && currentPlusDI > currentMinusDI) buy  = true;
                if (strongTrend && currentMinusDI > currentPlusDI) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi - +DI ile -DI farkı bandThreshold'dan fazla açılınca
                const double bandThreshold = 5.0; // DI puanı
                double diDiff = currentPlusDI - currentMinusDI;
                if (diDiff >  bandThreshold) buy  = true;
                if (diDiff < -bandThreshold) sell = true;
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest - DI kesişti, sonradan ADX threshold'u geçince onaylanır
                const int retestLookback = 10;
                for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                {
                    if (k < 1) continue;

                    if (!buy && YukarıKesti(k, plusDI, minusDI) && strongTrend && currentPlusDI > currentMinusDI)
                        buy = true;

                    if (!sell && AsagiKesti(k, plusDI, minusDI) && strongTrend && currentMinusDI > currentPlusDI)
                        sell = true;
                }
            }
            else if (signalModeIndex == 6)
            {
                // 6: Confirmation bars - kesişimden confirmBars sonra hâlâ aynı yönde VE ADX güçlü
                const int confirmBars = 3;
                if (currentIndex >= confirmBars + 1)
                {
                    int crossBar = currentIndex - confirmBars;

                    bool stayedAbove = YukarıKesti(crossBar, plusDI, minusDI);
                    bool stayedBelow = AsagiKesti(crossBar, plusDI, minusDI);
                    for (int k = crossBar + 1; k <= currentIndex; k++)
                    {
                        stayedAbove &= plusDI[k] > minusDI[k];
                        stayedBelow &= plusDI[k] < minusDI[k];
                    }
                    if (stayedAbove && strongTrend) buy  = true;
                    if (stayedBelow && strongTrend) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: ADX eğimi + DI state - rejim (DI konumu) + momentum (ADX N-bar eğimi)
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool adxRising = adx[currentIndex] > adx[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, plusDI, minusDI) && adxRising) buy  = true;
                    if (Kucuk(currentIndex, plusDI, minusDI) && adxRising) sell = true;
                }
            }

            if (1 == 1 && Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesaplaSeviyeli(currentIndex, 2, 10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesapla(currentIndex, 2000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesapla(currentIndex, 2.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(currentIndex, 1000.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
                    takeProfit = Trader.karAlZararKes.KarZararYuzdesindenKarAlHesapla(currentIndex, 3.0) != 0;
                }
            }

            if (1 == 1 && Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesaplaSeviyeli(currentIndex, -2, -10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesapla(currentIndex, -1000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesapla(currentIndex, -1.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    stopLoss = Trader.karAlZararKes.KarZararFiyatSeviyesindenZararKesHesapla(currentIndex, -500.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
                    stopLoss = Trader.karAlZararKes.KarZararYuzdesindenZararKesHesapla(currentIndex, -2.0) != 0;
                }
            }

            if (flatModeIndex == 0) flat = false;
            if (skipModeIndex == 0) skip = false;

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

        public double[]? GetADX() => adx;
        public double[]? GetPlusDI() => plusDI;
        public double[]? GetMinusDI() => minusDI;

        // IsFirstBarOfDay/IsLastBarOfDay/IsFirstBarOfWeek/IsFirstBarOfMonth artik BaseStrategy'de
        // (protected) - burada tekrar tanimlanmaz.

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            if (adx != null && adx.Length > 0) indicators["ADX"] = adx;
            if (plusDI != null && plusDI.Length > 0) indicators["+DI"] = plusDI;
            if (minusDI != null && minusDI.Length > 0) indicators["-DI"] = minusDI;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
