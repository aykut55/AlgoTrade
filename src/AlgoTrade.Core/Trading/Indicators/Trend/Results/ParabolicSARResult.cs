namespace AlgoTrade.Core.Trading.Indicators.Trend.Results
{
    /// <summary>
    /// Result container for Parabolic SAR (Stop and Reverse) indicator
    /// </summary>
    public class ParabolicSARResult
    {
        /// <summary>SAR values (stop and reverse levels)</summary>
        public double[] SAR { get; set; } = new double[0];

        /// <summary>Trend direction (true = uptrend, false = downtrend)</summary>
        public bool[] Trend { get; set; } = new bool[0];

        /// <summary>Acceleration factor step used</summary>
        public double Step { get; set; }

        /// <summary>Maximum acceleration factor used</summary>
        public double Max { get; set; }

        /// <summary>Current SAR value</summary>
        public double CurrentSAR => SAR.Length > 0 ? SAR[^1] : double.NaN;

        /// <summary>Current trend (true = uptrend, false = downtrend)</summary>
        public bool CurrentTrend => Trend.Length > 0 && Trend[^1];

        /// <summary>Is currently in uptrend?</summary>
        public bool IsUptrend => CurrentTrend;

        /// <summary>Is currently in downtrend?</summary>
        public bool IsDowntrend => !CurrentTrend;

        /// <summary>Number of data points</summary>
        public int Length => SAR.Length;

        /// <summary>
        /// Constructor for ParabolicSARResult
        /// </summary>
        public ParabolicSARResult(double[] sar, bool[] trend, double step, double max)
        {
            SAR = sar;
            Trend = trend;
            Step = step;
            Max = max;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public ParabolicSARResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] sar, out bool[] trend)
        {
            sar = SAR;
            trend = Trend;
        }
    }
}
