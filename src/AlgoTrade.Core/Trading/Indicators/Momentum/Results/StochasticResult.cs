namespace AlgoTrade.Core.Trading.Indicators.Momentum.Results
{
    /// <summary>
    /// Result container for Stochastic Oscillator indicator
    /// </summary>
    public class StochasticResult
    {
        /// <summary>%K values (fast stochastic)</summary>
        public double[] K { get; set; } = new double[0];

        /// <summary>%D values (slow stochastic, SMA of %K)</summary>
        public double[] D { get; set; } = new double[0];

        /// <summary>%K period used</summary>
        public int KPeriod { get; set; }

        /// <summary>%D period used</summary>
        public int DPeriod { get; set; }

        /// <summary>Current %K value</summary>
        public double CurrentK => K.Length > 0 ? K[^1] : double.NaN;

        /// <summary>Current %D value</summary>
        public double CurrentD => D.Length > 0 ? D[^1] : double.NaN;

        /// <summary>Is current %K overbought (>80)?</summary>
        public bool IsOverbought => CurrentK > 80;

        /// <summary>Is current %K oversold (<20)?</summary>
        public bool IsOversold => CurrentK < 20;

        /// <summary>Number of data points</summary>
        public int Length => K.Length;

        /// <summary>
        /// Constructor for StochasticResult
        /// </summary>
        public StochasticResult(double[] k, double[] d, int kPeriod, int dPeriod)
        {
            K = k;
            D = d;
            KPeriod = kPeriod;
            DPeriod = dPeriod;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public StochasticResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] k, out double[] d)
        {
            k = K;
            d = D;
        }
    }
}
