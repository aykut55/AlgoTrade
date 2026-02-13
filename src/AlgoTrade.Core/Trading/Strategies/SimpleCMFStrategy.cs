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
    /// CMF (Chaikin Money Flow) Stratejisi
    ///
    /// CMF Mantığı:
    /// - Alım/satım baskısını ölçen hacim indikatörü
    /// - -1 ile +1 arası değer alır
    /// - Pozitif: Alım baskısı, Negatif: Satım baskısı
    ///
    /// Trading Logic (choice=0):
    /// - AL: CMF pozitif eşiği (0.1) yukarı kesiyor
    /// - SAT: CMF negatif eşiği (-0.1) aşağı kesiyor
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - period: CMF periyodu (varsayılan 20)
    /// - positiveThreshold: Pozitif sinyal eşiği (varsayılan 0.1)
    /// - negativeThreshold: Negatif sinyal eşiği (varsayılan -0.1)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleCMFStrategy : BaseStrategy
    {
        public override string Name => "Simple CMF Strategy";

        private readonly int _period;
        private readonly double _positiveThreshold;
        private readonly double _negativeThreshold;
        private readonly int _choice;
        private double[]? _cmf;

        public SimpleCMFStrategy(int period = 20, double positiveThreshold = 0.1, double negativeThreshold = -0.1, int choice = 0)
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

        public SimpleCMFStrategy(List<StockData> data, IndicatorManager indicators, int period = 20, double positiveThreshold = 0.1, double negativeThreshold = -0.1, int choice = 0)
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
            // Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            _cmf = Indicators.VolumeInd.CMF(_period);

            LogManager.Log($"SimpleCMFStrategy initialized: Period={_period}, PositiveThreshold={_positiveThreshold}, NegativeThreshold={_negativeThreshold}");
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

            if (_cmf == null || _cmf.Length == 0)
                return TradeSignals.None;

            double currentCMF = _cmf[currentIndex];
            double prevCMF = _cmf[currentIndex - 1];

            if (double.IsNaN(currentCMF) || double.IsNaN(prevCMF))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = CMF threshold crossover, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: CMF pozitif eşiği yukarı kesiyor
                if (prevCMF <= _positiveThreshold && currentCMF > _positiveThreshold)
                {
                    buy = true;
                }

                // SAT: CMF negatif eşiği aşağı kesiyor
                if (prevCMF >= _negativeThreshold && currentCMF < _negativeThreshold)
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
                takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
            }

            if (Trader != null)
            {
                stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
            }

            if (skip) return TradeSignals.Skip;
            else if (flat) return TradeSignals.Flat;
            else if (takeProfit) return TradeSignals.TakeProfit;
            else if (stopLoss) return TradeSignals.StopLoss;
            else if (buy) return TradeSignals.Buy;
            else if (sell) return TradeSignals.Sell;

            return TradeSignals.None;
        }

        public double[]? GetCMF() => _cmf;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (_cmf != null && _cmf.Length > 0)
                indicators["CMF"] = _cmf;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
