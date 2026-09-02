using System.Collections.Generic;
using AlgoTrade.Core.Trading.Core;

namespace AlgoTrade.Core.Trading.Strategy
{
    /// <summary>
    /// Strategy interface
    /// All trading strategies must implement this interface
    /// </summary>
    public interface IStrategy : IDisposable
    {
        /// <summary>
        /// Strategy name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Strategy parameters
        /// </summary>
        Dictionary<string, object> Parameters { get; }

        /// <summary>
        /// Initialize strategy
        /// </summary>
        void OnInit();

        /// <summary>
        /// Called on each bar/step
        /// Returns trading signal
        /// </summary>
        TradeSignals OnStep(int currentIndex);

        /// <summary>
        /// Reset strategy to initial state
        /// </summary>
        void Reset();

        /// <summary>
        /// Get indicators for plotting
        /// Returns dictionary where key is indicator name, value is indicator data
        /// Example: { "MOST": mostArray, "EXMOV": exmovArray }
        /// Returns null if strategy has no indicators to plot
        /// </summary>
        Dictionary<string, double[]>? GetPlotIndicators();

        /// <summary>
        /// Opt sırasında bu parametre kombinasyonunun anlamlı olup olmadığını bildirir.
        /// false dönerse SingleTraderOptimizer backtest çalıştırmadan kombinasyonu atlar.
        /// Default true - stratejiler kendi mantığına göre override eder (örn. fast >= slow).
        /// </summary>
        bool IsValidParameterCombination() => true;
    }
}
