using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Queries;
using AlgoTrade.Core.Trading.Query;
using static AlgoTrade.Core.StockDataReader.StockDataReader;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Aynı sorguyu birden fazla sembolde bağımsız olarak çalıştırıp sonuçları tek bir özet
/// tabloda toplar (bkz. docs/tarama-motoru-plan.md — Sorgu Tarama Matrisi, Senaryo 5).
/// `SymbolScanner`'ın (Yapı Taşı C, Strateji tarafı) birebir Sorgu karşılığı — tek fark,
/// `SingleTrader.RunMode = TradeOnly` yerine `QueryOnly`, ve sonuç satırı `GetStatisticsDataRow`
/// yerine sorgunun dinamik kolonlarından (`QueryColumnNames`/`LastQueryResult`) kuruluyor —
/// QueryOnly modda istatistikler hiç hesaplanmadığı için `GetStatisticsDataRow` anlamsız kalırdı.
/// </summary>
public class QuerySymbolScanner : IDisposable
{
    private readonly LogManager? _logger;

    public List<QueryScanResult> Results { get; } = new();

    /// <summary>(işlenen sıra, toplam sembol sayısı, sembol adı)</summary>
    public Action<int, int, string>? OnProgress { get; set; }

    public QuerySymbolScanner(LogManager? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sembolleri sırayla tarar. Her sembol için sorguyu (SingleTrader, QueryOnly) çalıştırır,
    /// sonucu Results'a ekler ve csvPath/txtPath'e satır satır yazar. Bir sembolde hata olursa
    /// o sembol Success=false olarak işaretlenir, tarama devam eder (fail-soft).
    /// </summary>
    public void Run(QuerySymbolScanOptions options, string csvPath, string txtPath, CancellationToken ct = default)
    {
        Results.Clear();

        var symbols = ResolveSymbols(options);
        if (symbols.Count == 0)
        {
            LogManager.LogWarning($"QuerySymbolScanner: '{options.DataFolder}' içinde taranacak sembol bulunamadı.");
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
                string header = $"Symbol;{string.Join(";", result.QueryColumnNames)};SorguOzeti";
                csvWriter.WriteLine(header);
                txtWriter.WriteLine(header);
                headerWritten = true;
            }

            string row = result.Success
                ? $"{result.Symbol};{string.Join(";", result.LastQueryResult)};{result.SorguOzeti}"
                : $"{result.Symbol};HATA: {result.ErrorMessage}";
            csvWriter.WriteLine(row);
            txtWriter.WriteLine(row);
            csvWriter.Flush();
            txtWriter.Flush();

            OnProgress?.Invoke(idx + 1, symbols.Count, symbol);
        }
    }

    private List<(string Symbol, string FilePath)> ResolveSymbols(QuerySymbolScanOptions options)
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

    private QueryScanResult RunSingleSymbol(string symbol, string filePath, QuerySymbolScanOptions options,
        QueryRegistry queryRegistry)
    {
        var result = new QueryScanResult { Symbol = symbol, FilePath = filePath };

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

            trader = new SingleTrader(0, symbol, data, indicators, _logger);
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
            LogManager.LogError($"QuerySymbolScanner: '{symbol}' işlenirken hata: {ex.Message}");
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
        // StockDataReader/IndicatorManager/SingleTrader zaten sembol başına Dispose ediliyor
        // (RunSingleSymbol içindeki finally bloğu) — burada tutulan bir kaynak yok.
    }
}

/// <summary>QuerySymbolScanner.Run() için giriş parametreleri.</summary>
public class QuerySymbolScanOptions
{
    public string DataFolder { get; set; } = "";
    public bool AutoDiscover { get; set; } = true;

    /// <summary>AutoDiscover=false iken kullanılır. Her eleman dosya adı köküyle birebir (örn. "BTCUSDT_BNC").</summary>
    public List<string> SymbolList { get; set; } = new();

    public string QueryName { get; set; } = "";
    public Dictionary<string, object> QueryParameters { get; set; } = new();

    public FilterMode ReadFilterMode { get; set; } = FilterMode.All;
    public int N1 { get; set; }
    public int N2 { get; set; }
    public DateTime? Dt1 { get; set; }
    public DateTime? Dt2 { get; set; }
}

/// <summary>
/// Tek bir sembol için sorgu tarama sonucu. Strateji tarafındaki ScanResult'ın Sorgu karşılığı —
/// GetStatisticsDataRow yerine sorgunun dinamik kolonları (QueryColumnNames/LastQueryResult, son
/// bar) + SorguOzeti (TaramaOzeti'nin karşılığı) taşınıyor. SortValue/SortField yok — sorgu
/// sonuçları bir "performans" değeri değil, kullanıcı kendisi yorumluyor (bkz. Karar: Çoklu Sorgu).
/// </summary>
public class QueryScanResult
{
    public string Symbol { get; set; } = "";
    public string FilePath { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int BarCount { get; set; }

    public List<string> QueryColumnNames { get; set; } = new();
    public List<object> LastQueryResult { get; set; } = new();
    public string SorguOzeti { get; set; } = "";
}
