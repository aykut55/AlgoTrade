using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Queries;
using static AlgoTrade.Core.StockDataReader.StockDataReader;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Aynı sembolde, birden fazla sorguyu (<see cref="MultipleQuery"/>, Sorgu tarafının "Yapı Taşı
/// B"si) birden fazla zaman diliminde BAĞIMSIZ olarak çalıştırıp sonuçları tek bir özet tabloda
/// toplar (bkz. docs/tarama-motoru-plan.md — Sorgu Tarama Matrisi, Senaryo 4).
/// `MultiStrategyTimeframeScanner`'ın (Strateji tarafı, Senaryo 4) birebir Sorgu karşılığı —
/// tek fark, `MultipleTrader` consensus'u yerine `MultipleQuery`'nin N sorguyu BİRLEŞTİRMEDEN
/// ayrı ayrı raporlaması (bkz. "Karar: Çoklu Sorgu Ne Anlama Geliyor").
///
/// `MultiStrategyTimeframeScanner`'dan farklı olarak throwaway `AlgoTrader` kurmuyor —
/// `MultipleQuery` zaten `AlgoTrader`'a bağlı değil (Query mode `AlgoTrader.createChildTraders()`
/// akışına ihtiyaç duymuyor), bu yüzden `SymbolScanner`/`TimeframeScanner` gibi veriyi elle
/// (`StockDataReader`/`IndicatorManager`) okuyup `MultipleQuery`'yi doğrudan kuruyor.
/// </summary>
public class MultiQueryTimeframeScanner : IDisposable
{
    private readonly LogManager? _logger;

    public List<MultiQueryTimeframeScanResult> Results { get; } = new();

    /// <summary>(işlenen sıra, toplam TF sayısı, zaman dilimi)</summary>
    public Action<int, int, string>? OnProgress { get; set; }

    public MultiQueryTimeframeScanner(LogManager? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Zaman dilimlerini sırayla tarar. Her TF için taze bir MultipleQuery kurup çalıştırır,
    /// sonucu Results'a ekler ve csvPath/txtPath'e satır satır yazar. Bir TF'de hata olursa o TF
    /// Success=false olarak işaretlenir, tarama devam eder (fail-soft).
    /// </summary>
    public void Run(MultiQueryTimeframeScannerOptions options, string csvPath, string txtPath, CancellationToken ct = default)
    {
        Results.Clear();

        if (options.Timeframes.Count == 0)
        {
            LogManager.LogWarning("MultiQueryTimeframeScanner: taranacak zaman dilimi listesi boş.");
            return;
        }
        if (options.Queries.Count == 0)
        {
            LogManager.LogWarning("MultiQueryTimeframeScanner: sorgu listesi boş.");
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
                string header = $"Timeframe{BuildQueryHeaderSuffix(result.QuerySignals)}";
                csvWriter.WriteLine(header);
                txtWriter.WriteLine(header);
                headerWritten = true;
            }

            string row = result.Success
                ? $"{result.Timeframe}{BuildQueryRowSuffix(result.QuerySignals)}"
                : $"{result.Timeframe};HATA: {result.ErrorMessage}";
            csvWriter.WriteLine(row);
            txtWriter.WriteLine(row);
            csvWriter.Flush();
            txtWriter.Flush();

            OnProgress?.Invoke(idx + 1, options.Timeframes.Count, tf);
        }
    }

    private MultiQueryTimeframeScanResult RunSingleTimeframe(string tf, string filePath, MultiQueryTimeframeScannerOptions options,
        QueryRegistry queryRegistry)
    {
        var result = new MultiQueryTimeframeScanResult { Timeframe = tf, FilePath = filePath };

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
                    QueryId   = options.Queries[i].QueryId,
                    QueryName = options.Queries[i].QueryName,
                    SorguOzeti = trader.SorguOzeti,
                });
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            LogManager.LogError($"MultiQueryTimeframeScanner: '{tf}' işlenirken hata: {ex.Message}");
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
        // MultipleQuery/StockDataReader/IndicatorManager zaten TF başına Dispose ediliyor
        // (RunSingleTimeframe içindeki finally bloğu) — burada tutulan bir kaynak yok.
    }
}

/// <summary>MultiQueryTimeframeScanner.Run() için giriş parametreleri.</summary>
public class MultiQueryTimeframeScannerOptions
{
    public string BaseFolder { get; set; } = "";

    /// <summary>Dosya adı köküyle birebir (örn. "BTCUSDT_BNC").</summary>
    public string Symbol { get; set; } = "";

    /// <summary>Zaman dilimi klasör adları (örn. ["01","05","15","60"]).</summary>
    public List<string> Timeframes { get; set; } = new();

    /// <summary>Her TF'de bağımsız çalışacak sorgular — hiçbiri birleştirilmez, ayrı ayrı raporlanır.</summary>
    public List<QueryEntry> Queries { get; set; } = new();

    public FilterMode ReadFilterMode { get; set; } = FilterMode.All;
    public int N1 { get; set; }
    public int N2 { get; set; }
    public DateTime? Dt1 { get; set; }
    public DateTime? Dt2 { get; set; }
}

/// <summary>Bir MultipleQuery child'ının (bağımsız çalışan tek bir sorgunun) tanımı.</summary>
public class QueryEntry
{
    public int QueryId { get; set; }
    public string QueryName { get; set; } = "";
    public Dictionary<string, object> QueryParameters { get; set; } = new();
}

/// <summary>Tek bir zaman dilimi için çoklu-sorgu tarama sonucu.</summary>
public class MultiQueryTimeframeScanResult
{
    public string Timeframe { get; set; } = "";
    public string FilePath { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int BarCount { get; set; }

    /// <summary>Her sorgunun BAĞIMSIZ sonucu — hiçbiri birleştirilmez (bkz. "Karar: Çoklu Sorgu").</summary>
    public List<QuerySignalInfo> QuerySignals { get; set; } = new();
}

/// <summary>Bir MultipleQuery child'ının bağımsız sonucu — ChildSignalInfo'nun (Strateji tarafı) Sorgu karşılığı.</summary>
public class QuerySignalInfo
{
    public int QueryId { get; set; }
    public string QueryName { get; set; } = "";
    public string SorguOzeti { get; set; } = "";
}
