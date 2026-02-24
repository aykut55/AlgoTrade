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
    /// MACD (Moving Average Convergence Divergence) Stratejisi
    ///
    /// MACD Mantığı:
    /// - MACD Line = Fast EMA - Slow EMA
    /// - Signal Line = MACD'nin EMA'sı
    /// - Histogram = MACD - Signal
    ///
    /// Trading Logic (choice=0):
    /// - AL: MACD, Signal Line'ı yukarı kesiyor
    /// - SAT: MACD, Signal Line'ı aşağı kesiyor
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - fastPeriod: Hızlı EMA periyodu (varsayılan 12)
    /// - slowPeriod: Yavaş EMA periyodu (varsayılan 26)
    /// - signalPeriod: Signal line periyodu (varsayılan 9)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleMACDStrategy : BaseStrategy
    {
        public override string Name => "Simple MACD Strategy";

        private readonly int _fastPeriod;
        private readonly int _slowPeriod;
        private readonly int _signalPeriod;
        private readonly int _choice;
        private MACDResult? _macdResult;

        public SimpleMACDStrategy(int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9, int choice = 0)
        {
            _fastPeriod = fastPeriod;
            _slowPeriod = slowPeriod;
            _signalPeriod = signalPeriod;
            _choice = choice;

            Parameters["FastPeriod"] = fastPeriod;
            Parameters["SlowPeriod"] = slowPeriod;
            Parameters["SignalPeriod"] = signalPeriod;
            Parameters["Choice"] = choice;
        }

        public SimpleMACDStrategy(List<StockData> data, IndicatorManager indicators, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9, int choice = 0)
        {
            _fastPeriod = fastPeriod;
            _slowPeriod = slowPeriod;
            _signalPeriod = signalPeriod;
            _choice = choice;

            Parameters["FastPeriod"] = fastPeriod;
            Parameters["SlowPeriod"] = slowPeriod;
            Parameters["SignalPeriod"] = signalPeriod;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            var closes = Indicators.GetClosePrices();
            _macdResult = Indicators.Momentum.MACD(closes, _fastPeriod, _slowPeriod, _signalPeriod);

            //Log($"SimpleMACDStrategy initialized: Fast={_fastPeriod}, Slow={_slowPeriod}, Signal={_signalPeriod}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            if (currentIndex < _slowPeriod + _signalPeriod + 1)
                return TradeSignals.None;

            if (_macdResult == null || _macdResult.MACD.Length == 0)
                return TradeSignals.None;

            double currentMACD = _macdResult.MACD[currentIndex];
            double prevMACD = _macdResult.MACD[currentIndex - 1];
            double currentSignal = _macdResult.Signal[currentIndex];
            double prevSignal = _macdResult.Signal[currentIndex - 1];

            if (double.IsNaN(currentMACD) || double.IsNaN(prevMACD) || double.IsNaN(currentSignal) || double.IsNaN(prevSignal))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = MACD-Signal crossover, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: MACD, Signal'i yukarı kesiyor
                if (prevMACD <= prevSignal && currentMACD > currentSignal)
                {
                    buy = true;
                }

                // SAT: MACD, Signal'i aşağı kesiyor
                if (prevMACD >= prevSignal && currentMACD < currentSignal)
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

        public double[]? GetMACDLine() => _macdResult?.MACD;
        public double[]? GetSignalLine() => _macdResult?.Signal;
        public double[]? GetHistogram() => _macdResult?.Histogram;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (_macdResult?.MACD != null && _macdResult.MACD.Length > 0)
                indicators["MACD"] = _macdResult.MACD;

            if (_macdResult?.Signal != null && _macdResult.Signal.Length > 0)
                indicators["Signal"] = _macdResult.Signal;

            if (_macdResult?.Histogram != null && _macdResult.Histogram.Length > 0)
                indicators["Histogram"] = _macdResult.Histogram;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
