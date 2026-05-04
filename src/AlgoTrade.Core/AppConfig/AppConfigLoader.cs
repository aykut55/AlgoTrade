using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlgoTrade.Core.AppConfig;

public static class AppConfigLoader
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented               = true,
        PropertyNameCaseInsensitive = true,
        Converters                  = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition      = JsonIgnoreCondition.Never,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true
    };

    /// <summary>
    /// AppConfig.json dosyasını okur. Dosya yoksa veya parse edilemezse
    /// default değerlerle dolu bir AppConfig döner.
    /// </summary>
    public static AppConfig Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[AppConfig] Dosya bulunamadi: {filePath}  → varsayilan ayarlar kullaniliyor.");
            return CreateDefault();
        }

        try
        {
            string json   = File.ReadAllText(filePath);
            var    config = JsonSerializer.Deserialize<AppConfig>(json, _options);
            return config ?? CreateDefault();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppConfig] Parse hatasi: {ex.Message}  → varsayilan ayarlar kullaniliyor.");
            return CreateDefault();
        }
    }

    /// <summary>
    /// Mevcut AppConfig nesnesini dosyaya yazar (düzenleme sonrası kayıt için).
    /// </summary>
    public static void Save(AppConfig config, string filePath)
    {
        string json = JsonSerializer.Serialize(config, _options);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Eğer AppConfig.json yoksa örnek bir tane oluşturur.
    /// Uygulama ilk çalıştırıldığında çağrılabilir.
    /// </summary>
    public static void CreateSampleIfNotExists(string filePath)
    {
        if (File.Exists(filePath)) return;
        Save(CreateDefault(), filePath);
        Console.WriteLine($"[AppConfig] Örnek dosya oluşturuldu: {filePath}");
    }

    // -------------------------------------------------------------------------
    // Default config
    // -------------------------------------------------------------------------

    private static AppConfig CreateDefault() => new()
    {
        AppSettings = new AppSettingsConfig
        {
            StockDataFile = ""
        },

        SingleTrader = new SingleTraderConfig
        {
            RunMode  = "TradeOnly",
            Strategy = new StrategyRef { Name = "SimpleMostStrategy", Version = "v1" },
            Query    = new QueryRef    { Name = "SimpleQuery1",        Version = "v1" },
            TradeParams = new TradeParamsConfig
            {
                MarketType     = "ViopEndex",
                IlkBakiye      = 100_000.0,
                KontratSayisi  = 1,
                KomisyonCarpan = 20.0,
                KaymaMiktari   = 0.5
            },
            Signals = new TraderSignalsConfig
            {
                AlEnabled              = true,
                SatEnabled             = true,
                FlatOlEnabled          = true,
                PasGecEnabled          = true,
                KarAlEnabled           = true,
                ZararKesEnabled        = true,
                GunSonuPozKapatEnabled = false,
                TimeFilteringEnabled      = false,
                StartDateTime             = "2025.05.25 09:35:00",
                StopDateTime              = "2025.06.02 17:55:00",
                TradeStartBarIndexEnabled = false,
                TradeStartBarIndex        = 0
            },
            Save = new TraderSaveConfig
            {
                SaveStatisticsToFile                = false,
                SaveFullStatsTxtEnabled             = true,
                SaveFullStatsCsvEnabled             = true,
                SaveMinimalStatsTxtEnabled          = true,
                SaveMinimalStatsCsvEnabled          = true,
                SaveFullListsTxtEnabled             = true,
                SaveFullListsCsvEnabled             = true,
                SaveMinimalListsTxtEnabled          = true,
                SaveMinimalListsCsvEnabled          = true,
                SaveFullStatsTxtFormattedEnabled    = true,
                SaveMinimalStatsTxtFormattedEnabled = true,
                SavePerformansTxtEnabled            = true,
                SavePerformansCsvEnabled            = true
            },
            Plot = new TraderPlotConfig
            {
                PlotEnabled = false
            },
            Optimization = new TraderOptimizationConfig
            {
                OptimizationEnabled = false
            },
        },

        MultipleTrader = new MultipleTraderConfig
        {
            RunMode    = "TradeOnly",
            MainTrader = new MainTraderConfig
            {
                TradeParams = new TradeParamsConfig { MarketType = "ViopEndex", IlkBakiye = 100_000.0, KontratSayisi = 1, KomisyonCarpan = 20.0, KaymaMiktari = 0.5 }
            },
            ChildTraders = new List<ChildTraderEntry>
            {
                new() { ChildId = 0, Strategy = new StrategyRef { Name = "SimpleMostStrategy", Version = "v1" },
                        Query = new QueryRef { Name = "SimpleQuery1", Version = "v1" } },
                new() { ChildId = 1, Strategy = new StrategyRef { Name = "SimpleMostStrategy", Version = "v2" },
                        Query = new QueryRef { Name = "SimpleQuery1", Version = "v1" } }
            }
        },

        SingleTraderOptimizer = new SingleTraderOptConfig
        {
            Optimization      = new OptRef      { Name = "SimpleMostStrategy", Version = "v1" },
            Strategy          = new StrategyRef { Name = "SimpleMostStrategy", Version = "v1" },
            Range             = new SingleTraderOptRangeConfig { OptimizationFrom = -1, OptimizationTo = -1 },
            TradeParams       = new TradeParamsConfig
            {
                MarketType     = "ViopEndex",
                IlkBakiye      = 100_000.0,
                KontratSayisi  = 1,
                KomisyonCarpan = 20.0,
                KaymaMiktari   = 0.5
            },
            EquityCurveFilter = null,
            Signals           = new TraderSignalsConfig(),
            Save              = new SingleTraderOptSaveConfig(),
            Sort              = new SingleTraderOptSortConfig(),
            SingleTrader      = new SingleTraderOptTraderConfig
            {
                Plot         = new TraderPlotConfig         { PlotEnabled         = false },
                Optimization = new TraderOptimizationConfig { OptimizationEnabled = true  },
                Save         = new TraderSaveConfig()
            }
        }
    };
}
