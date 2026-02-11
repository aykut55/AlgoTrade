namespace AlgoTrade.Core.Trading.Indicators.SupportResistance.Results
{
    /// <summary>
    /// Result container for Fibonacci Retracement levels (auto-detected from data)
    /// </summary>
    public class FibonacciRetracementAutoResult
    {
        /// <summary>0% level array (high for uptrend, low for downtrend)</summary>
        public double[] Level_0 { get; set; } = new double[0];

        /// <summary>23.6% retracement level array</summary>
        public double[] Level_236 { get; set; } = new double[0];

        /// <summary>38.2% retracement level array (key Fibonacci level)</summary>
        public double[] Level_382 { get; set; } = new double[0];

        /// <summary>50% retracement level array</summary>
        public double[] Level_50 { get; set; } = new double[0];

        /// <summary>61.8% retracement level array (golden ratio)</summary>
        public double[] Level_618 { get; set; } = new double[0];

        /// <summary>78.6% retracement level array</summary>
        public double[] Level_786 { get; set; } = new double[0];

        /// <summary>100% level array (low for uptrend, high for downtrend)</summary>
        public double[] Level_100 { get; set; } = new double[0];

        /// <summary>Period used for high/low detection</summary>
        public int Period { get; set; }

        /// <summary>Current 0% level</summary>
        public double CurrentLevel_0 => Level_0.Length > 0 ? Level_0[^1] : double.NaN;

        /// <summary>Current 23.6% level</summary>
        public double CurrentLevel_236 => Level_236.Length > 0 ? Level_236[^1] : double.NaN;

        /// <summary>Current 38.2% level</summary>
        public double CurrentLevel_382 => Level_382.Length > 0 ? Level_382[^1] : double.NaN;

        /// <summary>Current 50% level</summary>
        public double CurrentLevel_50 => Level_50.Length > 0 ? Level_50[^1] : double.NaN;

        /// <summary>Current 61.8% level (golden ratio)</summary>
        public double CurrentLevel_618 => Level_618.Length > 0 ? Level_618[^1] : double.NaN;

        /// <summary>Current 78.6% level</summary>
        public double CurrentLevel_786 => Level_786.Length > 0 ? Level_786[^1] : double.NaN;

        /// <summary>Current 100% level</summary>
        public double CurrentLevel_100 => Level_100.Length > 0 ? Level_100[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => Level_0.Length;

        /// <summary>
        /// Constructor for FibonacciRetracementAutoResult
        /// </summary>
        public FibonacciRetracementAutoResult(double[] level_0, double[] level_236, double[] level_382,
            double[] level_50, double[] level_618, double[] level_786, double[] level_100, int period)
        {
            Level_0 = level_0;
            Level_236 = level_236;
            Level_382 = level_382;
            Level_50 = level_50;
            Level_618 = level_618;
            Level_786 = level_786;
            Level_100 = level_100;
            Period = period;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public FibonacciRetracementAutoResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] level_0, out double[] level_236, out double[] level_382,
            out double[] level_50, out double[] level_618, out double[] level_786, out double[] level_100)
        {
            level_0 = Level_0;
            level_236 = Level_236;
            level_382 = Level_382;
            level_50 = Level_50;
            level_618 = Level_618;
            level_786 = Level_786;
            level_100 = Level_100;
        }
    }
}
