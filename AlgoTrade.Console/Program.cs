using AlgoTrade.Core;
using AlgoTrade.Core.AppConfig;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Logging.Sinks;
using AlgoTrade.Core.Scripting;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Timer;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Strategies;
using AlgoTrade.Core.Trading.Queries;
using AlgoTrade.Core.Trading.EquityCurve;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding  = Encoding.UTF8;

// =============================================================================
// Global State
// =============================================================================

var sb                                              = new StringBuilder();
ConsoleLogger?      consoleLogger                   = null;
List<StockData>?    stockDataList                   = null;
StockDataReader?    stockDataReader                 = null;
ConcurrentDictionary<string, string>? stockMetaData = null;
AlgoTrader?         algoTrader                      = null;
TimeManager         timer                           = TimeManager.GetInstance();
LogManager          logger                          = LogManager.GetInstance();
TraderRunMode       selectedRunMode                 = TraderRunMode.TradeOnly;
bool                addHeadTailInfo                 = false;

string              appConfigPath                   = Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");
AppConfig           appConfig                       = new();          // startup'ta AppConfigLoader.Load() ile doldurulur
string              stockDataFullFileName           = "";

// =============================================================================
// Callbacks
// =============================================================================

void OnReadMetaData(StockDataReader sender, ConcurrentDictionary<string, string> metaData)
{
    if (!sender.IsMetaDataRead) return;

    var meta         = sender.GetMetaData();
    int padding      = 18;
    sb.Clear();
    sb.AppendLine($"{"\tKayit Zamani".PadRight(padding)}: {meta.GetValueOrDefault("Kayit_Zamani",    "N/A")}");
    sb.AppendLine($"{"\tGrafikSembol".PadRight(padding)}: {meta.GetValueOrDefault("GrafikSembol",    "N/A")}");
    sb.AppendLine($"{"\tGrafikPeriyot".PadRight(padding)}: {meta.GetValueOrDefault("GrafikPeriyot",  "N/A")}");
    sb.AppendLine($"{"\tBarCount".PadRight(padding)}: {meta.GetValueOrDefault("BarCount",            "N/A")}");
    sb.AppendLine($"{"\tBaslangic Tarihi".PadRight(padding)}: {meta.GetValueOrDefault("Baslangic_Tarihi", "N/A")}");
    sb.AppendLine($"{"\tBitis Tarihi".PadRight(padding)}: {meta.GetValueOrDefault("Bitis_Tarihi",    "N/A")}");
    sb.Append(    $"{"\tFormat".PadRight(padding)}: {meta.GetValueOrDefault("Format",                "N/A")}");
    LogManager.LogRaw(sb.ToString());
}

void OnProgress(StockDataReader sender, int count, bool isCompleted)
{
    if (isCompleted)
    {
        consoleLogger!.Write($"\r\tRecord count     : {count}");
        consoleLogger.WriteLine("");
    }
    else
    {
        consoleLogger!.Write($"\r\tRecord no        : {count}");
    }
}

void OnReadData(StockDataReader sender, List<StockData> data, long elapsedMs) { }

void OnTraderProgress(int currentBar, int totalBars, double percentage) { }

// =============================================================================
// Menu Utilities
// =============================================================================

/// <summary>
/// ESC → null (exit signal) | ENTER → typed string (empty = default)
/// </summary>
string? ReadMenuInput()
{
    var buf = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Escape)  { Console.WriteLine(); return null; }
        if (key.Key == ConsoleKey.Enter)   { Console.WriteLine(); return buf.ToString().Trim(); }
        if (key.Key == ConsoleKey.Backspace && buf.Length > 0) { buf.Remove(buf.Length - 1, 1); Console.Write("\b \b"); }
        else if (!char.IsControl(key.KeyChar)) { buf.Append(key.KeyChar); Console.Write(key.KeyChar); }
    }
}

(string name, string version)? ShowConfigSelectionMenu(
    string configType,
    List<(string name, string version, string display)> configs,
    int timeoutSeconds = 10)
{
    if (configs.Count == 0)
    {
        LogManager.LogRaw($"\n{configType} config dosyasinda yapilandirma bulunamadi.");
        return null;
    }

    Console.WriteLine();
    Console.WriteLine($"{configType} Config Secimi:");
    for (int i = 0; i < configs.Count; i++)
        Console.WriteLine($"  [{i + 1}] {configs[i].name} | {configs[i].version} | {configs[i].display}");
    Console.WriteLine();

    string? input = null;
    for (int i = timeoutSeconds; i > 0; i--)
    {
        Console.Write($"\rSeciminiz (default: 1) ({i} sn): ");
        if (Console.KeyAvailable) { input = Console.ReadLine()?.Trim(); break; }
        Thread.Sleep(1000);
    }

    if (input == null)
    {
        Console.Write($"\rSeciminiz (default: 1) (0 sn): ");
        Console.WriteLine();
        Console.WriteLine("Zaman asimi - ilk config secildi.");
    }

    if (string.IsNullOrEmpty(input)) return (configs[0].name, configs[0].version);
    if (int.TryParse(input, out int sel) && sel >= 1 && sel <= configs.Count)
        return (configs[sel - 1].name, configs[sel - 1].version);

    Console.WriteLine("Gecersiz secim - ilk config secildi.");
    return (configs[0].name, configs[0].version);
}

List<(string name, string version)>? ShowMultiConfigSelectionMenu(
    string configType,
    List<(string name, string version, string display)> configs,
    int timeoutSeconds = 15)
{
    if (configs.Count == 0)
    {
        LogManager.LogRaw($"\n{configType} config dosyasinda yapilandirma bulunamadi.");
        return null;
    }

    Console.WriteLine();
    Console.WriteLine($"{configType} Config Secimi (virgul ile coklu secim, ornek: 1,3,5 | all=tumunu sec):");
    for (int i = 0; i < configs.Count; i++)
        Console.WriteLine($"  [{i + 1}] {configs[i].name} | {configs[i].version} | {configs[i].display}");
    Console.WriteLine();

    string? input = null;
    for (int i = timeoutSeconds; i > 0; i--)
    {
        Console.Write($"\rSeciminiz (default: all) ({i} sn): ");
        if (Console.KeyAvailable) { input = Console.ReadLine()?.Trim(); break; }
        Thread.Sleep(1000);
    }

    if (input == null)
    {
        Console.Write($"\rSeciminiz (default: all) (0 sn): ");
        Console.WriteLine();
        Console.WriteLine("Zaman asimi - tum config'ler secildi.");
    }

    if (string.IsNullOrEmpty(input) || input.Equals("all", StringComparison.OrdinalIgnoreCase))
        return configs.Select(c => (c.name, c.version)).ToList();

    var selections = new List<(string name, string version)>();
    foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (int.TryParse(part, out int sel) && sel >= 1 && sel <= configs.Count)
            selections.Add((configs[sel - 1].name, configs[sel - 1].version));
        else
            Console.WriteLine($"Gecersiz numara: {part} — atlanıyor.");
    }

    if (selections.Count == 0)
    {
        Console.WriteLine("Gecersiz secim - tum config'ler secildi.");
        return configs.Select(c => (c.name, c.version)).ToList();
    }

    return selections;
}

TraderRunMode showRunModeMenu(int timeoutSeconds = 10)
{
    Console.WriteLine();
    Console.WriteLine("Run Mode Secimi:");
    Console.WriteLine("  [1] TradeOnly");
    Console.WriteLine("  [2] TradeAndQuery");
    Console.WriteLine("  [3] QueryOnly");
    Console.WriteLine();

    string? input = null;
    for (int i = timeoutSeconds; i > 0; i--)
    {
        Console.Write($"\rSeciminiz (default: 1) ({i} sn): ");
        if (Console.KeyAvailable) { input = Console.ReadLine()?.Trim(); break; }
        Thread.Sleep(1000);
    }

    if (input == null)
    {
        Console.Write($"\rSeciminiz (default: 1) (0 sn): ");
        Console.WriteLine();
        Console.WriteLine("Zaman asimi - TradeOnly secildi.");
    }

    return (input ?? "1") switch
    {
        "2" => TraderRunMode.TradeAndQuery,
        "3" => TraderRunMode.QueryOnly,
        _   => TraderRunMode.TradeOnly
    };
}

// =============================================================================
// Helpers
// =============================================================================

TraderRunMode ParseRunMode(string s) => s.Trim().ToLowerInvariant() switch
{
    "tradeandquery" => TraderRunMode.TradeAndQuery,
    "queryonly"     => TraderRunMode.QueryOnly,
    _               => TraderRunMode.TradeOnly
};

void editAndReloadAppConfig()
{
    LogManager.LogRaw($"\n[AppConfig] Açılıyor: {appConfigPath}", ConsoleColor.Cyan);
    try
    {
        Process.Start(new ProcessStartInfo(appConfigPath) { UseShellExecute = true });
    }
    catch (Exception ex)
    {
        LogManager.LogRaw($"  Dosya açılamadı: {ex.Message}  (Manuel yol: {appConfigPath})", ConsoleColor.Red);
    }
    LogManager.LogRaw("Düzenlemeyi tamamlayın ve kaydedin, sonra ENTER'a basın...");
    Console.ReadLine();
    appConfig            = AppConfigLoader.Load(appConfigPath);
    stockDataFullFileName = AppConfigApplier.ApplyAppSettings(appConfig.AppSettings);
    LogManager.LogRaw("[AppConfig] Yeniden yüklendi.", ConsoleColor.Green);
}

void DeleteFilesInGivenDirectory(string directoryPath, bool includeSubdirectories = false)
{
    if (!Directory.Exists(directoryPath)) return;
    var option = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
    foreach (var file in Directory.GetFiles(directoryPath, "*.*", option))
    {
        try { File.Delete(file); } catch { }
    }
}

// =============================================================================
// Configure  (TODO: AppConfigApplier'a taşınacak)
// =============================================================================

void ConfigureStrategy(bool bShowConfigSelectionMenu = true)
{
    if (algoTrader is null) return;

    string configPath = Path.Combine(AppSettings.ConfigsDir, "StrategyConfig.txt");
    if (File.Exists(configPath))
    {
        var loader = new StrategyConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();
        var menuItems  = allConfigs.Select(c => (c.StrategyName, c.Version, c.GetParametersDisplayString())).ToList();

        (string name, string version)? selection = bShowConfigSelectionMenu
            ? ShowConfigSelectionMenu("Strategy", menuItems)
            : (menuItems.Count > 0 ? (menuItems[0].StrategyName, menuItems[0].Version) : ((string, string)?)null);

        if (selection is null) return;
        algoTrader.ConfigureStrategyFromConfig(configPath, selection.Value.name, selection.Value.version);
    }
    else
    {
        LogManager.LogRaw($"\nStrategy config file not found: {configPath}");
        algoTrader.ConfigureStrategy("SimpleMostStrategy", new Dictionary<string, object>
        {
            ["period"]  = 21,
            ["percent"] = 1.0,
            ["choice"]  = 0
        });
    }
}

void ConfigureQuery(bool bShowConfigSelectionMenu = true)
{
    if (algoTrader is null) return;

    string configPath = Path.Combine(AppSettings.ConfigsDir, "QueryConfig.txt");
    if (File.Exists(configPath))
    {
        var loader = new QueryConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();
        var menuItems  = allConfigs.Select(c => (c.QueryName, c.Version, c.GetParametersDisplayString())).ToList();

        (string name, string version)? selection = bShowConfigSelectionMenu
            ? ShowConfigSelectionMenu("Query", menuItems)
            : (menuItems.Count > 0 ? (menuItems[0].QueryName, menuItems[0].Version) : ((string, string)?)null);

        if (selection is null) return;
        algoTrader.ConfigureQueryFromConfig(configPath, selection.Value.name, selection.Value.version);
    }
    else
    {
        LogManager.LogRaw($"\nQuery config file not found: {configPath}");
        algoTrader.ConfigureQuery("SimpleMaQuery", new Dictionary<string, object>
        {
            ["ma8Period"]   = 8,
            ["ma200Period"] = 200,
            ["choice"]      = 0
        });
    }
}

void ConfigureEquityCurveFilter(bool bShowConfigSelectionMenu = true)
{
    if (algoTrader is null) return;

    string configPath = Path.Combine(AppSettings.ConfigsDir, "EquityCurveFilterConfig.txt");
    if (File.Exists(configPath))
    {
        var loader = new EquityCurveFilterConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();
        var menuItems  = allConfigs.Select(c => ("Filter", c.Version, c.GetDisplayString())).ToList();

        (string name, string version)? selection = bShowConfigSelectionMenu
            ? ShowConfigSelectionMenu("EquityCurveFilter", menuItems)
            : (menuItems.Count > 0 ? ("Filter", menuItems[0].Version) : ((string, string)?)null);

        if (selection is null) return;
        algoTrader.ConfigureEquityCurveFilterFromConfig(configPath, selection.Value.version);
    }
    else
    {
        LogManager.LogRaw($"\nEquityCurveFilter config file not found: {configPath}");
        algoTrader.AddEquityCurveFilterConfig(id: 0, enabled: false, thresholdTypeIsPercent: false, profitThreshold: 10.0, lossThreshold: 5.0, trigger: ConfirmationTrigger.Both);
    }
}

void ConfigureOptimization(bool bShowConfigSelectionMenu = true)
{
    if (algoTrader is null) return;

    string configPath = Path.Combine(AppSettings.ConfigsDir, "OptimizationConfig.txt");
    if (File.Exists(configPath))
    {
        var loader = new OptimizationConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();
        var menuItems  = allConfigs.Select(c => (c.StrategyName, c.Version, c.GetDisplayString())).ToList();

        (string name, string version)? selection = bShowConfigSelectionMenu
            ? ShowConfigSelectionMenu("Optimization", menuItems)
            : (menuItems.Count > 0 ? (menuItems[0].StrategyName, menuItems[0].Version) : ((string, string)?)null);

        if (selection is null) return;
        algoTrader.ConfigureOptimizationFromConfig(configPath, selection.Value.name, selection.Value.version);
    }
    else
    {
        LogManager.LogRaw($"\nOptimization config file not found: {configPath}");
        algoTrader.ClearOptimizationParameterRanges();
        algoTrader.AddOptimizationParameterRange("period",  5,   50,  1);
        algoTrader.AddOptimizationParameterRange("percent", 0.5, 3.0, 0.5);
    }
}

void ConfigureStrategies()
{
    if (algoTrader is null) return;

    string configPath = Path.Combine(AppSettings.ConfigsDir, "StrategyConfig.txt");
    if (File.Exists(configPath))
    {
        var loader = new StrategyConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();
        var menuItems  = allConfigs.Select(c => (c.StrategyName, c.Version, c.GetParametersDisplayString())).ToList();
        var selections = ShowMultiConfigSelectionMenu("Strategy", menuItems);
        if (selections is null) return;

        algoTrader.ClearStrategyConfigs();
        algoTrader.ConfigureStrategiesFromConfig(configPath, selections);

        LogManager.LogRaw($"\nStrategies loaded from config ({selections.Count} adet):");
        for (int i = 0; i < selections.Count; i++)
            LogManager.LogRaw($"  Id={i}: {selections[i].name} | {selections[i].version}");
    }
    else
    {
        LogManager.LogRaw($"\nStrategy config file not found: {configPath}");
        algoTrader.ClearStrategyConfigs();
        algoTrader.AddStrategyConfig(0, "SimpleMostStrategy", new Dictionary<string, object>
            { ["period"] = 21, ["percent"] = 1.0, ["choice"] = 0 });
        algoTrader.AddStrategyConfig(1, "SimpleMostStrategy", new Dictionary<string, object>
            { ["period"] = 14, ["percent"] = 0.5, ["choice"] = 0 });
        LogManager.LogRaw("\nStrategy config file not found, fallback configured.");
    }
}


void ConfigureQueries()
{
    if (algoTrader is null) return;

    string configPath = Path.Combine(AppSettings.ConfigsDir, "QueryConfig.txt");
    if (File.Exists(configPath))
    {
        var loader = new QueryConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();
        var menuItems  = allConfigs.Select(c => (c.QueryName, c.Version, c.GetParametersDisplayString())).ToList();
        var selections = ShowMultiConfigSelectionMenu("Query", menuItems);
        if (selections is null) return;

        algoTrader.ClearQueryConfigs();
        algoTrader.ConfigureQueriesFromConfig(configPath, selections);

        LogManager.LogRaw($"\nQueries loaded from config ({selections.Count} adet):");
        for (int i = 0; i < selections.Count; i++)
            LogManager.LogRaw($"  Id={i}: {selections[i].name} | {selections[i].version}");
    }
    else
    {
        LogManager.LogRaw($"\nQuery config file not found: {configPath}");
        algoTrader.ClearQueryConfigs();
        algoTrader.AddQueryConfig(0, "SimpleMaQuery", new Dictionary<string, object>
            { ["ma8Period"] = 8, ["ma200Period"] = 200, ["choice"] = 0 });
        algoTrader.AddQueryConfig(1, "SimpleMaQuery", new Dictionary<string, object>
            { ["ma8Period"] = 5, ["ma200Period"] = 100, ["choice"] = 0 });
    }
}

void ConfigureEquityCurveFilters()
{
    if (algoTrader is null) return;

    string configPath = Path.Combine(AppSettings.ConfigsDir, "EquityCurveFilterConfig.txt");
    if (File.Exists(configPath))
    {
        var loader = new EquityCurveFilterConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();
        var menuItems  = allConfigs.Select(c => ("Filter", c.Version, c.GetDisplayString())).ToList();
        var selections = ShowMultiConfigSelectionMenu("EquityCurveFilter", menuItems);
        if (selections is null) return;

        algoTrader.ClearEquityCurveFilterConfigs();
        for (int i = 0; i < selections.Count; i++)
            algoTrader.ConfigureEquityCurveFilterFromConfig(configPath, selections[i].version, i);
    }
    else
    {
        LogManager.LogRaw($"\nEquityCurveFilter config file not found: {configPath}");
        algoTrader.ClearEquityCurveFilterConfigs();
        algoTrader.AddEquityCurveFilterConfig(id: 0, enabled: false, thresholdTypeIsPercent: false, profitThreshold: 10.0, lossThreshold: 5.0, trigger: ConfirmationTrigger.Both);
        algoTrader.AddEquityCurveFilterConfig(id: 1, enabled: false, thresholdTypeIsPercent: false, profitThreshold: 10.0, lossThreshold: 5.0, trigger: ConfirmationTrigger.Both);
        algoTrader.AddEquityCurveFilterConfig(id: 2, enabled: false, thresholdTypeIsPercent: false, profitThreshold: 10.0, lossThreshold: 5.0, trigger: ConfirmationTrigger.Both);
    }
}

// =============================================================================
// Data
// =============================================================================

void readStockData()
{
    try
    {
        if (!File.Exists(stockDataFullFileName))
        {
            LogManager.LogRaw($"File does not exist : {stockDataFullFileName}");
            return;
        }

        stockDataReader = new StockDataReader();
        stockDataReader.OnReadMetaData += OnReadMetaData;
        stockDataReader.OnReadData     += OnReadData;
        stockDataReader.OnProgress     += OnProgress;

        string fileName = Path.GetFileName(stockDataFullFileName);
        string fileDir  = Path.GetDirectoryName(stockDataFullFileName)!;
        string filePath = Path.Combine(fileDir, fileName);

        LogManager.LogRaw("");
        LogManager.LogRaw($"Reading Meta Data from   : {filePath}");

        stockDataReader.Clear();
        stockDataReader.ReStartTimer();
        stockMetaData = stockDataReader.ReadMetaData(filePath);
        stockDataReader.StopTimer();

        long t1 = stockDataReader.GetElapsedTimeMsec();
        LogManager.DisableConsoleSink();
        {
            consoleLogger!.Write("is completed in ");
            consoleLogger.Write($"{t1}", ConsoleColor.Green);
            consoleLogger.WriteLine(" ms.");
            LogManager.EnableConsoleSink();
        }

        if (!stockDataReader.IsMetaDataRead) return;

        LogManager.LogRaw($"Loading data from        : {filePath}");

        stockDataReader.ReStartTimer();
        stockDataReader.ReadDataFast(filePath);
        stockDataReader.StopTimer();

        long t2 = stockDataReader.GetElapsedTimeMsec();
        LogManager.DisableConsoleSink();
        {
            consoleLogger!.Write("is completed in ");
            consoleLogger.Write($"{t2}", ConsoleColor.Green);
            consoleLogger.WriteLine(" ms.");
            LogManager.EnableConsoleSink();
        }

        stockDataList = stockDataReader.GetData();
        LogManager.LogRaw($"{"\n\tData count".PadRight(18)} : {stockDataReader.GetDataCount()}");

        if (addHeadTailInfo)
        {
            LogManager.LogRaw("");
            LogManager.LogRaw(stockDataReader.Head());
            LogManager.LogRaw("");
            LogManager.LogRaw(stockDataReader.Tail());
        }
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred while reading data: {ex.Message}", ex);
    }
}

// =============================================================================
// Runners
// =============================================================================

async Task runSingleTraderAlgoTrade()
{
    try
    {
        if (stockDataReader is null || !stockDataReader.IsDataReady) return;
        if (stockMetaData is null) return;

        LogManager.LogRaw("");
        LogManager.LogRaw("Running SingleTrader AlgoTrader");

        algoTrader = new AlgoTrader("AlgoTrader");
        algoTrader.OnTraderProgress += OnTraderProgress;
        algoTrader.RegisterLogger(logger);
        algoTrader.RegisterTimer(timer);
        algoTrader.Reset();
        algoTrader.SetData(stockDataReader.GetData());

        algoTrader.SymbolName   = stockMetaData.GetValueOrDefault("GrafikSembol",  "N/A");
        algoTrader.SymbolPeriod = stockMetaData.GetValueOrDefault("GrafikPeriyot", "N/A");
        algoTrader.SingleTraderRunMode = selectedRunMode;

        AppConfigApplier.ApplySingleTrader(algoTrader, appConfig.SingleTrader, AppSettings.ConfigsDir);

        algoTrader.Initialize();

        LogManager.LogRaw("");
        LogManager.LogRaw(algoTrader.GetDataInfo().ToString());

        await algoTrader.RunSingleTraderWithProgressAsync();

        var writeTask = algoTrader.WriteTraderDataToFilesAsync(algoTrader.SingleTrader);

        // TODO aa_001 : Kullanıcı menüden Query secmesine rağmen algoTrader.SingleTraderRunMode = TradeOnly kalmış
        // algoTrader.SingleTraderRunMode kullanıcının secimine göre update edilmesi lazım

        if (algoTrader.SingleTraderRunMode != TraderRunMode.QueryOnly)
        {
            LogManager.LogRaw("");

            if (algoTrader.SetupPython())
                await algoTrader.PlotSingleTraderData(algoTrader.SingleTrader);
            else
                LogManager.LogError("Python setup failed. PlotSingleTraderData skipped.");
        }

        await writeTask;
        LogManager.LogRaw("[WriteTraderDataToFilesAsync] File writing confirmed complete.");
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred in runSingleTraderAlgoTrade: {ex.Message}", ex);
    }
}

async Task runMultipleTraderAlgoTrade()
{
    try
    {
        if (stockDataReader is null || !stockDataReader.IsDataReady) return;

        LogManager.LogRaw("");
        LogManager.LogRaw("Running MultipleTrader AlgoTrader");

        algoTrader = new AlgoTrader("AlgoTrader");
        algoTrader.OnTraderProgress += OnTraderProgress;
        algoTrader.RegisterLogger(logger);
        algoTrader.RegisterTimer(timer);
        algoTrader.Reset();
        algoTrader.SetData(stockDataReader.GetData());

        if (stockMetaData != null)
        {
            algoTrader.SymbolName   = stockMetaData.GetValueOrDefault("GrafikSembol",  "N/A");
            algoTrader.SymbolPeriod = stockMetaData.GetValueOrDefault("GrafikPeriyot", "N/A");
        }

        algoTrader.SingleTraderRunMode = selectedRunMode;

        AppConfigApplier.ApplyMultipleTrader(algoTrader, appConfig.MultipleTrader, AppSettings.ConfigsDir);

        algoTrader.Initialize();

        LogManager.LogRaw("");
        LogManager.LogRaw(algoTrader.GetDataInfo().ToString());

        await algoTrader.RunMultipleTraderWithProgressAsync();

        algoTrader.MultipleTrader!.WriteChildTradersDataToFiles = false;
        var writeTask = algoTrader.WriteTraderDataToFilesAsync(algoTrader.MultipleTrader);
        await writeTask;

        LogManager.LogRaw(algoTrader.MultipleTrader.WriteChildTradersDataToFiles
            ? "[WriteTraderDataToFilesAsync] File writing confirmed complete. (mainTrader + childTraders)"
            : "[WriteTraderDataToFilesAsync] File writing confirmed complete. (mainTrader only)");
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred in runMultipleTraderAlgoTrade: {ex.Message}", ex);
    }
}

async Task runSingleTraderOptimization()
{
    try
    {
        if (stockDataReader is null || !stockDataReader.IsDataReady) return;

        LogManager.LogRaw("");
        LogManager.LogRaw("Running SingleTraderOptimization");

        algoTrader = new AlgoTrader("AlgoTrader");
        algoTrader.OnTraderProgress += OnTraderProgress;
        algoTrader.RegisterLogger(logger);
        algoTrader.RegisterTimer(timer);
        algoTrader.Reset();
        algoTrader.SetData(stockDataReader.GetData());

        if (stockMetaData != null)
        {
            algoTrader.SymbolName   = stockMetaData.GetValueOrDefault("GrafikSembol",  "N/A");
            algoTrader.SymbolPeriod = stockMetaData.GetValueOrDefault("GrafikPeriyot", "N/A");
        }

        AppConfigApplier.ApplySingleTraderOpt(algoTrader, appConfig.SingleTraderOpt, AppSettings.ConfigsDir);

        algoTrader.Initialize();

        LogManager.LogRaw("");
        LogManager.LogRaw(algoTrader.GetDataInfo().ToString());

        await algoTrader.RunSingleTraderOptWithProgressAsync();
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred in runSingleTraderOptimization: {ex.Message}", ex);
    }
}

// =============================================================================
// Mode Handlers  (Config özeti göster → [ENTER] çalıştır | [E] düzenle | [B] geri)
// =============================================================================

void showModeConfigSummary(string title)
{
    string file   = string.IsNullOrEmpty(stockDataFullFileName) ? "(tanımsız)" : Path.GetFileName(stockDataFullFileName);
    string dataOk = (stockDataReader?.IsDataReady == true)
        ? $"{stockDataReader.GetDataCount()} bar yüklü"
        : "Veri yüklenmedi";

    // Toplam kutu genişliği: 67 karakter (║ + 65 içerik + ║)
    // İçerik genişliği: 65 karakter
    string Trunc(string s, int max) => s.Length > max ? s[..max] : s;

    Console.WriteLine();
    Console.WriteLine("╔═════════════════════════════════════════════════════════════════╗");
    Console.WriteLine($"║  {title,-63}║");
    Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║  Veri       : {Trunc(file, 50),-50}║");
    Console.WriteLine($"║  Durum      : {Trunc(dataOk, 50),-50}║");

    if (title == "SingleTrader")
    {
        var cfg = appConfig.SingleTrader;
        var tp  = cfg.TradeParams;

        // Strateji
        string stratInfo = Trunc($"{cfg.Strategy.Name}  /  {cfg.Strategy.Version}", 50);
        Console.WriteLine($"║  Strateji   : {stratInfo,-50}║");

        // Query — null ise Disabled, dolu ise Enabled
        string queryLine   = cfg.Query != null
            ? Trunc($"{cfg.Query.Name}  /  {cfg.Query.Version}", 40)
            : "(tanımsız)";
        string queryStatus = cfg.Query != null ? "[Enabled]" : "[Disabled]";
        // "  Query      : " = 15, queryLine = 40, queryStatus right-aligned = 10  → 15+40+10=65 ✓
        Console.WriteLine($"║  Query      : {queryLine,-40}{queryStatus,10}║");

        // EquityCurveFilter — config dosyasından Enabled alanını okumaya çalış
        string ecfLine   = "(tanımsız)";
        string ecfStatus = "[Disabled]";
        if (cfg.EquityCurveFilter != null)
        {
            ecfLine = Trunc(cfg.EquityCurveFilter.Version, 40);
            try
            {
                string ecfPath = Path.Combine(AppSettings.ConfigsDir, cfg.EquityCurveFilter.ConfigFile);
                var ecfLoader  = new EquityCurveFilterConfigLoader(ecfPath);
                ecfLoader.LoadFromFile();
                var ecfCfg = ecfLoader.GetConfiguration(cfg.EquityCurveFilter.Version);
                if (ecfCfg != null)
                {
                    ecfLine   = Trunc($"{ecfCfg.Version}  ({ecfCfg.DisplayName})", 40);
                    ecfStatus = ecfCfg.Enabled ? "[Enabled]" : "[Disabled]";
                }
            }
            catch { }
        }
        Console.WriteLine($"║  ECFilter   : {ecfLine,-40}{ecfStatus,10}║");

        // TradeParams
        string tradeInfo = Trunc($"{tp.MarketType}  |  Bakiye:{tp.IlkBakiye:N0}  |  Kontrat:{tp.KontratSayisi}", 50);
        Console.WriteLine($"║  TradeParam : {tradeInfo,-50}║");

        // RunMode seçim bölümü
        Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");

        string cur = cfg.RunMode;
        // İçerik = 65 karakter: seçili satırda PadRight(57) + "◄ seçili"(8) = 65
        string RmLine(string key, string name, bool selected)
        {
            string left = $"  [{key}]  {name}";
            return selected
                ? $"║{left.PadRight(57)}◄ seçili║"
                : $"║{left.PadRight(65)}║";
        }

        Console.WriteLine(RmLine("1", "TradeOnly",     cur.Equals("TradeOnly",     StringComparison.OrdinalIgnoreCase)));
        Console.WriteLine(RmLine("2", "TradeAndQuery", cur.Equals("TradeAndQuery", StringComparison.OrdinalIgnoreCase)));
        Console.WriteLine(RmLine("3", "QueryOnly",     cur.Equals("QueryOnly",     StringComparison.OrdinalIgnoreCase)));

        Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  [1/2/3]  RunMode seçip çalıştır                               ║");
        Console.WriteLine("║  [ENTER]  Çalıştır  (AppConfig RunMode)                         ║");
        Console.WriteLine("║  [E]      AppConfig.json Düzenle + Yeniden Yükle                ║");
        Console.WriteLine("║  [B]      Ana Menüye Dön                                        ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════════╝");
        Console.Write("\nSeçiminiz: ");
        return;
    }

    // MultipleTrader / SingleTraderOpt — mevcut tasarım korunur
    string stratInfo2  = "";
    string runModeStr2 = "";
    string tradeInfo2  = "";

    if (title == "MultipleTrader")
    {
        var cfg    = appConfig.MultipleTrader;
        stratInfo2  = $"{cfg.ChildTraders.Count} child trader";
        runModeStr2 = cfg.RunMode;
        if (cfg.ChildTraders.Count > 0)
        {
            var tp = cfg.ChildTraders[0].TradeParams;
            tradeInfo2 = $"{tp.MarketType}  |  Bakiye:{tp.IlkBakiye:N0}  (child[0])";
        }
    }
    else if (title == "SingleTraderOpt")
    {
        var cfg    = appConfig.SingleTraderOpt;
        var tp     = cfg.TradeParams;
        stratInfo2  = $"{cfg.Strategy.Name}  /  {cfg.Strategy.Version}  |  Opt:{cfg.Optimization.Name}";
        runModeStr2 = "Optimization";
        tradeInfo2  = $"{tp.MarketType}  |  Bakiye:{tp.IlkBakiye:N0}  |  Kontrat:{tp.KontratSayisi}";
    }

    Console.WriteLine($"║  RunMode    : {Trunc(runModeStr2, 50),-50}║");
    Console.WriteLine($"║  Strateji   : {Trunc(stratInfo2, 50),-50}║");
    Console.WriteLine($"║  TradeParam : {Trunc(tradeInfo2, 50),-50}║");
    Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");
    Console.WriteLine("║  [ENTER]  Çalıştır                                              ║");
    Console.WriteLine("║  [E]      AppConfig.json Düzenle + Yeniden Yükle                ║");
    Console.WriteLine("║  [B]      Ana Menüye Dön                                        ║");
    Console.WriteLine("╚═════════════════════════════════════════════════════════════════╝");
    Console.Write("\nSeçiminiz: ");
}

async Task handleSingleTrader()
{
    showModeConfigSummary("SingleTrader");
    var input = ReadMenuInput();
    if (input == null || input.Equals("b", StringComparison.OrdinalIgnoreCase)) return;

    if (input.Equals("e", StringComparison.OrdinalIgnoreCase))
    {
        editAndReloadAppConfig();
        return;
    }

    selectedRunMode = ParseRunMode(appConfig.SingleTrader.RunMode);

    // TODO aa_001 : Kullanıcı menüden Query secmesine rağmen algoTrader.SingleTraderRunMode = TradeOnly kalmış
    // algoTrader.SingleTraderRunMode kullanıcının secimine göre update edilmesi lazım

    await runSingleTraderAlgoTrade();
}

async Task handleMultipleTrader()
{
    showModeConfigSummary("MultipleTrader");
    var input = ReadMenuInput();
    if (input == null || input.Equals("b", StringComparison.OrdinalIgnoreCase)) return;

    if (input.Equals("e", StringComparison.OrdinalIgnoreCase))
    {
        editAndReloadAppConfig();
        return;
    }

    selectedRunMode = ParseRunMode(appConfig.MultipleTrader.RunMode);
    await runMultipleTraderAlgoTrade();
}

async Task handleSingleTraderOpt()
{
    showModeConfigSummary("SingleTraderOpt");
    var input = ReadMenuInput();
    if (input == null || input.Equals("b", StringComparison.OrdinalIgnoreCase)) return;

    if (input.Equals("e", StringComparison.OrdinalIgnoreCase))
    {
        editAndReloadAppConfig();
        return;
    }

    await runSingleTraderOptimization();
}

// =============================================================================
// Script Support
// =============================================================================

ScriptExecutor scriptExecutor = new ScriptExecutor();
CancellationTokenSource? scriptCts = null;

(string code, string filePath) readScriptFromFile()
{
    string defaultDir = AppSettings.ScriptsDir;
    if (!Directory.Exists(defaultDir)) Directory.CreateDirectory(defaultDir);

    Console.Write($"\nScript dosya yolu (default: {defaultDir}\\): ");
    var filePath = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(filePath))
    {
        var files = Directory.GetFiles(defaultDir, "*.csx");
        if (files.Length == 0) { LogManager.LogRaw($"Dizinde script bulunamadi: {defaultDir}"); return ("", ""); }

        Console.WriteLine("\nMevcut scriptler:");
        for (int idx = 0; idx < files.Length; idx++)
            Console.WriteLine($"  [{idx + 1}] {Path.GetFileName(files[idx])}");

        Console.Write("\nSeçiminiz: ");
        var choice = Console.ReadLine()?.Trim();
        if (int.TryParse(choice, out int sel) && sel >= 1 && sel <= files.Length)
            filePath = files[sel - 1];
        else
            return ("", "");
    }

    if (!File.Exists(filePath)) { LogManager.LogRaw($"Dosya bulunamadi: {filePath}"); return ("", ""); }
    return (File.ReadAllText(filePath), filePath);
}

async Task<ScriptExecutionResult> executeScriptWithCancellation(string code, ScriptGlobals globals, string? sourceDirectory = null)
{
    scriptCts = new CancellationTokenSource();
    var scriptTask = scriptExecutor.ExecuteAsync(code, globals, scriptCts.Token, sourceDirectory);

    LogManager.LogRaw("\n[INFO] Script calisiyor... (ESC ile durdurabilirsiniz)\n", ConsoleColor.Cyan);

    while (!scriptTask.IsCompleted)
    {
        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
        {
            LogManager.LogRaw("\n[ESC] Script durdurma istegi gonderildi...", ConsoleColor.Yellow);
            scriptCts.Cancel();
        }
        await Task.Delay(100);
    }
    return await scriptTask;
}

void printScriptResult(ScriptExecutionResult result)
{
    if (result.Success)
    {
        LogManager.LogRaw($"[OK] Script basariyla tamamlandi ({result.ExecutionTime.TotalMilliseconds:F0} ms)", ConsoleColor.Green);
        if (result.ReturnValue != null)
            LogManager.LogRaw($"[RETURN] {result.ReturnValue}", ConsoleColor.Cyan);
    }
    else
    {
        if (result.CompilationErrors?.Count > 0)
        {
            LogManager.LogRaw("[HATA] Derleme hatalari:", ConsoleColor.Red);
            foreach (var err in result.CompilationErrors)
                LogManager.LogRaw($"  {err}", ConsoleColor.Red);
        }
        else
        {
            LogManager.LogRaw($"[HATA] {result.Error}", ConsoleColor.Red);
            if (!string.IsNullOrEmpty(result.StackTrace))
                LogManager.LogRaw($"[STACK] {result.StackTrace}", ConsoleColor.DarkYellow);
        }
    }
}

async Task runFullScript()
{
    var (code, filePath) = readScriptFromFile();
    if (string.IsNullOrEmpty(code)) return;

    var sourceDir = Path.GetDirectoryName(filePath);
    var globals = new ScriptGlobals(
        algoTrader!,
        stockDataList ?? new List<StockData>(),
        (msg) => LogManager.LogRaw(msg),
        (key, val) => LogManager.LogRaw($"[RESULT] {key}: {val}"));

    var result = await executeScriptWithCancellation(code, globals, sourceDir);
    printScriptResult(result);
}

async Task runInteractiveScript()
{
    if (algoTrader is null)
    {
        LogManager.LogRaw("\n[UYARI] AlgoTrader henuz olusturulmadi. Once [2] veya [5] calistirin.", ConsoleColor.Yellow);
        return;
    }

    if (stockDataList != null && algoTrader.GetDataCount() == 0)
    {
        algoTrader.SetData(stockDataList);
        LogManager.LogRaw($"[INFO] Mevcut stockData ({stockDataList.Count} bar) AlgoTrader'a atandi.");
    }

    Console.WriteLine("\nScript kodunu yapistirin (bos satir + ENTER ile bitirin):");
    Console.WriteLine("─────────────────────────────────────────────────────────");
    var lines = new List<string>();
    string? line;
    while ((line = Console.ReadLine()) != null && line.Length > 0)
        lines.Add(line);

    string code = string.Join("\n", lines);
    if (string.IsNullOrWhiteSpace(code)) return;

    var globals = new ScriptGlobals(
        algoTrader!,
        stockDataList ?? new List<StockData>(),
        (msg) => LogManager.LogRaw(msg),
        (key, val) => LogManager.LogRaw($"[RESULT] {key}: {val}"));

    var result = await executeScriptWithCancellation(code, globals);
    printScriptResult(result);
}

// =============================================================================
// Main Menu
// =============================================================================

void showMainMenu()
{
    Console.WriteLine();
    Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║        AlgoTrade — Ana Menü                                        ║");
    Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [1]  Veri Oku                                                   ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("╠═══ Trader ═════════════════════════════════════════════════════════╣");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [2]  SingleTrader                                               ║");
    Console.WriteLine("║    [3]  MultipleTrader                                             ║");
    Console.WriteLine("║    [4]  SingleTraderOpt                                            ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("╠═══ Veri Oku + Çalıştır ════════════════════════════════════════════╣");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [5]  Veri Oku + SingleTrader                                    ║");
    Console.WriteLine("║    [6]  Veri Oku + MultipleTrader                                  ║");
    Console.WriteLine("║    [7]  Veri Oku + SingleTraderOpt                                 ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("╠═══ Script ═════════════════════════════════════════════════════════╣");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [8]  Script Çalıştır                                            ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [0]  Çıkış                                                      ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");
    Console.Write("\nSeçiminiz (default: 5): ");
}

// =============================================================================
// Main
// =============================================================================

async Task main()
{
    AppSettings.EnsureDirectories();

    logger.RegisterSink(new ConsoleSink());
    logger.RegisterSink(new DebugSink());
    logger.RegisterSink(new FileSink(AppSettings.LogsDir, "app.log"));
    consoleLogger = LogManager.GetConsoleLogger();
    try { consoleLogger.Clear(); } catch { }

    LogManager.LogRaw("Application started", ConsoleColor.Green);
    DeleteFilesInGivenDirectory(AppSettings.LogsDir);
    LogManager.LogRaw($"{AppSettings.LogsDir} cleared...");

    // AppConfig.json yükle
    Directory.CreateDirectory(Path.GetDirectoryName(appConfigPath)!);
    AppConfigLoader.CreateSampleIfNotExists(appConfigPath);

    appConfig             = AppConfigLoader.Load(appConfigPath);
    stockDataFullFileName = AppConfigApplier.ApplyAppSettings(appConfig.AppSettings);
    LogManager.LogRaw($"[AppConfig] Yüklendi: {appConfigPath}");
    if (!string.IsNullOrEmpty(stockDataFullFileName))
        LogManager.LogRaw($"[AppConfig] StockDataFile: {stockDataFullFileName}");

    bool running = true;
    while (running)
    {
        showMainMenu();
        var input = ReadMenuInput();
        if (input == null) { running = false; break; }
        if (string.IsNullOrEmpty(input)) input = "5";

        switch (input)
        {
            case "1": readStockData();                                      break;
            case "2": await handleSingleTrader();                           break;
            case "3": await handleMultipleTrader();                         break;
            case "4": await handleSingleTraderOpt();                        break;
            case "5": readStockData(); await handleSingleTrader();          break;
            case "6": readStockData(); await handleMultipleTrader();        break;
            case "7": readStockData(); await handleSingleTraderOpt();       break;
            case "8": await runFullScript();                                break;
            case "0": running = false;                                      break;
            default:  Console.WriteLine("Geçersiz seçim.");                 break;
        }
    }

    LogManager.LogRaw("Application finished", ConsoleColor.Green);

    algoTrader?.Dispose();
    stockDataReader?.Dispose();
    LogManager.Instance.Dispose();

    algoTrader      = null;
    stockDataReader = null;
    stockDataList   = null;
    stockMetaData   = null;
}

try
{
    await main();
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL ERROR: {ex}");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}


/*
 * 
 * 
 singleTrader için OnApplyUserFlags  içinde set edilen seyleri AppConfig'e almak!
singleTrader için OnApplyUserFlags2 içinde set edilen seyleri AppConfig'e almak!

AnaMenüde 5 ile SingleTrader menüsüne gelince orada seçimler yaptırıyor
	[1/2/3]  RunMode seçilince hangisi secildiyse onun içeriğini ekrana yazdırmak
        singleTrader için OnApplyUserFlags  içinde set edilen seyleri ekranda göstermek
        singleTrader için OnApplyUserFlags2  içinde set edilen seyleri
Son adımda ENTER ile çalıştırmak

 * AppConfig'e readStockData nın parametrelerini girmek ve kullancııdan onay istemek.
 * Defaultları gösterir : ENTER ise okuma baslar, değilse kullanıcıdan parametre girişi istenir..
 * 
 */