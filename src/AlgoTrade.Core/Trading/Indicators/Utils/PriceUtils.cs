using System;
using System.Linq;
using AlgoTrade.Core.Logging;

namespace AlgoTrade.Core.Trading.Indicators.Utils
{
    /// <summary>
    /// Utility functions for price calculations (building blocks for indicators)
    /// </summary>
    public class PriceUtils
    {
        private readonly LogManager? _logManager;
        private readonly bool _enableLogging;

        public PriceUtils(bool enableLogging = false)
        {
            _enableLogging = enableLogging;
            if (_enableLogging)
            {
                _logManager = LogManager.Instance;
            }
        }

        /// <summary>
        /// Highest High Value - Returns the highest value in the last N periods
        /// </summary>
        /// <param name="source">Data array</param>
        /// <param name="period">Lookback period</param>
        /// <returns>Array of HHV values</returns>
        public double[] HHV(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source array cannot be null or empty", nameof(source));

            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            _logManager?.WriteLog($"PriceUtils.HHV: period={period}, length={source.Length}");

            var result = new double[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                var max = double.MinValue;
                for (int j = 0; j < period; j++)
                {
                    max = Math.Max(max, source[i - j]);
                }
                result[i] = max;
            }

            return result;
        }

        /// <summary>
        /// Lowest Low Value - Returns the lowest value in the last N periods
        /// </summary>
        /// <param name="source">Data array</param>
        /// <param name="period">Lookback period</param>
        /// <returns>Array of LLV values</returns>
        public double[] LLV(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source array cannot be null or empty", nameof(source));

            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            _logManager?.WriteLog($"PriceUtils.LLV: period={period}, length={source.Length}");

            var result = new double[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                var min = double.MaxValue;
                for (int j = 0; j < period; j++)
                {
                    min = Math.Min(min, source[i - j]);
                }
                result[i] = min;
            }

            return result;
        }

        /// <summary>
        /// HY (İdeal Sistem Sistem.HY(Periyot) ile AYNI) - fiyatın, ÖNCEDEN HESAPLANMIŞ HHV
        /// serisine ham yüzde mesafesi: (source-hhv)/hhv*100. (HHV-LLV) aralığına göre normalize
        /// ETMEZ (RSI/Stochastic %K gibi 0-100 osilatör DEĞİL) - sadece HHV'ye göre. HHV mevcut
        /// barı da kapsadığı için fiyat HHV'yi hiç aşamaz -> sonuç hep &lt;= 0.
        /// </summary>
        /// <param name="source">Fiyat serisi (HY/LY hesabındaki "fiyat" tarafı, örn. Close)</param>
        /// <param name="hhv">Önceden hesaplanmış HHV serisi (bkz. HHV())</param>
        /// <returns>Array of HY values</returns>
        public double[] HY(double[] source, double[] hhv)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source array cannot be null or empty", nameof(source));
            if (hhv == null || hhv.Length != source.Length)
                throw new ArgumentException("hhv array must be non-null and same length as source", nameof(hhv));

            _logManager?.WriteLog($"PriceUtils.HY: length={source.Length}");

            var result = new double[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = (double.IsNaN(hhv[i]) || hhv[i] == 0)
                    ? double.NaN
                    : (source[i] - hhv[i]) / hhv[i] * 100;
            }
            return result;
        }

        /// <summary>
        /// LY (İdeal Sistem Sistem.LY(Periyot) ile AYNI) - fiyatın, ÖNCEDEN HESAPLANMIŞ LLV
        /// serisine ham yüzde mesafesi: (source-llv)/llv*100. (HHV-LLV) aralığına göre normalize
        /// ETMEZ - sadece LLV'ye göre. LLV mevcut barı da kapsadığı için fiyat LLV'nin hiç altına
        /// inemez -> sonuç hep &gt;= 0.
        /// </summary>
        /// <param name="source">Fiyat serisi (HY/LY hesabındaki "fiyat" tarafı, örn. Close)</param>
        /// <param name="llv">Önceden hesaplanmış LLV serisi (bkz. LLV())</param>
        /// <returns>Array of LY values</returns>
        public double[] LY(double[] source, double[] llv)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source array cannot be null or empty", nameof(source));
            if (llv == null || llv.Length != source.Length)
                throw new ArgumentException("llv array must be non-null and same length as source", nameof(llv));

            _logManager?.WriteLog($"PriceUtils.LY: length={source.Length}");

            var result = new double[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = (double.IsNaN(llv[i]) || llv[i] == 0)
                    ? double.NaN
                    : (source[i] - llv[i]) / llv[i] * 100;
            }
            return result;
        }

        /// <summary>
        /// HHLL (İdeal Sistem Sistem.HHLL(Periyot) ile AYNI) - ÖNCEDEN HESAPLANMIŞ HHV ile LLV
        /// serileri ARASINDAKİ FARK (kanal genişliği): hhv-llv.
        /// </summary>
        /// <param name="hhv">Önceden hesaplanmış HHV serisi (bkz. HHV())</param>
        /// <param name="llv">Önceden hesaplanmış LLV serisi (bkz. LLV())</param>
        /// <returns>Array of HHLL values</returns>
        public double[] HHLL(double[] hhv, double[] llv)
        {
            if (hhv == null || hhv.Length == 0)
                throw new ArgumentException("hhv array cannot be null or empty", nameof(hhv));
            if (llv == null || llv.Length != hhv.Length)
                throw new ArgumentException("llv array must be non-null and same length as hhv", nameof(llv));

            _logManager?.WriteLog($"PriceUtils.HHLL: length={hhv.Length}");

            var result = new double[hhv.Length];
            for (int i = 0; i < hhv.Length; i++)
            {
                result[i] = (double.IsNaN(hhv[i]) || double.IsNaN(llv[i])) ? double.NaN : hhv[i] - llv[i];
            }
            return result;
        }

        /// <summary>
        /// Sum of values over period
        /// </summary>
        /// <param name="source">Data array</param>
        /// <param name="period">Lookback period</param>
        /// <returns>Array of sum values</returns>
        public double[] Sum(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source array cannot be null or empty", nameof(source));

            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            _logManager?.WriteLog($"PriceUtils.Sum: period={period}, length={source.Length}");

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
                result[i] = sum;
            }

            return result;
        }

        /// <summary>
        /// Standard Deviation over period
        /// </summary>
        /// <param name="source">Data array</param>
        /// <param name="period">Lookback period</param>
        /// <returns>Array of standard deviation values</returns>
        public double[] StdDev(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source array cannot be null or empty", nameof(source));

            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            _logManager?.WriteLog($"PriceUtils.StdDev: period={period}, length={source.Length}");

            var result = new double[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Calculate mean
                var sum = 0.0;
                for (int j = 0; j < period; j++)
                {
                    sum += source[i - j];
                }
                var mean = sum / period;

                // Calculate variance
                var variance = 0.0;
                for (int j = 0; j < period; j++)
                {
                    var diff = source[i - j] - mean;
                    variance += diff * diff;
                }
                variance /= period;

                result[i] = Math.Sqrt(variance);
            }

            return result;
        }

        /// <summary>
        /// Mean (average) over period
        /// </summary>
        /// <param name="source">Data array</param>
        /// <param name="period">Lookback period</param>
        /// <returns>Array of mean values</returns>
        public double[] Mean(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source array cannot be null or empty", nameof(source));

            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var sumArray = Sum(source, period);
            var result = new double[sumArray.Length];

            for (int i = 0; i < sumArray.Length; i++)
            {
                result[i] = double.IsNaN(sumArray[i]) ? double.NaN : sumArray[i] / period;
            }

            return result;
        }

        /// <summary>
        /// Variance over period
        /// </summary>
        /// <param name="source">Data array</param>
        /// <param name="period">Lookback period</param>
        /// <returns>Array of variance values</returns>
        public double[] Variance(double[] source, int period)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source array cannot be null or empty", nameof(source));

            if (period <= 0)
                throw new ArgumentException("Period must be positive", nameof(period));

            var result = new double[source.Length];
            var meanArray = Mean(source, period);

            for (int i = 0; i < source.Length; i++)
            {
                if (i < period - 1 || double.IsNaN(meanArray[i]))
                {
                    result[i] = double.NaN;
                    continue;
                }

                var variance = 0.0;
                for (int j = 0; j < period; j++)
                {
                    var diff = source[i - j] - meanArray[i];
                    variance += diff * diff;
                }
                result[i] = variance / period;
            }

            return result;
        }

        /// <summary>
        /// Calculate True Range
        /// </summary>
        /// <param name="high">High prices</param>
        /// <param name="low">Low prices</param>
        /// <param name="close">Close prices</param>
        /// <returns>Array of True Range values</returns>
        public double[] TrueRange(double[] high, double[] low, double[] close)
        {
            if (high == null || low == null || close == null)
                throw new ArgumentException("Price arrays cannot be null");

            if (high.Length != low.Length || high.Length != close.Length)
                throw new ArgumentException("Price arrays must have the same length");

            _logManager?.WriteLog($"PriceUtils.TrueRange: length={high.Length}");

            var result = new double[high.Length];
            result[0] = high[0] - low[0]; // First bar

            for (int i = 1; i < high.Length; i++)
            {
                var tr1 = high[i] - low[i];
                var tr2 = Math.Abs(high[i] - close[i - 1]);
                var tr3 = Math.Abs(low[i] - close[i - 1]);

                result[i] = Math.Max(tr1, Math.Max(tr2, tr3));
            }

            return result;
        }

        /// <summary>
        /// Calculate differences (current - previous)
        /// </summary>
        /// <param name="source">Data array</param>
        /// <returns>Array of differences</returns>
        public double[] Diff(double[] source)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source array cannot be null or empty", nameof(source));

            var result = new double[source.Length];
            result[0] = 0; // First element has no difference

            for (int i = 1; i < source.Length; i++)
            {
                result[i] = source[i] - source[i - 1];
            }

            return result;
        }

        /// <summary>
        /// Calculate percentage change
        /// </summary>
        /// <param name="source">Data array</param>
        /// <returns>Array of percentage changes</returns>
        public double[] PercentChange(double[] source)
        {
            if (source == null || source.Length == 0)
                throw new ArgumentException("Source array cannot be null or empty", nameof(source));

            var result = new double[source.Length];
            result[0] = 0; // First element has no change

            for (int i = 1; i < source.Length; i++)
            {
                if (source[i - 1] != 0)
                {
                    result[i] = ((source[i] - source[i - 1]) / source[i - 1]) * 100.0;
                }
                else
                {
                    result[i] = 0;
                }
            }

            return result;
        }
    }
}
