namespace AlgoTrade.Core
{
    /// <summary>
    /// Hisse senedi (Stock) bar verisi
    /// </summary>
    public struct StockData
    {
        // ====================================================================
        // ANA VERİLER (Raw Data)
        // ====================================================================
        public int Id { get; set; }
        public DateTime DateTime { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public long Volume { get; set; }
        public long Size { get; set; } // Lot değeri

        // ====================================================================
        // HESAPLANMIŞ DEĞERLER (Calculated/Computed Properties)
        // ====================================================================

        /// <summary>
        /// Unix Epoch Time (saniye cinsinden)
        /// </summary>
        public readonly long EpochTime => ((DateTimeOffset)DateTime).ToUnixTimeSeconds();

        /// <summary>
        /// Fiyat farkı (Close - Open)
        /// </summary>
        public readonly double Diff => Close - Open;

        /// <summary>
        /// Yüzdelik değişim (%) - Open'a göre
        /// </summary>
        public readonly double ChangePct => Open != 0 ? ((Close - Open) / Open) * 100.0 : 0.0;

        /// <summary>
        /// Yükseliş bayrağı (Close > Open)
        /// </summary>
        public readonly bool IsBullish => Close > Open;

        /// <summary>
        /// Düşüş bayrağı (Close < Open)
        /// </summary>
        public readonly bool IsBearish => Close < Open;

        /// <summary>
        /// Nötr (Close == Open veya çok küçük değişim)
        /// Eşik değeri: %0.01
        /// </summary>
        public readonly bool IsNeutral => Math.Abs(ChangePct) < 0.01;

        /// <summary>
        /// Bar aralığı (High - Low)
        /// </summary>
        public readonly double Range => High - Low;

        /// <summary>
        /// Mum gövde boyutu (|Close - Open|)
        /// </summary>
        public readonly double BodySize => Math.Abs(Close - Open);

        /// <summary>
        /// Üst gölge/fitil uzunluğu (High - Max(Open, Close))
        /// </summary>
        public readonly double UpperShadow => High - Math.Max(Open, Close);

        /// <summary>
        /// Alt gölge/fitil uzunluğu (Min(Open, Close) - Low)
        /// </summary>
        public readonly double LowerShadow => Math.Min(Open, Close) - Low;

        /// <summary>
        /// Orta fiyat (High + Low) / 2
        /// </summary>
        public readonly double MidPrice => (High + Low) / 2.0;

        /// <summary>
        /// Tipik fiyat (High + Low + Close) / 3
        /// Teknik analizde sıkça kullanılır
        /// </summary>
        public readonly double TypicalPrice => (High + Low + Close) / 3.0;

        /// <summary>
        /// Ağırlıklı kapanış fiyatı (High + Low + Close + Close) / 4
        /// Close'a daha fazla ağırlık verir
        /// </summary>
        public readonly double WeightedClose => (High + Low + Close + Close) / 4.0;
    }
}
