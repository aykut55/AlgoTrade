// =============================================================================
// RunMultipleTraderWithProgressAsync.csx - MultipleTrader Inlined Execution
// ProgramsMultipleTrader.csx'den gelen konfigürasyonu kullanarak MultipleTrader çalıştırır
// =============================================================================
#load "ProgramsMultipleTrader.csx"

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
// 1. Veri Oku
// =============================================================================
Log("=== RunMultipleTraderWithProgressAsync.csx ===");

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

// Strategy listesini yükle
algoTrader.ClearStrategyConfigs();
foreach (var sc in strategyConfigs)
    algoTrader.AddStrategyConfig(sc.id, sc.name, sc.parameters);

// Query listesini yükle
algoTrader.ClearQueryConfigs();
foreach (var qc in queryConfigs)
    algoTrader.AddQueryConfig(qc.id, qc.name, qc.parameters);

// ECF
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
// 4. MultipleTrader Olustur
// =============================================================================
Log("\nCreating multipleTrader...");

var multipleTrader = new MultipleTrader(0, data, indicators, null);
multipleTrader.Reset();

var mainTrader = multipleTrader.GetMainTrader();
mainTrader.Reset();

// Configure position sizing for mainTrader
mainTrader.initialTradeParams!.Reset()
    .SetBakiyeParams(ilkBakiye: ilkBakiye)
    .SetKontratParamsViopEndex(kontratSayisi: kontratSayisi)
    .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
    .SetKaymaParams(kaymaMiktari: kaymaMiktari);

// Assign runMode
mainTrader.RunMode = selectedRunMode;

// Apply user flags
mainTrader.ConfigureUserFlagsOnce();

// Configure equity curve filter for mainTrader
algoTrader.SetSingleTraderConfigureEquityCurveFilter(mainTrader);

// Enable saving statistics
mainTrader.SaveStatisticsToFile = saveMainTraderStatistics;

mainTrader.Init();

// =============================================================================
// 5. Child Traders Olustur
// =============================================================================
Log("\nCreating child traders...");

{
    int childId = 0;

    var childTrader = new SingleTrader(childId, "childTrader_0", data, indicators, null);

    childTrader.RunMode = selectedRunMode;

    if (childTrader.RunMode == TraderRunMode.TradeOnly || childTrader.RunMode == TraderRunMode.TradeAndQuery)
    {
        var strategy = algoTrader.GetStrategy(0);
        childTrader.SetStrategy(strategy);
    }

    if (childTrader.RunMode == TraderRunMode.TradeAndQuery || childTrader.RunMode == TraderRunMode.QueryOnly)
    {
        var query = algoTrader.GetQuery(0);
        childTrader.SetQuery(query);
    }

    childTrader.Reset();

    childTrader.SymbolName = symbolName;
    childTrader.SymbolPeriod = symbolPeriod;
    childTrader.LastExecutionTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
    childTrader.LastExecutionTimeStart = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

    childTrader.initialTradeParams!.Reset()
        .SetBakiyeParams(ilkBakiye: ilkBakiye)
        .SetKontratParamsViopEndex(kontratSayisi: kontratSayisi)
        .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
        .SetKaymaParams(kaymaMiktari: kaymaMiktari);

    childTrader.ConfigureUserFlagsOnce();

    algoTrader.SetSingleTraderConfigureEquityCurveFilter(childTrader);

    childTrader.SaveStatisticsToFile = saveChildTraderStatistics;

    childTrader.Init();

    multipleTrader.AddTrader(childTrader);
    Log($"  childTrader_{childId} created (strategy=0, query=0)");
}
{
    int childId = 1;

    var childTrader = new SingleTrader(childId, "childTrader_1", data, indicators, null);

    childTrader.RunMode = selectedRunMode;

    if (childTrader.RunMode == TraderRunMode.TradeOnly || childTrader.RunMode == TraderRunMode.TradeAndQuery)
    {
        var strategy = algoTrader.GetStrategy(1);
        childTrader.SetStrategy(strategy);
    }

    if (childTrader.RunMode == TraderRunMode.TradeAndQuery || childTrader.RunMode == TraderRunMode.QueryOnly)
    {
        var query = algoTrader.GetQuery(1);
        childTrader.SetQuery(query);
    }

    childTrader.Reset();

    childTrader.SymbolName = symbolName;
    childTrader.SymbolPeriod = symbolPeriod;
    childTrader.LastExecutionTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
    childTrader.LastExecutionTimeStart = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

    childTrader.initialTradeParams!.Reset()
        .SetBakiyeParams(ilkBakiye: ilkBakiye)
        .SetKontratParamsViopEndex(kontratSayisi: kontratSayisi)
        .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
        .SetKaymaParams(kaymaMiktari: kaymaMiktari);

    childTrader.ConfigureUserFlagsOnce();

    algoTrader.SetSingleTraderConfigureEquityCurveFilter(childTrader);

    childTrader.SaveStatisticsToFile = saveChildTraderStatistics;

    childTrader.Init();

    multipleTrader.AddTrader(childTrader);
    Log($"  childTrader_{childId} created (strategy=1, query=1)");
}
{
    int childId = 2;

    var childTrader = new SingleTrader(childId, "childTrader_2", data, indicators, null);

    childTrader.RunMode = selectedRunMode;

    if (childTrader.RunMode == TraderRunMode.TradeOnly || childTrader.RunMode == TraderRunMode.TradeAndQuery)
    {
        var strategy = algoTrader.GetStrategy(1);
        childTrader.SetStrategy(strategy);
    }

    if (childTrader.RunMode == TraderRunMode.TradeAndQuery || childTrader.RunMode == TraderRunMode.QueryOnly)
    {
        var query = algoTrader.GetQuery(1);
        childTrader.SetQuery(query);
    }

    childTrader.Reset();

    childTrader.SymbolName = symbolName;
    childTrader.SymbolPeriod = symbolPeriod;
    childTrader.LastExecutionTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
    childTrader.LastExecutionTimeStart = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

    childTrader.initialTradeParams!.Reset()
        .SetBakiyeParams(ilkBakiye: ilkBakiye)
        .SetKontratParamsViopEndex(kontratSayisi: kontratSayisi)
        .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
        .SetKaymaParams(kaymaMiktari: kaymaMiktari);

    childTrader.ConfigureUserFlagsOnce();

    algoTrader.SetSingleTraderConfigureEquityCurveFilter(childTrader);

    childTrader.SaveStatisticsToFile = saveChildTraderStatistics;

    childTrader.Init();

    multipleTrader.AddTrader(childTrader);
    Log($"  childTrader_{childId} created (strategy=1, query=1)");
}

multipleTrader.Init();

Log($"Total child traders: {multipleTrader.Traders.Count}");

// =============================================================================
// 6. Run Loop
// =============================================================================
int totalBars = data.Count;

Log($"\nRunning multipleTrader... Total bars: {totalBars}");

sw.Restart();

multipleTrader.IsStarted = true;
multipleTrader.IsRunning = true;
multipleTrader.IsStopped = false;
multipleTrader.IsStopRequested = false;

int updateFreq = 5;

for (int i = 0; i < totalBars; i++)
{
    if (IsCancellationRequested)
    {
        Log($"Script cancelled by ESC at bar {i}/{totalBars}");
        break;
    }

    if (multipleTrader.IsStopRequested)
    {
        Log($"MultipleTrader stopped by user request at bar {i}/{totalBars}");
        break;
    }

    multipleTrader.Run(i);

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

// =============================================================================
// 7. Tarama Bilgileri
// =============================================================================
foreach (var childTrader in multipleTrader.Traders)
{
    var ozet = childTrader.TaramaOzeti;
    Log($"\nChild [{childTrader.GetId()}] screening: {ozet}");
}

var mainOzet = mainTrader.TaramaOzeti;
Log($"\nMainTrader screening: {mainOzet}");

// =============================================================================
// 8. Finalize
// =============================================================================
Log("\nFinalizing multipleTrader...");

sw.Restart();

multipleTrader.Finalize();

// Dosyaya yazma
if (!IsCancellationRequested && !multipleTrader.IsStopRequested)
{
    // MultipleTrader bar-by-bar lists (Yon/Seviye/Sinyal per trader)
    Log("\nSaving MultipleTraderLists to files...");
    multipleTrader.WriteMultipleTraderListsToFiles(AppSettings.LogsDir);

    if (mainTrader.SaveStatisticsToFile)
    {
        Log("\nSaving mainTrader statistics to files...");
        mainTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
    }

    foreach (var childTrader in multipleTrader.Traders)
    {
        if (childTrader.SaveStatisticsToFile)
            childTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
    }
}

sw.Stop();
long finalizeElapsed = sw.ElapsedMilliseconds;

// =============================================================================
// 9. Sonuc
// =============================================================================
multipleTrader.IsRunning = false;
multipleTrader.IsStopped = true;

Log($"\nt_run      = {runElapsed} msec.");
Log($"t_finalize = {finalizeElapsed} msec.");
Log($"t_total    = {runElapsed + finalizeElapsed} msec.");

Log($"\nProcessed {totalBars} bars with {multipleTrader.Traders.Count} child traders.");

// =============================================================================
// 10. Temizle
// =============================================================================
multipleTrader?.Dispose();
stockDataReader?.Dispose();

Log("=== Bitti ===");
