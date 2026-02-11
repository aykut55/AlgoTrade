using System;

namespace AlgoTrade.Core.Trading.Indicators.MovingAverages
{
    /// <summary>
    /// Moving Average Calculator - Specialized MAs
    /// Square Root Weighted, Sine Weighted, Range-based, RSI-based, etc.
    /// </summary>
    public partial class MovingAverageCalculator
    {
        #region Specialized Weighted Moving Averages

        /// <summary>
        /// Square Root Weighted Moving Average
        /// Weights: √1, √2, √3, ..., √period
        /// More emphasis on recent data than linear WMA
        /// </summary>
        public double[] SRWMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];

            // Calculate weights: √1 + √2 + ... + √period
            var weights = new double[period];
            var weightSum = 0.0;
            for (int i = 0; i < period; i++)
            {
                weights[i] = Math.Sqrt(i + 1);
                weightSum += weights[i];
            }

            // Normalize weights
            for (int i = 0; i < period; i++)
            {
                weights[i] /= weightSum;
            }

            // Calculate SRWMA
            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                var srwma = 0.0;
                for (int j = 0; j < period; j++)
                {
                    srwma += source[i - period + 1 + j] * weights[j];
                }
                result[i] = srwma;
            }

            return result;
        }

        /// <summary>
        /// Symmetrically (Sine-) Weighted Moving Average
        /// Uses sine wave for symmetric weighting around middle period
        /// </summary>
        public double[] SWMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];

            // Calculate sine weights: sin(π * i / (period - 1))
            var weights = new double[period];
            var weightSum = 0.0;
            for (int i = 0; i < period; i++)
            {
                weights[i] = Math.Sin(Math.PI * i / (period - 1));
                weightSum += weights[i];
            }

            // Normalize weights
            for (int i = 0; i < period; i++)
            {
                weights[i] /= weightSum;
            }

            // Calculate SWMA
            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                var swma = 0.0;
                for (int j = 0; j < period; j++)
                {
                    swma += source[i - period + 1 + j] * weights[j];
                }
                result[i] = swma;
            }

            return result;
        }

        /// <summary>
        /// Elastic Volume Weighted Moving Average
        /// VWMA adjusted by volatility (uses standard deviation)
        /// </summary>
        public double[] EVWMA(double[] source, int period)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized");

            var volume = _manager.GetVolume();
            var result = new double[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Calculate volatility (standard deviation)
                var sumSq = 0.0;
                var sum = 0.0;
                for (int j = 0; j < period; j++)
                {
                    var val = source[i - j];
                    sum += val;
                    sumSq += val * val;
                }
                var mean = sum / period;
                var variance = (sumSq / period) - (mean * mean);
                var volatility = Math.Sqrt(Math.Max(0, variance));

                // Elastic factor: higher volatility = more weight to volume
                var elasticFactor = Math.Min(volatility / mean, 1.0);

                // Calculate EVWMA with elastic adjustment
                var sumPriceVolume = 0.0;
                var sumVolume = 0.0;

                for (int j = 0; j < period; j++)
                {
                    var adjustedVolume = volume[i - j] * (1 + elasticFactor);
                    sumPriceVolume += source[i - j] * adjustedVolume;
                    sumVolume += adjustedVolume;
                }

                result[i] = sumVolume > 0 ? sumPriceVolume / sumVolume : double.NaN;
            }

            return result;
        }

        /// <summary>
        /// Regularized Exponential Moving Average
        /// EMA with regularization term to reduce overfitting
        /// </summary>
        public double[] REGMA(double[] source, int period, double lambda = 0.1)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            var multiplier = 2.0 / (period + 1);

            // First value
            if (source.Length > 0)
                result[0] = source[0];

            // Apply regularized EMA
            for (int i = 1; i < source.Length; i++)
            {
                // Regularization: pull towards simple mean
                var ema = (source[i] - result[i - 1]) * multiplier + result[i - 1];
                var regularization = lambda * (SMA(source, Math.Min(period, i + 1))[i] - ema);
                result[i] = ema + regularization;
            }

            return result;
        }

        /// <summary>
        /// Range Exponential Moving Average
        /// EMA weighted by True Range (high volatility = lower weight)
        /// </summary>
        public double[] REMA(double[] source, int period)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            var result = new double[source.Length];
            result[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                // Calculate True Range
                var tr = Math.Max(highs[i] - lows[i],
                         Math.Max(Math.Abs(highs[i] - closes[i - 1]),
                                  Math.Abs(lows[i] - closes[i - 1])));

                // Adaptive alpha: lower during high volatility
                var baseAlpha = 2.0 / (period + 1);
                var trRatio = tr / source[i];
                var adaptiveAlpha = baseAlpha / (1 + trRatio);

                result[i] = (source[i] - result[i - 1]) * adaptiveAlpha + result[i - 1];
            }

            return result;
        }

        /// <summary>
        /// Repulsion Moving Average
        /// Adapts based on distance between price and MA (repulsion effect)
        /// </summary>
        public double[] REPMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            var sma = SMA(source, period);

            result[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                if (double.IsNaN(sma[i]))
                {
                    result[i] = source[i];
                    continue;
                }

                // Repulsion factor based on distance
                var distance = Math.Abs(source[i] - sma[i]);
                var avgPrice = (source[i] + sma[i]) / 2;
                var repulsionFactor = distance / avgPrice;

                // Adaptive smoothing
                var alpha = 2.0 / (period + 1);
                var adaptiveAlpha = alpha * (1 - Math.Min(repulsionFactor, 0.5));

                result[i] = (source[i] - result[i - 1]) * adaptiveAlpha + result[i - 1];
            }

            return result;
        }

        /// <summary>
        /// RSI-based Moving Average
        /// Uses RSI to weight the moving average (high RSI = more weight to recent)
        /// </summary>
        public double[] RSIMA(double[] source, int period, int rsiPeriod = 14)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            result[0] = source[0];

            // Calculate RSI for weighting
            var diff = new double[source.Length];
            for (int i = 1; i < source.Length; i++)
            {
                diff[i] = source[i] - source[i - 1];
            }

            // Simple RSI calculation
            for (int i = 1; i < source.Length; i++)
            {
                if (i < rsiPeriod)
                {
                    result[i] = source[i];
                    continue;
                }

                // Calculate gains and losses
                var gains = 0.0;
                var losses = 0.0;
                for (int j = 0; j < rsiPeriod; j++)
                {
                    if (diff[i - j] > 0)
                        gains += diff[i - j];
                    else
                        losses += Math.Abs(diff[i - j]);
                }

                var avgGain = gains / rsiPeriod;
                var avgLoss = losses / rsiPeriod;
                var rsi = avgLoss == 0 ? 100 : 100 - (100 / (1 + avgGain / avgLoss));

                // Use RSI to adapt alpha (RSI > 50 = more weight to recent)
                var baseAlpha = 2.0 / (period + 1);
                var rsiWeight = rsi / 100.0;
                var adaptiveAlpha = baseAlpha * (0.5 + rsiWeight);

                result[i] = (source[i] - result[i - 1]) * adaptiveAlpha + result[i - 1];
            }

            return result;
        }

        #endregion

        #region Triangular Variants

        /// <summary>
        /// Exponential Triangular Moving Average
        /// Triangular pattern applied to EMA
        /// </summary>
        public double[] ETMA(double[] source, int period)
        {
            var halfPeriod = (period + 1) / 2;
            var ema1 = EMA(source, halfPeriod);
            return EMA(ema1, halfPeriod);
        }

        /// <summary>
        /// Triangular Exponential Moving Average
        /// EMA applied in triangular pattern
        /// </summary>
        public double[] TREMA(double[] source, int period)
        {
            var halfPeriod = (period + 1) / 2;
            var sma1 = SMA(source, halfPeriod);
            return EMA(sma1, halfPeriod);
        }

        /// <summary>
        /// Triangular Simple Moving Average
        /// Same as Triangular() method - SMA of SMA
        /// </summary>
        public double[] TRSMA(double[] source, int period)
        {
            return Triangular(source, period);
        }

        /// <summary>
        /// Triple Hull Moving Average (Alternative implementation)
        /// Same as THULL but can have different parameters
        /// </summary>
        public double[] THMA(double[] source, int period)
        {
            return THULL(source, period);
        }

        #endregion
    }
}
