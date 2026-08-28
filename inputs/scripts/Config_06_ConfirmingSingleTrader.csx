// =============================================================================
// Config_06_ConfirmingSingleTrader.csx - 06_RunConfirmingSingleTraderWithProgressAsync.csx icin
// Konfigurasyon Scripti. Strategy, confirmation, trade params ve diger ayarlari burada tanimlayin.
// =============================================================================

// =============================================================================
// Parite Kontrol Listesi (docs/manual/07-menu-vs-script-parity.md SS4)
// AppConfigApplier.ApplyConfirmingSingleTrader() (AppConfigApplier.cs:433-614) hangi config
// bloklarini AppConfig.json'dan okuyup uyguluyorsa, bu dosyanin/scriptin bir karsiligi olmali.
//
//   RunMode                    -> TradeOnly sabit (menude de simdilik tek desteklenen)
//   Save (ConfirmingSingleTrader objesi) -> saveConfirmingSingleTraderLists (asagida)
//   Confirmation                -> thresholdIsPercentage/profitThreshold/lossThreshold/
//                                confirmationTrigger/conflictMode/flattenImmediatelyOnFlatSignal
//   FilePrefix                  -> filePrefix (asagida) - SignalTrader -> {prefix}_Signal_{file},
//                                MainTrader -> {prefix}_Main_{file}
//   SignalTrader.Strategy       -> strategyName + strategyParameters
//   SignalTrader.Signals        -> ORTAK "userFlags" bloguyla mainTrader ile PAYLASILIYOR (bkz.
//                                asagidaki not) - AppConfig.json'da SignalTrader/MainTrader
//                                Signals'i AYRI ayarlanabilir, script bunu KASITLI sadelestirmis.
//   SignalTrader.Save           -> saveSignalTraderStatistics (asagida, varsayilan false - eski
//                                script davranisiyla ayni, ama artik config'ten acilabiliyor)
//   SignalTrader.Plot           -> signalPlotEnabled (asagida, varsayilan false)
//   SignalTrader.Export         -> exportEnabled/exportConfigFile/exportVersion (main ile ORTAK,
//                                Config_02'deki gibi kasitli sadelestirme)
//   MainTrader.TradeParams      -> marketType/ilkBakiye/kontratSayisi/lotSayisi/hisseSayisi/
//                                komisyonCarpan/kaymaMiktari/pyramidingEnabled (mainTrader VE
//                                signalTrader icin ORTAK - eski scriptte de ortaktı)
//   MainTrader.Signals          -> ayni ortak "userFlags" (SignalTrader ile ayni)
//   MainTrader.Save             -> saveMainTraderStatistics (asagida)
//   MainTrader.Plot             -> mainPlotEnabled (asagida)
//   MainTrader.Export           -> main ile ortak exportEnabled/exportConfigFile/exportVersion
//   MainTrader.EquityCurveFilter (opsiyonel) -> ecfEnabled/ecfConfigFile/ecfVersion (asagida)
//
// Not: Signals icin SignalTrader/MainTrader ayrimi AppConfig.json'da mumkun ama bu script
// KASITLI olarak TEK ortak "userFlags" seti kullaniyor (eski scriptin zaten yaptigi gibi -
// ikisi de ayni AL/SAT/FlatOl degerlerini paylasiyordu). Farkli Signals gerekiyorsa bu dosyayi
// genisletip 06_RunConfirmingSingleTraderWithProgressAsync.csx'teki iki
// SetConfirmingSignalTraderSignalsConfig/SetSingleTraderSignalsConfig cagrisini ayirin.
// =============================================================================

using System.Collections.Generic;
using AlgoTrade.Core.Trading;

// =============================================================================
// Ayarlar
// =============================================================================
string stockDataFullFileName = @"C:\data\csvFiles\CRP\05\BTCUSDT_BNC.csv";

// =============================================================================
// Veri Filtreleme (ReadData) - AppConfig.json'daki ReadData bolumunun karsiligi
// FilterMode: All, LastN, FirstN, IndexRange, AfterDateTime, BeforeDateTime, DateTimeRange
// Dt1/Dt2 formati: "yyyy.MM.dd HH:mm:ss" (bos string = kullanilmiyor)
// =============================================================================
string readDataFilterMode = "All";
int readDataN1 = 0;
int readDataN2 = 0;
string readDataDt1 = "";
string readDataDt2 = "";

// =============================================================================
// FilePrefix - AppConfigApplier.cs:460,503-517,576-589 ile ayni mantik: SignalTrader ve
// MainTrader'in dosya adlari bu on ekle ayristirilir ({prefix}_Signal_..., {prefix}_Main_...).
// Bu olmadan ikisi de ayni varsayilan dosya adina (SingleTraderStatistics.txt) yazar ve
// birbirinin uzerine yazar.
// =============================================================================
string filePrefix = "ConfirmingSingleTrader";

// =============================================================================
// SignalTrader Stratejisi (ham Al/Sat/Flat sinyalini uretir)
// =============================================================================
int strategyChoice = 0; // 0=SimpleMostStrategy, 1=SimpleMAStrategy

string strategyName;
Dictionary<string, object> strategyParameters;

if (strategyChoice == 0)
{
    strategyName = "SimpleMostStrategy";
    strategyParameters = new Dictionary<string, object>
    {
        ["period"] = 21,
        ["percent"] = 1.0,
        ["mostMaMethod"] = "EMA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 1)
{
    strategyName = "SimpleMAStrategy";
    strategyParameters = new Dictionary<string, object>
    {
        ["fastPeriod"] = 10,
        ["slowPeriod"] = 20,
        ["fastMaMethod"] = "EMA",
        ["slowMaMethod"] = "EMA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else
{
    throw new ArgumentOutOfRangeException(nameof(strategyChoice), $"Bilinmeyen strategyChoice: {strategyChoice}");
}

// =============================================================================
// Sanal Pozisyon Konfirmasyon Ayarlari - bkz. docs/todo.md, "Getiri Egrisi /
// KarZarar Egrisi Konfirmasyonu (Madde 3)"
// Trigger: "ProfitOnly" | "LossOnly" | "Both"
// ConflictMode: "CancelAndRestart" | "LockAndIgnore"
// =============================================================================
bool thresholdIsPercentage = false;
double profitThreshold = 5000.0;
double lossThreshold = -3000.0;
string confirmationTrigger = "Both";
string conflictMode = "CancelAndRestart";
bool flattenImmediatelyOnFlatSignal = true;

// =============================================================================
// Signals - mainTrader VE signalTrader icin ORTAK (bkz. yukaridaki parite listesi notu).
// Bunlar false/eksik kalirsa ConfigureUserFlagsOnce() sinyalleri resetler ve trader'lar hicbir
// islem acmaz.
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
// Trade Params (mainTrader ve signalTrader ayni parametreleri kullanir)
// Gecerli MarketType degerleri: BistEndex, BistHisse, BistParite, BistMetal,
// ViopEndex, ViopHisse, ViopParite, ViopMetal, FxEndex, FxHisse, FxParite, FxMetal,
// FxCrypto, Crypto.
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
// Equity Curve Filter (opsiyonel, sadece MainTrader icin - AppConfigApplier.cs:607-613 ile ayni)
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
bool saveSignalTraderStatistics = false;
bool saveConfirmingSingleTraderLists = true;

// =============================================================================
// Plot (pythonnet/imgui_bundle - menudeki gibi eski tip; yeni tip DearPyGuiDataPlotter
// Confirming* icin henuz menude de yok, bkz. docs/yapilacak.md)
// =============================================================================
bool mainPlotEnabled = false;
bool signalPlotEnabled = false;

// =============================================================================
// Export (versiyonlu sutun tanimlariyla FullListsTxt/PerformansTxt uzerine ek yazim) -
// mainTrader VE signalTrader icin ORTAK (Config_02'deki gibi kasitli sadelestirme).
// =============================================================================
bool exportEnabled = false;
string exportConfigFile = "StatisticsExporterConfig.json";
string exportVersion = "v1";
