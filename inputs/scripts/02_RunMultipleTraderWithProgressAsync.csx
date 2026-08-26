// =============================================================================
// RunMultipleTraderWithProgressAsync.csx - MultipleTrader Inlined Execution
// Config_02_MultipleTrader.csx'den gelen konfigürasyonu kullanarak MultipleTrader çalıştırır
// =============================================================================
#load "Config_02_MultipleTrader.csx"

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

// KRITIK: ConfigureUserFlagsOnce() TUM sinyal flag'lerini false'a resetler (SingleTrader.cs:2508-2542)
// ve bir daha true yapilmazsa MapStrategyCommandsToTradeCommands() (SingleTrader.cs:769-786) hicbir
// Al/Sat sinyalini isleme almaz - trader hicbir zaman pozisyon acmaz. mainTrader ve her childTrader
// icin bu cagridan HEMEN SONRA cagirilmali (01 script'teki OnApplyUserFlags'in ayni karsiligi).
void ApplyUserFlags(SingleTrader trader)
{
    trader.signals.AlEnabled                   = true;
    trader.signals.SatEnabled                  = true;
    trader.signals.FlatOlEnabled               = true;
    trader.signals.PasGecEnabled               = true;
    trader.signals.KarAlEnabled                = true;
    trader.signals.ZararKesEnabled             = true;
    trader.signals.GunSonuPozKapatEnabled      = false;
    trader.signals.TimeFilteringEnabled        = false;
    trader.signals.EquityCurveFilteringEnabled = false;

    var dateTimes           = new string[] { "2025.05.25 09:35:00", "2025.06.02 17:55:00" };
    trader.StartDateTimeStr = dateTimes[0];
    trader.StopDateTimeStr  = dateTimes[1];

    var startDt          = DateTime.ParseExact(dateTimes[0], "yyyy.MM.dd HH:mm:ss", null);
    trader.StartDateStr  = startDt.ToString("yyyy.MM.dd");
    trader.StartTimeStr  = startDt.ToString("HH:mm:ss");

    var stopDt           = DateTime.ParseExact(dateTimes[1], "yyyy.MM.dd HH:mm:ss", null);
    trader.StopDateStr   = stopDt.ToString("yyyy.MM.dd");
    trader.StopTimeStr   = stopDt.ToString("HH:mm:ss");
}

void ApplyFileNamesAndExport(SingleTrader trader, string cp)
{
    trader.FullStatsTxtFileName             = $"{cp}_SingleTraderStatistics.txt";
    trader.FullStatsCsvFileName             = $"{cp}_SingleTraderStatistics.csv";
    trader.MinimalStatsTxtFileName          = $"{cp}_SingleTraderStatisticsMinimal.txt";
    trader.MinimalStatsCsvFileName          = $"{cp}_SingleTraderStatisticsMinimal.csv";
    trader.FullListsTxtFileName             = $"{cp}_SingleTraderLists.txt";
    trader.FullListsCsvFileName             = $"{cp}_SingleTraderLists.csv";
    trader.MinimalListsTxtFileName          = $"{cp}_SingleTraderListsMinimal.txt";
    trader.MinimalListsCsvFileName          = $"{cp}_SingleTraderListsMinimal.csv";
    trader.FullStatsTxtFormattedFileName    = $"{cp}_SingleTraderStatisticsFormatted.txt";
    trader.MinimalStatsTxtFormattedFileName = $"{cp}_SingleTraderStatisticsMinimalFormatted.txt";
    trader.GridStatsTxtFileName             = $"{cp}_SingleTraderStatisticsGrid.txt";
    trader.MinimalGridStatsTxtFileName      = $"{cp}_SingleTraderStatisticsMinimalGrid.txt";
    trader.PerformansTxtFileName            = $"{cp}_SingleTraderPerformans.txt";
    trader.PerformansCsvFileName            = $"{cp}_SingleTraderPerformans.csv";

    trader.ExportEnabled    = exportEnabled;
    trader.ExportConfigFile = exportConfigFile;
    trader.ExportVersion    = exportVersion;
}

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

// Program.cs:64-92 (OnReadMetaData/OnProgress) ile ayni icerik/tetikleme noktasi -
// [6] menusundeki karsiliginin birebir aynisi.
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

// Program.cs:680-686 (addHeadTailInfo) ile ayni - [6]'da da varsayilan/hep kapali (bkz. Config_02_MultipleTrader.csx).
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

// AlgoTrade.cs:1899 RestartTimer("0") ile ayni baslangic noktasi (indicators olusturmadan hemen once).
timeManager.RestartTimer("0");

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
multipleTrader.WriteChildTradersDataToFiles = writeChildTradersDataToFiles;

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
ApplyUserFlags(mainTrader);

// Configure equity curve filter for mainTrader
algoTrader.SetSingleTraderConfigureEquityCurveFilter(mainTrader);

// Enable saving statistics
mainTrader.SaveStatisticsToFile = saveMainTraderStatistics;
ApplyFileNamesAndExport(mainTrader, $"{filePrefix}_Main");

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
    ApplyUserFlags(childTrader);

    algoTrader.SetSingleTraderConfigureEquityCurveFilter(childTrader);

    childTrader.SaveStatisticsToFile = saveChildTraderStatistics;
    ApplyFileNamesAndExport(childTrader, $"{filePrefix}_Child{childId}");

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
    ApplyUserFlags(childTrader);

    algoTrader.SetSingleTraderConfigureEquityCurveFilter(childTrader);

    childTrader.SaveStatisticsToFile = saveChildTraderStatistics;
    ApplyFileNamesAndExport(childTrader, $"{filePrefix}_Child{childId}");

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
    ApplyUserFlags(childTrader);

    algoTrader.SetSingleTraderConfigureEquityCurveFilter(childTrader);

    childTrader.SaveStatisticsToFile = saveChildTraderStatistics;
    ApplyFileNamesAndExport(childTrader, $"{filePrefix}_Child{childId}");

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

// AlgoTrade.cs:2038-2039 RestartTimer("1")+RestartTimer("2") ile ayni nokta (Init sonrasi, run loop'tan once).
timeManager.RestartTimer("1");
timeManager.RestartTimer("2");

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

timeManager.StopTimer("2");

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

// AlgoTrade.cs:2096/2136 RestartTimer("3")/StopTimer("3") ile ayni sinir - sadece Finalize() maliyeti.
timeManager.RestartTimer("3");
multipleTrader.Finalize();
timeManager.StopTimer("3");

// Dosyaya yazma
if (!IsCancellationRequested && !multipleTrader.IsStopRequested)
{
    // MultipleTrader bar-by-bar lists (Yon/Seviye/Sinyal per trader)
    Log("\nSaving MultipleTraderLists to files...");
    multipleTrader.WriteMultipleTraderListsToFiles(AppSettings.LogsDir);

    // mainTrader + childTrader'lari yan yana karsilastiran tek dosya (grid) -
    // AlgoTrade.cs:1598 WriteMultipleTraderStatistics ile ayni.
    Log("\nSaving MultipleTraderStatistics (grid) to files...");
    multipleTrader.WriteMultipleTraderStatistics(AppSettings.LogsDir);

    if (mainTrader.SaveStatisticsToFile)
    {
        Log("\nSaving mainTrader statistics to files...");
        mainTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
    }

    // AlgoTrade.cs:1611 (trader.WriteChildTradersDataToFiles) ile ayni gate.
    if (writeChildTradersDataToFiles)
    {
        foreach (var childTrader in multipleTrader.Traders)
        {
            if (childTrader.SaveStatisticsToFile)
                childTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
        }
    }
}

// AlgoTrade.cs:2138-2139 StopTimer("1")/StopTimer("0") ile ayni nokta (dosya yazimindan sonra,
// plot'tan once - menude de t0/t1 olcumu plot'u kapsamiyor, bkz. asagidaki "8b. Plot" notu).
timeManager.StopTimer("1");
timeManager.StopTimer("0");

// =============================================================================
// 9. Sonuc
// =============================================================================
multipleTrader.IsRunning = false;
multipleTrader.IsStopped = true;

var t0 = timeManager.GetElapsedTime("0");
var t1 = timeManager.GetElapsedTime("1");
var t2 = timeManager.GetElapsedTime("2");
var t3 = timeManager.GetElapsedTime("3");

Log($"\nt0 = {t0} msec. <==> RunMultipleTraderWithProgressAsync elapsed time");
Log($"\nt1 = {t1} msec. <==> Running + Finalizing multipleTrader elapsed time");
Log($"\nt2 = {t2} msec. <==> Running multipleTrader elapsed time");
Log($"\nt3 = {t3} msec. <==> Finalizing multipleTrader elapsed time");

Log($"\nProcessed {totalBars} bars with {multipleTrader.Traders.Count} child traders.");

// =============================================================================
// 8b. Plot (pythonnet + DearPyGuiDataPlotter)
// t0-t3 olcumunden SONRA calisiyor - menude de Plot, RunMultipleTraderWithProgressAsync()
// donduktan (yani t0-t3 hesaplandiktan) SONRA, runMultipleTraderAlgoTrade() icinde tetikleniyor
// (Program.cs:872-904) - plot penceresinin acik kalma suresi t0/t1'i sismesin diye.
// =============================================================================
if (!IsCancellationRequested && !multipleTrader.IsStopRequested && selectedRunMode != TraderRunMode.QueryOnly)
{
    Log("");

    algoTrader.RegisterLogger(LogManager.GetInstance());

    if (algoTrader.SetupPython())
        await algoTrader.PlotMultipleTraderData(multipleTrader);
    else
        Log("[HATA] Python setup failed. PlotMultipleTraderData skipped.");

    try
    {
        var bundleConverter = new TradeDataBundleConverter();
        string bundleOutDir = Path.Combine(AppSettings.DearPyGuiDataPlotterDir, "inputs");
        var (bundlePath, viewPath) = bundleConverter.ConvertMultipleTrader(multipleTrader, bundleOutDir);

        // Ayni bundle'i outputs/logs'a da yaz (MultipleTraderLists.csv/Statistics.txt ile ayni
        // klasor) - gorunurluk icin, "normal" (DearPyGuiDataPlotter/inputs) konumun yaninda.
        bundleConverter.ConvertMultipleTrader(multipleTrader, AppSettings.LogsDir, fileBaseName: "MultipleTraderBundle");
        Log($"[DearPyGuiDataPlotter] Bundle ayrica {AppSettings.LogsDir}\\MultipleTraderBundle.npz'e de yazildi.");

        // Her plotter'in kendi AlgoTrade-native runtime klasoru (2026-08-26, ayri fiziksel
        // kopyalar - bkz. docs/todo.md "Kalinti cift ROOT yapisi").
        bundleConverter.ConvertMultipleTrader(multipleTrader, AppSettings.DearPyGuiPlotterBundleDir, fileBaseName: "latest_bundle");
        bundleConverter.ConvertMultipleTrader(multipleTrader, AppSettings.PythonPlotterBundleDir, fileBaseName: "latest_bundle");
        Log($"[DearPyGuiDataPlotter] Bundle ayrica {AppSettings.DearPyGuiPlotterBundleDir} ve {AppSettings.PythonPlotterBundleDir}'e de yazildi.");

        // true: eski tip plotter gibi, pencere kapanana kadar bloklar. false: hemen doner,
        // process arka planda acik kalir (hot-reload akisi).
        bool blockDearPyGuiPlotterUntilClosed = true;

        var dearPyGuiTestPlotter = new DearPyGuiDataPlotter();
        dearPyGuiTestPlotter.SetLogger(LogManager.GetInstance());
        dearPyGuiTestPlotter.StartPlotter();
        dearPyGuiTestPlotter.LoadBundle(bundlePath, viewPath, blockDearPyGuiPlotterUntilClosed);
        Log($"[DearPyGuiDataPlotter] MultipleTrader datasi yuklendi: {bundlePath}");
    }
    catch (Exception ex)
    {
        Log($"[HATA][DearPyGuiDataPlotter] Converter hatasi: {ex.Message}");
    }
}

// =============================================================================
// 10. Temizle
// =============================================================================
multipleTrader?.Dispose();
stockDataReader?.Dispose();

Log("=== Bitti ===");
