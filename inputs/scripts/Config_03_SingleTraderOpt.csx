// =============================================================================
// Config_03_SingleTraderOpt.csx - 03_RunSingleTraderOptWithProgressAsync.csx icin Konfigurasyon Scripti
// Parametre range'leri, strategy factory, optimization range ve diger ayarları
// burada tanimlayin
// =============================================================================
using System.Collections.Generic;
using System.Globalization;
using AlgoTrade.Core.Trading;

// =============================================================================
// Ayarlar
// =============================================================================
string stockDataFullFileName = @"C:\data\csvFiles\VIP\01\VIP-X030-T.csv";

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
// Signals - AppConfig.json'daki SingleTraderOptimizer.Signals bolumunun karsiligi
// (AppConfigApplier.ApplySingleTraderOpt() -> SetSingleTraderOptSignalsConfig ile ayni alanlar).
// Her test trader'ina (her parametre kombinasyonu) uygulanir - bunlar false/eksik kalirsa
// ConfigureUserFlagsOnce() tum sinyalleri false'a resetler ve HICBIR kombinasyon islem acmaz
// (bkz. docs/manual/07-menu-vs-script-parity.md SS3, kritik hata notu).
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
// Optimizer Log (CSV/TXT) - AppConfig.json'daki SingleTraderOptimizer.Save bolumunun karsiligi
// (AppConfigApplier.ApplySingleTraderOpt() -> SetSingleTraderOptLogConfig ile ayni alanlar).
// Kapaliysa hicbir kombinasyon dosyaya yazilmaz, sadece konsola en iyi sonuc basilir.
// =============================================================================
bool csvFileLoggingEnabled = true;
string csvFileName = "singleTraderOptLog.csv";
bool txtFileLoggingEnabled = true;
string txtFileName = "singleTraderOptLog.txt";
bool appendEnabled = true;
bool statisticsExporterConfigFileEnabled = true;
string statisticsExporterConfigFile = "StatisticsExporterConfig.json";
int fileFlushIntervalMs = -1;

// =============================================================================
// Optimizer Sort (best-to-worst siralanmis ek dosya) - SingleTraderOptimizer.Sort karsiligi
// (AppConfigApplier.ApplySingleTraderOpt() -> SetSingleTraderOptSortOutputConfig ile ayni alanlar).
// =============================================================================
string sortField = "GetiriFiyatNet";
string sortedCsvFileName = "singleTraderOptLog_sorted.csv";
string sortedTxtFileName = "singleTraderOptLog_sorted.txt";

// =============================================================================
// Optimization Parameter Ranges
// =============================================================================
var optimizationRanges = new List<(string name, double min, double max, double step)>
{
    ("period",  10, 50, 10),
    ("percent", 1.0, 3.0, 1.0)
};

// =============================================================================
// Fixed Parameters (optimize edilmeyen sabit degerler)
// =============================================================================
var fixedParams = new Dictionary<string, object>
{
    ["choice"] = 0
};

// =============================================================================
// Strategy Factory Configuration
// =============================================================================
string optimizationStrategyName = "SimpleMostStrategy";

// =============================================================================
// Optimization Range (PartialOpt)
// -1 = en bastan / en sona kadar (FullOpt)
// Ornek: from=5, to=10 -> sadece 5-10 arasi kombinasyonlari calistir
// =============================================================================
int optimizationFrom = -1;
int optimizationTo = -1;

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
