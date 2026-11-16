using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Python;
using AlgoTrade.Core.Python.DearPyGuiDataPlotter;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Indicators;
using ImGuiBundlePlotter = AlgoTrade.Core.Python.DearImGuiBundleDataPlotter.DearImGuiBundleDataPlotter;
using PyGuiPlotter = AlgoTrade.Core.Python.DearPyGuiDataPlotter.DearPyGuiDataPlotter;

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

        /// <summary>
        /// CancellationToken - ESC ile iptal edildiginde true olur.
        /// Script icindeki dongulerde bunu kontrol edin.
        /// </summary>
        private CancellationTokenSource? _cts;
        public bool IsCancellationRequested => _cts?.Token.IsCancellationRequested ?? false;

        public void SetCancellationTokenSource(CancellationTokenSource cts)
        {
            _cts = cts;
        }

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

        #region Plot (menu [5] ile ayni kurallar, ayarlar script Config dosyasindan)

        /// <summary>
        /// Host (Program.cs) tarafindan verilen, menu cizimleriyle paylasilan DearImGuiBundleDataPlotter.
        /// null ise ilk cizimde script icin yeni bir ornek olusturulur.
        /// </summary>
        public ImGuiBundlePlotter? DearImGuiBundlePlotter { get; set; }

        /// <summary>
        /// Host tarafindan verilen, menu cizimleriyle paylasilan DearPyGuiDataPlotter (PlotBackend = 0).
        /// </summary>
        public PyGuiPlotter? DearPyGuiPlotter { get; set; }

        /// <summary>
        /// SingleTrader'i runSingleTraderAlgoTrade() (menu [5]) ile ayni kurallarla cizer.
        /// Ayarlar script'in kendi Config dosyasindan gelir (AppConfig.json okunmaz):
        /// pythonPlotterEnabled == 1 ise PythonPlotter da calisir; bundle plotBackend'e gore
        /// DearImGuiBundleDataPlotter (1) veya DearPyGuiDataPlotter (0) ile acilir.
        /// Pencere acik kalir, cagri bundle pencereye gonderilince doner. Uretilen bundle yolunu dondurur.
        /// </summary>
        public Task<string?> PlotSingleTraderAsync(SingleTrader trader, int plotBackend, int pythonPlotterEnabled,
                                                   string label = "SingleTrader") =>
            PlotCoreAsync(
                () => algoTrader.PlotSingleTraderData(trader),
                outDir => new TradeDataBundleConverter().ConvertSingleTrader(trader, outDir),
                plotBackend, pythonPlotterEnabled, label);

        /// <summary>
        /// MultipleTrader'i runMultipleTraderAlgoTrade() (menu [6]) ile ayni kurallarla cizer.
        /// </summary>
        public Task<string?> PlotMultipleTraderAsync(MultipleTrader multipleTrader, int plotBackend, int pythonPlotterEnabled) =>
            PlotCoreAsync(
                () => algoTrader.PlotMultipleTraderData(multipleTrader),
                outDir => new TradeDataBundleConverter().ConvertMultipleTrader(multipleTrader, outDir),
                plotBackend, pythonPlotterEnabled, "MultipleTrader");

        /// <summary>
        /// enabled true ise bundle yanina rapor paketini olusturur (menu [5] BundleReportsEnabled ile ayni).
        /// Istatistik dosyalari yazildiktan SONRA cagrilmalidir.
        /// </summary>
        public void CreateBundleReports(string? bundlePath, string traderType, bool enabled)
        {
            if (!enabled || string.IsNullOrEmpty(bundlePath)) return;
            try
            {
                string indexPath = BundleReportPackager.Create(
                    bundlePath, AppSettings.LogsDir, AppSettings.ConfigsDir, traderType);
                Log($"[BundleReports] Rapor paketi olusturuldu: {indexPath}");
            }
            catch (Exception ex)
            {
                Log($"[HATA][BundleReports] {ex.Message}");
            }
        }

        private async Task<string?> PlotCoreAsync(
            Func<Task> pythonPlot,
            Func<string, (string bundlePath, string viewPath)> convert,
            int plotBackend,
            int pythonPlotterEnabled,
            string label)
        {
            var logger = LogManager.GetInstance();

            if (pythonPlotterEnabled == 1)
            {
                if (algoTrader.SetupPython())
                    await pythonPlot();
                else
                    Log("[HATA] Python setup failed. PythonPlotter skipped.");
            }

            bool useImGuiBundle = plotBackend == 1;
            try
            {
                string bundleOutDir = Path.Combine(
                    useImGuiBundle ? AppSettings.DearImGuiBundleDataPlotterDir : AppSettings.DearPyGuiDataPlotterDir,
                    "inputs");
                var (bundlePath, viewPath) = convert(bundleOutDir);

                if (useImGuiBundle)
                {
                    var plotter = DearImGuiBundlePlotter ??= new ImGuiBundlePlotter();
                    plotter.SetLogger(logger);
                    if (!plotter.IsRunning)
                    {
                        // Kapali plotter: bundle input.json'a yazilir, plotter acilista onu yukler.
                        // Ayrica load_bundle gonderilmez - gonderilirse ayni bundle iki kez yuklenir.
                        plotter.PrepareStartupBundle(bundlePath, viewPath);
                        plotter.StartPlotter();
                    }
                    else
                    {
                        // Acik plotter: yeni bundle load_bundle komutuyla ayni pencereye yuklenir.
                        string commandFileName = plotter.LoadBundle(bundlePath, viewPath);
                        if (!await plotter.WaitForCommandProcessedAsync(commandFileName, TimeSpan.FromMinutes(2)))
                            Log("[PlotBackend] Plot yukleme onayi zaman asimina ugradi.");
                    }
                    Log($"[DearImGuiBundleDataPlotter] {label} datasi gonderildi: {bundlePath}");
                }
                else
                {
                    var plotter = DearPyGuiPlotter ??= new PyGuiPlotter();
                    plotter.SetLogger(logger);
                    plotter.StartPlotter();
                    plotter.LoadBundle(bundlePath, viewPath);
                    Log($"[DearPyGuiDataPlotter] {label} datasi yuklendi: {bundlePath}");
                }
                return bundlePath;
            }
            catch (Exception ex)
            {
                Log($"[HATA][PlotBackend] Converter/plotter hatasi: {ex.Message}");
                return null;
            }
        }

        #endregion
    }
}
