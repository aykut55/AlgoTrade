using System;
using System.Globalization;

namespace AlgoTrade.Core.Trading.Core
{
    /// <summary>
    /// Time utilities - Handles time-based trading filters and controls
    /// Used for session filtering, day-end operations, and time-based rules
    /// </summary>
    public class TimeUtils
    {
        private SingleTrader Trader { get; set; }

        #region Time Filtering

        public bool TimeFilteringEnabled { get; set; }
        public bool GunSonuPozKapatEnabled { get; set; }
        public bool GunBasiPozAcEnabled { get; set; }

        #endregion

        #region Trading Hours

        public TimeOnly BaslangicSaati { get; set; }
        public TimeOnly BitisSaati { get; set; }
        public TimeOnly GunSonuKapatmaSaati { get; set; }

        #endregion

        #region Current Time Info

        public DateTime CurrentBarTime { get; set; }
        public TimeOnly CurrentTime { get; set; }
        public DateOnly CurrentDate { get; set; }

        #endregion

        #region Session Info

        public bool IsInTradingSession { get; set; }
        public bool IsFirstBarOfDay { get; set; }
        public bool IsLastBarOfDay { get; set; }
        public bool IsAfterCloseTime { get; set; }

        #endregion

        #region Constructor

        public TimeUtils()
        {
            // Initialize with default values
            Reset();
        }

        #endregion

        #region Methods

        public void SetTrader(SingleTrader trader)
        {
            Trader = trader;
        }

        /// <summary>
        /// Reset all time utility values to defaults
        /// </summary>
        public TimeUtils Reset()
        {
            TimeFilteringEnabled = false;
            GunSonuPozKapatEnabled = false;
            GunBasiPozAcEnabled = false;

            // Default trading hours (Istanbul: 09:30 - 18:00)
            BaslangicSaati = new TimeOnly(9, 30, 0);
            BitisSaati = new TimeOnly(18, 0, 0);
            GunSonuKapatmaSaati = new TimeOnly(17, 55, 0);

            CurrentBarTime = DateTime.MinValue;
            CurrentTime = TimeOnly.MinValue;
            CurrentDate = DateOnly.MinValue;

            IsInTradingSession = false;
            IsFirstBarOfDay = false;
            IsLastBarOfDay = false;
            IsAfterCloseTime = false;

            return this;
        }

        /// <summary>
        /// Initialize time utils - Does nothing currently but returns this for method chaining
        /// </summary>
        public TimeUtils Init() { return this; }

        public TimeUtils InitOrReuse()
        {
            // Stateless; nothing to allocate
            return this;
        }

        /// <summary>
        /// Update current time from bar data
        /// </summary>
        public void UpdateTime(DateTime barTime)
        {
            CurrentBarTime = barTime;
            CurrentTime = TimeOnly.FromDateTime(barTime);
            CurrentDate = DateOnly.FromDateTime(barTime);

            // Update session status
            UpdateSessionStatus();
        }

        /// <summary>
        /// Update trading session status
        /// </summary>
        private void UpdateSessionStatus()
        {
            if (!TimeFilteringEnabled)
            {
                IsInTradingSession = true;
                return;
            }

            // Check if current time is within trading hours
            IsInTradingSession = CurrentTime >= BaslangicSaati && CurrentTime <= BitisSaati;

            // Check if it's after close time for day-end position closing
            IsAfterCloseTime = CurrentTime >= GunSonuKapatmaSaati;
        }

        /// <summary>
        /// Check if current time is within trading hours
        /// </summary>
        public bool IsWithinTradingHours()
        {
            if (!TimeFilteringEnabled)
                return true;

            return IsInTradingSession;
        }

        /// <summary>
        /// Check if it's time to close day-end positions
        /// </summary>
        public bool ShouldCloseDayEndPosition()
        {
            if (!GunSonuPozKapatEnabled)
                return false;

            return IsAfterCloseTime;
        }

        /// <summary>
        /// Compare bar time with given time string
        /// Returns: -1 if bar time < given time, 0 if equal, 1 if bar time > given time
        /// </summary>
        public int check_bar_time_with(int barIndex, string timeStr)
        {
            if (Trader == null || barIndex < 0 || barIndex >= Trader.Data.Count)
                return 0;

            DateTime barDateTime = Trader.Data[barIndex].DateTime;
            TimeOnly barTime = TimeOnly.FromDateTime(barDateTime);

            // Parse time string (format: "HH:mm:ss" or "HH:mm")
            if (TimeOnly.TryParseExact(timeStr, new[] { "HH:mm:ss", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly compareTime))
            {
                return barTime.CompareTo(compareTime);
            }

            return 0;
        }

        /// <summary>
        /// Compare bar date with given date string
        /// Returns: -1 if bar date < given date, 0 if equal, 1 if bar date > given date
        /// </summary>
        public int check_bar_date_with(int barIndex, string dateStr)
        {
            if (Trader == null || barIndex < 0 || barIndex >= Trader.Data.Count)
                return 0;

            DateTime barDateTime = Trader.Data[barIndex].DateTime;
            DateOnly barDate = DateOnly.FromDateTime(barDateTime);

            // Parse date string (format: "yyyy.MM.dd")
            if (DateOnly.TryParseExact(dateStr, new[] { "yyyy.MM.dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly compareDate))
            {
                return barDate.CompareTo(compareDate);
            }

            return 0;
        }

        /// <summary>
        /// Compare bar datetime with given datetime string
        /// Returns: -1 if bar datetime < given datetime, 0 if equal, 1 if bar datetime > given datetime
        /// </summary>
        public int check_bar_date_time_with(int barIndex, string dateTimeStr)
        {
            if (Trader == null || barIndex < 0 || barIndex >= Trader.Data.Count)
                return 0;

            DateTime barDateTime = Trader.Data[barIndex].DateTime;

            // Parse datetime string (format: "yyyy.MM.dd HH:mm:ss")
            if (DateTime.TryParseExact(dateTimeStr, new[] { "yyyy.MM.dd HH:mm:ss", "yyyy.MM.dd HH:mm" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime compareDateTime))
            {
                return barDateTime.CompareTo(compareDateTime);
            }

            return 0;
        }

        #endregion
    }
}
