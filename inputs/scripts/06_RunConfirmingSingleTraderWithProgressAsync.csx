// =============================================================================
// 06_RunConfirmingSingleTraderWithProgressAsync.csx - ConfirmingSingleTrader Inlined Execution
// ProgramsConfirmingSingleTrader.csx'den gelen konfigurasyonu kullanarak
// ConfirmingSingleTrader'i calistirir. 02_RunMultipleTraderWithProgressAsync.csx'in
// deseniyle aynidir.
// =============================================================================
#load "ProgramsConfirmingSingleTrader.csx"

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
Log("=== 05_RunConfirmingSingleTraderWithProgressAsync.csx ===");

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
// 2. AlgoTrader Configure Et (strateji kaydi icin GetStrategy kullaniliyor)
// =============================================================================
algoTrader.SetData(data);
algoTrader.SymbolName = symbolName;
algoTrader.SymbolPeriod = symbolPeriod;

algoTrader.ClearStrategyConfigs();
algoTrader.AddStrategyConfig(0, strategyName, strategyParameters);

algoTrader.Initialize();

Log($"\n{algoTrader.GetDataInfo()}");

// =============================================================================
// 3. Indicators Olustur
// =============================================================================
Log("\nCreating indicators...");
var indicators = algoTrader.CreateIndicators();

// =============================================================================
// 4. ConfirmingSingleTrader Olustur
// =============================================================================
Log("\nCreating confirmingSingleTrader...");

var confirmingSingleTrader = new ConfirmingSingleTrader(0, data, indicators, null);
confirmingSingleTrader.Reset();

// Confirmation ayarlari
confirmingSingleTrader.ThresholdIsPercentage = thresholdIsPercentage;
confirmingSingleTrader.ProfitThreshold = profitThreshold;
confirmingSingleTrader.LossThreshold = lossThreshold;
confirmingSingleTrader.Trigger = confirmationTrigger;
confirmingSingleTrader.ConflictMode = conflictMode;
confirmingSingleTrader.FlattenImmediatelyOnFlatSignal = flattenImmediatelyOnFlatSignal;

var mainTrader = confirmingSingleTrader.GetMainTrader();
var signalTrader = confirmingSingleTrader.GetSignalTrader();

// TradeParams - mainTrader ve signalTrader ayni parametreleri kullanir
mainTrader.initialTradeParams!.Reset()
    .SetBakiyeParams(ilkBakiye: ilkBakiye)
    .SetKontratParamsFxCrypto(lotSayisi: lotSayisi)
    .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
    .SetKaymaParams(kaymaMiktari: kaymaMiktari);
signalTrader.initialTradeParams!.Reset()
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

signalTrader.SymbolName = symbolName;
signalTrader.SymbolPeriod = symbolPeriod;
signalTrader.RunMode = TraderRunMode.TradeOnly;
signalTrader.ConfigureUserFlagsOnce();
signalTrader.signals.AlEnabled = true;
signalTrader.signals.SatEnabled = true;
signalTrader.signals.FlatOlEnabled = true;
signalTrader.SaveStatisticsToFile = false;

var strategy = algoTrader.GetStrategy(0);
confirmingSingleTrader.SetStrategy(strategy);

confirmingSingleTrader.SaveStatisticsToFile = saveConfirmingSingleTraderLists;

confirmingSingleTrader.Init();

Log("confirmingSingleTrader hazir.");

// =============================================================================
// 5. Run Loop
// =============================================================================
int totalBars = data.Count;

Log($"\nRunning confirmingSingleTrader... Total bars: {totalBars}");

sw.Restart();

confirmingSingleTrader.IsStarted = true;
confirmingSingleTrader.IsRunning = true;
confirmingSingleTrader.IsStopped = false;
confirmingSingleTrader.IsStopRequested = false;

int updateFreq = 5;

for (int i = 0; i < totalBars; i++)
{
    if (IsCancellationRequested)
    {
        Log($"Script cancelled by ESC at bar {i}/{totalBars}");
        break;
    }

    if (confirmingSingleTrader.IsStopRequested)
    {
        Log($"ConfirmingSingleTrader stopped by user request at bar {i}/{totalBars}");
        break;
    }

    confirmingSingleTrader.Run(i);

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
// 6. Ozet
// =============================================================================
Log($"\nSignalTrader screening: {signalTrader.TaramaOzeti}");
Log($"MainTrader screening  : {mainTrader.TaramaOzeti}");
Log($"IsConfirmed (final)   : {confirmingSingleTrader.IsConfirmed}");

int virtualBuy = 0, virtualSell = 0, mainBuy = 0, mainSell = 0;
foreach (var v in confirmingSingleTrader.VirtualSignals)
{
    if (v > 0) virtualBuy++;
    else if (v < 0) virtualSell++;
}
foreach (var v in confirmingSingleTrader.Signals)
{
    if (v > 0) mainBuy++;
    else if (v < 0) mainSell++;
}
Log($"VirtualSignals (ham)  : Buy={virtualBuy} Sell={virtualSell} (toplam {confirmingSingleTrader.VirtualSignals.Count} bar)");
Log($"Signals (konfirme)    : Buy={mainBuy} Sell={mainSell} (toplam {confirmingSingleTrader.Signals.Count} bar)");

// =============================================================================
// 7. Finalize
// =============================================================================
Log("\nFinalizing confirmingSingleTrader...");

sw.Restart();

confirmingSingleTrader.Finalize();

if (!IsCancellationRequested && !confirmingSingleTrader.IsStopRequested)
{
    if (mainTrader.SaveStatisticsToFile)
    {
        Log("\nSaving mainTrader statistics to files...");
        mainTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
    }
}

sw.Stop();
long finalizeElapsed = sw.ElapsedMilliseconds;

// =============================================================================
// 8. Sonuc
// =============================================================================
confirmingSingleTrader.IsRunning = false;
confirmingSingleTrader.IsStopped = true;

Log($"\nt_run      = {runElapsed} msec.");
Log($"t_finalize = {finalizeElapsed} msec.");
Log($"t_total    = {runElapsed + finalizeElapsed} msec.");

Log($"\nProcessed {totalBars} bars.");

// =============================================================================
// 9. Temizle
// =============================================================================
confirmingSingleTrader?.Dispose();
stockDataReader?.Dispose();

Log("=== Bitti ===");
