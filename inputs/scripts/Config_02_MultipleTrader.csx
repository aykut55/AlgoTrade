// =============================================================================
// Config_02_MultipleTrader.csx - 02_RunMultipleTraderWithProgressAsync.csx icin Konfigurasyon Scripti
// Strategy, query, ECF listelerini ve diger ayarları burada tanimlayin
// =============================================================================

// =============================================================================
// Parite Kontrol Listesi (docs/manual/07-menu-vs-script-parity.md SS2)
// AppConfigApplier.ApplyMultipleTrader() (AppConfigApplier.cs:138-416) hangi config
// bloklarini AppConfig.json'dan okuyup uyguluyorsa, bu dosyanin/scriptin bir karsiligi
// olmali. AppConfig.json'a yeni bir alan eklenirse veya ApplyMultipleTrader() degisirse,
// asagidaki liste ve karsiliklari da guncellenmeli - yoksa script sessizce menuden
// geri kalir (bkz. SS2'deki iki 🔴 kritik hata: ApplyUserFlags eksikligi ve dosya adi
// cakismasi - ikisi de tam olarak bu sekilde olustu).
//
//   RunMode                          -> selectedRunMode
//   MultipleTrader (obje) Save        -> saveMainTraderStatistics + saveChildTraderStatistics +
//                                        writeChildTradersDataToFiles (asagida)
//   Consensus (Mode/MinNetCount)      -> KAPSANMIYOR - script'te hic set edilmiyor, MultipleTrader.
//                                        ConsensusMode kendi sinif-varsayilanini ("Net") kullanir.
//                                        AppConfig.json'da "Net" disinda bir Mode/MinNetCount
//                                        secilmisse script bunu YANSITMAZ.
//   MainTrader.TradeParams            -> ilkBakiye + lotSayisi + komisyonCarpan + kaymaMiktari
//   MainTrader.Signals                -> BURADA DEGIL - 02_RunMultipleTraderWithProgressAsync.csx'teki
//                                        ApplyUserFlags(SingleTrader) local fonksiyonunda HARDCODED
//                                        (mainTrader ve her childTrader icin AYNI degerler kullanilir -
//                                        AppConfig.json'da child'lara farkli Signals verilebilir, script
//                                        bunu ayirt etmiyor, bilerek sadelestirilmis)
//   MainTrader.Optimization           -> BURADA DEGIL - script'te hep false (OptimizationEnabled
//                                        set edilmiyor, sinif-varsayilani kullaniliyor)
//   MainTrader.Plot                   -> KASITLI ATLANDI - SS1'deki gibi, script PlotEnabled'i
//                                        kontrol etmiyor
//   MainTrader.Export                 -> exportEnabled + exportConfigFile + exportVersion (tum
//                                        trader'lar icin ORTAK - AppConfig.json'da per-trader farkli
//                                        Export ayari olabilir, script tek ortak sete sadelestirmis)
//   ChildTraders[].Strategy/Query/ECF -> strategyConfigs + queryConfigs (id bazli) + ecf* (ortak,
//                                        tum child'lar icin tek ECF seti)
//   ChildTraders[].Signals            -> ayni ApplyUserFlags() - mainTrader ile ayni sekilde ortak
//   ChildTraders[].Save (FilePrefix)  -> filePrefix + ApplyFileNamesAndExport(trader, cp) local
//                                        fonksiyonu (ana scriptte)
//   ChildTraders[].Export             -> ayni ortak exportEnabled/exportConfigFile/exportVersion
//
// Not: Signals/Optimization/Plot'un cogu burada degil, ANA SCRIPT dosyasinda
// (02_RunMultipleTraderWithProgressAsync.csx, ApplyUserFlags/ApplyFileNamesAndExport local
// fonksiyonlari) hardcoded. Ayrica script, AppConfig.json'un per-child farkli Signals/Export
// destekleyebilmesini KASITLI olarak tek ortak sete sadelestirmis (tum child'lar ayni degerleri
// kullanir) - cok-farkli-child senaryosu test edilecekse bu bilerek atlanan bir esneklik.
// =============================================================================
using System.Collections.Generic;
using AlgoTrade.Core.Trading;

// =============================================================================
// Ayarlar
// =============================================================================
string stockDataFullFileName = @"C:\data\csvFiles\CRP\05\BTCUSDT_BNC.csv";
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
    }),
    (3, "SimpleOTTStrategy", new Dictionary<string, object>
    {
        ["period"] = 2,
        ["percent"] = 1.4,
        ["ottMaMethod"] = "VIDYA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    }),
    (4, "SimpleSuperTrendStrategy", new Dictionary<string, object>
    {
        ["period"] = 10,
        ["multiplier"] = 3.0,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    }),
    (5, "SimpleParabolicSARStrategy", new Dictionary<string, object>
    {
        ["step"] = 0.02,
        ["max"] = 0.2,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    }),
    (6, "SimpleADXStrategy", new Dictionary<string, object>
    {
        ["period"] = 14,
        ["adxThreshold"] = 25,
        ["signalModeIndex"] = 0
    }),
    (7, "SimpleDIStrategy", new Dictionary<string, object>
    {
        ["period"] = 14,
        ["signalModeIndex"] = 0
    }),
    (8, "SimpleMACDStrategy", new Dictionary<string, object>
    {
        ["fastPeriod"] = 12,
        ["slowPeriod"] = 26,
        ["signalPeriod"] = 9,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    }),
    (9, "SimpleStochasticStrategy", new Dictionary<string, object>
    {
        ["kPeriod"] = 14,
        ["dPeriod"] = 3,
        ["centerLine"] = 50,
        ["signalModeIndex"] = 0
    }),
    (10, "SimpleBollingerStrategy", new Dictionary<string, object>
    {
        ["period"] = 20,
        ["multiplier"] = 2.0,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    }),
    (11, "SimpleATRStrategy", new Dictionary<string, object>
    {
        ["atrPeriod"] = 14,
        ["maPeriod"] = 20,
        ["multiplier"] = 2.0,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    }),
    (12, "SimpleCMFStrategy", new Dictionary<string, object>
    {
        ["period"] = 20,
        ["positiveThreshold"] = 0.1,
        ["negativeThreshold"] = -0.1,
        ["signalModeIndex"] = 0
    }),
    (13, "SimpleMFIStrategy", new Dictionary<string, object>
    {
        ["period"] = 14,
        ["oversold"] = 20,
        ["overbought"] = 80,
        ["signalModeIndex"] = 0
    }),
    (14, "SimpleKairiStrategy", new Dictionary<string, object>
    {
        ["period"] = 20,
        ["positiveThreshold"] = 5,
        ["negativeThreshold"] = -5,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    }),
    (15, "SimpleMomentumStrategy", new Dictionary<string, object>
    {
        ["period"] = 12,
        ["positiveThreshold"] = 0,
        ["negativeThreshold"] = 0,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    }),
    (16, "SimpleHHVLLVStrategy", new Dictionary<string, object>
    {
        ["period"] = 20,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    }),
    (17, "SimpleHYLYStrategy", new Dictionary<string, object>
    {
        ["period"] = 20,
        ["threshold"] = 80,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    }),
    (18, "SimpleIchimokuStrategy", new Dictionary<string, object>
    {
        ["tenkanPeriod"] = 9,
        ["kijunPeriod"] = 26,
        ["senkouPeriod"] = 52,
        ["signalModeIndex"] = 0
    }),
    (19, "SimpleMavilimWStrategy", new Dictionary<string, object>
    {
        ["param1"] = 3,
        ["param2"] = 5,
        ["signalModeIndex"] = 0
    }),
    (20, "SimplePMaxStrategy", new Dictionary<string, object>
    {
        ["atrPeriod"] = 10,
        ["multiplier"] = 3.0,
        ["maPeriod"] = 10,
        ["pmaxMaMethod"] = "EMA",
        ["signalModeIndex"] = 0
    }),
    (21, "SimpleTillsonT3Strategy", new Dictionary<string, object>
    {
        ["period"] = 5,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    }),
    (22, "SimpleAlphaTrendStrategy", new Dictionary<string, object>
    {
        ["atrPeriod"] = 14,
        ["coefficient"] = 1.0,
        ["momentumPeriod"] = 14,
        ["useMFI"] = true,
        ["signalModeIndex"] = 0
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
double lotSayisi = 0.01;
double komisyonCarpan = 0.0;
double kaymaMiktari = 0.0;

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
