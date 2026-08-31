using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Indicators.Base;
using AlgoTrade.Core.Trading.Strategy;
using ScottPlot.TickGenerators.Financial;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static AlgoTrade.Core.Trading.Utils.Utils;
using static Nessos.LinqOptimizer.Core.QueryExpr;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// Basit Moving Average Crossover Stratejisi
    ///
    /// MA Mantığı:
    /// - Hızlı MA yavaş MA'yı yukarı keserse AL (Golden Cross)
    /// - Hızlı MA yavaş MA'yı aşağı keserse SAT (Death Cross)
    ///
    /// Parametreler:
    /// - fastPeriod: Hızlı MA periyodu (varsayılan 10)
    /// - slowPeriod: Yavaş MA periyodu (varsayılan 20)
    /// - fastMaMethod: Hızlı MA'nın hareketli ortalama tipi (varsayılan SIMPLE)
    /// - slowMaMethod: Yavaş MA'nın hareketli ortalama tipi (varsayılan SIMPLE)
    /// - priceSource: Her iki MA'nın da beslendiği kaynak + OnStep sinyal serisi (varsayılan Close)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: Fast/Slow MA kesişimi     (fastMA slowMA'yı yukarı/aşağı kesince)
    ///     1: Fiyat-FastMA kesişimi     (fiyat fastMA'yı yukarı/aşağı kesince)
    ///     2: SlowMA slope flip         (slowMA'nın kendi yönü dönünce)
    ///     3: Fast/Slow MA state        (fastMA'nın slowMA'ya göre konumu - kesişim değil, koşul sürdükçe her bar)
    ///     4: Band / uzaklık filtresi   (fastMA slowMA'dan %bandThreshold'dan fazla uzaklaşınca)
    ///     5: Breakout + retest         (fast/slow kesişip fiyat slowMA'ya geri gelip retest tutunca)
    ///     6: Confirmation bars         (kesişimden sonra confirmBars bar aynı tarafta kalınca)
    ///     7: FastMA eğimi + Fast/Slow state (rejim: fastMA-slowMA konumu + momentum: fastMA N-bar eğimi)
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
    /// Enable/disable katmanlarinda asimetri var:
    /// - Buy/Sell/Flat/Skip: strateji disinda hicbir flag'e bakmaz, sadece kendi mantigiyla karar
    ///   verip TradeSignals doner. Islem gerceklesip gerceklesmeyecegi TEK katmanda,
    ///   SingleTrader.MapStrategyCommandsToTradeCommands() -> signals.AlEnabled/SatEnabled/... ile
    ///   disaridan kontrol edilir.
    /// - TakeProfit/StopLoss: buradaki her exitModeIndex dali, sinyali uretmeden ONCE
    ///   Trader.flags?.XHesaplaEnabled kontrolu yapiyor (KarAlZararKes helper'inin kendi ic
    ///   gate'i - karAlZararKes.cs'teki her metod zaten ayni flag'i tekrar kontrol ediyor, yani bu
    ///   dis kontrol pratikte gereksiz/redundant). Sinyal uretilse bile ayrica
    ///   MapStrategyCommandsToTradeCommands() -> signals.KarAlEnabled/ZararKesEnabled ikinci kez
    ///   gate ediyor. Yani KarAl/ZararKes cift katmanli, Buy/Sell/Flat/Skip tek katmanli - tutarsiz
    ///   ama zararsiz (flags.XHesaplaEnabled ilk trade'den sonra otomatik true olup bir daha hic
    ///   false'a donmuyor, bkz. SingleTrader.cs ExecuteOrders).
    /// </summary>
    public class SimpleMAStrategy : BaseStrategy
    {
        public override string Name => "Simple MA Crossover";

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

        private readonly int fastPeriod;
        private readonly int slowPeriod;
        private readonly int signalModeIndex; // buy/sell yöntemi
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        private readonly MAMethod fastMaMethod = MAMethod.SIMPLE;
        private readonly MAMethod slowMaMethod = MAMethod.SIMPLE;
        private readonly PriceSource priceSource = PriceSource.Close;

        private double[]? source;   // priceSource'un çözülmüş hali - OnInit'te bir kez, OnStep bundan okur
        private double[]? fastMA;
        private double[]? slowMA;

        // Parametreli constructor (data/indicators gerekli — parametresiz ctor kaldırıldı, hiç kullanılmıyordu)
        public SimpleMAStrategy(List<StockData> data, IndicatorManager indicators,
            int fastPeriod = 10, int slowPeriod = 20, MAMethod fastMaMethod = MAMethod.SIMPLE, MAMethod slowMaMethod = MAMethod.SIMPLE, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.fastPeriod      = fastPeriod;
            this.slowPeriod      = slowPeriod;
            this.fastMaMethod    = fastMaMethod;
            this.slowMaMethod    = slowMaMethod;
            this.priceSource     = priceSource;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["FastPeriod"]      = fastPeriod;
            Parameters["SlowPeriod"]      = slowPeriod;
            Parameters["FastMaMethod"]    = fastMaMethod;
            Parameters["SlowMaMethod"]    = slowMaMethod;
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

            try
            {
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

                // Moving average'leri hesapla (fastMaMethod/slowMaMethod / priceSource ile parametrik)
                fastMA = Indicators.MA.Calculate(source, fastMaMethod, fastPeriod);
                slowMA = Indicators.MA.Calculate(source, slowMaMethod, slowPeriod);

                // Tüm seriler OnStep'te aynı index ile birlikte okunuyor - uzunlukları uyuşmazsa
                // (örn. biri filtrelenmiş/kırpılmış gelirse) IndexOutOfRange yerine burada net hata ver
                bool allSeriesLengthsMatch = true;
                allSeriesLengthsMatch &= fastMA.Length       == barCount;
                allSeriesLengthsMatch &= slowMA.Length       == barCount;
                allSeriesLengthsMatch &= source.Length       == barCount;
                allSeriesLengthsMatch &= openPrices.Length   == barCount;
                allSeriesLengthsMatch &= highPrices.Length   == barCount;
                allSeriesLengthsMatch &= lowPrices.Length    == barCount;
                allSeriesLengthsMatch &= closePrices.Length  == barCount;
                allSeriesLengthsMatch &= volumes.Length      == barCount;
                allSeriesLengthsMatch &= lotSizes.Length     == barCount;
                allSeriesLengthsMatch &= dateTimes.Length    == barCount;
                allSeriesLengthsMatch &= dates.Length        == barCount;
                allSeriesLengthsMatch &= times.Length        == barCount;
                allSeriesLengthsMatch &= epochTimes.Length   == barCount;

                if (!allSeriesLengthsMatch)
                {
                    throw new InvalidOperationException(
                        $"Seri uzunlukları uyuşmuyor (barCount={barCount}): " +
                        $"fastMA={fastMA.Length}, slowMA={slowMA.Length}, source={source.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                        $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                        $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
                }

                //Log($"Strategy initialized: Fast={fastPeriod}, Slow={slowPeriod}, SignalModeIndex={signalModeIndex}");
            }
            catch (NotImplementedException)
            {
                // MA implement edilmiş durumda, bu blok normalde tetiklenmez -
                // savunma amaçlı bırakıldı, indikatör ileride kaldırılır/bozulursa sessizce crash yerine uyarı verir.
                LogWarning("MA indicator threw NotImplementedException! Strategy will not generate signals.");
                LogWarning("Check src/Trading/Indicators/MovingAverages/MovingAverageCalculator.cs — Calculate() implementation may be missing/broken.");

                barCount    = Indicators.BarCount;
                fastMA      = new double[barCount];
                slowMA      = new double[barCount];
                source      = new double[barCount];
                openPrices  = new double[barCount];
                highPrices  = new double[barCount];
                lowPrices   = new double[barCount];
                closePrices = new double[barCount];
                volumes     = new long[barCount];
                lotSizes    = new long[barCount];
                dateTimes   = new DateTime[barCount];
                dates       = new DateOnly[barCount];
                times       = new TimeOnly[barCount];
                epochTimes  = new long[barCount];
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

            // İlk barlarda yeterli veri yok (fastPeriod slowPeriod'dan büyük verilse bile güvenli)
            if (currentIndex < Math.Max(fastPeriod, slowPeriod))
                return TradeSignals.None;

            // OnInit'teki catch bloğu tetiklenip boş array birakmışsa sinyal üretme
            if (fastMA == null || fastMA.Length == 0)
                return TradeSignals.None;

            if (slowMA == null || slowMA.Length == 0)
                return TradeSignals.None;

            if (source == null || source.Length == 0)
                return TradeSignals.None;
            // ************************************************************************************************************************

            // Geçerli ve önceki değerler (source = OnInit'te priceSource'tan çözülen seri)
            double currentPrice  = source[currentIndex];
            double prevPrice     = source[currentIndex - 1];
            double currentFastMA = fastMA[currentIndex];
            double prevFastMA    = fastMA[currentIndex - 1];
            double currentSlowMA = slowMA[currentIndex];
            double prevSlowMA    = slowMA[currentIndex - 1];
            // ************************************************************************************************************************

            // signalModeIndex ile buy/sell yöntemi seçilir - detay için sınıf başı doc comment
            if (signalModeIndex == 0)
            {
                // 0: Fast/Slow MA kesişimi - fastMA slowMA'yı yukarı kesince AL, aşağı kesince SAT
                if (YukarıKesti(currentIndex, fastMA, slowMA)) buy  = true;
                if (AsagiKesti(currentIndex, fastMA, slowMA))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: Fiyat-FastMA kesişimi - fiyat fastMA'yı yukarı kesince AL, aşağı kesince SAT
                if (YukarıKesti(currentIndex, source, fastMA)) buy  = true;
                if (AsagiKesti(currentIndex, source, fastMA))  sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: SlowMA slope flip - slowMA'nın kendi yönü dönüyor (düşen/düz → yükselen = AL)
                if (currentIndex >= 2)
                {
                    double slopeNow  = slowMA[currentIndex]     - slowMA[currentIndex - 1];
                    double slopePrev = slowMA[currentIndex - 1] - slowMA[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: Fast/Slow MA state - fastMA'nın slowMA'ya göre konumu (kesişim değil, koşul sürdükçe her bar)
                if (Buyuk(currentIndex, fastMA, slowMA)) buy  = true;
                if (Kucuk(currentIndex, fastMA, slowMA)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Band / uzaklık filtresi - fastMA slowMA'dan %bandThreshold'dan fazla uzaklaşınca (trend-following)
                const double bandThreshold = 0.01; // %1
                if (currentSlowMA != 0.0)
                {
                    double distanceRatio = (currentFastMA - currentSlowMA) / currentSlowMA;
                    if (distanceRatio >  bandThreshold) buy  = true;
                    if (distanceRatio < -bandThreshold) sell = true;
                }
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest - son retestLookback bar içinde fast/slow kesişti, şimdi fiyat
                //    slowMA'ya geri dokunup (retest) kırılım yönünde kapattıysa → sinyal
                const int retestLookback = 10;
                double barLow  = Data[currentIndex].Low;
                double barHigh = Data[currentIndex].High;

                for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                {
                    if (k < 1) continue;

                    if (!buy && YukarıKesti(k, fastMA, slowMA)
                        && barLow <= currentSlowMA          // bu bar slowMA'ya geri dokundu (retest)
                        && currentPrice > currentSlowMA)    // ama üstünde kapattı (retest tuttu)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(k, fastMA, slowMA)
                        && barHigh >= currentSlowMA
                        && currentPrice < currentSlowMA)
                    {
                        sell = true;
                    }
                }
            }
            else if (signalModeIndex == 6)
            {
                // 6: Confirmation bars - kesişim confirmBars bar önce oldu ve o zamandan beri fastMA
                //    hep slowMA'nın aynı tarafında kaldıysa gir
                const int confirmBars = 3;
                if (currentIndex >= confirmBars + 1)
                {
                    int crossBar = currentIndex - confirmBars;

                    bool stayedAbove = YukarıKesti(crossBar, fastMA, slowMA);
                    bool stayedBelow = AsagiKesti(crossBar, fastMA, slowMA);
                    for (int k = crossBar + 1; k <= currentIndex; k++)
                    {
                        stayedAbove &= fastMA[k] > slowMA[k];
                        stayedBelow &= fastMA[k] < slowMA[k];
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: FastMA eğimi + Fast/Slow state - rejim (fastMA-slowMA konumu) + momentum (fastMA N-bar eğimi)
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool fastMARising  = fastMA[currentIndex] > fastMA[currentIndex - slopeLookback];
                    bool fastMAFalling = fastMA[currentIndex] < fastMA[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, fastMA, slowMA) && fastMARising)  buy  = true;
                    if (Kucuk(currentIndex, fastMA, slowMA) && fastMAFalling) sell = true;
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
        /// Fast MA değerlerini al (plotting veya analiz için)
        /// </summary>
        public double[]? GetFastMA() => fastMA;

        /// <summary>
        /// Slow MA değerlerini al (plotting veya analiz için)
        /// </summary>
        public double[]? GetSlowMA() => slowMA;

        /// <summary>
        /// FastPeriod parametresini al
        /// </summary>
        public int FastPeriod => fastPeriod;

        /// <summary>
        /// SlowPeriod parametresini al
        /// </summary>
        public int SlowPeriod => slowPeriod;

        /// <summary>
        /// Get indicators for plotting (IStrategy implementation)
        /// </summary>
        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (fastMA != null && fastMA.Length > 0)
                indicators[$"Fast MA ({fastPeriod})"] = fastMA;

            if (slowMA != null && slowMA.Length > 0)
                indicators[$"Slow MA ({slowPeriod})"] = slowMA;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
