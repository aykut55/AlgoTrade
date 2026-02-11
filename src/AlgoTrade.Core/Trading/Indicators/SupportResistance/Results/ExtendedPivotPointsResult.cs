namespace AlgoTrade.Core.Trading.Indicators.SupportResistance.Results
{
    /// <summary>
    /// Result container for Extended Pivot Points indicator
    /// Includes additional levels R4, R5, S4, S5 for extended range trading
    /// </summary>
    public class ExtendedPivotPointsResult
    {
        /// <summary>Pivot point values</summary>
        public double[] Pivot { get; set; } = new double[0];

        /// <summary>Resistance level 1</summary>
        public double[] R1 { get; set; } = new double[0];

        /// <summary>Resistance level 2</summary>
        public double[] R2 { get; set; } = new double[0];

        /// <summary>Resistance level 3</summary>
        public double[] R3 { get; set; } = new double[0];

        /// <summary>Resistance level 4 (Extended)</summary>
        public double[] R4 { get; set; } = new double[0];

        /// <summary>Resistance level 5 (Extended)</summary>
        public double[] R5 { get; set; } = new double[0];

        /// <summary>Support level 1</summary>
        public double[] S1 { get; set; } = new double[0];

        /// <summary>Support level 2</summary>
        public double[] S2 { get; set; } = new double[0];

        /// <summary>Support level 3</summary>
        public double[] S3 { get; set; } = new double[0];

        /// <summary>Support level 4 (Extended)</summary>
        public double[] S4 { get; set; } = new double[0];

        /// <summary>Support level 5 (Extended)</summary>
        public double[] S5 { get; set; } = new double[0];

        /// <summary>Current pivot point value</summary>
        public double CurrentPivot => Pivot.Length > 0 ? Pivot[^1] : double.NaN;

        /// <summary>Current R1 value</summary>
        public double CurrentR1 => R1.Length > 0 ? R1[^1] : double.NaN;

        /// <summary>Current R2 value</summary>
        public double CurrentR2 => R2.Length > 0 ? R2[^1] : double.NaN;

        /// <summary>Current R3 value</summary>
        public double CurrentR3 => R3.Length > 0 ? R3[^1] : double.NaN;

        /// <summary>Current R4 value</summary>
        public double CurrentR4 => R4.Length > 0 ? R4[^1] : double.NaN;

        /// <summary>Current R5 value</summary>
        public double CurrentR5 => R5.Length > 0 ? R5[^1] : double.NaN;

        /// <summary>Current S1 value</summary>
        public double CurrentS1 => S1.Length > 0 ? S1[^1] : double.NaN;

        /// <summary>Current S2 value</summary>
        public double CurrentS2 => S2.Length > 0 ? S2[^1] : double.NaN;

        /// <summary>Current S3 value</summary>
        public double CurrentS3 => S3.Length > 0 ? S3[^1] : double.NaN;

        /// <summary>Current S4 value</summary>
        public double CurrentS4 => S4.Length > 0 ? S4[^1] : double.NaN;

        /// <summary>Current S5 value</summary>
        public double CurrentS5 => S5.Length > 0 ? S5[^1] : double.NaN;

        /// <summary>Number of data points</summary>
        public int Length => Pivot.Length;

        /// <summary>
        /// Constructor for ExtendedPivotPointsResult
        /// </summary>
        public ExtendedPivotPointsResult(double[] pivot,
            double[] r1, double[] r2, double[] r3, double[] r4, double[] r5,
            double[] s1, double[] s2, double[] s3, double[] s4, double[] s5)
        {
            Pivot = pivot;
            R1 = r1;
            R2 = r2;
            R3 = r3;
            R4 = r4;
            R5 = r5;
            S1 = s1;
            S2 = s2;
            S3 = s3;
            S4 = s4;
            S5 = s5;
        }

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public ExtendedPivotPointsResult()
        {
        }

        /// <summary>
        /// Deconstruct method for tuple-like usage
        /// </summary>
        public void Deconstruct(out double[] pivot,
            out double[] r1, out double[] r2, out double[] r3, out double[] r4, out double[] r5,
            out double[] s1, out double[] s2, out double[] s3, out double[] s4, out double[] s5)
        {
            pivot = Pivot;
            r1 = R1;
            r2 = R2;
            r3 = R3;
            r4 = R4;
            r5 = R5;
            s1 = S1;
            s2 = S2;
            s3 = S3;
            s4 = S4;
            s5 = S5;
        }
    }
}
