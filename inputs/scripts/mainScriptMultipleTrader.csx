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
}

void OnTraderProgress(int currentBar, int totalBars, double percentage)
{
}

void ConfigureStrategies()
{
    if (algoTrader is null)
        throw new InvalidOperationException("AlgoTrader instance is null.");

    algoTrader.ClearStrategyConfigs();

    // Id=0 : SimpleMostStrategy
    algoTrader.AddStrategyConfig(0, "SimpleMostStrategy", new Dictionary<string, object>
    {
        ["period"] = 21,
        ["percent"] = 1.0,
        ["mostMaMethod"] = "EMA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=1 : SimpleMAStrategy
    algoTrader.AddStrategyConfig(1, "SimpleMAStrategy", new Dictionary<string, object>
    {
        ["fastPeriod"] = 10,
        ["slowPeriod"] = 20,
        ["fastMaMethod"] = "EMA",
        ["slowMaMethod"] = "EMA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=2 : SimpleRSIStrategy
    algoTrader.AddStrategyConfig(2, "SimpleRSIStrategy", new Dictionary<string, object>
    {
        ["period"] = 14,
        ["oversold"] = 30,
        ["overbought"] = 70,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=3 : SimpleOTTStrategy
    algoTrader.AddStrategyConfig(3, "SimpleOTTStrategy", new Dictionary<string, object>
    {
        ["period"] = 2,
        ["percent"] = 1.4,
        ["ottMaMethod"] = "VIDYA",
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=4 : SimpleSuperTrendStrategy
    algoTrader.AddStrategyConfig(4, "SimpleSuperTrendStrategy", new Dictionary<string, object>
    {
        ["period"] = 10,
        ["multiplier"] = 3.0,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=5 : SimpleParabolicSARStrategy
    algoTrader.AddStrategyConfig(5, "SimpleParabolicSARStrategy", new Dictionary<string, object>
    {
        ["step"] = 0.02,
        ["max"] = 0.2,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=6 : SimpleADXStrategy
    algoTrader.AddStrategyConfig(6, "SimpleADXStrategy", new Dictionary<string, object>
    {
        ["period"] = 14,
        ["adxThreshold"] = 25,
        ["signalModeIndex"] = 0
    });

    // Id=7 : SimpleDIStrategy
    algoTrader.AddStrategyConfig(7, "SimpleDIStrategy", new Dictionary<string, object>
    {
        ["period"] = 14,
        ["signalModeIndex"] = 0
    });

    // Id=8 : SimpleMACDStrategy
    algoTrader.AddStrategyConfig(8, "SimpleMACDStrategy", new Dictionary<string, object>
    {
        ["fastPeriod"] = 12,
        ["slowPeriod"] = 26,
        ["signalPeriod"] = 9,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=9 : SimpleStochasticStrategy
    algoTrader.AddStrategyConfig(9, "SimpleStochasticStrategy", new Dictionary<string, object>
    {
        ["kPeriod"] = 14,
        ["dPeriod"] = 3,
        ["centerLine"] = 50,
        ["signalModeIndex"] = 0
    });

    // Id=10 : SimpleBollingerStrategy
    algoTrader.AddStrategyConfig(10, "SimpleBollingerStrategy", new Dictionary<string, object>
    {
        ["period"] = 20,
        ["multiplier"] = 2.0,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=11 : SimpleATRStrategy
    algoTrader.AddStrategyConfig(11, "SimpleATRStrategy", new Dictionary<string, object>
    {
        ["atrPeriod"] = 14,
        ["maPeriod"] = 20,
        ["multiplier"] = 2.0,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=12 : SimpleCMFStrategy
    algoTrader.AddStrategyConfig(12, "SimpleCMFStrategy", new Dictionary<string, object>
    {
        ["period"] = 20,
        ["positiveThreshold"] = 0.1,
        ["negativeThreshold"] = -0.1,
        ["signalModeIndex"] = 0
    });

    // Id=13 : SimpleMFIStrategy
    algoTrader.AddStrategyConfig(13, "SimpleMFIStrategy", new Dictionary<string, object>
    {
        ["period"] = 14,
        ["oversold"] = 20,
        ["overbought"] = 80,
        ["signalModeIndex"] = 0
    });

    // Id=14 : SimpleKairiStrategy
    algoTrader.AddStrategyConfig(14, "SimpleKairiStrategy", new Dictionary<string, object>
    {
        ["period"] = 20,
        ["positiveThreshold"] = 5,
        ["negativeThreshold"] = -5,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=15 : SimpleMomentumStrategy
    algoTrader.AddStrategyConfig(15, "SimpleMomentumStrategy", new Dictionary<string, object>
    {
        ["period"] = 12,
        ["positiveThreshold"] = 0,
        ["negativeThreshold"] = 0,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=16 : SimpleHHVLLVStrategy
    algoTrader.AddStrategyConfig(16, "SimpleHHVLLVStrategy", new Dictionary<string, object>
    {
        ["period"] = 20,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=17 : SimpleHYLYStrategy
    algoTrader.AddStrategyConfig(17, "SimpleHYLYStrategy", new Dictionary<string, object>
    {
        ["period"] = 20,
        ["threshold"] = 80,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=18 : SimpleIchimokuStrategy
    algoTrader.AddStrategyConfig(18, "SimpleIchimokuStrategy", new Dictionary<string, object>
    {
        ["tenkanPeriod"] = 9,
        ["kijunPeriod"] = 26,
        ["senkouPeriod"] = 52,
        ["signalModeIndex"] = 0
    });

    // Id=19 : SimpleMavilimWStrategy
    algoTrader.AddStrategyConfig(19, "SimpleMavilimWStrategy", new Dictionary<string, object>
    {
        ["param1"] = 3,
        ["param2"] = 5,
        ["signalModeIndex"] = 0
    });

    // Id=20 : SimplePMaxStrategy
    algoTrader.AddStrategyConfig(20, "SimplePMaxStrategy", new Dictionary<string, object>
    {
        ["atrPeriod"] = 10,
        ["multiplier"] = 3.0,
        ["maPeriod"] = 10,
        ["pmaxMaMethod"] = "EMA",
        ["signalModeIndex"] = 0
    });

    // Id=21 : SimpleTillsonT3Strategy
    algoTrader.AddStrategyConfig(21, "SimpleTillsonT3Strategy", new Dictionary<string, object>
    {
        ["period"] = 5,
        ["priceSource"] = "Close",
        ["signalModeIndex"] = 0
    });

    // Id=22 : SimpleAlphaTrendStrategy
    algoTrader.AddStrategyConfig(22, "SimpleAlphaTrendStrategy", new Dictionary<string, object>
    {
        ["atrPeriod"] = 14,
        ["coefficient"] = 1.0,
        ["momentumPeriod"] = 14,
        ["useMFI"] = true,
        ["signalModeIndex"] = 0
    });
}

void ConfigureQueries()
{
    if (algoTrader is null)
        throw new InvalidOperationException("AlgoTrader instance is null.");

    algoTrader.ClearQueryConfigs();

    // Id=0 : SimpleQuery1
    algoTrader.AddQueryConfig(0, "SimpleQuery1", new Dictionary<string, object>
    {
        ["ma8Period"] = 8,
        ["ma200Period"] = 200,
        ["choice"] = 0
    });

    // Id=1 : SimpleQuery1 (farklı parametrelerle)
    algoTrader.AddQueryConfig(1, "SimpleQuery1", new Dictionary<string, object>
    {
        ["ma8Period"] = 5,
        ["ma200Period"] = 100,
        ["choice"] = 0
    });
}

void ConfigureEquityCurveFilters()
{
    if (algoTrader is null)
        throw new InvalidOperationException("AlgoTrader instance is null.");

    algoTrader.ClearEquityCurveFilterConfigs();

    // Id=0
    algoTrader.AddEquityCurveFilterConfig(0,
        enabled: false,
        thresholdTypeIsPercent: true,
        profitThreshold: 0.05,
        lossThreshold: -0.05,
        trigger: ConfirmationTrigger.Both);

    // Id=1
    algoTrader.AddEquityCurveFilterConfig(1,
        enabled: false,
        thresholdTypeIsPercent: true,
        profitThreshold: 0.05,
        lossThreshold: -0.05,
        trigger: ConfirmationTrigger.Both);
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

                LogManager.LogRaw("");

                StockDataReader.FilterMode mode = StockDataReader.FilterMode.All;

                LogManager.LogRaw($"Loading data from        : {filePath}");

                stockDataReader.ReStartTimer();

                stockDataReader.ReadDataFast(filePath);

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

TraderRunMode showRunModeMenu()
{
    Console.WriteLine();
    Console.WriteLine("Run Mode Secimi:");
    Console.WriteLine("  [1] TradeOnly");
    Console.WriteLine("  [2] TradeAndQuery");
    Console.WriteLine("  [3] QueryOnly");
    Console.Write("\nSeciminiz (default: 2): ");

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
    Console.WriteLine("║   AlgoTrade - MultipleTrader Script Menu            ║");
    Console.WriteLine("╠═════════════════════════════════════════════════════╣");
    Console.WriteLine("║  [1] Read Stock Data                                ║");
    Console.WriteLine("║  [2] Run MultipleTrader With Progress               ║");
    Console.WriteLine("║  [3] Read Data + Run MultipleTrader With Progress   ║");
    Console.WriteLine("║  [0] Cikis                                          ║");
    Console.WriteLine("╚═════════════════════════════════════════════════════╝");
    Console.Write("\nSeciminiz (default: 3): ");
}

async Task main()
{
    AppSettings.EnsureDirectories();

    logger.RegisterSink(new ConsoleSink());
    logger.RegisterSink(new DebugSink());
    logger.RegisterSink(new FileSink(AppSettings.LogsDir, "app.log"));
    consoleLogger = LogManager.GetConsoleLogger();
    consoleLogger.Clear();

    LogManager.LogRaw("Application started (MultipleTrader Script)", ConsoleColor.Green);

    bool running = true;
    while (running)
    {
        showMainMenu();
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input)) input = "3";

        switch (input)
        {
            case "1":
                readStockData();
                break;
            case "2":
                selectedRunMode = showRunModeMenu();
                await runMultipleTraderAlgoTrade();
                break;
            case "3":
                selectedRunMode = showRunModeMenu();
                readStockData();
                await runMultipleTraderAlgoTrade();
                break;
            case "0":
                running = false;
                break;
            default:
                Console.WriteLine("Gecersiz secim!");
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

Console.WriteLine("[mainScriptMultipleTrader.csx] Script basariyla yuklendi ve calisti!");
await main();
