using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AlgoTrade.Core.Timer
{
    // 1. Property olarak singleton
    // var sharedTimer = TimeManager.Instance;

    // 2. Method olarak singleton
    // var sharedTimer = TimeManager.GetInstance();

    // 3. Lokal yeni instance
    // var localTimer = TimeManager.GetNewInstance();

    /// <summary>
    /// Thread-safe singleton timer manager for measuring elapsed time across different parts of the application.
    /// </summary>
    public sealed class TimeManager
    {
        private static readonly Lazy<TimeManager> _instance = new Lazy<TimeManager>(() => new TimeManager());
        private readonly ConcurrentDictionary<string, Stopwatch> _timers;
        private readonly object _lockObject = new object();

        /// <summary>
        /// Gets the singleton instance of TimeManager.
        /// </summary>
        public static TimeManager Instance => _instance.Value;

        /// <summary>
        /// Gets the singleton instance of TimeManager.
        /// </summary>
        /// <returns>The shared singleton instance</returns>
        public static TimeManager GetInstance()
        {
            return Instance;
        }

        /// <summary>
        /// Creates a new instance of TimeManager for local use.
        /// Use this when you need an isolated timer manager instead of the shared singleton.
        /// </summary>
        /// <returns>A new TimeManager instance</returns>
        public static TimeManager GetNewInstance()
        {
            return new TimeManager();
        }

        private TimeManager()
        {
            _timers = new ConcurrentDictionary<string, Stopwatch>();
        }

        /// <summary>
        /// Starts a timer with the specified ID. If the timer doesn't exist, it will be created.
        /// If the timer already exists and is stopped, it will continue from where it stopped.
        /// </summary>
        /// <param name="timerId">Unique identifier for the timer</param>
        public void StartTimer(string timerId)
        {
            if (string.IsNullOrWhiteSpace(timerId))
                throw new ArgumentException("Timer ID cannot be null or empty.", nameof(timerId));

            var stopwatch = _timers.GetOrAdd(timerId, _ => new Stopwatch());

            if (!stopwatch.IsRunning)
            {
                stopwatch.Start();
            }
        }

        /// <summary>
        /// Stops the timer with the specified ID without resetting it.
        /// </summary>
        /// <param name="timerId">Unique identifier for the timer</param>
        public void StopTimer(string timerId)
        {
            if (string.IsNullOrWhiteSpace(timerId))
                throw new ArgumentException("Timer ID cannot be null or empty.", nameof(timerId));

            if (_timers.TryGetValue(timerId, out var stopwatch))
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }
            }
        }

        /// <summary>
        /// Resets the timer with the specified ID to zero and stops it.
        /// </summary>
        /// <param name="timerId">Unique identifier for the timer</param>
        public void ResetTimer(string timerId)
        {
            if (string.IsNullOrWhiteSpace(timerId))
                throw new ArgumentException("Timer ID cannot be null or empty.", nameof(timerId));

            if (_timers.TryGetValue(timerId, out var stopwatch))
            {
                stopwatch.Reset();
            }
        }

        /// <summary>
        /// Resets the timer to zero and starts it from the beginning.
        /// This is equivalent to calling ResetTimer() followed by StartTimer().
        /// Use this when you want to measure a new operation from scratch.
        /// </summary>
        /// <param name="timerId">Unique identifier for the timer</param>
        public void RestartTimer(string timerId)
        {
            if (string.IsNullOrWhiteSpace(timerId))
                throw new ArgumentException("Timer ID cannot be null or empty.", nameof(timerId));

            var stopwatch = _timers.GetOrAdd(timerId, _ => new Stopwatch());
            stopwatch.Restart();  // Reset to 0 and start
        }

        /// <summary>
        /// Starts all existing timers.
        /// </summary>
        public void StartAll()
        {
            lock (_lockObject)
            {
                foreach (var timer in _timers.Values)
                {
                    if (!timer.IsRunning)
                    {
                        timer.Start();
                    }
                }
            }
        }

        /// <summary>
        /// Stops all running timers without resetting them.
        /// </summary>
        public void StopAll()
        {
            lock (_lockObject)
            {
                foreach (var timer in _timers.Values)
                {
                    if (timer.IsRunning)
                    {
                        timer.Stop();
                    }
                }
            }
        }

        /// <summary>
        /// Resets all timers to zero and stops them.
        /// </summary>
        public void ResetAll()
        {
            lock (_lockObject)
            {
                foreach (var timer in _timers.Values)
                {
                    timer.Reset();
                }
            }
        }

        /// <summary>
        /// Gets the elapsed time in milliseconds for the specified timer.
        /// Returns the elapsed time whether the timer is running or stopped.
        /// </summary>
        /// <param name="timerId">Unique identifier for the timer</param>
        /// <returns>Elapsed time in milliseconds, or 0 if timer doesn't exist</returns>
        public long GetElapsedTime(string timerId)
        {
            if (string.IsNullOrWhiteSpace(timerId))
                throw new ArgumentException("Timer ID cannot be null or empty.", nameof(timerId));

            if (_timers.TryGetValue(timerId, out var stopwatch))
            {
                return stopwatch.ElapsedMilliseconds;
            }

            return 0;
        }

        /// <summary>
        /// Checks if a timer with the specified ID is currently running.
        /// </summary>
        /// <param name="timerId">Unique identifier for the timer</param>
        /// <returns>True if the timer exists and is running, false otherwise</returns>
        public bool IsRunning(string timerId)
        {
            if (string.IsNullOrWhiteSpace(timerId))
                throw new ArgumentException("Timer ID cannot be null or empty.", nameof(timerId));

            if (_timers.TryGetValue(timerId, out var stopwatch))
            {
                return stopwatch.IsRunning;
            }

            return false;
        }

        /// <summary>
        /// Checks if a timer with the specified ID exists.
        /// </summary>
        /// <param name="timerId">Unique identifier for the timer</param>
        /// <returns>True if the timer exists, false otherwise</returns>
        public bool TimerExists(string timerId)
        {
            if (string.IsNullOrWhiteSpace(timerId))
                return false;

            return _timers.ContainsKey(timerId);
        }

        /// <summary>
        /// Removes a timer from the manager.
        /// </summary>
        /// <param name="timerId">Unique identifier for the timer</param>
        /// <returns>True if the timer was removed, false if it didn't exist</returns>
        public bool RemoveTimer(string timerId)
        {
            if (string.IsNullOrWhiteSpace(timerId))
                throw new ArgumentException("Timer ID cannot be null or empty.", nameof(timerId));

            return _timers.TryRemove(timerId, out _);
        }

        /// <summary>
        /// Gets all timer IDs currently managed by TimeManager.
        /// </summary>
        /// <returns>Collection of timer IDs</returns>
        public IEnumerable<string> GetAllTimerIds()
        {
            return _timers.Keys.ToList();
        }

        /// <summary>
        /// Gets information about all timers including their IDs, running status, and elapsed time.
        /// </summary>
        /// <returns>Dictionary with timer IDs as keys and timer info as values</returns>
        public Dictionary<string, TimerInfo> GetAllTimers()
        {
            var result = new Dictionary<string, TimerInfo>();

            foreach (var kvp in _timers)
            {
                result[kvp.Key] = new TimerInfo
                {
                    TimerId = kvp.Key,
                    IsRunning = kvp.Value.IsRunning,
                    ElapsedMilliseconds = kvp.Value.ElapsedMilliseconds
                };
            }

            return result;
        }

        /// <summary>
        /// Clears all timers from the manager.
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                foreach (var timer in _timers.Values)
                {
                    timer.Reset();
                }
                _timers.Clear();
            }
        }
    }

    /// <summary>
    /// Contains information about a timer's current state.
    /// </summary>
    public class TimerInfo
    {
        public string TimerId { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public long ElapsedMilliseconds { get; set; }
    }
}
