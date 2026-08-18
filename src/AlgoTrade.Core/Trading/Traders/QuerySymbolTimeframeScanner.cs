using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Queries;
using AlgoTrade.Core.Trading.Query;
using static AlgoTrade.Core.StockDataReader.StockDataReader;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Aynı sorguyu hem sembol hem zaman dilimi ekseninde, ikisi de TAMAMEN BAĞIMSIZ olacak
/// şekilde tarar (bkz. docs/tarama-motoru-plan.md — Sorgu Tarama Matrisi, Senaryo 6).
/// `SymbolTimeframeScanner`'ın (Strateji tarafı, Senaryo 6) birebir Sorgu karşılığı —
/// `QuerySymbolScanner`/`QueryTimeframeScanner`'ın iç içe geçmiş hali (üçüncü kopya, aynı
/// iskelet — `SingleTrader.RunMode = QueryOnly`, dinamik sorgu kolonları).
/// </summary>
public class QuerySymbolTimeframeScanner : IDisposable
{
    private readonly LogManager? _logger;

    public List<QuerySymbolTimeframeScanResult> Results { get; } = new();

    /// <summary>(işlenen sıra, toplam hücre sayısı, "Sembol/TF")</summary>
    public Action<int, int, string>? OnProgress { get; set; }

    public QuerySymbolTimeframeScanner(LogManager? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sembol × zaman dilimi matrisini sırayla tarar (dış döngü sembol, iç döngü TF). Her hücre
    /// için sorguyu (SingleTrader, QueryOnly) çalıştırır, sonucu Results'a ekler ve csvPath/
    /// txtPath'e satır satır yazar. Bir hücrede hata olursa o hücre Success=false olarak
    /// işaretlenir, tarama devam eder (fail-soft).
    /// </summary>
    public void Run(QuerySymbolTimeframeScannerOptions options, string csvPath, string txtPath, CancellationToken ct = default)
    {
        Results.Clear();

        var symbols = ResolveSymbols(options);
        if (symbols.Count == 0)
        {
            LogManager.LogWarning($"QuerySymbolTimeframeScanner: '{options.BaseFolder}\\{options.ReferenceTimeframe}' içinde taranacak sembol bulunamadı.");
            return;
        }
        if (options.Timeframes.Count == 0)
        {
            LogManager.LogWarning("QuerySymbolTimeframeScanner: taranacak zaman dilimi listesi boş.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(csvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(csvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(txtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        var queryRegistry = new QueryRegistry();
        bool headerWritten = false;

        int total = symbols.Count * options.Timeframes.Count;
        int idx = 0;

        foreach (var symbol in symbols)
        {
            foreach (var tf in options.Timeframes)
            {
                ct.ThrowIfCancellationRequested();

                var filePath = Path.Combine(options.BaseFolder, tf, symbol + ".csv");
                var result = RunSingleSymbolTimeframe(symbol, tf, filePath, options, queryRegistry);
                Results.Add(result);

                if (!headerWritten && result.Success)
                {
                    string header = $"Symbol;Timeframe;{string.Join(";", result.QueryColumnNames)};SorguOzeti";
                    csvWriter.WriteLine(header);
                    txtWriter.WriteLine(header);
                    headerWritten = true;
                }

                string row = result.Success
                    ? $"{result.Symbol};{result.Timeframe};{string.Join(";", result.LastQueryResult)};{result.SorguOzeti}"
                    : $"{result.Symbol};{result.Timeframe};HATA: {result.ErrorMessage}";
                csvWriter.WriteLine(row);
                txtWriter.WriteLine(row);
                csvWriter.Flush();
                txtWriter.Flush();

                idx++;
                OnProgress?.Invoke(idx, total, $"{symbol}/{tf}");
            }
        }
    }

    private List<string> ResolveSymbols(QuerySymbolTimeframeScannerOptions options)
    {
        var list = new List<string>();

        if (options.AutoDiscover)
        {
            var refFolder = Path.Combine(options.BaseFolder, options.ReferenceTimeframe);
            if (!Directory.Exists(refFolder))
                return list;

            foreach (var file in Directory.GetFiles(refFolder, "*.csv").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                list.Add(Path.GetFileNameWithoutExtension(file));
        }
        else
        {
            list.AddRange(options.SymbolList);
        }

        return list;
    }

    private QuerySymbolTimeframeScanResult RunSingleSymbolTimeframe(string symbol, string tf, string filePath,
        QuerySymbolTimeframeScannerOptions options, QueryRegistry queryRegistry)
    {
        var result = new QuerySymbolTimeframeScanResult { Symbol = symbol, Timeframe = tf, FilePath = filePath };

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

            trader = new SingleTrader(0, $"{symbol}_{tf}", data, indicators, _logger);
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
            LogManager.LogError($"QuerySymbolTimeframeScanner: '{symbol}/{tf}' işlenirken hata: {ex.Message}");
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
        // StockDataReader/IndicatorManager/SingleTrader zaten hücre başına Dispose ediliyor
        // (RunSingleSymbolTimeframe içindeki finally bloğu) — burada tutulan bir kaynak yok.
    }
}

/// <summary>QuerySymbolTimeframeScanner.Run() için giriş parametreleri.</summary>
public class QuerySymbolTimeframeScannerOptions
{
    /// <summary>Zaman dilimi klasörlerinin bulunduğu üst klasör (tam yol). Örn. C:\data\csvFiles\CRP</summary>
    public string BaseFolder { get; set; } = "";

    public bool AutoDiscover { get; set; } = true;

    /// <summary>AutoDiscover=true iken sembol keşfi için taranacak TF klasörü (örn. "05").</summary>
    public string ReferenceTimeframe { get; set; } = "";

    /// <summary>AutoDiscover=false iken kullanılır. Her eleman dosya adı köküyle birebir (örn. "BTCUSDT_BNC").</summary>
    public List<string> SymbolList { get; set; } = new();

    /// <summary>Taranacak zaman dilimi klasör adları (örn. ["01","05","15","60"]). Otomatik keşif yok, açık liste.</summary>
    public List<string> Timeframes { get; set; } = new();

    public string QueryName { get; set; } = "";
    public Dictionary<string, object> QueryParameters { get; set; } = new();

    public FilterMode ReadFilterMode { get; set; } = FilterMode.All;
    public int N1 { get; set; }
    public int N2 { get; set; }
    public DateTime? Dt1 { get; set; }
    public DateTime? Dt2 { get; set; }
}

/// <summary>Tek bir (Sembol, Zaman Dilimi) hücresi için sorgu tarama sonucu.</summary>
public class QuerySymbolTimeframeScanResult
{
    public string Symbol { get; set; } = "";
    public string Timeframe { get; set; } = "";
    public string FilePath { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int BarCount { get; set; }

    public List<string> QueryColumnNames { get; set; } = new();
    public List<object> LastQueryResult { get; set; } = new();
    public string SorguOzeti { get; set; } = "";
}
