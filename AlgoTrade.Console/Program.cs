using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Logging.Sinks;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Trading;
using ScottPlot.Colormaps;
using System;
using System.Collections.Concurrent;
using System.Text;
using static Nessos.LinqOptimizer.Core.QueryExpr;

var consoleLogger = new ConsoleLogger();
var sb = new StringBuilder();
bool addHeadTailInfo = false;

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

void main()
{
    /*
    var trader = new AlgoTrader("MyStrategy");

    trader.MessageReceived += message => Console.WriteLine(message);

    trader.Start();
    Thread.Sleep(10);
    trader.Stop();
    */


    AppSettings.EnsureDirectories();

    // ====================================================================
    // LogManager Setup
    // ====================================================================
    LogManager.Instance.RegisterSink(new ConsoleSink());
    LogManager.Instance.RegisterSink(new DebugSink());
    LogManager.Instance.RegisterSink(new FileSink(AppSettings.LogsDir, "app.log"));
    // LogManager.DisableTimestamp(); LogManager.DisableLevel(); LogManager.DisableSource();
    consoleLogger = LogManager.GetConsoleLogger();
    consoleLogger.Clear();

    LogManager.LogRaw("Application started", ConsoleColor.Green);

    string stockDataFullFileName = "C:\\data\\csvFiles\\VIP\\01\\VIP-X030-T.csv";

    StockDataReader? stockDataReader = null;
    List<StockData>? stockDataList = null;
    ConcurrentDictionary<string, string>? stockMetaData = null;

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
                stockDataReader.WriteToCsvFile(Path.Combine(AppSettings.OutputsDir, "data_all.csv"), stockDataReader.GetData());
                stockDataReader.WriteToTxtFile(Path.Combine(AppSettings.OutputsDir, "data_all.txt"), stockDataReader.GetData());

                // İlk 100 kayıt
                stockDataReader.WriteToCsvFile(Path.Combine(AppSettings.OutputsDir, "data_head.csv"), stockDataReader.GetData().Take(100).ToList());
                stockDataReader.WriteToTxtFile(Path.Combine(AppSettings.OutputsDir, "data_head.txt"), stockDataReader.GetData().Take(100).ToList());

                // Son 50 kayıt
                stockDataReader.WriteToCsvFile(Path.Combine(AppSettings.OutputsDir, "data_tail.csv"), stockDataReader.GetData().TakeLast(50).ToList());
                stockDataReader.WriteToTxtFile(Path.Combine(AppSettings.OutputsDir, "data_tail.txt"), stockDataReader.GetData().TakeLast(50).ToList());

                // Belirli aralık (index 200-299)
                stockDataReader.WriteToCsvFile(Path.Combine(AppSettings.OutputsDir, "data_range.csv"), stockDataReader.GetData(200, 299));
                stockDataReader.WriteToTxtFile(Path.Combine(AppSettings.OutputsDir, "data_range.txt"), stockDataReader.GetData(200, 299));

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
        stockDataReader?.Dispose();
        stockDataReader = null;
        stockDataList = null;
        stockMetaData = null;
    }

    LogManager.LogRaw("");
    LogManager.LogRaw("Application finished", ConsoleColor.Green);

    LogManager.LogRaw("\nÇıkmak için bir tuşa basın...");
    Console.ReadKey();

    LogManager.Instance.Dispose();
}

main();

