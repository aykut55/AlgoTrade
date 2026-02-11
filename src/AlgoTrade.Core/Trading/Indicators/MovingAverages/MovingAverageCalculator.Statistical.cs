using System;
using System.Linq;

namespace AlgoTrade.Core.Trading.Indicators.MovingAverages
{
    /// <summary>
    /// Moving Average Calculator - Statistical MAs
    /// Median, Geometric, Zero-Lag variants
    /// </summary>
    public partial class MovingAverageCalculator
    {
        #region Statistical Moving Averages

        /// <summary>
        /// Median Moving Average - Uses median instead of mean
        /// More robust to outliers than SMA
        /// </summary>
        public double[] MEDIAN(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Extract period values
                var values = new double[period];
                for (int j = 0; j < period; j++)
                {
                    values[j] = source[i - j];
                }

                // Sort and find median
                Array.Sort(values);

                if (period % 2 == 0)
                {
                    // Even: average of two middle values
                    result[i] = (values[period / 2 - 1] + values[period / 2]) / 2.0;
                }
                else
                {
                    // Odd: middle value
                    result[i] = values[period / 2];
                }
            }

            return result;
        }

        /// <summary>
        /// Geometric Moving Average - Uses geometric mean
        /// Formula: GMA = (Price1 * Price2 * ... * PriceN)^(1/N)
        /// Useful for percentage changes and ratios
        /// </summary>
        public double[] GMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Calculate geometric mean using log transform to avoid overflow
                // GM = exp(mean(log(x)))
                var logSum = 0.0;
                var hasNegative = false;

                for (int j = 0; j < period; j++)
                {
                    var value = source[i - j];
                    if (value <= 0)
                    {
                        hasNegative = true;
                        break;
                    }
                    logSum += Math.Log(value);
                }

                if (hasNegative)
                {
                    result[i] = double.NaN; // GMA undefined for non-positive values
                }
                else
                {
                    result[i] = Math.Exp(logSum / period);
                }
            }

            return result;
        }

        /// <summary>
        /// Zero-Lag Simple Moving Average
        /// Applies lag compensation to SMA (similar to ZLEMA but for SMA)
        /// </summary>
        public double[] ZSMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var lag = (period - 1) / 2;
            var zsmaSource = new double[source.Length];

            // Apply lag compensation
            for (int i = 0; i < source.Length; i++)
            {
                if (i < lag)
                {
                    zsmaSource[i] = source[i];
                }
                else
                {
                    // Compensate for lag: source[i] + (source[i] - source[i-lag])
                    zsmaSource[i] = source[i] + (source[i] - source[i - lag]);
                }
            }

            // Apply SMA to compensated source
            return SMA(zsmaSource, period);
        }

        #endregion
    }
}
