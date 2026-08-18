using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Queries;
using static AlgoTrade.Core.StockDataReader.StockDataReader;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Tek bir zaman diliminde, birden fazla sorguyu (<see cref="MultipleQuery"/>) birden fazla
/// sembolde BAĞIMSIZ olarak çalıştırıp sonuçları tek bir özet tabloda toplar (bkz.
/// docs/tarama-motoru-plan.md — Sorgu Tarama Matrisi, Senaryo 7). `MultiStrategySymbolScanner`'ın
/// (Strateji tarafı, Senaryo 7) birebir Sorgu karşılığı — `MultiQueryTimeframeScanner`'ın
/// (Senaryo 4) doğrudan uyarlanmışı, döngü değişkeni TF yerine sembol.
/// </summary>
public class MultiQuerySymbolScanner : IDisposable
{
    private readonly LogManager? _logger;

    public List<MultiQuerySymbolScanResult> Results { get; } = new();

    /// <summary>(işlenen sıra, toplam sembol sayısı, sembol adı)</summary>
    public Action<int, int, string>? OnProgress { get; set; }

    public MultiQuerySymbolScanner(LogManager? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sembolleri sırayla tarar. Her sembol için taze bir MultipleQuery kurup çalıştırır, sonucu
    /// Results'a ekler ve csvPath/txtPath'e satır satır yazar. Bir sembolde hata olursa o sembol
    /// Success=false olarak işaretlenir, tarama devam eder (fail-soft).
    /// </summary>
    public void Run(MultiQuerySymbolScannerOptions options, string csvPath, string txtPath, CancellationToken ct = default)
    {
        Results.Clear();

        var symbols = ResolveSymbols(options);
        if (symbols.Count == 0)
        {
            LogManager.LogWarning($"MultiQuerySymbolScanner: '{options.DataFolder}' içinde taranacak sembol bulunamadı.");
            return;
        }
        if (options.Queries.Count == 0)
        {
            LogManager.LogWarning("MultiQuerySymbolScanner: sorgu listesi boş.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(csvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(csvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(txtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        var queryRegistry = new QueryRegistry();
        bool headerWritten = false;

        for (int idx = 0; idx < symbols.Count; idx++)
        {
            ct.ThrowIfCancellationRequested();

            var (symbol, filePath) = symbols[idx];
            var result = RunSingleSymbol(symbol, filePath, options, queryRegistry);
            Results.Add(result);

            if (!headerWritten && result.Success)
            {
                string header = $"Symbol{BuildQueryHeaderSuffix(result.QuerySignals)}";
                csvWriter.WriteLine(header);
                txtWriter.WriteLine(header);
                headerWritten = true;
            }

            string row = result.Success
                ? $"{result.Symbol}{BuildQueryRowSuffix(result.QuerySignals)}"
                : $"{result.Symbol};HATA: {result.ErrorMessage}";
            csvWriter.WriteLine(row);
            txtWriter.WriteLine(row);
            csvWriter.Flush();
            txtWriter.Flush();

            OnProgress?.Invoke(idx + 1, symbols.Count, symbol);
        }
    }

    private List<(string Symbol, string FilePath)> ResolveSymbols(MultiQuerySymbolScannerOptions options)
    {
        var list = new List<(string, string)>();

        if (options.AutoDiscover)
        {
            if (!Directory.Exists(options.DataFolder))
                return list;

            foreach (var file in Directory.GetFiles(options.DataFolder, "*.csv").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                list.Add((Path.GetFileNameWithoutExtension(file), file));
        }
        else
        {
            foreach (var symbol in options.SymbolList)
                list.Add((symbol, Path.Combine(options.DataFolder, symbol + ".csv")));
        }

        return list;
    }

    private MultiQuerySymbolScanResult RunSingleSymbol(string symbol, string filePath, MultiQuerySymbolScannerOptions options,
        QueryRegistry queryRegistry)
    {
        var result = new MultiQuerySymbolScanResult { Symbol = symbol, FilePath = filePath };

        AlgoTrade.Core.StockDataReader.StockDataReader? reader = null;
        IndicatorManager? indicators = null;
        MultipleQuery? multipleQuery = null;

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
            multipleQuery = new MultipleQuery(data, indicators, _logger);

            foreach (var q in options.Queries)
            {
                var query = queryRegistry.CreateQuery(data, indicators, _logger, q.QueryName, q.QueryParameters);
                multipleQuery.AddChildQuery(q.QueryId, query);
            }

            for (int i = 0; i < data.Count; i++)
                multipleQuery.Run(i);

            multipleQuery.Finalize();

            result.Success = true;
            result.BarCount = data.Count;

            for (int i = 0; i < multipleQuery.Traders.Count; i++)
            {
                var trader = multipleQuery.Traders[i];
                result.QuerySignals.Add(new QuerySignalInfo
                {
                    QueryId    = options.Queries[i].QueryId,
                    QueryName  = options.Queries[i].QueryName,
                    SorguOzeti = trader.SorguOzeti,
                });
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            LogManager.LogError($"MultiQuerySymbolScanner: '{symbol}' işlenirken hata: {ex.Message}");
        }
        finally
        {
            multipleQuery?.Dispose();
            indicators?.Dispose();
            reader?.Dispose();
        }

        return result;
    }

    /// <summary>Her sorgu için "Query{id}_SorguOzeti" başlık kolonunu üretir.</summary>
    private static string BuildQueryHeaderSuffix(List<QuerySignalInfo> queries)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var q in queries)
            sb.Append($";Query{q.QueryId}_SorguOzeti");
        return sb.ToString();
    }

    /// <summary>Her sorgu için "SorguOzeti" veri kolonunu üretir (header ile aynı sırada).</summary>
    private static string BuildQueryRowSuffix(List<QuerySignalInfo> queries)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var q in queries)
            sb.Append($";{q.SorguOzeti}");
        return sb.ToString();
    }

    public void Dispose()
    {
        // MultipleQuery/StockDataReader/IndicatorManager zaten sembol başına Dispose ediliyor
        // (RunSingleSymbol içindeki finally bloğu) — burada tutulan bir kaynak yok.
    }
}

/// <summary>MultiQuerySymbolScanner.Run() için giriş parametreleri.</summary>
public class MultiQuerySymbolScannerOptions
{
    public string DataFolder { get; set; } = "";
    public bool AutoDiscover { get; set; } = true;

    /// <summary>AutoDiscover=false iken kullanılır. Her eleman dosya adı köküyle birebir (örn. "BTCUSDT_BNC").</summary>
    public List<string> SymbolList { get; set; } = new();

    /// <summary>Her sembolde bağımsız çalışacak sorgular — hiçbiri birleştirilmez, ayrı ayrı raporlanır.</summary>
    public List<QueryEntry> Queries { get; set; } = new();

    public FilterMode ReadFilterMode { get; set; } = FilterMode.All;
    public int N1 { get; set; }
    public int N2 { get; set; }
    public DateTime? Dt1 { get; set; }
    public DateTime? Dt2 { get; set; }
}

/// <summary>Tek bir sembol için çoklu-sorgu tarama sonucu.</summary>
public class MultiQuerySymbolScanResult
{
    public string Symbol { get; set; } = "";
    public string FilePath { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int BarCount { get; set; }

    /// <summary>Her sorgunun BAĞIMSIZ sonucu — hiçbiri birleştirilmez (bkz. "Karar: Çoklu Sorgu").</summary>
    public List<QuerySignalInfo> QuerySignals { get; set; } = new();
}
