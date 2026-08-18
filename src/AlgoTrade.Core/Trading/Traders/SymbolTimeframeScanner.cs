using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Strategies;
using AlgoTrade.Core.Trading.Strategy;
using System.Globalization;
using static AlgoTrade.Core.StockDataReader.StockDataReader;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Aynı stratejiyi hem sembol hem zaman dilimi ekseninde, ikisi de TAMAMEN BAĞIMSIZ olacak
/// şekilde tarar (bkz. docs/tarama-motoru-plan.md — Senaryo 6). N sembol × M zaman dilimi =
/// N×M ayrı backtest, hiçbir eksende konsensüs/bileşke yok (A'daki ve todo.md'deki düzeltmeyle
/// tutarlı: "bileşke" sadece strateji ekseninde, MultipleTrader/Senaryo 4/7/8'de).
///
/// SymbolScanner (Yapı Taşı C) ve TimeframeScanner'ın (Yapı Taşı A) iç içe geçmiş hali —
/// per-item mantık (dosya oku → IndicatorManager → StrategyRegistry.CreateStrategy →
/// SingleTrader → sinyal bayraklarını enable et → bar-bar Run → Finalize → sonuç topla →
/// Dispose) o iki sınıfla birebir aynı, üçüncü kopya, aynı iskelet (projenin "her biri kendi
/// başına yeten dosya" tarzına uygun).
/// </summary>
public class SymbolTimeframeScanner : IDisposable
{
    private readonly LogManager? _logger;
    private string? _statsHeader;

    public List<SymbolTimeframeScanResult> Results { get; } = new();

    /// <summary>(işlenen sıra, toplam hücre sayısı, "Sembol/TF")</summary>
    public Action<int, int, string>? OnProgress { get; set; }

    public SymbolTimeframeScanner(LogManager? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sembol × zaman dilimi matrisini sırayla tarar (dış döngü sembol, iç döngü TF). Her
    /// hücre için tam bir backtest (SingleTrader) çalıştırır, sonucu Results'a ekler ve
    /// csvPath/txtPath'e satır satır (ilerleyerek) yazar. Bir hücrede hata olursa (dosya yok,
    /// veri boş, strateji hatası vb.) o hücre Success=false olarak işaretlenir, tarama devam
    /// eder (fail-soft).
    /// </summary>
    public void Run(SymbolTimeframeScanOptions options, string csvPath, string txtPath, CancellationToken ct = default)
    {
        Results.Clear();

        var symbols = ResolveSymbols(options);
        if (symbols.Count == 0)
        {
            LogManager.LogWarning($"SymbolTimeframeScanner: '{options.BaseFolder}\\{options.ReferenceTimeframe}' içinde taranacak sembol bulunamadı.");
            return;
        }
        if (options.Timeframes.Count == 0)
        {
            LogManager.LogWarning("SymbolTimeframeScanner: taranacak zaman dilimi listesi boş.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(csvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(csvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(txtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        var strategyRegistry = new StrategyRegistry();
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
                var result = RunSingleSymbolTimeframe(symbol, tf, filePath, options, strategyRegistry);
                Results.Add(result);

                if (!headerWritten && _statsHeader != null)
                {
                    string header = $"Symbol;Timeframe;{_statsHeader};SonYon;SonKarZararFiyat;SonKarZararYuzde;SonSinyaldenBeriBarSayisi;TaramaOzeti";
                    csvWriter.WriteLine(header);
                    txtWriter.WriteLine(header);
                    headerWritten = true;
                }

                string row = result.Success
                    ? $"{result.Symbol};{result.Timeframe};{result.StatisticsDataRow};{result.SonYon};{result.SonKarZararFiyat.ToString("F2", CultureInfo.InvariantCulture)};{result.SonKarZararYuzde.ToString("F2", CultureInfo.InvariantCulture)};{result.SonSinyaldenBeriBarSayisi};{result.TaramaOzeti}"
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

    private List<string> ResolveSymbols(SymbolTimeframeScanOptions options)
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

    private SymbolTimeframeScanResult RunSingleSymbolTimeframe(string symbol, string tf, string filePath,
        SymbolTimeframeScanOptions options, StrategyRegistry strategyRegistry)
    {
        var result = new SymbolTimeframeScanResult { Symbol = symbol, Timeframe = tf, FilePath = filePath };

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

            trader = new SingleTrader(0, $"{symbol}_{tf}", data, indicators, _logger);
            trader.Reset();
            trader.RunMode = TraderRunMode.TradeOnly;
            // OptimizationEnabled bilinçli olarak false bırakılıyor: Finalize() içinde
            // GetPerformansParams/CalculatePerformances'ın tam çalışması gerekiyor,
            // aksi halde GetStatisticsDataRow'daki performans kolonları boş/sıfır kalır.
            trader.initialTradeParams!.ApplyFrom(options.TradeParams);

            // ConfigureUserFlagsOnce() her şeyi false'a resetler (SymbolScanner/TimeframeScanner'da
            // bulunan bug'ın aynısı — açıkça enable etmezsek trader hiçbir sinyali işleme almaz).
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

            if (options.WriteFullStatsPerCell)
            {
                var cellOutDir = Path.Combine(AppSettings.ScanLogsDir, symbol, tf);
                Directory.CreateDirectory(cellOutDir);
                trader.WriteStatisticsToFile(cellOutDir, AppSettings.ConfigsDir);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.SortValue = double.MinValue;
            LogManager.LogError($"SymbolTimeframeScanner: '{symbol}/{tf}' işlenirken hata: {ex.Message}");
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

    /// <summary>Başarılı sonuçları SortField'e göre sıralayıp ayrı bir CSV/TXT çifti yazar (gruplama yok, global sıralama).</summary>
    public void WriteSortedResults(SymbolTimeframeScanOptions options, string sortedCsvPath, string sortedTxtPath)
    {
        var ordered = options.SortDescending
            ? Results.Where(r => r.Success).OrderByDescending(r => r.SortValue)
            : Results.Where(r => r.Success).OrderBy(r => r.SortValue);
        var sorted = ordered.ToList();

        if (sorted.Count == 0) return;

        Directory.CreateDirectory(Path.GetDirectoryName(sortedCsvPath) ?? AppSettings.ScanLogsDir);

        using var csvWriter = new StreamWriter(new FileStream(sortedCsvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
        using var txtWriter = new StreamWriter(new FileStream(sortedTxtPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        var header = $"Symbol;Timeframe;{_statsHeader};SonYon;SonKarZararFiyat;SonKarZararYuzde;SonSinyaldenBeriBarSayisi;TaramaOzeti";
        csvWriter.WriteLine(header);
        txtWriter.WriteLine(header);

        foreach (var r in sorted)
        {
            string row = $"{r.Symbol};{r.Timeframe};{r.StatisticsDataRow};{r.SonYon};{r.SonKarZararFiyat.ToString("F2", CultureInfo.InvariantCulture)};{r.SonKarZararYuzde.ToString("F2", CultureInfo.InvariantCulture)};{r.SonSinyaldenBeriBarSayisi};{r.TaramaOzeti}";
            csvWriter.WriteLine(row);
            txtWriter.WriteLine(row);
        }
    }

    /// <summary>SortField'e göre en iyi sonucu döner (Success=false olanlar hariç, tüm matriste global en iyi).</summary>
    public SymbolTimeframeScanResult? GetBestResult(SymbolTimeframeScanOptions options)
    {
        var query = Results.Where(r => r.Success);
        return options.SortDescending
            ? query.OrderByDescending(r => r.SortValue).FirstOrDefault()
            : query.OrderBy(r => r.SortValue).FirstOrDefault();
    }

    public void Dispose()
    {
        // StockDataReader/IndicatorManager/SingleTrader zaten hücre başına Dispose ediliyor
        // (RunSingleSymbolTimeframe içindeki finally bloğu) — burada tutulan bir kaynak yok.
    }
}

/// <summary>SymbolTimeframeScanner.Run() için giriş parametreleri.</summary>
public class SymbolTimeframeScanOptions
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

    public string StrategyName { get; set; } = "";
    public Dictionary<string, object> StrategyParameters { get; set; } = new();

    /// <summary>Zaten kurulmuş (BuildInitialTradeParams ile), tüm hücrelerde aynı şekilde uygulanır.</summary>
    public InitialTradeParams TradeParams { get; set; } = new();

    // Sinyal etkinleştirme bayrakları (SingleTrader.ConfigureUserFlagsOnce() varsayılan olarak
    // hepsini false yapar — SymbolScanner/TimeframeScanner'da bulunan bug burada baştan doğru işlendi).
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

    public bool WriteFullStatsPerCell { get; set; } = false;

    public string SortField { get; set; } = "NetProfit";
    public bool SortDescending { get; set; } = true;
}

/// <summary>Tek bir (Sembol, Zaman Dilimi) hücresi için tarama sonucu.</summary>
public class SymbolTimeframeScanResult
{
    public string Symbol { get; set; } = "";
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
