using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Indicators.Momentum.Results;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// Stochastic Osilatör Stratejisi
    ///
    /// Stochastic Mantığı:
    /// - %K = (Close - LLV) / (HHV - LLV) * 100
    /// - %D = %K'nın SMA'sı
    /// - 80 üstü: Aşırı alım, 20 altı: Aşırı satım
    ///
    /// Trading Logic (choice=0):
    /// - AL: %K, %D'yi yukarı kesiyor ve her ikisi 50'nin altında
    /// - SAT: %K, %D'yi aşağı kesiyor ve her ikisi 50'nin üstünde
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - kPeriod: %K periyodu (varsayılan 14)
    /// - dPeriod: %D periyodu (varsayılan 3)
    /// - centerLine: Merkez çizgi (varsayılan 50)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleStochasticStrategy : BaseStrategy
    {
        public override string Name => "Simple Stochastic Strategy";

        private readonly int _kPeriod;
        private readonly int _dPeriod;
        private readonly double _centerLine;
        private readonly int _choice;
        private StochasticResult? _stochResult;

        public SimpleStochasticStrategy(int kPeriod = 14, int dPeriod = 3, double centerLine = 50, int choice = 0)
        {
            _kPeriod = kPeriod;
            _dPeriod = dPeriod;
            _centerLine = centerLine;
            _choice = choice;

            Parameters["KPeriod"] = kPeriod;
            Parameters["DPeriod"] = dPeriod;
            Parameters["CenterLine"] = centerLine;
            Parameters["Choice"] = choice;
        }

        public SimpleStochasticStrategy(List<StockData> data, IndicatorManager indicators, int kPeriod = 14, int dPeriod = 3, double centerLine = 50, int choice = 0)
        {
            _kPeriod = kPeriod;
            _dPeriod = dPeriod;
            _centerLine = centerLine;
            _choice = choice;

            Parameters["KPeriod"] = kPeriod;
            Parameters["DPeriod"] = dPeriod;
            Parameters["CenterLine"] = centerLine;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            _stochResult = Indicators.Momentum.Stochastic(_kPeriod, _dPeriod);

            //Log($"SimpleStochasticStrategy initialized: K={_kPeriod}, D={_dPeriod}, CenterLine={_centerLine}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            if (currentIndex < _kPeriod + _dPeriod + 1)
                return TradeSignals.None;

            if (_stochResult == null || _stochResult.K.Length == 0)
                return TradeSignals.None;

            double currentK = _stochResult.K[currentIndex];
            double prevK = _stochResult.K[currentIndex - 1];
            double currentD = _stochResult.D[currentIndex];
            double prevD = _stochResult.D[currentIndex - 1];

            if (double.IsNaN(currentK) || double.IsNaN(prevK) || double.IsNaN(currentD) || double.IsNaN(prevD))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = %K-%D crossover with center filter, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: %K, %D'yi yukarı kesiyor ve her ikisi 50'nin altında
                if (prevK <= prevD && currentK > currentD && currentK < _centerLine && currentD < _centerLine)
                {
                    buy = true;
                }

                // SAT: %K, %D'yi aşağı kesiyor ve her ikisi 50'nin üstünde
                if (prevK >= prevD && currentK < currentD && currentK > _centerLine && currentD > _centerLine)
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

        public double[]? GetK() => _stochResult?.K;
        public double[]? GetD() => _stochResult?.D;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (_stochResult?.K != null && _stochResult.K.Length > 0)
                indicators["Stoch_K"] = _stochResult.K;

            if (_stochResult?.D != null && _stochResult.D.Length > 0)
                indicators["Stoch_D"] = _stochResult.D;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
