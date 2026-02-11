namespace AlgoTrade.Core.Trading.Indicators.PriceAction.Results
{
    /// <summary>
    /// Result container for Higher High / Lower Low pattern detection
    /// </summary>
    public class HigherHighLowerLowResult
    {
        /// <summary>Higher High detected at each bar (current high > previous high)</summary>
        public bool[] HigherHigh { get; set; } = new bool[0];

        /// <summary>Lower High detected at each bar (current high < previous high)</summary>
        public bool[] LowerHigh { get; set; } = new bool[0];

        /// <summary>Higher Low detected at each bar (current low > previous low)</summary>
        public bool[] HigherLow { get; set; } = new bool[0];

        /// <summary>Lower Low detected at each bar (current low < previous low)</summary>
        public bool[] LowerLow { get; set; } = new bool[0];

        /// <summary>Current bar has higher high?</summary>
        public bool CurrentHigherHigh => HigherHigh.Length > 0 && HigherHigh[^1];

        /// <summary>Current bar has lower high?</summary>
        public bool CurrentLowerHigh => LowerHigh.Length > 0 && LowerHigh[^1];

        /// <summary>Current bar has higher low?</summary>
        public bool CurrentHigherLow => HigherLow.Length > 0 && HigherLow[^1];

        /// <summary>Current bar has lower low?</summary>
        public bool CurrentLowerLow => LowerLow.Length > 0 && LowerLow[^1];

        /// <summary>Is in uptrend? (Higher High AND Higher Low)</summary>
        public bool IsUptrend => CurrentHigherHigh && CurrentHigherLow;

        /// <summary>Is in downtrend? (Lower High AND Lower Low)</summary>
        public bool IsDowntrend => CurrentLowerHigh && CurrentLowerLow;

        /// <summary>Number of data points</summary>
        public int Length => HigherHigh.Length;

        /// <summary>
        /// Constructor for HigherHighLowerLowResult
        /// </summary>
        public HigherHighLowerLowResult(bool[] higherHigh, bool[] lowerHigh, bool[] higherLow, bool[] lowerLow)
        {
            HigherHigh = higherHigh;
            LowerHigh = lowerHigh;
            HigherLow = higherLow;
            LowerLow = lowerLow;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public HigherHighLowerLowResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out bool[] higherHigh, out bool[] lowerHigh, out bool[] higherLow, out bool[] lowerLow)
        {
            higherHigh = HigherHigh;
            lowerHigh = LowerHigh;
            higherLow = HigherLow;
            lowerLow = LowerLow;
        }
    }
}
