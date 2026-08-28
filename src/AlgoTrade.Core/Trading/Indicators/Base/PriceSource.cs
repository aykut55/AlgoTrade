namespace AlgoTrade.Core.Trading.Indicators.Base
{
    /// <summary>
    /// Bir indikatörün besleneceği fiyat serisi (TradingView'deki "source" / MT'deki "applied price").
    /// </summary>
    public enum PriceSource
    {
        /// <summary>Kapanış (close)</summary>
        Close,
        /// <summary>Açılış (open)</summary>
        Open,
        /// <summary>Yüksek (high)</summary>
        High,
        /// <summary>Düşük (low)</summary>
        Low,
        /// <summary>Medyan fiyat - HL2 = (high + low) / 2</summary>
        Median,
        /// <summary>Tipik fiyat - HLC3 = (high + low + close) / 3</summary>
        Typical,
        /// <summary>Ağırlıklı kapanış - HLCC4 = (high + low + close + close) / 4</summary>
        Weighted,
        /// <summary>Ortalama fiyat - OHLC4 = (open + high + low + close) / 4</summary>
        Average
    }
}
