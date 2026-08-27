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
int optChoice = 0;

string optimizationStrategyName;
List<(string name, double min, double max, double step)> optimizationRanges;
Dictionary<string, object> fixedParams;

if (optChoice == 0)
{
    optimizationStrategyName = "SimpleMostStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("period",  10, 50, 10),
        ("percent", 1.0, 3.0, 1.0),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["signalModeIndex"] = 0
    };
}
else if (optChoice == 1)
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
double kaymaMiktari = 0.5;
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
