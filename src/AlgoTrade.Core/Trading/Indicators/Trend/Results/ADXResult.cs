namespace AlgoTrade.Core.Trading.Indicators.Trend.Results
{
    /// <summary>
    /// Result container for ADX (Average Directional Index) indicator
    /// </summary>
    public class ADXResult
    {
        /// <summary>ADX values (trend strength)</summary>
        public double[] ADX { get; set; } = new double[0];

        /// <summary>+DI values (positive directional indicator)</summary>
        public double[] PlusDI { get; set; } = new double[0];

        /// <summary>-DI values (negative directional indicator)</summary>
        public double[] MinusDI { get; set; } = new double[0];

        /// <summary>Period used for calculation</summary>
        public int Period { get; set; }

        /// <summary>Current ADX value</summary>
        public double CurrentADX => ADX.Length > 0 ? ADX[^1] : double.NaN;

        /// <summary>Current +DI value</summary>
        public double CurrentPlusDI => PlusDI.Length > 0 ? PlusDI[^1] : double.NaN;

        /// <summary>Current -DI value</summary>
        public double CurrentMinusDI => MinusDI.Length > 0 ? MinusDI[^1] : double.NaN;

        /// <summary>Is trend strong (ADX > 25)?</summary>
        public bool IsStrongTrend => CurrentADX > 25;

        /// <summary>Is trend weak (ADX < 20)?</summary>
        public bool IsWeakTrend => CurrentADX < 20;

        /// <summary>Is uptrend (+DI > -DI)?</summary>
        public bool IsUptrend => CurrentPlusDI > CurrentMinusDI;

        /// <summary>Is downtrend (-DI > +DI)?</summary>
        public bool IsDowntrend => CurrentMinusDI > CurrentPlusDI;

        /// <summary>Number of data points</summary>
        public int Length => ADX.Length;

        /// <summary>
        /// Constructor for ADXResult
        /// </summary>
        public ADXResult(double[] adx, double[] plusDI, double[] minusDI, int period)
        {
            ADX = adx;
            PlusDI = plusDI;
            MinusDI = minusDI;
            Period = period;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public ADXResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] adx, out double[] plusDI, out double[] minusDI)
        {
            adx = ADX;
            plusDI = PlusDI;
            minusDI = MinusDI;
        }
    }
}
