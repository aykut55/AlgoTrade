using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Indicators.Base
{
    /// <summary>
    /// Configuration container for indicators
    /// Matches Python IndicatorConfig implementation
    /// </summary>
    public class IndicatorConfig
    {
        /// <summary>
        /// Common MA periods based on Fibonacci sequence
        /// </summary>
        public List<int> FibonacciPeriods { get; set; } = new List<int>
        {
            3, 5, 8, 13, 21, 34, 55, 89, 144, 233
        };

        /// <summary>
        /// Common standard periods
        /// </summary>
        public List<int> CommonPeriods { get; set; } = new List<int>
        {
            5, 10, 15, 20, 30, 45, 50, 100, 200, 500, 1000
        };

        /// <summary>
        /// Cache size for performance optimization (default: 128)
        /// </summary>
        public int CacheSize { get; set; } = 128;

        /// <summary>
        /// Enable debug logging for indicator calculations
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Enable performance timing measurements
        /// </summary>
        public bool EnablePerformanceTiming { get; set; } = true;

        /// <summary>
        /// Default RSI period
        /// </summary>
        public int DefaultRSIPeriod { get; set; } = 14;

        /// <summary>
        /// Default MACD parameters
        /// </summary>
        public int DefaultMACDFastPeriod { get; set; } = 12;
        public int DefaultMACDSlowPeriod { get; set; } = 26;
        public int DefaultMACDSignalPeriod { get; set; } = 9;

        /// <summary>
        /// Default ATR period
        /// </summary>
        public int DefaultATRPeriod { get; set; } = 14;

        /// <summary>
        /// Default Bollinger Bands parameters
        /// </summary>
        public int DefaultBBPeriod { get; set; } = 20;
        public double DefaultBBStdDev { get; set; } = 2.0;

        /// <summary>
        /// Default SuperTrend parameters
        /// </summary>
        public int DefaultSuperTrendPeriod { get; set; } = 10;
        public double DefaultSuperTrendMultiplier { get; set; } = 3.0;

        /// <summary>
        /// Default MOST parameters
        /// </summary>
        public int DefaultMOSTPeriod { get; set; } = 21;
        public double DefaultMOSTPercent { get; set; } = 1.0;
    }
}
