namespace AlgoTrade.Core.AppConfig;

// =============================================================================
// Root
// =============================================================================

public class AppConfig
{
    public AppSettingsConfig     AppSettings    { get; set; } = new();
    public SingleTraderConfig    SingleTrader   { get; set; } = new();
    public MultipleTraderConfig  MultipleTrader { get; set; } = new();
    public SingleTraderOptConfig SingleTraderOpt { get; set; } = new();
}

// =============================================================================
// AppSettings
// =============================================================================

public class AppSettingsConfig
{
    /// <summary>CSV veri dosyasının tam yolu.</summary>
    public string StockDataFile { get; set; } = "";
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
    public bool   TimeFilteringEnabled   { get; set; } = false;
    public string StartDateTime          { get; set; } = "2025.05.25 09:35:00";
    public string StopDateTime           { get; set; } = "2025.06.02 17:55:00";
}

/// <summary>SingleTrader kayıt ve optimizasyon bayrakları (OnApplyUserFlags2 karşılığı).</summary>
public class TraderSaveConfig
{
    public bool OptimizationEnabled                 { get; set; } = false;
    public bool SaveStatisticsToFile                { get; set; } = true;
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
    public TraderSignalsConfig Signals           { get; set; } = new();
    public TraderSaveConfig    Save              { get; set; } = new();
}

// =============================================================================
// MultipleTrader
// =============================================================================

/// <summary>
/// MultipleTrader içindeki tek bir child trader tanımı.
/// Her child kendi stratejisine ve trade parametrelerine sahiptir.
/// </summary>
public class ChildTraderEntry
{
    public int               ChildId           { get; set; }
    public StrategyRef       Strategy          { get; set; } = new();
    public QueryRef?         Query             { get; set; }
    public EcfRef?           EquityCurveFilter { get; set; }
    public TradeParamsConfig TradeParams       { get; set; } = new();
}

public class MultipleTraderConfig
{
    /// <summary>TradeOnly | TradeAndQuery | QueryOnly</summary>
    public string RunMode { get; set; } = "TradeOnly";

    /// <summary>
    /// Her child trader tanımı burada listelenir.
    /// Sıra önemlidir — ChildId alanı referans olarak kullanılır.
    /// </summary>
    public List<ChildTraderEntry> ChildTraders { get; set; } = new();
}

// =============================================================================
// SingleTraderOpt
// =============================================================================

public class SingleTraderOptConfig
{
    public StrategyRef       Strategy          { get; set; } = new();
    public OptRef            Optimization      { get; set; } = new();
    public EcfRef?           EquityCurveFilter { get; set; }
    public TradeParamsConfig TradeParams       { get; set; } = new();
}
