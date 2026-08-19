// =============================================================================
// RunSingleTraderOptWithProgressAsync.csx - SingleTraderOptimizer Inlined Execution
// ProgramsSingleTraderOpt.csx'den gelen konfigürasyonu kullanarak optimization çalıştırır
// =============================================================================
#load "ProgramsSingleTraderOpt.csx"

using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Strategy;
using AlgoTrade.Core.Timer;

// =============================================================================
// Degiskenler
// =============================================================================
StockDataReader? stockDataReader = null;
ConcurrentDictionary<string, string>? stockMetaData = null;
var sw = new Stopwatch();

// =============================================================================
// 1. Veri Oku
// =============================================================================
Log("=== RunSingleTraderOptWithProgressAsync.csx ===");

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

// RunSingleTraderOptWithProgressAsync() AlgoTrader'in kendi ic _timer/_logger
// alanlarini kullaniyor (orn. _timer!.RestartTimer("0")) - console'daki her
// menu bunlari RegisterLogger/RegisterTimer ile dolduruyor, script'te de aynisi
// gerekiyor, yoksa _timer null kalip NullReferenceException firlatiyor.
algoTrader.RegisterLogger(LogManager.GetInstance());
algoTrader.RegisterTimer(TimeManager.GetInstance());

algoTrader.SymbolName = symbolName;
algoTrader.SymbolPeriod = symbolPeriod;

// =============================================================================
// 3. Optimization Konfigurasyonu
// =============================================================================
Log("\nConfiguring optimization...");

// Parametre range'leri
algoTrader.ClearOptimizationParameterRanges();
foreach (var range in optimizationRanges)
{
    algoTrader.AddOptimizationParameterRange(range.name, range.min, range.max, range.step);
    Log($"  Range: {range.name} [{range.min} - {range.max}] step {range.step}");
}

// Strategy factory: range params + fixed params -> strateji
algoTrader.SetOptimizationStrategyFactory((factoryData, ind, parameters) =>
{
    // Merge: range params (optimizer'dan) + fixed params (config'den)
    var merged = new Dictionary<string, object>(fixedParams, StringComparer.OrdinalIgnoreCase);
    foreach (var kvp in parameters)
    {
        merged[kvp.Key] = kvp.Value;
    }
    return algoTrader.CreateStrategyFromRegistry(factoryData, ind, optimizationStrategyName, merged);
});

// Trade params
algoTrader.SetSingleTraderOptTradeParamsConfig(new SingleTraderOptTradeParamsConfig
{
    IlkBakiye      = ilkBakiye,
    KontratSayisi  = kontratSayisi,
    KomisyonCarpan = komisyonCarpan,
    KaymaMiktari   = kaymaMiktari,
});

// Optimization range (PartialOpt)
algoTrader.SetSingleTraderOptRangeConfig(new SingleTraderOptRangeConfig
{
    OptimizationFrom = optimizationFrom,
    OptimizationTo   = optimizationTo,
});
if (optimizationFrom != -1 || optimizationTo != -1)
{
    Log($"  PartialOpt: [{optimizationFrom} - {optimizationTo}]");
}
else
{
    Log($"  FullOpt (tum kombinasyonlar)");
}

// =============================================================================
// 4. Initialize ve Run
// =============================================================================
algoTrader.Initialize();

Log($"\n{algoTrader.GetDataInfo()}");

Log("\nStarting optimization...");

sw.Restart();

await algoTrader.RunSingleTraderOptWithProgressAsync();

sw.Stop();
long totalElapsed = sw.ElapsedMilliseconds;

// =============================================================================
// 5. Sonuclar
// =============================================================================
var optimizer = algoTrader.SingleTraderOptimizer;
if (optimizer != null && optimizer.Results.Count > 0)
{
    Log($"\n=== OPTIMIZATION RESULTS ({optimizer.Results.Count} combinations) ===");

    var bestResult = optimizer.GetBestResult();
    if (bestResult != null)
    {
        Log($"\n--- BEST RESULT ---");
        foreach (var kvp in bestResult.Parameters)
            Log($"  {kvp.Key}: {kvp.Value}");
        Log($"  NetProfit      : {bestResult.NetProfit:F2}");
        Log($"  WinRate        : {bestResult.WinRate:F2}%");
        Log($"  ProfitFactor   : {bestResult.ProfitFactor:F2}");
        Log($"  ProfitFactorNet: {bestResult.ProfitFactorNet:F2}");
        Log($"  MaxDrawdown    : {bestResult.MaxDrawdown:F2}");
        Log($"  IslemSayisi    : {bestResult.Values.GetValueOrDefault("IslemSayisi", "N/A")}");
    }
}

Log($"\nt_total = {totalElapsed} msec.");
Log($"\nProcessed {data.Count} bars.");

// =============================================================================
// 6. Temizle
// =============================================================================
stockDataReader?.Dispose();

Log("=== Bitti ===");
