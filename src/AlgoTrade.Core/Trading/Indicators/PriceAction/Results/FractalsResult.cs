namespace AlgoTrade.Core.Trading.Indicators.PriceAction.Results
{
    /// <summary>
    /// Result container for Williams Fractals indicator
    /// </summary>
    public class FractalsResult
    {
        /// <summary>Fractal high detected at each bar</summary>
        public bool[] FractalHighs { get; set; } = new bool[0];

        /// <summary>Fractal low detected at each bar</summary>
        public bool[] FractalLows { get; set; } = new bool[0];

        /// <summary>Current bar has fractal high?</summary>
        public bool CurrentFractalHigh => FractalHighs.Length > 0 && FractalHighs[^1];

        /// <summary>Current bar has fractal low?</summary>
        public bool CurrentFractalLow => FractalLows.Length > 0 && FractalLows[^1];

        /// <summary>Number of data points</summary>
        public int Length => FractalHighs.Length;

        /// <summary>
        /// Constructor for FractalsResult
        /// </summary>
        public FractalsResult(bool[] fractalHighs, bool[] fractalLows)
        {
            FractalHighs = fractalHighs;
            FractalLows = fractalLows;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public FractalsResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out bool[] fractalHighs, out bool[] fractalLows)
        {
            fractalHighs = FractalHighs;
            fractalLows = FractalLows;
        }
    }
}
