using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
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
    /// </summary>
    public class SimpleMostStrategy : BaseStrategy
    {
        public override string Name => "Simple MOST Strategy";

        private readonly int _period;
        private readonly double _percent;
        private readonly int _choice; // 0: Price-MOST cross, 1: EXMOV-MOST cross
        private double[]? _most;
        private double[]? _exmov;

        // Parametresiz constructor (eski kullanımlar için)
        public SimpleMostStrategy(int period = 21, double percent = 1.0, int choice = 0)
        {
            _period = period;
            _percent = percent;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Percent"] = percent;
            Parameters["Choice"] = choice;
        }

        // Parametreli constructor (yeni kullanım)
        public SimpleMostStrategy(List<StockData> data, IndicatorManager indicators, int period = 21, double percent = 1.0, int choice = 0)
        {
            _period = period;
            _percent = percent;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["Percent"] = percent;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            try
            {
                // MOST indicator'ı hesapla
                (_most, _exmov) = Indicators.Trend.MOST(_period, _percent);

                //Log($"SimpleMostStrategy initialized: Period={_period}, Percent={_percent}, Choice={_choice}");
            }
            catch (NotImplementedException)
            {
                // MOST implement edilmiş durumda (TrendIndicators.cs), bu blok normalde tetiklenmez -
                // savunma amaçlı bırakıldı, indikatör ileride kaldırılır/bozulursa sessizce crash yerine uyarı verir.
                LogWarning("MOST indicator threw NotImplementedException! Strategy will not generate signals.");
                LogWarning("Check src/Trading/Indicators/Trend/TrendIndicators.cs — MOST() implementation may be missing/broken.");

                int barCount = Indicators.BarCount;
                _most = new double[barCount];
                _exmov = new double[barCount];
            }
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;
            // ************************************************************************************************************************

            // İlk barlarda yeterli veri yok
            if (currentIndex < _period)
                return TradeSignals.None;

            // OnInit'teki catch bloğu tetiklenip boş array birakmışsa sinyal üretme
            if (_most == null || _most.Length == 0)
                return TradeSignals.None;

            if (_exmov == null || _exmov.Length == 0)
                return TradeSignals.None;
            // ************************************************************************************************************************

            // Geçerli ve önceki değerler
            double currentPrice = Data[currentIndex].Close;
            double prevPrice = Data[currentIndex - 1].Close;
            double currentMost = _most[currentIndex];
            double prevMost = _most[currentIndex - 1];
            double currentExmov = _exmov[currentIndex];
            double prevExmov = _exmov[currentIndex - 1];

            // ************************************************************************************************************************
            // choice: 0 = Fiyat MOST kırılımı, 1 = EXMOV-MOST kesişimi (configurable via constructor)
            if (_choice == 0)
            {
                // MOST AL Sinyali: Fiyat MOST'u yukarı kırıyor (trend değişimi: düşüşten yükselişe)
                // Önceki bar: fiyat <= MOST
                // Şimdiki bar: fiyat > MOST
                if (prevPrice <= prevMost && currentPrice > currentMost)
                {
                    buy = true;
                }

                // MOST SAT Sinyali: Fiyat MOST'u aşağı kırıyor (trend değişimi: yükselişten düşüşe)
                // Önceki bar: fiyat >= MOST
                // Şimdiki bar: fiyat < MOST
                if (prevPrice >= prevMost && currentPrice < currentMost)
                {
                    sell = true;
                }
            }
            else
            {
                // ExMov, MOST çizgisini yukarı kestiğinde BUY
                // (EXMOV alttan yukarı doğru MOST'u geçer: MOST üstte → EXMOV üstte)
                // Önceki bar: MOST >= EXMOV
                // Şimdiki bar: MOST < EXMOV
                if (prevMost >= prevExmov && currentMost < currentExmov)
                {
                    buy = true;
                }

                // ExMov, MOST çizgisini aşağı kestiğinde SELL
                // (EXMOV üstten aşağı doğru MOST'u geçer: EXMOV üstte → MOST üstte)
                // Önceki bar: MOST <= EXMOV
                // Şimdiki bar: MOST > EXMOV
                if (prevMost <= prevExmov && currentMost > currentExmov)
                {
                    sell = true;
                }
            }
            // ************************************************************************************************************************

            // ÖRNEK: Trader referansını kullanarak kar al / zarar kes hesaplama
            // Trader property'si BaseStrategy.SetTrader() ile otomatik set edilir
            if (Trader != null)
            {
                // Trader.flags.KarAlSeviyeHesaplaEnabled kapaliysa metod iceride 0 doner (takeProfit hep false kalir)
                takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
            }
            
            if (Trader != null)
            {
                // Trader.flags.ZararKesSeviyeHesaplaEnabled kapaliysa metod iceride 0 doner (stopLoss hep false kalir)
                stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
            }
            // ************************************************************************************************************************

            // Flat olma durumu burada incelenir ve flat flag'i setlenir
            flat = false;
            
            // Skip olma durumu burada incelenir ve skip flag'i setlenir            
            skip = false;
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
        public double[]? GetMOST() => _most;

        /// <summary>
        /// EXMOV değerlerini al (plotting veya analiz için)
        /// </summary>
        public double[]? GetEXMOV() => _exmov;

        /// <summary>
        /// Period parametresini al
        /// </summary>
        public int Period => _period;

        /// <summary>
        /// Percent parametresini al
        /// </summary>
        public double Percent => _percent;

        /// <summary>
        /// Get indicators for plotting (IStrategy implementation)
        /// </summary>
        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (_most != null && _most.Length > 0)
                indicators["MOST"] = _most;

            if (_exmov != null && _exmov.Length > 0)
                indicators["EXMOV"] = _exmov;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
