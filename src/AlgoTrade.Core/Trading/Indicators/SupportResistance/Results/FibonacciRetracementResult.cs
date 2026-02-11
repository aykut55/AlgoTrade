namespace AlgoTrade.Core.Trading.Indicators.SupportResistance.Results
{
    /// <summary>
    /// Result container for Fibonacci Retracement levels (single calculation)
    /// </summary>
    public class FibonacciRetracementResult
    {
        /// <summary>0% level (high for uptrend, low for downtrend)</summary>
        public double Level_0 { get; set; }

        /// <summary>23.6% retracement level</summary>
        public double Level_236 { get; set; }

        /// <summary>38.2% retracement level (key Fibonacci level)</summary>
        public double Level_382 { get; set; }

        /// <summary>50% retracement level</summary>
        public double Level_50 { get; set; }

        /// <summary>61.8% retracement level (golden ratio)</summary>
        public double Level_618 { get; set; }

        /// <summary>78.6% retracement level</summary>
        public double Level_786 { get; set; }

        /// <summary>100% level (low for uptrend, high for downtrend)</summary>
        public double Level_100 { get; set; }

        /// <summary>Is calculated for uptrend?</summary>
        public bool IsUptrend { get; set; }

        /// <summary>High price used in calculation</summary>
        public double High { get; set; }

        /// <summary>Low price used in calculation</summary>
        public double Low { get; set; }

        /// <summary>Price range (High - Low)</summary>
        public double Range => High - Low;

        /// <summary>
        /// Constructor for FibonacciRetracementResult
        /// </summary>
        public FibonacciRetracementResult(double level_0, double level_236, double level_382,
            double level_50, double level_618, double level_786, double level_100,
            double high, double low, bool isUptrend)
        {
            Level_0 = level_0;
            Level_236 = level_236;
            Level_382 = level_382;
            Level_50 = level_50;
            Level_618 = level_618;
            Level_786 = level_786;
            Level_100 = level_100;
            High = high;
            Low = low;
            IsUptrend = isUptrend;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public FibonacciRetracementResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double level_0, out double level_236, out double level_382,
            out double level_50, out double level_618, out double level_786, out double level_100)
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
