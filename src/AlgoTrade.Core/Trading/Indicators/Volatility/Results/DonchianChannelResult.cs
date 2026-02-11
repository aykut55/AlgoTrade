namespace AlgoTrade.Core.Trading.Indicators.Volatility.Results
{
    /// <summary>
    /// Result container for Donchian Channel indicator
    /// </summary>
    public class DonchianChannelResult
    {
        /// <summary>Upper band values (highest high)</summary>
        public double[] Upper { get; set; } = new double[0];

        /// <summary>Middle band values</summary>
        public double[] Middle { get; set; } = new double[0];

        /// <summary>Lower band values (lowest low)</summary>
        public double[] Lower { get; set; } = new double[0];

        /// <summary>Period used for calculation</summary>
        public int Period { get; set; }

        /// <summary>Current upper band value</summary>
        public double CurrentUpper => Upper.Length > 0 ? Upper[^1] : double.NaN;

        /// <summary>Current middle band value</summary>
        public double CurrentMiddle => Middle.Length > 0 ? Middle[^1] : double.NaN;

        /// <summary>Current lower band value</summary>
        public double CurrentLower => Lower.Length > 0 ? Lower[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => Upper.Length;

        /// <summary>
        /// Constructor for DonchianChannelResult
        /// </summary>
        public DonchianChannelResult(double[] upper, double[] middle, double[] lower, int period)
        {
            Upper = upper;
            Middle = middle;
            Lower = lower;
            Period = period;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public DonchianChannelResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] upper, out double[] middle, out double[] lower)
        {
            upper = Upper;
            middle = Middle;
            lower = Lower;
        }
    }
}
