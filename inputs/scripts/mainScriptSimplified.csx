using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Logging.Sinks;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Timer;
using AlgoTrade.Core.Trading;

// =============================================================================
// Ayarlar
// =============================================================================
string stockDataFullFileName = "C:\\data\\csvFiles\\VIP\\01\\VIP-X030-T.csv";
TraderRunMode selectedRunMode = TraderRunMode.TradeAndQuery;
bool addHeadTailInfo = false;

// =============================================================================
// Degiskenler
// =============================================================================
var sb = new StringBuilder();
ConsoleLogger? consoleLogger = null;
List<StockData>? stockDataList = null;
StockDataReader? stockDataReader = null;
ConcurrentDictionary<string, string>? stockMetaData = null;
AlgoTrader? algoTrader = null;
TimeManager timer = TimeManager.GetInstance();
LogManager logger = LogManager.GetInstance();

// =============================================================================
// Event Handlers
// =============================================================================
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

// =============================================================================
// Configure
// =============================================================================
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
            ["mostMaMethod"] = "EMA",
            ["priceSource"] = "Close",
            ["signalModeIndex"] = 0
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

// =============================================================================
// Read Stock Data
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
        stockDataReader.OnReadData += OnReadData;
        stockDataReader.OnProgress += OnProgress;

        string filePath = stockDataFullFileName;

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

        if (!stockDataReader.IsMetaDataRead)
            return;

        LogManager.LogRaw("");
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
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred while reading data: {ex.Message}", ex);
    }
}

// =============================================================================
// Run AlgoTrader
// =============================================================================
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

        if (stockMetaData != null)
        {
            algoTrader.SymbolName = stockMetaData.GetValueOrDefault("GrafikSembol", "N/A");
            algoTrader.SymbolPeriod = stockMetaData.GetValueOrDefault("GrafikPeriyot", "N/A");
        }

        algoTrader.SingleTraderRunMode = selectedRunMode;

        if (algoTrader.SingleTraderRunMode == TraderRunMode.TradeOnly)
        {
            ConfigureStrategy();
        }
        else if (algoTrader.SingleTraderRunMode == TraderRunMode.TradeAndQuery)
        {
            ConfigureStrategy();
            ConfigureQuery();
        }
        else if (algoTrader.SingleTraderRunMode == TraderRunMode.QueryOnly)
        {
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
        LogManager.LogError($"An error occurred while running AlgoTrader: {ex.Message}", ex);
    }
}

// =============================================================================
// Ana Akis: Oku -> Calistir -> Temizle
// =============================================================================
AppSettings.EnsureDirectories();

logger.RegisterSink(new ConsoleSink());
logger.RegisterSink(new DebugSink());
logger.RegisterSink(new FileSink(AppSettings.LogsDir, "app.log"));
consoleLogger = LogManager.GetConsoleLogger();

LogManager.LogRaw("[mainScriptSimplified.csx] Baslatiliyor...", ConsoleColor.Green);

readStockData();
await runAlgoTrade();

LogManager.LogRaw("[mainScriptSimplified.csx] Tamamlandi.", ConsoleColor.Green);

algoTrader?.Dispose();
stockDataReader?.Dispose();
