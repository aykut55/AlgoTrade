using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategy;
using ScottPlot.TickGenerators.Financial;
using System;
using System.Collections.Generic;
using static Nessos.LinqOptimizer.Core.QueryExpr;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// Basit Moving Average Crossover Stratejisi
    /// Hızlı MA yavaş MA'yı yukarı keserse AL, aşağı keserse SAT
    /// </summary>
    public class SimpleMAStrategy : BaseStrategy
    {
        public override string Name => "Simple MA Crossover";

        private readonly int _fastPeriod;
        private readonly int _slowPeriod;
        private readonly int _choice;
        private double[]? _fastMA;
        private double[]? _slowMA;

        // Parametresiz constructor (eski kullanımlar için)
        public SimpleMAStrategy(int fastPeriod = 10, int slowPeriod = 20, int choice = 0)
        {
            _fastPeriod = fastPeriod;
            _slowPeriod = slowPeriod;
            _choice = choice;

            Parameters["FastPeriod"] = fastPeriod;
            Parameters["SlowPeriod"] = slowPeriod;
            Parameters["Choice"] = choice;
        }

        // Parametreli constructor (yeni kullanım)
        public SimpleMAStrategy(List<StockData> data, IndicatorManager indicators, int fastPeriod = 10, int slowPeriod = 20, int choice = 0)
        {
            _fastPeriod = fastPeriod;
            _slowPeriod = slowPeriod;
            _choice = choice;

            Parameters["FastPeriod"] = fastPeriod;
            Parameters["SlowPeriod"] = slowPeriod;
            Parameters["Choice"] = choice;

            // Initialize base strategy
            // Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            // Moving average'leri hesapla
            var closes = Indicators.GetClosePrices();
            _fastMA = Indicators.MA.SMA(closes, _fastPeriod);
            _slowMA = Indicators.MA.SMA(closes, _slowPeriod);

            LogManager.Log($"Strategy initialized: Fast={_fastPeriod}, Slow={_slowPeriod}, Choice={_choice}");
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy = false;
            bool sell = false;
            bool takeProfit = false;
            bool stopLoss = false;
            bool flat = false;
            bool skip = false;

            // İlk barlarda yeterli veri yok
            if (currentIndex < _slowPeriod)
                return TradeSignals.None;

            // Geçerli ve önceki MA değerleri
            double currentFastMA = _fastMA[currentIndex];
            double currentSlowMA = _slowMA[currentIndex];
            double prevFastMA = _fastMA[currentIndex - 1];
            double prevSlowMA = _slowMA[currentIndex - 1];

            // Golden Cross (Hızlı MA yukarı kesiyor) - AL sinyali
            if (prevFastMA <= prevSlowMA && currentFastMA > currentSlowMA)
            {
                buy = true;
            }

            // Death Cross (Hızlı MA aşağı kesiyor) - SAT sinyali
            if (prevFastMA >= prevSlowMA && currentFastMA < currentSlowMA)
            {
                sell = true;
            }

            // ÖRNEK: Trader referansını kullanarak kar al / zarar kes hesaplama
            // Trader property'si BaseStrategy.SetTrader() ile otomatik set edilir
            if (Trader != null)
            {
                takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
            }

            if (Trader != null)
            {
                stopLoss = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
            }

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
        /// Get indicators for plotting (IStrategy implementation)
        /// </summary>
        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            var indicators = new Dictionary<string, double[]>();

            if (_fastMA != null && _fastMA.Length > 0)
                indicators[$"Fast MA ({_fastPeriod})"] = _fastMA;

            if (_slowMA != null && _slowMA.Length > 0)
                indicators[$"Slow MA ({_slowPeriod})"] = _slowMA;

            return indicators.Count > 0 ? indicators : null;
        }
    }
}
