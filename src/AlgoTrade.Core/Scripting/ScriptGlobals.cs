using System;
using System.Collections.Generic;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Indicators;

namespace AlgoTrade.Core.Scripting
{
    /// <summary>
    /// Global objects and helpers exposed to C# scripts.
    /// Scripts can access these directly without any prefix.
    /// </summary>
    public class ScriptGlobals
    {
        /// <summary>
        /// Main AlgoTrader instance - provides access to traders, indicators, strategies
        /// </summary>
        public AlgoTrader algoTrader { get; set; }

        /// <summary>
        /// Stock data list loaded from file
        /// </summary>
        public List<StockData> stockData { get; set; }

        private readonly Action<string> _outputCallback;
        private readonly Action<string, object>? _resultCallback;

        // Store subscribed handlers for cleanup
        private Action<SingleTrader, int, int, double>? _progressHandler;
        private Action<SingleTrader, string, int>? _signalHandler;

        public ScriptGlobals(
            AlgoTrader trader,
            List<StockData> data,
            Action<string> outputCallback,
            Action<string, object>? resultCallback = null)
        {
            algoTrader = trader;
            stockData = data;
            _outputCallback = outputCallback;
            _resultCallback = resultCallback;
        }

        #region Logging

        /// <summary>
        /// Log a message to the script output
        /// </summary>
        public void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logLine = $"[{timestamp}] {message}";
            _outputCallback?.Invoke(logLine);
        }

        /// <summary>
        /// Log with format string support
        /// </summary>
        public void Log(string format, params object[] args)
        {
            Log(string.Format(format, args));
        }

        /// <summary>
        /// Clear the output window
        /// </summary>
        public void ClearOutput()
        {
            _outputCallback?.Invoke("[CLEAR]");
        }

        #endregion

        #region Script → Host Communication (Callbacks)

        /// <summary>
        /// Send a result/data from script to the host.
        /// Host can handle this via resultCallback.
        /// </summary>
        public void SendResult(string key, object value)
        {
            _resultCallback?.Invoke(key, value);
            Log($"[RESULT] {key}: {value}");
        }

        /// <summary>
        /// Send a simple string message to the host
        /// </summary>
        public void SendMessage(string message)
        {
            SendResult("Message", message);
        }

        #endregion

        #region Event Subscriptions

        /// <summary>
        /// Subscribe to SingleTrader progress events.
        /// Callback receives: (currentBar, totalBars)
        /// </summary>
        public void OnProgress(Action<int, int> callback, int intervalBars = 1000)
        {
            var trader = algoTrader?.SingleTrader;
            if (trader == null)
            {
                Log("[WARNING] SingleTrader not available for OnProgress subscription");
                return;
            }

            int lastReported = -intervalBars;
            _progressHandler = (t, current, total, percentage) =>
            {
                if (current - lastReported >= intervalBars || current == total - 1)
                {
                    lastReported = current;
                    callback(current, total);
                }
            };

            trader.OnProgress += _progressHandler;
            Log($"[SUBSCRIBED] OnProgress (every {intervalBars} bars)");
        }

        /// <summary>
        /// Subscribe to strategy signal events.
        /// Callback receives: (signalType, barIndex) where signalType is "A", "S", "F", etc.
        /// </summary>
        public void OnSignal(Action<string, int> callback)
        {
            var trader = algoTrader?.SingleTrader;
            if (trader == null)
            {
                Log("[WARNING] SingleTrader not available for OnSignal subscription");
                return;
            }

            _signalHandler = (t, signal, barIndex) =>
            {
                callback(signal, barIndex);
            };

            trader.OnNotifySignal += _signalHandler;
            Log("[SUBSCRIBED] OnSignal");
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Unsubscribe from all events. Call this when script execution ends.
        /// </summary>
        public void Cleanup()
        {
            var trader = algoTrader?.SingleTrader;
            if (trader != null)
            {
                if (_progressHandler != null)
                {
                    trader.OnProgress -= _progressHandler;
                    _progressHandler = null;
                }

                if (_signalHandler != null)
                {
                    trader.OnNotifySignal -= _signalHandler;
                    _signalHandler = null;
                }
            }
        }

        #endregion

        #region Helper Properties

        /// <summary>
        /// Quick access to SingleTrader
        /// </summary>
        public SingleTrader? Trader => algoTrader?.SingleTrader;

        /// <summary>
        /// Quick access to IndicatorManager
        /// </summary>
        public IndicatorManager? Indicators => algoTrader?.indicators;

        /// <summary>
        /// Total number of bars in data
        /// </summary>
        public int TotalBars => stockData?.Count ?? 0;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Run SingleTrader for all bars (convenience method).
        /// </summary>
        public void RunAll(int progressInterval = 0)
        {
            var trader = algoTrader?.SingleTrader;
            if (trader == null)
            {
                Log("[ERROR] SingleTrader not available");
                return;
            }

            if (stockData == null || stockData.Count == 0)
            {
                Log("[ERROR] No stock data loaded");
                return;
            }

            var total = stockData.Count;
            for (int i = 0; i < total; i++)
            {
                trader.Run(i);

                if (progressInterval > 0 && (i % progressInterval == 0 || i == total - 1))
                {
                    Log($"Progress: {i + 1}/{total} ({100.0 * (i + 1) / total:F1}%)");
                }
            }
        }

        /// <summary>
        /// Initialize AlgoTrader with current stockData, configure a strategy, and prepare SingleTrader.
        /// </summary>
        public void Setup(string strategyName, Dictionary<string, object>? parameters = null)
        {
            if (algoTrader == null)
            {
                Log("[ERROR] AlgoTrader not available");
                return;
            }

            if (stockData == null || stockData.Count == 0)
            {
                Log("[ERROR] No stock data loaded");
                return;
            }

            // 1. Set data and initialize
            algoTrader.SetData(stockData);
            algoTrader.Initialize();
            Log($"AlgoTrader initialized with {stockData.Count} bars");

            // 2. Configure strategy
            if (!string.IsNullOrEmpty(strategyName))
            {
                algoTrader.ConfigureStrategy(strategyName, parameters ?? new Dictionary<string, object>());
                Log($"Strategy configured: {strategyName}");
            }

            Log("Setup complete");
        }

        #endregion
    }
}
