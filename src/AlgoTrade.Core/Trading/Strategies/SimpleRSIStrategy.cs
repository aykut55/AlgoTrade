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
    /// RSI (Relative Strength Index) Stratejisi
    ///
    /// RSI Mantığı:
    /// - 0-100 arası momentum osilatörü
    /// - 70 üstü: Aşırı alım, 30 altı: Aşırı satım
    ///
    /// Parametreler:
    /// - period: RSI periyodu (varsayılan 14)
    /// - oversold: Aşırı satım seviyesi (varsayılan 30)
    /// - overbought: Aşırı alım seviyesi (varsayılan 70)
    /// - priceSource: RSI'ın beslendiği kaynak + OnStep sinyal serisi (varsayılan Close - klasik RSI)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: Oversold/Overbought kesişimi (fiyat MOST'un fiyat-MOST kesişiminin analogu)
    ///     1: Orta hat (50) kesişimi     (RSI 50'yi yukarı/aşağı kesince)
    ///     2: RSI slope flip             (RSI'ın kendi yönü dönünce)
    ///     3: RSI state                  (RSI'ın 50'ye göre konumu - kesişim değil, koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi    (RSI 50'den bandThreshold puandan fazla uzaklaşınca)
    ///     5: Breakout + retest          (RSI oversold/overbought kırılıp geri gelip retest tutunca)
    ///     6: Confirmation bars          (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: RSI eğimi + RSI state      (rejim: RSI-50 konumu + momentum: RSI N-bar eğimi)
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
    public class SimpleRSIStrategy : BaseStrategy
    {
        public override string Name => "Simple RSI Strategy";

        // barCount/openPrices/.../epochTimes artik BaseStrategy'de (protected) - LoadCommonSeries()
        // tarafindan Initialize() icinde OnInit()'ten once doldurulur, burada tekrar tanimlanmaz.

        private readonly int period;
        private readonly double oversold;
        private readonly double overbought;

        // signalModeIndex/exitModeIndex/flatModeIndex/skipModeIndex/ruleModeIndex artik BaseStrategy'de
        // tanimli (protected, readonly degil) - degerleri asagida constructor'da parametre olarak atanir.
        // signalModeIndex'in dispatch mantigi (OnStep'teki if/else zinciri) stratejiye ozgu, burada kalir.

        // RSI hesabı - parametreli ctor'dan gelir; verilmezse Close (klasik RSI ile birebir aynı).
        private readonly PriceSource priceSource = PriceSource.Close;

        // startTime/stopTime/startDay/stopDay/isTimeEnabled/isDayEnabled/triggerTime/isTriggerTimeEnabled
        // artik BaseStrategy'de tanimli (protected, readonly degil) - degerleri asagida constructor'da atanir.

        private double[]? source;   // priceSource'un çözülmüş hali - OnInit'te bir kez, OnStep bundan okur
        private double[]? rsi;

        // runContextResolved/timeframeMinutes/isOptimizationRun/isOneMinute.../ResolveRunContext() artik
        // BaseStrategy'de (protected) - burada tekrar tanimlanmaz.

        // Parametreli constructor (data/indicators gerekli — parametresiz ctor kaldırıldı, hiç kullanılmıyordu)
        public SimpleRSIStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 14, double oversold = 30, double overbought = 70, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period          = period;
            this.oversold        = oversold;
            this.overbought      = overbought;
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
            Parameters["Oversold"]             = oversold;
            Parameters["Overbought"]           = overbought;
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

            // RSI'ı hesapla (priceSource ile parametrik)
            rsi = Indicators.Momentum.RSI(source, period).Values;

            // rsi/source OnStep'te barCount ile ayni index'te okunuyor - uzunluklari uyusmazsa
            // (or. indikator filtrelenmis/kirpilmis dondurdu) IndexOutOfRange yerine burada net hata ver
            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= rsi.Length    == barCount;
            allSeriesLengthsMatch &= source.Length == barCount;

            if (!allSeriesLengthsMatch)
            {
                throw new InvalidOperationException(
                    $"Seri uzunlukları uyuşmuyor (barCount={barCount}): rsi={rsi.Length}, source={source.Length}");
            }

            //Log($"SimpleRSIStrategy initialized: Period={period}, Oversold={oversold}, Overbought={overbought}, SignalModeIndex={signalModeIndex}");
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

            // İlk barlarda yeterli veri yok (RSI period+1'e kadar NaN döner)
            if (currentIndex < period + 1)
                return TradeSignals.None;

            // OnInit'te seri boş kalmışsa sinyal üretme
            if (rsi == null || rsi.Length == 0)
                return TradeSignals.None;

            if (source == null || source.Length == 0)
                return TradeSignals.None;
            // ************************************************************************************************************************

            // Geçerli ve önceki değerler (source = OnInit'te priceSource'tan çözülen seri)
            double currentPrice = source[currentIndex];
            double prevPrice    = source[currentIndex - 1];
            double currentRSI   = rsi[currentIndex];
            double prevRSI      = rsi[currentIndex - 1];

            // RSI ilk period bar boyunca NaN döner - erken çık
            if (double.IsNaN(currentRSI) || double.IsNaN(prevRSI))
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

            // signalModeIndex ile buy/sell yöntemi seçilir - detay için sınıf başı doc comment (0-7)
            if (signalModeIndex == 0)
            {
                // 0: Oversold/Overbought kesişimi - RSI oversold'u yukarı kesince AL, overbought'u aşağı kesince SAT
                if (YukarıKesti(currentIndex, rsi, oversold))   buy  = true;
                if (AsagiKesti(currentIndex, rsi, overbought))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: Orta hat (50) kesişimi - RSI 50'yi yukarı kesince AL, aşağı kesince SAT
                const double midline = 50.0;
                if (YukarıKesti(currentIndex, rsi, midline)) buy  = true;
                if (AsagiKesti(currentIndex, rsi, midline))  sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: RSI slope flip - RSI'ın kendi yönü dönüyor (düşen/düz → yükselen = AL)
                if (currentIndex >= 2)
                {
                    double slopeNow  = rsi[currentIndex]     - rsi[currentIndex - 1];
                    double slopePrev = rsi[currentIndex - 1] - rsi[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: RSI state - RSI'ın 50'ye göre konumu (kesişim değil, koşul sürdükçe her bar)
                const double midline = 50.0;
                if (Buyuk(currentIndex, rsi, midline)) buy  = true;
                if (Kucuk(currentIndex, rsi, midline)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi - RSI 50'den bandThreshold puandan fazla uzaklaşınca (trend-following)
                const double midline       = 50.0;
                const double bandThreshold = 20.0; // RSI puanı (50±20 => 70/30 seviyeleri)
                double distance = currentRSI - midline;
                if (distance >  bandThreshold) buy  = true;
                if (distance < -bandThreshold) sell = true;
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest - son retestLookback bar içinde RSI oversold/overbought'u kırdı, şimdi RSI
                //    seviyeye geri yaklaşıp (retest) kırılım yönünde tuttuysa → sinyal
                const int retestLookback = 10;
                const double retestBand  = 2.0; // RSI puanı toleransı

                for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                {
                    if (k < 1) continue;

                    if (!buy && YukarıKesti(k, rsi, oversold)
                        && currentRSI <= oversold + retestBand   // RSI oversold'a geri yaklaştı (retest)
                        && currentRSI > oversold)                // ama üstünde tuttu (retest tuttu)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(k, rsi, overbought)
                        && currentRSI >= overbought - retestBand
                        && currentRSI < overbought)
                    {
                        sell = true;
                    }
                }
            }
            else if (signalModeIndex == 6)
            {
                // 6: Confirmation bars - kırılım confirmBars bar önce oldu ve o zamandan beri RSI
                //    hep seviyenin aynı tarafında kaldıysa gir
                const int confirmBars = 3;
                if (currentIndex >= confirmBars + 1)
                {
                    int crossBar = currentIndex - confirmBars;

                    bool stayedAbove = YukarıKesti(crossBar, rsi, oversold);
                    bool stayedBelow = AsagiKesti(crossBar, rsi, overbought);
                    for (int k = crossBar + 1; k <= currentIndex; k++)
                    {
                        stayedAbove &= rsi[k] > oversold;
                        stayedBelow &= rsi[k] < overbought;
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: RSI eğimi + RSI state - rejim (RSI-50 konumu) + momentum (RSI N-bar eğimi)
                const int slopeLookback = 3;
                const double midline     = 50.0;
                if (currentIndex >= slopeLookback)
                {
                    bool rsiRising  = rsi[currentIndex] > rsi[currentIndex - slopeLookback];
                    bool rsiFalling = rsi[currentIndex] < rsi[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, rsi, midline) && rsiRising)  buy  = true;
                    if (Kucuk(currentIndex, rsi, midline) && rsiFalling) sell = true;
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
        /// RSI değerlerini al (plotting veya analiz için)
        /// </summary>
        public double[]? GetRSI() => rsi;

        /// <summary>
        /// Period parametresini al
        /// </summary>
        public int Period => period;

        /// <summary>
        /// Oversold parametresini al
        /// </summary>
        public double Oversold => oversold;

        /// <summary>
        /// Overbought parametresini al
        /// </summary>
        public double Overbought => overbought;

        // IsFirstBarOfDay/IsLastBarOfDay/IsFirstBarOfWeek/IsFirstBarOfMonth artik BaseStrategy'de
        // (protected) - burada tekrar tanimlanmaz.

        /// <summary>
        /// Get indicators for plotting (IStrategy implementation)
        /// </summary>
        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (rsi != null && rsi.Length > 0)
                indicators[$"RSI ({period})"] = rsi;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
