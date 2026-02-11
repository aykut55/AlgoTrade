namespace AlgoTrade.Core.Trading.Indicators.SupportResistance.Results
{
    /// <summary>
    /// Result container for Pivot Points indicator
    /// </summary>
    public class PivotPointsResult
    {
        /// <summary>Pivot point values</summary>
        public double[] Pivot { get; set; } = new double[0];

        /// <summary>Resistance level 1</summary>
        public double[] R1 { get; set; } = new double[0];

        /// <summary>Resistance level 2</summary>
        public double[] R2 { get; set; } = new double[0];

        /// <summary>Resistance level 3</summary>
        public double[] R3 { get; set; } = new double[0];

        /// <summary>Support level 1</summary>
        public double[] S1 { get; set; } = new double[0];

        /// <summary>Support level 2</summary>
        public double[] S2 { get; set; } = new double[0];

        /// <summary>Support level 3</summary>
        public double[] S3 { get; set; } = new double[0];

        /// <summary>Current pivot point value</summary>
        public double CurrentPivot => Pivot.Length > 0 ? Pivot[^1] : double.NaN;

        /// <summary>Current R1 value</summary>
        public double CurrentR1 => R1.Length > 0 ? R1[^1] : double.NaN;

        /// <summary>Current R2 value</summary>
        public double CurrentR2 => R2.Length > 0 ? R2[^1] : double.NaN;

        /// <summary>Current R3 value</summary>
        public double CurrentR3 => R3.Length > 0 ? R3[^1] : double.NaN;

        /// <summary>Current S1 value</summary>
        public double CurrentS1 => S1.Length > 0 ? S1[^1] : double.NaN;

        /// <summary>Current S2 value</summary>
        public double CurrentS2 => S2.Length > 0 ? S2[^1] : double.NaN;

        /// <summary>Current S3 value</summary>
        public double CurrentS3 => S3.Length > 0 ? S3[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => Pivot.Length;

        /// <summary>
        /// Constructor for PivotPointsResult
        /// </summary>
        public PivotPointsResult(double[] pivot, double[] r1, double[] r2, double[] r3,
            double[] s1, double[] s2, double[] s3)
        {
            Pivot = pivot;
            R1 = r1;
            R2 = r2;
            R3 = r3;
            S1 = s1;
            S2 = s2;
            S3 = s3;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public PivotPointsResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] pivot, out double[] r1, out double[] r2, out double[] r3,
            out double[] s1, out double[] s2, out double[] s3)
        {
            pivot = Pivot;
            r1 = R1;
            r2 = R2;
            r3 = R3;
            s1 = S1;
            s2 = S2;
            s3 = S3;
        }
    }
}
