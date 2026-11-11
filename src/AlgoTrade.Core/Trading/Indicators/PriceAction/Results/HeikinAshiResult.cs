namespace AlgoTrade.Core.Trading.Indicators.PriceAction.Results
{
    /// <summary>
    /// Result container for Heikin-Ashi indicator
    /// </summary>
    public class HeikinAshiResult
    {
        /// <summary>Heikin-Ashi open values</summary>
        public double[] Open { get; set; } = new double[0];

        /// <summary>Heikin-Ashi high values</summary>
        public double[] High { get; set; } = new double[0];

        /// <summary>Heikin-Ashi low values</summary>
        public double[] Low { get; set; } = new double[0];

        /// <summary>Heikin-Ashi close values</summary>
        public double[] Close { get; set; } = new double[0];

        /// <summary>Current (last bar) Heikin-Ashi open</summary>
        public double CurrentOpen => Open.Length > 0 ? Open[^1] : double.NaN;

        /// <summary>Current (last bar) Heikin-Ashi high</summary>
        public double CurrentHigh => High.Length > 0 ? High[^1] : double.NaN;

        /// <summary>Current (last bar) Heikin-Ashi low</summary>
        public double CurrentLow => Low.Length > 0 ? Low[^1] : double.NaN;

        /// <summary>Current (last bar) Heikin-Ashi close</summary>
        public double CurrentClose => Close.Length > 0 ? Close[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => Close.Length;

        /// <summary>
        /// Constructor for HeikinAshiResult
        /// </summary>
        public HeikinAshiResult(double[] open, double[] high, double[] low, double[] close)
        {
            Open = open;
            High = high;
            Low = low;
            Close = close;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public HeikinAshiResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] open, out double[] high, out double[] low, out double[] close)
        {
            open = Open;
            high = High;
            low = Low;
            close = Close;
        }
    }
}
