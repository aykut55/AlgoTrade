// =============================================================================
// GenerateReplaySampleBundles.csx - Offline Replay (playlist/merge) ozelligini
// gelistirmek/test etmek icin, FARKLI stratejilerle calistirilmis birden fazla
// SingleTrader run'ini, HER BIRINI AYRI bir klasore bundle olarak yazar.
// 04_GenerateDearPyGuiDataPlotterBundle.csx ile ayni desen (Python/pencere YOK,
// tamamen arka planda, SingleTrader'i dogrudan bar-loop ile calistirir) - farki:
// tek strateji degil, bir LISTE strateji uzerinde donuyor.
//
// Cikti: outputs/logs/replay_samples/<StrategyName>/bundle.npz + .view.json (her strateji
// kendi klasorunde - fileBaseName sabit "bundle" ama klasor farkli, boylece uzerine yazma/
// cakisma olmuyor). BILINCLI OLARAK "gercek"/kalici konumdan (inputs/python/offlineReplay/
// samples/, playlist.json'in isaret ettigi) AYRI tutuluyor - bu script'i tekrar tekrar
// calistirip denemek, playlist'in kullandigi kalici veriyi bozmasin diye. Kullanici burdan
// inputs/python/offlineReplay/samples/'e ELLE kopyaliyor (2026-08-26).
//
// Amac: docs/todo.md "Yeni Ozellik Fikri: Gecmis (Offline)... Hizli Sinyal Plot'u"
// > Option C > Pipeline adim 2+ (playlist/merge) icin gercek, farkli test verisi
// uretmek - elde hazir "farkli stratejilerle calistirilmis kosum sonucu" olmadigi
// icin burada kendimiz uretiyoruz.
// =============================================================================
#load "Config_01_SingleTrader.csx"

using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Python.DearPyGuiDataPlotter;

Log("=== GenerateReplaySampleBundles.csx ===");

// =============================================================================
// 1. Veri Oku (bir kere, tum stratejiler ayni veriyi kullanacak)
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

stockDataReader.ReadDataFast(stockDataFullFileName);
var data = stockDataReader.GetData();
Log($"Sembol: {symbolName}  Periyot: {symbolPeriod}  Bar: {data.Count}");

if (data.Count == 0)
{
    Log("[HATA] Data bos.");
    return;
}

// =============================================================================
// 2. Denenecek stratejiler: {strategyName, strategyParams}
// Config_01'deki strategyName/strategyParams (SimpleMostStrategy) oldugu gibi
// kullaniliyor; digerleri BOS parametreyle (StrategyRegistry kendi varsayilan
// degerleriyle "en uygun constructor"i buluyor, ozel param bilmemize gerek yok).
// =============================================================================
var strategiesToRun = new List<(string Name, Dictionary<string, object> Params)>
{
    (strategyName, strategyParams),                                  // Config_01'deki (SimpleMostStrategy)
    ("SimpleRSIStrategy", new Dictionary<string, object>()),
    ("SimpleMACDStrategy", new Dictionary<string, object>()),
    ("SimpleBollingerStrategy", new Dictionary<string, object>()),
    ("SimpleADXStrategy", new Dictionary<string, object>()),
    ("SimpleATRStrategy", new Dictionary<string, object>()),
    ("SimpleStochasticStrategy", new Dictionary<string, object>()),
    ("SimpleSuperTrendStrategy", new Dictionary<string, object>()),
    ("SimpleMAStrategy", new Dictionary<string, object>()),
    ("SimpleParabolicSARStrategy", new Dictionary<string, object>()),
};

foreach (var (stratName, stratParams) in strategiesToRun)
{
    if (IsCancellationRequested) { Log("ESC ile iptal edildi."); break; }

    Log($"\n--- Strateji: {stratName} ---");

    algoTrader.SetData(data);
    algoTrader.SymbolName = symbolName;
    algoTrader.SymbolPeriod = symbolPeriod;
    algoTrader.SingleTraderRunMode = TraderRunMode.TradeOnly;
    algoTrader.ConfigureStrategy(stratName, stratParams);
    algoTrader.Initialize();

    var indicators = algoTrader.CreateIndicators();
    IStrategy strategy;
    try
    {
        strategy = algoTrader.CreateConfiguredStrategy(indicators);
    }
    catch (Exception ex)
    {
        Log($"[HATA] '{stratName}' olusturulamadi, atlaniyor: {ex.Message}");
        continue;
    }

    var singleTrader = new SingleTrader(0, "singleTrader", data, indicators, null);
    singleTrader.Reset();
    singleTrader.SymbolName = symbolName;
    singleTrader.SymbolPeriod = symbolPeriod;
    singleTrader.StrategyName = stratName;

    singleTrader.initialTradeParams!.Reset()
        .SetBakiyeParams(ilkBakiye: ilkBakiye)
        .SetKontratParamsViopEndex(kontratSayisi: kontratSayisi)
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

    var sw = Stopwatch.StartNew();
    singleTrader.IsStarted = true;
    singleTrader.IsRunning = true;

    int totalBars = data.Count;
    for (int i = 0; i < totalBars; i++)
    {
        if (IsCancellationRequested) break;
        singleTrader.Run(i);
    }

    sw.Stop();
    singleTrader.Finalize();
    singleTrader.IsRunning = false;
    singleTrader.IsStopped = true;

    Log($"Run tamamlandi: {sw.ElapsedMilliseconds} msec. IslemSayisi bilgisi icin Lists/Statistics'e bakin.");

    // =============================================================================
    // 3. Bundle'i strateji-ozel klasore yaz (Dispose'dan ONCE)
    // =============================================================================
    var converter = new TradeDataBundleConverter();
    string outputDir = Path.Combine(AppSettings.LogsDir, "replay_samples", stratName);
    var (bundlePath, viewPath) = converter.ConvertSingleTrader(singleTrader, outputDir, fileBaseName: "bundle");
    Log($"Bundle yazildi: {bundlePath}");

    strategy?.Dispose();
    singleTrader?.Dispose();
}

stockDataReader?.Dispose();
Log("\n=== Bitti - outputs/logs/replay_samples/<StrategyName>/bundle.npz dosyalarina bakin. ===");
Log("Playlist icin kullanacaksaniz inputs/python/offlineReplay/samples/'e ELLE kopyalayin.");
