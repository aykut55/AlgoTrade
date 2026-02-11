namespace AlgoTrade.Core.Trading.Indicators.Volatility.Results
{
    /// <summary>
    /// Result container for Keltner Channel indicator
    /// </summary>
    public class KeltnerChannelResult
    {
        /// <summary>Upper band values</summary>
        public double[] Upper { get; set; } = new double[0];

        /// <summary>Middle band (EMA) values</summary>
        public double[] Middle { get; set; } = new double[0];

        /// <summary>Lower band values</summary>
        public double[] Lower { get; set; } = new double[0];

        /// <summary>Period used for calculation</summary>
        public int Period { get; set; }

        /// <summary>ATR multiplier used</summary>
        public double Multiplier { get; set; }

        /// <summary>Current upper band value</summary>
        public double CurrentUpper => Upper.Length > 0 ? Upper[^1] : double.NaN;

        /// <summary>Current middle band value</summary>
        public double CurrentMiddle => Middle.Length > 0 ? Middle[^1] : double.NaN;

        /// <summary>Current lower band value</summary>
        public double CurrentLower => Lower.Length > 0 ? Lower[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => Upper.Length;

        /// <summary>
        /// Constructor for KeltnerChannelResult
        /// </summary>
        public KeltnerChannelResult(double[] upper, double[] middle, double[] lower, int period, double multiplier)
        {
            Upper = upper;
            Middle = middle;
            Lower = lower;
            Period = period;
            Multiplier = multiplier;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public KeltnerChannelResult()
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
