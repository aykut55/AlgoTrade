using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    public Action<int, int>? OnOptimizationProgress { get; set; }         // (currentCombination, totalCombinations)
    public Action<int, int>? OnSingleTraderProgressCallback { get; set; } // (currentBar, totalBars)

    // State flags
    public bool IsStarted { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsStopped { get; private set; }
    public bool IsStopRequested { get; private set; }

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

    public void runSingleTrader(SingleTrader singleTrader, int totalBars)
    {
        for (int i = 0; i < totalBars; i++)
        {
            if (i % 1000 == 0)
                OnSingleTraderProgressCallback?.Invoke(i, totalBars);

            /*
            TODO : Yukarıdaki TODO'ya gore burası acılacak
            if (IsStopRequested) {
                break;
            }
            */

            singleTrader.Run(i);
        }
    }
    
    public OptimizationResult? Run()
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

            // Run singleTrader
            runSingleTrader(singleTrader, totalBars);

            // Collect singleTrader statistics
            singleTrader.Finalize();



            /* TODO: Sonuc toplama ve raporlama
            // Get optimization summary
            var optSummary = singleTrader.statistics.GetOptimizationSummary();

            // Create result from optimization summary
            var optResult = CreateOptimizationResultFromSummary(optSummary, paramCombo);

            Results.Add(optResult);

            Logger?.Log($"  → NetProfit: {optResult.NetProfit:F2}, WinRate: {optResult.WinRate:F2}%, PF: {optResult.ProfitFactor:F2}, PFNet: {optResult.ProfitFactorNet:F2}");

            // Append to CSV and TXT files (if enabled)
            //AppendSingleResultToFiles(result, currentCombination);

            // Append to CSV and TXT files (if enabled)
            AppendSingleOptSummaryToFiles(optResult, optSummary, currentCombination);

            // Report optimization progress
            OnReadOptimizationResultsFile?.Invoke(this, singleTrader, currentCombination);

            // strategy.Dispose();
            strategy = null;

            // Intermediate save check
            if (SaveEveryN > 0 && effectiveCombinationCount % SaveEveryN == 0)
            {
                Logger?.Log($"Saving intermediate results at combination {currentCombination} (effective: {effectiveCombinationCount})...");
                OnSaveResults?.Invoke(Results, currentCombination);
            }

            // Update state flags
            singleTrader.IsRunning = false;
            singleTrader.IsStopped = true;
            */



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
    }

    #endregion
}
