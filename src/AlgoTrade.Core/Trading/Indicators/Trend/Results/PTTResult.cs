namespace AlgoTrade.Core.Trading.Indicators.Trend.Results
{
    /// <summary>
    /// Result container for PTT (Progressive Trend Tracker) indicator
    /// </summary>
    public class PTTResult
    {
        /// <summary>PTT Upper line (cyan)</summary>
        public double[] Upper { get; set; } = new double[0];

        /// <summary>PTT Lower line (cyan)</summary>
        public double[] Lower { get; set; } = new double[0];

        /// <summary>Faster period used</summary>
        public int FasterPeriod { get; set; }

        /// <summary>Period used</summary>
        public int Period { get; set; }

        /// <summary>MA period used</summary>
        public int MAPeriod { get; set; }

        /// <summary>Slower period used</summary>
        public int SlowerPeriod { get; set; }

        /// <summary>Current Upper value</summary>
        public double CurrentUpper => Upper.Length > 0 ? Upper[^1] : double.NaN;

        /// <summary>Current Lower value</summary>
        public double CurrentLower => Lower.Length > 0 ? Lower[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => Upper.Length;

        /// <summary>
        /// Constructor for PTTResult
        /// </summary>
        public PTTResult(double[] upper, double[] lower, int fasterPeriod, int period, int maPeriod, int slowerPeriod)
        {
            Upper = upper;
            Lower = lower;
            FasterPeriod = fasterPeriod;
            Period = period;
            MAPeriod = maPeriod;
            SlowerPeriod = slowerPeriod;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public PTTResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] upper, out double[] lower)
        {
            upper = Upper;
            lower = Lower;
        }
    }
}
