using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Timer;
using System.Globalization;
using static AlgoTrade.Core.StockDataReader.StockDataReader;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Matrisin en genel hâli: N sembol × M zaman dilimi, her hücrede birden fazla stratejinin
/// konsensüsünü (<see cref="MultipleTrader"/>, Yapı Taşı B) çalıştırıp sonuçları tek bir özet
/// tabloda toplar (bkz. docs/tarama-motoru-plan.md — Senaryo 8). Hiçbir eksende (sembol/TF)
/// konsensüs YOK — her hücre tamamen bağımsız, sadece hücrenin kendi içinde MultipleTrader'ın
/// strateji-ekseni consensus'u var.
///
/// Yeni bir teknik değil — iki mevcut desenin bileşimi: <see cref="SymbolTimeframeScanner"/>'ın
/// (Senaryo 6) iç içe döngü iskeleti (sembol × TF, dosya yolu çözümlemesi, sembol keşfi) +
/// <see cref="MultiStrategySymbolScanner"/>/<see cref="MultiStrategyTimeframeScanner"/>'ın
/// (Senaryo 4/7) her hücre için taze/throwaway bir <see cref="AlgoTrader"/> kurup
/// <see cref="AlgoTrader.RunMultipleTraderWithProgressAsync"/> çağırma tekniği. Sonuç tipi
/// olarak <see cref="SymbolTimeframeScanner"/>'daki <see cref="SymbolTimeframeScanResult"/>
/// (Symbol+Timeframe anahtarlı, ChildSignals dahil) reuse edilir.
///
/// Nasıl konfigüre edileceği (MultipleTrader child stratejileri, consensus modu, trade params)
/// bilinçli olarak bu sınıfın bilmediği bir konu — çağıran taraf
/// <see cref="MultiStrategySymbolTimeframeScannerOptions.ConfigureAlgoTrader"/> delegate'i ile
/// (genelde <c>AppConfigApplier.ApplyMultipleTrader(...)</c> çağırarak) wiring'i yapıyor.
/// </summary>
public class MultiStrategySymbolTimeframeScanner : IDisposable
{
    private readonly LogManager? _logger;
    private string? _statsHeader;

    public List<SymbolTimeframeScanResult> Results { get; } = new();

    /// <summary>(işlenen sıra, toplam hücre sayısı, "Sembol/TF")</summary>
    public Action<int, int, string>? OnProgress { get; set; }

    public MultiStrategySymbolTimeframeScanner(LogManager? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sembol × zaman dilimi matrisini sırayla tarar (dış döngü sembol, iç döngü TF). Her hücre
    /// için taze bir AlgoTrader+MultipleTrader kurup çalıştırır, sonucu Results'a ekler ve
    /// csvPath/txtPath'e satır satır yazar. Bir hücrede hata olursa (dosya yok, veri boş, config
    /// hatası vb.) o hücre Success=false olarak işaretlenir, tarama devam eder (fail-soft).
    /// </summary>
    public async Task RunAsync(MultiStrategySymbolTimeframeScannerOptions options, string csvPath, string txtPath, CancellationToken ct = default)
    {
        Results.Clear();

        var symbols = ResolveSymbols(options);
        if (symbols.Count == 0)
        {
            LogManager.LogWarning($"MultiStrategySymbolTimeframeScanner: '{options.BaseFolder}\\{options.ReferenceTimeframe}' içinde taranacak sembol bulunamadı.");
            return;
        }
        if (options.Timeframes.Count == 0)
        {
            LogManager.LogWarning("MultiStrategySymbolTimeframeScanner: taranacak zaman dilimi listesi boş.");
            return;
        }
        if (options.ConfigureAlgoTrader == null)
        {
            LogManager.LogError("MultiStrategySymbolTimeframeScanner: ConfigureAlgoTrader delegate'i atanmamış.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(csvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(csvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(txtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        _statsHeader = null;
        bool headerWritten = false;

        int total = symbols.Count * options.Timeframes.Count;
        int idx = 0;

        foreach (var symbol in symbols)
        {
            foreach (var tf in options.Timeframes)
            {
                ct.ThrowIfCancellationRequested();

                var filePath = Path.Combine(options.BaseFolder, tf, symbol + ".csv");
                var result = await RunSingleSymbolTimeframeAsync(symbol, tf, filePath, options, ct);
                Results.Add(result);

                if (!headerWritten && _statsHeader != null)
                {
                    string header = $"Symbol;Timeframe;{_statsHeader};SonYon;SonKarZararFiyat;SonKarZararYuzde;SonSinyaldenBeriBarSayisi;TaramaOzeti{BuildChildHeaderSuffix(result.ChildSignals)}";
                    csvWriter.WriteLine(header);
                    txtWriter.WriteLine(header);
                    headerWritten = true;
                }

                string row = result.Success
                    ? $"{result.Symbol};{result.Timeframe};{result.StatisticsDataRow};{result.SonYon};{result.SonKarZararFiyat.ToString("F2", CultureInfo.InvariantCulture)};{result.SonKarZararYuzde.ToString("F2", CultureInfo.InvariantCulture)};{result.SonSinyaldenBeriBarSayisi};{result.TaramaOzeti}{BuildChildRowSuffix(result.ChildSignals)}"
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

    private List<string> ResolveSymbols(MultiStrategySymbolTimeframeScannerOptions options)
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

    private async Task<SymbolTimeframeScanResult> RunSingleSymbolTimeframeAsync(string symbol, string tf, string filePath,
        MultiStrategySymbolTimeframeScannerOptions options, CancellationToken ct)
    {
        var result = new SymbolTimeframeScanResult { Symbol = symbol, Timeframe = tf, FilePath = filePath };

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

            algoTrader = new AlgoTrader($"scan_{symbol}_{tf}");
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
            // 4'te düzeltilen desen (bkz. docs/tarama-motoru-plan.md "✅ DÜZELTİLDİ"), burada da
            // baştan doğru uygulandı.
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
            LogManager.LogError($"MultiStrategySymbolTimeframeScanner: '{symbol}/{tf}' işlenirken hata: {ex.Message}");
        }
        finally
        {
            algoTrader?.Dispose();
            reader?.Dispose();
        }

        return result;
    }

    /// <summary>Başarılı sonuçları SortField'e göre sıralayıp ayrı bir CSV/TXT çifti yazar (gruplama yok, global sıralama).</summary>
    public void WriteSortedResults(MultiStrategySymbolTimeframeScannerOptions options, string sortedCsvPath, string sortedTxtPath)
    {
        var ordered = options.SortDescending
            ? Results.Where(r => r.Success).OrderByDescending(r => r.SortValue)
            : Results.Where(r => r.Success).OrderBy(r => r.SortValue);
        var sorted = ordered.ToList();

        if (sorted.Count == 0) return;

        Directory.CreateDirectory(Path.GetDirectoryName(sortedCsvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(sortedCsvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(sortedTxtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        var header = $"Symbol;Timeframe;{_statsHeader};SonYon;SonKarZararFiyat;SonKarZararYuzde;SonSinyaldenBeriBarSayisi;TaramaOzeti{BuildChildHeaderSuffix(sorted[0].ChildSignals)}";
        csvWriter.WriteLine(header);
        txtWriter.WriteLine(header);

        foreach (var r in sorted)
        {
            string row = $"{r.Symbol};{r.Timeframe};{r.StatisticsDataRow};{r.SonYon};{r.SonKarZararFiyat.ToString("F2", CultureInfo.InvariantCulture)};{r.SonKarZararYuzde.ToString("F2", CultureInfo.InvariantCulture)};{r.SonSinyaldenBeriBarSayisi};{r.TaramaOzeti}{BuildChildRowSuffix(r.ChildSignals)}";
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

    /// <summary>SortField'e göre en iyi sonucu döner (Success=false olanlar hariç, tüm matriste global en iyi).</summary>
    public SymbolTimeframeScanResult? GetBestResult(MultiStrategySymbolTimeframeScannerOptions options)
    {
        var query = Results.Where(r => r.Success);
        return options.SortDescending
            ? query.OrderByDescending(r => r.SortValue).FirstOrDefault()
            : query.OrderBy(r => r.SortValue).FirstOrDefault();
    }

    public void Dispose()
    {
        // AlgoTrader/StockDataReader zaten hücre başına Dispose ediliyor
        // (RunSingleSymbolTimeframeAsync içindeki finally bloğu) — burada tutulan bir kaynak yok.
    }
}

/// <summary>MultiStrategySymbolTimeframeScanner.RunAsync() için giriş parametreleri.</summary>
public class MultiStrategySymbolTimeframeScannerOptions
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

    /// <summary>
    /// Her hücre için taze kurulan AlgoTrader'ı MultipleTrader konfigürasyonuyla (child stratejiler,
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
