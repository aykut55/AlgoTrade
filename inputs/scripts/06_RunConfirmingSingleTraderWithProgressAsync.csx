// =============================================================================
// 06_RunConfirmingSingleTraderWithProgressAsync.csx - ConfirmingSingleTrader Inlined Execution
// Config_06_ConfirmingSingleTrader.csx'den gelen konfigurasyonu kullanarak calistirir.
//
// [22]/[23] menu handler'i (Program.cs::runConfirmingSingleTraderAlgoTrade()) ile AYNI kod
// yolunu izler: algoTrader'in Set*Config metodlariyla (AppConfigApplier.ApplyConfirmingSingleTrader()
// ile birebir ayni alan eslemesi) konfigure edilip algoTrader.RunConfirmingSingleTraderWithProgressAsync()
// (kendi icinde indicators/strategy/confirmingSingleTrader'i kuran, tamamen kendine yetien
// sarmalayici) cagriliyor - 01/02 scriptlerinin aksine elle bar-loop yazilmiyor (03 script ile
// ayni desen). Bkz. docs/manual/07-menu-vs-script-parity.md SS4.
// =============================================================================
#load "Config_06_ConfirmingSingleTrader.csx"

using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AlgoTrade.Core;
using AlgoTrade.Core.AppConfig;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Python;
using AlgoTrade.Core.Timer;

// =============================================================================
// Degiskenler
// =============================================================================
StockDataReader? stockDataReader = null;
ConcurrentDictionary<string, string>? stockMetaData = null;
var timeManager = TimeManager.GetInstance();

// =============================================================================
// 1. Veri Oku
// =============================================================================
Log("=== 06_RunConfirmingSingleTraderWithProgressAsync.csx ===");

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

// =============================================================================
// 2. AlgoTrader Konfigure Et
// =============================================================================
algoTrader.SetData(data);
algoTrader.SymbolName = symbolName;
algoTrader.SymbolPeriod = symbolPeriod;

// SignalTrader stratejisi - RunConfirmingSingleTraderWithProgressAsync() kendi icinde
// _currentStrategyName/_currentStrategyParams'tan strateji kuruyor (AppConfigApplier.
// ApplyConfirmingSingleTrader()'daki ConfigureStrategyFromConfig ile ayni alanlari besliyor,
// sadece dosya yerine dogrudan parametre dict'i kullaniliyor - 01/02/03 scriptleriyle tutarli).
algoTrader.ConfigureStrategy(strategyName, strategyParameters);

// ConfirmingSingleTrader nesnesi kayit ayarlari
algoTrader.SetConfirmingSingleTraderSaveConfig(new ConfirmingSingleTraderObjectSaveConfig
{
    SaveStatisticsToFile                      = saveConfirmingSingleTraderLists,
    SaveConfirmingSingleTraderListsTxtEnabled = saveConfirmingSingleTraderLists,
    SaveConfirmingSingleTraderListsCsvEnabled = saveConfirmingSingleTraderLists,
});

// Sanal pozisyon konfirmasyon ayarlari
algoTrader.SetConfirmingSingleTraderConfirmationConfig(new ConfirmingSingleTraderConfirmationConfig
{
    ThresholdIsPercentage          = thresholdIsPercentage,
    ProfitThreshold                = profitThreshold,
    LossThreshold                  = lossThreshold,
    Trigger                        = confirmationTrigger,
    ConflictMode                   = conflictMode,
    FlattenImmediatelyOnFlatSignal = flattenImmediatelyOnFlatSignal,
});

// Ortak Signals bloğu (mainTrader ve signalTrader icin - bkz. Config_06 basindaki not)
var sharedSignals = new SingleTraderSignalsConfig
{
    AlEnabled                 = alEnabled,
    SatEnabled                = satEnabled,
    FlatOlEnabled              = flatOlEnabled,
    PasGecEnabled              = pasGecEnabled,
    KarAlEnabled               = karAlEnabled,
    ZararKesEnabled            = zararKesEnabled,
    GunSonuPozKapatEnabled     = gunSonuPozKapatEnabled,
    TimeFilteringEnabled       = timeFilteringEnabled,
    StartDateTime              = signalsStartDateTime,
    StopDateTime               = signalsStopDateTime,
    TradeStartBarIndexEnabled  = tradeStartBarIndexEnabled,
    TradeStartBarIndex         = tradeStartBarIndex,
};

// TAM InitialTradeParams (MarketType dahil, mainTrader+signalTrader icin ortak) -
// AppConfigApplier.ApplyConfirmingSingleTrader() (satir 539-540) ile ayni yol.
var sharedTradeParams = AppConfigApplier.BuildInitialTradeParams(new TradeParamsConfig
{
    MarketType        = marketType,
    IlkBakiye         = ilkBakiye,
    KontratSayisi     = kontratSayisi,
    LotSayisi         = lotSayisi,
    HisseSayisi       = hisseSayisi,
    KomisyonCarpan    = komisyonCarpan,
    KaymaMiktari      = kaymaMiktari,
    PyramidingEnabled = pyramidingEnabled,
});
algoTrader.SetSingleTraderTradeParams(sharedTradeParams);

// --- SignalTrader (ham sinyal ureten strateji) ---
algoTrader.SetConfirmingSignalTraderSignalsConfig(sharedSignals);

algoTrader.SetConfirmingSignalTraderSaveConfig(new SingleTraderSaveConfig
{
    SaveStatisticsToFile                = saveSignalTraderStatistics,
    FullStatsTxtFileName                = $"{filePrefix}_Signal_SingleTraderStatistics.txt",
    FullStatsCsvFileName                = $"{filePrefix}_Signal_SingleTraderStatistics.csv",
    MinimalStatsTxtFileName             = $"{filePrefix}_Signal_SingleTraderStatisticsMinimal.txt",
    MinimalStatsCsvFileName             = $"{filePrefix}_Signal_SingleTraderStatisticsMinimal.csv",
    FullListsTxtFileName                = $"{filePrefix}_Signal_SingleTraderLists.txt",
    FullListsCsvFileName                = $"{filePrefix}_Signal_SingleTraderLists.csv",
    MinimalListsTxtFileName             = $"{filePrefix}_Signal_SingleTraderListsMinimal.txt",
    MinimalListsCsvFileName             = $"{filePrefix}_Signal_SingleTraderListsMinimal.csv",
    FullStatsTxtFormattedFileName       = $"{filePrefix}_Signal_SingleTraderStatisticsFormatted.txt",
    MinimalStatsTxtFormattedFileName    = $"{filePrefix}_Signal_SingleTraderStatisticsMinimalFormatted.txt",
    GridStatsTxtFileName                = $"{filePrefix}_Signal_SingleTraderStatisticsGrid.txt",
    MinimalGridStatsTxtFileName         = $"{filePrefix}_Signal_SingleTraderStatisticsMinimalGrid.txt",
    PerformansTxtFileName               = $"{filePrefix}_Signal_SingleTraderPerformans.txt",
    PerformansCsvFileName               = $"{filePrefix}_Signal_SingleTraderPerformans.csv",
});

algoTrader.SetConfirmingSignalTraderPlotConfig(new SingleTraderPlotConfig { PlotEnabled = signalPlotEnabled });

if (exportEnabled)
{
    algoTrader.SetConfirmingSignalTraderExportConfig(new SingleTraderExportConfig
    {
        ExportEnabled    = exportEnabled,
        ExportConfigFile = exportConfigFile,
        ExportVersion    = exportVersion,
    });
}

// --- MainTrader (konfirme edilmis sinyalle gercek islem yapan trader) ---
algoTrader.SetSingleTraderSignalsConfig(sharedSignals);

algoTrader.SetSingleTraderSaveConfig(new SingleTraderSaveConfig
{
    SaveStatisticsToFile                = saveMainTraderStatistics,
    FullStatsTxtFileName                = $"{filePrefix}_Main_SingleTraderStatistics.txt",
    FullStatsCsvFileName                = $"{filePrefix}_Main_SingleTraderStatistics.csv",
    MinimalStatsTxtFileName             = $"{filePrefix}_Main_SingleTraderStatisticsMinimal.txt",
    MinimalStatsCsvFileName             = $"{filePrefix}_Main_SingleTraderStatisticsMinimal.csv",
    FullListsTxtFileName                = $"{filePrefix}_Main_SingleTraderLists.txt",
    FullListsCsvFileName                = $"{filePrefix}_Main_SingleTraderLists.csv",
    MinimalListsTxtFileName             = $"{filePrefix}_Main_SingleTraderListsMinimal.txt",
    MinimalListsCsvFileName             = $"{filePrefix}_Main_SingleTraderListsMinimal.csv",
    FullStatsTxtFormattedFileName       = $"{filePrefix}_Main_SingleTraderStatisticsFormatted.txt",
    MinimalStatsTxtFormattedFileName    = $"{filePrefix}_Main_SingleTraderStatisticsMinimalFormatted.txt",
    GridStatsTxtFileName                = $"{filePrefix}_Main_SingleTraderStatisticsGrid.txt",
    MinimalGridStatsTxtFileName         = $"{filePrefix}_Main_SingleTraderStatisticsMinimalGrid.txt",
    PerformansTxtFileName               = $"{filePrefix}_Main_SingleTraderPerformans.txt",
    PerformansCsvFileName               = $"{filePrefix}_Main_SingleTraderPerformans.csv",
});

algoTrader.SetSingleTraderPlotConfig(new SingleTraderPlotConfig { PlotEnabled = mainPlotEnabled });

if (exportEnabled)
{
    algoTrader.SetSingleTraderExportConfig(new SingleTraderExportConfig
    {
        ExportEnabled    = exportEnabled,
        ExportConfigFile = exportConfigFile,
        ExportVersion    = exportVersion,
    });
}

// MainTrader Equity Curve Filter (opsiyonel) - AppConfigApplier.cs:607-613 ile ayni.
algoTrader.ClearEquityCurveFilterConfigs();
if (ecfEnabled)
{
    string ecfPath = Path.Combine(AppSettings.ConfigsDir, ecfConfigFile);
    algoTrader.ConfigureEquityCurveFilterFromConfig(ecfPath, ecfVersion, id: 0);
    Log($"  EquityCurveFilter: {ecfConfigFile} [{ecfVersion}]");
}

// =============================================================================
// 3. Initialize ve Run
// =============================================================================
algoTrader.Initialize();

Log($"\n{algoTrader.GetDataInfo()}");

await algoTrader.RunConfirmingSingleTraderWithProgressAsync();

var confirmingSingleTrader = algoTrader.ConfirmingSingleTrader!;
var writeTask = algoTrader.WriteTraderDataToFilesAsync(confirmingSingleTrader);

// =============================================================================
// 4. Ozet
// =============================================================================
var mainTrader = confirmingSingleTrader.GetMainTrader();
var signalTrader = confirmingSingleTrader.GetSignalTrader();

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
// 5. Plot (pythonnet + imgui_bundle - menudeki gibi eski tip, sadece mainTrader.PlotEnabled
// kontrol ediliyor - runConfirmingSingleTraderAlgoTrade() (Program.cs:956-967) ile ayni)
// =============================================================================
if (mainTrader.PlotEnabled)
{
    Log("");
    Log("[Plot] mainTrader'in gercek/konfirme edilmis sinyalleri ciziliyor.");

    if (algoTrader.SetupPython())
        await algoTrader.PlotSingleTraderData(mainTrader);
    else
        Log("[HATA] Python setup failed. PlotSingleTraderData skipped.");
}

await writeTask;
Log("[WriteTraderDataToFilesAsync] File writing confirmed complete. (mainTrader + signalTrader + ConfirmingSingleTraderLists)");

// =============================================================================
// 6. Temizle
// =============================================================================
stockDataReader?.Dispose();

Log("=== Bitti ===");
