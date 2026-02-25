using System;
using System.Collections.Generic;
using System.Linq;
using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Trading.Indicators.Base;
using AlgoTrade.Core.Trading.Indicators.MovingAverages;
using AlgoTrade.Core.Trading.Indicators.Trend;
using AlgoTrade.Core.Trading.Indicators.Momentum;
using AlgoTrade.Core.Trading.Indicators.Volatility;
using AlgoTrade.Core.Trading.Indicators.Volume;
using AlgoTrade.Core.Trading.Indicators.PriceAction;
using AlgoTrade.Core.Trading.Indicators.SupportResistance;
using AlgoTrade.Core.Trading.Indicators.Utils;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Timer;
using AlgoTrade.Core;

namespace AlgoTrade.Core.Trading.Indicators
{
    /// <summary>
    /// Main Indicator Manager - Top-level class for all technical indicators
    ///
    /// Architecture:
    /// - Categorized sub-managers (MA, Trend, Momentum, Volatility, Volume, PriceAction)
    /// - Utility functions (PriceUtils)
    /// - Caching for performance
    /// - Logging and timing support
    ///
    /// Usage:
    ///   var manager = new IndicatorManager();
    ///   manager.Initialize(stockDataList);
    ///
    ///   var sma20 = manager.MA.SMA(closes, 20);
    ///   var rsi = manager.Momentum.RSI(closes, 14);
    ///   var supertrend = manager.Trend.SuperTrend(10, 3.0);
    /// </summary>
    public class IndicatorManager : MarketDataProvider, IDisposable
    {
        #region Fields

        private readonly IndicatorConfig _config;
        private readonly LogManager? _logManager;
        private readonly TimeManager? _timeManager;
        private readonly Dictionary<string, double[]> _cache;
        private bool _disposed;

        #endregion

        #region Properties

        /// <summary>Configuration</summary>
        public IndicatorConfig Config => _config;

        /// <summary>Number of bars/candles</summary>
        public int BarCount => Data?.Count ?? 0;

        // ==================== Sub-Managers (Category-based Access) ====================

        /// <summary>Moving Average Calculator (70+ MA types)</summary>
        public MovingAverageCalculator MA { get; }

        /// <summary>Trend Indicators (SuperTrend, MOST, ADX, Parabolic SAR, etc.)</summary>
        public TrendIndicators Trend { get; }

        /// <summary>Momentum Indicators (RSI, MACD, Stochastic, CCI, etc.)</summary>
        public MomentumIndicators Momentum { get; }

        /// <summary>Volatility Indicators (ATR, Bollinger Bands, Keltner Channel, etc.)</summary>
        public VolatilityIndicators Volatility { get; }

        /// <summary>Volume Indicators (OBV, VWAP, MFI, CMF, etc.)</summary>
        public VolumeIndicators VolumeInd { get; }

        /// <summary>Price Action Indicators (HH/LL, Swing Points, ZigZag, etc.)</summary>
        public PriceActionIndicators PriceAction { get; }

        /// <summary>Support/Resistance Indicators (Pivot Points, Fibonacci, etc.)</summary>
        public SupportResistanceIndicators SupportResistance { get; }

        /// <summary>Utility functions (HHV, LLV, StdDev, Sum, etc.)</summary>
        public PriceUtils Utils { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initialize Indicator Manager (empty data)
        /// </summary>
        /// <param name="config">Optional configuration</param>
        public IndicatorManager(IndicatorConfig? config = null)
            : this(new List<StockData>(), config)
        {
        }

        /// <summary>
        /// Initialize Indicator Manager
        /// </summary>
        /// <param name="data">Initial market data</param>
        /// <param name="config">Optional configuration</param>
        public IndicatorManager(List<StockData> data, IndicatorConfig? config = null)
        {
            _config = config ?? new IndicatorConfig();
            _cache = new Dictionary<string, double[]>();
            _data = data == null ? new List<StockData>() : new List<StockData>(data);

            // Setup logging
            if (_config.EnableDebugLogging)
            {
                _logManager = LogManager.Instance;
                _logManager.WriteLog("IndicatorManager initialized with debug logging enabled");
            }

            // Setup timing
            if (_config.EnablePerformanceTiming)
            {
                _timeManager = TimeManager.Instance;
            }

            // Initialize sub-managers
            MA = new MovingAverageCalculator(this, _config);
            Trend = new TrendIndicators(this, _config);
            Momentum = new MomentumIndicators(this, _config);
            Volatility = new VolatilityIndicators(this, _config);
            VolumeInd = new VolumeIndicators(this, _config);
            PriceAction = new PriceActionIndicators(this, _config);
            SupportResistance = new SupportResistanceIndicators(this, _config);
            Utils = new PriceUtils(_config.EnableDebugLogging);

            _logManager?.WriteLog("All sub-managers initialized successfully");
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize with market data
        /// </summary>
        /// <param name="data">Stock data list</param>
        /// <returns>Self for method chaining</returns>
        public IndicatorManager SetData(List<StockData> data)
        {
            if (data == null || data.Count == 0)
                throw new ArgumentException("Data cannot be null or empty", nameof(data));

            _data = data;
            _logManager?.WriteLog($"IndicatorManager initialized with {data.Count} bars");

            return this;
        }

        /// <summary>
        /// Reset cache and clear data
        /// </summary>
        public void Reset()
        {
            _cache.Clear();
            _data.Clear();
            _logManager?.WriteLog("IndicatorManager reset - cache and data cleared");
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Hesaplanmış (cache'lenmiş) tüm indikatörleri döndürür.
        /// PythonPlotter tarafından td.indicators'a aktarmak için kullanılır.
        /// </summary>
        public IReadOnlyDictionary<string, double[]> GetCachedIndicators() => _cache;

        /// <summary>
        /// Get cached result or calculate new (internal helper for sub-managers)
        /// </summary>
        internal double[] GetOrCalculate(string cacheKey, Func<double[]> calculator)
        {
            // Check cache
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                _logManager?.WriteLog($"Cache HIT: {cacheKey}");
                return cached;
            }

            _logManager?.WriteLog($"Cache MISS: {cacheKey} - calculating...");

            // Start timing
            string? timerId = null;
            if (_timeManager != null)
            {
                timerId = $"Calc_{cacheKey}";
                _timeManager.StartTimer(timerId);
            }

            // Calculate
            var result = calculator();

            // Stop timing
            if (_timeManager != null && timerId != null)
            {
                _timeManager.StopTimer(timerId);
                var elapsed = _timeManager.GetElapsedTime(timerId);
                _logManager?.WriteLog($"Calculated {cacheKey} in {elapsed}ms");
            }

            // Cache if not full
            if (_cache.Count < _config.CacheSize)
            {
                _cache[cacheKey] = result;
                _logManager?.WriteLog($"Cached result for {cacheKey}");
            }
            else
            {
                _logManager?.WriteLog($"Cache FULL ({_cache.Count}/{_config.CacheSize}) - not caching {cacheKey}");
            }

            return result;
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public Dictionary<string, int> GetCacheStats()
        {
            return new Dictionary<string, int>
            {
                ["CacheSize"] = _cache.Count,
                ["MaxCacheSize"] = _config.CacheSize,
                ["BarCount"] = BarCount
            };
        }

        /// <summary>
        /// Clear all cached indicators
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
            _logManager?.WriteLog("Cache cleared");
        }

        #endregion

        #region Helper Methods

        // Data extraction methods (GetClosePrices, GetOpenPrices, GetHighPrices, GetLowPrices, GetVolume, etc.)
        // are inherited from MarketDataProvider base class

        #endregion

        #region IDisposable

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            ClearCache();
            _data.Clear();

            _logManager?.WriteLog("IndicatorManager disposed");

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        #endregion

        #region String Representation

        /// <summary>
        /// String representation
        /// </summary>
        public override string ToString()
        {
            var stats = GetCacheStats();
            return $"IndicatorManager(Bars={stats["BarCount"]}, Cache={stats["CacheSize"]}/{stats["MaxCacheSize"]})";
        }

        #endregion
    }
}

/*
  ✅ All Priority Indicators Implemented

  1️⃣ Trend Indicators (TrendIndicators.cs) - SuperTrend, MOST, ADX, Parabolic SAR, Aroon, Vortex, Ichimoku
  2️⃣ Momentum Indicators (MomentumIndicators.cs) - RSI, MACD, Stochastic, CCI, Williams%R, ROC
  3️⃣ Volatility Indicators (VolatilityIndicators.cs) - ATR, Bollinger Bands, Keltner Channel, Donchian Channel
  4️⃣ Volume Indicators (VolumeIndicators.cs) - OBV, VWAP, MFI, CMF
  5️⃣ Price Action Indicators (PriceActionIndicators.cs) - HH/LL, Swing Points, ZigZag, Fractals
  6️⃣ Support/Resistance (SupportResistanceIndicators.cs) - Pivot Points, Fibonacci
*/
