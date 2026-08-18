using AlgoTrade.Core;
using AlgoTrade.Core.AppConfig;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Logging.Sinks;
using AlgoTrade.Core.Python;
using AlgoTrade.Core.Python.DearPyGuiDataPlotter;
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
using System.Text.Json;
using System.Text.Json.Serialization;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding  = Encoding.UTF8;

// Sayı formatı: ondalık ayraç '.', grup ayraç ','. Tarih/diğer culture ayarları korunur.
{
    var ci = new System.Globalization.CultureInfo(System.Globalization.CultureInfo.CurrentCulture.Name);
    ci.NumberFormat.NumberDecimalSeparator   = ".";
    ci.NumberFormat.NumberGroupSeparator     = ",";
    ci.NumberFormat.PercentDecimalSeparator  = ".";
    ci.NumberFormat.PercentGroupSeparator    = ",";
    ci.NumberFormat.CurrencyDecimalSeparator = ".";
    ci.NumberFormat.CurrencyGroupSeparator   = ",";
    System.Globalization.CultureInfo.DefaultThreadCurrentCulture = ci;
    System.Threading.Thread.CurrentThread.CurrentCulture          = ci;
}

// =============================================================================
// Global State
// =============================================================================

var sb                                              = new StringBuilder();
ConsoleLogger?      consoleLogger                   = null;
List<StockData>?    stockDataList                   = null;
StockDataReader?    stockDataReader                 = null;
ConcurrentDictionary<string, string>? stockMetaData = null;
AlgoTrader?         algoTrader                      = null;
DearPyGuiDataPlotter? dearPyGuiTestPlotter           = null; // TODO: demo/test - bkz. docs/yapilacak.md
TimeManager         timer                           = TimeManager.GetInstance();
LogManager          logger                          = LogManager.GetInstance();
TraderRunMode       selectedRunMode                 = TraderRunMode.TradeOnly;
bool                addHeadTailInfo                 = false;

string              appConfigPath                   = Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");
AppConfig           appConfig                       = new();          // populated with AppConfigLoader.Load() at startup
string              stockDataFullFileName           = "";

bool exitRequested = false;

// =============================================================================
// Callbacks
// =============================================================================

void OnReadMetaData(StockDataReader sender, ConcurrentDictionary<string, string> metaData)
{
    if (!sender.IsMetaDataRead) return;

    var meta         = sender.GetMetaData();
    int padding      = 18;
    sb.Clear();
    sb.AppendLine($"{"\tRecord Time".PadRight(padding)}: {meta.GetValueOrDefault("Kayit_Zamani",    "N/A")}");
    sb.AppendLine($"{"\tChart Symbol".PadRight(padding)}: {meta.GetValueOrDefault("GrafikSembol",    "N/A")}");
    sb.AppendLine($"{"\tChart Period".PadRight(padding)}: {meta.GetValueOrDefault("GrafikPeriyot",  "N/A")}");
    sb.AppendLine($"{"\tBar Count".PadRight(padding)}: {meta.GetValueOrDefault("BarCount",            "N/A")}");
    sb.AppendLine($"{"\tStart Date".PadRight(padding)}: {meta.GetValueOrDefault("Baslangic_Tarihi", "N/A")}");
    sb.AppendLine($"{"\tEnd Date".PadRight(padding)}: {meta.GetValueOrDefault("Bitis_Tarihi",    "N/A")}");
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
/// Calls ReadMenuInputWithTimeout or ReadMenuInput automatically
/// based on AppConfig.AppSettings.MenuTimeoutSeconds.
/// </summary>
string? MenuInput(string defaultReturn = "")
{
    int t = appConfig.AppSettings.MenuTimeoutSeconds;
    return t > 0
        ? ReadMenuInputWithTimeout(t, defaultReturn)
        : ReadMenuInput();
}

/// <summary>
/// ESC → null (exit signal) | ENTER → typed string (empty = default)
/// </summary>
string? ReadMenuInput()
{
    Console.Write("Selection: ");
    var buf = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Escape)  { Console.WriteLine(); return null; }
        if (key.Key == ConsoleKey.Enter)   { Console.WriteLine(); return buf.ToString().Trim(); }
        if (key.Key == ConsoleKey.Backspace && buf.Length > 0) { buf.Remove(buf.Length - 1, 1); Console.Write("\b \b"); }
        else if (key.Key == ConsoleKey.Backspace)              { Console.WriteLine(); return "b"; }
        else if (!char.IsControl(key.KeyChar)) { buf.Append(key.KeyChar); Console.Write(key.KeyChar); }
    }
}

/// <summary>
/// Same behavior as ReadMenuInput, but returns defaultReturn after timeoutSeconds.
/// The countdown is updated on the same line as "\rSelection (XX s): ".
/// When the user presses a key, the countdown stops and normal input mode begins.
/// </summary>
string? ReadMenuInputWithTimeout(int timeoutSeconds, string? defaultReturn = "")
{
    var buf             = new StringBuilder();
    var deadline        = DateTime.Now.AddSeconds(timeoutSeconds);
    int lastShown       = -1;
    bool userStarted    = false;
    bool paused         = false;
    int pausedRemaining = 0;
    int lastPromptLen   = 0;

    void RewritePrompt(string prompt)
    {
        int clearLen = Math.Max(lastPromptLen, prompt.Length);
        Console.Write("\r" + new string(' ', clearLen));
        Console.Write("\r" + prompt);
        lastPromptLen = prompt.Length;
    }

    while (true)
    {
        int remaining = paused
            ? pausedRemaining
            : Math.Max(0, (int)(deadline - DateTime.Now).TotalSeconds);

        // Update countdown (only if the user has not started typing yet)
        if (!userStarted && !paused && remaining != lastShown)
        {
            RewritePrompt($"Selection ({remaining:D2} s): ");
            lastShown = remaining;
        }

        if (!paused && remaining == 0 && !userStarted)
        {
            Console.WriteLine();
            return defaultReturn;
        }

        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Escape) { Console.WriteLine(); return null; }
            if (key.Key == ConsoleKey.Enter)  { Console.WriteLine(); return buf.ToString().Trim(); }

            if (key.Key == ConsoleKey.Backspace && buf.Length > 0) { buf.Remove(buf.Length - 1, 1); Console.Write("\b \b"); }
            else if (key.Key == ConsoleKey.Backspace) { Console.WriteLine(); return "b"; }
            else if (!userStarted && (key.KeyChar == 't' || key.KeyChar == 'T'))
            {
                // Toggle pause / resume
                if (!paused)
                {
                    paused          = true;
                    pausedRemaining = remaining;
                    RewritePrompt($"Selection (PAUSED {pausedRemaining:D2} s): ");
                    lastShown = -1;
                }
                else
                {
                    paused   = false;
                    deadline = DateTime.Now.AddSeconds(pausedRemaining);
                    lastShown = -1;
                }
                continue; // deadline reset'i atla
            }
            else if (!char.IsControl(key.KeyChar))
            {
                if (!userStarted)
                {
                    userStarted = true;
                }
                buf.Append(key.KeyChar);
                Console.Write(key.KeyChar);
            }
            // Reset timeout on each key press
            deadline = DateTime.Now.AddSeconds(timeoutSeconds);
        }
        else
        {
            Thread.Sleep(100);
        }
    }
}

(string name, string version)? ShowConfigSelectionMenu(
    string configType,
    List<(string name, string version, string display)> configs,
    int timeoutSeconds = 10)
{
    if (configs.Count == 0)
    {
        LogManager.LogRaw($"\nNo configuration found in {configType} config file.");
        return null;
    }

    Console.WriteLine();
    Console.WriteLine($"{configType} Config Selection:");
    for (int i = 0; i < configs.Count; i++)
        Console.WriteLine($"  [{i + 1}] {configs[i].name} | {configs[i].version} | {configs[i].display}");
    Console.WriteLine();

    string? input = null;
    for (int i = timeoutSeconds; i > 0; i--)
    {
        Console.Write($"\rSelection (default: 1) ({i} s): ");
        if (Console.KeyAvailable) { input = Console.ReadLine()?.Trim(); break; }
        Thread.Sleep(1000);
    }

    if (input == null)
    {
        Console.Write($"\rSelection (default: 1) (0 s): ");
        Console.WriteLine();
        Console.WriteLine("Timeout - first config selected.");
    }

    if (string.IsNullOrEmpty(input)) return (configs[0].name, configs[0].version);
    if (int.TryParse(input, out int sel) && sel >= 1 && sel <= configs.Count)
        return (configs[sel - 1].name, configs[sel - 1].version);

    Console.WriteLine("Invalid selection - first config selected.");
    return (configs[0].name, configs[0].version);
}

List<(string name, string version)>? ShowMultiConfigSelectionMenu(
    string configType,
    List<(string name, string version, string display)> configs,
    int timeoutSeconds = 15)
{
    if (configs.Count == 0)
    {
        LogManager.LogRaw($"\nNo configuration found in {configType} config file.");
        return null;
    }

    Console.WriteLine();
    Console.WriteLine($"{configType} Config Selection (comma-separated multi-select, e.g.: 1,3,5 | all=select all):");
    for (int i = 0; i < configs.Count; i++)
        Console.WriteLine($"  [{i + 1}] {configs[i].name} | {configs[i].version} | {configs[i].display}");
    Console.WriteLine();

    string? input = null;
    for (int i = timeoutSeconds; i > 0; i--)
    {
        Console.Write($"\rSelection (default: all) ({i} s): ");
        if (Console.KeyAvailable) { input = Console.ReadLine()?.Trim(); break; }
        Thread.Sleep(1000);
    }

    if (input == null)
    {
        Console.Write($"\rSelection (default: all) (0 s): ");
        Console.WriteLine();
        Console.WriteLine("Timeout - all configs selected.");
    }

    if (string.IsNullOrEmpty(input) || input.Equals("all", StringComparison.OrdinalIgnoreCase))
        return configs.Select(c => (c.name, c.version)).ToList();

    var selections = new List<(string name, string version)>();
    foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (int.TryParse(part, out int sel) && sel >= 1 && sel <= configs.Count)
            selections.Add((configs[sel - 1].name, configs[sel - 1].version));
        else
            Console.WriteLine($"Invalid number: {part} — skipping.");
    }

    if (selections.Count == 0)
    {
        Console.WriteLine("Invalid selection - all configs selected.");
        return configs.Select(c => (c.name, c.version)).ToList();
    }

    return selections;
}

TraderRunMode showRunModeMenu(int timeoutSeconds = 10)
{
    Console.WriteLine();
    Console.WriteLine("Run Mode Selection:");
    Console.WriteLine("  [1] TradeOnly");
    Console.WriteLine("  [2] TradeAndQuery");
    Console.WriteLine("  [3] QueryOnly");
    Console.WriteLine();

    string? input = null;
    for (int i = timeoutSeconds; i > 0; i--)
    {
        Console.Write($"\rSelection (default: 1) ({i} s): ");
        if (Console.KeyAvailable) { input = Console.ReadLine()?.Trim(); break; }
        Thread.Sleep(1000);
    }

    if (input == null)
    {
        Console.Write($"\rSelection (default: 1) (0 s): ");
        Console.WriteLine();
        Console.WriteLine("Timeout - TradeOnly selected.");
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
    LogManager.LogRaw($"\n[AppConfig] Opening: {appConfigPath}", ConsoleColor.Cyan);
    try
    {
        Process.Start(new ProcessStartInfo(appConfigPath) { UseShellExecute = true });
    }
    catch (Exception ex)
    {
        LogManager.LogRaw($"  File could not be opened: {ex.Message}  (Manual path: {appConfigPath})", ConsoleColor.Red);
    }
    LogManager.LogRaw("Complete editing and save, then press ENTER...");
    Console.ReadLine();
    appConfig            = AppConfigLoader.Load(appConfigPath);
    stockDataFullFileName = AppConfigApplier.ApplyAppSettings(appConfig.AppSettings);
    LogManager.LogRaw("");
    LogManager.LogRaw("[AppConfig] Reloaded.", ConsoleColor.Green);
}

void reloadAppConfig()
{
    appConfig            = AppConfigLoader.Load(appConfigPath);
    stockDataFullFileName = AppConfigApplier.ApplyAppSettings(appConfig.AppSettings);
    LogManager.LogRaw("");
    LogManager.LogRaw("[AppConfig] Reloaded.", ConsoleColor.Green);
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
// Configure  (TODO: to be moved to AppConfigApplier)
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

        LogManager.LogRaw($"\nStrategies loaded from config ({selections.Count} items):");
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

void readStockData(ReadDataConfig? cfg = null)
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

        // ReadDataFast parametrelerini config'den çöz
        var    filterMode = StockDataReader.FilterMode.All;
        int    n1 = 0, n2 = 0;
        DateTime? dt1 = null, dt2 = null;

        if (cfg != null)
        {
            Enum.TryParse<StockDataReader.FilterMode>(cfg.FilterMode, ignoreCase: true, out filterMode);
            n1 = cfg.N1;
            n2 = cfg.N2;
            if (!string.IsNullOrWhiteSpace(cfg.Dt1)) dt1 = DateTime.Parse(cfg.Dt1);
            if (!string.IsNullOrWhiteSpace(cfg.Dt2)) dt2 = DateTime.Parse(cfg.Dt2);
        }

        LogManager.LogRaw($"Loading data from        : {filePath}");

        stockDataReader.ReStartTimer();
        stockDataReader.ReadDataFast(filePath, filterMode, n1, n2, dt1, dt2);
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

void showReadDataPreview()
{
    var cfg = appConfig.ReadData;

    var jsonOpts = new JsonSerializerOptions
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters             = { new JsonStringEnumConverter() }
    };

    var preview = new
    {
        StockDataFile = string.IsNullOrEmpty(stockDataFullFileName) ? "(undefined)" : stockDataFullFileName,
        cfg.FilterMode,
        cfg.N1,
        cfg.N2,
        cfg.Dt1,
        cfg.Dt2
    };

    string json = JsonSerializer.Serialize(preview, jsonOpts);
    string sep  = new string('═', 66);
    Console.WriteLine();
    Console.WriteLine("══ Data Read Preview ══════════════════════════════════════════════");
    Console.WriteLine(json);
    Console.WriteLine(sep);
    Console.WriteLine("  [ENTER]  Start Reading");
    Console.WriteLine("  [E]      Edit AppConfig.json + Reload");
    Console.WriteLine("  [R]      Reload AppConfig");
    Console.WriteLine("  [T]      Pause/Resume Timer");
    Console.WriteLine("  [B]      Back");
    Console.WriteLine();
}

bool handleReadData()
{
    while (true)
    {
        showReadDataPreview();
        var input = MenuInput("");

        if (input == null || input.Equals("b", StringComparison.OrdinalIgnoreCase)) return false;

        if (input.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            editAndReloadAppConfig();
            continue;
        }

        if (input.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            reloadAppConfig();
            continue;
        }

        // ENTER → oku
        readStockData(appConfig.ReadData);
        return true;
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
        // Önce AppConfig'i uygula (RunMode dahil baseline), sonra kullanıcı seçimini override et
        AppConfigApplier.ApplySingleTrader(algoTrader, appConfig.SingleTrader, AppSettings.ConfigsDir);
        algoTrader.SingleTraderRunMode = selectedRunMode;

        algoTrader.Initialize();

        LogManager.LogRaw("");
        LogManager.LogRaw(algoTrader.GetDataInfo().ToString());

        await algoTrader.RunSingleTraderWithProgressAsync();

        var writeTask = algoTrader.WriteTraderDataToFilesAsync(algoTrader.SingleTrader);

        bool plotEnabled = algoTrader.SingleTrader.PlotEnabled;
        if (algoTrader.SingleTraderRunMode != TraderRunMode.QueryOnly && plotEnabled)
        {
            LogManager.LogRaw("");

            if (algoTrader.SetupPython())
                await algoTrader.PlotSingleTraderData(algoTrader.SingleTrader);
            else
                LogManager.LogError("Python setup failed. PlotSingleTraderData skipped.");

            // TODO: DearPyGuiDataPlotter converter/switch TESTİ - bkz. docs/yapilacak.md.
            // Gerçek PlotBackend switch'i entegre olunca bu blok kaldırılıp yukarıdaki
            // pythonnet/imgui_bundle çağrısıyla aynı yerde düzgünce (switch ile) sarılacak.
            // Şimdilik pythonnet akışına dokunmadan, AYNI SingleTrader'dan npz bundle
            // üretip DearPyGuiDataPlotter'da da açıyor (converter'ı gerçek veriyle test etmek için).
            try
            {
                var bundleConverter = new TradeDataBundleConverter();
                string bundleOutDir = Path.Combine(AppSettings.DearPyGuiDataPlotterDir, "inputs");
                var (bundlePath, viewPath) = bundleConverter.ConvertSingleTrader(
                    algoTrader.SingleTrader, bundleOutDir);

                dearPyGuiTestPlotter ??= new DearPyGuiDataPlotter();
                dearPyGuiTestPlotter.SetLogger(logger);
                dearPyGuiTestPlotter.StartPlotter();
                dearPyGuiTestPlotter.LoadBundle(bundlePath, viewPath);
                LogManager.LogRaw($"[DearPyGuiDataPlotter] Gerçek SingleTrader datası yüklendi: {bundlePath}", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[DearPyGuiDataPlotter] Converter test hatası: {ex.Message}", ex);
            }
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

        var writeTask = algoTrader.WriteTraderDataToFilesAsync(algoTrader.MultipleTrader!);

        var mainTrader = algoTrader.MultipleTrader.GetMainTrader();
        bool plotEnabled = mainTrader.PlotEnabled;
        if (algoTrader.SingleTraderRunMode != TraderRunMode.QueryOnly && plotEnabled)
        {
            LogManager.LogRaw("");

            if (algoTrader.SetupPython())
                await algoTrader.PlotMultipleTraderData(algoTrader.MultipleTrader);
            else
                LogManager.LogError("Python setup failed. PlotMultipleTraderData skipped.");
        }

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

        AppConfigApplier.ApplySingleTraderOpt(algoTrader, appConfig.SingleTraderOptimizer, AppSettings.ConfigsDir);

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

async Task runSymbolScan()
{
    try
    {
        LogManager.LogRaw("");
        LogManager.LogRaw("Running SymbolScan (Tarama)");

        var cfg     = appConfig.SymbolScan;
        var options = AppConfigApplier.BuildSymbolScanOptions(cfg, AppSettings.ConfigsDir);

        using var scanner = new SymbolScanner(logger);
        scanner.OnProgress = (current, total, symbol) =>
        {
            consoleLogger!.Write($"\r\t[{current}/{total}] {symbol}".PadRight(60));
        };

        string csvPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.CsvFileName);
        string txtPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.TxtFileName);
        string sortedCsvPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedCsvFileName);
        string sortedTxtPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedTxtFileName);

        await Task.Run(() => scanner.Run(options, csvPath, txtPath));
        Console.WriteLine();

        scanner.WriteSortedResults(options, sortedCsvPath, sortedTxtPath);

        int successCount = scanner.Results.Count(r => r.Success);
        int failCount    = scanner.Results.Count - successCount;

        LogManager.LogRaw("");
        LogManager.LogRaw($"=== Tarama tamamlandı: {scanner.Results.Count} sembol ({successCount} başarılı, {failCount} hata) ===", ConsoleColor.Green);

        foreach (var r in scanner.Results)
        {
            if (r.Success)
                LogManager.LogRaw($"  {r.Symbol,-20} {r.TaramaOzeti}");
            else
                LogManager.LogRaw($"  {r.Symbol,-20} HATA: {r.ErrorMessage}", ConsoleColor.Red);
        }

        var best = scanner.GetBestResult(options);
        if (best != null)
        {
            LogManager.LogRaw("");
            LogManager.LogRaw($"En iyi ({cfg.Sort.SortField}): {best.Symbol}  ->  {best.TaramaOzeti}", ConsoleColor.Yellow);
        }

        LogManager.LogRaw("");
        LogManager.LogRaw($"Sonuçlar     : {csvPath}");
        LogManager.LogRaw($"Sıralı sonuç : {sortedCsvPath}");
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred in runSymbolScan: {ex.Message}", ex);
    }
}

async Task runTimeframeScan()
{
    try
    {
        LogManager.LogRaw("");
        LogManager.LogRaw("Running TimeframeScan (Tarama)");

        var cfg     = appConfig.TimeframeScan;
        var options = AppConfigApplier.BuildTimeframeScanOptions(cfg, AppSettings.ConfigsDir);

        using var scanner = new TimeframeScanner(logger);
        scanner.OnProgress = (current, total, tf) =>
        {
            consoleLogger!.Write($"\r\t[{current}/{total}] {tf}".PadRight(60));
        };

        string csvPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.CsvFileName);
        string txtPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.TxtFileName);
        string sortedCsvPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedCsvFileName);
        string sortedTxtPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedTxtFileName);

        await Task.Run(() => scanner.Run(options, csvPath, txtPath));
        Console.WriteLine();

        scanner.WriteSortedResults(options, sortedCsvPath, sortedTxtPath);

        int successCount = scanner.Results.Count(r => r.Success);
        int failCount    = scanner.Results.Count - successCount;

        LogManager.LogRaw("");
        LogManager.LogRaw($"=== Tarama tamamlandı: {scanner.Results.Count} zaman dilimi ({successCount} başarılı, {failCount} hata) ===", ConsoleColor.Green);

        foreach (var r in scanner.Results)
        {
            if (r.Success)
                LogManager.LogRaw($"  {r.Timeframe,-8} {r.TaramaOzeti}");
            else
                LogManager.LogRaw($"  {r.Timeframe,-8} HATA: {r.ErrorMessage}", ConsoleColor.Red);
        }

        var best = scanner.GetBestResult(options);
        if (best != null)
        {
            LogManager.LogRaw("");
            LogManager.LogRaw($"En iyi ({cfg.Sort.SortField}): TF={best.Timeframe}  ->  {best.TaramaOzeti}", ConsoleColor.Yellow);
        }

        LogManager.LogRaw("");
        LogManager.LogRaw($"Sonuçlar     : {csvPath}");
        LogManager.LogRaw($"Sıralı sonuç : {sortedCsvPath}");
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred in runTimeframeScan: {ex.Message}", ex);
    }
}

async Task runMultiStrategyTimeframeScan()
{
    try
    {
        LogManager.LogRaw("");
        LogManager.LogRaw("Running MultiStrategyTimeframeScan (Tarama)");

        var cfg = appConfig.MultiStrategyTimeframeScan;

        var options = new MultiStrategyTimeframeScannerOptions
        {
            BaseFolder     = cfg.BaseFolder,
            Symbol         = cfg.Symbol,
            Timeframes     = cfg.Timeframes,
            SortField      = cfg.Sort.SortField,
            SortDescending = cfg.Sort.SortDescending,
            ConfigureAlgoTrader = at =>
            {
                AppConfigApplier.ApplyMultipleTrader(at, cfg.MultipleTrader, AppSettings.ConfigsDir);
                // Her TF icin ayni dosya adlarina yazildigi icin (collision) ve zaten kendi
                // ozet CSV/TXT'imiz asil ciktimiz oldugu icin MultipleTrader'in kendi dosya
                // yazimlari kapatiliyor.
                at.SetMultipleTraderSaveConfig(new MultipleTraderObjectSaveConfig
                {
                    SaveStatisticsToFile              = false,
                    SaveMultipleTraderListsTxtEnabled = false,
                    SaveMultipleTraderListsCsvEnabled = false,
                    WriteChildTradersDataToFiles      = false,
                });
                // AlgoTrader.ProgressLoggingEnabled varsayilan false, ama burada bilincli true
                // yapiliyor: her TF icin MultipleTrader (2+ child + consensus) buyuk TF'lerde
                // (orn. "01", 4.5M bar) dakikalarca surebiliyor, hicbir geri bildirim olmadan
                // ekran donmus gibi gorunuyordu (scanner'in kendi [current/total] satiri sadece
                // hucre BITTIKTEN sonra basiliyor). Bu satir ConsoleLogger.Write uzerinden yaziyor
                // (sink sisteminden bagimsiz), yani asagidaki DisableConsoleSink'ten etkilenmiyor,
                // consensus debug spam'i geri gelmiyor.
                at.ProgressLoggingEnabled = true;
            },
        };

        if (Enum.TryParse<StockDataReader.FilterMode>(cfg.ReadData.FilterMode, ignoreCase: true, out var filterMode))
            options.ReadFilterMode = filterMode;
        options.N1 = cfg.ReadData.N1;
        options.N2 = cfg.ReadData.N2;
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt1)) options.Dt1 = DateTime.Parse(cfg.ReadData.Dt1);
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt2)) options.Dt2 = DateTime.Parse(cfg.ReadData.Dt2);

        using var scanner = new MultiStrategyTimeframeScanner(logger);
        scanner.OnProgress = (current, total, tf) =>
        {
            consoleLogger!.Write($"\r\t[{current}/{total}] {tf}".PadRight(60));
        };

        string csvPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.CsvFileName);
        string txtPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.TxtFileName);
        string sortedCsvPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedCsvFileName);
        string sortedTxtPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedTxtFileName);

        // MultipleTrader.BuildConsensusSignal() her bar'da (Buy/Sell sinyalinde) bir Debug log
        // basıyor ("MultipleTrader consensus [Mode]: Buy=.. Sell=.. Flat=.. -> ..."). Normal [3]
        // MultipleTrader çalıştırmasında (tek dataset) sorun değil, ama burada N TF art arda
        // çalıştığı için konsolu dolduruyor. Console sink'i sadece RunAsync süresince kapatıp
        // hemen sonra geri açıyoruz — scanner'ın kendi [current/total] progress satırı (ham
        // ConsoleLogger.Write, sink sisteminden bağımsız) buna rağmen görünmeye devam eder, sadece
        // aşağıdaki özet/sonuç LogManager.LogRaw(...) çağrıları etkilenmeyecek şekilde try/finally
        // ile scope'landı.
        LogManager.DisableConsoleSink();
        try
        {
            await scanner.RunAsync(options, csvPath, txtPath);
        }
        finally
        {
            LogManager.EnableConsoleSink();
        }
        Console.WriteLine();

        scanner.WriteSortedResults(options, sortedCsvPath, sortedTxtPath);

        int successCount = scanner.Results.Count(r => r.Success);
        int failCount    = scanner.Results.Count - successCount;

        LogManager.LogRaw("");
        LogManager.LogRaw($"=== Tarama tamamlandı: {scanner.Results.Count} zaman dilimi ({successCount} başarılı, {failCount} hata) ===", ConsoleColor.Green);

        foreach (var r in scanner.Results)
        {
            if (r.Success)
            {
                LogManager.LogRaw($"  {r.Timeframe,-8} Bileşke: {r.TaramaOzeti}");
                foreach (var child in r.ChildSignals)
                    LogManager.LogRaw($"           Child{child.ChildId} (bağımsız): {child.TaramaOzeti}");
            }
            else
                LogManager.LogRaw($"  {r.Timeframe,-8} HATA: {r.ErrorMessage}", ConsoleColor.Red);
        }

        var best = scanner.GetBestResult(options);
        if (best != null)
        {
            LogManager.LogRaw("");
            LogManager.LogRaw($"En iyi ({cfg.Sort.SortField}): TF={best.Timeframe}  ->  {best.TaramaOzeti}", ConsoleColor.Yellow);
        }

        LogManager.LogRaw("");
        LogManager.LogRaw($"Sonuçlar     : {csvPath}");
        LogManager.LogRaw($"Sıralı sonuç : {sortedCsvPath}");
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred in runMultiStrategyTimeframeScan: {ex.Message}", ex);
    }
}

async Task runSymbolTimeframeScan()
{
    try
    {
        LogManager.LogRaw("");
        LogManager.LogRaw("Running SymbolTimeframeScan (Tarama)");

        var cfg     = appConfig.SymbolTimeframeScan;
        var options = AppConfigApplier.BuildSymbolTimeframeScanOptions(cfg, AppSettings.ConfigsDir);

        using var scanner = new SymbolTimeframeScanner(logger);
        scanner.OnProgress = (current, total, cell) =>
        {
            consoleLogger!.Write($"\r\t[{current}/{total}] {cell}".PadRight(60));
        };

        string csvPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.CsvFileName);
        string txtPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.TxtFileName);
        string sortedCsvPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedCsvFileName);
        string sortedTxtPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedTxtFileName);

        await Task.Run(() => scanner.Run(options, csvPath, txtPath));
        Console.WriteLine();

        scanner.WriteSortedResults(options, sortedCsvPath, sortedTxtPath);

        int successCount = scanner.Results.Count(r => r.Success);
        int failCount    = scanner.Results.Count - successCount;

        LogManager.LogRaw("");
        LogManager.LogRaw($"=== Tarama tamamlandı: {scanner.Results.Count} hücre ({successCount} başarılı, {failCount} hata) ===", ConsoleColor.Green);

        foreach (var r in scanner.Results)
        {
            if (r.Success)
                LogManager.LogRaw($"  {r.Symbol,-20} {r.Timeframe,-8} {r.TaramaOzeti}");
            else
                LogManager.LogRaw($"  {r.Symbol,-20} {r.Timeframe,-8} HATA: {r.ErrorMessage}", ConsoleColor.Red);
        }

        var best = scanner.GetBestResult(options);
        if (best != null)
        {
            LogManager.LogRaw("");
            LogManager.LogRaw($"En iyi ({cfg.Sort.SortField}): {best.Symbol}/{best.Timeframe}  ->  {best.TaramaOzeti}", ConsoleColor.Yellow);
        }

        LogManager.LogRaw("");
        LogManager.LogRaw($"Sonuçlar     : {csvPath}");
        LogManager.LogRaw($"Sıralı sonuç : {sortedCsvPath}");
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred in runSymbolTimeframeScan: {ex.Message}", ex);
    }
}

async Task runMultiStrategySymbolScan()
{
    try
    {
        LogManager.LogRaw("");
        LogManager.LogRaw("Running MultiStrategySymbolScan (Tarama)");

        var cfg = appConfig.MultiStrategySymbolScan;

        var options = new MultiStrategySymbolScannerOptions
        {
            DataFolder     = cfg.DataFolder,
            AutoDiscover   = cfg.AutoDiscover,
            SymbolList     = cfg.SymbolList,
            SortField      = cfg.Sort.SortField,
            SortDescending = cfg.Sort.SortDescending,
            ConfigureAlgoTrader = at =>
            {
                AppConfigApplier.ApplyMultipleTrader(at, cfg.MultipleTrader, AppSettings.ConfigsDir);
                // Her sembol icin ayni dosya adlarina yazildigi icin (collision) ve zaten kendi
                // ozet CSV/TXT'imiz asil ciktimiz oldugu icin MultipleTrader'in kendi dosya
                // yazimlari kapatiliyor.
                at.SetMultipleTraderSaveConfig(new MultipleTraderObjectSaveConfig
                {
                    SaveStatisticsToFile              = false,
                    SaveMultipleTraderListsTxtEnabled = false,
                    SaveMultipleTraderListsCsvEnabled = false,
                    WriteChildTradersDataToFiles      = false,
                });
                // AlgoTrader.ProgressLoggingEnabled varsayilan false, ama burada bilincli true
                // yapiliyor — [12]'deki ile ayni sebep (bkz. runMultiStrategyTimeframeScan): her
                // sembol icin MultipleTrader gecikmeli olabiliyor, hicbir geri bildirim olmadan
                // ekran donmus gibi gorunuyordu. ConsoleLogger.Write uzerinden yaziyor (sink
                // sisteminden bagimsiz), consensus debug spam'i geri gelmiyor.
                at.ProgressLoggingEnabled = true;
            },
        };

        if (Enum.TryParse<StockDataReader.FilterMode>(cfg.ReadData.FilterMode, ignoreCase: true, out var filterMode))
            options.ReadFilterMode = filterMode;
        options.N1 = cfg.ReadData.N1;
        options.N2 = cfg.ReadData.N2;
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt1)) options.Dt1 = DateTime.Parse(cfg.ReadData.Dt1);
        if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt2)) options.Dt2 = DateTime.Parse(cfg.ReadData.Dt2);

        using var scanner = new MultiStrategySymbolScanner(logger);
        scanner.OnProgress = (current, total, symbol) =>
        {
            consoleLogger!.Write($"\r\t[{current}/{total}] {symbol}".PadRight(60));
        };

        string csvPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.CsvFileName);
        string txtPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.TxtFileName);
        string sortedCsvPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedCsvFileName);
        string sortedTxtPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedTxtFileName);

        // [12]'deki (MultiStrategyTimeframeScan) ayni sebep: MultipleTrader.BuildConsensusSignal()
        // her bar'da bir Debug log basiyor, N sembol art arda calistigi icin konsolu dolduruyor.
        // Console sink'i sadece RunAsync suresince kapatip hemen sonra geri aciyoruz.
        LogManager.DisableConsoleSink();
        try
        {
            await scanner.RunAsync(options, csvPath, txtPath);
        }
        finally
        {
            LogManager.EnableConsoleSink();
        }
        Console.WriteLine();

        scanner.WriteSortedResults(options, sortedCsvPath, sortedTxtPath);

        int successCount = scanner.Results.Count(r => r.Success);
        int failCount    = scanner.Results.Count - successCount;

        LogManager.LogRaw("");
        LogManager.LogRaw($"=== Tarama tamamlandı: {scanner.Results.Count} sembol ({successCount} başarılı, {failCount} hata) ===", ConsoleColor.Green);

        foreach (var r in scanner.Results)
        {
            if (r.Success)
            {
                LogManager.LogRaw($"  {r.Symbol,-20} Bileşke: {r.TaramaOzeti}");
                foreach (var child in r.ChildSignals)
                    LogManager.LogRaw($"           Child{child.ChildId} (bağımsız): {child.TaramaOzeti}");
            }
            else
                LogManager.LogRaw($"  {r.Symbol,-20} HATA: {r.ErrorMessage}", ConsoleColor.Red);
        }

        var best = scanner.GetBestResult(options);
        if (best != null)
        {
            LogManager.LogRaw("");
            LogManager.LogRaw($"En iyi ({cfg.Sort.SortField}): {best.Symbol}  ->  {best.TaramaOzeti}", ConsoleColor.Yellow);
        }

        LogManager.LogRaw("");
        LogManager.LogRaw($"Sonuçlar     : {csvPath}");
        LogManager.LogRaw($"Sıralı sonuç : {sortedCsvPath}");
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred in runMultiStrategySymbolScan: {ex.Message}", ex);
    }
}

// =============================================================================
// Mode Handlers  (Config özeti göster → [ENTER] çalıştır | [E] düzenle | [B] geri)
// =============================================================================

void showModeConfigSummary(string title)
{
    string file   = string.IsNullOrEmpty(stockDataFullFileName) ? "(undefined)" : Path.GetFileName(stockDataFullFileName);
    string dataOk = (stockDataReader?.IsDataReady == true)
        ? $"{stockDataReader.GetDataCount()} bars loaded"
        : "Data not loaded";

    // Toplam kutu genişliği: 67 karakter (║ + 65 içerik + ║)
    // İçerik genişliği: 65 karakter
    string Trunc(string s, int max) => s.Length > max ? s[..max] : s;

    Console.WriteLine();
    Console.WriteLine("╔═════════════════════════════════════════════════════════════════╗");
    Console.WriteLine($"║  {title,-63}║");
    Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║  Data       : {Trunc(file, 50),-50}║");
    Console.WriteLine($"║  Status     : {Trunc(dataOk, 50),-50}║");

    if (title == "SingleTrader")
    {
        var cfg = appConfig.SingleTrader;
        var tp  = cfg.TradeParams;

        // Strateji
        string stratInfo = Trunc($"{cfg.Strategy.Name}  /  {cfg.Strategy.Version}", 50);
        Console.WriteLine($"║  Strategy   : {stratInfo,-50}║");

        // Query — null ise Disabled, dolu ise Enabled
        string queryLine   = cfg.Query != null
            ? Trunc($"{cfg.Query.Name}  /  {cfg.Query.Version}", 40)
            : "(undefined)";
        string queryStatus = cfg.Query != null ? "[Enabled]" : "[Disabled]";
        // "  Query      : " = 15, queryLine = 40, queryStatus right-aligned = 10  → 15+40+10=65 ✓
        Console.WriteLine($"║  Query      : {queryLine,-40}{queryStatus,10}║");

        // EquityCurveFilter — config dosyasından Enabled alanını okumaya çalış
        string ecfLine   = "(undefined)";
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
        string tradeInfo = Trunc($"{tp.MarketType}  |  Balance:{tp.IlkBakiye:N0}  |  Contract:{tp.KontratSayisi}", 50);
        Console.WriteLine($"║  TradeParam : {tradeInfo,-50}║");

        // RunMode seçim bölümü
        Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");

        string cur = cfg.RunMode;
        // İçerik = 65 karakter: seçili satırda PadRight(57) + "◄ seçili"(8) = 65
        string RmLine(string key, string name, bool selected)
        {
            string left = $"  [{key}]  {name}";
            return selected
                ? $"║{left.PadRight(55)}◄ selected║"
                : $"║{left.PadRight(65)}║";
        }

        Console.WriteLine(RmLine("1", "TradeOnly",     cur.Equals("TradeOnly",     StringComparison.OrdinalIgnoreCase)));
        Console.WriteLine(RmLine("2", "TradeAndQuery", cur.Equals("TradeAndQuery", StringComparison.OrdinalIgnoreCase)));
        Console.WriteLine(RmLine("3", "QueryOnly",     cur.Equals("QueryOnly",     StringComparison.OrdinalIgnoreCase)));

        Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  [1/2/3]  Select RunMode and Run                                ║");
        Console.WriteLine("║                                                                 ║");
        Console.WriteLine("║  [ENTER]  Run  (AppConfig RunMode)                              ║");
        Console.WriteLine("║  [E]      Edit AppConfig.json + Reload                          ║");
        Console.WriteLine("║  [R]      Reload AppConfig                                      ║");
        Console.WriteLine("║  [T]      Pause/Resume Timer                                    ║");
        Console.WriteLine("║  [B]      Return to Main Menu                                   ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        return;
    }

    if (title == "SingleTraderOptimizer")
    {
        var cfg = appConfig.SingleTraderOptimizer;
        var tp  = cfg.TradeParams;

        string stratInfo = Trunc($"{cfg.Strategy.Name}  /  {cfg.Strategy.Version}  |  Opt:{cfg.Optimization.Name}", 50);
        Console.WriteLine($"║  Strategy   : {stratInfo,-50}║");

        string ecfLine   = "(undefined)";
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

        string tradeInfo = Trunc($"{tp.MarketType}  |  Balance:{tp.IlkBakiye:N0}  |  Contract:{tp.KontratSayisi}", 50);
        Console.WriteLine($"║  TradeParam : {tradeInfo,-50}║");

        Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  [ENTER]  Run                                                   ║");
        Console.WriteLine("║  [E]      Edit AppConfig.json + Reload                          ║");
        Console.WriteLine("║  [R]      Reload AppConfig                                      ║");
        Console.WriteLine("║  [T]      Pause/Resume Timer                                    ║");
        Console.WriteLine("║  [B]      Return to Main Menu                                   ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        return;
    }

    if (title == "MultipleTrader")
    {
        var cfg = appConfig.MultipleTrader;
        var tp  = cfg.MainTrader.TradeParams;

        string runMode  = Trunc($"{cfg.RunMode}  |  Consensus: {cfg.Consensus.Mode}", 50);
        string tpInfo   = Trunc($"{tp.MarketType}  |  Balance:{tp.IlkBakiye:N0}  (MainTrader)", 50);

        Console.WriteLine($"║  RunMode    : {runMode,-50}║");

        // Per-child strategy rows (max 4, then "...")
        int nChildren = cfg.ChildTraders.Count;
        foreach (var child in cfg.ChildTraders.Take(4))
        {
            string childLine = Trunc($"[{child.ChildId}]  {child.Strategy.Name} / {child.Strategy.Version}", 50);
            Console.WriteLine($"║  Child      : {childLine,-50}║");
        }
        if (nChildren > 4)
        {
            string more = Trunc($"... and {nChildren - 4} more", 50);
            Console.WriteLine($"║  Child      : {more,-50}║");
        }

        Console.WriteLine($"║  TradeParam : {tpInfo,-50}║");

        Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  [ENTER]  Preview & Run                                         ║");
        Console.WriteLine("║  [E]      Edit AppConfig.json + Reload                          ║");
        Console.WriteLine("║  [R]      Reload AppConfig                                      ║");
        Console.WriteLine("║  [T]      Pause/Resume Timer                                    ║");
        Console.WriteLine("║  [B]      Return to Main Menu                                   ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        return;
    }

    if (title == "SymbolScan")
    {
        var cfg = appConfig.SymbolScan;

        string sourceInfo = cfg.AutoDiscover
            ? Trunc($"Auto-discover: {cfg.DataFolder}", 50)
            : Trunc($"{cfg.SymbolList.Count} sembol (liste)  |  {cfg.DataFolder}", 50);
        string stratInfo = Trunc($"{cfg.Strategy.Name}  /  {cfg.Strategy.Version}", 50);
        string sortInfo  = Trunc($"{cfg.Sort.SortField}  ({(cfg.Sort.SortDescending ? "desc" : "asc")})", 50);
        string fullStats = cfg.WriteFullStatsPerSymbol ? "[Enabled]" : "[Disabled]";

        Console.WriteLine($"║  Symbols    : {sourceInfo,-50}║");
        Console.WriteLine($"║  Strategy   : {stratInfo,-50}║");
        Console.WriteLine($"║  Sort       : {sortInfo,-50}║");
        Console.WriteLine($"║  FullStats  : {Trunc(fullStats, 50),-50}║");

        Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  [ENTER]  Run                                                   ║");
        Console.WriteLine("║  [E]      Edit AppConfig.json + Reload                          ║");
        Console.WriteLine("║  [R]      Reload AppConfig                                      ║");
        Console.WriteLine("║  [B]      Return to Main Menu                                   ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        return;
    }

    if (title == "TimeframeScan")
    {
        var cfg = appConfig.TimeframeScan;

        string sourceInfo = Trunc($"{cfg.Symbol}  |  {cfg.BaseFolder}", 50);
        string tfInfo     = Trunc(string.Join(", ", cfg.Timeframes), 50);
        string stratInfo  = Trunc($"{cfg.Strategy.Name}  /  {cfg.Strategy.Version}", 50);
        string sortInfo   = Trunc($"{cfg.Sort.SortField}  ({(cfg.Sort.SortDescending ? "desc" : "asc")})", 50);
        string fullStats  = cfg.WriteFullStatsPerTimeframe ? "[Enabled]" : "[Disabled]";

        Console.WriteLine($"║  Symbol     : {sourceInfo,-50}║");
        Console.WriteLine($"║  Timeframes : {tfInfo,-50}║");
        Console.WriteLine($"║  Strategy   : {stratInfo,-50}║");
        Console.WriteLine($"║  Sort       : {sortInfo,-50}║");
        Console.WriteLine($"║  FullStats  : {Trunc(fullStats, 50),-50}║");

        Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  [ENTER]  Run                                                   ║");
        Console.WriteLine("║  [E]      Edit AppConfig.json + Reload                          ║");
        Console.WriteLine("║  [R]      Reload AppConfig                                      ║");
        Console.WriteLine("║  [B]      Return to Main Menu                                   ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        return;
    }

    if (title == "MultiStrategyTimeframeScan")
    {
        var cfg = appConfig.MultiStrategyTimeframeScan;

        string sourceInfo = Trunc($"{cfg.Symbol}  |  {cfg.BaseFolder}", 50);
        string tfInfo     = Trunc(string.Join(", ", cfg.Timeframes), 50);
        string childInfo  = Trunc($"{cfg.MultipleTrader.ChildTraders.Count} child  |  Consensus: {cfg.MultipleTrader.Consensus.Mode}", 50);
        string sortInfo   = Trunc($"{cfg.Sort.SortField}  ({(cfg.Sort.SortDescending ? "desc" : "asc")})", 50);

        Console.WriteLine($"║  Symbol     : {sourceInfo,-50}║");
        Console.WriteLine($"║  Timeframes : {tfInfo,-50}║");
        Console.WriteLine($"║  MultiTrader: {childInfo,-50}║");
        Console.WriteLine($"║  Sort       : {sortInfo,-50}║");

        Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  [ENTER]  Run                                                   ║");
        Console.WriteLine("║  [E]      Edit AppConfig.json + Reload                          ║");
        Console.WriteLine("║  [R]      Reload AppConfig                                      ║");
        Console.WriteLine("║  [B]      Return to Main Menu                                   ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        return;
    }

    if (title == "SymbolTimeframeScan")
    {
        var cfg = appConfig.SymbolTimeframeScan;

        string sourceInfo = cfg.AutoDiscover
            ? Trunc($"Auto-discover ({cfg.ReferenceTimeframe}): {cfg.BaseFolder}", 50)
            : Trunc($"{cfg.SymbolList.Count} sembol (liste)  |  {cfg.BaseFolder}", 50);
        string tfInfo     = Trunc(string.Join(", ", cfg.Timeframes), 50);
        string stratInfo  = Trunc($"{cfg.Strategy.Name}  /  {cfg.Strategy.Version}", 50);
        string sortInfo   = Trunc($"{cfg.Sort.SortField}  ({(cfg.Sort.SortDescending ? "desc" : "asc")})", 50);
        string fullStats  = cfg.WriteFullStatsPerCell ? "[Enabled]" : "[Disabled]";

        Console.WriteLine($"║  Symbols    : {sourceInfo,-50}║");
        Console.WriteLine($"║  Timeframes : {tfInfo,-50}║");
        Console.WriteLine($"║  Strategy   : {stratInfo,-50}║");
        Console.WriteLine($"║  Sort       : {sortInfo,-50}║");
        Console.WriteLine($"║  FullStats  : {Trunc(fullStats, 50),-50}║");

        Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  [ENTER]  Run                                                   ║");
        Console.WriteLine("║  [E]      Edit AppConfig.json + Reload                          ║");
        Console.WriteLine("║  [R]      Reload AppConfig                                      ║");
        Console.WriteLine("║  [B]      Return to Main Menu                                   ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        return;
    }

    if (title == "MultiStrategySymbolScan")
    {
        var cfg = appConfig.MultiStrategySymbolScan;

        string sourceInfo = cfg.AutoDiscover
            ? Trunc($"Auto-discover: {cfg.DataFolder}", 50)
            : Trunc($"{cfg.SymbolList.Count} sembol (liste)  |  {cfg.DataFolder}", 50);
        string childInfo  = Trunc($"{cfg.MultipleTrader.ChildTraders.Count} child  |  Consensus: {cfg.MultipleTrader.Consensus.Mode}", 50);
        string sortInfo   = Trunc($"{cfg.Sort.SortField}  ({(cfg.Sort.SortDescending ? "desc" : "asc")})", 50);

        Console.WriteLine($"║  Symbols    : {sourceInfo,-50}║");
        Console.WriteLine($"║  MultiTrader: {childInfo,-50}║");
        Console.WriteLine($"║  Sort       : {sortInfo,-50}║");

        Console.WriteLine("╠═════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  [ENTER]  Run                                                   ║");
        Console.WriteLine("║  [E]      Edit AppConfig.json + Reload                          ║");
        Console.WriteLine("║  [R]      Reload AppConfig                                      ║");
        Console.WriteLine("║  [B]      Return to Main Menu                                   ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        return;
    }
}

void showSingleTraderRunPreview(TraderRunMode mode)
{
    var cfg = appConfig.SingleTrader;

    string runModeStr = mode switch
    {
        TraderRunMode.TradeAndQuery => "TradeAndQuery",
        TraderRunMode.QueryOnly     => "QueryOnly",
        _                           => "TradeOnly"
    };

    var jsonOpts = new JsonSerializerOptions
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters             = { new JsonStringEnumConverter() }
    };

    // Strategy: config dosyasından parse edilmiş parametreler (QueryOnly'de gösterilmez)
    object strategySection = new { cfg.Strategy.Name, cfg.Strategy.Version };
    if (mode != TraderRunMode.QueryOnly)
    {
        try
        {
            string stratPath = Path.Combine(AppSettings.ConfigsDir, cfg.Strategy.ConfigFile);
            var stratLoader  = new StrategyConfigLoader(stratPath);
            stratLoader.LoadFromFile();
            var stratCfg = stratLoader.GetConfiguration(cfg.Strategy.Name, cfg.Strategy.Version);
            if (stratCfg != null)
                strategySection = new
                {
                    stratCfg.StrategyName,
                    stratCfg.Version,
                    stratCfg.DisplayName,
                    Parameters = stratCfg.GetParameterValues()
                };
        }
        catch { }
    }

    // Query: config dosyasından parse edilmiş parametreler (TradeOnly'de gösterilmez)
    object? querySection = null;
    if (mode != TraderRunMode.TradeOnly && cfg.Query != null)
    {
        querySection = new { cfg.Query.Name, cfg.Query.Version }; // fallback
        try
        {
            string queryPath = Path.Combine(AppSettings.ConfigsDir, cfg.Query.ConfigFile);
            var queryLoader  = new QueryConfigLoader(queryPath);
            queryLoader.LoadFromFile();
            var queryCfg = queryLoader.GetConfiguration(cfg.Query.Name, cfg.Query.Version);
            if (queryCfg != null)
                querySection = new
                {
                    queryCfg.QueryName,
                    queryCfg.Version,
                    queryCfg.DisplayName,
                    Parameters = queryCfg.GetParameterValues()
                };
        }
        catch { }
    }

    // EquityCurveFilter: config dosyasından parse edilmiş parametreler
    object? ecfSection = null;
    if (cfg.EquityCurveFilter != null)
    {
        ecfSection = new { cfg.EquityCurveFilter.Version }; // fallback
        try
        {
            string ecfPath = Path.Combine(AppSettings.ConfigsDir, cfg.EquityCurveFilter.ConfigFile);
            var ecfLoader  = new EquityCurveFilterConfigLoader(ecfPath);
            ecfLoader.LoadFromFile();
            var ecfCfg = ecfLoader.GetConfiguration(cfg.EquityCurveFilter.Version);
            if (ecfCfg != null)
                ecfSection = new
                {
                    ecfCfg.Version,
                    ecfCfg.DisplayName,
                    ecfCfg.Enabled,
                    ecfCfg.ThresholdTypeIsPercent,
                    ecfCfg.ProfitThreshold,
                    ecfCfg.LossThreshold,
                    ecfCfg.Trigger
                };
        }
        catch { }
    }

    // Mode'a göre preview nesnesi oluştur
    object preview;
    switch (mode)
    {
        case TraderRunMode.TradeOnly:
            preview = new { RunMode = runModeStr, Strategy = strategySection, EquityCurveFilter = ecfSection, cfg.TradeParams, cfg.Signals, cfg.Plot, cfg.Optimization, cfg.Save };
            break;
        case TraderRunMode.TradeAndQuery:
            preview = new { RunMode = runModeStr, Strategy = strategySection, Query = querySection, EquityCurveFilter = ecfSection, cfg.TradeParams, cfg.Signals, cfg.Plot, cfg.Optimization, cfg.Save };
            break;
        default: // QueryOnly
            preview = new { RunMode = runModeStr, Query = querySection, EquityCurveFilter = ecfSection, cfg.TradeParams, cfg.Signals, cfg.Plot, cfg.Optimization, cfg.Save };
            break;
    }

    string json = JsonSerializer.Serialize(preview, jsonOpts);

    string sep = new string('═', 66);
    Console.WriteLine();
    Console.WriteLine("══ SingleTrader — Run Preview ════════════════════════════════════");
    WriteColoredJsonLines(json);
    Console.WriteLine(sep);
    Console.WriteLine("  [ENTER]  Run");
    Console.WriteLine("  [E]      Edit AppConfig.json + Reload");
    Console.WriteLine("  [R]      Reload AppConfig");
    Console.WriteLine("  [T]      Pause/Resume Timer");
    Console.WriteLine("  [B]      Back");
    Console.WriteLine();

    static void WriteColoredJsonLines(string json)
    {
        foreach (string rawLine in json.Split('\n'))
        {
            string line     = rawLine.TrimEnd('\r');
            int    colonIdx = line.IndexOf("\": ");
            if (colonIdx >= 0)
            {
                string valuePart = line.Substring(colonIdx + 3).TrimEnd(',').Trim();
                if (valuePart == "true" || valuePart == "false")
                {
                    Console.Write(line.Substring(0, colonIdx + 3));
                    Console.ForegroundColor = valuePart == "true" ? ConsoleColor.Green : ConsoleColor.Red;
                    Console.Write(line.Substring(colonIdx + 3));
                    Console.ResetColor();
                    Console.WriteLine();
                    continue;
                }
            }
            Console.WriteLine(line);
        }
    }
}

void showSingleTraderOptRunPreview()
{
    var cfg = appConfig.SingleTraderOptimizer;

    var jsonOpts = new JsonSerializerOptions
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters             = { new JsonStringEnumConverter() }
    };

    // Strategy: config dosyasından parse edilmiş parametreler
    object strategySection = new { cfg.Strategy.Name, cfg.Strategy.Version };
    try
    {
        string stratPath = Path.Combine(AppSettings.ConfigsDir, cfg.Strategy.ConfigFile);
        var stratLoader  = new StrategyConfigLoader(stratPath);
        stratLoader.LoadFromFile();
        var stratCfg = stratLoader.GetConfiguration(cfg.Strategy.Name, cfg.Strategy.Version);
        if (stratCfg != null)
            strategySection = new
            {
                stratCfg.StrategyName,
                stratCfg.Version,
                stratCfg.DisplayName,
                Parameters = stratCfg.GetParameterValues()
            };
    }
    catch { }

    // Optimization: config dosyasından parse edilmiş parametre aralıkları
    object optimizationSection = new { cfg.Optimization.Name, cfg.Optimization.Version };
    try
    {
        string optPath = Path.Combine(AppSettings.ConfigsDir, cfg.Optimization.ConfigFile);
        var optLoader  = new OptimizationConfigLoader(optPath);
        optLoader.LoadFromFile();
        var optCfg = optLoader.GetConfiguration(cfg.Optimization.Name, cfg.Optimization.Version);
        if (optCfg != null)
            optimizationSection = new
            {
                optCfg.StrategyName,
                optCfg.Version,
                optCfg.DisplayName,
                ParameterRanges = optCfg.ParameterRanges.Select(r => new { r.Name, r.Min, r.Max, r.Step }).ToList(),
                FixedParameters = optCfg.GetFixedParameterValues()
            };
    }
    catch { }

    // EquityCurveFilter: config dosyasından parse edilmiş parametreler
    object? ecfSection = null;
    if (cfg.EquityCurveFilter != null)
    {
        ecfSection = new { cfg.EquityCurveFilter.Version };
        try
        {
            string ecfPath = Path.Combine(AppSettings.ConfigsDir, cfg.EquityCurveFilter.ConfigFile);
            var ecfLoader  = new EquityCurveFilterConfigLoader(ecfPath);
            ecfLoader.LoadFromFile();
            var ecfCfg = ecfLoader.GetConfiguration(cfg.EquityCurveFilter.Version);
            if (ecfCfg != null)
                ecfSection = new
                {
                    ecfCfg.Version,
                    ecfCfg.DisplayName,
                    ecfCfg.Enabled,
                    ecfCfg.ThresholdTypeIsPercent,
                    ecfCfg.ProfitThreshold,
                    ecfCfg.LossThreshold,
                    ecfCfg.Trigger
                };
        }
        catch { }
    }

    var preview = new
    {
        Strategy          = strategySection,
        Optimization      = optimizationSection,
        cfg.Range,
        EquityCurveFilter = ecfSection,
        cfg.TradeParams,
        cfg.Signals,
        cfg.Save,
        cfg.Sort,
        SingleTrader      = new { cfg.SingleTrader.Plot, cfg.SingleTrader.Optimization, cfg.SingleTrader.Save }
    };

    string json = JsonSerializer.Serialize(preview, jsonOpts);

    string sep = new string('═', 66);
    Console.WriteLine();
    Console.WriteLine("══ SingleTraderOptimizer — Run Preview ════════════════════════════");
    foreach (string rawLine in json.Split('\n'))
    {
        string line     = rawLine.TrimEnd('\r');
        int    colonIdx = line.IndexOf("\": ");
        if (colonIdx >= 0)
        {
            string valuePart = line.Substring(colonIdx + 3).TrimEnd(',').Trim();
            if (valuePart == "true" || valuePart == "false")
            {
                Console.Write(line.Substring(0, colonIdx + 3));
                Console.ForegroundColor = valuePart == "true" ? ConsoleColor.Green : ConsoleColor.Red;
                Console.Write(line.Substring(colonIdx + 3));
                Console.ResetColor();
                Console.WriteLine();
                continue;
            }
        }
        Console.WriteLine(line);
    }
    Console.WriteLine(sep);
    Console.WriteLine("  [ENTER]  Run");
    Console.WriteLine("  [E]      Edit AppConfig.json + Reload");
    Console.WriteLine("  [R]      Reload AppConfig");
    Console.WriteLine("  [T]      Pause/Resume Timer");
    Console.WriteLine("  [B]      Back");
    Console.WriteLine();
}

void showMultipleTraderRunPreview()
{
    var cfg = appConfig.MultipleTrader;

    var jsonOpts = new JsonSerializerOptions
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters             = { new JsonStringEnumConverter() }
    };

    // MainTrader ECF
    object? mainEcfSection = null;
    if (cfg.MainTrader.EquityCurveFilter != null)
    {
        mainEcfSection = new { cfg.MainTrader.EquityCurveFilter.Version };
        try
        {
            string ecfPath = Path.Combine(AppSettings.ConfigsDir, cfg.MainTrader.EquityCurveFilter.ConfigFile);
            var ecfLoader  = new EquityCurveFilterConfigLoader(ecfPath);
            ecfLoader.LoadFromFile();
            var ecfCfg = ecfLoader.GetConfiguration(cfg.MainTrader.EquityCurveFilter.Version);
            if (ecfCfg != null)
                mainEcfSection = new { ecfCfg.Version, ecfCfg.DisplayName, ecfCfg.Enabled,
                    ecfCfg.ThresholdTypeIsPercent, ecfCfg.ProfitThreshold, ecfCfg.LossThreshold, ecfCfg.Trigger };
        }
        catch { }
    }

    // ChildTraders — her biri için Strategy, Query, ECF, Signals
    var childSections = new List<object>();
    foreach (var child in cfg.ChildTraders)
    {
        // Strategy
        object stratSection = new { child.Strategy.Name, child.Strategy.Version };
        try
        {
            string stratPath = Path.Combine(AppSettings.ConfigsDir, child.Strategy.ConfigFile);
            var stratLoader  = new StrategyConfigLoader(stratPath);
            stratLoader.LoadFromFile();
            var stratCfg = stratLoader.GetConfiguration(child.Strategy.Name, child.Strategy.Version);
            if (stratCfg != null)
                stratSection = new { stratCfg.StrategyName, stratCfg.Version, stratCfg.DisplayName,
                    Parameters = stratCfg.GetParameterValues() };
        }
        catch { }

        // Query
        object? querySection = null;
        if (child.Query != null)
        {
            querySection = new { child.Query.Name, child.Query.Version };
            try
            {
                string queryPath = Path.Combine(AppSettings.ConfigsDir, child.Query.ConfigFile);
                var queryLoader  = new QueryConfigLoader(queryPath);
                queryLoader.LoadFromFile();
                var queryCfg = queryLoader.GetConfiguration(child.Query.Name, child.Query.Version);
                if (queryCfg != null)
                    querySection = new { queryCfg.QueryName, queryCfg.Version, queryCfg.DisplayName,
                        Parameters = queryCfg.GetParameterValues() };
            }
            catch { }
        }

        // ECF
        object? ecfSection = null;
        if (child.EquityCurveFilter != null)
        {
            ecfSection = new { child.EquityCurveFilter.Version };
            try
            {
                string ecfPath = Path.Combine(AppSettings.ConfigsDir, child.EquityCurveFilter.ConfigFile);
                var ecfLoader  = new EquityCurveFilterConfigLoader(ecfPath);
                ecfLoader.LoadFromFile();
                var ecfCfg = ecfLoader.GetConfiguration(child.EquityCurveFilter.Version);
                if (ecfCfg != null)
                    ecfSection = new { ecfCfg.Version, ecfCfg.DisplayName, ecfCfg.Enabled };
            }
            catch { }
        }

        childSections.Add(new
        {
            child.ChildId,
            Strategy          = stratSection,
            Query             = querySection,
            EquityCurveFilter = ecfSection,
            child.Signals
        });
    }

    var preview = new
    {
        cfg.RunMode,
        cfg.Consensus,
        MainTrader   = new { cfg.MainTrader.TradeParams, cfg.MainTrader.Signals, EquityCurveFilter = mainEcfSection },
        ChildTraders = childSections,
        cfg.Save
    };

    string json = JsonSerializer.Serialize(preview, jsonOpts);

    string sep = new string('═', 66);
    Console.WriteLine();
    Console.WriteLine("══ MultipleTrader — Run Preview ════════════════════════════════════");
    foreach (string rawLine in json.Split('\n'))
    {
        string line     = rawLine.TrimEnd('\r');
        int    colonIdx = line.IndexOf("\": ");
        if (colonIdx >= 0)
        {
            string valuePart = line.Substring(colonIdx + 3).TrimEnd(',').Trim();
            if (valuePart == "true" || valuePart == "false")
            {
                Console.Write(line.Substring(0, colonIdx + 3));
                Console.ForegroundColor = valuePart == "true" ? ConsoleColor.Green : ConsoleColor.Red;
                Console.Write(line.Substring(colonIdx + 3));
                Console.ResetColor();
                Console.WriteLine();
                continue;
            }
        }
        Console.WriteLine(line);
    }
    Console.WriteLine(sep);
    Console.WriteLine("  [ENTER]  Run");
    Console.WriteLine("  [E]      Edit AppConfig.json + Reload");
    Console.WriteLine("  [R]      Reload AppConfig");
    Console.WriteLine("  [T]      Pause/Resume Timer");
    Console.WriteLine("  [B]      Back");
    Console.WriteLine();
}

async Task handleSingleTrader()
{
    reloadAppConfig();

    while (true)
    {
        showModeConfigSummary("SingleTrader");
        var input = MenuInput("");

        if (input == null || input.Equals("b", StringComparison.OrdinalIgnoreCase)) return;

        if (input.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            editAndReloadAppConfig();
            continue;
        }

        if (input.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            reloadAppConfig();
            continue;
        }

        // 1/2/3 → RunMode seç | ENTER → AppConfig'deki mevcut RunMode
        selectedRunMode = input switch
        {
            "1" => TraderRunMode.TradeOnly,
            "2" => TraderRunMode.TradeAndQuery,
            "3" => TraderRunMode.QueryOnly,
            _   => ParseRunMode(appConfig.SingleTrader.RunMode)
        };

        // Seçilen config'i önizle, ENTER/E/B bekle
        showSingleTraderRunPreview(selectedRunMode);
        var confirm = MenuInput("");

        // B veya ESC → özet ekranına geri dön
        if (confirm == null || confirm.Equals("b", StringComparison.OrdinalIgnoreCase)) continue;

        // E → düzenle ve önizlemeye geri dön
        if (confirm.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            editAndReloadAppConfig();
            continue;
        }

        if (confirm.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            reloadAppConfig();
            continue;
        }

        // ENTER → çalıştır
        await runSingleTraderAlgoTrade();

        // Run tamamlandı: ENTER ana menü, R tekrar çalıştır, ESC uygulamadan çık
        Console.WriteLine();
        Console.WriteLine("  Run completed.  [ENTER] Back to Main Menu   [R] Run again   [ESC] Exit");
        Console.WriteLine();
        var postRunInput = ReadMenuInput();
        if (postRunInput == null) { exitRequested = true; return; } // ESC → program çıkışı
        if (postRunInput.Equals("r", StringComparison.OrdinalIgnoreCase)) continue;
        return; // ENTER/diğer tuşlar → ana menü
    }
}

async Task handleMultipleTrader()
{
    reloadAppConfig();

    while (true)
    {
        showModeConfigSummary("MultipleTrader");
        var input = MenuInput("");

        if (input == null || input.Equals("b", StringComparison.OrdinalIgnoreCase)) return;

        if (input.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            editAndReloadAppConfig();
            continue;
        }

        if (input.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            reloadAppConfig();
            continue;
        }

        // ENTER → önizleme göster
        showMultipleTraderRunPreview();
        var confirm = MenuInput("");

        if (confirm == null || confirm.Equals("b", StringComparison.OrdinalIgnoreCase)) continue;

        if (confirm.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            editAndReloadAppConfig();
            continue;
        }

        if (confirm.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            reloadAppConfig();
            continue;
        }

        selectedRunMode = ParseRunMode(appConfig.MultipleTrader.RunMode);
        await runMultipleTraderAlgoTrade();

        // Run tamamlandı: ENTER ana menü, R tekrar çalıştır, ESC uygulamadan çık
        Console.WriteLine();
        Console.WriteLine("  Run completed.  [ENTER] Back to Main Menu   [R] Run again   [ESC] Exit");
        Console.WriteLine();
        var postRunInput = ReadMenuInput();
        if (postRunInput == null) { exitRequested = true; return; } // ESC → program çıkışı
        if (postRunInput.Equals("r", StringComparison.OrdinalIgnoreCase)) continue;
        return; // ENTER/diğer tuşlar → ana menü
    }
}

async Task handleSingleTraderOpt()
{
    reloadAppConfig();

    while (true)
    {
        showModeConfigSummary("SingleTraderOptimizer");
        var input = MenuInput("");

        if (input == null || input.Equals("b", StringComparison.OrdinalIgnoreCase)) return;

        if (input.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            editAndReloadAppConfig();
            continue;
        }

        if (input.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            reloadAppConfig();
            continue;
        }

        // ENTER (veya herhangi bir tuş) → önizleme göster
        showSingleTraderOptRunPreview();
        var confirm = MenuInput("");

        if (confirm == null || confirm.Equals("b", StringComparison.OrdinalIgnoreCase)) continue;

        if (confirm.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            editAndReloadAppConfig();
            continue;
        }

        if (confirm.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            reloadAppConfig();
            continue;
        }

        await runSingleTraderOptimization();

        // Run tamamlandı: ENTER ana menü, R tekrar çalıştır, ESC uygulamadan çık
        Console.WriteLine();
        Console.WriteLine("  Run completed.  [ENTER] Back to Main Menu   [R] Run again   [ESC] Exit");
        Console.WriteLine();
        var postRunInput = ReadMenuInput();
        if (postRunInput == null) { exitRequested = true; return; } // ESC → program çıkışı
        if (postRunInput.Equals("r", StringComparison.OrdinalIgnoreCase)) continue;
        return; // ENTER/diğer tuşlar → ana menü
    }
}

async Task handleSymbolScan()
{
    reloadAppConfig();

    while (true)
    {
        showModeConfigSummary("SymbolScan");
        var input = MenuInput("");

        if (input == null || input.Equals("b", StringComparison.OrdinalIgnoreCase)) return;

        if (input.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            editAndReloadAppConfig();
            continue;
        }

        if (input.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            reloadAppConfig();
            continue;
        }

        // ENTER (veya herhangi bir tuş) → çalıştır
        await runSymbolScan();

        // Run tamamlandı: ENTER ana menü, R tekrar çalıştır, ESC uygulamadan çık
        Console.WriteLine();
        Console.WriteLine("  Run completed.  [ENTER] Back to Main Menu   [R] Run again   [ESC] Exit");
        Console.WriteLine();
        var postRunInput = ReadMenuInput();
        if (postRunInput == null) { exitRequested = true; return; } // ESC → program çıkışı
        if (postRunInput.Equals("r", StringComparison.OrdinalIgnoreCase)) continue;
        return; // ENTER/diğer tuşlar → ana menü
    }
}

async Task handleTimeframeScan()
{
    reloadAppConfig();

    while (true)
    {
        showModeConfigSummary("TimeframeScan");
        var input = MenuInput("");

        if (input == null || input.Equals("b", StringComparison.OrdinalIgnoreCase)) return;

        if (input.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            editAndReloadAppConfig();
            continue;
        }

        if (input.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            reloadAppConfig();
            continue;
        }

        // ENTER (veya herhangi bir tuş) → çalıştır
        await runTimeframeScan();

        // Run tamamlandı: ENTER ana menü, R tekrar çalıştır, ESC uygulamadan çık
        Console.WriteLine();
        Console.WriteLine("  Run completed.  [ENTER] Back to Main Menu   [R] Run again   [ESC] Exit");
        Console.WriteLine();
        var postRunInput = ReadMenuInput();
        if (postRunInput == null) { exitRequested = true; return; } // ESC → program çıkışı
        if (postRunInput.Equals("r", StringComparison.OrdinalIgnoreCase)) continue;
        return; // ENTER/diğer tuşlar → ana menü
    }
}

async Task handleMultiStrategyTimeframeScan()
{
    reloadAppConfig();

    while (true)
    {
        showModeConfigSummary("MultiStrategyTimeframeScan");
        var input = MenuInput("");

        if (input == null || input.Equals("b", StringComparison.OrdinalIgnoreCase)) return;

        if (input.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            editAndReloadAppConfig();
            continue;
        }

        if (input.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            reloadAppConfig();
            continue;
        }

        // ENTER (veya herhangi bir tuş) → çalıştır
        await runMultiStrategyTimeframeScan();

        // Run tamamlandı: ENTER ana menü, R tekrar çalıştır, ESC uygulamadan çık
        Console.WriteLine();
        Console.WriteLine("  Run completed.  [ENTER] Back to Main Menu   [R] Run again   [ESC] Exit");
        Console.WriteLine();
        var postRunInput = ReadMenuInput();
        if (postRunInput == null) { exitRequested = true; return; } // ESC → program çıkışı
        if (postRunInput.Equals("r", StringComparison.OrdinalIgnoreCase)) continue;
        return; // ENTER/diğer tuşlar → ana menü
    }
}

async Task handleSymbolTimeframeScan()
{
    reloadAppConfig();

    while (true)
    {
        showModeConfigSummary("SymbolTimeframeScan");
        var input = MenuInput("");

        if (input == null || input.Equals("b", StringComparison.OrdinalIgnoreCase)) return;

        if (input.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            editAndReloadAppConfig();
            continue;
        }

        if (input.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            reloadAppConfig();
            continue;
        }

        // ENTER (veya herhangi bir tuş) → çalıştır
        await runSymbolTimeframeScan();

        // Run tamamlandı: ENTER ana menü, R tekrar çalıştır, ESC uygulamadan çık
        Console.WriteLine();
        Console.WriteLine("  Run completed.  [ENTER] Back to Main Menu   [R] Run again   [ESC] Exit");
        Console.WriteLine();
        var postRunInput = ReadMenuInput();
        if (postRunInput == null) { exitRequested = true; return; } // ESC → program çıkışı
        if (postRunInput.Equals("r", StringComparison.OrdinalIgnoreCase)) continue;
        return; // ENTER/diğer tuşlar → ana menü
    }
}

async Task handleMultiStrategySymbolScan()
{
    reloadAppConfig();

    while (true)
    {
        showModeConfigSummary("MultiStrategySymbolScan");
        var input = MenuInput("");

        if (input == null || input.Equals("b", StringComparison.OrdinalIgnoreCase)) return;

        if (input.Equals("e", StringComparison.OrdinalIgnoreCase))
        {
            editAndReloadAppConfig();
            continue;
        }

        if (input.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            reloadAppConfig();
            continue;
        }

        // ENTER (veya herhangi bir tuş) → çalıştır
        await runMultiStrategySymbolScan();

        // Run tamamlandı: ENTER ana menü, R tekrar çalıştır, ESC uygulamadan çık
        Console.WriteLine();
        Console.WriteLine("  Run completed.  [ENTER] Back to Main Menu   [R] Run again   [ESC] Exit");
        Console.WriteLine();
        var postRunInput = ReadMenuInput();
        if (postRunInput == null) { exitRequested = true; return; } // ESC → program çıkışı
        if (postRunInput.Equals("r", StringComparison.OrdinalIgnoreCase)) continue;
        return; // ENTER/diğer tuşlar → ana menü
    }
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

    Console.Write($"\nScript file path (default: {defaultDir}\\): ");
    var filePath = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(filePath))
    {
        var files = Directory.GetFiles(defaultDir, "*.csx");
        if (files.Length == 0) { LogManager.LogRaw($"No script found in directory: {defaultDir}"); return ("", ""); }

        Console.WriteLine("\nAvailable scripts:");
        for (int idx = 0; idx < files.Length; idx++)
            Console.WriteLine($"  [{idx + 1}] {Path.GetFileName(files[idx])}");

        Console.Write("\nYour selection: ");
        var choice = Console.ReadLine()?.Trim();
        if (int.TryParse(choice, out int sel) && sel >= 1 && sel <= files.Length)
            filePath = files[sel - 1];
        else
            return ("", "");
    }

    if (!File.Exists(filePath)) { LogManager.LogRaw($"File not found: {filePath}"); return ("", ""); }
    return (File.ReadAllText(filePath), filePath);
}

async Task<ScriptExecutionResult> executeScriptWithCancellation(string code, ScriptGlobals globals, string? sourceDirectory = null)
{
    scriptCts = new CancellationTokenSource();
    var scriptTask = scriptExecutor.ExecuteAsync(code, globals, scriptCts.Token, sourceDirectory);

    LogManager.LogRaw("\n[INFO] Script is running... (you can stop with ESC)\n", ConsoleColor.Cyan);

    while (!scriptTask.IsCompleted)
    {
        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
        {
            LogManager.LogRaw("\n[ESC] Script stop request sent...", ConsoleColor.Yellow);
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
        LogManager.LogRaw($"[OK] Script completed successfully ({result.ExecutionTime.TotalMilliseconds:F0} ms)", ConsoleColor.Green);
        if (result.ReturnValue != null)
            LogManager.LogRaw($"[RETURN] {result.ReturnValue}", ConsoleColor.Cyan);
    }
    else
    {
        if (result.CompilationErrors?.Count > 0)
        {
            LogManager.LogRaw("[ERROR] Compilation errors:", ConsoleColor.Red);
            foreach (var err in result.CompilationErrors)
                LogManager.LogRaw($"  {err}", ConsoleColor.Red);
        }
        else
        {
            LogManager.LogRaw($"[ERROR] {result.Error}", ConsoleColor.Red);
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
        LogManager.LogRaw("\n[WARNING] AlgoTrader has not been created yet. Run [2] or [5] first.", ConsoleColor.Yellow);
        return;
    }

    if (stockDataList != null && algoTrader.GetDataCount() == 0)
    {
        algoTrader.SetData(stockDataList);
        LogManager.LogRaw($"[INFO] Current stockData ({stockDataList.Count} bars) assigned to AlgoTrader.");
    }

    Console.WriteLine("\nPaste script code (finish with an empty line + ENTER):");
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
    Console.WriteLine("║        AlgoTrade — Main Menu                                       ║");
    Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [1]  Read Data                                                  ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("╠═══ Trader ═════════════════════════════════════════════════════════╣");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [2]  SingleTrader                                               ║");
    Console.WriteLine("║    [3]  MultipleTrader                                             ║");
    Console.WriteLine("║    [4]  SingleTraderOptimizer                                      ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("╠═══ Read Data + Run ════════════════════════════════════════════════╣");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [5]  Read Data + SingleTrader                                   ║");
    Console.WriteLine("║    [6]  Read Data + MultipleTrader                                 ║");
    Console.WriteLine("║    [7]  Read Data + SingleTraderOptimizer                          ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("╠═══ Script ═════════════════════════════════════════════════════════╣");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [8]  Run Script                                                 ║");
    Console.WriteLine("║                                                                    ║");
    // TODO: Demo/test bölümü - gerçek switch entegre olunca silinecek (bkz. handleDearPyGuiPlotterTest()).
    Console.WriteLine("╠═══ DearPyGuiDataPlotter (Test) ════════════════════════════════════╣");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [9]  DearPyGuiDataPlotter (Start/Stop Test)                     ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("╠═══ Tarama ═════════════════════════════════════════════════════════╣");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [10]  Tarama (Symbol Scan)                                      ║");
    Console.WriteLine("║    [11]  Tarama (Timeframe Scan)                                   ║");
    Console.WriteLine("║    [12]  Tarama (Multi-Strategy Timeframe Scan)                    ║");
    Console.WriteLine("║    [13]  Tarama (Symbol-Timeframe Scan)                            ║");
    Console.WriteLine("║    [14]  Tarama (Multi-Strategy Symbol Scan)                       ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [0]  Exit                                                       ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
}

// TODO: Bu fonksiyon + ana menüdeki [9] seçeneği (case "9" ve showMainMenu()
// içindeki "DearPyGuiDataPlotter (Test)" bölümü) sadece DearPyGuiDataPlotter
// process başlatma/StopPlotter/LoadBundle akışını doğrulamak için eklenmiş
// DEMO/TEST amaçlı koddur. Gerçek switch (PlotBackend seçimi) mevcut
// PlotSingleTraderData/PlotMultipleTraderData akışına taşındığında bu demo
// kod ve menü seçeneği silinecek.
void handleDearPyGuiPlotterTest()
{
    LogManager.LogRaw("");
    LogManager.LogRaw("[DearPyGuiDataPlotter] Process başlatılıyor...", ConsoleColor.Cyan);

    using var plotter = new DearPyGuiDataPlotter();
    plotter.SetLogger(logger);

    try
    {
        plotter.StartPlotter();
        LogManager.LogRaw($"[DearPyGuiDataPlotter] Başlatıldı. IsRunning={plotter.IsRunning}", ConsoleColor.Green);
    }
    catch (Exception ex)
    {
        LogManager.LogError($"[DearPyGuiDataPlotter] Başlatma hatası: {ex.Message}");
        return;
    }

    // Test amaçlı örnek bundle: src/DearPyGuiDataPlotter/inputs/full_pipeline_bundle.npz
    string testBundlePath = Path.Combine(plotter.ProjectDir, "inputs", "full_pipeline_bundle.npz");
    string testViewPath   = Path.Combine(plotter.ProjectDir, "inputs", "full_pipeline_bundle.view.json");

    if (File.Exists(testBundlePath))
    {
        plotter.LoadBundle(testBundlePath, File.Exists(testViewPath) ? testViewPath : null);
        LogManager.LogRaw($"[DearPyGuiDataPlotter] load_bundle komutu gönderildi: {Path.GetFileName(testBundlePath)}", ConsoleColor.Green);
    }
    else
    {
        LogManager.LogRaw($"[DearPyGuiDataPlotter] Test bundle bulunamadı, load_bundle atlandı: {testBundlePath}", ConsoleColor.DarkYellow);
    }

    Console.WriteLine();
    Console.WriteLine("  [ENTER] Panel 0'ı temizle (clear_panel testi)   [ESC] Kapat ve ana menüye dön");
    if (ReadMenuInput() != null)
    {
        plotter.ClearPanel(0);
        LogManager.LogRaw("[DearPyGuiDataPlotter] clear_panel komutu gönderildi: panelId=0", ConsoleColor.Green);

        Console.WriteLine();
        Console.WriteLine("  [ENTER] Plotter'ı kapat ve ana menüye dön");
        ReadMenuInput();
    }

    plotter.StopPlotter();
    LogManager.LogRaw("[DearPyGuiDataPlotter] Process durduruldu.", ConsoleColor.Yellow);
}

// =============================================================================
// Main
// =============================================================================

async Task main()
{
    bool returnedFromAutoRun = false;

    AppSettings.EnsureDirectories();

    logger.RegisterSink(new ConsoleSink());
    logger.RegisterSink(new DebugSink());
    logger.RegisterSink(new FileSink(AppSettings.LogsDir, "app.log"));
    consoleLogger = LogManager.GetConsoleLogger();
    try { consoleLogger.Clear(); } catch { }

    LogManager.LogRaw("Application started", ConsoleColor.Green);
    
    LogManager.LogRaw("");
    DeleteFilesInGivenDirectory(AppSettings.LogsDir);
    LogManager.LogRaw($"{AppSettings.LogsDir} cleared...");

    LogManager.LogRaw("");
    // AppConfig.json yükle
    Directory.CreateDirectory(Path.GetDirectoryName(appConfigPath)!);
    AppConfigLoader.CreateSampleIfNotExists(appConfigPath);

    appConfig             = AppConfigLoader.Load(appConfigPath);
    stockDataFullFileName = AppConfigApplier.ApplyAppSettings(appConfig.AppSettings);
    LogManager.LogRaw($"[AppConfig] Loaded: {appConfigPath}");
    if (!string.IsNullOrEmpty(stockDataFullFileName))
        LogManager.LogRaw($"[AppConfig] StockDataFile: {stockDataFullFileName}");

    LogManager.LogRaw("");
    // AutoRun: JSON'dan okuyup onay almadan çalıştır
    var autoRun = appConfig.AppSettings.AutoRunMode?.Trim() ?? "";
    if (!string.IsNullOrEmpty(autoRun) && !autoRun.Equals("None", StringComparison.OrdinalIgnoreCase))
    {
        while (true)
        {
            LogManager.LogRaw($"[AutoRun] Mode: {autoRun}", ConsoleColor.Cyan);
            readStockData(appConfig.ReadData);
            switch (autoRun.ToUpperInvariant())
            {
                case "SINGLETRADER":
                    selectedRunMode = ParseRunMode(appConfig.SingleTrader.RunMode);
                    await runSingleTraderAlgoTrade();
                    break;
                case "MULTIPLETRADER":
                    selectedRunMode = ParseRunMode(appConfig.MultipleTrader.RunMode);
                    await runMultipleTraderAlgoTrade();
                    break;
                case "SINGLETRADEROPTIMIZER":
                    await runSingleTraderOptimization();
                    break;
                default:
                    LogManager.LogRaw($"[AutoRun] Unknown mode: '{autoRun}'. Valid: SingleTrader, MultipleTrader, SingleTraderOptimizer", ConsoleColor.Red);
                    break;
            }

            Console.WriteLine();
            Console.WriteLine("  Run completed.  [ENTER] Back to Main Menu   [R] Run again   [ESC] Exit");
            Console.WriteLine();
            var postRunInput = ReadMenuInput();
            if (postRunInput == null) { exitRequested = true; break; } // ESC → program çıkışı
            if (postRunInput.Equals("r", StringComparison.OrdinalIgnoreCase)) continue;
            returnedFromAutoRun = true;
            break; // ENTER/diğer tuşlar → ana menü
        }
    }

    bool running = true;
    while (running && !exitRequested)
    {
        showMainMenu();
        var input = returnedFromAutoRun ? MenuInput("") : MenuInput("5");
        returnedFromAutoRun = false;
        if (input == null) { running = false; break; }
        if (string.IsNullOrEmpty(input)) input = "5";

        switch (input)
        {
            case "1": handleReadData();                                     break;
            case "2": await handleSingleTrader();                           break;
            case "3": await handleMultipleTrader();                         break;
            case "4": await handleSingleTraderOpt();                        break;
            case "5": if (handleReadData()) await handleSingleTrader();     break;
            case "6": if (handleReadData()) await handleMultipleTrader();   break;
            case "7": if (handleReadData()) await handleSingleTraderOpt();  break;
            case "8": await runFullScript();                                break;
            case "9": handleDearPyGuiPlotterTest();                         break; // TODO: demo/test, silinecek
            case "10": await handleSymbolScan();                            break;
            case "11": await handleTimeframeScan();                         break;
            case "12": await handleMultiStrategyTimeframeScan();             break;
            case "13": await handleSymbolTimeframeScan();                    break;
            case "14": await handleMultiStrategySymbolScan();                break;
            case "0": running = false;                                      break;
            default:  Console.WriteLine("Invalid selection.");              break;
        }
    }

    LogManager.LogRaw("Application finished", ConsoleColor.Green);

    algoTrader?.Dispose();
    try { PythonPlotter.Shutdown(); } catch { }
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
 * TODO : OPT En iyi hangi parametre setini seçmeliyim'in cevaplarını bulmak için kullanılabilecek yöntemler
 * 
2️⃣ Risk-adjusted skor üret

Mesela:

Score = NetReturn / MaxDD

Bu daha mantıklı seçim yaptırır.
 * 
 * 
4800 kombinasyon bitince:

Heatmap çıkar (period x percent)

NetReturn ısı haritası

MaxDD ısı haritası

PFNet ısı haritası

Ve şu soruyu sor:

En stabil bölge neresi?

Tek bir parametre değil,
bir parametre bandı aramalısın.
 * 
 * 
 * 
4800 sonuç bitince şunları yap:

NetReturn heatmap

MaxDD heatmap

NetReturn/MaxDD skoru

En iyi %10’luk parametrelerin kümelenmesi

Eğer en iyi %10 aynı bölgede toplanıyorsa → sistem sağlıklı

Eğer dağınıksa → overfit
 * 
 * 
*/ 
