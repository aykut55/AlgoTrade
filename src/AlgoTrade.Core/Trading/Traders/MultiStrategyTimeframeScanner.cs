using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Timer;
using System.Globalization;
using static AlgoTrade.Core.StockDataReader.StockDataReader;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Aynı sembolde, birden fazla stratejinin konsensüsünü (<see cref="MultipleTrader"/>, Yapı
/// Taşı B) birden fazla zaman diliminde BAĞIMSIZ olarak çalıştırıp sonuçları tek bir özet
/// tabloda toplar (bkz. docs/tarama-motoru-plan.md — Senaryo 4). Zaman dilimleri arasında hâlâ
/// konsensüs YOK (yapı taşı A'daki düzeltmeyle tutarlı) — sadece MultipleTrader'ın kendisi
/// strateji ekseninde konsensüs alıyor, her TF'nin sonucu ayrı raporlanıyor.
///
/// TimeframeScanner'dan farklı olarak <see cref="SingleTrader"/>'ı elle kurmuyor — bunun yerine
/// her TF için tek kullanımlık (throwaway) bir <see cref="AlgoTrader"/> kurup
/// <see cref="AlgoTrader.RunMultipleTraderWithProgressAsync"/>'i çağırıyor. Sebep:
/// AlgoTrader.createChildTraders() (strateji cache'i, per-child Signals/Save/Export,
/// EquityCurveFilter id eşlemesi) elle tekrar yazılamayacak kadar karmaşık ve zaten test
/// edilmiş — bunu kopyalamak yerine, AlgoTrader'ı (her zaman tek bir veri seti görecek şekilde,
/// TimeframeScanner'ın SingleTrader'ı taze taze kurup atması gibi) TF başına taze kurup
/// atıyoruz. Bu, "AlgoTrader tek veri seti varsayımıyla kurulu" ilkesini bozmuyor.
///
/// Nasıl konfigüre edileceği (MultipleTrader child stratejileri, consensus modu, trade params)
/// bilinçli olarak bu sınıfın bilmediği bir konu — çağıran taraf (AppConfig'i bilen katman)
/// <see cref="MultiStrategyTimeframeScannerOptions.ConfigureAlgoTrader"/> delegate'i ile
/// (genelde <c>AppConfigApplier.ApplyMultipleTrader(...)</c> çağırarak) wiring'i yapıyor — bu
/// sayede bu dosya AppConfig namespace'ine bağımlı olmuyor (SymbolScanner/TimeframeScanner ile
/// aynı katman ayrımı).
/// </summary>
public class MultiStrategyTimeframeScanner : IDisposable
{
    private readonly LogManager? _logger;
    private string? _statsHeader;

    public List<TimeframeScanResult> Results { get; } = new();

    /// <summary>(işlenen sıra, toplam TF sayısı, zaman dilimi)</summary>
    public Action<int, int, string>? OnProgress { get; set; }

    public MultiStrategyTimeframeScanner(LogManager? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Zaman dilimlerini sırayla tarar. Her TF için taze bir AlgoTrader+MultipleTrader kurup
    /// çalıştırır, sonucu Results'a ekler ve csvPath/txtPath'e satır satır yazar. Bir TF'de hata
    /// olursa (dosya yok, veri boş, config hatası vb.) o TF Success=false olarak işaretlenir,
    /// tarama devam eder (fail-soft).
    /// </summary>
    public async Task RunAsync(MultiStrategyTimeframeScannerOptions options, string csvPath, string txtPath, CancellationToken ct = default)
    {
        Results.Clear();

        if (options.Timeframes.Count == 0)
        {
            LogManager.LogWarning("MultiStrategyTimeframeScanner: taranacak zaman dilimi listesi boş.");
            return;
        }

        if (options.ConfigureAlgoTrader == null)
        {
            LogManager.LogError("MultiStrategyTimeframeScanner: ConfigureAlgoTrader delegate'i atanmamış.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(csvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(csvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(txtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        _statsHeader = null;
        bool headerWritten = false;

        for (int idx = 0; idx < options.Timeframes.Count; idx++)
        {
            ct.ThrowIfCancellationRequested();

            var tf = options.Timeframes[idx];
            var filePath = Path.Combine(options.BaseFolder, tf, options.Symbol + ".csv");
            var result = await RunSingleTimeframeAsync(tf, filePath, options, ct);
            Results.Add(result);

            if (!headerWritten && _statsHeader != null)
            {
                string header = $"Timeframe;{_statsHeader};SonYon;SonKarZararFiyat;SonKarZararYuzde;SonSinyaldenBeriBarSayisi;TaramaOzeti";
                csvWriter.WriteLine(header);
                txtWriter.WriteLine(header);
                headerWritten = true;
            }

            string row = result.Success
                ? $"{result.Timeframe};{result.StatisticsDataRow};{result.SonYon};{result.SonKarZararFiyat.ToString("F2", CultureInfo.InvariantCulture)};{result.SonKarZararYuzde.ToString("F2", CultureInfo.InvariantCulture)};{result.SonSinyaldenBeriBarSayisi};{result.TaramaOzeti}"
                : $"{result.Timeframe};HATA: {result.ErrorMessage}";
            csvWriter.WriteLine(row);
            txtWriter.WriteLine(row);
            csvWriter.Flush();
            txtWriter.Flush();

            OnProgress?.Invoke(idx + 1, options.Timeframes.Count, tf);
        }
    }

    private async Task<TimeframeScanResult> RunSingleTimeframeAsync(string tf, string filePath, MultiStrategyTimeframeScannerOptions options, CancellationToken ct)
    {
        var result = new TimeframeScanResult { Timeframe = tf, FilePath = filePath };

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

            algoTrader = new AlgoTrader($"scan_{tf}");
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
            LogManager.LogError($"MultiStrategyTimeframeScanner: '{tf}' işlenirken hata: {ex.Message}");
        }
        finally
        {
            algoTrader?.Dispose();
            reader?.Dispose();
        }

        return result;
    }

    /// <summary>Başarılı sonuçları SortField'e göre sıralayıp ayrı bir CSV/TXT çifti yazar.</summary>
    public void WriteSortedResults(MultiStrategyTimeframeScannerOptions options, string sortedCsvPath, string sortedTxtPath)
    {
        var ordered = options.SortDescending
            ? Results.Where(r => r.Success).OrderByDescending(r => r.SortValue)
            : Results.Where(r => r.Success).OrderBy(r => r.SortValue);
        var sorted = ordered.ToList();

        if (sorted.Count == 0) return;

        Directory.CreateDirectory(Path.GetDirectoryName(sortedCsvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(sortedCsvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(sortedTxtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        var header = $"Timeframe;{_statsHeader};SonYon;SonKarZararFiyat;SonKarZararYuzde;SonSinyaldenBeriBarSayisi;TaramaOzeti";
        csvWriter.WriteLine(header);
        txtWriter.WriteLine(header);

        foreach (var r in sorted)
        {
            string row = $"{r.Timeframe};{r.StatisticsDataRow};{r.SonYon};{r.SonKarZararFiyat.ToString("F2", CultureInfo.InvariantCulture)};{r.SonKarZararYuzde.ToString("F2", CultureInfo.InvariantCulture)};{r.SonSinyaldenBeriBarSayisi};{r.TaramaOzeti}";
            csvWriter.WriteLine(row);
            txtWriter.WriteLine(row);
        }
    }

    /// <summary>SortField'e göre en iyi sonucu döner (Success=false olanlar hariç).</summary>
    public TimeframeScanResult? GetBestResult(MultiStrategyTimeframeScannerOptions options)
    {
        var query = Results.Where(r => r.Success);
        return options.SortDescending
            ? query.OrderByDescending(r => r.SortValue).FirstOrDefault()
            : query.OrderBy(r => r.SortValue).FirstOrDefault();
    }

    public void Dispose()
    {
        // AlgoTrader/StockDataReader zaten TF başına Dispose ediliyor
        // (RunSingleTimeframeAsync içindeki finally bloğu) — burada tutulan bir kaynak yok.
    }
}

/// <summary>MultiStrategyTimeframeScanner.RunAsync() için giriş parametreleri.</summary>
public class MultiStrategyTimeframeScannerOptions
{
    public string BaseFolder { get; set; } = "";

    /// <summary>Dosya adı köküyle birebir (örn. "BTCUSDT_BNC").</summary>
    public string Symbol { get; set; } = "";

    /// <summary>Zaman dilimi klasör adları (örn. ["01","05","15","60"]).</summary>
    public List<string> Timeframes { get; set; } = new();

    /// <summary>
    /// Her TF için taze kurulan AlgoTrader'ı MultipleTrader konfigürasyonuyla (child stratejiler,
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
