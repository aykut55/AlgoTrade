using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Strategy;
using AlgoTrade.Core.Trading.Utils;
using OptimizationSummary = AlgoTrade.Core.Trading.Statistics.Statistics.OptimizationSummary;

namespace AlgoTrade.Core.Trading;

// ==========================================================================
// StrategyFactory delegate
// ==========================================================================
public delegate IStrategy StrategyFactory(List<StockData> data, IndicatorManager indicators, Dictionary<string, object> parameters);

// ==========================================================================
// ParameterRange - Parametre araligi tanimlama
// ==========================================================================
public class ParameterRange
{
    public string Name { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Step { get; set; }

    public ParameterRange(string name, double min, double max, double step)
    {
        Name = name;
        Min = min;
        Max = max;
        Step = step;
    }

    public List<double> GetValues()
    {
        var values = new List<double>();
        for (double v = Min; v <= Max + Step * 0.001; v += Step)
        {
            values.Add(Math.Round(v, 10));
        }
        return values;
    }
}

// ==========================================================================
// OptimizationResult - Her kombinasyonun sonucu
// ==========================================================================
public class OptimizationResult
{
    /// <summary>Test edilen parametre kombinasyonu (ör. {"Period": "20", "StopLoss": "50"})</summary>
    public Dictionary<string, string> Parameters { get; set; }

    /// <summary>GetOptimizationSummary()'den gelen tüm istatistik değerleri</summary>
    public Dictionary<string, string> Values { get; set; }

    // Convenience getters — sıralama / GetBestResult için
    public double NetProfit       => TryGetD("NetProfit");
    public double WinRate         => TryGetD("WinRate");
    public double ProfitFactor    => TryGetD("ProfitFactor");
    public double ProfitFactorNet => TryGetD("ProfitFactorNet");
    public double MaxDrawdown     => TryGetD("MaxDrawdown");
    public string StrategyName    => Values.GetValueOrDefault("StrategyName", "");

    public OptimizationResult()
    {
        Parameters = new Dictionary<string, string>();
        Values     = new Dictionary<string, string>();
    }

    private double TryGetD(string key)
        => Values.TryGetValue(key, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0.0;
}

// ==========================================================================
// SingleTraderOptimizer
// ==========================================================================
public class SingleTraderOptimizer : IDisposable
{
    #region Properties

    public int Id { get; private set; }
    public List<StockData> Data { get; private set; }
    public IndicatorManager Indicators { get; private set; }
    public StrategyFactory? StrategyFactoryMethod { get; private set; }
    public List<ParameterRange> ParameterRanges { get; private set; }
    public List<OptimizationResult> Results { get; private set; }
    public List<Dictionary<string, object>> AllCombinations { get; private set; }
    public bool IsInitialized { get; private set; }

    private LogManager? Logger { get; set; }

    // Progress callbacks
    public event Action<SingleTraderOptimizer, int, int, double>? OnOptimizationProgress;  // (this, currentCombination, totalCombinations, percentage)
    public Action<SingleTrader, int, int, double>? OnSingleTraderProgressCallback { get; set; } // (trader, currentBar, totalBars, percentage)
    public event Action<SingleTraderOptimizer, SingleTrader, int>? OnReadOptimizationResultsFile;  // (this, singleTrader, currentCombination)

    // State flags
    public bool IsStarted { get; internal set; }
    public bool IsRunning { get; internal set; }
    public bool IsStopped { get; internal set; }
    public bool IsStopRequested { get; internal set; }

    // Optimization range (PartialOpt)
    public int OptimizationFrom { get; set; } = -1;   // -1 = en bastan
    public int OptimizationTo { get; set; } = -1;     // -1 = en sona kadar

    // Save intermediate results
    public int SaveEveryN { get; set; }  // Her kaç kombinasyonda bir ara sonuç kaydet (0 = disable)
    public event Action<List<OptimizationResult>, int>? OnSaveResults;  // (results, currentCombination)

    // Optimization log file settings
    public bool CsvFileLoggingEnabled { get; set; }
    public string CsvFilePath { get; set; } = "";
    public bool TxtFileLoggingEnabled { get; set; }
    public string TxtFilePath { get; set; } = "";
    public bool AppendEnabled { get; set; }
    public string ConfigFilePath { get; set; } = "";

    // Sorted output
    public string SortField { get; set; } = "ProfitFactor";
    public string SortedCsvFilePath { get; set; } = "";
    public string SortedTxtFilePath { get; set; } = "";

    // Tracks which output files have been created/cleared in the current run (used when AppendEnabled=false)
    private readonly HashSet<string> _initializedFiles = new HashSet<string>();

    // Cached opt results for sorted output (loaded from file on first WriteSortedFiles() call)
    private List<(int CombNo, OptimizationResult Result)>? _cachedOptResults = null;

    #endregion

    #region Constructor

    public SingleTraderOptimizer(int id, List<StockData> data, IndicatorManager indicators, LogManager? logger)
    {
        Id = id;
        Data = data;
        Indicators = indicators;
        Logger = logger;
        ParameterRanges = new List<ParameterRange>();
        Results = new List<OptimizationResult>();
        AllCombinations = new List<Dictionary<string, object>>();
        IsInitialized = true;
    }

    #endregion

    #region Configuration

    public void AddParameterRange(string name, double min, double max, double step)
    {
        ParameterRanges.Add(new ParameterRange(name, min, max, step));
    }

    public void SetStrategyFactory(StrategyFactory factory)
    {
        StrategyFactoryMethod = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    #endregion

    #region Run

    public void Reset()
    {
        IsStarted = false;
        IsRunning = false;
        IsStopped = false;
        IsStopRequested = false;
        _initializedFiles.Clear();
        _cachedOptResults = null;
    }

    public void Init()
    {
    }

    public void Stop()
    {
        if (IsRunning)
        {
            IsStopRequested = true;
            LogManager.LogRaw("Stop requested - optimization will stop after current iteration");
        }
    }

    public SingleTrader createSingleTrader()
    {
        // TODO : RunSingleTraderWithProgressAsync(CancellationToken cancellationToken = default) içindeki 
        // singleTrader = new SingleTrader(..) ile başlayan ve singleTrader.Init(); ile biten ksımlar arasını
        // bırasıyla kontrol et. SignleTrader kısmı en son halini aldı. oradakilerin buraya map'i tamam mı?

        var singleTrader = new SingleTrader(0, "singleTrader", this.Data, Indicators, Logger);
        if (singleTrader == null)
            throw new InvalidOperationException("singleTrader can not be created...");

        // Assign callbacks
        singleTrader.ClearCallbacks()
                    .SetCallbacks(OnSingleTraderReset, OnSingleTraderInit, OnSingleTraderRun, OnSingleTraderFinal, OnSingleTraderBeforeOrder, OnSingleTraderNotifySignal, OnSingleTraderAfterOrder, OnSingleTraderProgress);

        // Reset
        singleTrader.Reset();

        // Set attributes
        singleTrader.SymbolName             = this.SymbolName;
        singleTrader.SymbolPeriod           = this.SymbolPeriod;
        singleTrader.SystemId               = this.SystemId;
        singleTrader.SystemName             = this.SystemName;
        singleTrader.StrategyId             = this.StrategyId;
        singleTrader.StrategyName           = this.StrategyName;
        singleTrader.QueryId                = this.QueryId;
        singleTrader.QueryName              = this.QueryName;
        singleTrader.LastExecutionTime      = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
        singleTrader.LastExecutionTimeStart = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

        // Configure position sizing
        singleTrader.initialTradeParams!.Reset().SetBakiyeParams(ilkBakiye: IlkBakiye).SetKontratParamsViopEndex(kontratSayisi: KontratSayisi).SetKomisyonParams(komisyonCarpan: KomisyonCarpan).SetKaymaParams(kaymaMiktari: KaymaMiktari);

        // Siralama Onemli
        // Apply user flags
        OnApplyUserFlags(singleTrader);

        // Apply user flags (2)
        OnApplyUserFlags2(singleTrader);

        // Configure equity curve filter
        SetSingleTraderConfigureEquityCurveFilter(singleTrader);

        // Assign runMode
        singleTrader.RunMode = TraderRunMode.TradeOnly;

        // Init
        singleTrader.Init();

        return singleTrader;
    }

    public void runSingleTrader(SingleTrader singleTrader, int totalBars, CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < totalBars; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (i % 1000 == 0)
            {
                double barPct = totalBars > 0 ? (double)i / totalBars * 100.0 : 0.0;
                OnSingleTraderProgressCallback?.Invoke(singleTrader, i, totalBars, barPct);
            }

            if (IsStopRequested)
                break;

            singleTrader.Run(i);
        }
    }
    
    public OptimizationResult? Run(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Optimizer not initialized");
        if (StrategyFactoryMethod == null)
            throw new InvalidOperationException("StrategyFactory must be set before running. Use SetStrategyFactory().");
        if (ParameterRanges.Count == 0)
            throw new InvalidOperationException("No parameter ranges defined. Use AddParameterRange().");
        if (AllCombinations == null || AllCombinations.Count == 0)
            throw new InvalidOperationException("No combinations generated. Call GenerateParameterCombinations() first.");

        // State flags
        IsStarted = true;
        IsRunning = true;
        IsStopped = false;
        IsStopRequested = false;

        int totalBars = Data.Count;
        var indicators = this.Indicators;

        Results.Clear();
        int totalCombinations = AllCombinations.Count;
        int currentCombination = 0;

        // Resolve From/To (-1 means start/end)
        int effectiveFrom = OptimizationFrom == -1 ? 1 : OptimizationFrom;
        int effectiveTo = OptimizationTo == -1 ? totalCombinations : OptimizationTo;

        LogManager.LogRaw("");
        LogManager.LogRaw($"Starting optimization: {totalCombinations} combinations total, running [{effectiveFrom}-{effectiveTo}]");
        foreach (var range in ParameterRanges)
        {
            LogManager.LogRaw($"  - {range.Name}: {range.Min} to {range.Max} (step: {range.Step})");
        }
        LogManager.LogRaw("");
        var headerLine = AlgoTrade.Core.Trading.Statistics.Statistics.GetOptimizationProgressHeader(AllCombinations[0].Keys);
        LogManager.LogRaw($"{headerLine}");

        // Her kombinasyon icin
        foreach (var paramCombo in AllCombinations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsStopRequested)
            {
                LogManager.LogRaw($"Optimization stopped at combination {currentCombination}/{totalCombinations}");
                break;
            }

            currentCombination++;

            // PartialOpt: skip combinations outside [From-To] range
            if (currentCombination < effectiveFrom)
                continue;
            if (currentCombination > effectiveTo)
                break;

            //if (currentCombination > 5)
                //break;

            //LogManager.LogRaw($"");

            // Progress raporla
            double combPct = totalCombinations > 0 ? (double)currentCombination / totalCombinations * 100.0 : 0.0;
            OnOptimizationProgress?.Invoke(this, currentCombination, totalCombinations, combPct);

            string paramsStr = string.Join(", ", paramCombo.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            //LogManager.LogRaw($"  [{currentCombination}/{totalCombinations}] {paramsStr}");

            // Create strategy
            var strategy = StrategyFactoryMethod!(this.Data, Indicators, paramCombo);

            // Create singleTrader
            SingleTrader singleTrader = createSingleTrader();

            // Assign strategy (factory'den)
            singleTrader.SetStrategy(strategy);

            // Set state flags
            singleTrader.IsStarted = true;
            singleTrader.IsRunning = true;
            singleTrader.IsStopped = false;
            singleTrader.IsStopRequested = false;

            // Run singleTrader
            runSingleTrader(singleTrader, totalBars, cancellationToken);

            // Collect singleTrader statistics
            singleTrader.Finalize();

            // Update state flags
            singleTrader.IsRunning = false;
            singleTrader.IsStopped = true;

            // Get optimization results map
            var optResultsMap = singleTrader.statistics.GetOptimizationSummary();

            // Build OptimizationResult
            var optResult = new OptimizationResult();
            foreach (var kvp in paramCombo)
                optResult.Parameters[kvp.Key] = kvp.Value?.ToString() ?? "";
            optResult.Values = new Dictionary<string, string>(optResultsMap);

            Results.Add(optResult);

            var progressLine = singleTrader.statistics.GetOptimizationProgressLine(currentCombination, totalCombinations, paramCombo);
            LogManager.LogRaw($"{progressLine}");

            // Append to files
            AppendSingleOptSummaryToFiles(optResult, currentCombination);

            // Report optimization progress
            OnReadOptimizationResultsFile?.Invoke(this, singleTrader, currentCombination);

            // Intermediate save check
            if (SaveEveryN > 0 && currentCombination % SaveEveryN == 0)
            {
                //LogManager.LogRaw($"Saving intermediate results at combination {currentCombination}...");
                //OnSaveResults?.Invoke(Results, currentCombination);
            }

            // Temizlik
            strategy?.Dispose();
            strategy = null;
            singleTrader.Dispose();
            singleTrader = null;
        }

        LogManager.LogRaw($"Optimization completed! Tested {currentCombination}/{totalCombinations} combinations");

        IsRunning = false;
        IsStopped = true;

        return GetBestResult();
    }

    public OptimizationResult? GetBestResult()
    {
        if (Results.Count == 0)
            return null;
        return Results.OrderByDescending(r => r.NetProfit).FirstOrDefault();
    }

    private void AppendSingleOptSummaryToFiles(OptimizationResult optResult, int currentCombination)
    {
        if (CsvFileLoggingEnabled && !string.IsNullOrEmpty(CsvFilePath))
        {
            try
            {
                AppendSingleOptSummaryCsvFromConfig(optResult, currentCombination, CsvFilePath);
            }
            catch (Exception ex)
            {
                LogManager.LogRaw($"Error appending OptSummary (config) to CSV: {ex.Message}");
            }
        }

        if (TxtFileLoggingEnabled && !string.IsNullOrEmpty(TxtFilePath))
        {
            try
            {
                AppendSingleOptSummaryTxtFromConfig(optResult, currentCombination, TxtFilePath);
            }
            catch (Exception ex)
            {
                LogManager.LogRaw($"Error appending OptSummary (config) to TXT: {ex.Message}");
            }
        }

        // Her kombinasyondan sonra sorted dosyaları da güncelle
        if ((CsvFileLoggingEnabled && !string.IsNullOrEmpty(SortedCsvFilePath)) ||
            (TxtFileLoggingEnabled && !string.IsNullOrEmpty(SortedTxtFilePath)))
        {
            try
            {
                WriteSortedFiles();
            }
            catch (Exception ex)
            {
                LogManager.LogRaw($"Error writing sorted opt files: {ex.Message}");
            }
        }
    }

    private void AppendSingleOptSummaryCsvFromConfig(
        OptimizationResult optResult,
        int currentCombination,
        string filePath)
    {
        var configColumns = !string.IsNullOrEmpty(ConfigFilePath)
            ? StatisticsExporter.LoadOptimizationColumns(ConfigFilePath)
            : new System.Collections.Generic.List<(string Field, string Header, int Width)>();

        // AppendEnabled=false → bu run'ın ilk yazımında dosyayı sıfırla; sonrakilerde ekle
        // AppendEnabled=true  → her zaman sona ekle, dosya yoksa yeni oluştur
        System.IO.FileMode fileMode;
        bool writeHeader;

        if (!AppendEnabled)
        {
            if (!_initializedFiles.Contains(filePath))
            {
                fileMode = System.IO.FileMode.Create;   // dosyayı sıfırla
                writeHeader = true;
                _initializedFiles.Add(filePath);
            }
            else
            {
                fileMode = System.IO.FileMode.Append;   // aynı run, sonraki kombinasyon
                writeHeader = false;
            }
        }
        else
        {
            fileMode = System.IO.FileMode.Append;
            writeHeader = !System.IO.File.Exists(filePath);
        }

        using var fs = new System.IO.FileStream(
            filePath,
            fileMode,
            System.IO.FileAccess.Write,
            System.IO.FileShare.ReadWrite);
        using var sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);

        if (writeHeader)
        {
            var headerParts = new List<string> { "CombNo" };
            foreach (var key in optResult.Parameters.Keys)
                headerParts.Add(key);
            foreach (var col in configColumns)
                headerParts.Add(col.Header);
            sw.WriteLine(string.Join(";", headerParts));
        }

        var dataParts = new List<string> { currentCombination.ToString() };
        foreach (var val in optResult.Parameters.Values)
            dataParts.Add(val ?? "");
        foreach (var col in configColumns)
            dataParts.Add(GetOptColumnValue(col.Field, optResult.Values));
        sw.WriteLine(string.Join(";", dataParts));
        sw.Flush();

        // Cache'e de ekle (WriteSortedFiles() için — eğer daha önce yüklendiyse)
        _cachedOptResults?.Add((currentCombination, optResult));
    }

    private void AppendSingleOptSummaryTxtFromConfig(
        OptimizationResult optResult,
        int currentCombination,
        string filePath)
    {
        var configColumns = !string.IsNullOrEmpty(ConfigFilePath)
            ? StatisticsExporter.LoadOptimizationColumns(ConfigFilePath)
            : new System.Collections.Generic.List<(string Field, string Header, int Width)>();

        // AppendEnabled=false → bu run'ın ilk yazımında dosyayı sıfırla; sonrakilerde ekle
        // AppendEnabled=true  → her zaman sona ekle, dosya yoksa yeni oluştur
        System.IO.FileMode fileMode;
        bool writeHeader;

        if (!AppendEnabled)
        {
            if (!_initializedFiles.Contains(filePath))
            {
                fileMode = System.IO.FileMode.Create;   // dosyayı sıfırla
                writeHeader = true;
                _initializedFiles.Add(filePath);
            }
            else
            {
                fileMode = System.IO.FileMode.Append;   // aynı run, sonraki kombinasyon
                writeHeader = false;
            }
        }
        else
        {
            fileMode = System.IO.FileMode.Append;
            writeHeader = !System.IO.File.Exists(filePath);
        }

        using var fs = new System.IO.FileStream(
            filePath,
            fileMode,
            System.IO.FileAccess.Write,
            System.IO.FileShare.ReadWrite);
        using var sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);

        // Parametre sütun genişliği: key isminden büyük, min 10
        var paramWidths = optResult.Parameters.Keys
            .ToDictionary(k => k, k => Math.Max(k.Length, 10) + 1);

        if (writeHeader)
        {
            sw.WriteLine($"OPTIMIZATION RESULTS - {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
            sw.WriteLine("".PadRight(1360, '='));

            var headerParts = new List<string>();
            headerParts.Add("CombNo".PadLeft(8));
            foreach (var key in optResult.Parameters.Keys)
                headerParts.Add(key.PadLeft(paramWidths[key]));
            foreach (var col in configColumns)
                headerParts.Add(col.Header.PadLeft(col.Width));
            sw.WriteLine(string.Join(" | ", headerParts));
            sw.WriteLine("".PadRight(1360, '-'));
        }

        var dataParts = new List<string>();
        dataParts.Add(currentCombination.ToString().PadLeft(8));
        foreach (var kvp in optResult.Parameters)
            dataParts.Add((kvp.Value ?? "").PadLeft(paramWidths[kvp.Key]));
        foreach (var col in configColumns)
            dataParts.Add(GetOptColumnValue(col.Field, optResult.Values).PadLeft(col.Width));
        sw.WriteLine(string.Join(" | ", dataParts));
        sw.Flush();
    }

    private static double ParseD(Dictionary<string, string> map, string key)
        => map.TryGetValue(key, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0.0;

    private string GetOptColumnValue(string field, Dictionary<string, string> optResultsMap)
    {
        if (string.IsNullOrEmpty(field)) return "";

        // Strip "List" suffix (e.g. BakiyeFiyatList → BakiyeFiyat) for backward compat
        var lookupField = field.EndsWith("List", StringComparison.Ordinal) ? field[..^4] : field;

        // Lookup in OptimizationResultsMap
        if (optResultsMap.TryGetValue(lookupField, out var val))
            return val ?? "";

        return "";
    }

    /// <summary>
    /// CSV dosyasını okuyup _cachedOptResults'a yükler. Sadece ilk WriteSortedFiles() çağrısında tetiklenir.
    /// </summary>
    private void LoadOptCsvToCache()
    {
        _cachedOptResults = new List<(int, OptimizationResult)>();

        if (string.IsNullOrEmpty(CsvFilePath) || !System.IO.File.Exists(CsvFilePath))
            return;

        var lines = System.IO.File.ReadAllLines(CsvFilePath, System.Text.Encoding.UTF8);
        if (lines.Length < 2) return;  // sadece header var ya da boş

        var headers = lines[0].Split(';');

        // Config kolon başlıklarını bul → bu başlıkların soldaki kalan sütunlar param
        var configColumns = !string.IsNullOrEmpty(ConfigFilePath)
            ? StatisticsExporter.LoadOptimizationColumns(ConfigFilePath)
            : new List<(string Field, string Header, int Width)>();
        var configHeaderSet = new HashSet<string>(configColumns.Select(c => c.Header));

        // headers[0] = "CombNo", headers[1..k-1] = params, headers[k..] = config fields
        int firstConfigIdx = headers.Length;
        for (int i = 1; i < headers.Length; i++)
        {
            if (configHeaderSet.Contains(headers[i]))
            {
                firstConfigIdx = i;
                break;
            }
        }

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            var cols = lines[i].Split(';');
            int combNo = cols.Length > 0 && int.TryParse(cols[0], out var n) ? n : i;

            var result = new OptimizationResult();

            // Params
            for (int j = 1; j < firstConfigIdx && j < cols.Length; j++)
                result.Parameters[headers[j]] = cols[j];

            // Values — field adına göre sakla (header adı değil)
            for (int j = firstConfigIdx; j < headers.Length && j < cols.Length; j++)
            {
                var col = configColumns.FirstOrDefault(c => c.Header == headers[j]);
                if (col.Field != null)
                    result.Values[col.Field] = cols[j];
            }

            _cachedOptResults.Add((combNo, result));
        }

        //LogManager.LogRaw($"  [Sorted] {_cachedOptResults.Count} sonuç dosyadan yüklendi ({CsvFilePath})");
    }

    /// <summary>
    /// Tüm opt sonuçlarını SortField'e göre sıralayıp sorted dosyalara yazar.
    /// İlk çağrıda CSV dosyasından yükler; sonraki çağrılarda cache'i kullanır.
    /// </summary>
    public void WriteSortedFiles()
    {
        // İlk kez → dosyadan yükle
        if (_cachedOptResults == null)
            LoadOptCsvToCache();

        if (_cachedOptResults == null || _cachedOptResults.Count == 0)
            return;

        // SortField'e göre sırala (desc)
        var sorted = _cachedOptResults
            .OrderByDescending(r =>
            {
                var v = r.Result.Values.GetValueOrDefault(SortField, "");
                return double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : double.MinValue;
            })
            .ToList();

        if (CsvFileLoggingEnabled && !string.IsNullOrEmpty(SortedCsvFilePath))
        {
            try { WriteSortedCsv(sorted, SortedCsvFilePath); }
            catch (Exception ex) { LogManager.LogRaw($"  [Sorted] CSV yazma hatası: {ex.Message}"); }
        }

        if (TxtFileLoggingEnabled && !string.IsNullOrEmpty(SortedTxtFilePath))
        {
            try { WriteSortedTxt(sorted, SortedTxtFilePath); }
            catch (Exception ex) { LogManager.LogRaw($"  [Sorted] TXT yazma hatası: {ex.Message}"); }
        }
    }

    private void WriteSortedCsv(List<(int CombNo, OptimizationResult Result)> sorted, string filePath)
    {
        var configColumns = !string.IsNullOrEmpty(ConfigFilePath)
            ? StatisticsExporter.LoadOptimizationColumns(ConfigFilePath)
            : new List<(string Field, string Header, int Width)>();

        using var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
        using var sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);

        // Header
        var first = sorted[0].Result;
        var headerParts = new List<string> { "CombNo" };
        foreach (var key in first.Parameters.Keys) headerParts.Add(key);
        foreach (var col in configColumns) headerParts.Add(col.Header);
        sw.WriteLine(string.Join(";", headerParts));

        // Data
        foreach (var (combNo, result) in sorted)
        {
            var dataParts = new List<string> { combNo.ToString() };
            foreach (var val in result.Parameters.Values) dataParts.Add(val ?? "");
            foreach (var col in configColumns) dataParts.Add(GetOptColumnValue(col.Field, result.Values));
            sw.WriteLine(string.Join(";", dataParts));
        }

        sw.Flush();
    }

    private void WriteSortedTxt(List<(int CombNo, OptimizationResult Result)> sorted, string filePath)
    {
        var configColumns = !string.IsNullOrEmpty(ConfigFilePath)
            ? StatisticsExporter.LoadOptimizationColumns(ConfigFilePath)
            : new List<(string Field, string Header, int Width)>();

        using var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
        using var sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);

        var first = sorted[0].Result;
        var paramWidths = first.Parameters.Keys.ToDictionary(k => k, k => Math.Max(k.Length, 10) + 1);

        sw.WriteLine($"OPTIMIZATION RESULTS (sorted by {SortField}) - {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
        sw.WriteLine("".PadRight(1360, '='));

        var headerParts = new List<string> { "CombNo".PadLeft(8) };
        foreach (var key in first.Parameters.Keys) headerParts.Add(key.PadLeft(paramWidths[key]));
        foreach (var col in configColumns) headerParts.Add(col.Header.PadLeft(col.Width));
        sw.WriteLine(string.Join(" | ", headerParts));
        sw.WriteLine("".PadRight(1360, '-'));

        foreach (var (combNo, result) in sorted)
        {
            var dataParts = new List<string> { combNo.ToString().PadLeft(8) };
            foreach (var kvp in result.Parameters)
                dataParts.Add((kvp.Value ?? "").PadLeft(paramWidths.GetValueOrDefault(kvp.Key, 11)));
            foreach (var col in configColumns)
                dataParts.Add(GetOptColumnValue(col.Field, result.Values).PadLeft(col.Width));
            sw.WriteLine(string.Join(" | ", dataParts));
        }

        sw.Flush();
    }

    #endregion

    #region Parameter Combinations

    public List<Dictionary<string, object>> GenerateParameterCombinations()
    {
        if (ParameterRanges == null || ParameterRanges.Count == 0)
            return new List<Dictionary<string, object>>();

        var results = new List<Dictionary<string, object>>();
        GenerateCombinationsRecursive(0, new Dictionary<string, object>(), results);
        AllCombinations = results;
        return results;
    }

    private void GenerateCombinationsRecursive(int paramIndex, Dictionary<string, object> current, List<Dictionary<string, object>> results)
    {
        if (paramIndex >= ParameterRanges.Count)
        {
            results.Add(new Dictionary<string, object>(current));
            return;
        }

        var range = ParameterRanges[paramIndex];
        var values = range.GetValues();

        foreach (var value in values)
        {
            current[range.Name] = value;
            GenerateCombinationsRecursive(paramIndex + 1, current, results);
        }
    }

    #endregion

    #region SingleTrader Callbacks (no-op)

    private void OnSingleTraderReset(SingleTrader trader, int mode) { }
    private void OnSingleTraderInit(SingleTrader trader, int mode) { }
    private void OnSingleTraderRun(SingleTrader trader, int mode) { }
    private void OnSingleTraderFinal(SingleTrader trader, int mode) { }
    private void OnSingleTraderBeforeOrder(SingleTrader trader, int barIndex) { }
    private void OnSingleTraderNotifySignal(SingleTrader trader, string signal, int barIndex) { }
    private void OnSingleTraderAfterOrder(SingleTrader trader, int barIndex) { }
    private void OnSingleTraderProgress(SingleTrader trader, int currentBar, int totalBars, double percentage)
    {
        OnSingleTraderProgressCallback?.Invoke(trader, currentBar, totalBars, percentage);
    }

    private void OnApplyUserFlags(SingleTrader trader)
    {
        trader.ConfigureUserFlagsOnce();

        // 0 id'li trader icin (SingleTrader için bu default'dur)

        trader.signals.AlEnabled                   = true;
        trader.signals.SatEnabled                  = true;
        trader.signals.FlatOlEnabled               = true;
        trader.signals.PasGecEnabled               = true;
        trader.signals.KarAlEnabled                = true;
        trader.signals.ZararKesEnabled             = true;
        trader.signals.GunSonuPozKapatEnabled      = false;     // DEFAULT = False, Ek maliyet getirir : BackTest icin anlamli 
        trader.signals.TimeFilteringEnabled        = false;     // DEFAULT = False, Ek maliyet getirir : 
        trader.signals.EquityCurveFilteringEnabled = false;     // Her zaman false olarak ilklenecek, asıl degeri dosyadan okununca geliyor

        var dateTimes           = new string[] { "2025.05.25 09:35:00", "2025.06.02 17:55:00" };
        trader.StartDateTimeStr = dateTimes[0];
        trader.StopDateTimeStr  = dateTimes[1];

        var startDateTime       = System.DateTime.ParseExact(dateTimes[0], "yyyy.MM.dd HH:mm:ss", null);
        trader.StartDateStr     = startDateTime.ToString("yyyy.MM.dd");  // "2025.05.25"
        trader.StartTimeStr     = startDateTime.ToString("HH:mm:ss");    // "14:30:00"

        var stopDateTime        = System.DateTime.ParseExact(dateTimes[1], "yyyy.MM.dd HH:mm:ss", null);
        trader.StopDateStr      = stopDateTime.ToString("yyyy.MM.dd");    // "2025.06.02"
        trader.StopTimeStr      = stopDateTime.ToString("HH:mm:ss");      // "14:00:00"
    }
    private void OnApplyUserFlags2(SingleTrader trader)
    {
        // Configure optimization flag
        trader.OptimizationEnabled = true;

        // Enable savingStatistics
        trader.SaveStatisticsToFile = false;

        // Enable all per-output statistics flags explicitly
        trader.SaveFullStatsTxtEnabled             = true;
        trader.SaveFullStatsCsvEnabled             = true;
        trader.SaveMinimalStatsTxtEnabled          = true;
        trader.SaveMinimalStatsCsvEnabled          = true;
        trader.SaveFullListsTxtEnabled             = true;
        trader.SaveFullListsCsvEnabled             = true;
        trader.SaveMinimalListsTxtEnabled          = true;
        trader.SaveMinimalListsCsvEnabled          = true;
        trader.SaveFullStatsTxtFormattedEnabled    = true;
        trader.SaveMinimalStatsTxtFormattedEnabled = true;
        trader.SavePerformansTxtEnabled            = true;
        trader.SavePerformansCsvEnabled            = true;

        // Manually assign custom output file names (as requested)
        trader.FullStatsTxtFileName                = "SingleTraderStatistics.txt";
        trader.FullStatsCsvFileName                = "SingleTraderStatistics.csv";
        trader.MinimalStatsTxtFileName             = "SingleTraderStatisticsMinimal.txt";
        trader.MinimalStatsCsvFileName             = "SingleTraderStatisticsMinimal.csv";
        trader.FullListsTxtFileName                = "SingleTraderLists.txt";
        trader.FullListsCsvFileName                = "SingleTraderLists.csv";
        trader.MinimalListsTxtFileName             = "SingleTraderListsMinimal.txt";
        trader.MinimalListsCsvFileName             = "SingleTraderListsMinimal.csv";
        trader.FullStatsTxtFormattedFileName       = "SingleTraderStatisticsFormatted.txt";
        trader.MinimalStatsTxtFormattedFileName    = "SingleTraderStatisticsMinimalFormatted.txt";
        trader.PerformansTxtFileName               = "SingleTraderPerformans.txt";
        trader.PerformansCsvFileName               = "SingleTraderPerformans.csv";
    }    

    public EquityCurveFilterConfigEntry? EquityCurveFilterConfig { get; set; }

    private void SetSingleTraderConfigureEquityCurveFilter(SingleTrader trader)
    {
        if (EquityCurveFilterConfig is null)
            return;

        trader.signals.EquityCurveFilteringEnabled = EquityCurveFilterConfig.Enabled;
        trader.ConfigureEquityCurveFilter(
            isPercent: EquityCurveFilterConfig.ThresholdTypeIsPercent,
            profitThreshold: EquityCurveFilterConfig.ProfitConfirmationThreshold,
            lossThreshold: EquityCurveFilterConfig.LossConfirmationThreshold,
            trigger: EquityCurveFilterConfig.ConfirmationTrigger
        );
    }

    #endregion

    #region Attributes (SingleTrader'a atanacak bilgiler)

    public string SymbolName { get; set; } = "";
    public string SymbolPeriod { get; set; } = "";
    public string SystemId { get; set; } = "";
    public string SystemName { get; set; } = "";
    public string StrategyId { get; set; } = "";
    public string StrategyName { get; set; } = "";
    public string QueryId { get; set; } = "";
    public string QueryName { get; set; } = "";

    // Trade params (AlgoTrader.SetOptimizationTradeParams() tarafindan set edilir)
    public double IlkBakiye      { get; set; } = 100000.0;
    public int    KontratSayisi  { get; set; } = 1;
    public double KomisyonCarpan { get; set; } = 20.0;
    public double KaymaMiktari   { get; set; } = 0.5;

    #endregion

    #region Dispose

    public void Dispose()
    {
        Results?.Clear();
        ParameterRanges?.Clear();
        OnOptimizationProgress = null;
        OnSingleTraderProgressCallback = null;
        OnReadOptimizationResultsFile = null;
        OnSaveResults = null;
    }

    #endregion
}
