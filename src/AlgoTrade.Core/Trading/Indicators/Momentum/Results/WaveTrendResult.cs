namespace AlgoTrade.Core.Trading.Indicators.Momentum.Results
{
    /// <summary>
    /// WaveTrend result. Based on "WaveTrend with Crosses [LazyBear]" by LazyBear.
    /// Original author attribution is intentionally preserved.
    /// </summary>
    public class WaveTrendResult
    {
        public double[] WT1 { get; set; } = Array.Empty<double>();
        public double[] WT2 { get; set; } = Array.Empty<double>();
        public double[] Histogram { get; set; } = Array.Empty<double>();
        public bool[] CrossUp { get; set; } = Array.Empty<bool>();
        public bool[] CrossDown { get; set; } = Array.Empty<bool>();

        public int ChannelLength { get; set; }
        public int AverageLength { get; set; }
        public int SignalLength { get; set; }
        public double OverboughtLevel1 { get; set; }
        public double OverboughtLevel2 { get; set; }
        public double OversoldLevel1 { get; set; }
        public double OversoldLevel2 { get; set; }

        public int Length => WT1.Length;
        public double CurrentWT1 => WT1.Length > 0 ? WT1[^1] : double.NaN;
        public double CurrentWT2 => WT2.Length > 0 ? WT2[^1] : double.NaN;
        public double CurrentHistogram => Histogram.Length > 0 ? Histogram[^1] : double.NaN;
        public bool IsCrossUp => CrossUp.Length > 0 && CrossUp[^1];
        public bool IsCrossDown => CrossDown.Length > 0 && CrossDown[^1];

        public WaveTrendResult(
            double[] wt1, double[] wt2, double[] histogram,
            bool[] crossUp, bool[] crossDown,
            int channelLength, int averageLength, int signalLength,
            double overboughtLevel1, double overboughtLevel2,
            double oversoldLevel1, double oversoldLevel2)
        {
            WT1 = wt1;
            WT2 = wt2;
            Histogram = histogram;
            CrossUp = crossUp;
            CrossDown = crossDown;
            ChannelLength = channelLength;
            AverageLength = averageLength;
            SignalLength = signalLength;
            OverboughtLevel1 = overboughtLevel1;
            OverboughtLevel2 = overboughtLevel2;
            OversoldLevel1 = oversoldLevel1;
            OversoldLevel2 = oversoldLevel2;
        }

        public WaveTrendResult() { }
    }
}
