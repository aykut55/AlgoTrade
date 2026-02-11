using System;

namespace AlgoTrade.Core.Trading.Indicators.MovingAverages
{
    /// <summary>
    /// Moving Average Calculator - Advanced MAs (KAMA, VIDYA, ZLEMA, T3, ALMA, JMA)
    /// </summary>
    public partial class MovingAverageCalculator
    {
        #region Advanced Moving Averages

        /// <summary>
        /// Kaufman's Adaptive Moving Average - Adapts to market volatility
        /// </summary>
        public double[] KAMA(double[] source, int period, int fastPeriod = 2, int slowPeriod = 30)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            var fastAlpha = 2.0 / (fastPeriod + 1);
            var slowAlpha = 2.0 / (slowPeriod + 1);

            result[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                if (i < period)
                {
                    result[i] = source[i];
                    continue;
                }

                // Efficiency Ratio (ER)
                var change = Math.Abs(source[i] - source[i - period]);
                var volatility = 0.0;
                for (int j = 0; j < period; j++)
                {
                    volatility += Math.Abs(source[i - j] - source[i - j - 1]);
                }

                var er = volatility > 0 ? change / volatility : 0;

                // Smoothing Constant (SC)
                var sc = Math.Pow(er * (fastAlpha - slowAlpha) + slowAlpha, 2);

                // KAMA
                result[i] = result[i - 1] + sc * (source[i] - result[i - 1]);
            }

            return result;
        }

        /// <summary>
        /// Variable Index Dynamic Average - Uses CMO for variable smoothing
        /// </summary>
        public double[] VIDYA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            var alpha = 2.0 / (period + 1);

            result[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                if (i < period)
                {
                    result[i] = source[i];
                    continue;
                }

                // Chande Momentum Oscillator (CMO) for volatility
                var upSum = 0.0;
                var downSum = 0.0;

                for (int j = 0; j < period; j++)
                {
                    var mom = source[i - j] - source[i - j - 1];
                    if (mom > 0)
                        upSum += mom;
                    else
                        downSum += Math.Abs(mom);
                }

                var cmo = (upSum + downSum) > 0 ? Math.Abs((upSum - downSum) / (upSum + downSum)) : 0;

                // VIDYA
                result[i] = source[i] * alpha * cmo + result[i - 1] * (1 - alpha * cmo);
            }

            return result;
        }

        /// <summary>
        /// Zero-Lag Exponential Moving Average - Reduces lag by compensating for delay
        /// </summary>
        public double[] ZLEMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var lag = (period - 1) / 2;
            var zlemaSource = new double[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                if (i < lag)
                {
                    zlemaSource[i] = source[i];
                }
                else
                {
                    zlemaSource[i] = source[i] + (source[i] - source[i - lag]);
                }
            }

            return EMA(zlemaSource, period);
        }

        /// <summary>
        /// Tillson T3 Moving Average - Multiple EMA smoothing
        /// </summary>
        public double[] T3(double[] source, int period, double vFactor = 0.7)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var ema1 = EMA(source, period);
            var ema2 = EMA(ema1, period);
            var ema3 = EMA(ema2, period);
            var ema4 = EMA(ema3, period);
            var ema5 = EMA(ema4, period);
            var ema6 = EMA(ema5, period);

            var c1 = -1 * Math.Pow(vFactor, 3);
            var c2 = 3 * Math.Pow(vFactor, 2) + 3 * Math.Pow(vFactor, 3);
            var c3 = -6 * Math.Pow(vFactor, 2) - 3 * vFactor - 3 * Math.Pow(vFactor, 3);
            var c4 = 1 + 3 * vFactor + Math.Pow(vFactor, 3) + 3 * Math.Pow(vFactor, 2);

            var result = new double[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (double.IsNaN(ema6[i]))
                    result[i] = double.NaN;
                else
                    result[i] = c1 * ema6[i] + c2 * ema5[i] + c3 * ema4[i] + c4 * ema3[i];
            }

            return result;
        }

        /// <summary>
        /// Arnaud Legoux Moving Average - Gaussian distribution weighted
        /// </summary>
        public double[] ALMA(double[] source, int period, double sigma = 6.0, double offset = 0.85)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            var m = offset * (period - 1);
            var s = period / sigma;

            // Calculate weights
            var weights = new double[period];
            var weightSum = 0.0;

            for (int i = 0; i < period; i++)
            {
                weights[i] = Math.Exp(-Math.Pow(i - m, 2) / (2 * s * s));
                weightSum += weights[i];
            }

            // Normalize weights
            for (int i = 0; i < period; i++)
            {
                weights[i] /= weightSum;
            }

            // Calculate ALMA
            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                var alma = 0.0;
                for (int j = 0; j < period; j++)
                {
                    alma += source[i - period + 1 + j] * weights[j];
                }
                result[i] = alma;
            }

            return result;
        }

        /// <summary>
        /// Jurik Moving Average - Advanced smoothing algorithm
        /// </summary>
        public double[] JMA(double[] source, int period, double phase = 0, double power = 2)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            var phaseRatio = phase < -100 ? 0.5 : phase > 100 ? 2.5 : phase / 100.0 + 1.5;
            var beta = 0.45 * (period - 1) / (0.45 * (period - 1) + 2);
            var alpha = Math.Pow(beta, power);

            var ma1 = new double[source.Length];
            var det0 = new double[source.Length];
            var jma = new double[source.Length];

            ma1[0] = source[0];
            det0[0] = 0;
            jma[0] = source[0];

            for (int i = 1; i < source.Length; i++)
            {
                ma1[i] = (1 - alpha) * source[i] + alpha * ma1[i - 1];
                det0[i] = (source[i] - ma1[i]) * (1 - beta) + beta * det0[i - 1];

                var ma2 = ma1[i] + phaseRatio * det0[i];
                var det1 = (ma2 - jma[i - 1]) * Math.Pow(1 - alpha, 2) + Math.Pow(alpha, 2) * (i > 1 ? det0[i - 1] : 0);

                jma[i] = jma[i - 1] + det1;
                result[i] = jma[i];
            }

            return result;
        }

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Calculate multiple MAs with same method but different periods
        /// </summary>
        public double[][] CalculateBulk(double[] source, Base.MAMethod method, int[] periods)
        {
            var results = new double[periods.Length][];
            for (int i = 0; i < periods.Length; i++)
            {
                results[i] = Calculate(source, method, periods[i]);
            }
            return results;
        }

        /// <summary>
        /// Calculate single period with multiple methods
        /// </summary>
        public double[][] CalculateBulk(double[] source, Base.MAMethod[] methods, int period)
        {
            var results = new double[methods.Length][];
            for (int i = 0; i < methods.Length; i++)
            {
                results[i] = Calculate(source, methods[i], period);
            }
            return results;
        }

        #endregion
    }
}
