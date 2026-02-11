namespace AlgoTrade.Core.Trading.Indicators.PriceAction.Results
{
    /// <summary>
    /// Result container for Highest High Value (HHV) and Lowest Low Value (LLV)
    /// These indicators calculate the highest and lowest values over a rolling period
    /// </summary>
    public class HHVLLVResult
    {
        /// <summary>Highest High Value array (highest value in the last N periods)</summary>
        public double[] HHV { get; set; } = new double[0];

        /// <summary>Lowest Low Value array (lowest value in the last N periods)</summary>
        public double[] LLV { get; set; } = new double[0];

        /// <summary>Period used for calculation</summary>
        public int Period { get; set; }

        /// <summary>Current HHV value (most recent bar)</summary>
        public double CurrentHHV => HHV.Length > 0 ? HHV[^1] : double.NaN;

        /// <summary>Current LLV value (most recent bar)</summary>
        public double CurrentLLV => LLV.Length > 0 ? LLV[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => HHV.Length;

        /// <summary>
        /// Constructor for HHVLLVResult
        /// </summary>
        public HHVLLVResult(double[] hhv, double[] llv, int period)
        {
            HHV = hhv;
            LLV = llv;
            Period = period;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public HHVLLVResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] hhv, out double[] llv)
        {
            hhv = HHV;
            llv = LLV;
        }
    }
}
