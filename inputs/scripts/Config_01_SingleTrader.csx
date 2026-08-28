// =============================================================================
// Config_01_SingleTrader.csx - 01_RunSingleTraderWithProgressAsync.csx (ve
// 04_GenerateDearPyGuiDataPlotterBundle.csx) icin Konfigurasyon Scripti
// Strateji, sorgu, ECF, trade params ve diger ayarları burada tanimlayin
// =============================================================================

// =============================================================================
// Parite Kontrol Listesi (docs/manual/07-menu-vs-script-parity.md SS1)
// AppConfigApplier.ApplySingleTrader() (AppConfigApplier.cs:32-130) hangi config
// bloklarini AppConfig.json'dan okuyup uyguluyorsa, bu dosyanin/scriptin bir karsiligi
// olmali. AppConfig.json'a yeni bir alan eklenirse veya ApplySingleTrader() degisirse,
// asagidaki liste ve karsiliklari da guncellenmeli - yoksa script sessizce menuden
// geri kalir (SS3'teki SignalsConfig hatasi tam olarak boyle olmustu).
//
//   RunMode              -> selectedRunMode
//   Strategy             -> strategyName + strategyParams
//   Query (opsiyonel)    -> queryEnabled + queryName + queryParams
//   EquityCurveFilter    -> ecfEnabled + ecfThresholdTypeIsPercent + ecfProfitThreshold +
//                           ecfLossThreshold + ecfTrigger
//   TradeParams          -> ilkBakiye + kontratSayisi + komisyonCarpan + kaymaMiktari
//   Signals              -> BURADA DEGIL - 01_RunSingleTraderWithProgressAsync.csx'teki
//                           OnApplyUserFlags(SingleTrader) local fonksiyonunda HARDCODED
//                           (AlEnabled/SatEnabled/... + StartDateTime/StopDateTime)
//   Optimization         -> BURADA DEGIL - ana scriptteki OnApplyUserFlags2() icinde
//                           hardcoded (OptimizationEnabled=false)
//   Save                 -> saveStatisticsToFile (yukarida) + ana scriptteki
//                           OnApplyUserFlags2() icindeki hardcoded dosya adlari/enable flag'leri
//   Plot                 -> KASITLI ATLANDI - script PlotEnabled'i hic kontrol etmiyor,
//                           sadece selectedRunMode != QueryOnly bakiyor (bkz.
//                           07-menu-vs-script-parity.md SS1)
//   Export               -> exportEnabled + exportConfigFile + exportVersion (asagida)
//
// Not: Signals/Optimization/Save'in cogu burada degil, ANA SCRIPT dosyasinda
// (01_RunSingleTraderWithProgressAsync.csx) hardcoded - bu Config dosyasi sadece "en sik
// degisen" ayarlari disari cikarmis. Yeni bir alan eklerken hangi dosyada oldugunu unutmayin.
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
//
// readDataChoice: 0=All, 1=LastN, 2=FirstN, 3=IndexRange,
//                 4=AfterDateTime, 5=BeforeDateTime, 6=DateTimeRange
// =============================================================================
int readDataChoice = 0;

string readDataFilterMode;
int readDataN1 = 0;
int readDataN2 = 0;
string readDataDt1 = "";
string readDataDt2 = "";

if (readDataChoice == 0)
{
    readDataFilterMode = "All";
}
else if (readDataChoice == 1)
{
    readDataFilterMode = "LastN";
    readDataN1 = 5000;
}
else if (readDataChoice == 2)
{
    readDataFilterMode = "FirstN";
    readDataN1 = 5000;
}
else if (readDataChoice == 3)
{
    readDataFilterMode = "IndexRange";
    readDataN1 = 0;
    readDataN2 = 5000;
}
else if (readDataChoice == 4)
{
    readDataFilterMode = "AfterDateTime";
    readDataDt1 = "2020.01.01 00:00:00";
}
else if (readDataChoice == 5)
{
    readDataFilterMode = "BeforeDateTime";
    readDataDt1 = "2020.01.01 00:00:00";
}
else if (readDataChoice == 6)
{
    readDataFilterMode = "DateTimeRange";
    readDataDt1 = "2020.01.01 00:00:00";
    readDataDt2 = "2024.01.01 00:00:00";
}
else
{
    throw new ArgumentOutOfRangeException(nameof(readDataChoice), $"Bilinmeyen readDataChoice: {readDataChoice}");
}

// Head/Tail log - [5] menusundeki addHeadTailInfo karsiligi (menude de varsayilan/hep kapali,
// bkz. Program.cs:52 - orada hicbir zaman true yapilmiyor). Debug icin script'te acilabilir.
bool addHeadTailInfo = false;

// =============================================================================
// Strategy Configuration

// strategyParams'daki key'ler, secilen strategyName'in constructor parametre adlarina
// birebir (case-insensitive) eslesmeli (bkz. StrategyRegistry.CreateFromBestMatchingConstructor).
// Eslesmeyen bir key (orn. baska bir stratejiye ait "ruleIndex") HATA VERMEZ, sessizce yok
// sayilir; o parametre kendi varsayilan degerine duser. Yani yanlis key yazarsan calisir ama
// sessizce yanlis calisir - once ilgili Strategy sinifinin constructor'ina bak.
// =============================================================================
int strategyChoice = 0;

string strategyName;
Dictionary<string, object> strategyParams;

if (strategyChoice == 0)
{
    strategyName = "SimpleMostStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["period"]  = 21,
        ["percent"] = 1.0,
        ["mostMaMethod"] = "EMA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (strategyChoice == 1)
{
    strategyName = "SimpleComboStrategy";
    strategyParams = new Dictionary<string, object>
    {
        ["ruleModeIndex"]   = 0,
        ["signalModeIndex"] = 0
    };
}
else
{
    throw new ArgumentOutOfRangeException(nameof(strategyChoice), $"Bilinmeyen strategyChoice: {strategyChoice}");
}

// =============================================================================
// Query Configuration

// queryParams'daki key'ler, secilen queryName'in constructor parametre adlarina birebir
// (case-insensitive) eslesmeli (bkz. QueryRegistry.CreateFromBestMatchingConstructor).
// Eslesmeyen bir key HATA VERMEZ, sessizce yok sayilir; o parametre kendi varsayilan
// degerine duser. Yani yanlis key yazarsan calisir ama sessizce yanlis calisir - once
// ilgili Query sinifinin constructor'ina bak.
// =============================================================================
int queryChoice = 0;

bool queryEnabled = true;
string queryName;
Dictionary<string, object> queryParams;

if (queryChoice == 0)
{
    queryName = "SimpleQuery1";
    queryParams = new Dictionary<string, object>
    {
        ["ma8Period"]   = 8,
        ["ma200Period"] = 200,
        ["choice"]      = 0
    };
}
else if (queryChoice == 1)
{
    queryName = "SimpleQuery1";
    queryParams = new Dictionary<string, object>
    {
        ["ma8Period"]   = 21,
        ["ma200Period"] = 200,
        ["choice"]      = 1
    };
}
else
{
    throw new ArgumentOutOfRangeException(nameof(queryChoice), $"Bilinmeyen queryChoice: {queryChoice}");
}

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
