using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
using static AlgoTrade.Core.Trading.Utils.Utils;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// MOST (Moving Stop Loss) İndikatörü Stratejisi
    ///
    /// MOST Mantığı:
    /// - Yükseliş trendinde: MOST fiyatın altında stop loss görevi görür
    /// - Düşüş trendinde: MOST fiyatın üstünde direnç görevi görür
    /// - Fiyat MOST'u yukarı kırınca AL (trend değişimi)
    /// - Fiyat MOST'u aşağı kırınca SAT (trend değişimi)
    ///
    /// Parametreler:
    /// - period: MOST periyodu (varsayılan 21)
    /// - percent: MOST yüzde sapması (varsayılan 1.0)
    /// - signalModeIndex: buy/sell kategorisinin dispatch parametresi - 0: Fiyat-MOST kırılımı,
    ///                     1: MOST-EXMOV kesişimi (eskiden choice)
    /// - exitModeIndex: takeProfit/stopLoss kategorisinin dispatch parametresi - PLACEHOLDER, henuz okunmuyor
    /// - flatModeIndex: flat kategorisinin dispatch parametresi - PLACEHOLDER, henuz okunmuyor
    /// - skipModeIndex: skip kategorisinin dispatch parametresi - PLACEHOLDER, henuz okunmuyor
    /// - ruleModeIndex: PLACEHOLDER, henuz okunmuyor - ileride ihtiyaç halinde kullanilacak ekstra eksen
    /// </summary>
    public class SimpleMostStrategy : BaseStrategy
    {
        public override string Name => "Simple MOST Strategy";

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
        private readonly double percent;
        private readonly int signalModeIndex; // eskiden choice: 0: Price-MOST cross, 1: EXMOV-MOST cross
        private readonly int exitModeIndex;
        private readonly int flatModeIndex;
        private readonly int skipModeIndex;
        private readonly int ruleModeIndex;
        private double[]? most;
        private double[]? exmov;

        // Parametresiz constructor (eski kullanımlar için)
        public SimpleMostStrategy(int period = 21, double percent = 1.0,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period          = period;
            this.percent         = percent;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Period"]          = period;
            Parameters["Percent"]         = percent;
            Parameters["RuleModeIndex"]   = ruleModeIndex;
            Parameters["SignalModeIndex"] = signalModeIndex;
            Parameters["ExitModeIndex"]   = exitModeIndex;
            Parameters["FlatModeIndex"]   = flatModeIndex;
            Parameters["SkipModeIndex"]   = skipModeIndex;
        }

        // Parametreli constructor (yeni kullanım)
        public SimpleMostStrategy(List<StockData> data, IndicatorManager indicators,
            int period = 21, double percent = 1.0,
            int signalModeIndex = 0, int exitModeIndex = 0, int flatModeIndex = 0, int skipModeIndex = 0, int ruleModeIndex = 0)
        {
            this.period          = period;
            this.percent         = percent;
            this.ruleModeIndex   = ruleModeIndex;
            this.signalModeIndex = signalModeIndex;
            this.exitModeIndex   = exitModeIndex;
            this.flatModeIndex   = flatModeIndex;
            this.skipModeIndex   = skipModeIndex;

            Parameters["Period"]          = period;
            Parameters["Percent"]         = percent;
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

                // MOST indicator'ı hesapla
                (most, exmov) = Indicators.Trend.MOST(period, percent);

                // Tüm seriler OnStep'te aynı index ile birlikte okunuyor - uzunlukları uyuşmazsa
                // (örn. biri filtrelenmiş/kırpılmış gelirse) IndexOutOfRange yerine burada net hata ver
                bool allSeriesLengthsMatch = true;
                allSeriesLengthsMatch &= most.Length        == barCount;
                allSeriesLengthsMatch &= exmov.Length       == barCount;
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
                        $"most={most.Length}, exmov={exmov.Length}, open={openPrices.Length}, high={highPrices.Length}, " +
                        $"low={lowPrices.Length}, close={closePrices.Length}, volume={volumes.Length}, lot={lotSizes.Length}, " +
                        $"dateTime={dateTimes.Length}, date={dates.Length}, time={times.Length}, epoch={epochTimes.Length}");
                }

                //Log($"SimpleMostStrategy initialized: Period={period}, Percent={percent}, SignalModeIndex={signalModeIndex}");
            }
            catch (NotImplementedException)
            {
                // MOST implement edilmiş durumda (TrendIndicators.cs), bu blok normalde tetiklenmez -
                // savunma amaçlı bırakıldı, indikatör ileride kaldırılır/bozulursa sessizce crash yerine uyarı verir.
                LogWarning("MOST indicator threw NotImplementedException! Strategy will not generate signals.");
                LogWarning("Check src/Trading/Indicators/Trend/TrendIndicators.cs — MOST() implementation may be missing/broken.");

                barCount = Indicators.BarCount;
                most = new double[barCount];
                exmov = new double[barCount];
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

            // İlk barlarda yeterli veri yok
            if (currentIndex < period)
                return TradeSignals.None;

            // OnInit'teki catch bloğu tetiklenip boş array birakmışsa sinyal üretme
            if (most == null || most.Length == 0)
                return TradeSignals.None;

            if (exmov == null || exmov.Length == 0)
                return TradeSignals.None;

            if (closePrices == null || closePrices.Length == 0)
                return TradeSignals.None;
            // ************************************************************************************************************************

            // Geçerli ve önceki değerler
            double currentPrice = Data[currentIndex].Close;
            double prevPrice    = Data[currentIndex - 1].Close;
            double currentMost  = most[currentIndex];
            double prevMost     = most[currentIndex - 1];
            double currentExmov = exmov[currentIndex];
            double prevExmov    = exmov[currentIndex - 1];

            // ************************************************************************************************************************
            // choice: 0 = Fiyat MOST kırılımı, 1 = EXMOV-MOST kesişimi (configurable via constructor)
            if (signalModeIndex == 0)
            {
                // MOST AL Sinyali: Fiyat MOST'u yukarı kırıyor (trend değişimi: düşüşten yükselişe)
                // Önceki bar: fiyat <= MOST, şimdiki bar: fiyat > MOST
                if (YukarıKesti(currentIndex, closePrices, most))
                {
                    buy = true;
                }

                // MOST SAT Sinyali: Fiyat MOST'u aşağı kırıyor (trend değişimi: yükselişten düşüşe)
                // Önceki bar: fiyat >= MOST, şimdiki bar: fiyat < MOST
                if (AsagiKesti(currentIndex, closePrices, most))
                {
                    sell = true;
                }
            }
            else
            {
                // EXMOV, MOST çizgisini yukarı kestiğinde BUY
                // (EXMOV alttan yukarı doğru MOST'u geçer: MOST üstte → EXMOV üstte)
                // Önceki bar: EXMOV <= MOST, şimdiki bar: EXMOV > MOST
                if (YukarıKesti(currentIndex, exmov, most))
                {
                    buy = true;
                }

                // EXMOV, MOST çizgisini aşağı kestiğinde SELL
                // (EXMOV üstten aşağı doğru MOST'u geçer: EXMOV üstte → MOST üstte)
                // Önceki bar: EXMOV >= MOST, şimdiki bar: EXMOV < MOST
                if (AsagiKesti(currentIndex, exmov, most))
                {
                    sell = true;
                }
            }
            // ************************************************************************************************************************

            // ÖRNEK: Trader referansını kullanarak kar al / zarar kes hesaplama
            // Trader property'si BaseStrategy.SetTrader() ile otomatik set edilir
            if (Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    // Trader.flags.KarAlSeviyeHesaplaEnabled kapaliysa metod iceride 0 doner (takeProfit hep false kalir)
                    takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
                }
            }
            
            if (Trader != null)
            {
                if (exitModeIndex == 0)
                {
                    // Trader.flags.ZararKesSeviyeHesaplaEnabled kapaliysa metod iceride 0 doner (stopLoss hep false kalir)
                    stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
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
        /// MOST değerlerini al (plotting veya analiz için)
        /// </summary>
        public double[]? GetMOST() => most;

        /// <summary>
        /// EXMOV değerlerini al (plotting veya analiz için)
        /// </summary>
        public double[]? GetEXMOV() => exmov;

        /// <summary>
        /// Period parametresini al
        /// </summary>
        public int Period => period;

        /// <summary>
        /// Percent parametresini al
        /// </summary>
        public double Percent => percent;

        /// <summary>
        /// Get indicators for plotting (IStrategy implementation)
        /// </summary>
        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (most != null && most.Length > 0)
                indicators["MOST"] = most;

            if (exmov != null && exmov.Length > 0)
                indicators["EXMOV"] = exmov;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
