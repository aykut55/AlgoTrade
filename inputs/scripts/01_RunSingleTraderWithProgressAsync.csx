// =============================================================================
// RunSingleTraderWithProgressAsync.csx - SingleTrader Inlined Execution
// Programs.csx'den gelen konfigürasyonu kullanarak SingleTrader çalıştırır
// =============================================================================
#load "Programs.csx"

using System;
using System.Diagnostics;
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

// =============================================================================
// Degiskenler
// =============================================================================
StockDataReader? stockDataReader = null;
ConcurrentDictionary<string, string>? stockMetaData = null;
var sw = new Stopwatch();

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
stockDataReader.ReadMetaData(stockDataFullFileName);

if (!stockDataReader.IsMetaDataRead)
{
    Log("[HATA] MetaData okunamadi.");
    return;
}

stockMetaData = stockDataReader.GetMetaData();
symbolName = stockMetaData.GetValueOrDefault("GrafikSembol", "N/A");
symbolPeriod = stockMetaData.GetValueOrDefault("GrafikPeriyot", "N/A");

Log($"Sembol    : {symbolName}");
Log($"Periyot   : {symbolPeriod}");
Log($"Bar Count : {stockMetaData.GetValueOrDefault("BarCount", "N/A")}");

stockDataReader.ReadDataFast(stockDataFullFileName);
var data = stockDataReader.GetData();
Log($"Okunan    : {data.Count} bar");

if (data.Count == 0)
{
    Log("[HATA] Data bos.");
    return;
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

// =============================================================================
// 7. Run Loop
// =============================================================================
int totalBars = data.Count;

Log($"\nRunning singleTrader... Total bars: {totalBars}");

sw.Restart();

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

sw.Stop();
long runElapsed = sw.ElapsedMilliseconds;

singleTrader.LastExecutionTimeStop = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
singleTrader.LastExecutionTimeInMSec = runElapsed.ToString();

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

sw.Restart();

singleTrader.Finalize();

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

sw.Stop();
long finalizeElapsed = sw.ElapsedMilliseconds;

// =============================================================================
// 10. Query Ozeti
// =============================================================================
if (selectedRunMode == TraderRunMode.TradeAndQuery || selectedRunMode == TraderRunMode.QueryOnly)
{
    var sorguOzeti = singleTrader.SorguOzeti;
    Log($"\nQuery summary: {sorguOzeti}");
}

// =============================================================================
// 11. Sonuc
// =============================================================================
singleTrader.IsRunning = false;
singleTrader.IsStopped = true;

Log($"\nt_run      = {runElapsed} msec.");
Log($"t_finalize = {finalizeElapsed} msec.");
Log($"t_total    = {runElapsed + finalizeElapsed} msec.");

Log($"\nProcessed {totalBars} bars.");

// =============================================================================
// 12. Temizle
// =============================================================================
strategy?.Dispose();
query?.Dispose();
singleTrader?.Dispose();
stockDataReader?.Dispose();

Log("=== Bitti ===");
