using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Logging.Sinks;
using AlgoTrade.Core.Scripting;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Timer;
using AlgoTrade.Core.Trading;

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

void ConfigureStrategy()
{
    if (algoTrader is null)
        throw new InvalidOperationException("AlgoTrader instance is null.");

    string configPath = Path.Combine(AppSettings.ConfigsDir, "StrategyConfig.txt");

    if (File.Exists(configPath))
    {
        algoTrader.ConfigureStrategyFromConfig(configPath, "SimpleMostStrategy", "v1-Default");
        LogManager.LogRaw($"\nStrategy loaded from config: {configPath}");
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

void ConfigureQuery()
{
    if (algoTrader is null)
        throw new InvalidOperationException("AlgoTrader instance is null.");

    string configPath = Path.Combine(AppSettings.ConfigsDir, "QueryConfig.txt");

    if (File.Exists(configPath))
    {
        algoTrader.ConfigureQueryFromConfig(configPath, "SimpleQuery1", "v1-Default");
        LogManager.LogRaw($"\nQuery loaded from config: {configPath}");
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

void ConfigureEquityCurveFilter()
{
    algoTrader.EquityCurveFilteringEnabled = false;
    algoTrader.ThresholdTypeIsPercent = true;
    if (algoTrader.ThresholdTypeIsPercent)
    {
        algoTrader.ProfitConfirmationThreshold = 0.05;
        algoTrader.LossConfirmationThreshold = -0.05;
    }
    else
    {
        algoTrader.ProfitConfirmationThreshold = 1000;
        algoTrader.LossConfirmationThreshold = -1000;
    }
    algoTrader.ConfirmationTrigger = ConfirmationTrigger.Both;
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

async Task runAlgoTrade()
{
    try
    {
        if (!stockDataReader!.IsDataReady)
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
        if (stockMetaData != null)
        {
            algoTrader.SymbolName = stockMetaData.GetValueOrDefault("GrafikSembol", "N/A");
            algoTrader.SymbolPeriod = stockMetaData.GetValueOrDefault("GrafikPeriyot", "N/A");
        }

        // Set run mode
        algoTrader.SingleTraderRunMode = selectedRunMode;

        if (algoTrader.SingleTraderRunMode == TraderRunMode.TradeOnly)
        {
            // Set strategy
            ConfigureStrategy();
        }
        else if (algoTrader.SingleTraderRunMode == TraderRunMode.TradeAndQuery)
        {
            // Set strategy
            ConfigureStrategy();

            // Set query
            ConfigureQuery();
        }
        else if (algoTrader.SingleTraderRunMode == TraderRunMode.QueryOnly)
        {
            // Set query
            ConfigureQuery();
        }

        ConfigureEquityCurveFilter();

        algoTrader.Initialize();

        var sb = algoTrader.GetDataInfo();
        LogManager.LogRaw("");
        LogManager.LogRaw(sb.ToString());

        await algoTrader.RunSingleTraderWithProgressAsync();
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

string readScriptFromFile()
{
    string defaultDir = Path.Combine(AppSettings.InputsDir, "scripts");
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
            return "";
        }

        Console.WriteLine("\nMevcut scriptler:");
        for (int idx = 0; idx < files.Length; idx++)
            Console.WriteLine($"  [{idx + 1}] {Path.GetFileName(files[idx])}");

        Console.Write("\nSeçiminiz: ");
        var choice = Console.ReadLine()?.Trim();
        if (int.TryParse(choice, out int sel) && sel >= 1 && sel <= files.Length)
            filePath = files[sel - 1];
        else
            return "";
    }

    if (!File.Exists(filePath))
    {
        LogManager.LogRaw($"Dosya bulunamadi: {filePath}");
        return "";
    }

    return File.ReadAllText(filePath);
}

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

async Task<ScriptExecutionResult> executeScriptWithCancellation(string code, ScriptGlobals globals)
{
    scriptCts = new CancellationTokenSource();

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

    var result = await scriptExecutor.ExecuteAsync(code, globals, scriptCts.Token);

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
        var code = readScriptFromFile();
        if (string.IsNullOrEmpty(code))
        {
            LogManager.LogRaw("Script okunamadi veya bos.");
            return;
        }

        LogManager.LogRaw($"\nScript boyutu: {code.Length} karakter");

        // Full mode: yeni AlgoTrader olustur, script her seyi kendisi yapar
        var scriptAlgoTrader = new AlgoTrader("ScriptAlgoTrader");
        scriptAlgoTrader.RegisterLogger(logger);
        scriptAlgoTrader.RegisterTimer(timer);

        var globals = new ScriptGlobals(
            scriptAlgoTrader,
            stockDataList ?? new List<StockData>(),
            msg => LogManager.LogRaw(msg),
            (key, val) => LogManager.LogRaw($"[RESULT] {key}: {val}")
        );

        var result = await executeScriptWithCancellation(code, globals);

        globals.Cleanup();
        printScriptResult(result);

        scriptAlgoTrader.Dispose();
    }
    catch (Exception ex)
    {
        LogManager.LogError($"Script calistirma hatasi: {ex.Message}", ex);
    }
}

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

TraderRunMode showRunModeMenu()
{
    Console.WriteLine();
    Console.WriteLine("Run Mode Seçimi:");
    Console.WriteLine("  [1] TradeOnly");
    Console.WriteLine("  [2] TradeAndQuery");
    Console.WriteLine("  [3] QueryOnly");
    Console.Write("\nSeçiminiz (default: 2): ");

    var input = Console.ReadLine()?.Trim();
    return input switch
    {
        "1" => TraderRunMode.TradeOnly,
        "3" => TraderRunMode.QueryOnly,
        _ => TraderRunMode.TradeAndQuery
    };
}

void showMainMenu()
{
    Console.WriteLine();
    Console.WriteLine("╔═════════════════════════════════════════════════════╗");
    Console.WriteLine("║        AlgoTrade - Ana Menü (Script)                ║");
    Console.WriteLine("╠═════════════════════════════════════════════════════╣");
    Console.WriteLine("║  [1] Read Stock Data                                ║");
    Console.WriteLine("║  [2] Run SingleTrader With Progress                 ║");
    Console.WriteLine("║  [3] Read Data + Run SingleTrader With Progress     ║");
    Console.WriteLine("║  [4] Run Full Script (from file)                    ║");
    Console.WriteLine("║  [5] Run Interactive Script (console paste)         ║");
    Console.WriteLine("║  [0] Çıkış                                          ║");
    Console.WriteLine("╚═════════════════════════════════════════════════════╝");
    Console.Write("\nSeçiminiz: ");
}

async Task main()
{
    AppSettings.EnsureDirectories();

    logger.RegisterSink(new ConsoleSink());
    logger.RegisterSink(new DebugSink());
    logger.RegisterSink(new FileSink(AppSettings.LogsDir, "app.log"));
    consoleLogger = LogManager.GetConsoleLogger();
    consoleLogger.Clear();

    LogManager.LogRaw("Application started", ConsoleColor.Green);

    bool running = true;
    while (running)
    {
        showMainMenu();
        var input = Console.ReadLine()?.Trim();

        switch (input)
        {
            case "1":
                readStockData();
                break;
            case "2":
                selectedRunMode = showRunModeMenu();
                await runAlgoTrade();
                break;
            case "3":
                selectedRunMode = showRunModeMenu();
                readStockData();
                await runAlgoTrade();
                break;
            case "4":
                await runFullScript();
                break;
            case "5":
                await runInteractiveScript();
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

Console.WriteLine("[mainScript.csx] Script basariyla yuklendi ve calisti!");
await main();
