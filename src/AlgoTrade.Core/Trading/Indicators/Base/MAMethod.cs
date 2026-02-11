using System;

namespace AlgoTrade.Core.Trading.Indicators.Base
{
    /// <summary>
    /// Moving Average calculation methods
    /// Supports 70+ different MA types (matching Python implementation)
    /// </summary>
    public enum MAMethod
    {
        // ==================== Basic MAs ====================
        /// <summary>Simple Moving Average</summary>
        SIMPLE,
        /// <summary>Exponential Moving Average</summary>
        EMA,
        /// <summary>Weighted Moving Average</summary>
        WMA,
        /// <summary>Hull Moving Average</summary>
        HULL,
        /// <summary>Triangular Moving Average</summary>
        TRIANGULAR,
        /// <summary>Volume Weighted Moving Average</summary>
        VWMA,
        /// <summary>Wilder's Smoothing (used in RSI)</summary>
        WILDER,
        /// <summary>Smoothed Moving Average</summary>
        SMMA,

        // ==================== Advanced MAs ====================
        /// <summary>Double Exponential Moving Average</summary>
        DEMA,
        /// <summary>Triple Exponential Moving Average</summary>
        TEMA,
        /// <summary>Least Squares Moving Average (Linear Regression)</summary>
        LSMA,
        /// <summary>Kaufman's Adaptive Moving Average</summary>
        KAMA,
        /// <summary>Variable Index Dynamic Average</summary>
        VIDYA,
        /// <summary>Zero-Lag Exponential Moving Average</summary>
        ZLEMA,
        /// <summary>Tillson T3 Moving Average</summary>
        T3,
        /// <summary>Arnaud Legoux Moving Average</summary>
        ALMA,
        /// <summary>Jurik Moving Average</summary>
        JMA,

        // ==================== Exotic MAs ====================
        /// <summary>Fractal Adaptive Moving Average</summary>
        FRAMA,
        /// <summary>MESA Adaptive Moving Average</summary>
        MAMA,
        /// <summary>McGinley Dynamic</summary>
        MCGINLEY,
        /// <summary>Volatility Adjusted Moving Average</summary>
        VAMA,

        // ==================== Compound MAs ====================
        /// <summary>Double Hull Moving Average</summary>
        DHULL,
        /// <summary>Triple Hull Moving Average</summary>
        THULL,
        /// <summary>Double ZLEMA</summary>
        DZLEMA,
        /// <summary>Triple ZLEMA</summary>
        TZLEMA,
        /// <summary>Double SMMA</summary>
        DSMMA,
        /// <summary>Triple SMMA</summary>
        TSMMA,
        /// <summary>Double SMA</summary>
        DSMA,
        /// <summary>Triple SMA</summary>
        TSMA,
        /// <summary>Double WMA</summary>
        DWMA,
        /// <summary>Triple WMA</summary>
        TWMA,
        /// <summary>Double VWMA</summary>
        DVWMA,
        /// <summary>Triple VWMA</summary>
        TVWMA,

        // ==================== Statistical MAs ====================
        /// <summary>Median Moving Average</summary>
        MEDIAN,
        /// <summary>Geometric Moving Average</summary>
        GMA,
        /// <summary>Zero-Lag Simple MA</summary>
        ZSMA,

        // ==================== Specialized MAs ====================
        /// <summary>Time Series Moving Average</summary>
        TIME_SERIES,
        /// <summary>Coefficient of Variation Weighted MA</summary>
        COVWMA,
        /// <summary>Coefficient of Variation Weighted EMA</summary>
        COVWEMA,
        /// <summary>Following Adaptive Moving Average</summary>
        FAMA,
        /// <summary>Square Root Weighted Moving Average</summary>
        SRWMA,
        /// <summary>Symmetrically (Sine-) Weighted Moving Average</summary>
        SWMA,
        /// <summary>Elastic Volume Weighted MA</summary>
        EVWMA,
        /// <summary>Regularized Exponential Moving Average</summary>
        REGMA,
        /// <summary>Range EMA</summary>
        REMA,
        /// <summary>Repulsion MA</summary>
        REPMA,
        /// <summary>RSI-based MA</summary>
        RSIMA,
        /// <summary>Exponential Triangular MA</summary>
        ETMA,
        /// <summary>Triangular Exponential MA</summary>
        TREMA,
        /// <summary>Triangular Simple MA</summary>
        TRSMA,
        /// <summary>Triple Hull MA (alternative)</summary>
        THMA,

        // ==================== Advanced Exotic MAs ====================
        /// <summary>Alpha-Decreasing EMA</summary>
        ADEMA,
        /// <summary>Exponentially Deviating MA</summary>
        EDMA,
        /// <summary>Ehlers Dynamic Smoothed MA</summary>
        EDSMA,
        /// <summary>Ahrens MA</summary>
        AHMA,
        /// <summary>Exponential Hull MA</summary>
        EHMA,
        /// <summary>Adaptive Least Squares MA</summary>
        ALSMA,
        /// <summary>Adaptive Autonomous Recursive MA</summary>
        AARMA,
        /// <summary>McNicholl MA</summary>
        MCMA,
        /// <summary>Leo MA</summary>
        LEOMA,
        /// <summary>Corrective MA</summary>
        CMA,
        /// <summary>Correlation MA</summary>
        CORMA,
        /// <summary>Auto-Line</summary>
        AUTOL,
        /// <summary>Optimized Exponential MA</summary>
        XEMA,
        /// <summary>Double SSMA</summary>
        DSSMA,
        /// <summary>Triple SSMA</summary>
        TSSMA
    }

    /// <summary>
    /// Extension methods for MAMethod enum
    /// </summary>
    public static class MAMethodExtensions
    {
        /// <summary>
        /// Get display name for MA method
        /// </summary>
        public static string GetDisplayName(this MAMethod method)
        {
            return method switch
            {
                MAMethod.SIMPLE => "Simple MA",
                MAMethod.EMA => "Exponential MA",
                MAMethod.WMA => "Weighted MA",
                MAMethod.HULL => "Hull MA",
                MAMethod.DEMA => "Double EMA",
                MAMethod.TEMA => "Triple EMA",
                MAMethod.KAMA => "Kaufman's Adaptive MA",
                MAMethod.ALMA => "Arnaud Legoux MA",
                MAMethod.JMA => "Jurik MA",
                MAMethod.T3 => "Tillson T3",
                MAMethod.ZLEMA => "Zero-Lag EMA",
                MAMethod.VWMA => "Volume Weighted MA",
                _ => method.ToString()
            };
        }

        /// <summary>
        /// Check if MA method is implemented
        /// </summary>
        public static bool IsImplemented(this MAMethod method)
        {
            return method switch
            {
                // Basic MAs
                MAMethod.SIMPLE or MAMethod.EMA or MAMethod.WMA or
                MAMethod.HULL or MAMethod.DEMA or MAMethod.TEMA or
                MAMethod.VWMA or MAMethod.LSMA or MAMethod.TRIANGULAR or
                MAMethod.WILDER or MAMethod.SMMA => true,

                // Advanced MAs
                MAMethod.KAMA or MAMethod.VIDYA or MAMethod.ZLEMA or
                MAMethod.T3 or MAMethod.ALMA or MAMethod.JMA => true,

                // Compound MAs (Double)
                MAMethod.DSMA or MAMethod.DWMA or MAMethod.DVWMA or
                MAMethod.DHULL or MAMethod.DZLEMA or MAMethod.DSMMA or
                MAMethod.DSSMA => true,

                // Compound MAs (Triple)
                MAMethod.TSMA or MAMethod.TWMA or MAMethod.TVWMA or
                MAMethod.THULL or MAMethod.TZLEMA or MAMethod.TSMMA or
                MAMethod.TSSMA => true,

                // Statistical MAs
                MAMethod.MEDIAN or MAMethod.GMA or MAMethod.ZSMA => true,

                // Specialized MAs
                MAMethod.SRWMA or MAMethod.SWMA or MAMethod.EVWMA or
                MAMethod.REGMA or MAMethod.REMA or MAMethod.REPMA or
                MAMethod.RSIMA or MAMethod.ETMA or MAMethod.TREMA or
                MAMethod.TRSMA or MAMethod.THMA => true,

                // Advanced2 MAs
                MAMethod.COVWMA or MAMethod.COVWEMA or MAMethod.FAMA or
                MAMethod.TIME_SERIES => true,

                // Exotic MAs
                MAMethod.FRAMA or MAMethod.MAMA or MAMethod.MCGINLEY or
                MAMethod.VAMA or MAMethod.ADEMA or MAMethod.EDMA or
                MAMethod.EDSMA or MAMethod.AHMA or MAMethod.EHMA or
                MAMethod.ALSMA or MAMethod.AARMA or MAMethod.MCMA or
                MAMethod.LEOMA or MAMethod.CMA or MAMethod.CORMA or
                MAMethod.AUTOL or MAMethod.XEMA => true,

                // Not implemented
                _ => false
            };
        }
    }
}
