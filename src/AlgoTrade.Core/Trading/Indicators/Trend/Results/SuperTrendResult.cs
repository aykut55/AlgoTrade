namespace AlgoTrade.Core.Trading.Indicators.Trend.Results
{
    /// <summary>
    /// Result container for SuperTrend indicator
    /// </summary>
    public class SuperTrendResult
    {
        /// <summary>SuperTrend values</summary>
        public double[] SuperTrend { get; set; } = new double[0];

        /// <summary>Trend direction (1 = bullish, -1 = bearish)</summary>
        public int[] Direction { get; set; } = new int[0];

        /// <summary>Current SuperTrend value</summary>
        public double Current => SuperTrend.Length > 0 ? SuperTrend[^1] : double.NaN;

        /// <summary>Current trend direction</summary>
        public int CurrentDirection => Direction.Length > 0 ? Direction[^1] : 0;

        /// <summary>Is current trend bullish?</summary>
        public bool IsBullish => CurrentDirection == 1;

        /// <summary>Is current trend bearish?</summary>
        public bool IsBearish => CurrentDirection == -1;

        /// <summary>Number of data points</summary>
        public int Length => SuperTrend.Length;
    }
}
