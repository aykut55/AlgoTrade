using System;

namespace AlgoTrade.Core.Trading.Indicators.MovingAverages
{
    /// <summary>
    /// Moving Average Calculator - Advanced Statistical MAs
    /// Coefficient of Variation, Following Adaptive, Time Series
    /// </summary>
    public partial class MovingAverageCalculator
    {
        #region Advanced Statistical Moving Averages

        /// <summary>
        /// Coefficient of Variation Weighted Moving Average
        /// Uses CV (StdDev/Mean) to weight the moving average
        /// Lower CV = more weight (more stable periods)
        /// </summary>
        public double[] COVWMA(double[] source, int period)
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

                // Calculate mean and standard deviation for the period
                var sum = 0.0;
                var sumSq = 0.0;
                for (int j = 0; j < period; j++)
                {
                    var val = source[i - j];
                    sum += val;
                    sumSq += val * val;
                }

                var mean = sum / period;
                var variance = (sumSq / period) - (mean * mean);
                var stdDev = Math.Sqrt(Math.Max(0, variance));

                // Coefficient of Variation
                var cv = mean != 0 ? stdDev / Math.Abs(mean) : 0;

                // Weight calculation: inverse of CV (lower CV = higher weight)
                var weights = new double[period];
                var weightSum = 0.0;

                for (int j = 0; j < period; j++)
                {
                    // Calculate local CV for each sub-window
                    var localSum = 0.0;
                    var localSumSq = 0.0;
                    var localPeriod = Math.Min(j + 1, period / 2);

                    for (int k = 0; k < localPeriod; k++)
                    {
                        var val = source[i - j - k];
                        localSum += val;
                        localSumSq += val * val;
                    }

                    var localMean = localSum / localPeriod;
                    var localVar = (localSumSq / localPeriod) - (localMean * localMean);
                    var localStdDev = Math.Sqrt(Math.Max(0, localVar));
                    var localCV = localMean != 0 ? localStdDev / Math.Abs(localMean) : 0.5;

                    // Inverse weighting: lower CV = higher weight
                    weights[j] = 1.0 / (1.0 + localCV);
                    weightSum += weights[j];
                }

                // Normalize weights and calculate weighted average
                var covwma = 0.0;
                for (int j = 0; j < period; j++)
                {
                    covwma += source[i - j] * (weights[j] / weightSum);
                }

                result[i] = covwma;
            }

            return result;
        }

        /// <summary>
        /// Coefficient of Variation Weighted Exponential Moving Average
        /// EMA with CV-based adaptive smoothing
        /// </summary>
        public double[] COVWEMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            result[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                if (i < period)
                {
                    result[i] = source[i];
                    continue;
                }

                // Calculate CV for recent period
                var sum = 0.0;
                var sumSq = 0.0;
                for (int j = 0; j < period; j++)
                {
                    var val = source[i - j];
                    sum += val;
                    sumSq += val * val;
                }

                var mean = sum / period;
                var variance = (sumSq / period) - (mean * mean);
                var stdDev = Math.Sqrt(Math.Max(0, variance));
                var cv = mean != 0 ? stdDev / Math.Abs(mean) : 0.5;

                // Adaptive alpha: lower CV = slower EMA (more smoothing)
                var baseAlpha = 2.0 / (period + 1);
                var adaptiveAlpha = baseAlpha * (1.0 + cv);

                result[i] = (source[i] - result[i - 1]) * adaptiveAlpha + result[i - 1];
            }

            return result;
        }

        /// <summary>
        /// Following Adaptive Moving Average
        /// Adapts to follow price movement with momentum
        /// </summary>
        public double[] FAMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            result[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                if (i < period)
                {
                    result[i] = source[i];
                    continue;
                }

                // Calculate momentum
                var momentum = source[i] - source[i - period];
                var avgPrice = (source[i] + source[i - period]) / 2.0;
                var momentumRatio = avgPrice != 0 ? Math.Abs(momentum / avgPrice) : 0;

                // Following factor: higher momentum = faster following
                var followingFactor = Math.Min(momentumRatio, 1.0);

                // Adaptive alpha
                var baseAlpha = 2.0 / (period + 1);
                var adaptiveAlpha = baseAlpha * (1.0 + followingFactor);

                // Direction aware: follow the direction of momentum
                var direction = momentum >= 0 ? 1 : -1;
                var adjustment = followingFactor * Math.Abs(momentum) * 0.1 * direction;

                result[i] = (source[i] - result[i - 1]) * adaptiveAlpha + result[i - 1] + adjustment;
            }

            return result;
        }

        /// <summary>
        /// Time Series Moving Average (Time Series Forecast)
        /// Uses linear regression forecast as the moving average
        /// Similar to LSMA but projects the trend forward
        /// </summary>
        public double[] TIME_SERIES(double[] source, int period)
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

                // Linear regression for the period
                var sumX = 0.0;
                var sumY = 0.0;
                var sumXY = 0.0;
                var sumX2 = 0.0;

                for (int j = 0; j < period; j++)
                {
                    var x = j;
                    var y = source[i - period + 1 + j];

                    sumX += x;
                    sumY += y;
                    sumXY += x * y;
                    sumX2 += x * x;
                }

                var slope = (period * sumXY - sumX * sumY) / (period * sumX2 - sumX * sumX);
                var intercept = (sumY - slope * sumX) / period;

                // Forecast one period ahead (time series forecast)
                result[i] = intercept + slope * period;
            }

            return result;
        }

        #endregion
    }
}
