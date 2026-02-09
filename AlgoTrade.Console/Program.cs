using AlgoTrade.Core;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Trading;
using System;
using System.Collections.Concurrent;
using System.Text;
using static Nessos.LinqOptimizer.Core.QueryExpr;

AppSettings.EnsureDirectories();

Console.Clear();
Console.WriteLine("#######################################\n");

/*
var trader = new AlgoTrader("MyStrategy");

trader.MessageReceived += message => Console.WriteLine(message);

trader.Start();
Thread.Sleep(10);
trader.Stop();
*/

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
        var sb = new StringBuilder();
        sb.AppendLine($"{"\tKayit Zamani".PadRight(padding)}: {kayitZamani}");
        sb.AppendLine($"{"\tGrafikSembol".PadRight(padding)}: {grafikSembol}");
        sb.AppendLine($"{"\tGrafikPeriyot".PadRight(padding)}: {grafikPeriyot}");
        sb.AppendLine($"{"\tBarCount".PadRight(padding)}: {barCount}");
        sb.AppendLine($"{"\tBaslangic Tarihi".PadRight(padding)}: {baslangicTarihi}");
        sb.AppendLine($"{"\tBitis Tarihi".PadRight(padding)}: {bitisTarihi}");
        sb.AppendLine($"{"\tFormat".PadRight(padding)}: {format}");
        Console.Write(sb.ToString());
    }
}

void OnProgress(StockDataReader sender, int count, bool isCompleted)
{
    if (isCompleted) {
        Console.Write($"\r\tRecord count     : {count}");
        Console.WriteLine("");
    }
    else
    {
        Console.Write($"\r\tRecord no        : {count}");
    }
}

void OnReadData(StockDataReader sender, List<StockData> data, long elapsedMs)
{
    //Console.WriteLine($"\tData okundu - {data.Count} kayit, {elapsedMs} ms");
}

string stockDataFullFileName = "C:\\data\\csvFiles\\VIP\\01\\VIP-X030-T.csv";

StockDataReader? stockDataReader = null;
List<StockData>? stockDataList = null;
ConcurrentDictionary<string, string>? stockMetaData = null;

try
{
    if (!File.Exists(stockDataFullFileName))
    {
        Console.WriteLine($"File does not exist : {stockDataFullFileName}");
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

        Console.WriteLine("");
        Console.WriteLine($"Reading Meta Data from   : {filePath}");

        stockDataReader.Clear();

        stockDataReader.ReStartTimer();

        stockMetaData = stockDataReader.ReadMetaData(filePath);

        stockDataReader.StopTimer();

        long t1 = stockDataReader.GetElapsedTimeMsec();

        Console.Write("is completed in ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(t1);
        Console.ResetColor();
        Console.WriteLine(" ms.");

        if (stockDataReader.IsMetaDataRead)
        {
            var barCount        = stockMetaData.GetValueOrDefault("BarCount", "N/A");
            var baslangicTarihi = stockMetaData.GetValueOrDefault("Baslangic_Tarihi", "N/A");
            var bitisTarihi     = stockMetaData.GetValueOrDefault("Bitis_Tarihi", "N/A");

            bool useMenu = false;

            Console.WriteLine("");

            StockDataReader.FilterMode mode = StockDataReader.FilterMode.All;

            int n1 = 1000;
            int n2 = int.TryParse(barCount, out var bc) ? bc : 0;
            DateTime? dt1 = DateTime.TryParseExact(baslangicTarihi, "yyyy.MM.dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var d1) ? d1 : null;
            DateTime? dt2 = DateTime.TryParseExact(bitisTarihi, "yyyy.MM.dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var d2) ? d2 : null;

            Console.WriteLine($"Loading data from        : {filePath}");

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

            Console.Write("is completed in ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(t2);
            Console.ResetColor();
            Console.WriteLine(" ms.");

            stockDataList = stockDataReader.GetData();          // tümü

            Console.WriteLine("");
            Console.Write($"Data count : {stockDataReader.GetDataCount()}");
            Console.WriteLine("");

            Console.WriteLine("");
            Console.WriteLine(stockDataReader.Head());
            Console.WriteLine(stockDataReader.Tail());
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred while reading data: {ex.Message}");
}
finally
{
    stockDataReader?.Dispose();
    stockDataReader = null;
    stockDataList = null;
    stockMetaData = null;
}

Console.WriteLine("\n#######################################\n");
Console.WriteLine("\nÇıkmak için bir tuşa basın...");
Console.ReadKey();