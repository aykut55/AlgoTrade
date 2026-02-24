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
    /// MA Cross (Golden/Death Cross) Stratejisi
    ///
    /// MA Cross Mantığı:
    /// - Hızlı MA ve yavaş MA kesişimleri
    /// - Golden Cross: Hızlı MA, yavaş MA'yı yukarı kesiyor
    /// - Death Cross: Hızlı MA, yavaş MA'yı aşağı kesiyor
    ///
    /// Trading Logic (choice=0):
    /// - AL: Golden Cross (hızlı MA > yavaş MA)
    /// - SAT: Death Cross (hızlı MA < yavaş MA)
    ///
    /// Trading Logic (choice=1):
    /// - (İleride eklenecek alternatif sinyal mantığı)
    ///
    /// Parametreler:
    /// - fastPeriod: Hızlı MA periyodu (varsayılan 10)
    /// - slowPeriod: Yavaş MA periyodu (varsayılan 20)
    /// - choice: Sinyal mantığı seçimi (varsayılan 0)
    /// </summary>
    public class SimpleMACrossStrategy : BaseStrategy
    {
        public override string Name => "Simple MA Cross Strategy";

        private readonly int _fastPeriod;
        private readonly int _slowPeriod;
        private readonly int _choice;
        private double[]? _fastMA;
        private double[]? _slowMA;

        public SimpleMACrossStrategy(int fastPeriod = 10, int slowPeriod = 20, int choice = 0)
        {
            _fastPeriod = fastPeriod;
            _slowPeriod = slowPeriod;
            _choice = choice;

            Parameters["FastPeriod"] = fastPeriod;
            Parameters["SlowPeriod"] = slowPeriod;
            Parameters["Choice"] = choice;
        }

        public SimpleMACrossStrategy(List<StockData> data, IndicatorManager indicators, int fastPeriod = 10, int slowPeriod = 20, int choice = 0)
        {
            _fastPeriod = fastPeriod;
            _slowPeriod = slowPeriod;
            _choice = choice;

            Parameters["FastPeriod"] = fastPeriod;
            Parameters["SlowPeriod"] = slowPeriod;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            var closes = Indicators.GetClosePrices();
            _fastMA = Indicators.MA.SMA(closes, _fastPeriod);
            _slowMA = Indicators.MA.SMA(closes, _slowPeriod);

            //Log($"SimpleMACrossStrategy initialized: FastPeriod={_fastPeriod}, SlowPeriod={_slowPeriod}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            if (currentIndex < _slowPeriod + 1)
                return TradeSignals.None;

            if (_fastMA == null || _slowMA == null || _fastMA.Length == 0)
                return TradeSignals.None;

            double currentFast = _fastMA[currentIndex];
            double prevFast = _fastMA[currentIndex - 1];
            double currentSlow = _slowMA[currentIndex];
            double prevSlow = _slowMA[currentIndex - 1];

            if (double.IsNaN(currentFast) || double.IsNaN(currentSlow) ||
                double.IsNaN(prevFast) || double.IsNaN(prevSlow))
                return TradeSignals.None;

            // ************************************************************************************************************************
            // choice: 0 = Golden/Death cross, 1 = (İleride eklenecek)
            if (_choice == 0)
            {
                // AL: Golden Cross - Hızlı MA, yavaş MA'yı yukarı kesiyor
                if (prevFast <= prevSlow && currentFast > currentSlow)
                {
                    buy = true;
                }

                // SAT: Death Cross - Hızlı MA, yavaş MA'yı aşağı kesiyor
                if (prevFast >= prevSlow && currentFast < currentSlow)
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

        public double[]? GetFastMA() => _fastMA;
        public double[]? GetSlowMA() => _slowMA;

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            var closes = Indicators.GetClosePrices();
            if (closes != null && closes.Length > 0)
                indicators["Close"] = closes;

            if (_fastMA != null && _fastMA.Length > 0)
                indicators["FastMA"] = _fastMA;

            if (_slowMA != null && _slowMA.Length > 0)
                indicators["SlowMA"] = _slowMA;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
