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

// Reset
singleTrader.Reset();

// Set attributes
singleTrader.SymbolName = symbolName;
singleTrader.SymbolPeriod = symbolPeriod;
singleTrader.LastExecutionTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
singleTrader.LastExecutionTimeStart = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

// Configure position sizing
singleTrader.initialTradeParams!.Reset()
    .SetBakiyeParams(ilkBakiye: ilkBakiye)
    .SetKontratParamsViopEndex(kontratSayisi: kontratSayisi)
    .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
    .SetKaymaParams(kaymaMiktari: kaymaMiktari);

// Apply user flags
singleTrader.ConfigureUserFlagsOnce();

// Configure equity curve filter
singleTrader.signals.EquityCurveFilteringEnabled = ecfEnabled;
singleTrader.ConfigureEquityCurveFilter(
    isPercent: ecfThresholdTypeIsPercent,
    profitThreshold: ecfProfitThreshold,
    lossThreshold: ecfLossThreshold,
    trigger: ecfTrigger
);

// Enable saving statistics
singleTrader.SaveStatisticsToFile = saveStatisticsToFile;

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

for (int i = 0; i < totalBars; i++)
{
    if (singleTrader.IsStopRequested)
    {
        Log($"SingleTrader stopped by user request at bar {i}/{totalBars}");
        break;
    }

    singleTrader.Run(i);
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

if (!singleTrader.IsStopRequested && singleTrader.SaveStatisticsToFile)
{
    Log("\nSaving statistics to files...");
    singleTrader.WriteStatisticsToFile(AppSettings.LogsDir);
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
