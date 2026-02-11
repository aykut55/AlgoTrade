namespace AlgoTrade.Core.Trading.Indicators.PriceAction.Results
{
    /// <summary>
    /// Result container for Swing Points indicator
    /// </summary>
    public class SwingPointsResult
    {
        /// <summary>Swing high detected at each bar</summary>
        public bool[] SwingHighs { get; set; } = new bool[0];

        /// <summary>Swing low detected at each bar</summary>
        public bool[] SwingLows { get; set; } = new bool[0];

        /// <summary>Number of bars to the left</summary>
        public int LeftBars { get; set; }

        /// <summary>Number of bars to the right</summary>
        public int RightBars { get; set; }

        /// <summary>Current bar has swing high?</summary>
        public bool CurrentSwingHigh => SwingHighs.Length > 0 && SwingHighs[^1];

        /// <summary>Current bar has swing low?</summary>
        public bool CurrentSwingLow => SwingLows.Length > 0 && SwingLows[^1];

        /// <summary>Number of data points</summary>
        public int Length => SwingHighs.Length;

        /// <summary>
        /// Constructor for SwingPointsResult
        /// </summary>
        public SwingPointsResult(bool[] swingHighs, bool[] swingLows, int leftBars, int rightBars)
        {
            SwingHighs = swingHighs;
            SwingLows = swingLows;
            LeftBars = leftBars;
            RightBars = rightBars;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public SwingPointsResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out bool[] swingHighs, out bool[] swingLows)
        {
            swingHighs = SwingHighs;
            swingLows = SwingLows;
        }
    }
}
