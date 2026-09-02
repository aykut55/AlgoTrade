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
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: Üst/alt bant kırılımı      (fiyat üst bandı yukarı / alt bandı aşağı kesince)
    ///     1: Orta bant (MA) kesişimi     (fiyat orta bandı yukarı/aşağı kesince)
    ///     2: Üst bant slope flip         (üst bandın kendi yönü dönünce - volatilite rejimi)
    ///     3: Bant state                  (fiyat üst/alt bandın dışında - koşul sürdükçe her bar)
    ///     4: Bant genişliği filtresi     (üst-alt bant farkı %bandWidthThreshold'dan fazla açılınca)
    ///     5: Breakout + retest           (üst/alt bant kırılıp fiyat geri gelip retest tutunca)
    ///     6: Confirmation bars           (kırılımdan sonra confirmBars bar aynı tarafta kalınca)
    ///     7: Fiyat eğimi + bant state    (rejim: bant state + momentum: fiyatın N-bar eğimi)
    /// - exitModeIndex: takeProfit/stopLoss yöntemini seçer (Trader.karAlZararKes üzerinden):
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

        private int barCount;
        private double[]? openPrices;
        private double[]? highPrices;
        private double[]? lowPrices;
        private double[]? closePrices;
        private long[]? volumes;
        private long[]? lotSizes;
        private DateTime[]? dateTimes;
        private DateOnly[]? dates;
        private TimeOnly[]? times;
        private long[]? epochTimes;

        private readonly int period;
        private readonly double multiplier;
        private readonly int signalModeIndex;
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        private readonly PriceSource priceSource = PriceSource.Close;

        private double[]? source;
        private double[]? upper;
        private double[]? middle;
        private double[]? lower;
        // Run baglami - ILK OnStep cagrisinda Trader'dan cozulur (OnInit'te DEGIL: OnInit
        // constructor'dan calisir, SetTrader() daha sonra SetStrategy() icinde -> OnInit'te Trader null).
        private bool runContextResolved;
        private int  timeframeMinutes;    // 1, 5, 15, 60, 240 ... (0 = SymbolPeriod cozulemedi)
        private bool isOptimizationRun;   // true = opt taramasi icinde (Trader.OptimizationEnabled), false = tekli kosu

        // timeframeMinutes'in türevi - run boyunca degismez, ResolveRunContext'te bir kez set edilir
        private bool isOneMinute, isFiveMinute, isOneHour, isFourHour, isOneDay;


        public SimpleBollingerStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 20, double multiplier = 2.0, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period          = period;
            this.multiplier      = multiplier;
            this.priceSource     = priceSource;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Period"]         = period;
            Parameters["Multiplier"]     = multiplier;
            Parameters["PriceSource"]    = priceSource;
            Parameters["RuleModeIndex"]  = ruleModeIndex;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"]  = exitModeIndex;
            Parameters["FlatModeIndex"]  = flatModeIndex;
            Parameters["SkipModeIndex"]  = skipModeIndex;

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            barCount    = Indicators.GetDataCount();
            openPrices  = Indicators.GetOpenPrices();
            highPrices  = Indicators.GetHighPrices();
            lowPrices   = Indicators.GetLowPrices();
            closePrices = Indicators.GetClosePrices();
            volumes     = Indicators.GetVolume();
            lotSizes    = Indicators.GetLotSizes();
            dateTimes   = Indicators.GetDateTimes();
            dates       = Indicators.GetDates();
            times       = Indicators.GetTimes();
            epochTimes  = Indicators.GetEpochTimes();
            source      = Indicators.Trend.ResolvePriceSource(priceSource);

            var bbResult = Indicators.Volatility.BollingerBands(source, period, multiplier);
            upper  = bbResult.Upper;
            middle = bbResult.Middle;
            lower  = bbResult.Lower;

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= upper.Length       == barCount;
            allSeriesLengthsMatch &= middle.Length      == barCount;
            allSeriesLengthsMatch &= lower.Length       == barCount;
            allSeriesLengthsMatch &= source.Length      == barCount;
            allSeriesLengthsMatch &= openPrices.Length  == barCount;
            allSeriesLengthsMatch &= highPrices.Length  == barCount;
            allSeriesLengthsMatch &= lowPrices.Length   == barCount;
            allSeriesLengthsMatch &= closePrices.Length == barCount;
            allSeriesLengthsMatch &= volumes.Length     == barCount;
            allSeriesLengthsMatch &= lotSizes.Length    == barCount;
            allSeriesLengthsMatch &= dateTimes.Length   == barCount;
            allSeriesLengthsMatch &= dates.Length       == barCount;
            allSeriesLengthsMatch &= times.Length       == barCount;
            allSeriesLengthsMatch &= epochTimes.Length  == barCount;

            if (!allSeriesLengthsMatch)
            {
                throw new InvalidOperationException(
                    $"Seri uzunlukları uyuşmuyor (barCount={barCount}): " +
                    $"upper={upper.Length}, middle={middle.Length}, lower={lower.Length}, source={source.Length}, open={openPrices.Length}, " +
                    $"high={highPrices.Length}, low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                    $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            ResolveRunContext();

            bool buy = false, sell = false, takeProfit = false, stopLoss = false, flat = false, skip = false;

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
            // ************************************************************************************************************************

            // IsFirstBarOfDay/IsLastBarOfDay/IsFirstBarOfWeek/IsFirstBarOfMonth - henüz sinyal
            // mantığına dahil değil, kullanılmasa da her bar hesaplanıp açık/hazır tutuluyor.
            bool isFirstOfDay   = IsFirstBarOfDay(currentIndex);
            bool isLastOfDay    = IsLastBarOfDay(currentIndex);
            bool isFirstOfWeek  = IsFirstBarOfWeek(currentIndex);
            bool isFirstOfMonth = IsFirstBarOfMonth(currentIndex);

            if (isFirstOfDay)
            {
                // örn. gün başında önceki günden kalan pozisyonu flatle, ya da günlük bir
                // sayaç/limit'i sıfırla (GunSonuPozKapatEnabled'a benzer ama period-independent)
            }
            if (isLastOfDay)
            {
                // örn. gün sonuna gelmeden pozisyonu kapat (gün-içi strateji için)
            }
            // ************************************************************************************************************************

            if (signalModeIndex == 0)
            {
                // 0: Üst/alt bant kırılımı (klasik)
                if (YukarıKesti(currentIndex, source, upper)) buy  = true;
                if (AsagiKesti(currentIndex, source, lower))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: Orta bant (MA) kesişimi
                if (YukarıKesti(currentIndex, source, middle)) buy  = true;
                if (AsagiKesti(currentIndex, source, middle))  sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: Üst bant slope flip - volatilite rejimi
                if (currentIndex >= 2)
                {
                    double slopeNow  = upper[currentIndex]     - upper[currentIndex - 1];
                    double slopePrev = upper[currentIndex - 1] - upper[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: Bant state - fiyat üst/alt bandın dışında, koşul sürdükçe her bar
                if (Buyuk(currentIndex, source, upper)) buy  = true;
                if (Kucuk(currentIndex, source, lower)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Bant genişliği filtresi - üst-alt fark %bandWidthThreshold'dan fazla açılınca
                const double bandWidthThreshold = 0.04; // %4 (fiyata oranla)
                double width = (currentUpper - currentLower) / currentPrice;
                if (width > bandWidthThreshold && currentPrice > middle[currentIndex]) buy  = true;
                if (width > bandWidthThreshold && currentPrice < middle[currentIndex]) sell = true;
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest
                const int retestLookback = 10;
                double barLow  = Data[currentIndex].Low;
                double barHigh = Data[currentIndex].High;

                for (int m = currentIndex - retestLookback; m < currentIndex; m++)
                {
                    if (m < 1) continue;

                    if (!buy && YukarıKesti(m, source, upper)
                        && barLow <= currentUpper
                        && currentPrice > currentUpper)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(m, source, lower)
                        && barHigh >= currentLower
                        && currentPrice < currentLower)
                    {
                        sell = true;
                    }
                }
            }
            else if (signalModeIndex == 6)
            {
                // 6: Confirmation bars
                const int confirmBars = 3;
                if (currentIndex >= confirmBars + 1)
                {
                    int crossBar = currentIndex - confirmBars;

                    bool stayedAbove = YukarıKesti(crossBar, source, upper);
                    bool stayedBelow = AsagiKesti(crossBar, source, lower);
                    for (int m = crossBar + 1; m <= currentIndex; m++)
                    {
                        stayedAbove &= source[m] > upper[m];
                        stayedBelow &= source[m] < lower[m];
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: Fiyat eğimi + bant state
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool priceRising  = source[currentIndex] > source[currentIndex - slopeLookback];
                    bool priceFalling = source[currentIndex] < source[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, source, upper) && priceRising)  buy  = true;
                    if (Kucuk(currentIndex, source, lower) && priceFalling) sell = true;
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

            if (skip) return TradeSignals.Skip;
            else if (flat) return TradeSignals.Flat;
            else if (takeProfit) return TradeSignals.TakeProfit;
            else if (stopLoss) return TradeSignals.StopLoss;
            else if (buy) return TradeSignals.Buy;
            else if (sell) return TradeSignals.Sell;

            return TradeSignals.None;
        }

        // Run baglamini (timeframe + opt mu) Trader'dan bir kez cozer. OnInit'te yapilamiyor
        // (orada Trader henuz null); ilk OnStep cagrisinda cagrilir.
        private void ResolveRunContext()
        {
            if (runContextResolved)
                return;

            runContextResolved = true;

            isOptimizationRun = Trader?.OptimizationEnabled == true;

            // SymbolPeriod: intraday'de dakika sayisi string'i ("5","15","240"); A/G/H/Y = Aylik/Gunluk/Haftalik/Yillik.
            // Cozulemezse (null / "" / "N/A") timeframeMinutes = 0 -> cagiran kod "bilinmiyor" diye ele alir.
            string sp = (Trader?.SymbolPeriod ?? "").Trim().ToUpperInvariant();
            timeframeMinutes = sp switch
            {
                "G" => 1440,      // 1 gun   (takvim dk)
                "H" => 10080,     // 1 hafta
                "A" => 43200,     // ~1 ay
                "Y" => 525600,    // ~1 yil  (365 * 1440)
                _   => (int.TryParse(sp, out var tf) && tf > 0) ? tf : 0
            };

            isOneMinute  = timeframeMinutes == 1;
            isFiveMinute = timeframeMinutes == 5;
            isOneHour    = timeframeMinutes == 60;
            isFourHour   = timeframeMinutes == 240;
            isOneDay     = timeframeMinutes == 1440;

            // Opt'ta konsolu bogmasin diye sadece tekli kosuda logla
            if (!isOptimizationRun)
            {
                string tfStr = Trader?.SymbolPeriod ?? "?";
                Log($"[{Name}] timeframe={tfStr} ({timeframeMinutes}dk), optRun={isOptimizationRun}");
            }
        }

        public override bool IsValidParameterCombination()
        {
            bool isValid = true;

            return isValid;
        }

        public double[]? GetUpper() => upper;
        public double[]? GetMiddle() => middle;
        public double[]? GetLower() => lower;

        /// <summary>
        /// Bu bar günün ilk barı mı? Periyottan bağımsız - dates[] takvim tarihini karşılaştırır,
        /// bar sayımına dayanmaz (1dk/15dk/1h/4h fark etmeksizin aynı şekilde çalışır).
        /// </summary>
        private bool IsFirstBarOfDay(int currentIndex)
        {
            if (currentIndex <= 0)
                return true;

            return dates[currentIndex] != dates[currentIndex - 1];
        }

        /// <summary>
        /// Bu bar günün son barı mı? Periyottan bağımsız - bir sonraki barın dates[] takvim
        /// tarihine bakar (lookahead); veri setinin son barıysa da true döner.
        /// </summary>
        private bool IsLastBarOfDay(int currentIndex)
        {
            if (currentIndex >= barCount - 1)
                return true;

            return dates[currentIndex + 1] != dates[currentIndex];
        }

        /// <summary>
        /// Bu bar haftanın ilk barı mı? Periyottan bağımsız - ISO 8601 hafta numarasını karşılaştırır
        /// (yıl sınırını da doğru ele alır, örn. 2025 hafta 52 -> 2026 hafta 1).
        /// </summary>
        private bool IsFirstBarOfWeek(int currentIndex)
        {
            if (currentIndex <= 0)
                return true;

            var current = dates[currentIndex].ToDateTime(TimeOnly.MinValue);
            var prev = dates[currentIndex - 1].ToDateTime(TimeOnly.MinValue);

            int currentWeek = System.Globalization.ISOWeek.GetWeekOfYear(current);
            int prevWeek = System.Globalization.ISOWeek.GetWeekOfYear(prev);
            int currentIsoYear = System.Globalization.ISOWeek.GetYear(current);
            int prevIsoYear = System.Globalization.ISOWeek.GetYear(prev);

            return currentWeek != prevWeek || currentIsoYear != prevIsoYear;
        }

        /// <summary>
        /// Bu bar ayın ilk barı mı? Periyottan bağımsız - dates[] takvim ay/yılını karşılaştırır.
        /// </summary>
        private bool IsFirstBarOfMonth(int currentIndex)
        {
            if (currentIndex <= 0)
                return true;

            var current = dates[currentIndex];
            var prev = dates[currentIndex - 1];

            return current.Month != prev.Month || current.Year != prev.Year;
        }

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
