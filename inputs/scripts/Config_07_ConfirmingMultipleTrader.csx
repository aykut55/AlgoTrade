// =============================================================================
// Config_07_ConfirmingMultipleTrader.csx - 07_RunConfirmingMultipleTraderWithProgressAsync.csx
// icin Konfigurasyon Scripti. Child stratejiler, consensus, confirmation ve trade params
// ayarlari burada tanimlayin.
// =============================================================================

// =============================================================================
// Parite Kontrol Listesi (docs/manual/07-menu-vs-script-parity.md SS5)
// AppConfigApplier.ApplyConfirmingMultipleTrader() (AppConfigApplier.cs:622-867) hangi config
// bloklarini AppConfig.json'dan okuyup uyguluyorsa, bu dosyanin/scriptin bir karsiligi olmali.
//
//   RunMode                 -> TradeOnly sabit
//   Save (ConfirmingMultipleTrader objesi) -> saveConfirmingMultipleTraderLists,
//                              writeSignalMultipleTraderListsToFiles,
//                              writeSignalChildTradersDataToFiles (asagida). Not: child'larin
//                              istatistikleri YAZILSIN isteniyorsa HER IKI flag de true olmali
//                              (AlgoTrader.cs:2705-2728 - ic ice iki kapi).
//   FilePrefix               -> filePrefix (asagida) - MainTrader -> {prefix}_Main_{file},
//                              her SignalChild -> {prefix}_SignalChild{i}_{file}
//   Consensus                -> consensusMode + consensusMinNetCount
//   Confirmation             -> thresholdIsPercentage/profitThreshold/lossThreshold/
//                              confirmationTrigger/conflictMode/flattenImmediatelyOnFlatSignal
//   MainTrader.TradeParams   -> marketType/ilkBakiye/kontratSayisi/lotSayisi/hisseSayisi/
//                              komisyonCarpan/kaymaMiktari/pyramidingEnabled (MainTrader VE
//                              tum SignalChild'lar icin ORTAK - eski scriptte de ortakti)
//   MainTrader.Signals       -> ORTAK "userFlags" (asagida) - SignalChild'lar ile PAYLASILIYOR,
//                              AppConfig.json'da child basina farkli olabilir ama Config_02'deki
//                              gibi kasitli sadelestirme.
//   MainTrader.Save          -> saveMainTraderStatistics
//   MainTrader.Plot          -> mainPlotEnabled
//   MainTrader.Export        -> exportEnabled/exportConfigFile/exportVersion (tum trader'lar
//                              icin ORTAK)
//   MainTrader.EquityCurveFilter (opsiyonel) -> ecfEnabled/ecfConfigFile/ecfVersion
//   ChildTraders[].Strategy  -> strategyConfigs (id bazli)
//   ChildTraders[].Signals   -> ayni ortak "userFlags"
//   ChildTraders[].Save      -> saveChildTraderStatistics + filePrefix ile {prefix}_SignalChild{i}_
//   ChildTraders[].Export    -> ortak exportEnabled/exportConfigFile/exportVersion
//   ChildTraders[].EquityCurveFilter -> KAPSANMIYOR (script'te per-child ECF yok, sadece MainTrader)
// =============================================================================

using System.Collections.Generic;
using AlgoTrade.Core.Trading;

// =============================================================================
// Ayarlar
// =============================================================================
string stockDataFullFileName = @"C:\data\csvFiles\CRP\05\BTCUSDT_BNC.csv";

// =============================================================================
// Veri Filtreleme (ReadData) - AppConfig.json'daki ReadData bolumunun karsiligi
// =============================================================================
string readDataFilterMode = "All";
int readDataN1 = 0;
int readDataN2 = 0;
string readDataDt1 = "";
string readDataDt2 = "";

// =============================================================================
// FilePrefix - AppConfigApplier.cs:686,706-719,823,841-854 ile ayni mantik.
// =============================================================================
string filePrefix = "ConfirmingMultipleTrader";

// =============================================================================
// Child Strategy Configurations (consensus'u ureten stratejiler)
// =============================================================================
var strategyConfigs = new List<(int id, string name, Dictionary<string, object> parameters)>
{
    (0, "SimpleMostStrategy", new Dictionary<string, object>
    {
        ["period"] = 21,
        ["percent"] = 1.0,
        ["mostMaMethod"] = "EMA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    }),
    (1, "SimpleMAStrategy", new Dictionary<string, object>
    {
        ["fastPeriod"] = 10,
        ["slowPeriod"] = 20,
        ["fastMaMethod"] = "EMA",
        ["slowMaMethod"] = "EMA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    }),
    (2, "SimpleRSIStrategy", new Dictionary<string, object>
    {
        ["period"] = 14,
        ["oversold"] = 30,
        ["overbought"] = 70,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    })
};

// =============================================================================
// Consensus Ayarlari - bkz. MultipleTrader.BuildConsensusSignal()
// =============================================================================
string consensusMode = "Net";
int consensusMinNetCount = 1;

// =============================================================================
// Sanal Pozisyon Konfirmasyon Ayarlari - Trigger: "ProfitOnly"|"LossOnly"|"Both",
// ConflictMode: "CancelAndRestart"|"LockAndIgnore"
// =============================================================================
bool thresholdIsPercentage = false;
double profitThreshold = 5000.0;
double lossThreshold = -3000.0;
string confirmationTrigger = "Both";
string conflictMode = "CancelAndRestart";
bool flattenImmediatelyOnFlatSignal = true;

// =============================================================================
// Signals - MainTrader VE tum SignalChild'lar icin ORTAK (bkz. yukaridaki parite listesi notu)
// =============================================================================
bool alEnabled = true;
bool satEnabled = true;
bool flatOlEnabled = true;
bool pasGecEnabled = true;
bool karAlEnabled = true;
bool zararKesEnabled = true;
bool gunSonuPozKapatEnabled = false;
bool timeFilteringEnabled = false;
string signalsStartDateTime = "2025.05.25 09:35:00";
string signalsStopDateTime = "2025.06.02 17:55:00";
bool tradeStartBarIndexEnabled = false;
int tradeStartBarIndex = 0;

// =============================================================================
// Trade Params (MainTrader ve tum SignalChild'lar ayni parametreleri kullanir)
// =============================================================================
string marketType = "FxCrypto";
double ilkBakiye = 100000.0;
int kontratSayisi = 1;
double lotSayisi = 0.01;
double hisseSayisi = 1000.0;
double komisyonCarpan = 0.0;
double kaymaMiktari = 0.0;
bool pyramidingEnabled = false;

// =============================================================================
// Equity Curve Filter (opsiyonel, sadece MainTrader icin)
// =============================================================================
bool ecfEnabled = false;
string ecfConfigFile = "EquityCurveFilterConfig.txt";
string ecfVersion = "v1";

// =============================================================================
// Symbol Info
// =============================================================================
string symbolName = "...";
string symbolPeriod = "...";

// =============================================================================
// Save Statistics
// =============================================================================
bool saveMainTraderStatistics = true;
bool saveChildTraderStatistics = false;
bool saveConfirmingMultipleTraderLists = true;

// writeSignalMultipleTraderListsToFiles/writeSignalChildTradersDataToFiles -
// AlgoTrader.cs:2705,2721 (WriteTraderDataToFilesAsync) ic ice iki kapi: SignalChild
// istatistiklerinin gercekten dosyaya yazilmasi icin HER IKISI de true olmali (saveChildTraderStatistics
// yukarida her child'in kendi SaveStatisticsToFile'ini kontrol eder, bunlar ise ust katmanin
// "yaz/yazma" anahtaridir).
bool writeSignalMultipleTraderListsToFiles = true;
bool writeSignalChildTradersDataToFiles = true;

// =============================================================================
// Plot (pythonnet/imgui_bundle - menudeki gibi eski tip)
// =============================================================================
bool mainPlotEnabled = false;

// =============================================================================
// Export (tum trader'lar icin ORTAK)
// =============================================================================
bool exportEnabled = false;
string exportConfigFile = "StatisticsExporterConfig.json";
string exportVersion = "v1";
