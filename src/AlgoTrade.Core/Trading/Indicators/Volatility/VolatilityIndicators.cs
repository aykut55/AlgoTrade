using System;
using AlgoTrade.Core.Trading.Indicators.Base;
using AlgoTrade.Core.Trading.Indicators.Volatility.Results;

namespace AlgoTrade.Core.Trading.Indicators.Volatility
{
    /// <summary>
    /// Volatility Indicators - ATR, Bollinger Bands, Keltner Channel, etc.
    /// TODO: Implement remaining indicators
    /// </summary>
    public class VolatilityIndicators
    {
        private readonly IndicatorManager _manager;
        private readonly IndicatorConfig _config;

        public VolatilityIndicators(IndicatorManager manager, IndicatorConfig config)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Average True Range
        /// Measures market volatility by calculating average of true ranges
        /// TR = max(High - Low, |High - Previous Close|, |Low - Previous Close|)
        /// ATR = Wilder's smoothing of TR
        /// </summary>
        public double[] ATR(int period = 14)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            // Calculate True Range for each bar
            var tr = _manager.Utils.TrueRange(highs, lows, closes);

            // Apply Wilder's smoothing to True Range
            var atr = _manager.MA.Wilder(tr, period);

            return atr;
        }

        /// <summary>
        /// Bollinger Bands
        /// Volatility bands placed above and below a moving average
        /// Middle Band = SMA
        /// Upper Band = Middle + (StdDev * multiplier)
        /// Lower Band = Middle - (StdDev * multiplier)
        /// </summary>
        public BollingerBandsResult BollingerBands(double[] source, int period = 20, double stdDevMultiplier = 2.0)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));
            if (stdDevMultiplier <= 0)
                throw new ArgumentException("Standard deviation multiplier must be positive", nameof(stdDevMultiplier));

            // Calculate Middle Band (SMA)
            var middle = _manager.MA.SMA(source, period);

            // Calculate Standard Deviation
            var stdDev = _manager.Utils.StdDev(source, period);

            // Calculate Upper and Lower Bands
            var upper = new double[source.Length];
            var lower = new double[source.Length];
            var bandwidth = new double[source.Length];
            var percentB = new double[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                if (double.IsNaN(middle[i]) || double.IsNaN(stdDev[i]))
                {
                    upper[i] = double.NaN;
                    lower[i] = double.NaN;
                    bandwidth[i] = double.NaN;
                    percentB[i] = double.NaN;
                }
                else
                {
                    upper[i] = middle[i] + (stdDev[i] * stdDevMultiplier);
                    lower[i] = middle[i] - (stdDev[i] * stdDevMultiplier);

                    // Bandwidth = (Upper - Lower) / Middle
                    bandwidth[i] = middle[i] != 0 ? (upper[i] - lower[i]) / middle[i] : double.NaN;

                    // %B = (Close - Lower) / (Upper - Lower)
                    var range = upper[i] - lower[i];
                    percentB[i] = range != 0 ? (source[i] - lower[i]) / range : double.NaN;
                }
            }

            return new BollingerBandsResult(upper, middle, lower, bandwidth, percentB, period, stdDevMultiplier);
        }

        /// <summary>
        /// Keltner Channel
        /// Volatility-based envelope indicator
        /// Middle = EMA of Close
        /// Upper = EMA + (ATR * multiplier)
        /// Lower = EMA - (ATR * multiplier)
        /// Similar to Bollinger Bands but uses ATR instead of standard deviation
        /// </summary>
        public KeltnerChannelResult KeltnerChannel(int period = 20, double multiplier = 2.0)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));
            if (multiplier <= 0)
                throw new ArgumentException("Multiplier must be positive", nameof(multiplier));

            var closes = _manager.GetClosePrices();

            // Calculate middle line (EMA of close)
            var middle = _manager.MA.EMA(closes, period);

            // Calculate ATR
            var atr = ATR(period);

            // Calculate upper and lower bands
            var upper = new double[closes.Length];
            var lower = new double[closes.Length];

            for (int i = 0; i < closes.Length; i++)
            {
                if (double.IsNaN(middle[i]) || double.IsNaN(atr[i]))
                {
                    upper[i] = double.NaN;
                    lower[i] = double.NaN;
                }
                else
                {
                    upper[i] = middle[i] + (atr[i] * multiplier);
                    lower[i] = middle[i] - (atr[i] * multiplier);
                }
            }

            return new KeltnerChannelResult(upper, middle, lower, period, multiplier);
        }

        /// <summary>
        /// Donchian Channel
        /// Price channel based on highest high and lowest low
        /// Upper = Highest High over period
        /// Lower = Lowest Low over period
        /// Middle = (Upper + Lower) / 2
        /// Breakout indicator - price above upper = buy signal, below lower = sell signal
        /// </summary>
        public DonchianChannelResult DonchianChannel(int period = 20)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized with data");
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();

            // Calculate upper band (highest high)
            var upper = _manager.Utils.HHV(highs, period);

            // Calculate lower band (lowest low)
            var lower = _manager.Utils.LLV(lows, period);

            // Calculate middle line
            var middle = new double[highs.Length];
            for (int i = 0; i < highs.Length; i++)
            {
                if (double.IsNaN(upper[i]) || double.IsNaN(lower[i]))
                {
                    middle[i] = double.NaN;
                }
                else
                {
                    middle[i] = (upper[i] + lower[i]) / 2.0;
                }
            }

            return new DonchianChannelResult(upper, middle, lower, period);
        }
    }
}
