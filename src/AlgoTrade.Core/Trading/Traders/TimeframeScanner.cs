using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Strategies;
using AlgoTrade.Core.Trading.Strategy;
using System.Globalization;
using static AlgoTrade.Core.StockDataReader.StockDataReader;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Aynı sembolü birden fazla zaman diliminde (farklı klasörlerdeki dosyalarda) bağımsız
/// olarak çalıştırıp sonuçları tek bir özet tabloda toplar (bkz. docs/tarama-motoru-plan.md —
/// Yapı Taşı A).
///
/// SymbolScanner'ın (Yapı Taşı C) yapısal ikizi — tek fark, sembol listesi yerine tek bir
/// sembolün N farklı zaman-dilimi klasöründeki dosyası üzerinde dönülmesi. Zaman dilimleri
/// arasında KONSENSÜS/BİLEŞKE yok — her biri tamamen bağımsız bir backtest, sonuçlar ayrı ayrı
/// raporlanır (kullanıcı bunu bilinçli olarak istedi: "sinyallerin bileşkesini almak hiç
/// aklıma gelmedi", bkz. plan-mode notu).
///
/// SymbolScanner'a benzer şekilde bilinçli olarak bağımsız bir sınıf — SymbolScanner'ı
/// genelleştirmek yerine kopyalandı (projenin "her biri kendi başına yeten dosya" tarzına
/// uygun).
/// </summary>
public class TimeframeScanner : IDisposable
{
    private readonly LogManager? _logger;
    private string? _statsHeader;

    public List<TimeframeScanResult> Results { get; } = new();

    /// <summary>(işlenen sıra, toplam TF sayısı, zaman dilimi)</summary>
    public Action<int, int, string>? OnProgress { get; set; }

    public TimeframeScanner(LogManager? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Zaman dilimlerini sırayla tarar. Her TF için tam bir backtest (SingleTrader) çalıştırır,
    /// sonucu Results'a ekler ve csvPath/txtPath'e satır satır (ilerleyerek) yazar.
    /// Bir TF'de hata olursa (dosya yok, veri boş, strateji hatası vb.) o TF Success=false
    /// olarak işaretlenir, tarama devam eder (fail-soft).
    /// </summary>
    public void Run(TimeframeScannerOptions options, string csvPath, string txtPath, CancellationToken ct = default)
    {
        Results.Clear();

        if (options.Timeframes.Count == 0)
        {
            LogManager.LogWarning("TimeframeScanner: taranacak zaman dilimi listesi boş.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(csvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(csvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(txtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        var strategyRegistry = new StrategyRegistry();
        _statsHeader = null;
        bool headerWritten = false;

        for (int idx = 0; idx < options.Timeframes.Count; idx++)
        {
            ct.ThrowIfCancellationRequested();

            var tf = options.Timeframes[idx];
            var filePath = Path.Combine(options.BaseFolder, tf, options.Symbol + ".csv");
            var result = RunSingleTimeframe(tf, filePath, options, strategyRegistry);
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

    private TimeframeScanResult RunSingleTimeframe(string tf, string filePath, TimeframeScannerOptions options,
        StrategyRegistry strategyRegistry)
    {
        var result = new TimeframeScanResult { Timeframe = tf, FilePath = filePath };

        AlgoTrade.Core.StockDataReader.StockDataReader? reader = null;
        IndicatorManager? indicators = null;
        IStrategy? strategy = null;
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
            strategy = strategyRegistry.CreateStrategy(data, indicators, _logger, options.StrategyName, options.StrategyParameters);

            trader = new SingleTrader(0, $"{options.Symbol}_{tf}", data, indicators, _logger);
            trader.Reset();
            trader.RunMode = TraderRunMode.TradeOnly;
            // OptimizationEnabled bilinçli olarak false bırakılıyor: Finalize() içinde
            // GetPerformansParams/CalculatePerformances'ın tam çalışması gerekiyor,
            // aksi halde GetStatisticsDataRow'daki performans kolonları boş/sıfır kalır.
            trader.initialTradeParams!.ApplyFrom(options.TradeParams);

            // ConfigureUserFlagsOnce() her şeyi false'a resetler (SymbolScanner'da bulunan
            // bug'ın aynısı — açıkça enable etmezsek trader hiçbir sinyali işleme almaz).
            trader.ConfigureUserFlagsOnce();
            trader.signals!.AlEnabled              = options.AlEnabled;
            trader.signals!.SatEnabled             = options.SatEnabled;
            trader.signals!.FlatOlEnabled          = options.FlatOlEnabled;
            trader.signals!.PasGecEnabled          = options.PasGecEnabled;
            trader.signals!.KarAlEnabled           = options.KarAlEnabled;
            trader.signals!.ZararKesEnabled        = options.ZararKesEnabled;
            trader.signals!.GunSonuPozKapatEnabled = options.GunSonuPozKapatEnabled;

            trader.SetStrategy(strategy);
            trader.Init();

            for (int i = 0; i < data.Count; i++)
                trader.Run(i);

            trader.Finalize();

            _statsHeader ??= trader.GetStatisticsHeaderRow(";").ToString();

            result.Success = true;
            result.BarCount = data.Count;
            result.StatisticsDataRow = trader.GetStatisticsDataRow(";").ToString();
            result.OptimizationSummary = trader.statistics.GetOptimizationSummary();
            result.SonYon = trader.SonYon;
            result.SonKarZararFiyat = trader.SonKarZararFiyat;
            result.SonKarZararYuzde = trader.SonKarZararYuzde;
            result.SonSinyaldenBeriBarSayisi = trader.SonSinyaldenBeriBarSayisi;
            result.TaramaOzeti = trader.TaramaOzeti;

            result.SortValue = (result.OptimizationSummary.TryGetValue(options.SortField, out var sortStr)
                && double.TryParse(sortStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var sortVal))
                ? sortVal
                : double.MinValue;

            if (options.WriteFullStatsPerTimeframe)
            {
                var tfOutDir = Path.Combine(AppSettings.ScanLogsDir, tf);
                Directory.CreateDirectory(tfOutDir);
                trader.WriteStatisticsToFile(tfOutDir, AppSettings.ConfigsDir);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.SortValue = double.MinValue;
            LogManager.LogError($"TimeframeScanner: '{tf}' işlenirken hata: {ex.Message}");
        }
        finally
        {
            trader?.Dispose();
            strategy?.Dispose();
            indicators?.Dispose();
            reader?.Dispose();
        }

        return result;
    }

    /// <summary>Başarılı sonuçları SortField'e göre sıralayıp ayrı bir CSV/TXT çifti yazar.</summary>
    public void WriteSortedResults(TimeframeScannerOptions options, string sortedCsvPath, string sortedTxtPath)
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
    public TimeframeScanResult? GetBestResult(TimeframeScannerOptions options)
    {
        var query = Results.Where(r => r.Success);
        return options.SortDescending
            ? query.OrderByDescending(r => r.SortValue).FirstOrDefault()
            : query.OrderBy(r => r.SortValue).FirstOrDefault();
    }

    public void Dispose()
    {
        // StockDataReader/IndicatorManager/SingleTrader zaten TF başına Dispose ediliyor
        // (RunSingleTimeframe içindeki finally bloğu) — burada tutulan bir kaynak yok.
    }
}

/// <summary>TimeframeScanner.Run() için giriş parametreleri.</summary>
public class TimeframeScannerOptions
{
    public string BaseFolder { get; set; } = "";

    /// <summary>Dosya adı köküyle birebir (örn. "BTCUSDT_BNC").</summary>
    public string Symbol { get; set; } = "";

    /// <summary>Zaman dilimi klasör adları (örn. ["01","05","15","60"]).</summary>
    public List<string> Timeframes { get; set; } = new();

    public string StrategyName { get; set; } = "";
    public Dictionary<string, object> StrategyParameters { get; set; } = new();

    /// <summary>Zaten kurulmuş (BuildInitialTradeParams ile), tüm zaman dilimlerinde aynı şekilde uygulanır.</summary>
    public InitialTradeParams TradeParams { get; set; } = new();

    // Sinyal etkinleştirme bayrakları (SingleTrader.ConfigureUserFlagsOnce() varsayılan olarak
    // hepsini false yapar — SymbolScanner'da bulunan bug burada baştan doğru işlendi).
    public bool AlEnabled { get; set; } = true;
    public bool SatEnabled { get; set; } = true;
    public bool FlatOlEnabled { get; set; } = true;
    public bool PasGecEnabled { get; set; } = true;
    public bool KarAlEnabled { get; set; } = true;
    public bool ZararKesEnabled { get; set; } = true;
    public bool GunSonuPozKapatEnabled { get; set; } = false;

    public FilterMode ReadFilterMode { get; set; } = FilterMode.All;
    public int N1 { get; set; }
    public int N2 { get; set; }
    public DateTime? Dt1 { get; set; }
    public DateTime? Dt2 { get; set; }

    public bool WriteFullStatsPerTimeframe { get; set; } = false;

    public string SortField { get; set; } = "NetProfit";
    public bool SortDescending { get; set; } = true;
}

/// <summary>Tek bir zaman dilimi için tarama sonucu.</summary>
public class TimeframeScanResult
{
    public string Timeframe { get; set; } = "";
    public string FilePath { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int BarCount { get; set; }

    /// <summary>GetStatisticsDataRow(";") — StatisticsExporterConfig.json'daki kolonlara göre tek satır.</summary>
    public string StatisticsDataRow { get; set; } = "";

    /// <summary>Statistics.GetOptimizationSummary() — SortField çözümlemesi ve programatik erişim için.</summary>
    public Dictionary<string, string> OptimizationSummary { get; set; } = new();

    public string SonYon { get; set; } = "F";
    public double SonKarZararFiyat { get; set; }
    public double SonKarZararYuzde { get; set; }
    public int SonSinyaldenBeriBarSayisi { get; set; }
    public string TaramaOzeti { get; set; } = "";

    public double SortValue { get; set; }
}
