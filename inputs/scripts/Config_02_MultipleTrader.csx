// =============================================================================
// Config_02_MultipleTrader.csx - 02_RunMultipleTraderWithProgressAsync.csx icin Konfigurasyon Scripti
// Strategy, query, ECF listelerini ve diger ayarları burada tanimlayin
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

// Head/Tail log - [6] menusundeki addHeadTailInfo karsiligi (menude de varsayilan/hep kapali,
// bkz. Program.cs:52 - orada hicbir zaman true yapilmiyor). Debug icin script'te acilabilir.
bool addHeadTailInfo = false;

// =============================================================================
// Strategy Configurations (Id bazli)
// =============================================================================
var strategyConfigs = new List<(int id, string name, Dictionary<string, object> parameters)>
{
    (0, "SimpleMostStrategy", new Dictionary<string, object>
    {
        ["period"] = 21,
        ["percent"] = 1.0,
        ["choice"] = 0
    }),
    (1, "SimpleMostStrategy", new Dictionary<string, object>
    {
        ["period"] = 14,
        ["percent"] = 0.5,
        ["choice"] = 0
    })
};

// =============================================================================
// Query Configurations (Id bazli)
// =============================================================================
var queryConfigs = new List<(int id, string name, Dictionary<string, object> parameters)>
{
    (0, "SimpleQuery1", new Dictionary<string, object>
    {
        ["ma8Period"] = 8,
        ["ma200Period"] = 200,
        ["choice"] = 0
    }),
    (1, "SimpleQuery1", new Dictionary<string, object>
    {
        ["ma8Period"] = 5,
        ["ma200Period"] = 100,
        ["choice"] = 0
    })
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
bool saveMainTraderStatistics = true;
bool saveChildTraderStatistics = true;

// AppConfigApplier.cs:156 (Save.WriteChildTradersDataToFiles) karsiligi - false ise child
// istatistik dosyalari yazilmaz (mainTrader'inki her zaman yazilir, saveMainTraderStatistics'e bagli).
bool writeChildTradersDataToFiles = true;

// FilePrefix - AppConfigApplier.cs:214-215,236-249,379,397-410 ile ayni mantik: mainTrader ve
// her childTrader'in dosya adlari bu on ekle ayristirilir ({prefix}_Main_..., {prefix}_Child{i}_...).
// Bu olmadan tum trader'lar ayni varsayilan dosya adina (orn. SingleTraderStatistics.txt) yazar
// ve birbirinin uzerine yazar - sadece son calisan trader'in ciktisi hayatta kalir.
string filePrefix = "MultipleTrader";

// =============================================================================
// Export (versiyonlu sutun tanimlariyla FullListsTxt/PerformansTxt uzerine ek yazim)
// AppConfig.json'daki MultipleTrader.MainTrader/ChildTraders[].Export bolumunun karsiligi
// (tum trader'lar icin ortak - script zaten tek bir hardcoded ayar setini paylasiyor).
// =============================================================================
bool exportEnabled = false;
string exportConfigFile = "StatisticsExporterConfig.json";
string exportVersion = "v1";
