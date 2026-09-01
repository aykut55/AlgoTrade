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
    /// MACD (Moving Average Convergence Divergence) Stratejisi
    ///
    /// MACD Mantığı:
    /// - MACD/Signal çifti MOST'un most/exmov çiftinin analogu
    /// - MACD Line = Fast EMA - Slow EMA, Signal Line = MACD'nin EMA'sı
    ///
    /// Parametreler:
    /// - fastPeriod/slowPeriod/signalPeriod: EMA periyotları (varsayılan 12/26/9)
    /// - priceSource: MACD'nin beslendiği kaynak (varsayılan Close - klasik MACD)
    /// - signalModeIndex: buy/sell yöntemini seçer:
    ///     0: MACD-Signal kesişimi       (MACD, Signal'i yukarı/aşağı kesince)
    ///     1: MACD-zero kesişimi         (MACD 0'ı yukarı/aşağı kesince - ikinci klasik MACD sinyali)
    ///     2: MACD slope flip            (MACD'nin kendi yönü dönünce)
    ///     3: MACD-Signal state          (konum - kesişim değil, koşul sürdükçe her bar)
    ///     4: Histogram band/uzaklık     (MACD-Signal farkı %bandThreshold'dan fazla açılınca)
    ///     5: Breakout + retest          (MACD Signal'i kesip geri yaklaşıp tutunca)
    ///     6: Confirmation bars          (kesişimden sonra confirmBars bar aynı tarafta kalınca)
    ///     7: MACD eğimi + state combo   (rejim: MACD-Signal konumu + momentum: MACD N-bar eğimi)
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
    public class SimpleMACDStrategy : BaseStrategy
    {
        public override string Name => "Simple MACD Strategy";

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
        private readonly int signalPeriod;
        private readonly int signalModeIndex;
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;

        private readonly PriceSource priceSource = PriceSource.Close;

        private double[]? source;
        private double[]? macd;
        private double[]? signal;
        // Run baglami - ILK OnStep cagrisinda Trader'dan cozulur (OnInit'te DEGIL: OnInit
        // constructor'dan calisir, SetTrader() daha sonra SetStrategy() icinde -> OnInit'te Trader null).
        private bool runContextResolved;
        private int  timeframeMinutes;    // 1, 5, 15, 60, 240 ... (0 = SymbolPeriod cozulemedi)
        private bool isOptimizationRun;   // true = opt taramasi icinde (Trader.OptimizationEnabled), false = tekli kosu


        public SimpleMACDStrategy(List<StockData> data, IndicatorManager indicators,
            int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9, PriceSource priceSource = PriceSource.Close,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.fastPeriod      = fastPeriod;
            this.slowPeriod      = slowPeriod;
            this.signalPeriod    = signalPeriod;
            this.priceSource     = priceSource;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["FastPeriod"]     = fastPeriod;
            Parameters["SlowPeriod"]     = slowPeriod;
            Parameters["SignalPeriod"]   = signalPeriod;
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

            var macdResult = Indicators.Momentum.MACD(source, fastPeriod, slowPeriod, signalPeriod);
            macd   = macdResult.MACD;
            signal = macdResult.Signal;

            bool allSeriesLengthsMatch = true;
            allSeriesLengthsMatch &= macd.Length        == barCount;
            allSeriesLengthsMatch &= signal.Length      == barCount;
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
                    $"macd={macd.Length}, signal={signal.Length}, source={source.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                    $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                    $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            ResolveRunContext();

            bool buy = false, sell = false, takeProfit = false, stopLoss = false, flat = false, skip = false;

            if (currentIndex < slowPeriod + signalPeriod + 1)
                return TradeSignals.None;

            if (macd == null || signal == null || macd.Length == 0)
                return TradeSignals.None;

            double currentMACD = macd[currentIndex];
            double currentSignal = signal[currentIndex];

            if (double.IsNaN(currentMACD) || double.IsNaN(currentSignal))
                return TradeSignals.None;

            if (signalModeIndex == 0)
            {
                // 0: MACD-Signal kesişimi
                if (YukarıKesti(currentIndex, macd, signal)) buy  = true;
                if (AsagiKesti(currentIndex, macd, signal))  sell = true;
            }
            else if (signalModeIndex == 1)
            {
                // 1: MACD-zero kesişimi
                const double zero = 0.0;
                if (YukarıKesti(currentIndex, macd, zero)) buy  = true;
                if (AsagiKesti(currentIndex, macd, zero))  sell = true;
            }
            else if (signalModeIndex == 2)
            {
                // 2: MACD slope flip
                if (currentIndex >= 2)
                {
                    double slopeNow  = macd[currentIndex]     - macd[currentIndex - 1];
                    double slopePrev = macd[currentIndex - 1] - macd[currentIndex - 2];
                    if (slopePrev <= 0.0 && slopeNow > 0.0) buy  = true;
                    if (slopePrev >= 0.0 && slopeNow < 0.0) sell = true;
                }
            }
            else if (signalModeIndex == 3)
            {
                // 3: MACD-Signal state - koşul sürdükçe her bar
                if (Buyuk(currentIndex, macd, signal)) buy  = true;
                if (Kucuk(currentIndex, macd, signal)) sell = true;
            }
            else if (signalModeIndex == 4)
            {
                // 4: Histogram band/uzaklık filtresi
                const double bandThreshold = 0.5; // MACD birimi
                double histogram = currentMACD - currentSignal;
                if (histogram >  bandThreshold) buy  = true;
                if (histogram < -bandThreshold) sell = true;
            }
            else if (signalModeIndex == 5)
            {
                // 5: Breakout + retest
                const int retestLookback = 10;
                const double retestBand  = 0.1;

                for (int k = currentIndex - retestLookback; k < currentIndex; k++)
                {
                    if (k < 1) continue;

                    if (!buy && YukarıKesti(k, macd, signal)
                        && Math.Abs(currentMACD - currentSignal) <= retestBand
                        && currentMACD > currentSignal)
                    {
                        buy = true;
                    }

                    if (!sell && AsagiKesti(k, macd, signal)
                        && Math.Abs(currentMACD - currentSignal) <= retestBand
                        && currentMACD < currentSignal)
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

                    bool stayedAbove = YukarıKesti(crossBar, macd, signal);
                    bool stayedBelow = AsagiKesti(crossBar, macd, signal);
                    for (int k = crossBar + 1; k <= currentIndex; k++)
                    {
                        stayedAbove &= macd[k] > signal[k];
                        stayedBelow &= macd[k] < signal[k];
                    }
                    if (stayedAbove) buy  = true;
                    if (stayedBelow) sell = true;
                }
            }
            else if (signalModeIndex == 7)
            {
                // 7: MACD eğimi + state combo
                const int slopeLookback = 3;
                if (currentIndex >= slopeLookback)
                {
                    bool macdRising  = macd[currentIndex] > macd[currentIndex - slopeLookback];
                    bool macdFalling = macd[currentIndex] < macd[currentIndex - slopeLookback];
                    if (Buyuk(currentIndex, macd, signal) && macdRising)  buy  = true;
                    if (Kucuk(currentIndex, macd, signal) && macdFalling) sell = true;
                }
            }

            if (Trader != null)
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

            if (Trader != null)
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

            // Opt'ta konsolu bogmasin diye sadece tekli kosuda logla
            if (!isOptimizationRun)
            {
                string tfStr = Trader?.SymbolPeriod ?? "?";
                Log($"[{Name}] timeframe={tfStr} ({timeframeMinutes}dk), optRun={isOptimizationRun}");
            }
        }

        public double[]? GetMACDLine() => macd;
        public double[]? GetSignalLine() => signal;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();
            if (macd != null && macd.Length > 0) indicators["MACD"] = macd;
            if (signal != null && signal.Length > 0) indicators["Signal"] = signal;
            return indicators.Count > 0 ? indicators : null;
        }
    }
}
