namespace AlgoTrade.Core.Trading.Indicators.Momentum.Results
{
    /// <summary>
    /// Result container for Stochastic OTT indicator
    /// </summary>
    public class StochasticOTTResult
    {
        /// <summary>Stochastic %K values</summary>
        public double[] K { get; set; } = new double[0];

        /// <summary>Stochastic %D values (smoothed K)</summary>
        public double[] D { get; set; } = new double[0];

        /// <summary>OTT filtered values</summary>
        public double[] OTT { get; set; } = new double[0];

        /// <summary>OTT Support line</summary>
        public double[] Support { get; set; } = new double[0];

        /// <summary>Stochastic K period</summary>
        public int KPeriod { get; set; }

        /// <summary>Stochastic smoothK period</summary>
        public int SmoothKPeriod { get; set; }

        /// <summary>Stochastic smoothD period</summary>
        public int SmoothDPeriod { get; set; }

        /// <summary>OTT period</summary>
        public int OTTPeriod { get; set; }

        /// <summary>OTT percent</summary>
        public double OTTPercent { get; set; }

        /// <summary>Current K value</summary>
        public double CurrentK => K.Length > 0 ? K[^1] : double.NaN;

        /// <summary>Current D value</summary>
        public double CurrentD => D.Length > 0 ? D[^1] : double.NaN;

        /// <summary>Current OTT value</summary>
        public double CurrentOTT => OTT.Length > 0 ? OTT[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => K.Length;

        /// <summary>
        /// Constructor for StochasticOTTResult
        /// </summary>
        public StochasticOTTResult(double[] k, double[] d, double[] ott, double[] support,
            int kPeriod, int smoothKPeriod, int smoothDPeriod, int ottPeriod, double ottPercent)
        {
            K = k;
            D = d;
            OTT = ott;
            Support = support;
            KPeriod = kPeriod;
            SmoothKPeriod = smoothKPeriod;
            SmoothDPeriod = smoothDPeriod;
            OTTPeriod = ottPeriod;
            OTTPercent = ottPercent;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public StochasticOTTResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] k, out double[] d, out double[] ott)
        {
            k = K;
            d = D;
            ott = OTT;
        }
    }
}
