namespace AlgoTrade.Core.AppConfig;

// =============================================================================
// Root
// =============================================================================

public class AppConfig
{
    public AppSettingsConfig     AppSettings    { get; set; } = new();
    public ReadDataConfig        ReadData       { get; set; } = new();
    public SingleTraderConfig    SingleTrader   { get; set; } = new();
    public MultipleTraderConfig  MultipleTrader { get; set; } = new();
    public SingleTraderOptConfig SingleTraderOptimizer { get; set; } = new();
    public SymbolScanConfig      SymbolScan     { get; set; } = new();
    public TimeframeScanConfig   TimeframeScan  { get; set; } = new();
    public MultiStrategyTimeframeScanConfig MultiStrategyTimeframeScan { get; set; } = new();
    public SymbolTimeframeScanConfig SymbolTimeframeScan { get; set; } = new();
    public MultiStrategySymbolScanConfig MultiStrategySymbolScan { get; set; } = new();
    public MultiStrategySymbolTimeframeScanConfig MultiStrategySymbolTimeframeScan { get; set; } = new();
}

// =============================================================================
// AppSettings
// =============================================================================

public class AppSettingsConfig
{
    /// <summary>CSV veri dosyasının tam yolu.</summary>
    public string StockDataFile { get; set; } = "";

    /// <summary>
    /// Menü geri sayım süresi (saniye).
    /// 0 → sayaç yok, kullanıcı giriş yapana kadar bekler.
    /// </summary>
    public int MenuTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Otomatik çalıştırma modu. Uygulama başlarken JSON config'den
    /// okuyup hiç onay almadan doğrudan çalıştırır ve çıkar.
    /// Boş veya "None" → normal menü akışı.
    /// Geçerli değerler: SingleTrader | MultipleTrader | SingleTraderOptimizer
    /// </summary>
    public string AutoRunMode { get; set; } = "";
}

// =============================================================================
// ReadData
// =============================================================================

/// <summary>
/// ReadDataFast parametreleri.
/// FilterMode: All | LastN | FirstN | IndexRange | AfterDateTime | BeforeDateTime | DateTimeRange
/// </summary>
public class ReadDataConfig
{
    public string FilterMode { get; set; } = "All";
    public int    N1         { get; set; } = 0;
    public int    N2         { get; set; } = 0;
    public string Dt1        { get; set; } = "";
    public string Dt2        { get; set; } = "";
}

// =============================================================================
// Export config
// =============================================================================

/// <summary>
/// Trader çıktılarını StatisticsExporterConfig.json'daki sütun tanımlarıyla dosyaya yazar.
/// Version → StatisticsExporterConfig.json içindeki versiyon anahtarı (örn. "v1", "v2").
/// </summary>
public class TraderExportConfig
{
    public bool   ExportEnabled { get; set; } = false;
    public string ConfigFile    { get; set; } = "StatisticsExporterConfig.json";
    public string Version       { get; set; } = "v1";
}

// =============================================================================
// Reference types  (config dosyalarına referans)
// =============================================================================

/// <summary>StrategyConfig.txt içindeki bir stratejiye referans.</summary>
public class StrategyRef
{
    public string ConfigFile { get; set; } = "StrategyConfig.txt";
    public string Name       { get; set; } = "";
    public string Version    { get; set; } = "";
}

/// <summary>QueryConfig.txt içindeki bir query'ye referans.</summary>
public class QueryRef
{
    public string ConfigFile { get; set; } = "QueryConfig.txt";
    public string Name       { get; set; } = "";
    public string Version    { get; set; } = "";
}

/// <summary>EquityCurveFilterConfig.txt içindeki bir filtreye referans.</summary>
public class EcfRef
{
    public string ConfigFile { get; set; } = "EquityCurveFilterConfig.txt";
    public string Name       { get; set; } = "";
    public string Version    { get; set; } = "";
}

/// <summary>OptimizationConfig.txt içindeki bir opt konfigürasyonuna referans.</summary>
public class OptRef
{
    public string ConfigFile { get; set; } = "OptimizationConfig.txt";
    public string Name       { get; set; } = "";
    public string Version    { get; set; } = "";
}

// =============================================================================
// TradeParamsConfig
// =============================================================================

/// <summary>
/// Her trader için pozisyon boyutu, bakiye, komisyon ve kayma parametreleri.
/// MarketType'a göre ilgili alan geçerlidir:
///   ViopXxx  → KontratSayisi
///   BistXxx  → HisseSayisi
///   FxXxx / Crypto → LotSayisi
/// </summary>
public class TradeParamsConfig
{
    /// <summary>
    /// Piyasa tipi. Geçerli değerler:
    /// BistEndex, BistHisse, BistParite, BistMetal,
    /// ViopEndex, ViopHisse, ViopParite, ViopMetal,
    /// FxEndex, FxHisse, FxParite, FxMetal, FxCrypto, Crypto
    /// </summary>
    public string MarketType      { get; set; } = "ViopEndex";

    public double IlkBakiye       { get; set; } = 100_000.0;

    /// <summary>Viop piyasaları için kontrat sayısı.</summary>
    public double KontratSayisi   { get; set; } = 1.0;

    /// <summary>Fx / Crypto piyasaları için lot sayısı.</summary>
    public double LotSayisi       { get; set; } = 0.01;

    /// <summary>Bist piyasaları için hisse sayısı.</summary>
    public double HisseSayisi     { get; set; } = 1000.0;

    public double KomisyonCarpan  { get; set; } = 20.0;
    public double KaymaMiktari    { get; set; } = 0.5;

    public bool PyramidingEnabled { get; set; } = false;
}

// =============================================================================
// Trader Signals & Save Config
// =============================================================================

/// <summary>SingleTrader sinyal bayrakları ve zaman filtresi (OnApplyUserFlags karşılığı).</summary>
public class TraderSignalsConfig
{
    public bool   AlEnabled              { get; set; } = true;
    public bool   SatEnabled             { get; set; } = true;
    public bool   FlatOlEnabled          { get; set; } = true;
    public bool   PasGecEnabled          { get; set; } = true;
    public bool   KarAlEnabled           { get; set; } = true;
    public bool   ZararKesEnabled        { get; set; } = true;
    public bool   GunSonuPozKapatEnabled { get; set; } = false;
    public bool   TimeFilteringEnabled       { get; set; } = false;
    public string StartDateTime              { get; set; } = "2025.05.25 09:35:00";
    public string StopDateTime               { get; set; } = "2025.06.02 17:55:00";
    public bool   TradeStartBarIndexEnabled  { get; set; } = false;
    public int    TradeStartBarIndex         { get; set; } = 0;
}

/// <summary>SingleTrader kayıt bayrakları (OnApplyUserFlags2 karşılığı).</summary>
public class TraderSaveConfig
{
    public bool SaveStatisticsToFile                { get; set; } = false;
    public bool SaveFullStatsTxtEnabled             { get; set; } = true;
    public bool SaveFullStatsCsvEnabled             { get; set; } = true;
    public bool SaveMinimalStatsTxtEnabled          { get; set; } = true;
    public bool SaveMinimalStatsCsvEnabled          { get; set; } = true;
    public bool SaveFullListsTxtEnabled             { get; set; } = true;
    public bool SaveFullListsCsvEnabled             { get; set; } = true;
    public bool SaveMinimalListsTxtEnabled          { get; set; } = true;
    public bool SaveMinimalListsCsvEnabled          { get; set; } = true;
    public bool SaveFullStatsTxtFormattedEnabled    { get; set; } = true;
    public bool SaveMinimalStatsTxtFormattedEnabled { get; set; } = true;
    public bool SavePerformansTxtEnabled            { get; set; } = true;
    public bool SavePerformansCsvEnabled            { get; set; } = true;

    // Çıktı dosya adları
    public string FullStatsTxtFileName             { get; set; } = "SingleTraderStatistics.txt";
    public string FullStatsCsvFileName             { get; set; } = "SingleTraderStatistics.csv";
    public string MinimalStatsTxtFileName          { get; set; } = "SingleTraderStatisticsMinimal.txt";
    public string MinimalStatsCsvFileName          { get; set; } = "SingleTraderStatisticsMinimal.csv";
    public string FullListsTxtFileName             { get; set; } = "SingleTraderLists.txt";
    public string FullListsCsvFileName             { get; set; } = "SingleTraderLists.csv";
    public string MinimalListsTxtFileName          { get; set; } = "SingleTraderListsMinimal.txt";
    public string MinimalListsCsvFileName          { get; set; } = "SingleTraderListsMinimal.csv";
    public string FullStatsTxtFormattedFileName    { get; set; } = "SingleTraderStatisticsFormatted.txt";
    public string MinimalStatsTxtFormattedFileName { get; set; } = "SingleTraderStatisticsMinimalFormatted.txt";
    public string PerformansTxtFileName            { get; set; } = "SingleTraderPerformans.txt";
    public string PerformansCsvFileName            { get; set; } = "SingleTraderPerformans.csv";
}

/// <summary>SingleTrader plot ayarları.</summary>
public class TraderPlotConfig
{
    public bool PlotEnabled { get; set; } = false;
}

/// <summary>SingleTrader optimizasyon modu ayarları.</summary>
public class TraderOptimizationConfig
{
    public bool OptimizationEnabled { get; set; } = false;
}

// =============================================================================
// SingleTrader
// =============================================================================

public class SingleTraderConfig
{
    /// <summary>TradeOnly | TradeAndQuery | QueryOnly</summary>
    public string RunMode { get; set; } = "TradeOnly";

    public StrategyRef         Strategy          { get; set; } = new();
    public QueryRef?           Query             { get; set; }
    public EcfRef?             EquityCurveFilter { get; set; }
    public TradeParamsConfig   TradeParams       { get; set; } = new();
    public TraderSignalsConfig      Signals      { get; set; } = new();
    public TraderPlotConfig         Plot         { get; set; } = new();
    public TraderOptimizationConfig Optimization { get; set; } = new();
    public TraderSaveConfig         Save         { get; set; } = new();
    public TraderExportConfig?      Export       { get; set; }
}

// =============================================================================
// MultipleTrader
// =============================================================================

/// <summary>
/// MultipleTrader nesnesinin kayıt ayarları (composite liste dosyaları).
/// MainTrader ve child'ların istatistiklerinden ayrı, MultipleTrader'a özgü.
/// </summary>
public class MultipleTraderSaveConfig
{
    public bool   SaveStatisticsToFile                { get; set; } = true;
    public bool   SaveMultipleTraderListsTxtEnabled   { get; set; } = true;
    public bool   SaveMultipleTraderListsCsvEnabled   { get; set; } = true;
    public string MultipleTraderListsTxtFileName      { get; set; } = "MultipleTraderLists.txt";
    public string MultipleTraderListsCsvFileName      { get; set; } = "MultipleTraderLists.csv";

    /// <summary>true → child trader istatistikleri de dosyaya yazılır.</summary>
    public bool   WriteChildTradersDataToFiles        { get; set; } = true;

    /// <summary>
    /// MainTrader ve child trader dosya isimlerine eklenen ön ek.
    /// MainTrader → {FilePrefix}_Main_{FileName}
    /// ChildTrader → {FilePrefix}_Child{i}_{FileName}
    /// </summary>
    public string FilePrefix                          { get; set; } = "MultipleTrader";
}

/// <summary>
/// BuildConsensusSignal() için konfigürasyon.
/// Mode:
///   Net      → buyCount - sellCount &gt; 0 → Buy; &lt; 0 → Sell; = 0 → Flat  (varsayılan)
///   Majority → buyCount &gt; N/2 → Buy; sellCount &gt; N/2 → Sell; aksi → Flat
///   All      → tüm child aynı yönde → o yön; aksi → Flat
///   Any      → en az 1 Buy → Buy; en az 1 Sell → Sell; ikisi de varsa → Flat
/// MinNetCount: Net modunda minimum net fark eşiği (varsayılan: 1).
///   Örn. MinNetCount=2 → buyCount - sellCount &gt;= 2 ise Buy, &lt;= -2 ise Sell.
/// </summary>
public class ConsensusConfig
{
    public string Mode        { get; set; } = "Net";
    public int    MinNetCount { get; set; } = 1;
}

/// <summary>
/// MultipleTrader'ın ana trader konfigürasyonu.
/// ChildTrader'lardan gelen composite sinyal üzerinde işlem yapar.
/// Strategy ve Query yok — sinyal tamamen ChildTrader'lardan gelir.
/// </summary>
public class MainTraderConfig
{
    public EcfRef?                  EquityCurveFilter { get; set; }
    public TradeParamsConfig        TradeParams       { get; set; } = new();
    public TraderSignalsConfig      Signals           { get; set; } = new();
    public TraderPlotConfig         Plot              { get; set; } = new();
    public TraderOptimizationConfig Optimization      { get; set; } = new();
    public TraderSaveConfig         Save              { get; set; } = new();
    public TraderExportConfig?      Export            { get; set; }
}

/// <summary>
/// MultipleTrader içindeki tek bir child trader tanımı.
/// TradeParams MainTrader'dan alınır; her child kendi Strategy/Signals/Save'ine sahiptir.
/// </summary>
public class ChildTraderEntry
{
    public int                      ChildId           { get; set; }
    public StrategyRef              Strategy          { get; set; } = new();
    public QueryRef?                Query             { get; set; }
    public EcfRef?                  EquityCurveFilter { get; set; }
    public TraderSignalsConfig      Signals           { get; set; } = new();
    public TraderPlotConfig         Plot              { get; set; } = new();
    public TraderOptimizationConfig Optimization      { get; set; } = new();
    public TraderSaveConfig         Save              { get; set; } = new();
    public TraderExportConfig?      Export            { get; set; }
}

public class MultipleTraderConfig
{
    /// <summary>TradeOnly | TradeAndQuery | QueryOnly</summary>
    public string             RunMode      { get; set; } = "TradeOnly";

    /// <summary>MultipleTrader nesnesinin liste/kayıt ayarları.</summary>
    public MultipleTraderSaveConfig Save   { get; set; } = new();

    /// <summary>MultipleTrader nesnesinin export ayarları.</summary>
    public TraderExportConfig?      Export { get; set; }

    /// <summary>BuildConsensusSignal() davranışını belirler.</summary>
    public ConsensusConfig    Consensus    { get; set; } = new();

    /// <summary>Ana trader konfigürasyonu (composite sinyal → gerçek işlem).</summary>
    public MainTraderConfig   MainTrader   { get; set; } = new();

    /// <summary>
    /// Her child trader tanımı burada listelenir.
    /// Sıra önemlidir — ChildId alanı referans olarak kullanılır.
    /// </summary>
    public List<ChildTraderEntry> ChildTraders { get; set; } = new();
}

// =============================================================================
// SingleTraderOptimizer
// =============================================================================

public class SingleTraderOptConfig
{
    public OptRef                      Optimization      { get; set; } = new();
    public StrategyRef                 Strategy          { get; set; } = new();
    public SingleTraderOptRangeConfig  Range             { get; set; } = new();
    public TradeParamsConfig           TradeParams       { get; set; } = new();
    public EcfRef?                     EquityCurveFilter { get; set; }
    public TraderSignalsConfig         Signals           { get; set; } = new();
    public SingleTraderOptSaveConfig   Save              { get; set; } = new();
    public TraderExportConfig?         Export            { get; set; }
    public SingleTraderOptSortConfig   Sort              { get; set; } = new();
    public SingleTraderOptTraderConfig SingleTrader      { get; set; } = new();
}

public class SingleTraderOptRangeConfig
{
    public int OptimizationFrom { get; set; } = -1;
    public int OptimizationTo   { get; set; } = -1;
}

public class SingleTraderOptSaveConfig
{
    public bool   CsvFileLoggingEnabled               { get; set; } = true;
    public bool   TxtFileLoggingEnabled               { get; set; } = true;
    public bool   StatisticsExporterConfigFileEnabled { get; set; } = true;
    public string CsvFileName                         { get; set; } = "singleTraderOptLog.csv";
    public string TxtFileName                         { get; set; } = "singleTraderOptLog.txt";
    public string StatisticsExporterConfigFile        { get; set; } = "StatisticsExporterConfig.json";
    public bool   AppendEnabled                       { get; set; } = true;
    /// <summary>-1: her kombinasyonda yaz. >=0: ms cinsinden aralıkta yaz (bellekte biriktirir).</summary>
    public int    FileFlushIntervalMs                 { get; set; } = -1;
}

public class SingleTraderOptSortConfig
{
    public string SortField         { get; set; } = "GetiriFiyatNet";
    public string SortedCsvFileName { get; set; } = "singleTraderOptLog_sorted.csv";
    public string SortedTxtFileName { get; set; } = "singleTraderOptLog_sorted.txt";
}

/// <summary>Best trader (optimizasyon sonucu) için ayarlar.</summary>
public class SingleTraderOptTraderConfig
{
    public TraderPlotConfig         Plot         { get; set; } = new();
    public TraderOptimizationConfig Optimization { get; set; } = new();
    public TraderSaveConfig         Save         { get; set; } = new();
    public TraderExportConfig?      Export       { get; set; }
}

// =============================================================================
// SymbolScan  (Tarama — roadmap madde 8: aynı strateji, birden fazla sembol)
// =============================================================================

/// <summary>
/// Aynı stratejiyi birden fazla sembolde bağımsız olarak çalıştırıp sonuçları
/// tek bir özet tabloda toplar (bkz. docs/tarama-motoru-plan.md).
/// SingleTraderOptimizer'dan bağımsız bir motor (SymbolScanner) kullanır —
/// veri (dosya) değiştiği için parametre-kombinasyonu değil sembol listesi üzerinde döner.
/// </summary>
public class SymbolScanConfig
{
    /// <summary>Taranacak sembol dosyalarının bulunduğu klasör (tam yol). Örn. C:\data\csvFiles\CRP\05</summary>
    public string DataFolder { get; set; } = "";

    /// <summary>true: DataFolder'daki tüm *.csv dosyaları otomatik taranır. false: SymbolList kullanılır.</summary>
    public bool AutoDiscover { get; set; } = true;

    /// <summary>AutoDiscover=false iken kullanılır. Her eleman dosya adı köküyle birebir (örn. "BTCUSDT_BNC").</summary>
    public List<string> SymbolList { get; set; } = new();

    public StrategyRef          Strategy    { get; set; } = new();
    public TradeParamsConfig    TradeParams { get; set; } = new();
    public TraderSignalsConfig  Signals     { get; set; } = new();
    public ReadDataConfig       ReadData    { get; set; } = new();

    /// <summary>true: her sembol için ayrıca tam istatistik dosyaları (Statistics/Lists/Performans) da yazılır.</summary>
    public bool WriteFullStatsPerSymbol { get; set; } = false;

    public SymbolScanSortConfig Sort { get; set; } = new();
    public SymbolScanSaveConfig Save { get; set; } = new();
}

public class SymbolScanSortConfig
{
    /// <summary>Statistics.GetOptimizationSummary() içindeki bir alan adı (örn. NetProfit, ProfitFactor).</summary>
    public string SortField      { get; set; } = "NetProfit";
    public bool   SortDescending { get; set; } = true;
}

public class SymbolScanSaveConfig
{
    public string CsvFileName       { get; set; } = "SymbolScanResults.csv";
    public string TxtFileName       { get; set; } = "SymbolScanResults.txt";
    public string SortedCsvFileName { get; set; } = "SymbolScanResults_sorted.csv";
    public string SortedTxtFileName { get; set; } = "SymbolScanResults_sorted.txt";
}

// =============================================================================
// TimeframeScan  (Tarama — Yapı Taşı A: aynı sembol, farklı zaman dilimleri, BAĞIMSIZ taranır)
// =============================================================================

/// <summary>
/// Aynı sembolü birden fazla zaman diliminde bağımsız olarak çalıştırıp sonuçları tek bir
/// özet tabloda toplar (bkz. docs/tarama-motoru-plan.md). Zaman dilimleri arasında
/// KONSENSÜS/BİLEŞKE yok — her biri ayrı bir backtest, ayrı bir sonuç satırı.
/// SymbolScanConfig'in yapısal ikizi (TimeframeScanner, SymbolScanner'dan bağımsız bir sınıf).
/// </summary>
public class TimeframeScanConfig
{
    /// <summary>Zaman dilimi klasörlerinin bulunduğu üst klasör (tam yol). Örn. C:\data\csvFiles\CRP</summary>
    public string BaseFolder { get; set; } = "";

    /// <summary>Dosya adı köküyle birebir (örn. "BTCUSDT_BNC"). Her TF klasöründe aynı isimle aranır.</summary>
    public string Symbol { get; set; } = "";

    /// <summary>Taranacak zaman dilimi klasör adları (örn. ["01","05","15","60"]). Otomatik keşif yok, açık liste.</summary>
    public List<string> Timeframes { get; set; } = new();

    public StrategyRef          Strategy    { get; set; } = new();
    public TradeParamsConfig    TradeParams { get; set; } = new();
    public TraderSignalsConfig  Signals     { get; set; } = new();
    public ReadDataConfig       ReadData    { get; set; } = new();

    /// <summary>true: her zaman dilimi için ayrıca tam istatistik dosyaları da yazılır.</summary>
    public bool WriteFullStatsPerTimeframe { get; set; } = false;

    public TimeframeScanSortConfig Sort { get; set; } = new();
    public TimeframeScanSaveConfig Save { get; set; } = new();
}

public class TimeframeScanSortConfig
{
    public string SortField      { get; set; } = "NetProfit";
    public bool   SortDescending { get; set; } = true;
}

public class TimeframeScanSaveConfig
{
    public string CsvFileName       { get; set; } = "TimeframeScanResults.csv";
    public string TxtFileName       { get; set; } = "TimeframeScanResults.txt";
    public string SortedCsvFileName { get; set; } = "TimeframeScanResults_sorted.csv";
    public string SortedTxtFileName { get; set; } = "TimeframeScanResults_sorted.txt";
}

// =============================================================================
// MultiStrategyTimeframeScan  (Tarama — Senaryo 4: tek sembol, MultipleTrader consensus'u,
// birden fazla zaman diliminde BAĞIMSIZ)
// =============================================================================

/// <summary>
/// Aynı sembolde MultipleTrader'ın (birden fazla stratejinin consensus'u) her zaman diliminde
/// bağımsız çalıştırılmasını konfigüre eder. Child stratejiler/consensus modu/trade params için
/// mevcut <see cref="MultipleTraderConfig"/> tipi birebir reuse edilir — AppConfig.json'daki
/// "MultipleTrader" bölümüyle aynı şema. Zaman dilimleri arasında konsensüs YOK, her TF ayrı bir
/// sonuç satırı (bkz. docs/tarama-motoru-plan.md, "Senaryo 4" bölümü).
/// </summary>
public class MultiStrategyTimeframeScanConfig
{
    /// <summary>Zaman dilimi klasörlerinin bulunduğu üst klasör (tam yol). Örn. C:\data\csvFiles\CRP</summary>
    public string BaseFolder { get; set; } = "";

    /// <summary>Dosya adı köküyle birebir (örn. "BTCUSDT_BNC"). Her TF klasöründe aynı isimle aranır.</summary>
    public string Symbol { get; set; } = "";

    /// <summary>Taranacak zaman dilimi klasör adları (örn. ["01","05","15","60"]). Açık liste, otomatik keşif yok.</summary>
    public List<string> Timeframes { get; set; } = new();

    /// <summary>Child stratejiler, consensus modu, trade params — mevcut MultipleTrader şemasıyla birebir aynı.</summary>
    public MultipleTraderConfig MultipleTrader { get; set; } = new();

    public ReadDataConfig ReadData { get; set; } = new();

    public MultiStrategyTimeframeScanSortConfig Sort { get; set; } = new();
    public MultiStrategyTimeframeScanSaveConfig Save { get; set; } = new();
}

public class MultiStrategyTimeframeScanSortConfig
{
    public string SortField      { get; set; } = "NetProfit";
    public bool   SortDescending { get; set; } = true;
}

public class MultiStrategyTimeframeScanSaveConfig
{
    public string CsvFileName       { get; set; } = "MultiStrategyTimeframeScanResults.csv";
    public string TxtFileName       { get; set; } = "MultiStrategyTimeframeScanResults.txt";
    public string SortedCsvFileName { get; set; } = "MultiStrategyTimeframeScanResults_sorted.csv";
    public string SortedTxtFileName { get; set; } = "MultiStrategyTimeframeScanResults_sorted.txt";
}

// =============================================================================
// SymbolTimeframeScan  (Tarama — Senaryo 6: çoklu sembol, tek strateji, çoklu zaman dilimi,
// ikisi de TAMAMEN BAĞIMSIZ)
// =============================================================================

/// <summary>
/// Tek bir stratejiyi hem sembol hem zaman dilimi ekseninde bağımsız çalıştırıp sonuçları tek
/// bir özet tabloda (N sembol × M TF satır) toplar (bkz. docs/tarama-motoru-plan.md, "Senaryo 6").
/// Hiçbir eksende konsensüs/bileşke yok — SymbolScanConfig/TimeframeScanConfig'in iç içe
/// geçmiş hali.
/// </summary>
public class SymbolTimeframeScanConfig
{
    /// <summary>Zaman dilimi klasörlerinin bulunduğu üst klasör (tam yol). Örn. C:\data\csvFiles\CRP</summary>
    public string BaseFolder { get; set; } = "";

    /// <summary>true: ReferenceTimeframe klasöründeki tüm *.csv dosyaları otomatik taranır. false: SymbolList kullanılır.</summary>
    public bool AutoDiscover { get; set; } = true;

    /// <summary>AutoDiscover=true iken sembol keşfi için taranacak TF klasör adı (örn. "05").</summary>
    public string ReferenceTimeframe { get; set; } = "";

    /// <summary>AutoDiscover=false iken kullanılır. Her eleman dosya adı köküyle birebir (örn. "BTCUSDT_BNC").</summary>
    public List<string> SymbolList { get; set; } = new();

    /// <summary>Taranacak zaman dilimi klasör adları (örn. ["01","05","15","60"]). Otomatik keşif yok, açık liste.</summary>
    public List<string> Timeframes { get; set; } = new();

    public StrategyRef          Strategy    { get; set; } = new();
    public TradeParamsConfig    TradeParams { get; set; } = new();
    public TraderSignalsConfig  Signals     { get; set; } = new();
    public ReadDataConfig       ReadData    { get; set; } = new();

    /// <summary>true: her (sembol, TF) hücresi için ayrıca tam istatistik dosyaları da yazılır.</summary>
    public bool WriteFullStatsPerCell { get; set; } = false;

    public SymbolTimeframeScanSortConfig Sort { get; set; } = new();
    public SymbolTimeframeScanSaveConfig Save { get; set; } = new();
}

public class SymbolTimeframeScanSortConfig
{
    public string SortField      { get; set; } = "NetProfit";
    public bool   SortDescending { get; set; } = true;
}

public class SymbolTimeframeScanSaveConfig
{
    public string CsvFileName       { get; set; } = "SymbolTimeframeScanResults.csv";
    public string TxtFileName       { get; set; } = "SymbolTimeframeScanResults.txt";
    public string SortedCsvFileName { get; set; } = "SymbolTimeframeScanResults_sorted.csv";
    public string SortedTxtFileName { get; set; } = "SymbolTimeframeScanResults_sorted.txt";
}

// =============================================================================
// MultiStrategySymbolScan  (Tarama — Senaryo 7: çoklu sembol, MultipleTrader consensus'u, tek
// zaman dilimi, hepsi BAĞIMSIZ)
// =============================================================================

/// <summary>
/// Aynı zaman diliminde MultipleTrader'ın (birden fazla stratejinin consensus'u) birden fazla
/// sembolde bağımsız çalıştırılmasını konfigüre eder. Child stratejiler/consensus modu/trade
/// params için mevcut <see cref="MultipleTraderConfig"/> tipi birebir reuse edilir —
/// AppConfig.json'daki ana "MultipleTrader" bölümüyle aynı şema. Semboller arasında konsensüs
/// YOK, her sembol ayrı bir sonuç satırı (bkz. docs/tarama-motoru-plan.md, "Senaryo 7" bölümü).
/// MultiStrategyTimeframeScanConfig'in (senaryo 4) doğrudan uyarlanmışı.
/// </summary>
public class MultiStrategySymbolScanConfig
{
    /// <summary>Taranacak sembol dosyalarının bulunduğu klasör (tam yol). Örn. C:\data\csvFiles\CRP\05</summary>
    public string DataFolder { get; set; } = "";

    /// <summary>true: DataFolder'daki tüm *.csv dosyaları otomatik taranır. false: SymbolList kullanılır.</summary>
    public bool AutoDiscover { get; set; } = true;

    /// <summary>AutoDiscover=false iken kullanılır. Her eleman dosya adı köküyle birebir (örn. "BTCUSDT_BNC").</summary>
    public List<string> SymbolList { get; set; } = new();

    /// <summary>Child stratejiler, consensus modu, trade params — mevcut MultipleTrader şemasıyla birebir aynı.</summary>
    public MultipleTraderConfig MultipleTrader { get; set; } = new();

    public ReadDataConfig ReadData { get; set; } = new();

    public MultiStrategySymbolScanSortConfig Sort { get; set; } = new();
    public MultiStrategySymbolScanSaveConfig Save { get; set; } = new();
}

public class MultiStrategySymbolScanSortConfig
{
    public string SortField      { get; set; } = "NetProfit";
    public bool   SortDescending { get; set; } = true;
}

public class MultiStrategySymbolScanSaveConfig
{
    public string CsvFileName       { get; set; } = "MultiStrategySymbolScanResults.csv";
    public string TxtFileName       { get; set; } = "MultiStrategySymbolScanResults.txt";
    public string SortedCsvFileName { get; set; } = "MultiStrategySymbolScanResults_sorted.csv";
    public string SortedTxtFileName { get; set; } = "MultiStrategySymbolScanResults_sorted.txt";
}

// =============================================================================
// MultiStrategySymbolTimeframeScan  (Tarama — Senaryo 8: çoklu sembol, MultipleTrader
// consensus'u, çoklu zaman dilimi, hepsi BAĞIMSIZ — matrisin en genel hâli)
// =============================================================================

/// <summary>
/// N sembol × M zaman dilimi, her hücrede MultipleTrader consensus'u — senaryo 6 (nested-loop
/// sembol × TF) ve senaryo 7'nin (throwaway AlgoTrader + MultipleTrader) bileşimi. Hiçbir eksende
/// (sembol/TF) konsensüs YOK. Child stratejiler/consensus modu/trade params için mevcut
/// <see cref="MultipleTraderConfig"/> tipi birebir reuse edilir (bkz. docs/tarama-motoru-plan.md,
/// "Senaryo 8" bölümü).
/// </summary>
public class MultiStrategySymbolTimeframeScanConfig
{
    /// <summary>Zaman dilimi klasörlerinin bulunduğu üst klasör (tam yol). Örn. C:\data\csvFiles\CRP</summary>
    public string BaseFolder { get; set; } = "";

    /// <summary>true: ReferenceTimeframe klasöründeki tüm *.csv dosyaları otomatik taranır. false: SymbolList kullanılır.</summary>
    public bool AutoDiscover { get; set; } = true;

    /// <summary>AutoDiscover=true iken sembol keşfi için taranacak TF klasör adı (örn. "05").</summary>
    public string ReferenceTimeframe { get; set; } = "";

    /// <summary>AutoDiscover=false iken kullanılır. Her eleman dosya adı köküyle birebir (örn. "BTCUSDT_BNC").</summary>
    public List<string> SymbolList { get; set; } = new();

    /// <summary>Taranacak zaman dilimi klasör adları (örn. ["01","05","15","60"]). Otomatik keşif yok, açık liste.</summary>
    public List<string> Timeframes { get; set; } = new();

    /// <summary>Child stratejiler, consensus modu, trade params — mevcut MultipleTrader şemasıyla birebir aynı.</summary>
    public MultipleTraderConfig MultipleTrader { get; set; } = new();

    public ReadDataConfig ReadData { get; set; } = new();

    public MultiStrategySymbolTimeframeScanSortConfig Sort { get; set; } = new();
    public MultiStrategySymbolTimeframeScanSaveConfig Save { get; set; } = new();
}

public class MultiStrategySymbolTimeframeScanSortConfig
{
    public string SortField      { get; set; } = "NetProfit";
    public bool   SortDescending { get; set; } = true;
}

public class MultiStrategySymbolTimeframeScanSaveConfig
{
    public string CsvFileName       { get; set; } = "MultiStrategySymbolTimeframeScanResults.csv";
    public string TxtFileName       { get; set; } = "MultiStrategySymbolTimeframeScanResults.txt";
    public string SortedCsvFileName { get; set; } = "MultiStrategySymbolTimeframeScanResults_sorted.csv";
    public string SortedTxtFileName { get; set; } = "MultiStrategySymbolTimeframeScanResults_sorted.txt";
}
