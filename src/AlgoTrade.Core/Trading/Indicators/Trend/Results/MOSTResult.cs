namespace AlgoTrade.Core.Trading.Indicators.Trend.Results
{
    /// <summary>
    /// Result container for MOST (Moving Stop Loss) indicator
    /// </summary>
    public class MOSTResult
    {
        /// <summary>MOST values (trailing stop loss line)</summary>
        public double[] MOST { get; set; } = new double[0];

        /// <summary>ExMov values (EMA)</summary>
        public double[] ExMov { get; set; } = new double[0];

        /// <summary>EMA period used</summary>
        public int Period { get; set; }

        /// <summary>Band percentage used</summary>
        public double Percent { get; set; }

        /// <summary>Current MOST value</summary>
        public double CurrentMOST => MOST.Length > 0 ? MOST[^1] : double.NaN;

        /// <summary>Current ExMov value</summary>
        public double CurrentExMov => ExMov.Length > 0 ? ExMov[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => MOST.Length;

        /// <summary>
        /// Constructor for MOSTResult
        /// </summary>
        public MOSTResult(double[] most, double[] exmov, int period, double percent)
        {
            MOST = most;
            ExMov = exmov;
            Period = period;
            Percent = percent;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public MOSTResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] most, out double[] exmov)
        {
            most = MOST;
            exmov = ExMov;
        }
    }
}
