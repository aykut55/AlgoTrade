namespace AlgoTrade.Core.Trading.Indicators.Trend.Results
{
    /// <summary>
    /// Result container for HOTT/LOTT (High/Low Optimized Trend Tracker) indicator
    /// </summary>
    public class HOTTLOTTResult
    {
        /// <summary>HOTT values (OTT on High prices)</summary>
        public double[] HOTT { get; set; } = new double[0];

        /// <summary>LOTT values (OTT on Low prices)</summary>
        public double[] LOTT { get; set; } = new double[0];

        /// <summary>HOTT Moving Average</summary>
        public double[] HOTTMA { get; set; } = new double[0];

        /// <summary>LOTT Moving Average</summary>
        public double[] LOTTMA { get; set; } = new double[0];

        /// <summary>Period used</summary>
        public int Period { get; set; }

        /// <summary>OTT percent used</summary>
        public double Percent { get; set; }

        /// <summary>Current HOTT value</summary>
        public double CurrentHOTT => HOTT.Length > 0 ? HOTT[^1] : double.NaN;

        /// <summary>Current LOTT value</summary>
        public double CurrentLOTT => LOTT.Length > 0 ? LOTT[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => HOTT.Length;

        /// <summary>
        /// Constructor for HOTTLOTTResult
        /// </summary>
        public HOTTLOTTResult(double[] hott, double[] lott, double[] hottMA, double[] lottMA, int period, double percent)
        {
            HOTT = hott;
            LOTT = lott;
            HOTTMA = hottMA;
            LOTTMA = lottMA;
            Period = period;
            Percent = percent;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public HOTTLOTTResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] hott, out double[] lott)
        {
            hott = HOTT;
            lott = LOTT;
        }
    }
}
