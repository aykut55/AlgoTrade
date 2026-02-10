using System;
using System.Collections.Generic;
using System.Linq;

namespace AlgoTrade.Core.DataProvider
{
    /// <summary>
    /// Base class for market data access
    /// Provides common methods to extract price, volume, and time data from StockData
    /// </summary>
    public class MarketDataProvider
    {
        /// <summary>
        /// Market data (OHLCV)
        /// </summary>
        protected List<StockData> _data = new();

        /// <summary>
        /// Market data (OHLCV) - readonly property
        /// </summary>
        public List<StockData> Data => _data;

        /// <summary>
        /// Is the provider initialized with data?
        /// </summary>
        public virtual bool IsInitialized => _data != null && _data.Count > 0;
        public virtual bool IsDataRead => IsInitialized;
        public virtual bool IsDataReady => IsInitialized;

        #region Price Data Extraction

        /// <summary>
        /// Extract close prices from data
        /// </summary>
        public virtual double[] GetClosePrices()
        {
            ValidateInitialized();
            return _data.Select(d => d.Close).ToArray();
        }

        /// <summary>
        /// Extract open prices from data
        /// </summary>
        public virtual double[] GetOpenPrices()
        {
            ValidateInitialized();
            return _data.Select(d => d.Open).ToArray();
        }

        /// <summary>
        /// Extract high prices from data
        /// </summary>
        public virtual double[] GetHighPrices()
        {
            ValidateInitialized();
            return _data.Select(d => d.High).ToArray();
        }

        /// <summary>
        /// Extract low prices from data
        /// </summary>
        public virtual double[] GetLowPrices()
        {
            ValidateInitialized();
            return _data.Select(d => d.Low).ToArray();
        }

        /// <summary>
        /// Extract volume from data
        /// </summary>
        public virtual long[] GetVolume()
        {
            ValidateInitialized();
            return _data.Select(d => (long)d.Volume).ToArray();
        }

        /// <summary>
        /// Extract lot sizes from data
        /// </summary>
        public virtual long[] GetLotSizes()
        {
            ValidateInitialized();
            return _data.Select(d => (long)d.Size).ToArray();
        }

        #endregion

        #region Time Data Extraction

        /// <summary>
        /// Extract DateTime values from data
        /// </summary>
        public virtual DateTime[] GetDateTimes()
        {
            ValidateInitialized();
            return _data.Select(d => d.DateTime).ToArray();
        }

        /// <summary>
        /// Extract dates (DateOnly) from data
        /// </summary>
        public virtual DateOnly[] GetDates()
        {
            ValidateInitialized();
            return _data.Select(d => DateOnly.FromDateTime(d.DateTime)).ToArray();
        }

        /// <summary>
        /// Extract times (TimeOnly) from data
        /// </summary>
        public virtual TimeOnly[] GetTimes()
        {
            ValidateInitialized();
            return _data.Select(d => TimeOnly.FromDateTime(d.DateTime)).ToArray();
        }

        /// <summary>
        /// Extract EpochTime values from data
        /// </summary>
        public virtual long[] GetEpochTimes()
        {
            ValidateInitialized();
            return _data.Select(d => d.EpochTime).ToArray();
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validate that the provider is initialized
        /// </summary>
        protected virtual void ValidateInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException($"{GetType().Name} not initialized. Data is null or empty.");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get all data
        /// </summary>
        public virtual List<StockData> GetData() => _data;

        /// <summary>
        /// Get data by index range (inclusive)
        /// </summary>
        public virtual List<StockData> GetData(int start, int end)
        {
            if (_data == null) return new List<StockData>();
            if (start < 0) start = 0;
            if (end >= _data.Count) end = _data.Count - 1;
            if (start > end) return new List<StockData>();
            return _data.Skip(start).Take(end - start + 1).ToList();
        }

        /// <summary>
        /// Get data count
        /// </summary>
        public virtual int GetDataCount() => _data?.Count ?? 0;

        /// <summary>
        /// Get last bar index (0-based index of the last bar)
        /// Returns -1 if no data available
        /// </summary>
        public virtual int GetLastBarIndex()
        {
            int count = GetDataCount();
            return count > 0 ? count - 1 : -1;
        }

        /// <summary>
        /// Get last bar index property
        /// Returns -1 if no data available
        /// </summary>
        public virtual int LastBarIndex => GetLastBarIndex();

        /// <summary>
        /// Get data range (first and last DateTime)
        /// </summary>
        public virtual (DateTime Start, DateTime End) GetDataRange()
        {
            ValidateInitialized();
            return (_data.First().DateTime, _data.Last().DateTime);
        }

        #endregion
    }
}
