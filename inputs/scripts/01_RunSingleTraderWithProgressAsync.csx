// =============================================================================
// RunSingleTraderWithProgressAsync.csx - SingleTrader Inlined Execution
// Config_01_SingleTrader.csx'den gelen konfigürasyonu kullanarak SingleTrader çalıştırır
// =============================================================================
#load "Config_01_SingleTrader.csx"

using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Strategy;
using AlgoTrade.Core.Trading.Query;
using AlgoTrade.Core.Python;
using AlgoTrade.Core.Python.DearPyGuiDataPlotter;
using AlgoTrade.Core.Timer;

// =============================================================================
// Degiskenler
// =============================================================================
StockDataReader? stockDataReader = null;
ConcurrentDictionary<string, string>? stockMetaData = null;
var timeManager = TimeManager.GetInstance();

// =============================================================================
// Local Methods
// =============================================================================
void OnApplyUserFlags(SingleTrader trader)
{
    trader.ConfigureUserFlagsOnce();
    trader.signals.AlEnabled                   = true;
    trader.signals.SatEnabled                  = true;
    trader.signals.FlatOlEnabled               = true;
    trader.signals.PasGecEnabled               = true;
    trader.signals.KarAlEnabled                = true;
    trader.signals.ZararKesEnabled             = true;
    trader.signals.GunSonuPozKapatEnabled      = false;
    trader.signals.TimeFilteringEnabled        = false;
    trader.signals.EquityCurveFilteringEnabled = false;

    var dateTimes       = new string[] { "2025.05.25 09:35:00", "2025.06.02 17:55:00" };
    trader.StartDateTimeStr = dateTimes[0];
    trader.StopDateTimeStr  = dateTimes[1];

    var startDt         = DateTime.ParseExact(dateTimes[0], "yyyy.MM.dd HH:mm:ss", null);
    trader.StartDateStr     = startDt.ToString("yyyy.MM.dd");
    trader.StartTimeStr     = startDt.ToString("HH:mm:ss");

    var stopDt          = DateTime.ParseExact(dateTimes[1], "yyyy.MM.dd HH:mm:ss", null);
    trader.StopDateStr      = stopDt.ToString("yyyy.MM.dd");
    trader.StopTimeStr      = stopDt.ToString("HH:mm:ss");
}

void SetSingleTraderConfigureEquityCurveFilter(SingleTrader trader)
{
    trader.signals.EquityCurveFilteringEnabled = ecfEnabled;
    trader.ConfigureEquityCurveFilter(
        isPercent: ecfThresholdTypeIsPercent,
        profitThreshold: ecfProfitThreshold,
        lossThreshold: ecfLossThreshold,
        trigger: ecfTrigger
    );
}

void OnApplyUserFlags2(SingleTrader trader)
{
    trader.OptimizationEnabled                 = false;
    trader.SaveStatisticsToFile                = saveStatisticsToFile;
    trader.SaveFullStatsTxtEnabled             = true;
    trader.SaveFullStatsCsvEnabled             = true;
    trader.SaveMinimalStatsTxtEnabled          = true;
    trader.SaveMinimalStatsCsvEnabled          = true;
    trader.SaveFullListsTxtEnabled             = true;
    trader.SaveFullListsCsvEnabled             = true;
    trader.SaveMinimalListsTxtEnabled          = true;
    trader.SaveMinimalListsCsvEnabled          = true;
    trader.SaveFullStatsTxtFormattedEnabled    = true;
    trader.SaveMinimalStatsTxtFormattedEnabled = true;
    trader.SavePerformansTxtEnabled            = true;
    trader.SavePerformansCsvEnabled            = true;
    trader.FullStatsTxtFileName                = "SingleTraderStatistics.txt";
    trader.FullStatsCsvFileName                = "SingleTraderStatistics.csv";
    trader.MinimalStatsTxtFileName             = "SingleTraderStatisticsMinimal.txt";
    trader.MinimalStatsCsvFileName             = "SingleTraderStatisticsMinimal.csv";
    trader.FullListsTxtFileName                = "SingleTraderLists.txt";
    trader.FullListsCsvFileName                = "SingleTraderLists.csv";
    trader.MinimalListsTxtFileName             = "SingleTraderListsMinimal.txt";
    trader.MinimalListsCsvFileName             = "SingleTraderListsMinimal.csv";
    trader.FullStatsTxtFormattedFileName       = "SingleTraderStatisticsFormatted.txt";
    trader.MinimalStatsTxtFormattedFileName    = "SingleTraderStatisticsMinimalFormatted.txt";
    trader.PerformansTxtFileName               = "SingleTraderPerformans.txt";
    trader.PerformansCsvFileName               = "SingleTraderPerformans.csv";

    // Export (AppConfigApplier.cs:121-129 ile ayni - versiyonlu sutun tanimlariyla ek yazim)
    trader.ExportEnabled    = exportEnabled;
    trader.ExportConfigFile = exportConfigFile;
    trader.ExportVersion    = exportVersion;
}

// =============================================================================
// 1. Veri Oku
// =============================================================================
Log("=== RunSingleTraderWithProgressAsync.csx ===");

if (!File.Exists(stockDataFullFileName))
{
    Log($"[HATA] Dosya bulunamadi: {stockDataFullFileName}");
    return;
}

stockDataReader = new StockDataReader();

// Program.cs:64-92 (OnReadMetaData/OnProgress) ile ayni icerik/tetikleme noktasi -
// [5] menusundeki karsiliginin birebir aynisi.
stockDataReader.OnReadMetaData += (reader, metaData) =>
{
    if (!reader.IsMetaDataRead) return;
    int padding = 18;
    Log($"{"\tRecord Time".PadRight(padding)}: {metaData.GetValueOrDefault("Kayit_Zamani", "N/A")}");
    Log($"{"\tChart Symbol".PadRight(padding)}: {metaData.GetValueOrDefault("GrafikSembol", "N/A")}");
    Log($"{"\tChart Period".PadRight(padding)}: {metaData.GetValueOrDefault("GrafikPeriyot", "N/A")}");
    Log($"{"\tBar Count".PadRight(padding)}: {metaData.GetValueOrDefault("BarCount", "N/A")}");
    Log($"{"\tStart Date".PadRight(padding)}: {metaData.GetValueOrDefault("Baslangic_Tarihi", "N/A")}");
    Log($"{"\tEnd Date".PadRight(padding)}: {metaData.GetValueOrDefault("Bitis_Tarihi", "N/A")}");
    Log($"{"\tFormat".PadRight(padding)}: {metaData.GetValueOrDefault("Format", "N/A")}");
};
stockDataReader.OnProgress += (reader, count, isCompleted) =>
{
    Log(isCompleted ? $"\tRecord count     : {count}" : $"\tRecord no        : {count}");
};

stockDataReader.ReadMetaData(stockDataFullFileName);

if (!stockDataReader.IsMetaDataRead)
{
    Log("[HATA] MetaData okunamadi.");
    return;
}

stockMetaData = stockDataReader.GetMetaData();
symbolName = stockMetaData.GetValueOrDefault("GrafikSembol", "N/A");
symbolPeriod = stockMetaData.GetValueOrDefault("GrafikPeriyot", "N/A");

Enum.TryParse<StockDataReader.FilterMode>(readDataFilterMode, ignoreCase: true, out var filterMode);
DateTime? dt1 = string.IsNullOrWhiteSpace(readDataDt1) ? null : DateTime.Parse(readDataDt1);
DateTime? dt2 = string.IsNullOrWhiteSpace(readDataDt2) ? null : DateTime.Parse(readDataDt2);

stockDataReader.ReadDataFast(stockDataFullFileName, filterMode, readDataN1, readDataN2, dt1, dt2);
var data = stockDataReader.GetData();
Log($"Okunan    : {data.Count} bar");

if (data.Count == 0)
{
    Log("[HATA] Data bos.");
    return;
}

// Program.cs:680-686 (addHeadTailInfo) ile ayni - [5]'te de varsayilan/hep kapali (bkz. Config_01_SingleTrader.csx).
if (addHeadTailInfo)
{
    Log("");
    Log(stockDataReader.Head());
    Log("");
    Log(stockDataReader.Tail());
}

// =============================================================================
// 2. AlgoTrader Konfigure Et
// =============================================================================
algoTrader.SetData(data);

algoTrader.SymbolName = symbolName;
algoTrader.SymbolPeriod = symbolPeriod;

algoTrader.SingleTraderRunMode = selectedRunMode;

algoTrader.ConfigureStrategy(strategyName, strategyParams);

if (queryEnabled)
{
    algoTrader.ConfigureQuery(queryName, queryParams);
}

algoTrader.EquityCurveFilteringEnabled = ecfEnabled;
algoTrader.ThresholdTypeIsPercent = ecfThresholdTypeIsPercent;
algoTrader.ProfitConfirmationThreshold = ecfProfitThreshold;
algoTrader.LossConfirmationThreshold = ecfLossThreshold;
algoTrader.ConfirmationTrigger = ecfTrigger;

algoTrader.Initialize();

Log($"\n{algoTrader.GetDataInfo()}");

// AlgoTrade.cs:1270 RestartTimer("0") ile ayni baslangic noktasi (indicators olusturmadan hemen once).
timeManager.RestartTimer("0");

// =============================================================================
// 3. Indicators Olustur
// =============================================================================
Log("\nCreating indicators...");
var indicators = algoTrader.CreateIndicators();

// =============================================================================
// 4. Strategy Olustur
// =============================================================================
Log($"\nCreating strategy: {strategyName}");
var strategy = algoTrader.CreateConfiguredStrategy(indicators);

// =============================================================================
// 5. Query Olustur
// =============================================================================
IQuery? query = null;
if (queryEnabled)
{
    Log($"\nCreating query: {queryName}");
    query = algoTrader.CreateConfiguredQuery(indicators);
}

// =============================================================================
// 6. SingleTrader Olustur ve Konfigure Et
// =============================================================================
Log("\nCreating singleTrader...");

var singleTrader = new SingleTrader(0, "singleTrader", data, indicators, null);

// =============================================================================
// 6b. Callbacks
// =============================================================================
Action<SingleTrader, int> onReset = (trader, barIndex) =>
{
    Log($"[CB] onReset: trader={trader.GetName()}");
};

Action<SingleTrader, int> onInit = (trader, barIndex) =>
{
    // Log($"[CB] onInit: trader={trader.GetName()}");
};

Action<SingleTrader, int> onRun = (trader, barIndex) =>
{
    // Log($"[CB] onRun: bar={barIndex}");
};

Action<SingleTrader, int> onFinal = (trader, barIndex) =>
{
    // Log($"[CB] onFinal: trader={trader.GetName()}");
};

Action<SingleTrader, int> onBeforeOrders = (trader, barIndex) =>
{
    // Log($"[CB] onBeforeOrders: bar={barIndex}");
};

Action<SingleTrader, string, int> onNotifySignal = (trader, signal, barIndex) =>
{
    // Log($"[CB] onNotifySignal: signal={signal}, bar={barIndex}");
};

Action<SingleTrader, int> onAfterOrders = (trader, barIndex) =>
{
    // Log($"[CB] onAfterOrders: bar={barIndex}");
};

Action<SingleTrader, int, int, double> onProgress = (trader, current, total, percentage) =>
{
    // Log($"[CB] onProgress: {current}/{total} ({percentage:F1}%)");
};

singleTrader.ClearCallbacks()
    .SetCallbacks(
        onReset: onReset,
        onInit: onInit,
        onRun: onRun,
        onFinal: onFinal,
        onBeforeOrders: onBeforeOrders,
        onNotifySignal: onNotifySignal,
        onAfterOrders: onAfterOrders,
        onProgress: onProgress
    );

// Reset
singleTrader.Reset();

// Set attributes
singleTrader.SymbolName   = symbolName;
singleTrader.SymbolPeriod = symbolPeriod;
singleTrader.StrategyName = strategyName;
singleTrader.QueryName    = queryEnabled ? queryName : "...";
singleTrader.LastExecutionTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
singleTrader.LastExecutionTimeStart = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

// Configure position sizing
singleTrader.initialTradeParams!.Reset()
    .SetBakiyeParams(ilkBakiye: ilkBakiye)
    .SetKontratParamsViopEndex(kontratSayisi: kontratSayisi)
    .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
    .SetKaymaParams(kaymaMiktari: kaymaMiktari);

// Apply user flags
OnApplyUserFlags(singleTrader);

// Apply user flags 2
OnApplyUserFlags2(singleTrader);

// Configure equity curve filter
SetSingleTraderConfigureEquityCurveFilter(singleTrader);

// Assign runMode
singleTrader.RunMode = selectedRunMode;

if (singleTrader.RunMode == TraderRunMode.TradeOnly || singleTrader.RunMode == TraderRunMode.TradeAndQuery)
{
    singleTrader.SetStrategy(strategy);
    Log($"Strategy configured: {strategyName}");
}

if (singleTrader.RunMode == TraderRunMode.TradeAndQuery || singleTrader.RunMode == TraderRunMode.QueryOnly)
{
    if (query is not null)
    {
        singleTrader.SetQuery(query);
        Log($"Query configured: {queryName}");
    }
}

// Init
singleTrader.Init();

// AlgoTrade.cs:1410-1412 RestartTimer("1")+RestartTimer("2") ile ayni nokta (Init sonrasi, run loop'tan once).
timeManager.RestartTimer("1");
timeManager.RestartTimer("2");

// =============================================================================
// 7. Run Loop
// =============================================================================
int totalBars = data.Count;

Log($"\nRunning singleTrader... Total bars: {totalBars}");

singleTrader.IsStarted = true;
singleTrader.IsRunning = true;
singleTrader.IsStopped = false;
singleTrader.IsStopRequested = false;

int updateFreq = 5;

for (int i = 0; i < totalBars; i++)
{
    if (IsCancellationRequested)
    {
        Log($"Script cancelled by ESC at bar {i}/{totalBars}");
        break;
    }

    if (singleTrader.IsStopRequested)
    {
        Log($"SingleTrader stopped by user request at bar {i}/{totalBars}");
        break;
    }

    singleTrader.Run(i);

    // Progress reporting
    double percentage = (i + 1) / (double)totalBars * 100.0;
    int prevPercentBucket = (int)(((i) / (double)totalBars * 100.0) / updateFreq);
    int currPercentBucket = (int)(percentage / updateFreq);
    if (currPercentBucket > prevPercentBucket || i + 1 >= totalBars)
    {
        Log($"Progress: {i + 1}/{totalBars} ({percentage:F1}%)");
    }
}

timeManager.StopTimer("2");

singleTrader.LastExecutionTimeStop = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
singleTrader.LastExecutionTimeInMSec = timeManager.GetElapsedTime("2").ToString();

// =============================================================================
// 8. Tarama Bilgileri
// =============================================================================
if (selectedRunMode == TraderRunMode.TradeOnly || selectedRunMode == TraderRunMode.TradeAndQuery)
{
    var ozet = singleTrader.TaramaOzeti;
    Log($"\nScreening summary: {ozet}");
}

// =============================================================================
// 9. Finalize
// =============================================================================
Log("\nFinalizing singleTrader...");

// AlgoTrade.cs:1468/1496 RestartTimer("3")/StopTimer("3") ile ayni sinir - sadece Finalize() maliyeti,
// dosyaya yazma (WriteStatisticsToFile) menude de bu olcumun disinda (ayri bir asamada).
timeManager.RestartTimer("3");
singleTrader.Finalize();
timeManager.StopTimer("3");

if (!IsCancellationRequested && !singleTrader.IsStopRequested && singleTrader.SaveStatisticsToFile)
{
    if (!singleTrader.OptimizationEnabled)
    {
        Log("\nSaving statistics to files...");
        singleTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
    }
    else
    {
        Log("\nSkipping full statistics write in optimization mode...");
    }
}

// =============================================================================
// 10. Query Ozeti
// =============================================================================
if (selectedRunMode == TraderRunMode.TradeAndQuery || selectedRunMode == TraderRunMode.QueryOnly)
{
    var sorguOzeti = singleTrader.SorguOzeti;
    Log($"\nQuery summary: {sorguOzeti}");
}

// AlgoTrade.cs:1506/1508 StopTimer("1")/StopTimer("0") ile ayni nokta (query summary sonrasi,
// plot'tan once - menude de t0/t1 olcumu plot'u kapsamiyor, bkz. asagidaki "9b. Plot" notu).
timeManager.StopTimer("1");
timeManager.StopTimer("0");

// =============================================================================
// 11. Sonuc
// =============================================================================
singleTrader.IsRunning = false;
singleTrader.IsStopped = true;

var t0 = timeManager.GetElapsedTime("0");
var t1 = timeManager.GetElapsedTime("1");
var t2 = timeManager.GetElapsedTime("2");
var t3 = timeManager.GetElapsedTime("3");

Log($"\nt0 = {t0} msec. <==> RunSingleTraderWithProgressAsync elapsed time");
Log($"\nt1 = {t1} msec. <==> Running + Finalizing singleTrader elapsed time");
Log($"\nt2 = {t2} msec. <==> Running singleTrader elapsed time");
Log($"\nt3 = {t3} msec. <==> Finalizing singleTrader elapsed time");

Log($"\nProcessed {totalBars} bars.");

// =============================================================================
// 9b. Plot (pythonnet + DearPyGuiDataPlotter)
// t0-t3 olcumunden SONRA calisiyor - menude de Plot, RunSingleTraderWithProgressAsync()
// donduktan (yani t0-t3 hesaplandiktan) SONRA, runSingleTraderAlgoTrade() icinde tetikleniyor
// (Program.cs:793-825) - plot penceresinin acik kalma suresi t0/t1'i sismesin diye.
// =============================================================================
if (!IsCancellationRequested && !singleTrader.IsStopRequested && selectedRunMode != TraderRunMode.QueryOnly)
{
    Log("");

    algoTrader.RegisterLogger(LogManager.GetInstance());

    if (algoTrader.SetupPython())
        await algoTrader.PlotSingleTraderData(singleTrader);
    else
        Log("[HATA] Python setup failed. PlotSingleTraderData skipped.");

    try
    {
        var bundleConverter = new TradeDataBundleConverter();
        string bundleOutDir = Path.Combine(AppSettings.DearPyGuiDataPlotterDir, "inputs");
        var (bundlePath, viewPath) = bundleConverter.ConvertSingleTrader(singleTrader, bundleOutDir);

        // Ayni bundle'i outputs/logs'a da yaz (SingleTraderLists.csv/Statistics.txt ile ayni
        // klasor) - gorunurluk icin, "normal" (DearPyGuiDataPlotter/inputs) konumun yaninda.
        bundleConverter.ConvertSingleTrader(singleTrader, AppSettings.LogsDir, fileBaseName: "SingleTraderBundle");
        Log($"[DearPyGuiDataPlotter] Bundle ayrica {AppSettings.LogsDir}\\SingleTraderBundle.npz'e de yazildi.");

        // true: eski tip plotter gibi, pencere kapanana kadar bloklar. false: hemen doner,
        // process arka planda acik kalir (hot-reload akisi).
        bool blockDearPyGuiPlotterUntilClosed = true;

        var dearPyGuiTestPlotter = new DearPyGuiDataPlotter();
        dearPyGuiTestPlotter.SetLogger(LogManager.GetInstance());
        dearPyGuiTestPlotter.StartPlotter();
        dearPyGuiTestPlotter.LoadBundle(bundlePath, viewPath, blockDearPyGuiPlotterUntilClosed);
        Log($"[DearPyGuiDataPlotter] SingleTrader datasi yuklendi: {bundlePath}");
    }
    catch (Exception ex)
    {
        Log($"[HATA][DearPyGuiDataPlotter] Converter hatasi: {ex.Message}");
    }
}

// =============================================================================
// 12. Temizle
// =============================================================================
strategy?.Dispose();
query?.Dispose();
singleTrader?.Dispose();
stockDataReader?.Dispose();

Log("=== Bitti ===");
