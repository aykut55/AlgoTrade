namespace AlgoTrade.Core.Trading.Indicators.Trend.Results
{
    /// <summary>
    /// Result container for OTT (Optimized Trend Tracker) indicator
    /// </summary>
    public class OTTResult
    {
        /// <summary>OTT values (optimized trend line)</summary>
        public double[] OTT { get; set; } = new double[0];

        /// <summary>Moving Average values</summary>
        public double[] MA { get; set; } = new double[0];

        /// <summary>OTT Support Line</summary>
        public double[] Support { get; set; } = new double[0];

        /// <summary>Period used</summary>
        public int Period { get; set; }

        /// <summary>OTT percent used</summary>
        public double Percent { get; set; }

        /// <summary>Current OTT value</summary>
        public double CurrentOTT => OTT.Length > 0 ? OTT[^1] : double.NaN;

        /// <summary>Current MA value</summary>
        public double CurrentMA => MA.Length > 0 ? MA[^1] : double.NaN;

        /// <summary>Current Support value</summary>
        public double CurrentSupport => Support.Length > 0 ? Support[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => OTT.Length;

        /// <summary>
        /// Constructor for OTTResult
        /// </summary>
        public OTTResult(double[] ott, double[] ma, double[] support, int period, double percent)
        {
            OTT = ott;
            MA = ma;
            Support = support;
            Period = period;
            Percent = percent;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public OTTResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] ott, out double[] ma, out double[] support)
        {
            ott = OTT;
            ma = MA;
            support = Support;
        }
    }
}
