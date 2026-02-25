using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Logging.Sinks;
using AlgoTrade.Core.Scripting;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Timer;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Strategies;
using AlgoTrade.Core.Trading.Queries;
using AlgoTrade.Core.Trading.EquityCurve;
using ScottPlot.Colormaps;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using static Nessos.LinqOptimizer.Core.QueryExpr;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

bool addHeadTailInfo = false;
var sb = new StringBuilder();
ConsoleLogger ? consoleLogger = null;
List<StockData>? stockDataList = null;
StockDataReader? stockDataReader = null;
ConcurrentDictionary<string, string>? stockMetaData = null;
AlgoTrader? algoTrader = null;
TimeManager timer = TimeManager.GetInstance();
LogManager logger = LogManager.GetInstance();
DateTime? _progressStartTime = null;
TraderRunMode selectedRunMode = TraderRunMode.TradeAndQuery;

// If true, show config selection menus; if false, pick defaults from files
bool bShowConfigSelectionMenuSingleTrader = false;
bool bShowConfigSelectionMenuSingleTraderOpt = false;
bool bShowConfigSelectionMenuMultiTrader = false;

// If true show Run Mode menu; if false pick default 1 (TradeOnly)
bool bShowRunModeMenu = false;

string stockDataFullFileName = "C:\\data\\csvFiles\\VIP\\01\\VIP-X030-T.csv";

void OnReadMetaData(StockDataReader sender, ConcurrentDictionary<string, string> metaData)
{
    if (sender.IsMetaDataRead)
    {
        var stockMetaData = sender.GetMetaData();

        var kayitZamani = stockMetaData.GetValueOrDefault("Kayit_Zamani", "N/A");
        var grafikSembol = stockMetaData.GetValueOrDefault("GrafikSembol", "N/A");
        var grafikPeriyot = stockMetaData.GetValueOrDefault("GrafikPeriyot", "N/A");
        var barCount = stockMetaData.GetValueOrDefault("BarCount", "N/A");
        var baslangicTarihi = stockMetaData.GetValueOrDefault("Baslangic_Tarihi", "N/A");
        var bitisTarihi = stockMetaData.GetValueOrDefault("Bitis_Tarihi", "N/A");
        var format = stockMetaData.GetValueOrDefault("Format", "N/A");

        int padding = 18;
        sb.Clear();
        sb.AppendLine($"{"\tKayit Zamani".PadRight(padding)}: {kayitZamani}");
        sb.AppendLine($"{"\tGrafikSembol".PadRight(padding)}: {grafikSembol}");
        sb.AppendLine($"{"\tGrafikPeriyot".PadRight(padding)}: {grafikPeriyot}");
        sb.AppendLine($"{"\tBarCount".PadRight(padding)}: {barCount}");
        sb.AppendLine($"{"\tBaslangic Tarihi".PadRight(padding)}: {baslangicTarihi}");
        sb.AppendLine($"{"\tBitis Tarihi".PadRight(padding)}: {bitisTarihi}");
        sb.Append($"{"\tFormat".PadRight(padding)}: {format}");

        LogManager.LogRaw(sb.ToString());
    }
}

void OnProgress(StockDataReader sender, int count, bool isCompleted)
{
    if (isCompleted)
    {
        consoleLogger.Write($"\r\tRecord count     : {count}");
        consoleLogger.WriteLine("");
    }
    else
    {
        consoleLogger.Write($"\r\tRecord no        : {count}");
    }
}

void OnReadData(StockDataReader sender, List<StockData> data, long elapsedMs)
{
    // Console.WriteLine($"\tData okundu - {data.Count} kayit, {elapsedMs} ms");
    // LogManager.Log(sb.ToString());
}

void OnTraderProgress(/*SingleTrader sender, */int currentBar, int totalBars, double percentage)
{
    //LogManager.LogRaw($"\rProgress: {currentBar}/{totalBars} ({percentage:F1}%)");

    // TODO: Ileride detayli progress bilgisi eklenebilir:
    // Ilk mesajda startTime set edilir, sonrakilerde elapsed/remaining hesaplanir.
    // if (_progressStartTime == null) _progressStartTime = DateTime.Now;
    //
    // var elapsed = DateTime.Now - _progressStartTime.Value;
    // double barsPerSecond = currentBar / elapsed.TotalSeconds;
    // int remainingBars = totalBars - currentBar;
    // TimeSpan estimatedRemaining = barsPerSecond > 0
    //     ? TimeSpan.FromSeconds(remainingBars / barsPerSecond)
    //     : TimeSpan.Zero;
    //
    // Not: Her yeni run oncesi _progressStartTime = null yapilmali.
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
    {
        var c = configs[i];
        Console.WriteLine($"  [{i + 1}] {c.name} | {c.version} | {c.display}");
    }
    Console.WriteLine();

    string? input = null;
    for (int i = timeoutSeconds; i > 0; i--)
    {
        Console.Write($"\rSeciminiz (default: 1) ({i} sn): ");
        if (Console.KeyAvailable)
        {
            input = Console.ReadLine()?.Trim();
            break;
        }
        Thread.Sleep(1000);
    }

    if (input == null)
    {
        Console.Write($"\rSeciminiz (default: 1) (0 sn): ");
        Console.WriteLine();
        Console.WriteLine("Zaman asimi - ilk config secildi.");
    }

    if (string.IsNullOrEmpty(input))
    {
        return (configs[0].name, configs[0].version);
    }

    if (int.TryParse(input, out int sel) && sel >= 1 && sel <= configs.Count)
    {
        return (configs[sel - 1].name, configs[sel - 1].version);
    }

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
    Console.WriteLine($"{configType} Config Secimi (MultiTrader - virgul ile coklu secim, ornek: 1,3,5 | all=tumunu sec):");
    for (int i = 0; i < configs.Count; i++)
    {
        var c = configs[i];
        Console.WriteLine($"  [{i + 1}] {c.name} | {c.version} | {c.display}");
    }
    Console.WriteLine();

    string? input = null;
    for (int i = timeoutSeconds; i > 0; i--)
    {
        Console.Write($"\rSeciminiz (default: all) ({i} sn): ");
        if (Console.KeyAvailable)
        {
            input = Console.ReadLine()?.Trim();
            break;
        }
        Thread.Sleep(1000);
    }

    if (input == null)
    {
        Console.Write($"\rSeciminiz (default: all) (0 sn): ");
        Console.WriteLine();
        Console.WriteLine("Zaman asimi - tum config'ler secildi.");
    }

    // all veya bos → tumu sec
    if (string.IsNullOrEmpty(input) || input.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        return configs.Select(c => (c.name, c.version)).ToList();
    }

    // Virgul ile ayrilmis numaralar: "1,3,5"
    var selections = new List<(string name, string version)>();
    var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var part in parts)
    {
        if (int.TryParse(part, out int sel) && sel >= 1 && sel <= configs.Count)
        {
            selections.Add((configs[sel - 1].name, configs[sel - 1].version));
        }
        else
        {
            Console.WriteLine($"Gecersiz numara: {part} — atlanıyor.");
        }
    }

    if (selections.Count == 0)
    {
        Console.WriteLine("Gecersiz secim - tum config'ler secildi.");
        return configs.Select(c => (c.name, c.version)).ToList();
    }

    return selections;
}

void ConfigureStrategy(bool bShowConfigSelectionMenu = true)
{
    if (algoTrader is null)
        throw new InvalidOperationException("AlgoTrader instance is null.");

    string configPath = Path.Combine(AppSettings.ConfigsDir, "StrategyConfig.txt");

    if (File.Exists(configPath))
    {
        var loader = new StrategyConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();

        if (bShowConfigSelectionMenu)
        {
            var menuItems = allConfigs
                .Select(c => (c.StrategyName, c.Version, c.GetParametersDisplayString()))
                .ToList();

            var selected = ShowConfigSelectionMenu("Strategy", menuItems);
            if (selected is null) return;

            algoTrader.ConfigureStrategyFromConfig(configPath, selected.Value.name, selected.Value.version);
            LogManager.LogRaw($"\nStrategy loaded from config: {selected.Value.name} | {selected.Value.version}");
        }
        else
        {
            var first = allConfigs.First();
            algoTrader.ConfigureStrategyFromConfig(configPath, first.StrategyName, first.Version);
            LogManager.LogRaw($"\nStrategy loaded from config (default): {first.StrategyName} | {first.Version}");
        }
    }
    else
    {
        LogManager.LogRaw($"\nStrategy config file not found: {configPath}");
        algoTrader.ConfigureStrategy("SimpleMostStrategy", new Dictionary<string, object>
        {
            ["period"] = 21,
            ["percent"] = 1.0,
            ["choice"] = 0
        });
        LogManager.LogRaw("\nStrategy config file not found, fallback strategy configured from in-code parameters.");
    }
}

void ConfigureQuery(bool bShowConfigSelectionMenu = true)
{
    if (algoTrader is null)
        throw new InvalidOperationException("AlgoTrader instance is null.");

    string configPath = Path.Combine(AppSettings.ConfigsDir, "QueryConfig.txt");

    if (File.Exists(configPath))
    {
        var loader = new QueryConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();

        if (bShowConfigSelectionMenu)
        {
            var menuItems = allConfigs
                .Select(c => (c.QueryName, c.Version, c.GetParametersDisplayString()))
                .ToList();

            var selected = ShowConfigSelectionMenu("Query", menuItems);
            if (selected is null) return;

            algoTrader.ConfigureQueryFromConfig(configPath, selected.Value.name, selected.Value.version);
            LogManager.LogRaw($"\nQuery loaded from config: {selected.Value.name} | {selected.Value.version}");
        }
        else
        {
            var first = allConfigs.First();
            algoTrader.ConfigureQueryFromConfig(configPath, first.QueryName, first.Version);
            LogManager.LogRaw($"\nQuery loaded from config (default): {first.QueryName} | {first.Version}");
        }
    }
    else
    {
        LogManager.LogRaw($"\nQuery config file not found: {configPath}");
        algoTrader.ConfigureQuery("SimpleQuery1", new Dictionary<string, object>
        {
            ["ma8Period"] = 8,
            ["ma200Period"] = 200,
            ["choice"] = 0
        });
        LogManager.LogRaw("\nQuery config file not found, fallback query configured from in-code parameters.");
    }
}

void ConfigureEquityCurveFilter(bool bShowConfigSelectionMenu = true)
{
    if (algoTrader is null)
        throw new InvalidOperationException("AlgoTrader instance is null.");

    string configPath = Path.Combine(AppSettings.ConfigsDir, "EquityCurveFilterConfig.txt");

    if (File.Exists(configPath))
    {
        var loader = new EquityCurveFilterConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();

        if (bShowConfigSelectionMenu)
        {
            var menuItems = allConfigs
                .Select(c => ("EquityCurveFilter", c.Version, c.GetDisplayString()))
                .ToList();

            var selected = ShowConfigSelectionMenu("EquityCurveFilter", menuItems);
            if (selected is null) return;

            algoTrader.ClearEquityCurveFilterConfigs();
            algoTrader.ConfigureEquityCurveFilterFromConfig(configPath, selected.Value.version, id: 0);
            LogManager.LogRaw($"\nEquityCurveFilter loaded from config: {selected.Value.version}");
        }
        else
        {
            var first = allConfigs.First();
            algoTrader.ClearEquityCurveFilterConfigs();
            algoTrader.ConfigureEquityCurveFilterFromConfig(configPath, first.Version, id: 0);
            LogManager.LogRaw($"\nEquityCurveFilter loaded from config (default): {first.Version}");
        }
    }
    else
    {
        LogManager.LogRaw($"\nEquityCurveFilter config file not found: {configPath}");
        algoTrader.ClearEquityCurveFilterConfigs();
        algoTrader.AddEquityCurveFilterConfig(0,
            enabled: false,
            thresholdTypeIsPercent: true,
            profitThreshold: 0.05,
            lossThreshold: -0.05,
            trigger: ConfirmationTrigger.Both);
        LogManager.LogRaw("\nEquityCurveFilter config file not found, fallback configured from in-code parameters.");
    }
}

void DeleteFilesInGivenDirectory(string directoryPath, bool includeSubdirectories = false)
{
    if (string.IsNullOrWhiteSpace(directoryPath))
        throw new ArgumentException("Directory path cannot be null or empty.", nameof(directoryPath));

    string targetDirectory = directoryPath;

    if (!Directory.Exists(targetDirectory))
    {
        LogManager.LogRaw($"\nDirectory not found: {targetDirectory}");
        return;
    }

    var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
    var files = Directory.GetFiles(targetDirectory, "*", searchOption);
    int deletedCount = 0;
    int skippedCount = 0;
    foreach (var file in files)
    {
        try
        {
            File.Delete(file);
            deletedCount++;
        }
        catch (IOException)
        {
            skippedCount++;
            LogManager.LogRaw($"\n\tSkipped (locked): {Path.GetFileName(file)}");
        }
    }

    LogManager.LogRaw($"\nDeleted file count: {deletedCount} \n\t(Directory: {targetDirectory})");
    if (skippedCount > 0)
        LogManager.LogRaw($"\nSkipped (locked) file count: {skippedCount}");
}


void ConfigureStrategies()
{
    if (algoTrader is null)
        throw new InvalidOperationException("AlgoTrader instance is null.");

    string configPath = Path.Combine(AppSettings.ConfigsDir, "StrategyConfig.txt");

    if (File.Exists(configPath))
    {
        var loader = new StrategyConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();

        var menuItems = allConfigs
            .Select(c => (c.StrategyName, c.Version, c.GetParametersDisplayString()))
            .ToList();

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
        {
            ["period"] = 21,
            ["percent"] = 1.0,
            ["choice"] = 0
        });
        algoTrader.AddStrategyConfig(1, "SimpleMostStrategy", new Dictionary<string, object>
        {
            ["period"] = 14,
            ["percent"] = 0.5,
            ["choice"] = 0
        });
        LogManager.LogRaw("\nStrategy config file not found, fallback configured from in-code parameters.");
    }
}

void ConfigureQueries()
{
    if (algoTrader is null)
        throw new InvalidOperationException("AlgoTrader instance is null.");

    string configPath = Path.Combine(AppSettings.ConfigsDir, "QueryConfig.txt");

    if (File.Exists(configPath))
    {
        var loader = new QueryConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();

        var menuItems = allConfigs
            .Select(c => (c.QueryName, c.Version, c.GetParametersDisplayString()))
            .ToList();

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
        algoTrader.AddQueryConfig(0, "SimpleQuery1", new Dictionary<string, object>
        {
            ["ma8Period"] = 8,
            ["ma200Period"] = 200,
            ["choice"] = 0
        });
        algoTrader.AddQueryConfig(1, "SimpleQuery1", new Dictionary<string, object>
        {
            ["ma8Period"] = 5,
            ["ma200Period"] = 100,
            ["choice"] = 0
        });
        LogManager.LogRaw("\nQuery config file not found, fallback configured from in-code parameters.");
    }
}

void ConfigureOptimization(bool bShowConfigSelectionMenu = true)
{
    if (algoTrader is null)
        throw new InvalidOperationException("AlgoTrader instance is null.");

    string configPath = Path.Combine(AppSettings.ConfigsDir, "OptimizationConfig.txt");

    if (File.Exists(configPath))
    {
        var loader = new OptimizationConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();

        if (bShowConfigSelectionMenu)
        {
            var menuItems = allConfigs
                .Select(c => (c.StrategyName, c.Version, c.GetDisplayString()))
                .ToList();

            var selected = ShowConfigSelectionMenu("Optimization", menuItems);
            if (selected is null) return;

            algoTrader.ConfigureOptimizationFromConfig(configPath, selected.Value.name, selected.Value.version);
            LogManager.LogRaw($"\nOptimization loaded from config: {selected.Value.name} | {selected.Value.version}");
        }
        else
        {
            var first = allConfigs.First();
            algoTrader.ConfigureOptimizationFromConfig(configPath, first.StrategyName, first.Version);
            LogManager.LogRaw($"\nOptimization loaded from config (default): {first.StrategyName} | {first.Version}");
        }
    }
    else
    {
        LogManager.LogRaw($"\nOptimization config file not found: {configPath}");

        algoTrader.ClearOptimizationParameterRanges();
        algoTrader.AddOptimizationParameterRange("period", 10, 50, 10);
        algoTrader.AddOptimizationParameterRange("percent", 1.0, 3.0, 1.0);

        // Strategy factory - her kombinasyon için strateji oluşturur
        algoTrader.SetOptimizationStrategyFactory((data, ind, parameters) =>
        {
            int period = Convert.ToInt32(parameters["period"]);
            double percent = Convert.ToDouble(parameters["percent"], System.Globalization.CultureInfo.InvariantCulture);
            return algoTrader.CreateStrategyFromRegistry(data, ind, "SimpleMostStrategy", new Dictionary<string, object>
            {
                ["period"] = period,
                ["percent"] = percent,
                ["choice"] = 0
            });
        });

        LogManager.LogRaw("\nOptimization config file not found, fallback optimization configured from in-code parameters.");
    }

    // Optimization range: FullOpt (default) or PartialOpt
    // -1 = en bastan / en sona kadar (FullOpt)
    algoTrader.SetOptimizationRange(from: -1, to: -1);
}

void ConfigureEquityCurveFilters()
{
    if (algoTrader is null)
        throw new InvalidOperationException("AlgoTrader instance is null.");

    string configPath = Path.Combine(AppSettings.ConfigsDir, "EquityCurveFilterConfig.txt");

    if (File.Exists(configPath))
    {
        var loader = new EquityCurveFilterConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();

        var menuItems = allConfigs
            .Select(c => ("EquityCurveFilter", c.Version, c.GetDisplayString()))
            .ToList();

        // Her childTrader icin ayri secim
        int childCount = algoTrader.StrategyConfigs.Count;
        algoTrader.ClearEquityCurveFilterConfigs();

        for (int i = 0; i < childCount; i++)
        {
            LogManager.LogRaw($"\n--- ChildTrader Id={i} icin EquityCurveFilter ---");
            var selected = ShowConfigSelectionMenu($"EquityCurveFilter (Id={i})", menuItems);
            if (selected is null) return;

            algoTrader.ConfigureEquityCurveFilterFromConfig(configPath, selected.Value.version, id: i);
            LogManager.LogRaw($"EquityCurveFilter Id={i} loaded: {selected.Value.version}");
        }
    }
    else
    {
        LogManager.LogRaw($"\nEquityCurveFilter config file not found: {configPath}");
        algoTrader.ClearEquityCurveFilterConfigs();

        int childCount = algoTrader.StrategyConfigs.Count;
        for (int i = 0; i < childCount; i++)
        {
            algoTrader.AddEquityCurveFilterConfig(i,
                enabled: false,
                thresholdTypeIsPercent: true,
                profitThreshold: 0.05,
                lossThreshold: -0.05,
                trigger: ConfirmationTrigger.Both);
        }
        LogManager.LogRaw("\nEquityCurveFilter config file not found, fallback configured from in-code parameters.");
    }
}

void readStockData()
{
    try
    {
        if (!File.Exists(stockDataFullFileName))
        {
            LogManager.LogRaw($"File does not exist : {stockDataFullFileName}");
        }
        else
        {
            stockDataReader = new StockDataReader();
            stockDataReader.OnReadMetaData += OnReadMetaData;
            stockDataReader.OnReadData += OnReadData;
            stockDataReader.OnProgress += OnProgress;

            string fileName = Path.GetFileName(stockDataFullFileName);
            string fileDir = Path.GetDirectoryName(stockDataFullFileName)!;
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
                sb.Clear();
                sb.Append("is completed in ");
                sb.Append($"{t1} ms.");
                LogManager.LogRaw(sb.ToString());

                consoleLogger.Write("is completed in ");
                consoleLogger.Write($"{t1}", ConsoleColor.Green);
                consoleLogger.WriteLine(" ms.");

                LogManager.EnableConsoleSink();
            }

            if (stockDataReader.IsMetaDataRead)
            {
                var barCount = stockMetaData.GetValueOrDefault("BarCount", "N/A");
                var baslangicTarihi = stockMetaData.GetValueOrDefault("Baslangic_Tarihi", "N/A");
                var bitisTarihi = stockMetaData.GetValueOrDefault("Bitis_Tarihi", "N/A");

                bool useMenu = false;

                LogManager.LogRaw("");

                StockDataReader.FilterMode mode = StockDataReader.FilterMode.All;

                int n1 = 1000;
                int n2 = int.TryParse(barCount, out var bc) ? bc : 0;
                DateTime? dt1 = DateTime.TryParseExact(baslangicTarihi, "yyyy.MM.dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var d1) ? d1 : null;
                DateTime? dt2 = DateTime.TryParseExact(bitisTarihi, "yyyy.MM.dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var d2) ? d2 : null;

                //Console.WriteLine($"Loading data from        : {filePath}");
                LogManager.LogRaw($"Loading data from        : {filePath}");

                if (useMenu)
                {
                    int timeout = 10;
                    Console.WriteLine();
                    Console.WriteLine($"Filter Mode ({timeout} sn icinde secim yapilmazsa All secilir):");
                    Console.WriteLine($"  [1] All");
                    Console.WriteLine($"  [2] Last N          \t{n1}");
                    Console.WriteLine($"  [3] First N         \t{n1}");
                    Console.WriteLine($"  [4] Index Range     \t{0} - {n2}");
                    Console.WriteLine($"  [5] After DateTime  \t{dt1}");
                    Console.WriteLine($"  [6] Before DateTime \t{dt2}");
                    Console.WriteLine($"  [7] DateTime Range  \t{dt1} - {dt2}");
                    Console.WriteLine();
                    Console.Write("Seciminiz: ");

                    string? input = null;
                    for (int i = timeout; i > 0; i--)
                    {
                        Console.Write($"\rSeciminiz ({i} sn): ");
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(false);
                            input = key.KeyChar.ToString();
                            Console.WriteLine();
                            break;
                        }
                        Thread.Sleep(1000);
                    }
                    if (input == null)
                    {
                        Console.Write($"\rSeciminiz (0 sn): ");
                        Console.WriteLine();
                        Console.WriteLine("Zaman asimi - All secildi.");
                    }

                    switch (input)
                    {
                        case "2":
                            mode = StockDataReader.FilterMode.LastN;
                            Console.Write("N degeri: ");
                            if (int.TryParse(Console.ReadLine(), out int lastN)) n1 = lastN;
                            break;
                        case "3":
                            mode = StockDataReader.FilterMode.FirstN;
                            Console.Write("N degeri: ");
                            if (int.TryParse(Console.ReadLine(), out int firstN)) n1 = firstN;
                            break;
                        case "4":
                            mode = StockDataReader.FilterMode.IndexRange;
                            Console.Write("Baslangic index: ");
                            if (int.TryParse(Console.ReadLine(), out int idx1)) n1 = idx1;
                            Console.Write("Bitis index: ");
                            if (int.TryParse(Console.ReadLine(), out int idx2)) n2 = idx2;
                            break;
                        case "5":
                            mode = StockDataReader.FilterMode.AfterDateTime;
                            Console.Write("Tarih (yyyy.MM.dd HH:mm:ss): ");
                            if (DateTime.TryParseExact(Console.ReadLine(), "yyyy.MM.dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var after)) dt1 = after;
                            break;
                        case "6":
                            mode = StockDataReader.FilterMode.BeforeDateTime;
                            Console.Write("Tarih (yyyy.MM.dd HH:mm:ss): ");
                            if (DateTime.TryParseExact(Console.ReadLine(), "yyyy.MM.dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var before)) dt1 = before;
                            break;
                        case "7":
                            mode = StockDataReader.FilterMode.DateTimeRange;
                            Console.Write("Baslangic tarihi (yyyy.MM.dd HH:mm:ss): ");
                            if (DateTime.TryParseExact(Console.ReadLine(), "yyyy.MM.dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var from)) dt1 = from;
                            Console.Write("Bitis tarihi (yyyy.MM.dd HH:mm:ss): ");
                            if (DateTime.TryParseExact(Console.ReadLine(), "yyyy.MM.dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var to)) dt2 = to;
                            break;
                        default:
                            mode = StockDataReader.FilterMode.All;
                            break;
                    }

                    Console.WriteLine();
                    Console.WriteLine($"Secilen mod: {mode}");
                }

                stockDataReader.ReStartTimer();

                mode = StockDataReader.FilterMode.LastN;    // Silinecek- sadece test amacli
                n1 = 100000;                                // Silinecek- sadece test amacli

                if (mode == StockDataReader.FilterMode.All)
                {
                    stockDataReader.ReadDataFast(filePath);
                }
                else if (mode == StockDataReader.FilterMode.LastN)
                {
                    stockDataReader.ReadDataFast(filePath, StockDataReader.FilterMode.LastN, n1);
                }
                else if (mode == StockDataReader.FilterMode.FirstN)
                {
                    stockDataReader.ReadDataFast(filePath, StockDataReader.FilterMode.FirstN, n1);
                }
                else if (mode == StockDataReader.FilterMode.IndexRange)
                {
                    stockDataReader.ReadDataFast(filePath, StockDataReader.FilterMode.IndexRange, n1, n2);
                }
                else if (mode == StockDataReader.FilterMode.AfterDateTime)
                {
                    stockDataReader.ReadDataFast(filePath, StockDataReader.FilterMode.AfterDateTime, dt1: dt1);
                }
                else if (mode == StockDataReader.FilterMode.BeforeDateTime)
                {
                    stockDataReader.ReadDataFast(filePath, StockDataReader.FilterMode.BeforeDateTime, dt1: dt1);
                }
                else if (mode == StockDataReader.FilterMode.DateTimeRange)
                {
                    stockDataReader.ReadDataFast(filePath, StockDataReader.FilterMode.DateTimeRange, dt1: dt1, dt2: dt2);
                }
                else
                {
                    stockDataReader.ReadDataFast(filePath);
                }

                stockDataReader.StopTimer();

                long t2 = stockDataReader.GetElapsedTimeMsec();

                LogManager.DisableConsoleSink();
                {
                    sb.Clear();
                    sb.Append("is completed in ");
                    sb.Append($"{t2} ms.");
                    LogManager.LogRaw(sb.ToString());

                    consoleLogger.Write("is completed in ");
                    consoleLogger.Write($"{t2}", ConsoleColor.Green);
                    consoleLogger.WriteLine(" ms.");

                    LogManager.EnableConsoleSink();
                }

                stockDataList = stockDataReader.GetData();          // tümü

                LogManager.LogRaw($"{"\n\tData count".PadRight(18)} : {stockDataReader.GetDataCount()}");

                if (addHeadTailInfo)
                {
                    LogManager.LogRaw("");
                    LogManager.LogRaw(stockDataReader.Head());

                    LogManager.LogRaw("");
                    LogManager.LogRaw(stockDataReader.Tail());
                }

                // --- WriteToCsvFile / WriteToTxtFile Kullanımları ---
                // Tüm data
                // stockDataReader.WriteToCsvFile(Path.Combine(AppSettings.OutputsDir, "data_all.csv"), stockDataReader.GetData());
                // stockDataReader.WriteToTxtFile(Path.Combine(AppSettings.OutputsDir, "data_all.txt"), stockDataReader.GetData());

                // İlk 100 kayıt
                // stockDataReader.WriteToCsvFile(Path.Combine(AppSettings.OutputsDir, "data_head.csv"), stockDataReader.GetData().Take(100).ToList());
                // stockDataReader.WriteToTxtFile(Path.Combine(AppSettings.OutputsDir, "data_head.txt"), stockDataReader.GetData().Take(100).ToList());

                // Son 50 kayıt
                // stockDataReader.WriteToCsvFile(Path.Combine(AppSettings.OutputsDir, "data_tail.csv"), stockDataReader.GetData().TakeLast(50).ToList());
                // stockDataReader.WriteToTxtFile(Path.Combine(AppSettings.OutputsDir, "data_tail.txt"), stockDataReader.GetData().TakeLast(50).ToList());

                // Belirli aralık (index 200-299)
                // stockDataReader.WriteToCsvFile(Path.Combine(AppSettings.OutputsDir, "data_range.csv"), stockDataReader.GetData(200, 299));
                // stockDataReader.WriteToTxtFile(Path.Combine(AppSettings.OutputsDir, "data_range.txt"), stockDataReader.GetData(200, 299));

                // Belirli bir tarihten sonrası (yyyy.MM.dd HH:mm:ss)
                // var afterDate = DateTime.ParseExact("2025.01.01 09:30:00", "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);
                // stockDataReader.WriteToCsvFile(Path.Combine(AppSettings.OutputsDir, "data_after.csv"), stockDataReader.GetData().Where(d => d.DateTime >= afterDate).ToList());
                // stockDataReader.WriteToTxtFile(Path.Combine(AppSettings.OutputsDir, "data_after.txt"), stockDataReader.GetData().Where(d => d.DateTime >= afterDate).ToList());

                // Belirli bir tarihten önce (yyyy.MM.dd)
                // var beforeDate = DateTime.ParseExact("2024.06.30", "yyyy.MM.dd", CultureInfo.InvariantCulture);
                // stockDataReader.WriteToCsvFile(Path.Combine(AppSettings.OutputsDir, "data_before.csv"), stockDataReader.GetData().Where(d => d.DateTime <= beforeDate).ToList());
                // stockDataReader.WriteToTxtFile(Path.Combine(AppSettings.OutputsDir, "data_before.txt"), stockDataReader.GetData().Where(d => d.DateTime <= beforeDate).ToList());

                // Tarih aralığı (yyyy.MM.dd HH:mm:ss)
                // var startDate = DateTime.ParseExact("2024.01.01 00:00:00", "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);
                // var endDate = DateTime.ParseExact("2024.12.31 23:59:59", "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);
                // stockDataReader.WriteToCsvFile(Path.Combine(AppSettings.OutputsDir, "data_daterange.csv"), stockDataReader.GetData().Where(d => d.DateTime >= startDate && d.DateTime <= endDate).ToList());
                // stockDataReader.WriteToTxtFile(Path.Combine(AppSettings.OutputsDir, "data_daterange.txt"), stockDataReader.GetData().Where(d => d.DateTime >= startDate && d.DateTime <= endDate).ToList());

                // Sadece saat bazlı filtreleme (HH:mm:ss)
                // var startTime = TimeSpan.ParseExact("09:30:00", "hh\\:mm\\:ss", CultureInfo.InvariantCulture);
                // var endTime = TimeSpan.ParseExact("15:00:00", "hh\\:mm\\:ss", CultureInfo.InvariantCulture);
                // stockDataReader.WriteToCsvFile(Path.Combine(AppSettings.OutputsDir, "data_time.csv"), stockDataReader.GetData().Where(d => d.Time >= startTime && d.Time <= endTime).ToList());
                // stockDataReader.WriteToTxtFile(Path.Combine(AppSettings.OutputsDir, "data_time.txt"), stockDataReader.GetData().Where(d => d.Time >= startTime && d.Time <= endTime).ToList());
            }
        }
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred while reading data: {ex.Message}", ex);
    }
    finally
    {
    }
}

async Task runSingleTraderAlgoTrade()
{
    try
    {
        if (!stockDataReader!.IsDataReady)
            return;

        if (stockMetaData == null) 
            return;

        LogManager.LogRaw("");
        LogManager.LogRaw($"Running AlgoTrader");

        algoTrader = new AlgoTrader("AlgoTrader");

        algoTrader.OnTraderProgress += OnTraderProgress;
        algoTrader.RegisterLogger(logger);
        algoTrader.RegisterTimer(timer);

        algoTrader.Reset();
        algoTrader.SetData(stockDataReader!.GetData());

        // Set symbol/system info from metadata
        algoTrader.SymbolName = stockMetaData.GetValueOrDefault("GrafikSembol", "N/A");
        algoTrader.SymbolPeriod = stockMetaData.GetValueOrDefault("GrafikPeriyot", "N/A");

        // Set run mode
        algoTrader.SingleTraderRunMode = selectedRunMode;

        if (algoTrader.SingleTraderRunMode == TraderRunMode.TradeOnly)
        {
            // Set strategy
            ConfigureStrategy(bShowConfigSelectionMenuSingleTrader);
        }
        else if (algoTrader.SingleTraderRunMode == TraderRunMode.TradeAndQuery)
        {
            // Set strategy
            ConfigureStrategy(bShowConfigSelectionMenuSingleTrader);

            // Set query
            ConfigureQuery(bShowConfigSelectionMenuSingleTrader);
        }
        else if (algoTrader.SingleTraderRunMode == TraderRunMode.QueryOnly)
        {
            // Set query
            ConfigureQuery(bShowConfigSelectionMenuSingleTrader);
        }

        ConfigureEquityCurveFilter(bShowConfigSelectionMenuSingleTrader);

        algoTrader.Initialize();

        var sb = algoTrader.GetDataInfo();
        LogManager.LogRaw("");
        LogManager.LogRaw(sb.ToString());

        await algoTrader.RunSingleTraderWithProgressAsync();

        LogManager.LogRaw("");
        if (algoTrader.SetupPython())
        {
            LogManager.LogRaw("");
            await algoTrader.PlotSingleTraderData(algoTrader.SingleTrader);
        }
        else
        {
            LogManager.LogError("Python setup failed. PlotSingleTraderData skipped.");
        }
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred while reading data: {ex.Message}", ex);
    }
    finally
    {
    }
}

async Task runMultipleTraderAlgoTrade()
{
    try
    {
        if (!stockDataReader!.IsDataReady)
            return;

        LogManager.LogRaw("");
        LogManager.LogRaw($"Running MultipleTrader AlgoTrader");

        algoTrader = new AlgoTrader("AlgoTrader");

        algoTrader.OnTraderProgress += OnTraderProgress;
        algoTrader.RegisterLogger(logger);
        algoTrader.RegisterTimer(timer);

        algoTrader.Reset();
        algoTrader.SetData(stockDataReader!.GetData());

        // Set symbol/system info from metadata
        if (stockMetaData != null)
        {
            algoTrader.SymbolName = stockMetaData.GetValueOrDefault("GrafikSembol", "N/A");
            algoTrader.SymbolPeriod = stockMetaData.GetValueOrDefault("GrafikPeriyot", "N/A");
        }

        // Set run mode
        algoTrader.SingleTraderRunMode = selectedRunMode;

        ConfigureStrategies();

        ConfigureQueries();

        ConfigureEquityCurveFilters();

        algoTrader.Initialize();

        var sb = algoTrader.GetDataInfo();
        LogManager.LogRaw("");
        LogManager.LogRaw(sb.ToString());

        await algoTrader.RunMultipleTraderWithProgressAsync();
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred while running MultipleTrader: {ex.Message}", ex);
    }
    finally
    {
    }
}

async Task runSingleTraderOptimization()
{
    try
    {
        if (!stockDataReader!.IsDataReady)
            return;

        LogManager.LogRaw("");
        LogManager.LogRaw($"Running SingleTraderOptimization");

        algoTrader = new AlgoTrader("AlgoTrader");

        algoTrader.OnTraderProgress += OnTraderProgress;
        algoTrader.RegisterLogger(logger);
        algoTrader.RegisterTimer(timer);

        algoTrader.Reset();
        algoTrader.SetData(stockDataReader!.GetData());

        // Set symbol/system info from metadata
        if (stockMetaData != null)
        {
            algoTrader.SymbolName = stockMetaData.GetValueOrDefault("GrafikSembol", "N/A");
            algoTrader.SymbolPeriod = stockMetaData.GetValueOrDefault("GrafikPeriyot", "N/A");
        }

        ConfigureEquityCurveFilter(bShowConfigSelectionMenuSingleTraderOpt);

        ConfigureOptimization(bShowConfigSelectionMenuSingleTraderOpt);

        algoTrader.Initialize();

        var sb = algoTrader.GetDataInfo();
        LogManager.LogRaw("");
        LogManager.LogRaw(sb.ToString());

        await algoTrader.RunSingleTraderOptWithProgressAsync();
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred while reading data: {ex.Message}", ex);
    }
    finally
    {
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
    if (!Directory.Exists(defaultDir))
        Directory.CreateDirectory(defaultDir);

    Console.Write($"\nScript dosya yolu (default: {defaultDir}\\): ");
    var filePath = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(filePath))
    {
        // List available scripts in default dir
        var files = Directory.GetFiles(defaultDir, "*.csx");
        if (files.Length == 0)
        {
            LogManager.LogRaw($"Dizinde script bulunamadi: {defaultDir}");
            return ("", "");
        }

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

    if (!File.Exists(filePath))
    {
        LogManager.LogRaw($"Dosya bulunamadi: {filePath}");
        return ("", "");
    }

    return (File.ReadAllText(filePath), filePath);
}

// TODO: Ileride silinecek - Moduler script yapisi (#load) ile artik gerek kalmadi
string readScriptFromConsole()
{
    Console.WriteLine("\nScript kodunu yapistirin (bos satir + ENTER ile bitirin):");
    Console.WriteLine("─────────────────────────────────────────────────────────");

    var lines = new List<string>();
    int emptyCount = 0;

    while (true)
    {
        var line = Console.ReadLine();
        if (line == null) break;

        if (string.IsNullOrWhiteSpace(line))
        {
            emptyCount++;
            if (emptyCount >= 2) break;  // 2 ardisik bos satir = bitis
            lines.Add(line);
        }
        else
        {
            emptyCount = 0;
            lines.Add(line);
        }
    }

    Console.WriteLine("─────────────────────────────────────────────────────────");
    return string.Join(Environment.NewLine, lines).TrimEnd();
}

async Task<ScriptExecutionResult> executeScriptWithCancellation(string code, ScriptGlobals globals, string? sourceDirectory = null)
{
    scriptCts = new CancellationTokenSource();

    // Script'e cancellation token'i aktar
    globals.SetCancellationTokenSource(scriptCts);

    // ESC dinle - ayri thread
    var escTask = Task.Run(() =>
    {
        while (!scriptCts.Token.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape)
                {
                    LogManager.LogRaw("\n[ESC] Script durdurma istegi gonderildi...", ConsoleColor.Yellow);
                    scriptCts.Cancel();
                    scriptExecutor.Cancel();
                    break;
                }
            }
            Thread.Sleep(100);
        }
    });

    LogManager.LogRaw("\n[INFO] Script calisiyor... (ESC ile durdurabilirsiniz)\n", ConsoleColor.Cyan);

    var result = await scriptExecutor.ExecuteAsync(code, globals, scriptCts.Token, sourceDirectory);

    scriptCts.Cancel();  // ESC listener'i durdur
    try { await escTask; } catch { }

    return result;
}

void printScriptResult(ScriptExecutionResult result)
{
    Console.WriteLine();
    if (result.Success)
    {
        LogManager.LogRaw($"[OK] Script basariyla tamamlandi ({result.ExecutionTime.TotalMilliseconds:F0} ms)", ConsoleColor.Green);
        if (result.ReturnValue != null)
            LogManager.LogRaw($"[RETURN] {result.ReturnValue}", ConsoleColor.Cyan);
    }
    else
    {
        if (result.CompilationErrors != null && result.CompilationErrors.Count > 0)
        {
            LogManager.LogRaw("[HATA] Derleme hatalari:", ConsoleColor.Red);
            foreach (var err in result.CompilationErrors)
                LogManager.LogRaw($"  {err}", ConsoleColor.Red);
        }
        else if (result.Error != null)
        {
            LogManager.LogRaw($"[HATA] {result.Error}", ConsoleColor.Red);
            if (result.StackTrace != null)
                LogManager.LogRaw($"[STACK] {result.StackTrace}", ConsoleColor.DarkYellow);
        }
    }
}

async Task runFullScript()
{
    try
    {
        var (code, scriptFilePath) = readScriptFromFile();
        if (string.IsNullOrEmpty(code))
        {
            LogManager.LogRaw("Script okunamadi veya bos.");
            return;
        }

        LogManager.LogRaw($"\nScript dosyasi: {scriptFilePath}");
        LogManager.LogRaw($"Script boyutu: {code.Length} karakter");

        var sourceDir = Path.GetDirectoryName(scriptFilePath);
        var scriptName = Path.GetFileNameWithoutExtension(scriptFilePath);

        // Full mode: yeni AlgoTrader olustur, script her seyi kendisi yapar
        var scriptAlgoTrader = new AlgoTrader(scriptName);
        scriptAlgoTrader.RegisterLogger(logger);
        scriptAlgoTrader.RegisterTimer(timer);

        var globals = new ScriptGlobals(
            scriptAlgoTrader,
            stockDataList ?? new List<StockData>(),
            msg => LogManager.LogRaw(msg),
            (key, val) => LogManager.LogRaw($"[RESULT] {key}: {val}")
        );

        var result = await executeScriptWithCancellation(code, globals, sourceDir);

        globals.Cleanup();
        printScriptResult(result);

        scriptAlgoTrader.Dispose();
    }
    catch (Exception ex)
    {
        LogManager.LogError($"Script calistirma hatasi: {ex.Message}", ex);
    }
}

// TODO: Ileride silinecek - Moduler script yapisi (#load) ile artik gerek kalmadi, menuден de kaldirildi
async Task runInteractiveScript()
{
    try
    {
        if (algoTrader == null)
        {
            LogManager.LogRaw("\n[UYARI] AlgoTrader henuz olusturulmadi. Once menu [2] veya [3] calistirin,");
            LogManager.LogRaw("        veya bu script icinde algoTrader'i kendiniz konfigure edin.");

            // Yine de bos algoTrader ile devam etsin mi?
            algoTrader = new AlgoTrader("InteractiveAlgoTrader");
            algoTrader.RegisterLogger(logger);
            algoTrader.RegisterTimer(timer);

            if (stockDataList != null && stockDataList.Count > 0)
            {
                algoTrader.SetData(stockDataList);
                LogManager.LogRaw($"[INFO] Mevcut stockData ({stockDataList.Count} bar) AlgoTrader'a atandi.");
            }
        }

        var code = readScriptFromConsole();
        if (string.IsNullOrEmpty(code))
        {
            LogManager.LogRaw("Script bos.");
            return;
        }

        LogManager.LogRaw($"\nScript boyutu: {code.Length} karakter");

        var globals = new ScriptGlobals(
            algoTrader,
            stockDataList ?? new List<StockData>(),
            msg => LogManager.LogRaw(msg),
            (key, val) => LogManager.LogRaw($"[RESULT] {key}: {val}")
        );

        var result = await executeScriptWithCancellation(code, globals);

        globals.Cleanup();
        printScriptResult(result);
    }
    catch (Exception ex)
    {
        LogManager.LogError($"Script calistirma hatasi: {ex.Message}", ex);
    }
}

// TODO: Ileride silinecek - runFullScript() ile merge edildi
// async Task runFullScriptMultipleTrader() { ... }

// TODO: Ileride silinecek - Moduler script yapisi (#load) ile artik gerek kalmadi, menuden de kaldirildi
async Task runInteractiveScriptMultipleTrader()
{
    try
    {
        if (algoTrader == null)
        {
            LogManager.LogRaw("\n[UYARI] AlgoTrader henuz olusturulmadi. Once menu [6] veya [7] calistirin,");
            LogManager.LogRaw("        veya bu script icinde algoTrader'i kendiniz konfigure edin.");

            algoTrader = new AlgoTrader("InteractiveMultipleTrader");
            algoTrader.RegisterLogger(logger);
            algoTrader.RegisterTimer(timer);

            if (stockDataList != null && stockDataList.Count > 0)
            {
                algoTrader.SetData(stockDataList);
                LogManager.LogRaw($"[INFO] Mevcut stockData ({stockDataList.Count} bar) AlgoTrader'a atandi.");
            }
        }

        var code = readScriptFromConsole();
        if (string.IsNullOrEmpty(code))
        {
            LogManager.LogRaw("Script bos.");
            return;
        }

        LogManager.LogRaw($"\nScript boyutu: {code.Length} karakter");

        var globals = new ScriptGlobals(
            algoTrader,
            stockDataList ?? new List<StockData>(),
            msg => LogManager.LogRaw(msg),
            (key, val) => LogManager.LogRaw($"[RESULT] {key}: {val}")
        );

        var result = await executeScriptWithCancellation(code, globals);

        globals.Cleanup();
        printScriptResult(result);
    }
    catch (Exception ex)
    {
        LogManager.LogError($"Script calistirma hatasi: {ex.Message}", ex);
    }
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
        if (Console.KeyAvailable)
        {
            input = Console.ReadLine()?.Trim();
            break;
        }
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
        _ => TraderRunMode.TradeOnly
    };
}

void showMainMenu()
{
    Console.WriteLine();
    Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║        AlgoTrade - Main Menu                                       ║");
    Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [1] Read Stock Data                                             ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║        [2] Run SingleTrader With Progress                          ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║        [3] Run MultipleTrader With Progress                        ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║        [4] Run SingleTraderOpt With Progress                       ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [5] Read Data + Run SingleTrader With Progress                  ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [6] Read Data + Run MultipleTrader With Progress                ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [7] Read Data + Run SingleTraderOpt With Progress               ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [8] Run Full Script (from file)                                 ║", ConsoleColor.Green);
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("║    [0] Exit                                                        ║");
    Console.WriteLine("║                                                                    ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");
    Console.Write("\nSeçiminiz (default: 8): ");
}

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
    
    LogManager.LogRaw("");
    LogManager.LogRaw(AppSettings.LogsDir + " cleared...");

    bool running = true;
    while (running)
    {
        showMainMenu();
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input)) input = "8";

        switch (input)
        {
            case "1":
                readStockData();
                break;
            case "2":
                selectedRunMode = bShowRunModeMenu ? showRunModeMenu() : TraderRunMode.TradeOnly;
                await runSingleTraderAlgoTrade();
                break;
            case "3":
                selectedRunMode = bShowRunModeMenu ? showRunModeMenu() : TraderRunMode.TradeOnly;
                await runMultipleTraderAlgoTrade();
                break;
            case "4":
                await runSingleTraderOptimization();
                break;
            case "5":
                selectedRunMode = bShowRunModeMenu ? showRunModeMenu() : TraderRunMode.TradeOnly;
                readStockData();
                await runSingleTraderAlgoTrade();
                break;
            case "6":
                selectedRunMode = bShowRunModeMenu ? showRunModeMenu() : TraderRunMode.TradeOnly;
                readStockData();
                await runMultipleTraderAlgoTrade();
                break;
            case "7":
                readStockData();
                await runSingleTraderOptimization();
                break;
            case "8":
                await runFullScript();
                break;
            case "0":
                running = false;
                break;
            default:
                Console.WriteLine("Geçersiz seçim!");
                break;
        }
    }

    LogManager.LogRaw("Application finished", ConsoleColor.Green);

    algoTrader?.Dispose();
    stockDataReader?.Dispose();
    LogManager.Instance.Dispose();

    algoTrader = null;
    stockDataReader = null;
    stockDataList = null;
    stockMetaData = null;
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

