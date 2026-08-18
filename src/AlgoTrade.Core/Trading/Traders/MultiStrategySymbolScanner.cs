using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Timer;
using System.Globalization;
using static AlgoTrade.Core.StockDataReader.StockDataReader;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Tek bir zaman diliminde, birden fazla stratejinin konsensüsünü (<see cref="MultipleTrader"/>,
/// Yapı Taşı B) birden fazla sembolde BAĞIMSIZ olarak çalıştırıp sonuçları tek bir özet tabloda
/// toplar (bkz. docs/tarama-motoru-plan.md — Senaryo 7). Semboller arasında konsensüs YOK —
/// sadece MultipleTrader'ın kendisi strateji ekseninde konsensüs alıyor, her sembolün sonucu
/// ayrı raporlanıyor.
///
/// MultiStrategyTimeframeScanner'ın (Senaryo 4) doğrudan uyarlanmışı — dış döngü değişkeni TF
/// yerine sembol. Aynı sebeple (AlgoTrader.createChildTraders() elle tekrar yazılamayacak kadar
/// karmaşık ve zaten test edilmiş) SingleTrader'ı elle kurmuyor, her sembol için tek kullanımlık
/// (throwaway) bir <see cref="AlgoTrader"/> kurup <see cref="AlgoTrader.RunMultipleTraderWithProgressAsync"/>'i
/// çağırıyor. Sembol listesi çözümlemesi SymbolScanner'daki (Yapı Taşı C) ile birebir aynı
/// (DataFolder + AutoDiscover/SymbolList). Sonuç tipi olarak SymbolScanner.cs'teki
/// <see cref="ScanResult"/> reuse edilir (Symbol-anahtarlı) — senaryo 4'ün TimeframeScanResult'ı
/// (Timeframe-anahtarlı) reuse etmesiyle aynı mantık.
///
/// Nasıl konfigüre edileceği (MultipleTrader child stratejileri, consensus modu, trade params)
/// bilinçli olarak bu sınıfın bilmediği bir konu — çağıran taraf (AppConfig'i bilen katman)
/// <see cref="MultiStrategySymbolScannerOptions.ConfigureAlgoTrader"/> delegate'i ile (genelde
/// <c>AppConfigApplier.ApplyMultipleTrader(...)</c> çağırarak) wiring'i yapıyor.
/// </summary>
public class MultiStrategySymbolScanner : IDisposable
{
    private readonly LogManager? _logger;
    private string? _statsHeader;

    public List<ScanResult> Results { get; } = new();

    /// <summary>(işlenen sıra, toplam sembol sayısı, sembol adı)</summary>
    public Action<int, int, string>? OnProgress { get; set; }

    public MultiStrategySymbolScanner(LogManager? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sembolleri sırayla tarar. Her sembol için taze bir AlgoTrader+MultipleTrader kurup
    /// çalıştırır, sonucu Results'a ekler ve csvPath/txtPath'e satır satır yazar. Bir sembolde
    /// hata olursa (dosya yok, veri boş, config hatası vb.) o sembol Success=false olarak
    /// işaretlenir, tarama devam eder (fail-soft).
    /// </summary>
    public async Task RunAsync(MultiStrategySymbolScannerOptions options, string csvPath, string txtPath, CancellationToken ct = default)
    {
        Results.Clear();

        var symbols = ResolveSymbols(options);
        if (symbols.Count == 0)
        {
            LogManager.LogWarning($"MultiStrategySymbolScanner: '{options.DataFolder}' içinde taranacak sembol bulunamadı.");
            return;
        }

        if (options.ConfigureAlgoTrader == null)
        {
            LogManager.LogError("MultiStrategySymbolScanner: ConfigureAlgoTrader delegate'i atanmamış.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(csvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(csvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(txtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        _statsHeader = null;
        bool headerWritten = false;

        for (int idx = 0; idx < symbols.Count; idx++)
        {
            ct.ThrowIfCancellationRequested();

            var (symbol, filePath) = symbols[idx];
            var result = await RunSingleSymbolAsync(symbol, filePath, options, ct);
            Results.Add(result);

            if (!headerWritten && _statsHeader != null)
            {
                string header = $"Symbol;{_statsHeader};SonYon;SonKarZararFiyat;SonKarZararYuzde;SonSinyaldenBeriBarSayisi;TaramaOzeti{BuildChildHeaderSuffix(result.ChildSignals)}";
                csvWriter.WriteLine(header);
                txtWriter.WriteLine(header);
                headerWritten = true;
            }

            string row = result.Success
                ? $"{result.Symbol};{result.StatisticsDataRow};{result.SonYon};{result.SonKarZararFiyat.ToString("F2", CultureInfo.InvariantCulture)};{result.SonKarZararYuzde.ToString("F2", CultureInfo.InvariantCulture)};{result.SonSinyaldenBeriBarSayisi};{result.TaramaOzeti}{BuildChildRowSuffix(result.ChildSignals)}"
                : $"{result.Symbol};HATA: {result.ErrorMessage}";
            csvWriter.WriteLine(row);
            txtWriter.WriteLine(row);
            csvWriter.Flush();
            txtWriter.Flush();

            OnProgress?.Invoke(idx + 1, symbols.Count, symbol);
        }
    }

    private List<(string Symbol, string FilePath)> ResolveSymbols(MultiStrategySymbolScannerOptions options)
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

    private async Task<ScanResult> RunSingleSymbolAsync(string symbol, string filePath, MultiStrategySymbolScannerOptions options, CancellationToken ct)
    {
        var result = new ScanResult { Symbol = symbol, FilePath = filePath };

        AlgoTrade.Core.StockDataReader.StockDataReader? reader = null;
        AlgoTrader? algoTrader = null;

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

            algoTrader = new AlgoTrader($"scan_{symbol}");
            algoTrader.RegisterLogger(_logger!);
            algoTrader.RegisterTimer(TimeManager.GetInstance());
            algoTrader.Reset();
            algoTrader.SetData(data);

            options.ConfigureAlgoTrader!(algoTrader);

            algoTrader.Initialize();
            await algoTrader.RunMultipleTraderWithProgressAsync(ct);

            var mainTrader = algoTrader.MultipleTrader?.GetMainTrader()
                ?? throw new InvalidOperationException("MultipleTrader/mainTrader oluşturulamadı.");

            _statsHeader ??= mainTrader.GetStatisticsHeaderRow(";").ToString();

            result.Success = true;
            result.BarCount = data.Count;
            result.StatisticsDataRow = mainTrader.GetStatisticsDataRow(";").ToString();
            result.OptimizationSummary = mainTrader.statistics.GetOptimizationSummary();
            result.SonYon = mainTrader.SonYon;
            result.SonKarZararFiyat = mainTrader.SonKarZararFiyat;
            result.SonKarZararYuzde = mainTrader.SonKarZararYuzde;
            result.SonSinyaldenBeriBarSayisi = mainTrader.SonSinyaldenBeriBarSayisi;
            result.TaramaOzeti = mainTrader.TaramaOzeti;

            // Bileşkeye (mainTrader) ek olarak her child'ın BAĞIMSIZ sinyalini de topla — senaryo
            // 4'te düzeltilen desen (bkz. docs/tarama-motoru-plan.md "✅ DÜZELTİLDİ"), burada baştan
            // doğru uygulandı.
            foreach (var child in algoTrader.MultipleTrader!.Traders)
            {
                result.ChildSignals.Add(new ChildSignalInfo
                {
                    ChildId = child.Id,
                    SonYon = child.SonYon,
                    TaramaOzeti = child.TaramaOzeti,
                });
            }

            result.SortValue = (result.OptimizationSummary.TryGetValue(options.SortField, out var sortStr)
                && double.TryParse(sortStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var sortVal))
                ? sortVal
                : double.MinValue;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.SortValue = double.MinValue;
            LogManager.LogError($"MultiStrategySymbolScanner: '{symbol}' işlenirken hata: {ex.Message}");
        }
        finally
        {
            algoTrader?.Dispose();
            reader?.Dispose();
        }

        return result;
    }

    /// <summary>Başarılı sonuçları SortField'e göre sıralayıp ayrı bir CSV/TXT çifti yazar.</summary>
    public void WriteSortedResults(MultiStrategySymbolScannerOptions options, string sortedCsvPath, string sortedTxtPath)
    {
        var ordered = options.SortDescending
            ? Results.Where(r => r.Success).OrderByDescending(r => r.SortValue)
            : Results.Where(r => r.Success).OrderBy(r => r.SortValue);
        var sorted = ordered.ToList();

        if (sorted.Count == 0) return;

        Directory.CreateDirectory(Path.GetDirectoryName(sortedCsvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(sortedCsvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(sortedTxtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        var header = $"Symbol;{_statsHeader};SonYon;SonKarZararFiyat;SonKarZararYuzde;SonSinyaldenBeriBarSayisi;TaramaOzeti{BuildChildHeaderSuffix(sorted[0].ChildSignals)}";
        csvWriter.WriteLine(header);
        txtWriter.WriteLine(header);

        foreach (var r in sorted)
        {
            string row = $"{r.Symbol};{r.StatisticsDataRow};{r.SonYon};{r.SonKarZararFiyat.ToString("F2", CultureInfo.InvariantCulture)};{r.SonKarZararYuzde.ToString("F2", CultureInfo.InvariantCulture)};{r.SonSinyaldenBeriBarSayisi};{r.TaramaOzeti}{BuildChildRowSuffix(r.ChildSignals)}";
            csvWriter.WriteLine(row);
            txtWriter.WriteLine(row);
        }
    }

    /// <summary>Her child için "Child{id}_SonYon;Child{id}_TaramaOzeti" başlık kolonlarını üretir.</summary>
    private static string BuildChildHeaderSuffix(List<ChildSignalInfo> children)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in children)
            sb.Append($";Child{c.ChildId}_SonYon;Child{c.ChildId}_TaramaOzeti");
        return sb.ToString();
    }

    /// <summary>Her child için "SonYon;TaramaOzeti" veri kolonlarını üretir (header ile aynı sırada).</summary>
    private static string BuildChildRowSuffix(List<ChildSignalInfo> children)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in children)
            sb.Append($";{c.SonYon};{c.TaramaOzeti}");
        return sb.ToString();
    }

    /// <summary>SortField'e göre en iyi sonucu döner (Success=false olanlar hariç).</summary>
    public ScanResult? GetBestResult(MultiStrategySymbolScannerOptions options)
    {
        var query = Results.Where(r => r.Success);
        return options.SortDescending
            ? query.OrderByDescending(r => r.SortValue).FirstOrDefault()
            : query.OrderBy(r => r.SortValue).FirstOrDefault();
    }

    public void Dispose()
    {
        // AlgoTrader/StockDataReader zaten sembol başına Dispose ediliyor
        // (RunSingleSymbolAsync içindeki finally bloğu) — burada tutulan bir kaynak yok.
    }
}

/// <summary>MultiStrategySymbolScanner.RunAsync() için giriş parametreleri.</summary>
public class MultiStrategySymbolScannerOptions
{
    public string DataFolder { get; set; } = "";
    public bool AutoDiscover { get; set; } = true;

    /// <summary>AutoDiscover=false iken kullanılır. Her eleman dosya adı köküyle birebir (örn. "BTCUSDT_BNC").</summary>
    public List<string> SymbolList { get; set; } = new();

    /// <summary>
    /// Her sembol için taze kurulan AlgoTrader'ı MultipleTrader konfigürasyonuyla (child stratejiler,
    /// consensus modu, trade params) donatan callback — genelde
    /// <c>AppConfigApplier.ApplyMultipleTrader(algoTrader, cfg, configsDir)</c> çağırır.
    /// Bu sınıf AppConfig namespace'ini bilmediği için wiring çağıran tarafa bırakılıyor.
    /// </summary>
    public Action<AlgoTrader>? ConfigureAlgoTrader { get; set; }

    public FilterMode ReadFilterMode { get; set; } = FilterMode.All;
    public int N1 { get; set; }
    public int N2 { get; set; }
    public DateTime? Dt1 { get; set; }
    public DateTime? Dt2 { get; set; }

    public string SortField { get; set; } = "NetProfit";
    public bool SortDescending { get; set; } = true;
}
