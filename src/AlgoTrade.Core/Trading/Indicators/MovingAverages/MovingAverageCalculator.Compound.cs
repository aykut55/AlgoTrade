using System;

namespace AlgoTrade.Core.Trading.Indicators.MovingAverages
{
    /// <summary>
    /// Moving Average Calculator - Compound MAs (Double/Triple variants)
    /// Double MA = MA(MA(source, period), period)
    /// Triple MA = MA(MA(MA(source, period), period), period)
    /// </summary>
    public partial class MovingAverageCalculator
    {
        #region Double Moving Averages

        /// <summary>
        /// Double Simple Moving Average - SMA of SMA
        /// Reduces lag, smoother than single SMA
        /// </summary>
        public double[] DSMA(double[] source, int period)
        {
            var sma1 = SMA(source, period);
            return SMA(sma1, period);
        }

        /// <summary>
        /// Double Weighted Moving Average - WMA of WMA
        /// </summary>
        public double[] DWMA(double[] source, int period)
        {
            var wma1 = WMA(source, period);
            return WMA(wma1, period);
        }

        /// <summary>
        /// Double Volume Weighted Moving Average - VWMA of VWMA
        /// </summary>
        public double[] DVWMA(double[] source, int period)
        {
            var vwma1 = VWMA(source, period);
            return VWMA(vwma1, period);
        }

        /// <summary>
        /// Double Hull Moving Average - Hull MA of Hull MA
        /// Extremely smooth and responsive
        /// </summary>
        public double[] DHULL(double[] source, int period)
        {
            var hull1 = HullMA(source, period);
            return HullMA(hull1, period);
        }

        /// <summary>
        /// Double Zero-Lag EMA - ZLEMA of ZLEMA
        /// Further reduces lag
        /// </summary>
        public double[] DZLEMA(double[] source, int period)
        {
            var zlema1 = ZLEMA(source, period);
            return ZLEMA(zlema1, period);
        }

        /// <summary>
        /// Double Smoothed Moving Average - SMMA of SMMA
        /// Also known as Double SSMA
        /// </summary>
        public double[] DSMMA(double[] source, int period)
        {
            var smma1 = SMMA(source, period);
            return SMMA(smma1, period);
        }

        /// <summary>
        /// Double SSMA - Same as DSMMA
        /// </summary>
        public double[] DSSMA(double[] source, int period)
        {
            return DSMMA(source, period);
        }

        #endregion

        #region Triple Moving Averages

        /// <summary>
        /// Triple Simple Moving Average - SMA of SMA of SMA
        /// Maximum smoothness, significant lag
        /// </summary>
        public double[] TSMA(double[] source, int period)
        {
            var sma1 = SMA(source, period);
            var sma2 = SMA(sma1, period);
            return SMA(sma2, period);
        }

        /// <summary>
        /// Triple Weighted Moving Average - WMA of WMA of WMA
        /// </summary>
        public double[] TWMA(double[] source, int period)
        {
            var wma1 = WMA(source, period);
            var wma2 = WMA(wma1, period);
            return WMA(wma2, period);
        }

        /// <summary>
        /// Triple Volume Weighted Moving Average - VWMA of VWMA of VWMA
        /// </summary>
        public double[] TVWMA(double[] source, int period)
        {
            var vwma1 = VWMA(source, period);
            var vwma2 = VWMA(vwma1, period);
            return VWMA(vwma2, period);
        }

        /// <summary>
        /// Triple Hull Moving Average - Hull MA of Hull MA of Hull MA
        /// Ultra-smooth, very responsive
        /// </summary>
        public double[] THULL(double[] source, int period)
        {
            var hull1 = HullMA(source, period);
            var hull2 = HullMA(hull1, period);
            return HullMA(hull2, period);
        }

        /// <summary>
        /// Triple Zero-Lag EMA - ZLEMA of ZLEMA of ZLEMA
        /// Minimal lag with maximum smoothness
        /// </summary>
        public double[] TZLEMA(double[] source, int period)
        {
            var zlema1 = ZLEMA(source, period);
            var zlema2 = ZLEMA(zlema1, period);
            return ZLEMA(zlema2, period);
        }

        /// <summary>
        /// Triple Smoothed Moving Average - SMMA of SMMA of SMMA
        /// Also known as Triple SSMA
        /// </summary>
        public double[] TSMMA(double[] source, int period)
        {
            var smma1 = SMMA(source, period);
            var smma2 = SMMA(smma1, period);
            return SMMA(smma2, period);
        }

        /// <summary>
        /// Triple SSMA - Same as TSMMA
        /// </summary>
        public double[] TSSMA(double[] source, int period)
        {
            return TSMMA(source, period);
        }

        #endregion
    }
}
