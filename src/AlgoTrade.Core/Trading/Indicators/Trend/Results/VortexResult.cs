namespace AlgoTrade.Core.Trading.Indicators.Trend.Results
{
    /// <summary>
    /// Result container for Vortex indicator
    /// </summary>
    public class VortexResult
    {
        /// <summary>VI+ values (positive vortex indicator)</summary>
        public double[] VIPlus { get; set; } = new double[0];

        /// <summary>VI- values (negative vortex indicator)</summary>
        public double[] VIMinus { get; set; } = new double[0];

        /// <summary>Period used for calculation</summary>
        public int Period { get; set; }

        /// <summary>Current VI+ value</summary>
        public double CurrentVIPlus => VIPlus.Length > 0 ? VIPlus[^1] : double.NaN;

        /// <summary>Current VI- value</summary>
        public double CurrentVIMinus => VIMinus.Length > 0 ? VIMinus[^1] : double.NaN;

        /// <summary>Is in uptrend (VI+ > VI-)?</summary>
        public bool IsUptrend => CurrentVIPlus > CurrentVIMinus;

        /// <summary>Is in downtrend (VI- > VI+)?</summary>
        public bool IsDowntrend => CurrentVIMinus > CurrentVIPlus;

        /// <summary>Number of data points</summary>
        public int Length => VIPlus.Length;

        /// <summary>
        /// Constructor for VortexResult
        /// </summary>
        public VortexResult(double[] viPlus, double[] viMinus, int period)
        {
            VIPlus = viPlus;
            VIMinus = viMinus;
            Period = period;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public VortexResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] viPlus, out double[] viMinus)
        {
            viPlus = VIPlus;
            viMinus = VIMinus;
        }
    }
}
