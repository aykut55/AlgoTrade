// =============================================================================
// 04_GenerateDearPyGuiDataPlotterBundle.csx - latest_bundle.npz / .view.json uretir
// SingleTrader'i (Config_01_SingleTrader.csx ayarlariyla, TradeOnly modda) kendi icinde calistirir
// ve TradeDataBundleConverter ile src/DearPyGuiDataPlotter/inputs/latest_bundle.npz +
// latest_bundle.view.json dosyalarini yazar - 05_RunDearPyGuiDataPlotterTest.csx'in
// yukledigi/bekledigi dosyalar bunlar.
//
// NOT: 01_RunSingleTraderWithProgressAsync.csx'i #load ETMİYOR - o script kendi
// sonunda singleTrader.Dispose() cagiriyor (DeleteModules() -> trader.Data bosaliyor),
// TradeDataBundleConverter ise "Finalize() sonrasi cagirin, Data dolu olmali" bekliyor.
// Bu yuzden burada kendi (Dispose'suz) minimal run akisi var - #load edilen script'in
// kendi cleanup sirasina mudahale etmenin bir yolu yok.
// =============================================================================
#load "Config_01_SingleTrader.csx"

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
using AlgoTrade.Core.Python.DearPyGuiDataPlotter;

Log("=== 04_GenerateDearPyGuiDataPlotterBundle.csx ===");

// =============================================================================
// 1. Veri Oku
// =============================================================================
if (!File.Exists(stockDataFullFileName))
{
    Log($"[HATA] Dosya bulunamadi: {stockDataFullFileName}");
    return;
}

var stockDataReader = new StockDataReader();
stockDataReader.ReadMetaData(stockDataFullFileName);

if (!stockDataReader.IsMetaDataRead)
{
    Log("[HATA] MetaData okunamadi.");
    return;
}

var stockMetaData = stockDataReader.GetMetaData();
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
// 2. AlgoTrader Configure Et (sadece TradeOnly - bundle icin query gerekmiyor)
// =============================================================================
algoTrader.SetData(data);
algoTrader.SymbolName = symbolName;
algoTrader.SymbolPeriod = symbolPeriod;
algoTrader.SingleTraderRunMode = TraderRunMode.TradeOnly;
algoTrader.ConfigureStrategy(strategyName, strategyParams);
algoTrader.Initialize();

Log($"\n{algoTrader.GetDataInfo()}");

// =============================================================================
// 3. Indicators + Strategy
// =============================================================================
Log("\nCreating indicators...");
var indicators = algoTrader.CreateIndicators();

Log($"Creating strategy: {strategyName}");
var strategy = algoTrader.CreateConfiguredStrategy(indicators);

// =============================================================================
// 4. SingleTrader Olustur ve Calistir
// =============================================================================
Log("\nCreating singleTrader...");

var singleTrader = new SingleTrader(0, "singleTrader", data, indicators, null);
singleTrader.Reset();

singleTrader.SymbolName = symbolName;
singleTrader.SymbolPeriod = symbolPeriod;
singleTrader.StrategyName = strategyName;

singleTrader.initialTradeParams!.Reset()
    .SetBakiyeParams(ilkBakiye: ilkBakiye)
    .SetKontratParamsFxCrypto(lotSayisi: lotSayisi)
    .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
    .SetKaymaParams(kaymaMiktari: kaymaMiktari);

singleTrader.ConfigureUserFlagsOnce();
singleTrader.signals.AlEnabled = true;
singleTrader.signals.SatEnabled = true;
singleTrader.signals.FlatOlEnabled = true;
singleTrader.signals.PasGecEnabled = true;
singleTrader.signals.KarAlEnabled = true;
singleTrader.signals.ZararKesEnabled = true;

singleTrader.SaveStatisticsToFile = false;
singleTrader.RunMode = TraderRunMode.TradeOnly;
singleTrader.SetStrategy(strategy);

singleTrader.Init();

int totalBars = data.Count;
Log($"\nRunning singleTrader... Total bars: {totalBars}");

var sw = Stopwatch.StartNew();

singleTrader.IsStarted = true;
singleTrader.IsRunning = true;

int updateFreq = 5;
for (int i = 0; i < totalBars; i++)
{
    if (IsCancellationRequested)
    {
        Log($"Script cancelled by ESC at bar {i}/{totalBars}");
        break;
    }

    singleTrader.Run(i);

    double percentage = (i + 1) / (double)totalBars * 100.0;
    int prevPercentBucket = (int)(((i) / (double)totalBars * 100.0) / updateFreq);
    int currPercentBucket = (int)(percentage / updateFreq);
    if (currPercentBucket > prevPercentBucket || i + 1 >= totalBars)
        Log($"Progress: {i + 1}/{totalBars} ({percentage:F1}%)");
}

sw.Stop();
Log($"Run tamamlandi: {sw.ElapsedMilliseconds} msec.");

Log("\nFinalizing singleTrader...");
singleTrader.Finalize();

singleTrader.IsRunning = false;
singleTrader.IsStopped = true;

// =============================================================================
// 5. Bundle Uret (Dispose'dan ONCE - ConvertSingleTrader trader.Data'yi bekliyor)
// =============================================================================
var converter = new TradeDataBundleConverter();
string outputDir = Path.Combine(AppSettings.DearPyGuiDataPlotterDir, "inputs");

Log($"\nBundle uretiliyor: {outputDir} (fileBaseName=latest_bundle)");

var (bundlePath, viewPath) = converter.ConvertSingleTrader(singleTrader, outputDir);

Log($"Bundle yazildi : {bundlePath}");
Log($"View yazildi   : {viewPath}");
Log("Artik 05_RunDearPyGuiDataPlotterTest.csx bu bundle'i yukleyebilir.");

// =============================================================================
// 6. Temizle
// =============================================================================
strategy?.Dispose();
singleTrader?.Dispose();
stockDataReader?.Dispose();

Log("=== Bitti ===");
