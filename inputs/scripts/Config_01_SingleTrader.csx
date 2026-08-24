// =============================================================================
// Config_01_SingleTrader.csx - 01_RunSingleTraderWithProgressAsync.csx (ve
// 04_GenerateDearPyGuiDataPlotterBundle.csx) icin Konfigurasyon Scripti
// Strateji, sorgu, ECF, trade params ve diger ayarları burada tanimlayin
// =============================================================================
using System.Collections.Generic;
using AlgoTrade.Core.Trading;

// =============================================================================
// Ayarlar
// =============================================================================
string stockDataFullFileName = @"C:\data\csvFiles\VIP\01\VIP-X030-T.csv";
TraderRunMode selectedRunMode = TraderRunMode.TradeAndQuery;

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

// Head/Tail log - [5] menusundeki addHeadTailInfo karsiligi (menude de varsayilan/hep kapali,
// bkz. Program.cs:52 - orada hicbir zaman true yapilmiyor). Debug icin script'te acilabilir.
bool addHeadTailInfo = false;

// =============================================================================
// Strategy Configuration
// =============================================================================
string strategyName = "SimpleMostStrategy";
var strategyParams = new Dictionary<string, object>
{
    ["period"]  = 21,
    ["percent"] = 1.0,
    ["choice"]  = 0
};

// =============================================================================
// Query Configuration
// =============================================================================
bool queryEnabled = true;
string queryName = "SimpleQuery1";
var queryParams = new Dictionary<string, object>
{
    ["ma8Period"]   = 8,
    ["ma200Period"] = 200,
    ["choice"]      = 0
};

// =============================================================================
// Equity Curve Filter Configuration
// =============================================================================
bool ecfEnabled = false;
bool ecfThresholdTypeIsPercent = true;
double ecfProfitThreshold = 0.05;
double ecfLossThreshold = -0.05;
ConfirmationTrigger ecfTrigger = ConfirmationTrigger.Both;

// =============================================================================
// Trade Params
// =============================================================================
double ilkBakiye = 100000.0;
int kontratSayisi = 1;
double komisyonCarpan = 20.0;
double kaymaMiktari = 0.5;

// =============================================================================
// Symbol Info
// =============================================================================
string symbolName = "...";
string symbolPeriod = "...";

// =============================================================================
// Save Statistics
// =============================================================================
bool saveStatisticsToFile = true;

// =============================================================================
// Export (versiyonlu sutun tanimlariyla FullListsTxt/PerformansTxt uzerine ek yazim)
// AppConfig.json'daki SingleTrader.Export bolumunun karsiligi (SingleTrader.cs:2662-2675).
// exportEnabled=false ise devre disi (varsayilan, AppConfig'de de Export bolumu yoksa boyle).
// =============================================================================
bool exportEnabled = false;
string exportConfigFile = "StatisticsExporterConfig.json";
string exportVersion = "v1";
