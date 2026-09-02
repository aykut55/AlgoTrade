using System.Collections.Generic;
using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading;

namespace AlgoTrade.Core.Trading.Strategy
{
    /// <summary>
    /// Base strategy class
    /// Provides common functionality for all strategies
    /// </summary>
    public abstract class BaseStrategy : IStrategy
    {
        #region Properties

        public abstract string Name { get; }
        public Dictionary<string, object> Parameters { get; protected set; }

        /// <summary>Market data</summary>
        protected List<StockData> Data { get; set; }

        /// <summary>Indicator manager</summary>
        protected IndicatorManager Indicators { get; set; }

        /// <summary>Trader instance - allows strategy to access trader's state and methods</summary>
        protected SingleTrader Trader { get; private set; }

        /// <summary>Is initialized?</summary>
        protected bool IsInitialized { get; set; }
        protected LogManager? Logger { get; private set; }

        #endregion

        #region Constructor

        protected BaseStrategy()
        {
            Parameters = new Dictionary<string, object>();
            IsInitialized = false;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize strategy with data and indicators
        /// </summary>
        public void Initialize(List<StockData> data, IndicatorManager indicators)
        {
            Data = data;
            Indicators = indicators;
            IsInitialized = true;
            OnInit();
        }

        /// <summary>
        /// Initialize strategy (override in derived classes)
        /// </summary>
        public virtual void OnInit()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Called on each bar/step
        /// </summary>
        public abstract TradeSignals OnStep(int currentIndex);

        /// <summary>
        /// Reset strategy
        /// </summary>
        public virtual void Reset()
        {
            IsInitialized = false;
        }

        /// <summary>
        /// Set parameter
        /// </summary>
        public void SetParameter(string key, object value)
        {
            Parameters[key] = value;
        }

        /// <summary>
        /// Get parameter
        /// </summary>
        public T GetParameter<T>(string key, T defaultValue = default)
        {
            if (Parameters.TryGetValue(key, out var value))
                return (T)value;
            return defaultValue;
        }

        /// <summary>
        /// Set trader instance
        /// Allows strategy to access trader's state, methods, and modules (KarAlZararKes, etc.)
        /// </summary>
        public void SetTrader(SingleTrader trader)
        {
            Trader = trader;
        }

        public void SetLogger(LogManager? logger)
        {
            Logger = logger;
        }

        protected void Log(string message)
        {
            if (Logger is not null)
            {
                Logger.LogRawInstance(message);
                return;
            }

            LogManager.LogRaw(message);
        }

        protected void LogWarning(string message)
        {
            if (Logger is not null)
            {
                Logger.LogRawInstance(message);
                return;
            }

            LogManager.LogRaw(message);
        }

        /// <summary>
        /// Get indicators for plotting (default implementation returns null)
        /// Override in derived classes to provide strategy-specific indicators
        /// </summary>
        public virtual Dictionary<string, double[]>? GetPlotIndicators()
        {
            return null;
        }

        /// <summary>
        /// Opt sırasında bu parametre kombinasyonunun anlamlı olup olmadığını bildirir (IStrategy).
        /// Default true - stratejiler kendi mantığına göre override eder (örn. fast &gt;= slow).
        /// </summary>
        public virtual bool IsValidParameterCombination() => true;

        public virtual void Dispose()
        {
            Reset();
        }

        #endregion
    }
}
