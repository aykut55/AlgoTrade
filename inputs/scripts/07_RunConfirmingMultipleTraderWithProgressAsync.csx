// =============================================================================
// 07_RunConfirmingMultipleTraderWithProgressAsync.csx - ConfirmingMultipleTrader Inlined Execution
// Config_07_ConfirmingMultipleTrader.csx'den gelen konfigurasyonu kullanarak calistirir.
//
// [24]/[25] menu handler'i (Program.cs::runConfirmingMultipleTraderAlgoTrade()) ile AYNI kod
// yolunu izler: algoTrader'in Set*Config metodlariyla (AppConfigApplier.ApplyConfirmingMultipleTrader()
// ile birebir ayni alan eslemesi) konfigure edilip algoTrader.RunConfirmingMultipleTraderWithProgressAsync()
// (kendi icinde indicators/confirmingMultipleTrader/SignalChild'lari kuran, tamamen kendine yeten
// sarmalayici) cagriliyor. SignalChild'larin strateji+config yuklemesi 02 script'iyle (plain
// MultipleTrader) birebir ayni desen (SetChildTraderCount + AddStrategyConfig). Bkz.
// docs/manual/07-menu-vs-script-parity.md SS5.
// =============================================================================
#load "Config_07_ConfirmingMultipleTrader.csx"

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
Log("=== 07_RunConfirmingMultipleTraderWithProgressAsync.csx ===");

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

// ConfirmingMultipleTrader nesnesi kayit ayarlari
algoTrader.SetConfirmingMultipleTraderSaveConfig(new ConfirmingMultipleTraderObjectSaveConfig
{
    SaveStatisticsToFile                        = saveConfirmingMultipleTraderLists,
    SaveConfirmingMultipleTraderListsTxtEnabled = saveConfirmingMultipleTraderLists,
    SaveConfirmingMultipleTraderListsCsvEnabled = saveConfirmingMultipleTraderLists,
    FilePrefix                                   = filePrefix,
    WriteSignalMultipleTraderListsToFiles       = writeSignalMultipleTraderListsToFiles,
    WriteSignalChildTradersDataToFiles          = writeSignalChildTradersDataToFiles,
});

// Consensus ayarlari - MultipleTrader ile paylasilan slot
algoTrader.SetMultipleTraderConsensusConfig(new MultipleTraderConsensusConfig
{
    Mode        = consensusMode,
    MinNetCount = consensusMinNetCount,
});

// Sanal pozisyon konfirmasyon ayarlari
algoTrader.SetConfirmingMultipleTraderConfirmationConfig(new ConfirmingMultipleTraderConfirmationConfig
{
    ThresholdIsPercentage          = thresholdIsPercentage,
    ProfitThreshold                = profitThreshold,
    LossThreshold                  = lossThreshold,
    Trigger                        = confirmationTrigger,
    ConflictMode                   = conflictMode,
    FlattenImmediatelyOnFlatSignal = flattenImmediatelyOnFlatSignal,
});

// Ortak Signals bloğu (MainTrader ve tum SignalChild'lar icin - bkz. Config_07 basindaki not)
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

// TAM InitialTradeParams (MarketType dahil, MainTrader+SignalChild'lar icin ortak) -
// AppConfigApplier.ApplyConfirmingMultipleTrader() (satir 667-668) ile ayni yol.
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

// --- MainTrader (konfirme edilmis consensus sinyaliyle gercek islem yapan trader) ---
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

// MainTrader Equity Curve Filter (opsiyonel, id=0)
algoTrader.ClearEquityCurveFilterConfigs();
if (ecfEnabled)
{
    string ecfPath = Path.Combine(AppSettings.ConfigsDir, ecfConfigFile);
    algoTrader.ConfigureEquityCurveFilterFromConfig(ecfPath, ecfVersion, id: 0);
    Log($"  EquityCurveFilter: {ecfConfigFile} [{ecfVersion}]");
}

// =============================================================================
// 3. SignalChild Stratejilerini Yukle (02 script - plain MultipleTrader - ile ayni desen)
// =============================================================================
algoTrader.ClearStrategyConfigs();
foreach (var sc in strategyConfigs)
    algoTrader.AddStrategyConfig(sc.id, sc.name, sc.parameters);

// =============================================================================
// 4. SignalChild Trader Config'lerini Olustur
// =============================================================================
algoTrader.SetChildTraderCount(strategyConfigs.Count, (entry, i) =>
{
    var sc = strategyConfigs[i];
    entry.StrategyId = sc.id;
    entry.TradeParams.ApplyFrom(sharedTradeParams);
    entry.Signals = sharedSignals;

    string cp = $"{filePrefix}_SignalChild{sc.id}";
    entry.Save = new SingleTraderSaveConfig
    {
        SaveStatisticsToFile                = saveChildTraderStatistics,
        FullStatsTxtFileName                = $"{cp}_SingleTraderStatistics.txt",
        FullStatsCsvFileName                = $"{cp}_SingleTraderStatistics.csv",
        MinimalStatsTxtFileName             = $"{cp}_SingleTraderStatisticsMinimal.txt",
        MinimalStatsCsvFileName             = $"{cp}_SingleTraderStatisticsMinimal.csv",
        FullListsTxtFileName                = $"{cp}_SingleTraderLists.txt",
        FullListsCsvFileName                = $"{cp}_SingleTraderLists.csv",
        MinimalListsTxtFileName             = $"{cp}_SingleTraderListsMinimal.txt",
        MinimalListsCsvFileName             = $"{cp}_SingleTraderListsMinimal.csv",
        FullStatsTxtFormattedFileName       = $"{cp}_SingleTraderStatisticsFormatted.txt",
        MinimalStatsTxtFormattedFileName    = $"{cp}_SingleTraderStatisticsMinimalFormatted.txt",
        GridStatsTxtFileName                = $"{cp}_SingleTraderStatisticsGrid.txt",
        MinimalGridStatsTxtFileName         = $"{cp}_SingleTraderStatisticsMinimalGrid.txt",
        PerformansTxtFileName               = $"{cp}_SingleTraderPerformans.txt",
        PerformansCsvFileName               = $"{cp}_SingleTraderPerformans.csv",
    };

    if (exportEnabled)
    {
        entry.Export = new SingleTraderExportConfig
        {
            ExportEnabled    = exportEnabled,
            ExportConfigFile = exportConfigFile,
            ExportVersion    = exportVersion,
        };
    }
});

// =============================================================================
// 5. Initialize ve Run
// =============================================================================
algoTrader.Initialize();

Log($"\n{algoTrader.GetDataInfo()}");

await algoTrader.RunConfirmingMultipleTraderWithProgressAsync();

var confirmingMultipleTrader = algoTrader.ConfirmingMultipleTrader!;
var writeTask = algoTrader.WriteTraderDataToFilesAsync(confirmingMultipleTrader);

// =============================================================================
// 6. Ozet
// =============================================================================
var mainTrader = confirmingMultipleTrader.GetMainTrader();

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
// 7. Plot (pythonnet + imgui_bundle - menudeki gibi eski tip, sadece mainTrader.PlotEnabled
// kontrol ediliyor - runConfirmingMultipleTraderAlgoTrade() (Program.cs:1017-1028) ile ayni)
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
Log("[WriteTraderDataToFilesAsync] File writing confirmed complete. (mainTrader + ConfirmingMultipleTraderLists)");

// =============================================================================
// 8. Temizle
// =============================================================================
stockDataReader?.Dispose();

Log("=== Bitti ===");
