// =============================================================================
// Config_03_SingleTraderOpt.csx - 03_RunSingleTraderOptWithProgressAsync.csx icin Konfigurasyon Scripti
// Parametre range'leri, strategy factory, optimization range ve diger ayarları
// burada tanimlayin
// =============================================================================

// =============================================================================
// Parite Kontrol Listesi (docs/manual/07-menu-vs-script-parity.md SS3)
// AppConfigApplier.ApplySingleTraderOpt() (AppConfigApplier.cs:872-998) hangi config
// bloklarini AppConfig.json'dan okuyup uyguluyorsa, bu dosyanin/scriptin bir karsiligi
// olmali. AppConfig.json'a yeni bir alan eklenirse veya ApplySingleTraderOpt() degisirse,
// asagidaki liste ve karsiliklari da guncellenmeli - SignalsConfig eksikligi (2026-08-25'te
// duzeltildi) tam olarak boyle bir kacaktan kaynaklanmisti.
//
//   Strategy                 -> optimizationStrategyName (+ fixedParams, ana scriptteki
//                              SetOptimizationStrategyFactory icinde range params ile merge edilir)
//   Optimization (range)     -> optimizationRanges (script kendi range'lerini dogrudan tanimliyor,
//                              AppConfig.json'daki gibi isimli bir "profil" dosyasindan yuklemiyor)
//   Range (PartialOpt)       -> optimizationFrom + optimizationTo
//   TradeParams (TAM, MarketType dahil) -> marketType/ilkBakiye/kontratSayisi/lotSayisi/
//                              hisseSayisi/komisyonCarpan/kaymaMiktari/pyramidingEnabled
//                              (yukarida) - 2026-08-25'te eklendi. Once BURADA DEGILDI:
//                              SetSingleTraderTradeParams() hic cagrilmiyordu, bu yuzden
//                              SingleTraderOptimizer.TradeParamsOverride null kaliyor ve
//                              SingleTraderOptimizer.cs:236'daki "ViopEndex fallback"
//                              (SetKontratParamsViopEndex) devreye giriyordu - MarketType
//                              AppConfig.json'daki degerden BAGIMSIZ hep ViopEndex-tarzi
//                              hesaplaniyordu. Artik ana scriptte
//                              AppConfigApplier.BuildInitialTradeParams(new TradeParamsConfig
//                              {...}) ile TAM InitialTradeParams olusturulup
//                              SetSingleTraderTradeParams()'a veriliyor - menude
//                              ApplySingleTraderOpt() (AppConfigApplier.cs:890) ile ayni yol.
//   EquityCurveFilter (opsiyonel) -> ecfEnabled/ecfConfigFile/ecfVersion (yukarida) - 2026-08-25'te
//                              eklendi (once BURADA DEGILDI, script hic ECF ayari yapmiyordu).
//   Signals                  -> alEnabled/satEnabled/.../tradeStartBarIndex (yukarida) -
//                              2026-08-25'te eklendi (once BURADA DEGILDI, kritik hataydi)
//   Save (Log: Csv/Txt)      -> csvFileLoggingEnabled/.../fileFlushIntervalMs (yukarida) -
//                              2026-08-25'te eklendi
//   Sort                     -> sortField/sortedCsvFileName/sortedTxtFileName (yukarida) -
//                              2026-08-25'te eklendi
//   SingleTrader.Plot/Optimization/Save/Export ("Best trader" bloklari) -> KAPSANMIYOR, ama
//                              docs/manual/07-menu-vs-script-parity.md SS3'e gore menu tarafinda
//                              da fiilen olu kod olabilir (SingleTraderOptimizer bu config'leri
//                              hic okumuyor) - script'in atlamasi bir eksiklik degil.
//
// Veri okuma filtreleme (ReadData: FilterMode/N1/N2/Dt1/Dt2) yukarida ayrica var - bu
// AppConfigApplier.ApplySingleTraderOpt()'un DEGIL, menudeki readStockData()'nin (Program.cs)
// karsiligi, SS1'deki gibi.
// =============================================================================
using System.Collections.Generic;
using System.Globalization;
using AlgoTrade.Core.Trading;

// =============================================================================
// Ayarlar
// =============================================================================
string stockDataFullFileName = @"C:\data\csvFiles\VIP\05\VIP-X030-T.csv";

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
// Optimization Strategy Configuration
// optChoice ile hangi strateji + hangi parametre range'lerinin taranacagi secilir - her deneme
// icin bu blok degistirilir, boylece Optimization Ranges / Fixed Params / Strategy Name uctan uca
// birlikte kalir (Config_01_SingleTrader.csx'teki strategyChoice ile ayni desen).
//
// optimizationRanges/fixedParams'daki key'ler, secilen optimizationStrategyName'in constructor
// parametre adlarina birebir (case-insensitive) eslesmeli (bkz. StrategyRegistry.
// CreateFromBestMatchingConstructor). Eslesmeyen bir key HATA VERMEZ, sessizce yok sayilir; o
// parametre kendi varsayilan degerine duser - once ilgili Strategy sinifinin constructor'ina bak.
// =============================================================================
int optChoice = 9;

string optimizationStrategyName;
List<(string name, double min, double max, double step)> optimizationRanges;
Dictionary<string, object> fixedParams;

if (optChoice == 0)
{
    optimizationStrategyName = "SimpleMostStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("mostMaMethod", 0, 2, 1),     // MAMethod enum indeksi: 0=SIMPLE(SMA), 1=EMA, 2=WMA
                                       // (registry Convert.ToInt64 + Enum.ToObject ile enum'a çevirir)
                                       // En dış döngüde - mostMaMethod sabit kalirken period/percent taranir.
        ("period",       10, 50, 10),
        ("percent",      1.0, 3.0, 1.0),
    };
    fixedParams = new Dictionary<string, object>
    {
        // priceSource enum - taranmadığı için string sabit (registry Enum.Parse, case-insensitive).
        ["priceSource"]     = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 1)
{
    // SimpleMAStrategy - fast/slow MA periyotlari ve MA tipleri taraniyor (MOST'taki mostMaMethod
    // range deseniyle ayni). priceSource + signalModeIndex burada sabit tutuluyor.
    optimizationStrategyName = "SimpleMAStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        // MA method'lar en dışta - sabit kalirken fastPeriod/slowPeriod taranir.
        ("fastMaMethod", 0, 2, 1),   // MAMethod enum indeksi: 0=SIMPLE(SMA), 1=EMA, 2=WMA
        ("slowMaMethod", 0, 2, 1),   // MAMethod enum indeksi: 0=SIMPLE(SMA), 1=EMA, 2=WMA
        ("fastPeriod",   5, 20, 5),
        ("slowPeriod",  20, 60, 10),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"]     = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 2)
{
    // SimpleComboStrategy'de taranan parametre ruleModeIndex (BuildSignals() - su an 3 eleman: 0-2).
    // Yeni bir kural eklersen ust siniri (max) da guncellemen gerekir. signalModeIndex (seviye/kesisim)
    // burada sabit tutuluyor - o da taranmak istenirse ikinci bir range olarak eklenir.
    optimizationStrategyName = "SimpleComboStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("ruleModeIndex", 0, 2, 1),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 3)
{
    // SimpleComboStrategyRule001 - SimpleComboStrategy'nin ruleModeIndex==0 dalinin (MA1 x MA2 kesisimi)
    // periyotlari gercek constructor parametresi yapilmis hali (bkz. sinifin basindaki doc
    // comment'teki "VERSIYONLAMA KARARI"). ruleModeIndex burada yok - taranacak olan dogrudan
    // periyotlar. signalModeIndex (seviye/kesisim) burada sabit tutuluyor.
    optimizationStrategyName = "SimpleComboStrategyRule001";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("ma1Period", 3, 21, 2),
        ("ma2Period", 5, 55, 5),
    };
    fixedParams = new Dictionary<string, object>
    {
        // seviye (0) yerine kesisim (1): MA siralamasi uzun bar araliklarinda sabit kaldigi icin
        // seviye modu her barda ayni yonde sinyal tekrarlayip asiri islem/komisyona yol aciyordu.
        ["signalModeIndex"] = 1
    };
}
else if (optChoice == 4)
{
    // SimpleComboStrategyRule002 - SimpleComboStrategy'nin ruleModeIndex==1 dalinin (MA1>MA2>MA3
    // siralamasi + RSI momentum) periyotlari gercek constructor parametresi yapilmis hali.
    optimizationStrategyName = "SimpleComboStrategyRule002";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("ma1Period",  3, 13, 2),
        ("ma2Period",  8, 34, 4),
        ("ma3Period", 13, 55, 6),
        ("rsiPeriod",  7, 21, 7),
    };
    fixedParams = new Dictionary<string, object>
    {
        // ayni gerekce (bkz. optChoice==3): seviye yerine kesisim
        ["signalModeIndex"] = 1
    };
}
else if (optChoice == 5)
{
    // SimpleComboStrategyRule003 - SimpleComboStrategy'nin ruleModeIndex==2 dalinin (MACD +
    // SuperTrend) periyotlari gercek constructor parametresi yapilmis hali.
    optimizationStrategyName = "SimpleComboStrategyRule003";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("macdFastPeriod",   8, 16, 2),
        ("macdSlowPeriod",  20, 32, 4),
        ("superTrendPeriod", 7, 21, 7),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["macdSignalPeriod"]     = 9,
        ["superTrendMultiplier"] = 3.0,
        ["signalModeIndex"]      = 0
    };
}
else if (optChoice == 6)
{
    // SimpleRSIStrategy - RSI periyodu ve oversold/overbought seviyeleri taraniyor.
    // priceSource + signalModeIndex burada sabit tutuluyor.
    optimizationStrategyName = "SimpleRSIStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("period",      7, 21, 7),
        ("oversold",   20, 35, 5),
        ("overbought", 65, 80, 5),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"]     = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 7)
{
    // SimpleOTTStrategy - OTT'un MA'si icin method + period/percent taraniyor
    // (MOST'taki mostMaMethod range deseniyle ayni). priceSource + signalModeIndex sabit tutuluyor.
    optimizationStrategyName = "SimpleOTTStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("ottMaMethod", 0, 2, 1),      // MAMethod enum indeksi: 0=SIMPLE(SMA), 1=EMA, 2=WMA
                                       // (VIDYA=12 ayri deneme gerektirir, taramaya SMA/EMA/WMA konuldu)
        ("period",   1, 10, 1),
        ("percent",  0.5, 3.0, 0.5),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"]     = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 8)
{
    // SimpleSuperTrendStrategy - ATR periyodu ve multiplier taraniyor.
    // priceSource + signalModeIndex burada sabit tutuluyor.
    optimizationStrategyName = "SimpleSuperTrendStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("period",     7, 21, 7),
        ("multiplier", 1.0, 4.0, 1.0),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"]     = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 9)
{
    // SimpleParabolicSARStrategy - hizlanma faktoru adimi (step) ve maksimumu (max) taraniyor.
    // priceSource + signalModeIndex burada sabit tutuluyor.
    optimizationStrategyName = "SimpleParabolicSARStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("step", 0.01, 0.05, 0.01),
        ("max",  0.1,  0.3,  0.1),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"]     = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 10)
{
    // SimpleADXStrategy - period ve adxThreshold taraniyor.
    optimizationStrategyName = "SimpleADXStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("period",       7, 21, 7),
        ("adxThreshold", 15, 35, 10),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 11)
{
    // SimpleDIStrategy - sadece period taraniyor (ADX filtresi yok).
    optimizationStrategyName = "SimpleDIStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("period", 7, 21, 7),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 12)
{
    // SimpleMACDStrategy - fast/slow EMA periyotlari taraniyor.
    optimizationStrategyName = "SimpleMACDStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("fastPeriod", 8, 16, 4),
        ("slowPeriod", 20, 30, 5),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["signalPeriod"]    = 9,
        ["priceSource"]     = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 13)
{
    // SimpleStochasticStrategy - kPeriod ve dPeriod taraniyor.
    optimizationStrategyName = "SimpleStochasticStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("kPeriod", 7, 21, 7),
        ("dPeriod", 2, 5, 1),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["centerLine"]      = 50,
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 14)
{
    // SimpleBollingerStrategy - period ve multiplier taraniyor.
    optimizationStrategyName = "SimpleBollingerStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("period",     10, 30, 10),
        ("multiplier", 1.5, 2.5, 0.5),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"]     = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 15)
{
    // SimpleATRStrategy - atrPeriod ve multiplier taraniyor.
    optimizationStrategyName = "SimpleATRStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("atrPeriod",  7, 21, 7),
        ("multiplier", 1.5, 2.5, 0.5),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["maPeriod"]        = 20,
        ["priceSource"]     = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 16)
{
    // SimpleCMFStrategy - period taraniyor.
    optimizationStrategyName = "SimpleCMFStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("period", 10, 30, 10),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["positiveThreshold"] = 0.1,
        ["negativeThreshold"] = -0.1,
        ["signalModeIndex"]   = 0
    };
}
else if (optChoice == 17)
{
    // SimpleMFIStrategy - period taraniyor.
    optimizationStrategyName = "SimpleMFIStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("period", 7, 21, 7),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["oversold"]        = 20,
        ["overbought"]      = 80,
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 18)
{
    // SimpleKairiStrategy - period taraniyor.
    optimizationStrategyName = "SimpleKairiStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("period", 10, 30, 10),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["positiveThreshold"] = 5,
        ["negativeThreshold"] = -5,
        ["priceSource"]       = "Close",
        ["signalModeIndex"]   = 0
    };
}
else if (optChoice == 19)
{
    // SimpleMomentumStrategy - period taraniyor.
    optimizationStrategyName = "SimpleMomentumStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("period", 7, 21, 7),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["positiveThreshold"] = 0,
        ["negativeThreshold"] = 0,
        ["priceSource"]       = "Close",
        ["signalModeIndex"]   = 0
    };
}
else if (optChoice == 20)
{
    // SimpleHHVLLVStrategy - period taraniyor.
    optimizationStrategyName = "SimpleHHVLLVStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("period", 10, 30, 10),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"]     = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 21)
{
    // SimpleHYLYStrategy - period taraniyor.
    optimizationStrategyName = "SimpleHYLYStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("period", 10, 30, 10),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["threshold"]       = 80,
        ["priceSource"]     = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 22)
{
    // SimpleIchimokuStrategy - tenkanPeriod taraniyor.
    optimizationStrategyName = "SimpleIchimokuStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("tenkanPeriod", 7, 11, 2),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["kijunPeriod"]     = 26,
        ["senkouPeriod"]    = 52,
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 23)
{
    // SimpleMavilimWStrategy - param1/param2 taraniyor.
    optimizationStrategyName = "SimpleMavilimWStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("param1", 2, 4, 1),
        ("param2", 4, 6, 1),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 24)
{
    // SimplePMaxStrategy - atrPeriod ve multiplier taraniyor.
    optimizationStrategyName = "SimplePMaxStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("atrPeriod",  7, 13, 3),
        ("multiplier", 2.0, 4.0, 1.0),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["maPeriod"]        = 10,
        ["pmaxMaMethod"]    = "EMA",
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 25)
{
    // SimpleTillsonT3Strategy - period taraniyor.
    optimizationStrategyName = "SimpleTillsonT3Strategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("period", 3, 7, 2),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["priceSource"]     = "Close",
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 26)
{
    // SimpleAlphaTrendStrategy - atrPeriod ve coefficient taraniyor.
    optimizationStrategyName = "SimpleAlphaTrendStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("atrPeriod",   7, 21, 7),
        ("coefficient", 0.5, 1.5, 0.5),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["momentumPeriod"]  = 14,
        ["useMFI"]          = true,
        ["signalModeIndex"] = 0
    };
}
else
{
    throw new ArgumentOutOfRangeException(nameof(optChoice), $"Bilinmeyen optChoice: {optChoice}");
}

// =============================================================================
// Optimization Range (PartialOpt)
// -1 = en bastan / en sona kadar (FullOpt)
// Ornek: from=5, to=10 -> sadece 5-10 arasi kombinasyonlari calistir
// =============================================================================
int optimizationFrom = -1;
int optimizationTo = -1;

// =============================================================================
// Trade Params
// MarketType/HisseSayisi/LotSayisi/PyramidingEnabled - AppConfigApplier.BuildInitialTradeParams()
// (AppConfigApplier.cs:1370) ile ayni alanlar, TAM InitialTradeParams olusturup
// SetSingleTraderTradeParams()'a veriliyor (asagida, ana scriptte). Bu olmadan
// SingleTraderOptimizer "ViopEndex fallback"a duserdi (bkz. docs/manual/
// 07-menu-vs-script-parity.md SS3, 2026-08-25 findings - artik duzeltildi).
// Gecerli MarketType degerleri: BistEndex, BistHisse, BistParite, BistMetal,
// ViopEndex, ViopHisse, ViopParite, ViopMetal, FxEndex, FxHisse, FxParite, FxMetal,
// FxCrypto, Crypto.
// =============================================================================
string marketType = "ViopEndex";
double ilkBakiye = 100000.0;
int kontratSayisi = 1;
double lotSayisi = 0.01;
double hisseSayisi = 1000.0;
double komisyonCarpan = 20.0;
double kaymaMiktari = 0.0;
bool pyramidingEnabled = false;

// =============================================================================
// Equity Curve Filter (opsiyonel) - AppConfig.json'daki SingleTraderOptimizer.EquityCurveFilter
// bolumunun karsiligi (AppConfigApplier.ApplySingleTraderOpt() -> ConfigureEquityCurveFilterFromConfig
// ile ayni yol). SS1/SS2'deki basit ecfEnabled/ecfThresholdTypeIsPercent/... alanlarindan FARKLI
// bir mekanizma: optimizer her kombinasyon icin ECF'yi Id=0 uzerinden "stored config" olarak okuyor
// (AlgoTrader.cs:2894-2896), bu yuzden degerler dogrudan degil, EquityCurveFilterConfig.txt
// dosyasindan versiyon adiyla yukleniyor.
// ecfEnabled=false (varsayilan): ECF hic yuklenmez, optimizasyon ECF'siz calisir - AppConfig.json'da
// bu bolum bos/yoksa [7] (menu) da ayni sekilde davranir.
// ecfVersion="v1" -> inputs/configs/EquityCurveFilterConfig.txt'deki "v1|Disabled|enabled:bool:false|..."
// satirina karsilik gelir (yani ecfEnabled=true yapip ecfVersion'i "v1" birakirsaniz ECF YINE devre
// disi kalir - dosyanin kendi "enabled" alani gecerli olur, buradaki ecfEnabled sadece "ECF config'i
// hic yukle" / "yukleme" anahtaridir). Gercekten filtreli test etmek icin ecfVersion'i "v2"-"v7"
// arasindan birine (bkz. inputs/configs/EquityCurveFilterConfig.txt) degistirin.
// =============================================================================
bool ecfEnabled = false;
string ecfConfigFile = "EquityCurveFilterConfig.txt";
string ecfVersion = "v1";

// =============================================================================
// Symbol Info
// =============================================================================
string symbolName = "...";
string symbolPeriod = "...";
