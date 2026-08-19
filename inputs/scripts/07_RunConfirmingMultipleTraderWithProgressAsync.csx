// =============================================================================
// 07_RunConfirmingMultipleTraderWithProgressAsync.csx - ConfirmingMultipleTrader Inlined Execution
// ProgramsConfirmingMultipleTrader.csx'den gelen konfigurasyonu kullanarak
// ConfirmingMultipleTrader'i calistirir. 02_RunMultipleTraderWithProgressAsync.csx'in
// deseniyle aynidir (child olusturma burada dongu ile yapiliyor, cunku sayi degisken).
// =============================================================================
#load "ProgramsConfirmingMultipleTrader.csx"

using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;

// =============================================================================
// Degiskenler
// =============================================================================
StockDataReader? stockDataReader = null;
ConcurrentDictionary<string, string>? stockMetaData = null;
var sw = new Stopwatch();

// =============================================================================
// 1. Veri Oku
// =============================================================================
Log("=== 06_RunConfirmingMultipleTraderWithProgressAsync.csx ===");

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
// 2. AlgoTrader Configure Et
// =============================================================================
algoTrader.SetData(data);
algoTrader.SymbolName = symbolName;
algoTrader.SymbolPeriod = symbolPeriod;

algoTrader.ClearStrategyConfigs();
foreach (var sc in strategyConfigs)
    algoTrader.AddStrategyConfig(sc.id, sc.name, sc.parameters);

algoTrader.Initialize();

Log($"\n{algoTrader.GetDataInfo()}");

// =============================================================================
// 3. Indicators Olustur
// =============================================================================
Log("\nCreating indicators...");
var indicators = algoTrader.CreateIndicators();

// =============================================================================
// 4. ConfirmingMultipleTrader Olustur
// =============================================================================
Log("\nCreating confirmingMultipleTrader...");

var confirmingMultipleTrader = new ConfirmingMultipleTrader(0, data, indicators, null);
confirmingMultipleTrader.Reset();

confirmingMultipleTrader.ConsensusMode = consensusMode;
confirmingMultipleTrader.ConsensusMinNetCount = consensusMinNetCount;

confirmingMultipleTrader.ThresholdIsPercentage = thresholdIsPercentage;
confirmingMultipleTrader.ProfitThreshold = profitThreshold;
confirmingMultipleTrader.LossThreshold = lossThreshold;
confirmingMultipleTrader.Trigger = confirmationTrigger;
confirmingMultipleTrader.ConflictMode = conflictMode;
confirmingMultipleTrader.FlattenImmediatelyOnFlatSignal = flattenImmediatelyOnFlatSignal;

var mainTrader = confirmingMultipleTrader.GetMainTrader();

mainTrader.initialTradeParams!.Reset()
    .SetBakiyeParams(ilkBakiye: ilkBakiye)
    .SetKontratParamsFxCrypto(lotSayisi: lotSayisi)
    .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
    .SetKaymaParams(kaymaMiktari: kaymaMiktari);

mainTrader.SymbolName = symbolName;
mainTrader.SymbolPeriod = symbolPeriod;
mainTrader.RunMode = TraderRunMode.TradeOnly;
mainTrader.ConfigureUserFlagsOnce();
mainTrader.signals.AlEnabled = true;
mainTrader.signals.SatEnabled = true;
mainTrader.signals.FlatOlEnabled = true;
algoTrader.SetSingleTraderConfigureEquityCurveFilter(mainTrader);
mainTrader.SaveStatisticsToFile = saveMainTraderStatistics;

confirmingMultipleTrader.SaveStatisticsToFile = saveConfirmingMultipleTraderLists;

// Not: confirmingMultipleTrader.Init() cagrisi signalMain'in AlEnabled/SatEnabled/FlatOlEnabled
// bayraklarini da acar (bkz. ConfirmingMultipleTrader.Init() - bu, gercek veride bulunan bir
// hatanin duzeltmesiydi, bkz. docs/todo.md).
confirmingMultipleTrader.Init();

// =============================================================================
// 5. Child Traders Olustur
// =============================================================================
Log("\nCreating child traders...");

foreach (var sc in strategyConfigs)
{
    var childTrader = new SingleTrader(sc.id, $"childTrader_{sc.id}", data, indicators, null);

    childTrader.RunMode = TraderRunMode.TradeOnly;

    var strategy = algoTrader.GetStrategy(sc.id);
    childTrader.SetStrategy(strategy);

    childTrader.Reset();

    childTrader.SymbolName = symbolName;
    childTrader.SymbolPeriod = symbolPeriod;
    childTrader.LastExecutionTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
    childTrader.LastExecutionTimeStart = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

    childTrader.initialTradeParams!.Reset()
        .SetBakiyeParams(ilkBakiye: ilkBakiye)
        .SetKontratParamsFxCrypto(lotSayisi: lotSayisi)
        .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
        .SetKaymaParams(kaymaMiktari: kaymaMiktari);

    childTrader.ConfigureUserFlagsOnce();
    childTrader.signals.AlEnabled = true;
    childTrader.signals.SatEnabled = true;
    childTrader.signals.FlatOlEnabled = true;

    childTrader.SaveStatisticsToFile = saveChildTraderStatistics;

    childTrader.Init();

    confirmingMultipleTrader.AddTrader(childTrader);
    Log($"  childTrader_{sc.id} created (strategy={sc.id})");
}

Log($"Total child traders: {confirmingMultipleTrader.GetSignalMultipleTrader().Traders.Count}");

// =============================================================================
// 6. Run Loop
// =============================================================================
int totalBars = data.Count;

Log($"\nRunning confirmingMultipleTrader... Total bars: {totalBars}");

sw.Restart();

confirmingMultipleTrader.IsStarted = true;
confirmingMultipleTrader.IsRunning = true;
confirmingMultipleTrader.IsStopped = false;
confirmingMultipleTrader.IsStopRequested = false;

int updateFreq = 5;

for (int i = 0; i < totalBars; i++)
{
    if (IsCancellationRequested)
    {
        Log($"Script cancelled by ESC at bar {i}/{totalBars}");
        break;
    }

    if (confirmingMultipleTrader.IsStopRequested)
    {
        Log($"ConfirmingMultipleTrader stopped by user request at bar {i}/{totalBars}");
        break;
    }

    confirmingMultipleTrader.Run(i);

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
// 7. Ozet
// =============================================================================
foreach (var childTrader in confirmingMultipleTrader.GetSignalMultipleTrader().Traders)
{
    Log($"\nChild [{childTrader.GetId()}] screening: {childTrader.TaramaOzeti}");
}

Log($"\nSignalConsensus screening: {confirmingMultipleTrader.GetSignalMultipleTrader().GetMainTrader().TaramaOzeti}");
Log($"MainTrader screening     : {mainTrader.TaramaOzeti}");
Log($"IsConfirmed (final)      : {confirmingMultipleTrader.IsConfirmed}");

int virtualBuy = 0, virtualSell = 0, mainBuy = 0, mainSell = 0;
foreach (var v in confirmingMultipleTrader.VirtualSignals)
{
    if (v > 0) virtualBuy++;
    else if (v < 0) virtualSell++;
}
foreach (var v in confirmingMultipleTrader.Signals)
{
    if (v > 0) mainBuy++;
    else if (v < 0) mainSell++;
}
Log($"VirtualSignals (consensus) : Buy={virtualBuy} Sell={virtualSell} (toplam {confirmingMultipleTrader.VirtualSignals.Count} bar)");
Log($"Signals (konfirme)         : Buy={mainBuy} Sell={mainSell} (toplam {confirmingMultipleTrader.Signals.Count} bar)");

// =============================================================================
// 8. Finalize
// =============================================================================
Log("\nFinalizing confirmingMultipleTrader...");

sw.Restart();

confirmingMultipleTrader.Finalize();

if (!IsCancellationRequested && !confirmingMultipleTrader.IsStopRequested)
{
    if (mainTrader.SaveStatisticsToFile)
    {
        Log("\nSaving mainTrader statistics to files...");
        mainTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
    }

    foreach (var childTrader in confirmingMultipleTrader.GetSignalMultipleTrader().Traders)
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
confirmingMultipleTrader.IsRunning = false;
confirmingMultipleTrader.IsStopped = true;

Log($"\nt_run      = {runElapsed} msec.");
Log($"t_finalize = {finalizeElapsed} msec.");
Log($"t_total    = {runElapsed + finalizeElapsed} msec.");

Log($"\nProcessed {totalBars} bars with {confirmingMultipleTrader.GetSignalMultipleTrader().Traders.Count} child traders.");

// =============================================================================
// 10. Temizle
// =============================================================================
confirmingMultipleTrader?.Dispose();
stockDataReader?.Dispose();

Log("=== Bitti ===");
