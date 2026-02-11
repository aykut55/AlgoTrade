using System.Linq;

namespace AlgoTrade.Core.Trading.Indicators.Momentum.Results
{
    /// <summary>
    /// Result container for RSI indicator
    /// </summary>
    public class RSIResult
    {
        /// <summary>RSI values (0-100)</summary>
        public double[] Values { get; set; } = new double[0];

        /// <summary>Overbought signals (RSI > 70)</summary>
        public bool[] Overbought { get; set; } = new bool[0];

        /// <summary>Oversold signals (RSI < 30)</summary>
        public bool[] Oversold { get; set; } = new bool[0];

        /// <summary>Period used for calculation</summary>
        public int Period { get; set; }

        /// <summary>Current RSI value (last element)</summary>
        public double Current => Values.Length > 0 ? Values[^1] : double.NaN;

        /// <summary>Is current RSI overbought?</summary>
        public bool IsOverbought => Overbought.Length > 0 && Overbought[^1];

        /// <summary>Is current RSI oversold?</summary>
        public bool IsOversold => Oversold.Length > 0 && Oversold[^1];

        /// <summary>Get RSI at specific index</summary>
        public double this[int index] => Values[index];

        /// <summary>Number of data points</summary>
        public int Length => Values.Length;

        /// <summary>
        /// Constructor for RSIResult
        /// </summary>
        public RSIResult(double[] values, int period)
        {
            Values = values;
            Period = period;

            // Initialize Overbought and Oversold arrays
            Overbought = new bool[values.Length];
            Oversold = new bool[values.Length];

            // Populate based on standard thresholds (70 for overbought, 30 for oversold)
            for (int i = 0; i < values.Length; i++)
            {
                Overbought[i] = values[i] > 70;
                Oversold[i] = values[i] < 30;
            }
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public RSIResult()
        {
        }
    }
}
