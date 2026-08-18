using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Queries;
using AlgoTrade.Core.Trading.Query;
using static AlgoTrade.Core.StockDataReader.StockDataReader;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Aynı sorguyu, aynı sembolün birden fazla zaman diliminde bağımsız olarak çalıştırıp
/// sonuçları tek bir özet tabloda toplar (bkz. docs/tarama-motoru-plan.md — Sorgu Tarama
/// Matrisi, Senaryo 2). `TimeframeScanner`'ın (Yapı Taşı A, Strateji tarafı) birebir Sorgu
/// karşılığı — `QuerySymbolScanner`'la aynı desende (`SingleTrader.RunMode = QueryOnly`,
/// dinamik sorgu kolonları), tek fark sembol listesi yerine tek bir sembolün N farklı
/// zaman-dilimi klasöründeki dosyası üzerinde dönülmesi.
/// </summary>
public class QueryTimeframeScanner : IDisposable
{
    private readonly LogManager? _logger;

    public List<QueryTimeframeScanResult> Results { get; } = new();

    /// <summary>(işlenen sıra, toplam TF sayısı, zaman dilimi)</summary>
    public Action<int, int, string>? OnProgress { get; set; }

    public QueryTimeframeScanner(LogManager? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Zaman dilimlerini sırayla tarar. Her TF için sorguyu (SingleTrader, QueryOnly) çalıştırır,
    /// sonucu Results'a ekler ve csvPath/txtPath'e satır satır yazar. Bir TF'de hata olursa o TF
    /// Success=false olarak işaretlenir, tarama devam eder (fail-soft).
    /// </summary>
    public void Run(QueryTimeframeScannerOptions options, string csvPath, string txtPath, CancellationToken ct = default)
    {
        Results.Clear();

        if (options.Timeframes.Count == 0)
        {
            LogManager.LogWarning("QueryTimeframeScanner: taranacak zaman dilimi listesi boş.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(csvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(csvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(txtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        var queryRegistry = new QueryRegistry();
        bool headerWritten = false;

        for (int idx = 0; idx < options.Timeframes.Count; idx++)
        {
            ct.ThrowIfCancellationRequested();

            var tf = options.Timeframes[idx];
            var filePath = Path.Combine(options.BaseFolder, tf, options.Symbol + ".csv");
            var result = RunSingleTimeframe(tf, filePath, options, queryRegistry);
            Results.Add(result);

            if (!headerWritten && result.Success)
            {
                string header = $"Timeframe;{string.Join(";", result.QueryColumnNames)};SorguOzeti";
                csvWriter.WriteLine(header);
                txtWriter.WriteLine(header);
                headerWritten = true;
            }

            string row = result.Success
                ? $"{result.Timeframe};{string.Join(";", result.LastQueryResult)};{result.SorguOzeti}"
                : $"{result.Timeframe};HATA: {result.ErrorMessage}";
            csvWriter.WriteLine(row);
            txtWriter.WriteLine(row);
            csvWriter.Flush();
            txtWriter.Flush();

            OnProgress?.Invoke(idx + 1, options.Timeframes.Count, tf);
        }
    }

    private QueryTimeframeScanResult RunSingleTimeframe(string tf, string filePath, QueryTimeframeScannerOptions options,
        QueryRegistry queryRegistry)
    {
        var result = new QueryTimeframeScanResult { Timeframe = tf, FilePath = filePath };

        AlgoTrade.Core.StockDataReader.StockDataReader? reader = null;
        IndicatorManager? indicators = null;
        IQuery? query = null;
        SingleTrader? trader = null;

        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Dosya bulunamadı: {filePath}");

            reader = new AlgoTrade.Core.StockDataReader.StockDataReader();
            reader.ReadMetaData(filePath);
            reader.ReadDataFast(filePath, options.ReadFilterMode, options.N1, options.N2, options.Dt1, options.Dt2);
            var data = reader.GetData();

            if (data.Count == 0)
                throw new InvalidOperationException("Okunan veri boş.");

            indicators = new IndicatorManager(data);
            query = queryRegistry.CreateQuery(data, indicators, _logger, options.QueryName, options.QueryParameters);

            trader = new SingleTrader(0, $"{options.Symbol}_{tf}", data, indicators, _logger);
            trader.Reset();
            trader.RunMode = TraderRunMode.QueryOnly;
            trader.SetQuery(query);
            trader.Init();

            for (int i = 0; i < data.Count; i++)
                trader.Run(i);

            trader.Finalize();

            result.Success = true;
            result.BarCount = data.Count;
            result.QueryColumnNames = new List<string>(trader.QueryColumnNames);
            result.LastQueryResult = new List<object>(trader.LastQueryResult);
            result.SorguOzeti = trader.SorguOzeti;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            LogManager.LogError($"QueryTimeframeScanner: '{tf}' işlenirken hata: {ex.Message}");
        }
        finally
        {
            trader?.Dispose();
            (query as IDisposable)?.Dispose();
            indicators?.Dispose();
            reader?.Dispose();
        }

        return result;
    }

    public void Dispose()
    {
        // StockDataReader/IndicatorManager/SingleTrader zaten TF başına Dispose ediliyor
        // (RunSingleTimeframe içindeki finally bloğu) — burada tutulan bir kaynak yok.
    }
}

/// <summary>QueryTimeframeScanner.Run() için giriş parametreleri.</summary>
public class QueryTimeframeScannerOptions
{
    public string BaseFolder { get; set; } = "";

    /// <summary>Dosya adı köküyle birebir (örn. "BTCUSDT_BNC").</summary>
    public string Symbol { get; set; } = "";

    /// <summary>Zaman dilimi klasör adları (örn. ["01","05","15","60"]).</summary>
    public List<string> Timeframes { get; set; } = new();

    public string QueryName { get; set; } = "";
    public Dictionary<string, object> QueryParameters { get; set; } = new();

    public FilterMode ReadFilterMode { get; set; } = FilterMode.All;
    public int N1 { get; set; }
    public int N2 { get; set; }
    public DateTime? Dt1 { get; set; }
    public DateTime? Dt2 { get; set; }
}

/// <summary>Tek bir zaman dilimi için sorgu tarama sonucu — QueryScanResult'ın TF-anahtarlı ikizi.</summary>
public class QueryTimeframeScanResult
{
    public string Timeframe { get; set; } = "";
    public string FilePath { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int BarCount { get; set; }

    public List<string> QueryColumnNames { get; set; } = new();
    public List<object> LastQueryResult { get; set; } = new();
    public string SorguOzeti { get; set; } = "";
}
