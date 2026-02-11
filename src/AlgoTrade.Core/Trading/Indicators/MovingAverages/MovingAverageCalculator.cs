using System;
using System.Linq;
using AlgoTrade.Core.Trading.Indicators.Base;

namespace AlgoTrade.Core.Trading.Indicators.MovingAverages
{
    /// <summary>
    /// Moving Average Calculator - Supports 70+ MA types
    /// Partial class - Basic MAs (SMA, EMA, WMA, Hull, DEMA, TEMA, VWMA, LSMA)
    /// </summary>
    public partial class MovingAverageCalculator
    {
        private readonly IndicatorManager _manager;
        private readonly IndicatorConfig _config;

        public MovingAverageCalculator(IndicatorManager manager, IndicatorConfig config)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        #region Generic MA Calculator

        /// <summary>
        /// Calculate Moving Average using specified method
        /// </summary>
        /// <param name="source">Price series</param>
        /// <param name="method">MA calculation method</param>
        /// <param name="period">Calculation period</param>
        /// <returns>MA values</returns>
        public double[] Calculate(double[] source, MAMethod method, int period)
        {
            var cacheKey = $"{method}_{period}_{GetHashCode(source)}";
            return _manager.GetOrCalculate(cacheKey, () =>
            {
                return method switch
                {
                    // Basic MAs
                    MAMethod.SIMPLE => SMA(source, period),
                    MAMethod.EMA => EMA(source, period),
                    MAMethod.WMA => WMA(source, period),
                    MAMethod.HULL => HullMA(source, period),
                    MAMethod.DEMA => DEMA(source, period),
                    MAMethod.TEMA => TEMA(source, period),
                    MAMethod.VWMA => VWMA(source, period),
                    MAMethod.LSMA => LSMA(source, period),
                    MAMethod.TRIANGULAR => Triangular(source, period),
                    MAMethod.WILDER => Wilder(source, period),
                    MAMethod.SMMA => SMMA(source, period),

                    // Advanced MAs
                    MAMethod.KAMA => KAMA(source, period),
                    MAMethod.VIDYA => VIDYA(source, period),
                    MAMethod.ZLEMA => ZLEMA(source, period),
                    MAMethod.T3 => T3(source, period),
                    MAMethod.ALMA => ALMA(source, period),
                    MAMethod.JMA => JMA(source, period),

                    // Compound MAs (Double)
                    MAMethod.DSMA => DSMA(source, period),
                    MAMethod.DWMA => DWMA(source, period),
                    MAMethod.DVWMA => DVWMA(source, period),
                    MAMethod.DHULL => DHULL(source, period),
                    MAMethod.DZLEMA => DZLEMA(source, period),
                    MAMethod.DSMMA => DSMMA(source, period),
                    MAMethod.DSSMA => DSSMA(source, period),

                    // Compound MAs (Triple)
                    MAMethod.TSMA => TSMA(source, period),
                    MAMethod.TWMA => TWMA(source, period),
                    MAMethod.TVWMA => TVWMA(source, period),
                    MAMethod.THULL => THULL(source, period),
                    MAMethod.TZLEMA => TZLEMA(source, period),
                    MAMethod.TSMMA => TSMMA(source, period),
                    MAMethod.TSSMA => TSSMA(source, period),

                    // Statistical MAs
                    MAMethod.MEDIAN => MEDIAN(source, period),
                    MAMethod.GMA => GMA(source, period),
                    MAMethod.ZSMA => ZSMA(source, period),

                    // Specialized MAs
                    MAMethod.SRWMA => SRWMA(source, period),
                    MAMethod.SWMA => SWMA(source, period),
                    MAMethod.EVWMA => EVWMA(source, period),
                    MAMethod.REGMA => REGMA(source, period),
                    MAMethod.REMA => REMA(source, period),
                    MAMethod.REPMA => REPMA(source, period),
                    MAMethod.RSIMA => RSIMA(source, period),
                    MAMethod.ETMA => ETMA(source, period),
                    MAMethod.TREMA => TREMA(source, period),
                    MAMethod.TRSMA => TRSMA(source, period),
                    MAMethod.THMA => THMA(source, period),

                    // Advanced2 MAs
                    MAMethod.COVWMA => COVWMA(source, period),
                    MAMethod.COVWEMA => COVWEMA(source, period),
                    MAMethod.FAMA => FAMA(source, period),
                    MAMethod.TIME_SERIES => TIME_SERIES(source, period),

                    // Exotic MAs
                    MAMethod.FRAMA => FRAMA(source, period),
                    MAMethod.MAMA => MAMA(source),
                    MAMethod.MCGINLEY => MCGINLEY(source, period),
                    MAMethod.VAMA => VAMA(source, period),
                    MAMethod.ADEMA => ADEMA(source, period),
                    MAMethod.EDMA => EDMA(source, period),
                    MAMethod.EDSMA => EDSMA(source, period),
                    MAMethod.AHMA => AHMA(source, period),
                    MAMethod.EHMA => EHMA(source, period),
                    MAMethod.ALSMA => ALSMA(source, period),
                    MAMethod.AARMA => AARMA(source, period),
                    MAMethod.MCMA => MCMA(source, period),
                    MAMethod.LEOMA => LEOMA(source, period),
                    MAMethod.CMA => CMA(source, period),
                    MAMethod.CORMA => CORMA(source, period),
                    MAMethod.AUTOL => AUTOL(source, period),
                    MAMethod.XEMA => XEMA(source, period),

                    _ => throw new NotImplementedException($"MA method '{method}' not yet implemented")
                };
            });
        }

        #endregion

        #region Basic Moving Averages

        /// <summary>
        /// Simple Moving Average
        /// </summary>
        public double[] SMA(double[] source, int period)
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

                var sum = 0.0;
                for (int j = 0; j < period; j++)
                {
                    sum += source[i - j];
                }
                result[i] = sum / period;
            }

            return result;
        }

        /// <summary>
        /// Exponential Moving Average
        /// </summary>
        public double[] EMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            var multiplier = 2.0 / (period + 1);

            // First EMA = SMA
            var sum = 0.0;
            for (int i = 0; i < Math.Min(period, source.Length); i++)
            {
                sum += source[i];
                if (i < period - 1)
                    result[i] = double.NaN;
            }

            if (source.Length >= period)
            {
                result[period - 1] = sum / period;

                // Subsequent EMAs
                for (int i = period; i < source.Length; i++)
                {
                    result[i] = (source[i] - result[i - 1]) * multiplier + result[i - 1];
                }
            }

            return result;
        }

        /// <summary>
        /// Weighted Moving Average
        /// </summary>
        public double[] WMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            var weightSum = (period * (period + 1)) / 2.0; // Sum of 1+2+3+...+period

            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                var wma = 0.0;
                for (int j = 0; j < period; j++)
                {
                    wma += source[i - j] * (period - j);
                }
                result[i] = wma / weightSum;
            }

            return result;
        }

        /// <summary>
        /// Hull Moving Average - Combines WMAs for reduced lag
        /// Formula: HMA = WMA(2*WMA(n/2) - WMA(n), sqrt(n))
        /// </summary>
        public double[] HullMA(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var halfPeriod = Math.Max(1, period / 2);
            var sqrtPeriod = Math.Max(1, (int)Math.Sqrt(period));

            var wmaHalf = WMA(source, halfPeriod);
            var wmaFull = WMA(source, period);

            // Calculate 2*WMA(n/2) - WMA(n)
            var hullSource = new double[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (double.IsNaN(wmaHalf[i]) || double.IsNaN(wmaFull[i]))
                    hullSource[i] = double.NaN;
                else
                    hullSource[i] = 2 * wmaHalf[i] - wmaFull[i];
            }

            // Apply WMA with sqrt(n) period
            return WMA(hullSource, sqrtPeriod);
        }

        /// <summary>
        /// Double Exponential Moving Average - Reduces lag
        /// Formula: DEMA = 2*EMA - EMA(EMA)
        /// </summary>
        public double[] DEMA(double[] source, int period)
        {
            var ema1 = EMA(source, period);
            var ema2 = EMA(ema1, period);

            var result = new double[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (double.IsNaN(ema1[i]) || double.IsNaN(ema2[i]))
                    result[i] = double.NaN;
                else
                    result[i] = 2 * ema1[i] - ema2[i];
            }

            return result;
        }

        /// <summary>
        /// Triple Exponential Moving Average - Further reduces lag
        /// Formula: TEMA = 3*EMA - 3*EMA(EMA) + EMA(EMA(EMA))
        /// </summary>
        public double[] TEMA(double[] source, int period)
        {
            var ema1 = EMA(source, period);
            var ema2 = EMA(ema1, period);
            var ema3 = EMA(ema2, period);

            var result = new double[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (double.IsNaN(ema1[i]) || double.IsNaN(ema2[i]) || double.IsNaN(ema3[i]))
                    result[i] = double.NaN;
                else
                    result[i] = 3 * (ema1[i] - ema2[i]) + ema3[i];
            }

            return result;
        }

        /// <summary>
        /// Volume Weighted Moving Average
        /// </summary>
        public double[] VWMA(double[] source, int period)
        {
            if (!_manager.IsInitialized)
                throw new InvalidOperationException("Manager not initialized");

            var volume = _manager.GetVolume();
            if (source.Length != volume.Length)
                throw new ArgumentException("Source and volume arrays must have same length");

            var result = new double[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                var sumPriceVolume = 0.0;
                var sumVolume = 0.0;

                for (int j = 0; j < period; j++)
                {
                    sumPriceVolume += source[i - j] * volume[i - j];
                    sumVolume += volume[i - j];
                }

                result[i] = sumVolume > 0 ? sumPriceVolume / sumVolume : double.NaN;
            }

            return result;
        }

        /// <summary>
        /// Least Squares Moving Average (Linear Regression)
        /// </summary>
        public double[] LSMA(double[] source, int period)
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

                // Linear regression
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

                result[i] = intercept + slope * (period - 1);
            }

            return result;
        }

        /// <summary>
        /// Triangular Moving Average - SMA of SMA
        /// </summary>
        public double[] Triangular(double[] source, int period)
        {
            var smaPeriod = (period + 1) / 2;
            var sma1 = SMA(source, smaPeriod);
            return SMA(sma1, smaPeriod);
        }

        /// <summary>
        /// Wilder's Smoothing (used in RSI, ATR)
        /// </summary>
        public double[] Wilder(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            var alpha = 1.0 / period;

            // First value = SMA
            var sum = 0.0;
            for (int i = 0; i < Math.Min(period, source.Length); i++)
            {
                sum += source[i];
                if (i < period - 1)
                    result[i] = double.NaN;
            }

            if (source.Length >= period)
            {
                result[period - 1] = sum / period;

                // Wilder's smoothing
                for (int i = period; i < source.Length; i++)
                {
                    result[i] = alpha * source[i] + (1 - alpha) * result[i - 1];
                }
            }

            return result;
        }

        /// <summary>
        /// Smoothed Moving Average (same as Wilder's)
        /// </summary>
        public double[] SMMA(double[] source, int period)
        {
            return Wilder(source, period);
        }

        #endregion

        #region Helpers

        private static int GetHashCode(double[] array)
        {
            if (array == null || array.Length == 0)
                return 0;

            unchecked
            {
                int hash = 17;
                hash = hash * 23 + array.Length;
                hash = hash * 23 + array[0].GetHashCode();
                if (array.Length > 1)
                    hash = hash * 23 + array[^1].GetHashCode();
                return hash;
            }
        }

        #endregion
    }
}
