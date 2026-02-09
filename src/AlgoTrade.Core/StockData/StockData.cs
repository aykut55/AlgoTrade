namespace AlgoTrade.Core
{
    public struct StockData
    {
        public int Id { get; set; }
        public DateTime DateTime { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public long Volume { get; set; }
        public long Size { get; set; }

        public readonly long EpochTime => ((DateTimeOffset)DateTime).ToUnixTimeSeconds();
        public readonly double Diff => Close - Open;
        public readonly double ChangePct => Open != 0 ? ((Close - Open) / Open) * 100.0 : 0.0;
        public readonly bool IsBullish => Close > Open;
        public readonly bool IsBearish => Close < Open;
        public readonly bool IsNeutral => Math.Abs(ChangePct) < 0.01;
        public readonly double Range => High - Low;
        public readonly double BodySize => Math.Abs(Close - Open);
        public readonly double UpperShadow => High - Math.Max(Open, Close);
        public readonly double LowerShadow => Math.Min(Open, Close) - Low;
        public readonly double MidPrice => (High + Low) / 2.0;
        public readonly double TypicalPrice => (High + Low + Close) / 3.0;
        public readonly double WeightedClose => (High + Low + Close + Close) / 4.0;
    }
}
