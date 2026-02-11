namespace AlgoTrade.Core.Trading.Indicators.Trend.Results
{
    /// <summary>
    /// Result container for Ichimoku Cloud indicator
    /// </summary>
    public class IchimokuResult
    {
        /// <summary>Tenkan-sen (Conversion Line) values</summary>
        public double[] Tenkan { get; set; } = new double[0];

        /// <summary>Kijun-sen (Base Line) values</summary>
        public double[] Kijun { get; set; } = new double[0];

        /// <summary>Senkou Span A (Leading Span A) values</summary>
        public double[] SenkouA { get; set; } = new double[0];

        /// <summary>Senkou Span B (Leading Span B) values</summary>
        public double[] SenkouB { get; set; } = new double[0];

        /// <summary>Chikou Span (Lagging Span) values</summary>
        public double[] Chikou { get; set; } = new double[0];

        /// <summary>Tenkan-sen period used</summary>
        public int TenkanPeriod { get; set; }

        /// <summary>Kijun-sen period used</summary>
        public int KijunPeriod { get; set; }

        /// <summary>Senkou Span B period used</summary>
        public int SenkouPeriod { get; set; }

        /// <summary>Cloud displacement used</summary>
        public int Displacement { get; set; }

        /// <summary>Current Tenkan value</summary>
        public double CurrentTenkan => Tenkan.Length > 0 ? Tenkan[^1] : double.NaN;

        /// <summary>Current Kijun value</summary>
        public double CurrentKijun => Kijun.Length > 0 ? Kijun[^1] : double.NaN;

        /// <summary>Current Senkou A value</summary>
        public double CurrentSenkouA => SenkouA.Length > 0 ? SenkouA[^1] : double.NaN;

        /// <summary>Current Senkou B value</summary>
        public double CurrentSenkouB => SenkouB.Length > 0 ? SenkouB[^1] : double.NaN;

        /// <summary>Current Chikou value</summary>
        public double CurrentChikou => Chikou.Length > 0 ? Chikou[^1] : double.NaN;

        /// <summary>Is cloud bullish (Senkou A > Senkou B)?</summary>
        public bool IsBullishCloud => CurrentSenkouA > CurrentSenkouB;

        /// <summary>Is cloud bearish (Senkou B > Senkou A)?</summary>
        public bool IsBearishCloud => CurrentSenkouB > CurrentSenkouA;

        /// <summary>Number of data points</summary>
        public int Length => Tenkan.Length;

        /// <summary>
        /// Constructor for IchimokuResult
        /// </summary>
        public IchimokuResult(double[] tenkan, double[] kijun, double[] senkouA, double[] senkouB, double[] chikou,
            int tenkanPeriod, int kijunPeriod, int senkouPeriod, int displacement)
        {
            Tenkan = tenkan;
            Kijun = kijun;
            SenkouA = senkouA;
            SenkouB = senkouB;
            Chikou = chikou;
            TenkanPeriod = tenkanPeriod;
            KijunPeriod = kijunPeriod;
            SenkouPeriod = senkouPeriod;
            Displacement = displacement;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public IchimokuResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] tenkan, out double[] kijun, out double[] senkouA, out double[] senkouB, out double[] chikou)
        {
            tenkan = Tenkan;
            kijun = Kijun;
            senkouA = SenkouA;
            senkouB = SenkouB;
            chikou = Chikou;
        }
    }
}
