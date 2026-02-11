namespace AlgoTrade.Core.Trading.Indicators.Momentum.Results
{
    /// <summary>
    /// Result container for OTTO (Optimized Trend Tracker Oscillator) indicator
    /// </summary>
    public class OTTOResult
    {
        /// <summary>OTTO oscillator values</summary>
        public double[] OTTO { get; set; } = new double[0];

        /// <summary>Fast VIDYA values</summary>
        public double[] FastVIDYA { get; set; } = new double[0];

        /// <summary>Slow VIDYA values</summary>
        public double[] SlowVIDYA { get; set; } = new double[0];

        /// <summary>Buy signals (1 = buy, 0 = no signal)</summary>
        public int[] BuySignals { get; set; } = new int[0];

        /// <summary>Sell signals (1 = sell, 0 = no signal)</summary>
        public int[] SellSignals { get; set; } = new int[0];

        /// <summary>Fast VIDYA period</summary>
        public int FastPeriod { get; set; }

        /// <summary>Slow VIDYA period</summary>
        public int SlowPeriod { get; set; }

        /// <summary>Correction constant</summary>
        public double CorrectionConstant { get; set; }

        /// <summary>Current OTTO value</summary>
        public double Current => OTTO.Length > 0 ? OTTO[^1] : double.NaN;

        /// <summary>Current Fast VIDYA value</summary>
        public double CurrentFast => FastVIDYA.Length > 0 ? FastVIDYA[^1] : double.NaN;

        /// <summary>Current Slow VIDYA value</summary>
        public double CurrentSlow => SlowVIDYA.Length > 0 ? SlowVIDYA[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => OTTO.Length;

        /// <summary>
        /// Constructor for OTTOResult
        /// </summary>
        public OTTOResult(double[] otto, double[] fastVIDYA, double[] slowVIDYA, int[] buySignals, int[] sellSignals,
            int fastPeriod, int slowPeriod, double correctionConstant)
        {
            OTTO = otto;
            FastVIDYA = fastVIDYA;
            SlowVIDYA = slowVIDYA;
            BuySignals = buySignals;
            SellSignals = sellSignals;
            FastPeriod = fastPeriod;
            SlowPeriod = slowPeriod;
            CorrectionConstant = correctionConstant;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public OTTOResult()
        {
        }
    }
}
