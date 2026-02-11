namespace AlgoTrade.Core.Trading.Indicators.Momentum.Results
{
    /// <summary>
    /// Result container for MACD indicator
    /// </summary>
    public class MACDResult
    {
        /// <summary>MACD line (Fast EMA - Slow EMA)</summary>
        public double[] MACD { get; set; } = new double[0];

        /// <summary>Signal line (EMA of MACD line)</summary>
        public double[] Signal { get; set; } = new double[0];

        /// <summary>Histogram (MACD - Signal)</summary>
        public double[] Histogram { get; set; } = new double[0];

        /// <summary>Fast EMA period used</summary>
        public int FastPeriod { get; set; }

        /// <summary>Slow EMA period used</summary>
        public int SlowPeriod { get; set; }

        /// <summary>Signal line period used</summary>
        public int SignalPeriod { get; set; }

        /// <summary>Current MACD value</summary>
        public double CurrentMACD => MACD.Length > 0 ? MACD[^1] : double.NaN;

        /// <summary>Current Signal value</summary>
        public double CurrentSignal => Signal.Length > 0 ? Signal[^1] : double.NaN;

        /// <summary>Current Histogram value</summary>
        public double CurrentHistogram => Histogram.Length > 0 ? Histogram[^1] : double.NaN;

        /// <summary>Is current histogram positive (bullish)?</summary>
        public bool IsBullish => CurrentHistogram > 0;

        /// <summary>Is current histogram negative (bearish)?</summary>
        public bool IsBearish => CurrentHistogram < 0;

        /// <summary>Number of data points</summary>
        public int Length => MACD.Length;

        /// <summary>
        /// Constructor for MACDResult
        /// </summary>
        public MACDResult(double[] macd, double[] signal, double[] histogram, int fastPeriod, int slowPeriod, int signalPeriod)
        {
            MACD = macd;
            Signal = signal;
            Histogram = histogram;
            FastPeriod = fastPeriod;
            SlowPeriod = slowPeriod;
            SignalPeriod = signalPeriod;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public MACDResult()
        {
        }
    }
}
