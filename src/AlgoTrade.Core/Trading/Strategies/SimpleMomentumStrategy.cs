using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// Momentum İndikatörü Stratejisi
    ///
    /// Momentum Mantığı:
    /// - Fiyatın belirli periyot öncesine göre yüzdesel değişimi
    /// - ROC (Rate of Change) formülü kullanılır
    ///
    /// Trading Logic (choice=0):
    /// - AL: Momentum pozitif eşiği yukarı kesiyor
    /// - SAT: Momentum negatif eşiği aşağı kesiyor
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - period: Momentum periyodu (varsayılan 12)
    /// - positiveThreshold: Pozitif sinyal eşiği (varsayılan 0)
    /// - negativeThreshold: Negatif sinyal eşiği (varsayılan 0)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleMomentumStrategy : BaseStrategy
    {
        public override string Name => "Simple Momentum Strategy";

        private readonly int _period;
        private readonly double _positiveThreshold;
        private readonly double _negativeThreshold;
        private readonly int _choice;
        private double[]? _momentum;

        public SimpleMomentumStrategy(int period = 12, double positiveThreshold = 0, double negativeThreshold = 0, int choice = 0)
        {
            _period = period;
            _positiveThreshold = positiveThreshold;
            _negativeThreshold = negativeThreshold;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["PositiveThreshold"] = positiveThreshold;
            Parameters["NegativeThreshold"] = negativeThreshold;
            Parameters["Choice"] = choice;
        }

        public SimpleMomentumStrategy(List<StockData> data, IndicatorManager indicators, int period = 12, double positiveThreshold = 0, double negativeThreshold = 0, int choice = 0)
        {
            _period = period;
            _positiveThreshold = positiveThreshold;
            _negativeThreshold = negativeThreshold;
            _choice = choice;

            Parameters["Period"] = period;
            Parameters["PositiveThreshold"] = positiveThreshold;
            Parameters["NegativeThreshold"] = negativeThreshold;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            var closes = Indicators.GetClosePrices();
            _momentum = Indicators.Momentum.ROC(closes, _period);

            //Log($"SimpleMomentumStrategy initialized: Period={_period}, PositiveThreshold={_positiveThreshold}, NegativeThreshold={_negativeThreshold}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            if (currentIndex < _period + 1)
                return TradeSignals.None;

            if (_momentum == null || _momentum.Length == 0)
                return TradeSignals.None;

            double currentMom = _momentum[currentIndex];
            double prevMom = _momentum[currentIndex - 1];

            if (double.IsNaN(currentMom) || double.IsNaN(prevMom))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = Threshold crossover, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: Momentum pozitif eşiği yukarı kesiyor
                if (prevMom <= _positiveThreshold && currentMom > _positiveThreshold)
                {
                    buy = true;
                }

                // SAT: Momentum negatif eşiği aşağı kesiyor
                if (prevMom >= _negativeThreshold && currentMom < _negativeThreshold)
                {
                    sell = true;
                }
            }
            else
            {
                // İleride eklenecek alternatif sinyal mantığı
            }
            // ************************************************************************************************************************

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

            // Flat olma durumu burada incelenir ve flat flag'i setlenir
            flat = false;

            // Skip olma durumu burada incelenir ve skip flag'i setlenir
            skip = false;

            if (skip) return TradeSignals.Skip;
            else if (flat) return TradeSignals.Flat;
            else if (takeProfit) return TradeSignals.TakeProfit;
            else if (stopLoss) return TradeSignals.StopLoss;
            else if (buy) return TradeSignals.Buy;
            else if (sell) return TradeSignals.Sell;

            return TradeSignals.None;
        }

        public double[]? GetMomentum() => _momentum;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (_momentum != null && _momentum.Length > 0)
                indicators["Momentum"] = _momentum;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
