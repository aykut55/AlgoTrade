using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Strategies;
using AlgoTrade.Core.Trading.Strategy;
using System.Globalization;
using static AlgoTrade.Core.StockDataReader.StockDataReader;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Aynı stratejiyi birden fazla sembolde bağımsız olarak çalıştırıp sonuçları tek bir
/// özet tabloda toplar (bkz. docs/tarama-motoru-plan.md — Yapı Taşı C).
///
/// SingleTraderOptimizer'dan bilinçli olarak bağımsız: Optimizer aynı veri üzerinde
/// parametre değiştirirken, tarama veriyi (dosyayı) değiştirir — bu yüzden kombinasyon
/// üretimi (GenerateParameterCombinations) yok, sadece bir sembol listesi/klasör taraması var.
/// AlgoTrader'a da bilinçli olarak bağlı değil (AlgoTrader tek veri seti varsayımıyla
/// kurulu); her sembol için kendi StockDataReader/IndicatorManager/SingleTrader nesnelerini
/// kurup sembol bazında bertaraf eder (Dispose).
/// </summary>
public class SymbolScanner : IDisposable
{
    private readonly LogManager? _logger;
    private string? _statsHeader;

    public List<ScanResult> Results { get; } = new();

    /// <summary>(işlenen sıra, toplam sembol sayısı, sembol adı)</summary>
    public Action<int, int, string>? OnProgress { get; set; }

    public SymbolScanner(LogManager? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sembolleri sırayla tarar. Her sembol için tam bir backtest (SingleTrader) çalıştırır,
    /// sonucu Results'a ekler ve csvPath/txtPath'e satır satır (ilerleyerek) yazar.
    /// Bir sembolde hata olursa (dosya yok, veri boş, strateji hatası vb.) o sembol
    /// Success=false olarak işaretlenir, tarama devam eder (fail-soft).
    /// </summary>
    public void Run(SymbolScanOptions options, string csvPath, string txtPath, CancellationToken ct = default)
    {
        Results.Clear();

        var symbols = ResolveSymbols(options);
        if (symbols.Count == 0)
        {
            LogManager.LogWarning($"SymbolScanner: '{options.DataFolder}' içinde taranacak sembol bulunamadı.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(csvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(csvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(txtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        var strategyRegistry = new StrategyRegistry();
        _statsHeader = null;
        bool headerWritten = false;

        for (int idx = 0; idx < symbols.Count; idx++)
        {
            ct.ThrowIfCancellationRequested();

            var (symbol, filePath) = symbols[idx];
            var result = RunSingleSymbol(symbol, filePath, options, strategyRegistry);
            Results.Add(result);

            if (!headerWritten && _statsHeader != null)
            {
                string header = $"Symbol;{_statsHeader};SonYon;SonKarZararFiyat;SonKarZararYuzde;SonSinyaldenBeriBarSayisi;TaramaOzeti";
                csvWriter.WriteLine(header);
                txtWriter.WriteLine(header);
                headerWritten = true;
            }

            string row = result.Success
                ? $"{result.Symbol};{result.StatisticsDataRow};{result.SonYon};{result.SonKarZararFiyat.ToString("F2", CultureInfo.InvariantCulture)};{result.SonKarZararYuzde.ToString("F2", CultureInfo.InvariantCulture)};{result.SonSinyaldenBeriBarSayisi};{result.TaramaOzeti}"
                : $"{result.Symbol};HATA: {result.ErrorMessage}";
            csvWriter.WriteLine(row);
            txtWriter.WriteLine(row);
            csvWriter.Flush();
            txtWriter.Flush();

            OnProgress?.Invoke(idx + 1, symbols.Count, symbol);
        }
    }

    private List<(string Symbol, string FilePath)> ResolveSymbols(SymbolScanOptions options)
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

    private ScanResult RunSingleSymbol(string symbol, string filePath, SymbolScanOptions options,
        StrategyRegistry strategyRegistry)
    {
        var result = new ScanResult { Symbol = symbol, FilePath = filePath };

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

            trader = new SingleTrader(0, symbol, data, indicators, _logger);
            trader.Reset();
            trader.RunMode = TraderRunMode.TradeOnly;
            // OptimizationEnabled bilinçli olarak false bırakılıyor: Finalize() içinde
            // GetPerformansParams/CalculatePerformances'ın tam çalışması gerekiyor,
            // aksi halde GetStatisticsDataRow'daki performans kolonları boş/sıfır kalır.
            trader.initialTradeParams!.ApplyFrom(options.TradeParams);

            // ConfigureUserFlagsOnce() her şeyi false'a resetler (AlgoTrader.
            // ApplySingleTraderFlagsConfigs() ile aynı akış) — açıkça enable etmezsek
            // trader hiçbir sinyali işleme almaz, sürekli Flat kalır.
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

            if (options.WriteFullStatsPerSymbol)
            {
                var symbolOutDir = Path.Combine(AppSettings.ScanLogsDir, symbol);
                Directory.CreateDirectory(symbolOutDir);
                trader.WriteStatisticsToFile(symbolOutDir, AppSettings.ConfigsDir);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.SortValue = double.MinValue;
            LogManager.LogError($"SymbolScanner: '{symbol}' işlenirken hata: {ex.Message}");
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
    public void WriteSortedResults(SymbolScanOptions options, string sortedCsvPath, string sortedTxtPath)
    {
        var ordered = options.SortDescending
            ? Results.Where(r => r.Success).OrderByDescending(r => r.SortValue)
            : Results.Where(r => r.Success).OrderBy(r => r.SortValue);
        var sorted = ordered.ToList();

        if (sorted.Count == 0) return;

        Directory.CreateDirectory(Path.GetDirectoryName(sortedCsvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(sortedCsvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(sortedTxtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        var header = $"Symbol;{_statsHeader};SonYon;SonKarZararFiyat;SonKarZararYuzde;SonSinyaldenBeriBarSayisi;TaramaOzeti";
        csvWriter.WriteLine(header);
        txtWriter.WriteLine(header);

        foreach (var r in sorted)
        {
            string row = $"{r.Symbol};{r.StatisticsDataRow};{r.SonYon};{r.SonKarZararFiyat.ToString("F2", CultureInfo.InvariantCulture)};{r.SonKarZararYuzde.ToString("F2", CultureInfo.InvariantCulture)};{r.SonSinyaldenBeriBarSayisi};{r.TaramaOzeti}";
            csvWriter.WriteLine(row);
            txtWriter.WriteLine(row);
        }
    }

    /// <summary>SortField'e göre en iyi sonucu döner (Success=false olanlar hariç).</summary>
    public ScanResult? GetBestResult(SymbolScanOptions options)
    {
        var query = Results.Where(r => r.Success);
        return options.SortDescending
            ? query.OrderByDescending(r => r.SortValue).FirstOrDefault()
            : query.OrderBy(r => r.SortValue).FirstOrDefault();
    }

    public void Dispose()
    {
        // StockDataReader/IndicatorManager/SingleTrader zaten sembol başına Dispose ediliyor
        // (RunSingleSymbol içindeki finally bloğu) — burada tutulan bir kaynak yok.
    }
}

/// <summary>SymbolScanner.Run() için giriş parametreleri.</summary>
public class SymbolScanOptions
{
    public string DataFolder { get; set; } = "";
    public bool AutoDiscover { get; set; } = true;

    /// <summary>AutoDiscover=false iken kullanılır. Her eleman dosya adı köküyle birebir (örn. "BTCUSDT_BNC").</summary>
    public List<string> SymbolList { get; set; } = new();

    public string StrategyName { get; set; } = "";
    public Dictionary<string, object> StrategyParameters { get; set; } = new();

    /// <summary>Zaten kurulmuş (BuildInitialTradeParams ile), tüm sembollerde aynı şekilde uygulanır.</summary>
    public InitialTradeParams TradeParams { get; set; } = new();

    // Sinyal etkinleştirme bayrakları (SingleTrader.ConfigureUserFlagsOnce() varsayılan olarak
    // hepsini false yapar — AlgoTrader.ApplySingleTraderFlagsConfigs() ile aynı, burada da
    // açıkça set edilmezse trader hiçbir zaman pozisyon açmaz).
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

    public bool WriteFullStatsPerSymbol { get; set; } = false;

    public string SortField { get; set; } = "NetProfit";
    public bool SortDescending { get; set; } = true;
}

/// <summary>Tek bir sembol için tarama sonucu.</summary>
public class ScanResult
{
    public string Symbol { get; set; } = "";
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
