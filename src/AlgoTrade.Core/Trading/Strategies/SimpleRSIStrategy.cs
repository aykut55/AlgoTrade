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
    /// </summary>
    public class SimpleRSIStrategy : BaseStrategy
    {
        public override string Name => "Simple RSI Strategy";

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
        private readonly double oversold;
        private readonly double overbought;
        private readonly int signalModeIndex; // buy/sell yöntemi - bkz. sınıf başı doc comment (0-7)
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        // RSI hesabı - parametreli ctor'dan gelir; verilmezse Close (klasik RSI ile birebir aynı).
        private readonly PriceSource priceSource = PriceSource.Close;

        private double[]? source;   // priceSource'un çözülmüş hali - OnInit'te bir kez, OnStep bundan okur
        private double[]? rsi;

        // Parametresiz constructor (eski kullanımlar için)
        public SimpleRSIStrategy(int period = 14, double oversold = 30, double overbought = 70, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period          = period;
            this.oversold        = oversold;
            this.overbought      = overbought;
            this.priceSource     = priceSource;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Period"]          = period;
            Parameters["Oversold"]        = oversold;
            Parameters["Overbought"]      = overbought;
            Parameters["PriceSource"]     = priceSource;
            Parameters["RuleModeIndex"]   = ruleModeIndex;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"]   = exitModeIndex;
            Parameters["FlatModeIndex"]   = flatModeIndex;
            Parameters["SkipModeIndex"]   = skipModeIndex;
        }

        // Parametreli constructor (yeni kullanım)
        public SimpleRSIStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 14, double oversold = 30, double overbought = 70, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period          = period;
            this.oversold        = oversold;
            this.overbought      = overbought;
            this.priceSource     = priceSource;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Period"]          = period;
            Parameters["Oversold"]        = oversold;
            Parameters["Overbought"]      = overbought;
            Parameters["PriceSource"]     = priceSource;
            Parameters["RuleModeIndex"]   = ruleModeIndex;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"]   = exitModeIndex;
            Parameters["FlatModeIndex"]   = flatModeIndex;
            Parameters["SkipModeIndex"]   = skipModeIndex;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            // verileri oku
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

            // RSI'ı hesapla (priceSource ile parametrik)
            rsi = Indicators.Momentum.RSI(source, period).Values;

            // Tüm seriler OnStep'te aynı index ile birlikte okunuyor - uzunlukları uyuşmazsa
            // (örn. biri filtrelenmiş/kırpılmış gelirse) IndexOutOfRange yerine burada net hata ver
            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= rsi.Length         == barCount;
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
                    $"rsi={rsi.Length}, source={source.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                    $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                    $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
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

            // ÖRNEK: Trader referansını kullanarak kar al / zarar kes hesaplama
            // Trader property'si BaseStrategy.SetTrader() ile otomatik set edilir
            if (Trader != null)
            {
                // Trader.flags.KarAlSeviyeHesaplaEnabled kapaliysa metod iceride 0 doner(takeProfit hep false kalir)
                if (exitModeIndex == 0)
                {
                    // 0: Seviye, seviyeli
                    if (Trader.flags?.KarAlSeviyeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    // 1: Yüzde, seviyeli
                    if (Trader.flags?.KarAlYuzdeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesaplaSeviyeli(currentIndex, 2, 10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    // 2: Seviye, tek seviye
                    if (Trader.flags?.KarAlSeviyeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesapla(currentIndex, 2000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    // 3: Yüzde, tek seviye
                    if (Trader.flags?.KarAlYuzdeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlYuzdeHesapla(currentIndex, 2.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    // 4: Anlık kar/zarar fiyat seviyesi (pozisyon bazlı)
                    if (Trader.flags?.KarAlSeviyeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(currentIndex, 1000.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
                    // 5: Anlık kar/zarar yüzdesi (pozisyon bazlı)
                    if (Trader.flags?.KarAlYuzdeHesaplaEnabled == true)
                        takeProfit = Trader.karAlZararKes.KarZararYuzdesindenKarAlHesapla(currentIndex, 3.0) != 0;
                }
            }

            if (Trader != null)
            {
                // Trader.flags.ZararKesSeviyeHesaplaEnabled kapaliysa metod iceride 0 doner(stopLoss hep false kalir)
                if (exitModeIndex == 0)
                {
                    // 0: Seviye, seviyeli
                    if (Trader.flags?.ZararKesSeviyeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
                }
                else if (exitModeIndex == 1)
                {
                    // 1: Yüzde, seviyeli
                    if (Trader.flags?.ZararKesYuzdeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesaplaSeviyeli(currentIndex, -2, -10, 0.01) != 0;
                }
                else if (exitModeIndex == 2)
                {
                    // 2: Seviye, tek seviye
                    if (Trader.flags?.ZararKesSeviyeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesapla(currentIndex, -1000.0) != 0;
                }
                else if (exitModeIndex == 3)
                {
                    // 3: Yüzde, tek seviye
                    if (Trader.flags?.ZararKesYuzdeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesYuzdeHesapla(currentIndex, -1.0) != 0;
                }
                else if (exitModeIndex == 4)
                {
                    // 4: Anlık kar/zarar fiyat seviyesi (pozisyon bazlı)
                    if (Trader.flags?.ZararKesSeviyeHesaplaEnabled == true)
                        stopLoss = Trader.karAlZararKes.KarZararFiyatSeviyesindenZararKesHesapla(currentIndex, -500.0) != 0;
                }
                else if (exitModeIndex == 5)
                {
                    // 5: Anlık kar/zarar yüzdesi (pozisyon bazlı)
                    if (Trader.flags?.ZararKesYuzdeHesaplaEnabled == true)
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
