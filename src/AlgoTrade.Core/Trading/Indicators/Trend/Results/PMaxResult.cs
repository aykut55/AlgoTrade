namespace AlgoTrade.Core.Trading.Indicators.Trend.Results
{
    /// <summary>
    /// Result container for PMax (Profit Maximizer) indicator
    /// </summary>
    public class PMaxResult
    {
        /// <summary>PMax values</summary>
        public double[] PMax { get; set; } = new double[0];

        /// <summary>PMax Moving Average</summary>
        public double[] PMaxMA { get; set; } = new double[0];

        /// <summary>Direction (1 = bullish, -1 = bearish)</summary>
        public int[] Direction { get; set; } = new int[0];

        /// <summary>ATR period used</summary>
        public int AtrPeriod { get; set; }

        /// <summary>ATR multiplier used</summary>
        public double Multiplier { get; set; }

        /// <summary>MA period used</summary>
        public int MAPeriod { get; set; }

        /// <summary>Current PMax value</summary>
        public double Current => PMax.Length > 0 ? PMax[^1] : double.NaN;

        /// <summary>Current PMaxMA value</summary>
        public double CurrentMA => PMaxMA.Length > 0 ? PMaxMA[^1] : double.NaN;

        /// <summary>Current direction</summary>
        public int CurrentDirection => Direction.Length > 0 ? Direction[^1] : 0;

        /// <summary>Is current trend bullish?</summary>
        public bool IsBullish => CurrentDirection == 1;

        /// <summary>Is current trend bearish?</summary>
        public bool IsBearish => CurrentDirection == -1;

        /// <summary>Number of data points</summary>
        public int Length => PMax.Length;

        /// <summary>
        /// Constructor for PMaxResult
        /// </summary>
        public PMaxResult(double[] pmax, double[] pmaxMA, int[] direction, int atrPeriod, double multiplier, int maPeriod)
        {
            PMax = pmax;
            PMaxMA = pmaxMA;
            Direction = direction;
            AtrPeriod = atrPeriod;
            Multiplier = multiplier;
            MAPeriod = maPeriod;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public PMaxResult()
        {
        }
    }
}
