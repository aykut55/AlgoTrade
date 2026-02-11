namespace AlgoTrade.Core.Trading.Indicators.PriceAction.Results
{
    /// <summary>
    /// Result container for ZigZag indicator
    /// </summary>
    public class ZigZagResult
    {
        /// <summary>ZigZag values (NaN for non-pivot points)</summary>
        public double[] ZigZag { get; set; } = new double[0];

        /// <summary>Pivot types: 1=high, -1=low, 0=none</summary>
        public int[] Pivots { get; set; } = new int[0];

        /// <summary>Minimum price change percentage used</summary>
        public double Deviation { get; set; }

        /// <summary>Current ZigZag value</summary>
        public double CurrentZigZag => ZigZag.Length > 0 ? ZigZag[^1] : double.NaN;

        /// <summary>Current pivot type</summary>
        public int CurrentPivot => Pivots.Length > 0 ? Pivots[^1] : 0;

        /// <summary>Is current pivot a high?</summary>
        public bool IsCurrentPivotHigh => CurrentPivot == 1;

        /// <summary>Is current pivot a low?</summary>
        public bool IsCurrentPivotLow => CurrentPivot == -1;

        /// <summary>Number of data points</summary>
        public int Length => ZigZag.Length;

        /// <summary>
        /// Constructor for ZigZagResult
        /// </summary>
        public ZigZagResult(double[] zigzag, int[] pivots, double deviation)
        {
            ZigZag = zigzag;
            Pivots = pivots;
            Deviation = deviation;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public ZigZagResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] zigzag, out int[] pivots)
        {
            zigzag = ZigZag;
            pivots = Pivots;
        }
    }
}
