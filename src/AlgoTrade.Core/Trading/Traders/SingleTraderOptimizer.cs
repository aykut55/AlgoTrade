using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Strategy;

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
    public Dictionary<string, object> Parameters { get; set; }

    // Temel Performans Metrikleri
    public double NetProfit { get; set; }
    public double WinRate { get; set; }
    public double ProfitFactor { get; set; }
    public double ProfitFactorNet { get; set; }
    public double MaxDrawdown { get; set; }

    // Bakiye
    public double IlkBakiyeFiyat { get; set; }
    public double BakiyeFiyat { get; set; }
    public double BakiyeFiyatNet { get; set; }
    public double GetiriFiyat { get; set; }
    public double GetiriFiyatNet { get; set; }
    public double GetiriFiyatYuzde { get; set; }
    public double GetiriFiyatYuzdeNet { get; set; }
    public double KomisyonFiyat { get; set; }

    // Islem Sayilari
    public int IslemSayisi { get; set; }
    public int KazandiranIslemSayisi { get; set; }
    public int KaybettirenIslemSayisi { get; set; }

    // Kar/Zarar
    public double ToplamKarFiyat { get; set; }
    public double ToplamZararFiyat { get; set; }
    public double NetKarFiyat { get; set; }

    // Bilgi
    public string StrategyName { get; set; }

    public OptimizationResult()
    {
        Parameters = new Dictionary<string, object>();
        StrategyName = string.Empty;
    }
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
    public event Action<int, int>? OnOptimizationProgress;         // (currentCombination, totalCombinations)
    public Action<int, int>? OnSingleTraderProgressCallback { get; set; } // (currentBar, totalBars)
    public event Action<SingleTraderOptimizer, SingleTrader, int>? OnReadOptimizationResultsFile;  // (this, singleTrader, currentCombination)

    // State flags
    public bool IsStarted { get; internal set; }
    public bool IsRunning { get; internal set; }
    public bool IsStopped { get; internal set; }
    public bool IsStopRequested { get; internal set; }

    // Save intermediate results
    public int SaveEveryN { get; set; }  // Her kaç kombinasyonda bir ara sonuç kaydet (0 = disable)
    public event Action<List<OptimizationResult>, int>? OnSaveResults;  // (results, currentCombination)

    // Optimization log file settings
    public bool CsvFileLoggingEnabled { get; set; }
    public string CsvFilePath { get; set; } = "";
    public bool TxtFileLoggingEnabled { get; set; }
    public string TxtFilePath { get; set; } = "";
    public bool AppendEnabled { get; set; }

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
        var singleTrader = new SingleTrader(0, "singleTrader", this.Data, Indicators, Logger);
        if (singleTrader == null)
            throw new InvalidOperationException("singleTrader can not be created...");

        // Assign callbacks
        singleTrader.ClearCallbacks()
                    .SetCallbacks(OnSingleTraderReset, OnSingleTraderInit, OnSingleTraderRun, OnSingleTraderFinal, OnSingleTraderBeforeOrder, OnSingleTraderNotifySignal, OnSingleTraderAfterOrder, OnSingleTraderProgress);

        // Assign runMode
        singleTrader.RunMode = TraderRunMode.TradeOnly;

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
        singleTrader.initialTradeParams!.Reset().SetBakiyeParams(ilkBakiye: 100000.0).SetKontratParamsFxParite(lotSayisi: 0.01).SetKomisyonParams(komisyonCarpan: 3.0).SetKaymaParams(kaymaMiktari: 0.5);
        singleTrader.initialTradeParams!.Reset().SetBakiyeParams(ilkBakiye: 100000.0).SetKontratParamsViopEndex(kontratSayisi: 1).SetKomisyonParams(komisyonCarpan: 20.0).SetKaymaParams(kaymaMiktari: 0.5);

        // Siralama Onemli
        // Apply user flags
        OnApplyUserFlags(singleTrader);

        // Configure equity curve filter
        SetSingleTraderConfigureEquityCurveFilter(singleTrader);

        // Enable savingStatistics
        singleTrader.SaveStatisticsToFile = true;

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
                OnSingleTraderProgressCallback?.Invoke(i, totalBars);

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

        LogManager.LogRaw($"Starting optimization: {totalCombinations} combinations to test");
        foreach (var range in ParameterRanges)
        {
            LogManager.LogRaw($"  - {range.Name}: {range.Min} to {range.Max} (step: {range.Step})");
        }

        // Her kombinasyon icin
        foreach (var paramCombo in AllCombinations)
        {
            LogManager.LogRaw($"");

            cancellationToken.ThrowIfCancellationRequested();

            if (IsStopRequested)
            {
                LogManager.LogRaw($"Optimization stopped at combination {currentCombination}/{totalCombinations}");
                break;
            }

            currentCombination++;

            // Progress raporla
            OnOptimizationProgress?.Invoke(currentCombination, totalCombinations);

            string paramsStr = string.Join(", ", paramCombo.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            LogManager.LogRaw($"  [{currentCombination}/{totalCombinations}] {paramsStr}");

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

            // Get optimization summary
            var optSummary = singleTrader.statistics.GetOptimizationSummary();

            // Create result from optimization summary
            var optResult = CreateOptimizationResultFromSummary(optSummary, paramCombo);

            Results.Add(optResult);

            LogManager.LogRaw($"  → NetProfit: {optResult.NetProfit:F2}, WinRate: {optResult.WinRate:F2}%, PF: {optResult.ProfitFactor:F2}, PFNet: {optResult.ProfitFactorNet:F2}");

            // Append to CSV and TXT files (if enabled)
            //AppendSingleResultToFiles(optResult, currentCombination);

            // Append to CSV and TXT files (if enabled)
            AppendSingleOptSummaryToFiles(optResult, optSummary, currentCombination);

            // Report optimization progress
            OnReadOptimizationResultsFile?.Invoke(this, singleTrader, currentCombination);

            // Intermediate save check
            if (SaveEveryN > 0 && currentCombination % SaveEveryN == 0)
            {
                LogManager.LogRaw($"Saving intermediate results at combination {currentCombination}...");
                OnSaveResults?.Invoke(Results, currentCombination);
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

    private OptimizationResult CreateOptimizationResultFromSummary(
        Statistics.Statistics.OptimizationSummary optSummary,
        Dictionary<string, object> paramCombo)
    {
        var result = new OptimizationResult
        {
            // Temel Performans Metrikleri
            NetProfit = optSummary.GetiriFiyatNet,
            WinRate = optSummary.KarliIslemOrani,
            ProfitFactor = optSummary.ProfitFactor,
            ProfitFactorNet = optSummary.ProfitFactorNet,
            MaxDrawdown = optSummary.GetiriMaxDD,

            // Bakiye
            IlkBakiyeFiyat = optSummary.IlkBakiyeFiyat,
            BakiyeFiyat = optSummary.BakiyeFiyat,
            BakiyeFiyatNet = optSummary.BakiyeFiyatNet,
            GetiriFiyat = optSummary.GetiriFiyat,
            GetiriFiyatNet = optSummary.GetiriFiyatNet,
            GetiriFiyatYuzde = optSummary.GetiriFiyatYuzde,
            GetiriFiyatYuzdeNet = optSummary.GetiriFiyatYuzdeNet,
            KomisyonFiyat = optSummary.KomisyonFiyat,

            // Islem Sayilari
            IslemSayisi = optSummary.IslemSayisi,
            KazandiranIslemSayisi = optSummary.KazandiranIslemSayisi,
            KaybettirenIslemSayisi = optSummary.KaybettirenIslemSayisi,

            // Kar/Zarar
            ToplamKarFiyat = optSummary.ToplamKarFiyat,
            ToplamZararFiyat = optSummary.ToplamZararFiyat,
            NetKarFiyat = optSummary.NetKarFiyat,

            // Bilgi
            StrategyName = optSummary.StrategyName ?? string.Empty
        };

        // Add all parameters to result
        foreach (var kvp in paramCombo)
        {
            result.Parameters[kvp.Key] = kvp.Value;
        }

        return result;
    }

    private void AppendSingleResultToFiles(OptimizationResult result, int currentCombination)
    {
        if (!CsvFileLoggingEnabled && !TxtFileLoggingEnabled)
            return;

        // Append to CSV
        if (CsvFileLoggingEnabled && !string.IsNullOrEmpty(CsvFilePath))
        {
            try
            {
                AppendSingleResultToCsv(result, CsvFilePath, currentCombination);
            }
            catch (Exception ex)
            {
                LogManager.LogRaw($"Error appending to CSV: {ex.Message}");
            }
        }

        // Append to TXT
        if (TxtFileLoggingEnabled && !string.IsNullOrEmpty(TxtFilePath))
        {
            try
            {
                AppendSingleResultToTxt(result, TxtFilePath, currentCombination);
            }
            catch (Exception ex)
            {
                LogManager.LogRaw($"Error appending to TXT: {ex.Message}");
            }
        }
    }

    private void AppendSingleResultToCsv(OptimizationResult result, string filePath, int currentCombination)
    {
        bool fileExists = System.IO.File.Exists(filePath);
        bool writeHeader = false;

        if (!fileExists || (!AppendEnabled && currentCombination == 1))
        {
            writeHeader = true;
        }

        using (var fs = new System.IO.FileStream(
            filePath,
            (AppendEnabled && fileExists) ? System.IO.FileMode.Append : System.IO.FileMode.Create,
            System.IO.FileAccess.Write,
            System.IO.FileShare.ReadWrite))
        using (var sw = new System.IO.StreamWriter(fs))
        {
            if (writeHeader)
            {
                var paramNames = result.Parameters.Keys.ToList();
                var header = string.Join(",", paramNames) + ",NetProfit,WinRate,ProfitFactor,ProfitFactorNet,MaxDrawdown";
                sw.WriteLine(header);
            }

            var paramValues = result.Parameters.Values.Select(v => v.ToString()).ToList();
            var metrics = new[]
            {
                result.NetProfit.ToString("F2"),
                result.WinRate.ToString("F2"),
                result.ProfitFactor.ToString("F2"),
                result.ProfitFactorNet.ToString("F2"),
                result.MaxDrawdown.ToString("F2")
            };
            var line = string.Join(",", paramValues.Concat(metrics));
            sw.WriteLine(line);
            sw.Flush();
        }
    }

    private void AppendSingleResultToTxt(OptimizationResult result, string filePath, int currentCombination)
    {
        bool fileExists = System.IO.File.Exists(filePath);
        bool writeHeader = false;

        if (!fileExists || (!AppendEnabled && currentCombination == 1))
        {
            writeHeader = true;
        }

        using (var fs = new System.IO.FileStream(
            filePath,
            (AppendEnabled && fileExists) ? System.IO.FileMode.Append : System.IO.FileMode.Create,
            System.IO.FileAccess.Write,
            System.IO.FileShare.ReadWrite))
        using (var sw = new System.IO.StreamWriter(fs))
        {
            if (writeHeader)
            {
                sw.WriteLine($"Optimization Results - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sw.WriteLine("================================================================================");
                sw.WriteLine();
            }

            sw.WriteLine($"Combination #{currentCombination}:");

            foreach (var kvp in result.Parameters)
            {
                sw.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }

            sw.WriteLine($"  NetProfit: {result.NetProfit:F2}");
            sw.WriteLine($"  WinRate: {result.WinRate:F2}%");
            sw.WriteLine($"  ProfitFactor: {result.ProfitFactor:F2}");
            sw.WriteLine($"  ProfitFactorNet: {result.ProfitFactorNet:F2}");
            sw.WriteLine($"  MaxDrawdown: {result.MaxDrawdown:F2}");
            sw.WriteLine();

            sw.Flush();
        }
    }

    private void AppendSingleOptSummaryToFiles(
        OptimizationResult optResult,
        Statistics.Statistics.OptimizationSummary optSummary,
        int currentCombination)
    {
        if (!CsvFileLoggingEnabled && !TxtFileLoggingEnabled)
            return;

        // Append to CSV
        if (CsvFileLoggingEnabled && !string.IsNullOrEmpty(CsvFilePath))
        {
            try
            {
                AppendSingleOptSummaryToCsv(optResult, optSummary, CsvFilePath, currentCombination);
            }
            catch (Exception ex)
            {
                LogManager.LogRaw($"Error appending OptSummary to CSV: {ex.Message}");
            }
        }

        // Append to TXT
        if (TxtFileLoggingEnabled && !string.IsNullOrEmpty(TxtFilePath))
        {
            try
            {
                AppendSingleOptSummaryToTxt(optResult, optSummary, TxtFilePath, currentCombination);
            }
            catch (Exception ex)
            {
                LogManager.LogRaw($"Error appending OptSummary to TXT: {ex.Message}");
            }
        }
    }

    private void AppendSingleOptSummaryToCsv(
        OptimizationResult optResult,
        Statistics.Statistics.OptimizationSummary optSummary,
        string filePath,
        int currentCombination)
    {
        bool fileExists = System.IO.File.Exists(filePath);
        bool writeHeader = false;

        if (!fileExists || (!AppendEnabled && currentCombination == 1))
        {
            writeHeader = true;
        }

        using (var fs = new System.IO.FileStream(
            filePath,
            (AppendEnabled && fileExists) ? System.IO.FileMode.Append : System.IO.FileMode.Create,
            System.IO.FileAccess.Write,
            System.IO.FileShare.ReadWrite))
        using (var sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8))
        {
            if (writeHeader)
            {
                var headerParts = new List<string> { "CombNo" };

                // Add parameter names
                if (optResult.Parameters != null && optResult.Parameters.Count > 0)
                {
                    headerParts.AddRange(optResult.Parameters.Keys);
                }

                // OptimizationResult fields
                headerParts.Add("OR_IlkBakFy");
                headerParts.Add("OR_BakFiyat");
                headerParts.Add("OR_GetFiyat");
                headerParts.Add("OR_GetFyt%");
                headerParts.Add("OR_KomFiyat");
                headerParts.Add("OR_BakFyNet");
                headerParts.Add("OR_GetFyNet");
                headerParts.Add("OR_GetFy%N");
                headerParts.Add("OR_Islem");
                headerParts.Add("OR_Kazand");
                headerParts.Add("OR_Kaybett");
                headerParts.Add("OR_TopKarFy");
                headerParts.Add("OR_TopZarFy");
                headerParts.Add("OR_NetKarFy");
                headerParts.Add("OR_KarliOra");
                headerParts.Add("OR_MaxDD");
                headerParts.Add("OR_ProfFact");
                headerParts.Add("OR_ProfFacN");
                headerParts.Add("OR_NetProf");
                headerParts.Add("OR_WinRate");
                headerParts.Add("OR_Strategy");

                // OptimizationSummary section
                headerParts.Add("OptSummary");
                headerParts.Add("TraderId");
                headerParts.Add("TraderName");
                headerParts.Add("SymbolName");
                headerParts.Add("SymbolPer");
                headerParts.Add("SystemId");
                headerParts.Add("SystemName");
                headerParts.Add("StrategyId");
                headerParts.Add("StrategyName");
                headerParts.Add("BarCnt");
                headerParts.Add("IlkBarDT");
                headerParts.Add("SonBarDT");
                headerParts.Add("IlkBakFyt");
                headerParts.Add("BakFiyat");
                headerParts.Add("GetFiyat");
                headerParts.Add("GetFyt%");
                headerParts.Add("BakFytNet");
                headerParts.Add("GetFytNet");
                headerParts.Add("GetFyt%N");
                headerParts.Add("KomFiyat");
                headerParts.Add("KomFyt%");
                headerParts.Add("MinBakFyt");
                headerParts.Add("MaxBakFyt");
                headerParts.Add("MinBakNet");
                headerParts.Add("MaxBakNet");
                headerParts.Add("Islem");
                headerParts.Add("Alis");
                headerParts.Add("Satis");
                headerParts.Add("Flat");
                headerParts.Add("Pass");
                headerParts.Add("KarAl");
                headerParts.Add("ZararKes");
                headerParts.Add("Kazand");
                headerParts.Add("Kaybett");
                headerParts.Add("Notr");
                headerParts.Add("TopKarFyt");
                headerParts.Add("TopZarFyt");
                headerParts.Add("NetKarFyt");
                headerParts.Add("MaxKarFyt");
                headerParts.Add("MaxZarFyt");
                headerParts.Add("KarliOran");
                headerParts.Add("MaxDD");
                headerParts.Add("MaxDDDate");
                headerParts.Add("MaxKayip");
                headerParts.Add("ProfitFac");
                headerParts.Add("ProfitFacNet");
                headerParts.Add("ProfFacSis");

                sw.WriteLine(string.Join(";", headerParts));
            }

            // Data row
            var dataParts = new List<string> { currentCombination.ToString() };

            // Parameter values
            if (optResult.Parameters != null && optResult.Parameters.Count > 0)
            {
                dataParts.AddRange(optResult.Parameters.Values.Select(v => v?.ToString() ?? ""));
            }

            // OptimizationResult values
            dataParts.Add(optResult.IlkBakiyeFiyat.ToString("F2"));
            dataParts.Add(optResult.BakiyeFiyat.ToString("F2"));
            dataParts.Add(optResult.GetiriFiyat.ToString("F2"));
            dataParts.Add(optResult.GetiriFiyatYuzde.ToString("F2"));
            dataParts.Add(optResult.KomisyonFiyat.ToString("F2"));
            dataParts.Add(optResult.BakiyeFiyatNet.ToString("F2"));
            dataParts.Add(optResult.GetiriFiyatNet.ToString("F2"));
            dataParts.Add(optResult.GetiriFiyatYuzdeNet.ToString("F2"));
            dataParts.Add(optResult.IslemSayisi.ToString());
            dataParts.Add(optResult.KazandiranIslemSayisi.ToString());
            dataParts.Add(optResult.KaybettirenIslemSayisi.ToString());
            dataParts.Add(optResult.ToplamKarFiyat.ToString("F2"));
            dataParts.Add(optResult.ToplamZararFiyat.ToString("F2"));
            dataParts.Add(optResult.NetKarFiyat.ToString("F2"));
            dataParts.Add(optResult.WinRate.ToString("F2"));
            dataParts.Add(optResult.MaxDrawdown.ToString("F2"));
            dataParts.Add(optResult.ProfitFactor.ToString("F2"));
            dataParts.Add(optResult.ProfitFactorNet.ToString("F2"));
            dataParts.Add(optResult.NetProfit.ToString("F2"));
            dataParts.Add(optResult.WinRate.ToString("F2"));
            dataParts.Add(optResult.StrategyName);

            // OptimizationSummary values
            dataParts.Add(""); // section placeholder
            dataParts.Add(optSummary.TraderId.ToString());
            dataParts.Add(optSummary.TraderName);
            dataParts.Add(optSummary.SymbolName);
            dataParts.Add(optSummary.SymbolPeriod);
            dataParts.Add(optSummary.SystemId);
            dataParts.Add(optSummary.SystemName);
            dataParts.Add(optSummary.StrategyId);
            dataParts.Add(optSummary.StrategyName);
            dataParts.Add(optSummary.ToplamBarSayisi.ToString());
            dataParts.Add(optSummary.IlkBarTarihSaati);
            dataParts.Add(optSummary.SonBarTarihSaati);
            dataParts.Add(optSummary.IlkBakiyeFiyat.ToString("F2"));
            dataParts.Add(optSummary.BakiyeFiyat.ToString("F2"));
            dataParts.Add(optSummary.GetiriFiyat.ToString("F2"));
            dataParts.Add(optSummary.GetiriFiyatYuzde.ToString("F2"));
            dataParts.Add(optSummary.BakiyeFiyatNet.ToString("F2"));
            dataParts.Add(optSummary.GetiriFiyatNet.ToString("F2"));
            dataParts.Add(optSummary.GetiriFiyatYuzdeNet.ToString("F2"));
            dataParts.Add(optSummary.KomisyonFiyat.ToString("F2"));
            dataParts.Add(optSummary.KomisyonFiyatYuzde.ToString("F4"));
            dataParts.Add(optSummary.MinBakiyeFiyat.ToString("F2"));
            dataParts.Add(optSummary.MaxBakiyeFiyat.ToString("F2"));
            dataParts.Add(optSummary.MinBakiyeFiyatNet.ToString("F2"));
            dataParts.Add(optSummary.MaxBakiyeFiyatNet.ToString("F2"));
            dataParts.Add(optSummary.IslemSayisi.ToString());
            dataParts.Add(optSummary.AlisSayisi.ToString());
            dataParts.Add(optSummary.SatisSayisi.ToString());
            dataParts.Add(optSummary.FlatSayisi.ToString());
            dataParts.Add(optSummary.PassSayisi.ToString());
            dataParts.Add(optSummary.KarAlSayisi.ToString());
            dataParts.Add(optSummary.ZararKesSayisi.ToString());
            dataParts.Add(optSummary.KazandiranIslemSayisi.ToString());
            dataParts.Add(optSummary.KaybettirenIslemSayisi.ToString());
            dataParts.Add(optSummary.NotrIslemSayisi.ToString());
            dataParts.Add(optSummary.ToplamKarFiyat.ToString("F2"));
            dataParts.Add(optSummary.ToplamZararFiyat.ToString("F2"));
            dataParts.Add(optSummary.NetKarFiyat.ToString("F2"));
            dataParts.Add(optSummary.MaxKarFiyat.ToString("F2"));
            dataParts.Add(optSummary.MaxZararFiyat.ToString("F2"));
            dataParts.Add(optSummary.KarliIslemOrani.ToString("F2"));
            dataParts.Add(optSummary.GetiriMaxDD.ToString("F2"));
            dataParts.Add(optSummary.GetiriMaxDDTarih);
            dataParts.Add(optSummary.GetiriMaxKayip.ToString("F2"));
            dataParts.Add(optSummary.ProfitFactor.ToString("F2"));
            dataParts.Add(optSummary.ProfitFactorNet.ToString("F2"));
            dataParts.Add(optSummary.ProfitFactorSistem.ToString("F2"));

            sw.WriteLine(string.Join(";", dataParts));
            sw.Flush();
        }
    }

    private void AppendSingleOptSummaryToTxt(
        OptimizationResult optResult,
        Statistics.Statistics.OptimizationSummary optSummary,
        string filePath,
        int currentCombination)
    {
        bool fileExists = System.IO.File.Exists(filePath);
        bool writeHeader = false;

        if (!fileExists || (!AppendEnabled && currentCombination == 1))
        {
            writeHeader = true;
        }

        using (var fs = new System.IO.FileStream(
            filePath,
            (AppendEnabled && fileExists) ? System.IO.FileMode.Append : System.IO.FileMode.Create,
            System.IO.FileAccess.Write,
            System.IO.FileShare.ReadWrite))
        using (var sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8))
        {
            if (writeHeader)
            {
                sw.WriteLine($"OPTIMIZATION RESULTS - {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
                sw.WriteLine("".PadRight(2000, '='));

                var headerBuilder = new System.Text.StringBuilder();
                headerBuilder.Append($"{"CombNo",10} | ");

                // Parameter columns
                if (optResult.Parameters != null && optResult.Parameters.Count > 0)
                {
                    foreach (var paramName in optResult.Parameters.Keys)
                    {
                        headerBuilder.Append($"{paramName,15} | ");
                    }
                }

                // OptimizationResult fields
                headerBuilder.Append(
                    $"{"OR_IlkBakFy",15} | " +
                    $"{"OR_BakFiyat",15} | " +
                    $"{"OR_GetFiyat",15} | " +
                    $"{"OR_GetFyt%",10} | " +
                    $"{"OR_KomFiyat",15} | " +
                    $"{"OR_BakFyNet",15} | " +
                    $"{"OR_GetFyNet",15} | " +
                    $"{"OR_GetFy%N",10} | " +
                    $"{"OR_Islem",10} | " +
                    $"{"OR_Kazand",10} | " +
                    $"{"OR_Kaybett",10} | " +
                    $"{"OR_TopKarFy",15} | " +
                    $"{"OR_TopZarFy",15} | " +
                    $"{"OR_NetKarFy",15} | " +
                    $"{"OR_KarliOra",10} | " +
                    $"{"OR_MaxDD",10} | " +
                    $"{"OR_ProfFact",10} | " +
                    $"{"OR_ProfFacN",10} | " +
                    $"{"OR_NetProf",15} | " +
                    $"{"OR_WinRate",10} | " +
                    $"{"OR_Strategy",20} | "
                );

                // OptimizationSummary fields
                headerBuilder.Append($"{"OptSummary",20} | ");
                headerBuilder.Append(
                    $"{"TraderId",10} | " +
                    $"{"TraderName",20} | " +
                    $"{"SymbolName",10} | " +
                    $"{"SymbolPer",10} | " +
                    $"{"SystemId",10} | " +
                    $"{"SystemName",20} | " +
                    $"{"StrategyId",10} | " +
                    $"{"StrategyName",20} | " +
                    $"{"BarCnt",10} | " +
                    $"{"IlkBarDT",20} | " +
                    $"{"SonBarDT",20} | " +
                    $"{"IlkBakFyt",15} | " +
                    $"{"BakFiyat",15} | " +
                    $"{"GetFiyat",15} | " +
                    $"{"GetFyt%",10} | " +
                    $"{"BakFytNet",15} | " +
                    $"{"GetFytNet",15} | " +
                    $"{"GetFyt%N",10} | " +
                    $"{"KomFiyat",10} | " +
                    $"{"KomFyt%",10} | " +
                    $"{"MinBakFyt",15} | " +
                    $"{"MaxBakFyt",15} | " +
                    $"{"MinBakNet",15} | " +
                    $"{"MaxBakNet",15} | " +
                    $"{"Islem",10} | " +
                    $"{"Alis",10} | " +
                    $"{"Satis",10} | " +
                    $"{"Flat",10} | " +
                    $"{"Pass",10} | " +
                    $"{"KarAl",10} | " +
                    $"{"ZararKes",10} | " +
                    $"{"Kazand",10} | " +
                    $"{"Kaybett",10} | " +
                    $"{"Notr",10} | " +
                    $"{"TopKarFyt",15} | " +
                    $"{"TopZarFyt",15} | " +
                    $"{"NetKarFyt",15} | " +
                    $"{"MaxKarFyt",15} | " +
                    $"{"MaxZarFyt",15} | " +
                    $"{"KarliOran",10} | " +
                    $"{"MaxDD",10} | " +
                    $"{"MaxDDDate",20} | " +
                    $"{"MaxKayip",10} | " +
                    $"{"ProfitFac",10} | " +
                    $"{"ProfitFacNet",10} | " +
                    $"{"ProfFacSis",10}"
                );

                sw.WriteLine(headerBuilder.ToString());
                sw.WriteLine("".PadRight(2000, '-'));
            }

            // Data row
            var dataBuilder = new System.Text.StringBuilder();
            dataBuilder.Append($"{currentCombination,10} | ");

            // Parameter values
            if (optResult.Parameters != null && optResult.Parameters.Count > 0)
            {
                foreach (var paramValue in optResult.Parameters.Values)
                {
                    dataBuilder.Append($"{paramValue,15} | ");
                }
            }

            // OptimizationResult values
            dataBuilder.Append(
                $"{optResult.IlkBakiyeFiyat,15:F2} | " +
                $"{optResult.BakiyeFiyat,15:F2} | " +
                $"{optResult.GetiriFiyat,15:F2} | " +
                $"{optResult.GetiriFiyatYuzde,10:F2} | " +
                $"{optResult.KomisyonFiyat,15:F2} | " +
                $"{optResult.BakiyeFiyatNet,15:F2} | " +
                $"{optResult.GetiriFiyatNet,15:F2} | " +
                $"{optResult.GetiriFiyatYuzdeNet,10:F2} | " +
                $"{optResult.IslemSayisi,10} | " +
                $"{optResult.KazandiranIslemSayisi,10} | " +
                $"{optResult.KaybettirenIslemSayisi,10} | " +
                $"{optResult.ToplamKarFiyat,15:F2} | " +
                $"{optResult.ToplamZararFiyat,15:F2} | " +
                $"{optResult.NetKarFiyat,15:F2} | " +
                $"{optResult.WinRate,10:F2} | " +
                $"{optResult.MaxDrawdown,10:F2} | " +
                $"{optResult.ProfitFactor,10:F2} | " +
                $"{optResult.ProfitFactorNet,10:F2} | " +
                $"{optResult.NetProfit,15:F2} | " +
                $"{optResult.WinRate,10:F2} | " +
                $"{optResult.StrategyName,20} | "
            );

            // OptimizationSummary values
            dataBuilder.Append($"{"",20} | ");
            dataBuilder.Append(
                $"{optSummary.TraderId,10} | " +
                $"{optSummary.TraderName,20} | " +
                $"{optSummary.SymbolName,10} | " +
                $"{optSummary.SymbolPeriod,10} | " +
                $"{optSummary.SystemId,10} | " +
                $"{optSummary.SystemName,20} | " +
                $"{optSummary.StrategyId,10} | " +
                $"{optSummary.StrategyName,20} | " +
                $"{optSummary.ToplamBarSayisi,10} | " +
                $"{optSummary.IlkBarTarihSaati,20} | " +
                $"{optSummary.SonBarTarihSaati,20} | " +
                $"{optSummary.IlkBakiyeFiyat,15:F2} | " +
                $"{optSummary.BakiyeFiyat,15:F2} | " +
                $"{optSummary.GetiriFiyat,15:F2} | " +
                $"{optSummary.GetiriFiyatYuzde,10:F2} | " +
                $"{optSummary.BakiyeFiyatNet,15:F2} | " +
                $"{optSummary.GetiriFiyatNet,15:F2} | " +
                $"{optSummary.GetiriFiyatYuzdeNet,10:F2} | " +
                $"{optSummary.KomisyonFiyat,10:F2} | " +
                $"{optSummary.KomisyonFiyatYuzde,10:F4} | " +
                $"{optSummary.MinBakiyeFiyat,15:F2} | " +
                $"{optSummary.MaxBakiyeFiyat,15:F2} | " +
                $"{optSummary.MinBakiyeFiyatNet,15:F2} | " +
                $"{optSummary.MaxBakiyeFiyatNet,15:F2} | " +
                $"{optSummary.IslemSayisi,10} | " +
                $"{optSummary.AlisSayisi,10} | " +
                $"{optSummary.SatisSayisi,10} | " +
                $"{optSummary.FlatSayisi,10} | " +
                $"{optSummary.PassSayisi,10} | " +
                $"{optSummary.KarAlSayisi,10} | " +
                $"{optSummary.ZararKesSayisi,10} | " +
                $"{optSummary.KazandiranIslemSayisi,10} | " +
                $"{optSummary.KaybettirenIslemSayisi,10} | " +
                $"{optSummary.NotrIslemSayisi,10} | " +
                $"{optSummary.ToplamKarFiyat,15:F2} | " +
                $"{optSummary.ToplamZararFiyat,15:F2} | " +
                $"{optSummary.NetKarFiyat,15:F2} | " +
                $"{optSummary.MaxKarFiyat,15:F2} | " +
                $"{optSummary.MaxZararFiyat,15:F2} | " +
                $"{optSummary.KarliIslemOrani,10:F2} | " +
                $"{optSummary.GetiriMaxDD,10:F2} | " +
                $"{optSummary.GetiriMaxDDTarih,20} | " +
                $"{optSummary.GetiriMaxKayip,10:F2} | " +
                $"{optSummary.ProfitFactor,10:F2} | " +
                $"{optSummary.ProfitFactorNet,10:F2} | " +
                $"{optSummary.ProfitFactorSistem,10:F2}"
            );

            sw.WriteLine(dataBuilder.ToString());
            sw.Flush();
        }
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
        OnSingleTraderProgressCallback?.Invoke(currentBar, totalBars);
    }

    private void OnApplyUserFlags(SingleTrader trader)
    {
        trader.ConfigureUserFlagsOnce();
    }

    private void SetSingleTraderConfigureEquityCurveFilter(SingleTrader trader)
    {
        // TODO: Equity curve filter konfigurasyonu ileride eklenecek
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
