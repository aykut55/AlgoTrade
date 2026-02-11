using System;

namespace AlgoTrade.Core.Trading.Indicators.MovingAverages
{
    /// <summary>
    /// Moving Average Calculator - Exotic/Advanced MAs
    /// Fractal, MESA, McGinley, Volatility-adjusted, and other complex algorithms
    /// </summary>
    public partial class MovingAverageCalculator
    {
        #region Exotic Moving Averages

        /// <summary>
        /// Fractal Adaptive Moving Average
        /// Adapts to market fractality (complexity/noise)
        /// Simplified implementation using price range fractal dimension
        /// </summary>
        public double[] FRAMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0 || period < 10)
                throw new ArgumentException("Period must be at least 10", nameof(period));

            var result = new double[source.Length];
            result[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                if (i < period)
                {
                    result[i] = source[i];
                    continue;
                }

                // Calculate fractal dimension using price range
                var n1 = period / 2;
                var n2 = period - n1;

                // High-Low range for each half
                var high1 = source[i - n2];
                var low1 = source[i - n2];
                for (int j = i - n2; j <= i - 1; j++)
                {
                    high1 = Math.Max(high1, source[j]);
                    low1 = Math.Min(low1, source[j]);
                }

                var high2 = source[i - period];
                var low2 = source[i - period];
                for (int j = i - period; j < i - n2; j++)
                {
                    high2 = Math.Max(high2, source[j]);
                    low2 = Math.Min(low2, source[j]);
                }

                var highFull = Math.Max(high1, high2);
                var lowFull = Math.Min(low1, low2);

                var range1 = high1 - low1;
                var range2 = high2 - low2;
                var rangeFull = highFull - lowFull;

                // Fractal dimension
                double dimension;
                if (rangeFull > 0 && range1 + range2 > 0)
                {
                    dimension = (Math.Log(range1 + range2) - Math.Log(rangeFull)) / Math.Log(2);
                }
                else
                {
                    dimension = 1.5; // Default middle value
                }

                dimension = Math.Max(1.0, Math.Min(2.0, dimension));

                // Adaptive alpha based on fractal dimension
                var alpha = Math.Exp(-4.6 * (dimension - 1.0));
                alpha = Math.Max(0.01, Math.Min(1.0, alpha));

                result[i] = alpha * source[i] + (1 - alpha) * result[i - 1];
            }

            return result;
        }

        /// <summary>
        /// MESA Adaptive Moving Average
        /// Uses Hilbert Transform to detect market cycle
        /// Simplified version using price momentum
        /// </summary>
        public double[] MAMA(double[] source, double fastLimit = 0.5, double slowLimit = 0.05)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));

            var result = new double[source.Length];
            var fama = new double[source.Length]; // Following Adaptive MA

            result[0] = source[0];
            fama[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                if (i < 6)
                {
                    result[i] = source[i];
                    fama[i] = source[i];
                    continue;
                }

                // Simplified phase calculation using momentum
                var momentum = source[i] - source[i - 4];
                var phase = Math.Atan(momentum / (source[i] + 1e-10));

                // Adaptive alpha based on phase
                var alpha = fastLimit / (1 + Math.Abs(phase) * 10);
                alpha = Math.Max(slowLimit, Math.Min(fastLimit, alpha));

                // MAMA
                result[i] = alpha * source[i] + (1 - alpha) * result[i - 1];

                // FAMA (Following Adaptive MA)
                fama[i] = 0.5 * alpha * result[i] + (1 - 0.5 * alpha) * fama[i - 1];
            }

            return result;
        }

        /// <summary>
        /// McGinley Dynamic
        /// Self-adjusting moving average that tracks price better than EMA
        /// Formula: MD[i] = MD[i-1] + (Price - MD[i-1]) / (Period * (Price/MD[i-1])^4)
        /// </summary>
        public double[] MCGINLEY(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];

            // Initialize with first value
            if (source.Length > 0)
                result[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                if (result[i - 1] == 0)
                {
                    result[i] = source[i];
                    continue;
                }

                var priceRatio = source[i] / result[i - 1];
                var adjustmentFactor = period * Math.Pow(priceRatio, 4);
                adjustmentFactor = Math.Max(1.0, adjustmentFactor); // Prevent division issues

                result[i] = result[i - 1] + (source[i] - result[i - 1]) / adjustmentFactor;
            }

            return result;
        }

        /// <summary>
        /// Volatility Adjusted Moving Average
        /// EMA with period adjusted by volatility (ATR-based)
        /// </summary>
        public double[] VAMA(double[] source, int period)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized");

            var highs = _manager.GetHighPrices();
            var lows = _manager.GetLowPrices();
            var closes = _manager.GetClosePrices();

            var result = new double[source.Length];
            result[0] = source[0];

            // Calculate ATR for volatility
            var atr = new double[source.Length];
            for (int i = 1; i < source.Length; i++)
            {
                var tr = Math.Max(highs[i] - lows[i],
                         Math.Max(Math.Abs(highs[i] - closes[i - 1]),
                                  Math.Abs(lows[i] - closes[i - 1])));

                atr[i] = i == 1 ? tr : (atr[i - 1] * (period - 1) + tr) / period;
            }

            for (int i = 1; i < source.Length; i++)
            {
                // Volatility ratio
                var volatilityRatio = source[i] != 0 ? atr[i] / source[i] : 0;

                // Adaptive period: higher volatility = shorter period
                var adaptivePeriod = period / (1 + volatilityRatio * 10);
                adaptivePeriod = Math.Max(2, Math.Min(period * 2, adaptivePeriod));

                var alpha = 2.0 / (adaptivePeriod + 1);
                result[i] = alpha * source[i] + (1 - alpha) * result[i - 1];
            }

            return result;
        }

        /// <summary>
        /// Alpha-Decreasing Exponential Moving Average
        /// EMA with decreasing alpha over time (reduces sensitivity as data ages)
        /// </summary>
        public double[] ADEMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            result[0] = source[0];

            var baseAlpha = 2.0 / (period + 1);

            for (int i = 1; i < source.Length; i++)
            {
                // Alpha decreases as we progress
                var decayFactor = Math.Exp(-0.01 * i / period);
                var alpha = baseAlpha * (0.5 + 0.5 * decayFactor);

                result[i] = alpha * source[i] + (1 - alpha) * result[i - 1];
            }

            return result;
        }

        /// <summary>
        /// Exponentially Deviating Moving Average
        /// EMA that emphasizes deviation from mean
        /// </summary>
        public double[] EDMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var ema = EMA(source, period);
            var result = new double[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                if (double.IsNaN(ema[i]))
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Deviation weight
                var deviation = Math.Abs(source[i] - ema[i]);
                var avgValue = (source[i] + ema[i]) / 2;
                var deviationRatio = avgValue != 0 ? deviation / avgValue : 0;

                // Emphasize deviation
                result[i] = ema[i] + deviation * deviationRatio;
            }

            return result;
        }

        /// <summary>
        /// Ehlers Dynamic Smoothed Moving Average
        /// Uses Ehlers' smoothing algorithm
        /// </summary>
        public double[] EDSMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            var alpha = 2.0 / (period + 1);

            // Ehlers smoothing with 2-pole filter
            for (int i = 0; i < source.Length; i++)
            {
                if (i < 2)
                {
                    result[i] = source[i];
                    continue;
                }

                // 2-pole super smoother
                var a1 = Math.Exp(-1.414 * Math.PI / period);
                var b1 = 2 * a1 * Math.Cos(1.414 * Math.PI / period);
                var c2 = b1;
                var c3 = -a1 * a1;
                var c1 = 1 - c2 - c3;

                result[i] = c1 * source[i] + c2 * result[i - 1] + c3 * (i >= 2 ? result[i - 2] : 0);
            }

            return result;
        }

        /// <summary>
        /// Ahrens Moving Average - Simplified implementation
        /// </summary>
        public double[] AHMA(double[] source, int period)
        {
            // Ahrens MA is similar to adaptive EMA with momentum
            return KAMA(source, period, 2, 30); // Use KAMA as approximation
        }

        /// <summary>
        /// Exponential Hull Moving Average
        /// Hull MA calculated with EMA instead of WMA
        /// </summary>
        public double[] EHMA(double[] source, int period)
        {
            var halfPeriod = Math.Max(1, period / 2);
            var sqrtPeriod = Math.Max(1, (int)Math.Sqrt(period));

            var emaHalf = EMA(source, halfPeriod);
            var emaFull = EMA(source, period);

            // Calculate 2*EMA(n/2) - EMA(n)
            var hullSource = new double[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (double.IsNaN(emaHalf[i]) || double.IsNaN(emaFull[i]))
                    hullSource[i] = double.NaN;
                else
                    hullSource[i] = 2 * emaHalf[i] - emaFull[i];
            }

            return EMA(hullSource, sqrtPeriod);
        }

        /// <summary>
        /// Adaptive Least Squares Moving Average
        /// LSMA with adaptive period based on price action
        /// </summary>
        public double[] ALSMA(double[] source, int period)
        {
            var result = new double[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Calculate volatility for adaptive period
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
                var cv = mean != 0 ? Math.Sqrt(Math.Max(0, variance)) / Math.Abs(mean) : 0.5;

                // Adaptive period
                var adaptivePeriod = (int)(period * (1 - cv));
                adaptivePeriod = Math.Max(5, Math.Min(period, adaptivePeriod));

                // Calculate LSMA with adaptive period
                result[i] = LSMA(source, adaptivePeriod)[i];
            }

            return result;
        }

        /// <summary>
        /// Adaptive Autonomous Recursive Moving Average - Simplified
        /// </summary>
        public double[] AARMA(double[] source, int period)
        {
            // Similar to KAMA but with different adaptation
            return KAMA(source, period);
        }

        /// <summary>
        /// McNicholl Moving Average - Simplified implementation
        /// </summary>
        public double[] MCMA(double[] source, int period)
        {
            // Similar to T3 with different coefficients
            return T3(source, period, 0.618); // Golden ratio
        }

        /// <summary>
        /// Leo Moving Average - Simplified implementation
        /// </summary>
        public double[] LEOMA(double[] source, int period)
        {
            // Combination of EMA and SMA
            var ema = EMA(source, period);
            var sma = SMA(source, period);

            var result = new double[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (double.IsNaN(ema[i]) || double.IsNaN(sma[i]))
                    result[i] = double.NaN;
                else
                    result[i] = (ema[i] + sma[i]) / 2.0;
            }

            return result;
        }

        /// <summary>
        /// Corrective Moving Average
        /// Self-correcting based on recent error
        /// </summary>
        public double[] CMA(double[] source, int period)
        {
            var result = new double[source.Length];
            var ema = EMA(source, period);

            result[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                if (double.IsNaN(ema[i]))
                {
                    result[i] = source[i];
                    continue;
                }

                // Calculate error
                var error = source[i] - ema[i];

                // Corrective factor
                var correction = error * 0.1; // 10% of error

                result[i] = ema[i] + correction;
            }

            return result;
        }

        /// <summary>
        /// Correlation Moving Average
        /// Weighted by correlation with price trend
        /// </summary>
        public double[] CORMA(double[] source, int period)
        {
            var result = new double[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Calculate correlation coefficient
                var meanX = (period - 1) / 2.0;
                var meanY = 0.0;
                for (int j = 0; j < period; j++)
                {
                    meanY += source[i - period + 1 + j];
                }
                meanY /= period;

                var numerator = 0.0;
                var denomX = 0.0;
                var denomY = 0.0;

                for (int j = 0; j < period; j++)
                {
                    var x = j - meanX;
                    var y = source[i - period + 1 + j] - meanY;
                    numerator += x * y;
                    denomX += x * x;
                    denomY += y * y;
                }

                var correlation = denomX * denomY > 0 ? numerator / Math.Sqrt(denomX * denomY) : 0;

                // Weight by correlation
                var weight = (correlation + 1) / 2; // Normalize to 0-1

                result[i] = weight * source[i] + (1 - weight) * meanY;
            }

            return result;
        }

        /// <summary>
        /// Auto-Line - Automatic trend line
        /// </summary>
        public double[] AUTOL(double[] source, int period)
        {
            // Use LSMA as automatic trend line
            return LSMA(source, period);
        }

        /// <summary>
        /// Optimized Exponential Moving Average
        /// EMA with optimized alpha based on market conditions
        /// </summary>
        public double[] XEMA(double[] source, int period)
        {
            var result = new double[source.Length];
            result[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                if (i < period)
                {
                    result[i] = source[i];
                    continue;
                }

                // Calculate efficiency
                var change = Math.Abs(source[i] - source[i - period]);
                var volatility = 0.0;
                for (int j = 1; j <= period; j++)
                {
                    volatility += Math.Abs(source[i - j + 1] - source[i - j]);
                }

                var efficiency = volatility > 0 ? change / volatility : 0;

                // Optimized alpha
                var fastAlpha = 2.0 / (2 + 1);
                var slowAlpha = 2.0 / (period + 1);
                var optimizedAlpha = slowAlpha + efficiency * (fastAlpha - slowAlpha);

                result[i] = optimizedAlpha * source[i] + (1 - optimizedAlpha) * result[i - 1];
            }

            return result;
        }

        #endregion
    }
}
