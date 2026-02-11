namespace AlgoTrade.Core.Trading.Indicators.Trend.Results
{
    /// <summary>
    /// Result container for MavilimW indicator
    /// </summary>
    public class MavilimWResult
    {
        /// <summary>MavilimW values</summary>
        public double[] MavilimW { get; set; } = new double[0];

        /// <summary>Trendline values (FAMA)</summary>
        public double[] Trendline { get; set; } = new double[0];

        /// <summary>First parameter (sensitivity)</summary>
        public int Param1 { get; set; }

        /// <summary>Second parameter (sensitivity)</summary>
        public int Param2 { get; set; }

        /// <summary>Current MavilimW value</summary>
        public double Current => MavilimW.Length > 0 ? MavilimW[^1] : double.NaN;

        /// <summary>Current Trendline value</summary>
        public double CurrentTrendline => Trendline.Length > 0 ? Trendline[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => MavilimW.Length;

        /// <summary>
        /// Constructor for MavilimWResult
        /// </summary>
        public MavilimWResult(double[] mavilimW, double[] trendline, int param1, int param2)
        {
            MavilimW = mavilimW;
            Trendline = trendline;
            Param1 = param1;
            Param2 = param2;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public MavilimWResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] mavilimW, out double[] trendline)
        {
            mavilimW = MavilimW;
            trendline = Trendline;
        }
    }
}
