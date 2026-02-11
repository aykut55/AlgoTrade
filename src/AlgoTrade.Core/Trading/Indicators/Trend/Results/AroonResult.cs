namespace AlgoTrade.Core.Trading.Indicators.Trend.Results
{
    /// <summary>
    /// Result container for Aroon indicator
    /// </summary>
    public class AroonResult
    {
        /// <summary>Aroon Up values (0-100)</summary>
        public double[] AroonUp { get; set; } = new double[0];

        /// <summary>Aroon Down values (0-100)</summary>
        public double[] AroonDown { get; set; } = new double[0];

        /// <summary>Period used for calculation</summary>
        public int Period { get; set; }

        /// <summary>Current Aroon Up value</summary>
        public double CurrentAroonUp => AroonUp.Length > 0 ? AroonUp[^1] : double.NaN;

        /// <summary>Current Aroon Down value</summary>
        public double CurrentAroonDown => AroonDown.Length > 0 ? AroonDown[^1] : double.NaN;

        /// <summary>Is in strong uptrend (Up > 70 and Down < 30)?</summary>
        public bool IsStrongUptrend => CurrentAroonUp > 70 && CurrentAroonDown < 30;

        /// <summary>Is in strong downtrend (Down > 70 and Up < 30)?</summary>
        public bool IsStrongDowntrend => CurrentAroonDown > 70 && CurrentAroonUp < 30;

        /// <summary>Is consolidating (both near 50)?</summary>
        public bool IsConsolidating => System.Math.Abs(CurrentAroonUp - 50) < 20 && System.Math.Abs(CurrentAroonDown - 50) < 20;

        /// <summary>Number of data points</summary>
        public int Length => AroonUp.Length;

        /// <summary>
        /// Constructor for AroonResult
        /// </summary>
        public AroonResult(double[] aroonUp, double[] aroonDown, int period)
        {
            AroonUp = aroonUp;
            AroonDown = aroonDown;
            Period = period;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public AroonResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] aroonUp, out double[] aroonDown)
        {
            aroonUp = AroonUp;
            aroonDown = AroonDown;
        }
    }
}
