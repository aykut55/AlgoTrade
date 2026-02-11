namespace AlgoTrade.Core.Trading.Indicators.Trend.Results
{
    /// <summary>
    /// Result container for AlphaTrend indicator
    /// </summary>
    public class AlphaTrendResult
    {
        /// <summary>AlphaTrend values</summary>
        public double[] AlphaTrend { get; set; } = new double[0];

        /// <summary>ATR period used</summary>
        public int AtrPeriod { get; set; }

        /// <summary>ATR multiplier used</summary>
        public double Coefficient { get; set; }

        /// <summary>MFI/RSI period used</summary>
        public int MomentumPeriod { get; set; }

        /// <summary>Current AlphaTrend value</summary>
        public double Current => AlphaTrend.Length > 0 ? AlphaTrend[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => AlphaTrend.Length;

        /// <summary>
        /// Constructor for AlphaTrendResult
        /// </summary>
        public AlphaTrendResult(double[] alphaTrend, int atrPeriod, double coefficient, int momentumPeriod)
        {
            AlphaTrend = alphaTrend;
            AtrPeriod = atrPeriod;
            Coefficient = coefficient;
            MomentumPeriod = momentumPeriod;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public AlphaTrendResult()
        {
        }
    }
}
