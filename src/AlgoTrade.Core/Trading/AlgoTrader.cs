using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Python;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Timer;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Queries;
using AlgoTrade.Core.Trading.Query;
using AlgoTrade.Core.Trading.Strategies;
using AlgoTrade.Core.Trading.Strategy;
using Nessos.LinqOptimizer.Core;
using ScottPlot.Plottables;
using SkiaSharp;
using System;
using System.Text;
using static Nessos.LinqOptimizer.Core.QueryExpr;

namespace AlgoTrade.Core.Trading;

public enum TraderApplyMode
{
    SingleTrader,
    MultipleTrader,
    SingleTraderOptimizer
}

public class AlgoTrader : MarketDataProvider, IDisposable
{
    #region Properties

    // Identification
    public string Name { get; }
    public bool IsRunning { get; private set; }
    public new bool IsInitialized { get; private set; }

    // Symbol and System Info
    public string SymbolName { get; set; } = "...";
    public string SymbolPeriod { get; set; } = "...";
    public string SystemId { get; set; } = "...";
    public string SystemName { get; set; } = "...";
    public string StrategyId { get; set; } = "...";
    public string StrategyName { get; set; } = "...";
    public string QueryId { get; set; } = "...";
    public string QueryName { get; set; } = "...";
    public bool QueryIsEnabled { get; private set; }
    public TraderRunMode SingleTraderRunMode { get; set; } = TraderRunMode.TradeAndQuery;

    // Bar-bar "\r\tProgress : x/y (%)" konsol yazımı (OnSingleTraderProgress). Varsayılan false —
    // isteyen çalıştırma (örn. Console [2]/[3]/[5]/[6]'daki normal interaktif SingleTrader/
    // MultipleTrader) bunu kendi kurulumunda true'ya çekebilir. Tarama sınıfları (senaryo 4/7/8 —
    // MultiStrategyTimeframeScanner vb.) N tane taze/throwaway AlgoTrader'ı arka arkaya çalıştırdığı
    // için varsayılan kapalı kalması gürültüyü önlüyor.
    public bool ProgressLoggingEnabled { get; set; } = false;

    // Equity Curve Filter
    public bool EquityCurveFilteringEnabled { get; set; } = false;
    public bool ThresholdTypeIsPercent { get; set; } = false;
    public double ProfitConfirmationThreshold { get; set; } = 10.0;
    public double LossConfirmationThreshold { get; set; } = 5.0;
    public ConfirmationTrigger ConfirmationTrigger { get; set; } = ConfirmationTrigger.Both;

    // Internal
    private LogManager? _logger;
    private TimeManager? _timer;
    private SingleTrader? singleTrader { get; set; }
    public SingleTrader? SingleTrader => singleTrader;
    private MultipleTrader? multipleTrader { get; set; }
    public MultipleTrader? MultipleTrader => multipleTrader;
    private SingleTraderOptimizer? singleTraderOptimizer { get; set; }
    public SingleTraderOptimizer? SingleTraderOptimizer => singleTraderOptimizer;
    public IndicatorManager? indicators { get; private set; }
    private IStrategy? strategy;
    private IQuery? query;
    private readonly StrategyRegistry _strategyRegistry = new();
    private readonly QueryRegistry _queryRegistry = new();
    private string? _currentStrategyName;
    private Dictionary<string, object>? _currentStrategyParams;
    private string? _currentQueryName;
    private Dictionary<string, object>? _currentQueryParams;
    public IReadOnlyCollection<string> AvailableStrategies => _strategyRegistry.GetStrategyNames();
    public IReadOnlyCollection<string> AvailableQueries => _queryRegistry.GetQueryNames();

    // Strategy list for MultipleTrader
    private readonly List<StrategyConfigEntry> _strategyConfigs = new();
    public IReadOnlyList<StrategyConfigEntry> StrategyConfigs => _strategyConfigs;

    // Query list for MultipleTrader
    private readonly List<QueryConfigEntry> _queryConfigs = new();
    public IReadOnlyList<QueryConfigEntry> QueryConfigs => _queryConfigs;

    // Optimization config for SingleTraderOptimizer
    private readonly List<OptimizationParameterRangeEntry> _optimizationParameterRanges = new();
    public IReadOnlyList<OptimizationParameterRangeEntry> OptimizationParameterRanges => _optimizationParameterRanges;
    private StrategyFactory? _optimizationStrategyFactory;

    // EquityCurveFilter list for MultipleTrader
    private readonly List<EquityCurveFilterConfigEntry> _equityCurveFilterConfigs = new();
    public IReadOnlyList<EquityCurveFilterConfigEntry> EquityCurveFilterConfigs => _equityCurveFilterConfigs;

    // ChildTrader config list for MultipleTrader (AppConfig ile set edilir)
    private readonly List<ChildTraderConfigEntry> _childTraderConfigs = new();
    public IReadOnlyList<ChildTraderConfigEntry> ChildTraderConfigs => _childTraderConfigs;

    // SingleTrader trade params override (AppConfig ile set edilir, null ise hardcoded parametreler kullanılır)
    private InitialTradeParams?         _singleTraderTradeParamsConfig = null;
    private SingleTraderSignalsConfig?  _singleTraderSignalsConfig     = null;
    private SingleTraderSaveConfig?     _singleTraderSaveConfig        = null;
    private SingleTraderPlotConfig?         _singleTraderPlotConfig         = null;
    private SingleTraderOptimizationConfig?  _singleTraderOptimizationConfig  = null;
    private MultipleTraderObjectSaveConfig?  _multipleTraderSaveConfig        = null;
    private MultipleTraderConsensusConfig?   _multipleTraderConsensusConfig   = null;
    private SingleTraderOptRangeConfig?      _singleTraderOptRangeConfig      = null;
    private SingleTraderOptTradeParamsConfig? _singleTraderOptTradeParamsConfig = null;
    private SingleTraderSignalsConfig?       _singleTraderOptSignalsConfig    = null;
    private SingleTraderOptLogConfig?   _singleTraderOptLogConfig      = null;
    private SingleTraderOptSortOutputConfig?  _singleTraderOptSortConfig     = null;
    private SingleTraderExportConfig?   _singleTraderExportConfig      = null;

    // Python görselleştirme
    private PythonPlotter? _pythonPlotter;

    /// <summary>
    /// Python DLL yolu. Boş bırakılırsa PATH'deki python otomatik tespit edilir.
    /// Python 3.12/3.13 kullanmak için açıkça set edin.
    /// Örn: algoTrader.PythonDll = @"C:\Python312\python312.dll"
    /// </summary>
    public string PythonDll { get; set; } = "";

    #endregion

    public AlgoTrader(string name)
    {
        Name = name;
    }

    private void OnSingleTraderReset(SingleTrader trader, int mode)
    {

    }

    private void OnSingleTraderInit(SingleTrader trader, int mode)
    {

    }

    private void OnSingleTraderRun(SingleTrader trader, int mode)
    {

    }

    private void OnSingleTraderFinal(SingleTrader trader, int mode)
    {

    }

    // Callback function to be assigned to SingleTrader.Callback
    // Runs right after emirleri_uygula(i) for each bar
    private void OnSingleTraderBeforeOrder(SingleTrader trader, int barIndex)
    {
        // Example: you can inspect last signal/direction here
        // Logger?.Log($"CB | Bar={barIndex} Yon={trader.signals.SonYon} EmirStatus={trader.signals.EmirStatus}");
        // No-op by default
    }

    // Notification when a concrete A/S/F sinyali gerçekleştiğinde tetiklenir
    private void OnSingleTraderNotifySignal(SingleTrader trader, string signal, int barIndex)
    {

    }

    // Callback function to be assigned to SingleTrader.Callback
    // Runs right after emirleri_uygula(i) for each bar
    private void OnSingleTraderAfterOrder(SingleTrader trader, int barIndex)
    {

    }

    private void OnSingleTraderProgress(SingleTrader trader, int currentBar, int totalBars, double percentage)
    {
        if (_logger == null) return;
        if (!ProgressLoggingEnabled) return;

        /*
         * Cok yavasladigi icin kapatildi


         * 
         */

        var consoleLogger = LogManager.GetConsoleLogger();

        if (currentBar >= totalBars)
        {
            consoleLogger.Write($"\r\tProgress         : {currentBar}/{totalBars} ({percentage:F1}%)");
            consoleLogger.WriteLine("");
        }
        else
        {
            consoleLogger.Write($"\r\tProgress         : {currentBar}/{totalBars} ({percentage:F1}%)");
        }
    }
    private void OnApplyUserFlags(SingleTrader trader)
    {
        // InitializeUserControlledFlags
        trader.ConfigureUserFlagsOnce();

        int traderId = trader.GetId();
        if (traderId == -1)
        {
            // mainTrader icin (MultiTrader için anlamlı)

            trader.signals.AlEnabled                   = true;
            trader.signals.SatEnabled                  = true;
            trader.signals.FlatOlEnabled               = true;
            trader.signals.PasGecEnabled               = true;
            trader.signals.KarAlEnabled                = true;
            trader.signals.ZararKesEnabled             = true;
            trader.signals.GunSonuPozKapatEnabled      = false;     // DEFAULT = False, Ek maliyet getirir : BackTest icin anlamli 
            trader.signals.TimeFilteringEnabled        = false;     // DEFAULT = False, Ek maliyet getirir :
            trader.signals.TradeStartBarIndexEnabled   = false;
            trader.signals.TradeStartBarIndex          = 0;
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
        else if(traderId == 0)
        {
            // 0 id'li trader icin (SingleTrader için bu default'dur)

            trader.signals.AlEnabled                   = true;
            trader.signals.SatEnabled                  = true;
            trader.signals.FlatOlEnabled               = true;
            trader.signals.PasGecEnabled               = true;
            trader.signals.KarAlEnabled                = true;
            trader.signals.ZararKesEnabled             = true;
            trader.signals.GunSonuPozKapatEnabled      = false;     // DEFAULT = False, Ek maliyet getirir : BackTest icin anlamli 
            trader.signals.TimeFilteringEnabled        = false;     // DEFAULT = False, Ek maliyet getirir :
            trader.signals.TradeStartBarIndexEnabled   = false;
            trader.signals.TradeStartBarIndex          = 0;
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
        else if (traderId == 1)
        {
            // 1 id'li trader icin

            trader.signals.AlEnabled                   = true;
            trader.signals.SatEnabled                  = true;
            trader.signals.FlatOlEnabled               = true;
            trader.signals.PasGecEnabled               = true;
            trader.signals.KarAlEnabled                = true;
            trader.signals.ZararKesEnabled             = true;
            trader.signals.GunSonuPozKapatEnabled      = false;     // DEFAULT = False, Ek maliyet getirir : BackTest icin anlamli 
            trader.signals.TimeFilteringEnabled        = false;     // DEFAULT = False, Ek maliyet getirir :
            trader.signals.TradeStartBarIndexEnabled   = false;
            trader.signals.TradeStartBarIndex          = 0;
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
        else if (traderId == 2)
        {
            // 2 id'li trader icin

            trader.signals.AlEnabled                   = true;
            trader.signals.SatEnabled                  = true;
            trader.signals.FlatOlEnabled               = true;
            trader.signals.PasGecEnabled               = true;
            trader.signals.KarAlEnabled                = true;
            trader.signals.ZararKesEnabled             = true;
            trader.signals.GunSonuPozKapatEnabled      = false;     // DEFAULT = False, Ek maliyet getirir : BackTest icin anlamli 
            trader.signals.TimeFilteringEnabled        = false;     // DEFAULT = False, Ek maliyet getirir :
            trader.signals.TradeStartBarIndexEnabled   = false;
            trader.signals.TradeStartBarIndex          = 0;
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
        else
        {
        }
    }

    private void OnApplyUserFlags2(SingleTrader trader, TraderApplyMode mode)
    {
        int traderId = trader.GetId();
        if (traderId == -1)
        {
            // mainTrader icin (MultiTrader için anlamlı)

            // Configure optimization flag
            trader.OptimizationEnabled = false;

            // Enable savingStatistics
            trader.SaveStatisticsToFile = true;

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
            trader.FullStatsTxtFileName                = "MainTraderStatistics.txt";
            trader.FullStatsCsvFileName                = "MainTraderStatistics.csv";
            trader.MinimalStatsTxtFileName             = "MainTraderStatisticsMinimal.txt";
            trader.MinimalStatsCsvFileName             = "MainTraderStatisticsMinimal.csv";
            trader.FullListsTxtFileName                = "MainTraderLists.txt";
            trader.FullListsCsvFileName                = "MainTraderLists.csv";
            trader.MinimalListsTxtFileName             = "MainTraderListsMinimal.txt";
            trader.MinimalListsCsvFileName             = "MainTraderListsMinimal.csv";
            trader.FullStatsTxtFormattedFileName       = "MainTraderStatisticsFormatted.txt";
            trader.MinimalStatsTxtFormattedFileName    = "MainTraderStatisticsMinimalFormatted.txt";
            trader.PerformansTxtFileName               = "MainTraderPerformans.txt";
            trader.PerformansCsvFileName               = "MainTraderPerformans.csv";
        }
        else if (traderId == 0)
        {
            // Configure optimization flag
            trader.OptimizationEnabled = false;

            // Enable savingStatistics
            trader.SaveStatisticsToFile = true;

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

            if (mode == TraderApplyMode.SingleTrader)
            {
                trader.FullStatsTxtFileName                = $"SingleTraderStatistics.txt";
                trader.FullStatsCsvFileName                = $"SingleTraderStatistics.csv";
                trader.MinimalStatsTxtFileName             = $"SingleTraderStatisticsMinimal.txt";
                trader.MinimalStatsCsvFileName             = $"SingleTraderStatisticsMinimal.csv";
                trader.FullListsTxtFileName                = $"SingleTraderLists.txt";
                trader.FullListsCsvFileName                = $"SingleTraderLists.csv";
                trader.MinimalListsTxtFileName             = $"SingleTraderListsMinimal.txt";
                trader.MinimalListsCsvFileName             = $"SingleTraderListsMinimal.csv";
                trader.FullStatsTxtFormattedFileName       = $"SingleTraderStatisticsFormatted.txt";
                trader.MinimalStatsTxtFormattedFileName    = $"SingleTraderStatisticsMinimalFormatted.txt";
                trader.PerformansTxtFileName               = $"SingleTraderPerformans.txt";
                trader.PerformansCsvFileName               = $"SingleTraderPerformans.csv";
            }
            else if (mode == TraderApplyMode.MultipleTrader)
            {
                // Unique output file names per trader
                string t = $"T{traderId}";
                trader.FullStatsTxtFileName                = $"{t}_SingleTraderStatistics.txt";
                trader.FullStatsCsvFileName                = $"{t}_SingleTraderStatistics.csv";
                trader.MinimalStatsTxtFileName             = $"{t}_SingleTraderStatisticsMinimal.txt";
                trader.MinimalStatsCsvFileName             = $"{t}_SingleTraderStatisticsMinimal.csv";
                trader.FullListsTxtFileName                = $"{t}_SingleTraderLists.txt";
                trader.FullListsCsvFileName                = $"{t}_SingleTraderLists.csv";
                trader.MinimalListsTxtFileName             = $"{t}_SingleTraderListsMinimal.txt";
                trader.MinimalListsCsvFileName             = $"{t}_SingleTraderListsMinimal.csv";
                trader.FullStatsTxtFormattedFileName       = $"{t}_SingleTraderStatisticsFormatted.txt";
                trader.MinimalStatsTxtFormattedFileName    = $"{t}_SingleTraderStatisticsMinimalFormatted.txt";
                trader.PerformansTxtFileName               = $"{t}_SingleTraderPerformans.txt";
                trader.PerformansCsvFileName               = $"{t}_SingleTraderPerformans.csv";
            }
        }
        else if (traderId == 1)
        {
            // Configure optimization flag
            trader.OptimizationEnabled = false;

            // Enable savingStatistics
            trader.SaveStatisticsToFile = true;

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

            // Unique output file names per trader
            string t = $"T{traderId}";
            trader.FullStatsTxtFileName                = $"{t}_SingleTraderStatistics.txt";
            trader.FullStatsCsvFileName                = $"{t}_SingleTraderStatistics.csv";
            trader.MinimalStatsTxtFileName             = $"{t}_SingleTraderStatisticsMinimal.txt";
            trader.MinimalStatsCsvFileName             = $"{t}_SingleTraderStatisticsMinimal.csv";
            trader.FullListsTxtFileName                = $"{t}_SingleTraderLists.txt";
            trader.FullListsCsvFileName                = $"{t}_SingleTraderLists.csv";
            trader.MinimalListsTxtFileName             = $"{t}_SingleTraderListsMinimal.txt";
            trader.MinimalListsCsvFileName             = $"{t}_SingleTraderListsMinimal.csv";
            trader.FullStatsTxtFormattedFileName       = $"{t}_SingleTraderStatisticsFormatted.txt";
            trader.MinimalStatsTxtFormattedFileName    = $"{t}_SingleTraderStatisticsMinimalFormatted.txt";
            trader.PerformansTxtFileName               = $"{t}_SingleTraderPerformans.txt";
            trader.PerformansCsvFileName               = $"{t}_SingleTraderPerformans.csv";
        }
        else if (traderId == 2)
        {
            // Configure optimization flag
            trader.OptimizationEnabled = false;

            // Enable savingStatistics
            trader.SaveStatisticsToFile = true;

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

            // Unique output file names per trader
            string t = $"T{traderId}";
            trader.FullStatsTxtFileName                = $"{t}_SingleTraderStatistics.txt";
            trader.FullStatsCsvFileName                = $"{t}_SingleTraderStatistics.csv";
            trader.MinimalStatsTxtFileName             = $"{t}_SingleTraderStatisticsMinimal.txt";
            trader.MinimalStatsCsvFileName             = $"{t}_SingleTraderStatisticsMinimal.csv";
            trader.FullListsTxtFileName                = $"{t}_SingleTraderLists.txt";
            trader.FullListsCsvFileName                = $"{t}_SingleTraderLists.csv";
            trader.MinimalListsTxtFileName             = $"{t}_SingleTraderListsMinimal.txt";
            trader.MinimalListsCsvFileName             = $"{t}_SingleTraderListsMinimal.csv";
            trader.FullStatsTxtFormattedFileName       = $"{t}_SingleTraderStatisticsFormatted.txt";
            trader.MinimalStatsTxtFormattedFileName    = $"{t}_SingleTraderStatisticsMinimalFormatted.txt";
            trader.PerformansTxtFileName               = $"{t}_SingleTraderPerformans.txt";
            trader.PerformansCsvFileName               = $"{t}_SingleTraderPerformans.csv";
        }
        else
        {
        }
    }
    public void SetSingleTraderConfigureEquityCurveFilter(SingleTrader trader, int id = 0)
    {
        var config = _equityCurveFilterConfigs.FirstOrDefault(c => c.Id == id);
        if (config is null)
        {
            // Listede yoksa AlgoTrader property'lerinden oku (backward compat / fallback)
            trader.signals.EquityCurveFilteringEnabled = this.EquityCurveFilteringEnabled;
            trader.ConfigureEquityCurveFilter(
                isPercent: this.ThresholdTypeIsPercent,
                profitThreshold: this.ProfitConfirmationThreshold,
                lossThreshold: this.LossConfirmationThreshold,
                trigger: this.ConfirmationTrigger
            );
            return;
        }

        trader.signals.EquityCurveFilteringEnabled = config.Enabled;
        trader.ConfigureEquityCurveFilter(
            isPercent: config.ThresholdTypeIsPercent,
            profitThreshold: config.ProfitConfirmationThreshold,
            lossThreshold: config.LossConfirmationThreshold,
            trigger: config.ConfirmationTrigger
        );
    }

    public IStrategy GetStrategy(int id)
    {
        var config = _strategyConfigs.FirstOrDefault(s => s.Id == id);
        if (config == null)
            throw new InvalidOperationException($"Strategy config not found for Id: {id}");

        var s = _strategyRegistry.CreateStrategy(this.Data, indicators, _logger, config.StrategyName, config.Parameters);
        if (s == null)
            throw new InvalidOperationException($"Strategy '{config.StrategyName}' (Id: {id}) can not be created...");

        return s;
    }

    public IQuery GetQuery(int id)
    {
        var config = _queryConfigs.FirstOrDefault(q => q.Id == id);
        if (config == null)
            throw new InvalidOperationException($"Query config not found for Id: {id}");

        var q = _queryRegistry.CreateQuery(this.Data, indicators, _logger, config.QueryName, config.Parameters);
        if (q == null)
            throw new InvalidOperationException($"Query '{config.QueryName}' (Id: {id}) can not be created...");

        return q;
    }

    public void Start()
    {
        IsRunning = true;
        OnMessage($"AlgoTrader '{Name}' başlatıldı.");
    }

    public void Stop()
    {
        IsRunning = false;
        OnMessage($"AlgoTrader '{Name}' durduruldu.");
    }

    public int DeleteAllFilesInDirectory(string directoryPath, bool includeSubdirectories = false)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Directory path cannot be null or empty.", nameof(directoryPath));

        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directoryPath, "*", searchOption);

        int deletedCount = 0;
        foreach (var file in files)
        {
            File.Delete(file);
            deletedCount++;
        }

        return deletedCount;
    }

    public void SetData(List<StockData> data)
    {
        _data = data;
    }

    public void RegisterLogger(LogManager logger)
    {
        _logger = logger;
    }

    public void RegisterTimer(TimeManager timer)
    {
        _timer = timer;
    }

    public void ConfigureStrategy(string strategyName, Dictionary<string, object> parameters)
    {
        if (string.IsNullOrWhiteSpace(strategyName))
            throw new ArgumentException("Strategy name cannot be null or empty.", nameof(strategyName));

        _currentStrategyName = strategyName.Trim();
        _currentStrategyParams = new Dictionary<string, object>(parameters ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase);
        if (!_currentStrategyParams.ContainsKey("choice"))
        {
            _currentStrategyParams["choice"] = 0;
        }
        StrategyName = _currentStrategyName;
    }

    public void ConfigureStrategyFromConfig(string configFilePath, string strategyName, string? version = null)
    {
        if (string.IsNullOrWhiteSpace(configFilePath))
            throw new ArgumentException("Config file path cannot be null or empty.", nameof(configFilePath));

        if (!File.Exists(configFilePath))
            throw new FileNotFoundException($"Strategy config file not found: {configFilePath}");

        var loader = new StrategyConfigLoader(configFilePath);
        loader.LoadFromFile();

        StrategyConfiguration? config = version is null
            ? loader.GetFirstConfigurationForStrategy(strategyName)
            : loader.GetConfiguration(strategyName, version);

        if (config is null)
            throw new InvalidOperationException($"Strategy configuration not found: strategy='{strategyName}', version='{version ?? "first"}'.");

        ConfigureStrategy(config.StrategyName, config.GetParameterValues());
    }

    public void ConfigureQuery(string queryName, Dictionary<string, object> parameters)
    {
        if (string.IsNullOrWhiteSpace(queryName))
            throw new ArgumentException("Query name cannot be null or empty.", nameof(queryName));

        _currentQueryName = queryName.Trim();
        _currentQueryParams = new Dictionary<string, object>(parameters ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase);
        QueryName = _currentQueryName;
        QueryIsEnabled = true;
    }

    public void SetQueryEnabled(bool enabled)
    {
        QueryIsEnabled = enabled;
    }

    public void ConfigureQueryFromConfig(string configFilePath, string queryName, string? version = null)
    {
        if (string.IsNullOrWhiteSpace(configFilePath))
            throw new ArgumentException("Config file path cannot be null or empty.", nameof(configFilePath));

        if (!File.Exists(configFilePath))
            throw new FileNotFoundException($"Query config file not found: {configFilePath}");

        var loader = new QueryConfigLoader(configFilePath);
        loader.LoadFromFile();

        QueryConfiguration? config = version is null
            ? loader.GetFirstConfigurationForQuery(queryName)
            : loader.GetConfiguration(queryName, version);

        if (config is null)
            throw new InvalidOperationException($"Query configuration not found: query='{queryName}', version='{version ?? "first"}'.");

        ConfigureQuery(config.QueryName, config.GetParameterValues());
    }

    public void ConfigureEquityCurveFilterFromConfig(string configFilePath, string version, int id = 0)
    {
        if (string.IsNullOrWhiteSpace(configFilePath))
            throw new ArgumentException("Config file path cannot be null or empty.", nameof(configFilePath));

        if (!File.Exists(configFilePath))
            throw new FileNotFoundException($"EquityCurveFilter config file not found: {configFilePath}");

        var loader = new EquityCurve.EquityCurveFilterConfigLoader(configFilePath);
        loader.LoadFromFile();

        var config = loader.GetConfiguration(version);

        if (config is null)
            throw new InvalidOperationException($"EquityCurveFilter configuration not found: version='{version}'.");

        AddEquityCurveFilterConfig(id, config.Enabled, config.ThresholdTypeIsPercent,
            config.ProfitThreshold, config.LossThreshold, config.Trigger);
    }

    // ==========================================================================
    // Public Factory Methods (for .csx scripts - inlined execution)
    // ==========================================================================

    public IndicatorManager CreateIndicators()
    {
        if (indicators != null)
        {
            indicators.Dispose();
            indicators = null;
        }

        indicators = new IndicatorManager(this.Data);
        if (indicators == null)
            throw new InvalidOperationException("indicators can not be created...");

        return indicators;
    }

    public IStrategy CreateConfiguredStrategy(IndicatorManager indicators)
    {
        if (string.IsNullOrWhiteSpace(_currentStrategyName))
            throw new InvalidOperationException("Strategy not configured. Call ConfigureStrategy(...) first.");

        var s = _strategyRegistry.CreateStrategy(this.Data, indicators, _logger, _currentStrategyName, _currentStrategyParams);
        if (s == null)
            throw new InvalidOperationException($"Strategy '{_currentStrategyName}' can not be created...");

        return s;
    }

    public IQuery? CreateConfiguredQuery(IndicatorManager indicators)
    {
        if (!QueryIsEnabled)
            return null;

        if (string.IsNullOrWhiteSpace(_currentQueryName))
            throw new InvalidOperationException("QueryIsEnabled is true but query name is not configured. Call ConfigureQuery(...) first.");

        var q = _queryRegistry.CreateQuery(this.Data, indicators, _logger, _currentQueryName, _currentQueryParams);
        if (q == null)
            throw new InvalidOperationException($"Query '{_currentQueryName}' can not be created...");

        return q;
    }

    // ==========================================================================
    // Multiple Strategy/Query/EquityCurveFilter Configuration (for MultipleTrader)
    // ==========================================================================

    public void AddStrategyConfig(int id, string strategyName, Dictionary<string, object> parameters)
    {
        _strategyConfigs.Add(new StrategyConfigEntry(id, strategyName, new Dictionary<string, object>(parameters, StringComparer.OrdinalIgnoreCase)));
    }

    public void ClearStrategyConfigs()
    {
        _strategyConfigs.Clear();
    }

    public void ConfigureStrategiesFromConfig(string configFilePath, List<(string name, string version)> selections)
    {
        if (string.IsNullOrWhiteSpace(configFilePath))
            throw new ArgumentException("Config file path cannot be null or empty.", nameof(configFilePath));

        if (!File.Exists(configFilePath))
            throw new FileNotFoundException($"Strategy config file not found: {configFilePath}");

        var loader = new StrategyConfigLoader(configFilePath);
        loader.LoadFromFile();

        for (int i = 0; i < selections.Count; i++)
        {
            var (name, version) = selections[i];
            var config = loader.GetConfiguration(name, version);
            if (config is null)
                throw new InvalidOperationException($"Strategy configuration not found: strategy='{name}', version='{version}'.");

            AddStrategyConfig(i, config.StrategyName, config.GetParameterValues());
        }
    }

    public void AddQueryConfig(int id, string queryName, Dictionary<string, object> parameters)
    {
        _queryConfigs.Add(new QueryConfigEntry(id, queryName, new Dictionary<string, object>(parameters, StringComparer.OrdinalIgnoreCase)));
    }

    public void ClearQueryConfigs()
    {
        _queryConfigs.Clear();
    }

    public void ConfigureQueriesFromConfig(string configFilePath, List<(string name, string version)> selections)
    {
        if (string.IsNullOrWhiteSpace(configFilePath))
            throw new ArgumentException("Config file path cannot be null or empty.", nameof(configFilePath));

        if (!File.Exists(configFilePath))
            throw new FileNotFoundException($"Query config file not found: {configFilePath}");

        var loader = new QueryConfigLoader(configFilePath);
        loader.LoadFromFile();

        for (int i = 0; i < selections.Count; i++)
        {
            var (name, version) = selections[i];
            var config = loader.GetConfiguration(name, version);
            if (config is null)
                throw new InvalidOperationException($"Query configuration not found: query='{name}', version='{version}'.");

            AddQueryConfig(i, config.QueryName, config.GetParameterValues());
        }
    }

    public void AddEquityCurveFilterConfig(int id, bool enabled, bool thresholdTypeIsPercent, double profitThreshold, double lossThreshold, ConfirmationTrigger trigger)
    {
        _equityCurveFilterConfigs.Add(new EquityCurveFilterConfigEntry(id, enabled, thresholdTypeIsPercent, profitThreshold, lossThreshold, trigger));
    }

    public void ClearEquityCurveFilterConfigs()
    {
        _equityCurveFilterConfigs.Clear();
    }

    // ==========================================================================
    // ChildTrader Configuration (for MultipleTrader)
    // ==========================================================================

    public void AddChildTraderConfig(ChildTraderConfigEntry entry)
    {
        _childTraderConfigs.Add(entry);
    }

    public void ClearChildTraderConfigs()
    {
        _childTraderConfigs.Clear();
    }

    /// <summary>
    /// N adet ChildTraderConfigEntry oluşturur.
    /// _strategyConfigs.Count == count ise 1-to-1 eşleşme; aksi hâlde hepsi _strategyConfigs[0].Id kullanır.
    /// </summary>
    public void SetChildTraderCount(int count, Action<ChildTraderConfigEntry, int>? configure = null)
    {
        _childTraderConfigs.Clear();
        for (int i = 0; i < count; i++)
        {
            int strategyId = (_strategyConfigs.Count == count)
                ? _strategyConfigs[i].Id
                : (_strategyConfigs.Count > 0 ? _strategyConfigs[0].Id : 0);
            var entry = new ChildTraderConfigEntry(childId: i, strategyId: strategyId);
            configure?.Invoke(entry, i);
            _childTraderConfigs.Add(entry);
        }
    }

    // ==========================================================================
    // SingleTrader Trade Params Override (AppConfig ile set edilir)
    // ==========================================================================

    /// <summary>
    /// SingleTrader için trade params override'ı set eder.
    /// RunSingleTraderWithProgressAsync() içindeki hardcoded parametreleri override eder.
    /// </summary>
    public void SetSingleTraderTradeParams(InitialTradeParams config)
    {
        _singleTraderTradeParamsConfig = config;
    }

    // ==========================================================================
    // SingleTrader Signals & Save Config (AppConfig ile set edilir)
    // ==========================================================================

    public void SetSingleTraderSignalsConfig(SingleTraderSignalsConfig config)
    {
        _singleTraderSignalsConfig = config;
    }

    public void SetSingleTraderSaveConfig(SingleTraderSaveConfig config)
    {
        _singleTraderSaveConfig = config;
    }

    public void SetMultipleTraderSaveConfig(MultipleTraderObjectSaveConfig config)
    {
        _multipleTraderSaveConfig = config;
    }

    public void SetMultipleTraderConsensusConfig(MultipleTraderConsensusConfig config)
    {
        _multipleTraderConsensusConfig = config;
    }

    public void SetSingleTraderPlotConfig(SingleTraderPlotConfig config)
    {
        _singleTraderPlotConfig = config;
    }

    public void SetSingleTraderExportConfig(SingleTraderExportConfig config)
    {
        _singleTraderExportConfig = config;
    }

    public void SetSingleTraderOptimizationConfig(SingleTraderOptimizationConfig config)
    {
        _singleTraderOptimizationConfig = config;
    }

    public void SetSingleTraderOptSignalsConfig(SingleTraderSignalsConfig config)
    {
        _singleTraderOptSignalsConfig = config;
    }

    public void SetSingleTraderOptLogConfig(SingleTraderOptLogConfig config)
    {
        _singleTraderOptLogConfig = config;
    }

    public void SetSingleTraderOptSortOutputConfig(SingleTraderOptSortOutputConfig config)
    {
        _singleTraderOptSortConfig = config;
    }

    /// <summary>
    /// _singleTraderSignalsConfig ve _singleTraderSaveConfig'i trader'a uygular.
    /// OnApplyUserFlags / OnApplyUserFlags2 (SingleTrader) yerine çağrılır.
    /// </summary>
    private void ApplySingleTraderFlagsConfigs(SingleTrader trader)
    {
        trader.ConfigureUserFlagsOnce();

        if (_singleTraderSignalsConfig is { } s)
        {
            trader.signals.AlEnabled              = s.AlEnabled;
            trader.signals.SatEnabled             = s.SatEnabled;
            trader.signals.FlatOlEnabled          = s.FlatOlEnabled;
            trader.signals.PasGecEnabled          = s.PasGecEnabled;
            trader.signals.KarAlEnabled           = s.KarAlEnabled;
            trader.signals.ZararKesEnabled        = s.ZararKesEnabled;
            trader.signals.GunSonuPozKapatEnabled = s.GunSonuPozKapatEnabled;
            trader.signals.TimeFilteringEnabled   = s.TimeFilteringEnabled;
            trader.signals.TradeStartBarIndexEnabled = s.TradeStartBarIndexEnabled;
            trader.signals.TradeStartBarIndex        = s.TradeStartBarIndex;
            trader.signals.EquityCurveFilteringEnabled = false; // asıl değer ECF config'den gelir

            trader.StartDateTimeStr = s.StartDateTime;
            trader.StopDateTimeStr  = s.StopDateTime;

            var startDt         = System.DateTime.ParseExact(s.StartDateTime, "yyyy.MM.dd HH:mm:ss", null);
            trader.StartDateStr = startDt.ToString("yyyy.MM.dd");
            trader.StartTimeStr = startDt.ToString("HH:mm:ss");

            var stopDt         = System.DateTime.ParseExact(s.StopDateTime, "yyyy.MM.dd HH:mm:ss", null);
            trader.StopDateStr = stopDt.ToString("yyyy.MM.dd");
            trader.StopTimeStr = stopDt.ToString("HH:mm:ss");
        }

        if (_singleTraderOptimizationConfig is { } oc)
            trader.OptimizationEnabled = oc.OptimizationEnabled;

        if (_singleTraderSaveConfig is { } sv)
        {
            trader.SaveStatisticsToFile               = sv.SaveStatisticsToFile;
            trader.SaveFullStatsTxtEnabled            = sv.SaveFullStatsTxtEnabled;
            trader.SaveFullStatsCsvEnabled            = sv.SaveFullStatsCsvEnabled;
            trader.SaveMinimalStatsTxtEnabled         = sv.SaveMinimalStatsTxtEnabled;
            trader.SaveMinimalStatsCsvEnabled         = sv.SaveMinimalStatsCsvEnabled;
            trader.SaveFullListsTxtEnabled            = sv.SaveFullListsTxtEnabled;
            trader.SaveFullListsCsvEnabled            = sv.SaveFullListsCsvEnabled;
            trader.SaveMinimalListsTxtEnabled         = sv.SaveMinimalListsTxtEnabled;
            trader.SaveMinimalListsCsvEnabled         = sv.SaveMinimalListsCsvEnabled;
            trader.SaveFullStatsTxtFormattedEnabled   = sv.SaveFullStatsTxtFormattedEnabled;
            trader.SaveMinimalStatsTxtFormattedEnabled = sv.SaveMinimalStatsTxtFormattedEnabled;
            trader.SavePerformansTxtEnabled           = sv.SavePerformansTxtEnabled;
            trader.SavePerformansCsvEnabled           = sv.SavePerformansCsvEnabled;

            // Dosya adları: AppConfig'den gelir, boşsa varsayılan değer korunur
            if (!string.IsNullOrWhiteSpace(sv.FullStatsTxtFileName))             trader.FullStatsTxtFileName             = sv.FullStatsTxtFileName;
            if (!string.IsNullOrWhiteSpace(sv.FullStatsCsvFileName))             trader.FullStatsCsvFileName             = sv.FullStatsCsvFileName;
            if (!string.IsNullOrWhiteSpace(sv.MinimalStatsTxtFileName))          trader.MinimalStatsTxtFileName          = sv.MinimalStatsTxtFileName;
            if (!string.IsNullOrWhiteSpace(sv.MinimalStatsCsvFileName))          trader.MinimalStatsCsvFileName          = sv.MinimalStatsCsvFileName;
            if (!string.IsNullOrWhiteSpace(sv.FullListsTxtFileName))             trader.FullListsTxtFileName             = sv.FullListsTxtFileName;
            if (!string.IsNullOrWhiteSpace(sv.FullListsCsvFileName))             trader.FullListsCsvFileName             = sv.FullListsCsvFileName;
            if (!string.IsNullOrWhiteSpace(sv.MinimalListsTxtFileName))          trader.MinimalListsTxtFileName          = sv.MinimalListsTxtFileName;
            if (!string.IsNullOrWhiteSpace(sv.MinimalListsCsvFileName))          trader.MinimalListsCsvFileName          = sv.MinimalListsCsvFileName;
            if (!string.IsNullOrWhiteSpace(sv.FullStatsTxtFormattedFileName))    trader.FullStatsTxtFormattedFileName    = sv.FullStatsTxtFormattedFileName;
            if (!string.IsNullOrWhiteSpace(sv.MinimalStatsTxtFormattedFileName)) trader.MinimalStatsTxtFormattedFileName = sv.MinimalStatsTxtFormattedFileName;
            if (!string.IsNullOrWhiteSpace(sv.PerformansTxtFileName))            trader.PerformansTxtFileName            = sv.PerformansTxtFileName;
            if (!string.IsNullOrWhiteSpace(sv.PerformansCsvFileName))            trader.PerformansCsvFileName            = sv.PerformansCsvFileName;
        }

        if (_singleTraderPlotConfig is { } pl)
        {
            trader.PlotEnabled = pl.PlotEnabled;
        }

        if (_singleTraderExportConfig is { } ex)
        {
            trader.ExportEnabled    = ex.ExportEnabled;
            trader.ExportConfigFile = ex.ExportConfigFile;
            trader.ExportVersion    = ex.ExportVersion;
        }
    }

    // ==========================================================================
    // Optimization Configuration (for SingleTraderOptimizer)
    // ==========================================================================

    public void AddOptimizationParameterRange(string name, double min, double max, double step)
    {
        _optimizationParameterRanges.Add(new OptimizationParameterRangeEntry(name, min, max, step));
    }

    public void ClearOptimizationParameterRanges()
    {
        _optimizationParameterRanges.Clear();
    }

    public void SetOptimizationStrategyFactory(StrategyFactory factory)
    {
        _optimizationStrategyFactory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public void SetSingleTraderOptRangeConfig(SingleTraderOptRangeConfig config)
    {
        _singleTraderOptRangeConfig = config;
    }

    public void SetSingleTraderOptTradeParamsConfig(SingleTraderOptTradeParamsConfig config)
    {
        _singleTraderOptTradeParamsConfig = config;
    }

    public IStrategy CreateStrategyFromRegistry(List<StockData> data, IndicatorManager ind, string strategyName, Dictionary<string, object> parameters)
    {
        return _strategyRegistry.CreateStrategy(data, ind, _logger, strategyName, parameters);
    }

    public void ConfigureOptimizationFromConfig(string configFilePath, string strategyName, string? version = null)
    {
        if (string.IsNullOrWhiteSpace(configFilePath))
            throw new ArgumentException("Config file path cannot be null or empty.", nameof(configFilePath));

        if (!File.Exists(configFilePath))
            throw new FileNotFoundException($"Optimization config file not found: {configFilePath}");

        var loader = new Strategies.OptimizationConfigLoader(configFilePath);
        loader.LoadFromFile();

        var config = version is null
            ? loader.GetFirstConfigurationForStrategy(strategyName)
            : loader.GetConfiguration(strategyName, version);

        if (config is null)
            throw new InvalidOperationException($"Optimization configuration not found: strategy='{strategyName}', version='{version ?? "first"}'.");

        // Parameter ranges
        ClearOptimizationParameterRanges();
        foreach (var range in config.ParameterRanges)
        {
            AddOptimizationParameterRange(range.Name, range.Min, range.Max, range.Step);
        }

        // Strategy factory: range params + fixed params -> strateji
        var fixedParams = config.GetFixedParameterValues();
        var factoryStrategyName = config.StrategyName;

        SetOptimizationStrategyFactory((data, ind, parameters) =>
        {
            // Merge: range params (optimizer'dan) + fixed params (config'den)
            var merged = new Dictionary<string, object>(fixedParams, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in parameters)
            {
                merged[kvp.Key] = kvp.Value;
            }
            return _strategyRegistry.CreateStrategy(data, ind, _logger, factoryStrategyName, merged);
        });
    }

    public void Reset()
    {
        _data = new();
        IsInitialized = false;
        IsRunning = false;

        SymbolName = "...";
        SymbolPeriod = "...";
        SystemId = "...";
        SystemName = "...";
        StrategyId = "...";
        StrategyName = "...";
        QueryId = "...";
        QueryName = "...";
        QueryIsEnabled = false;

        strategy?.Dispose();
        strategy = null;
        _currentStrategyName = null;
        _currentStrategyParams = null;

        query?.Dispose();
        query = null;
        _currentQueryName = null;
        _currentQueryParams = null;
    }

    public void Initialize()
    {
        if (_data == null || _data.Count == 0)
            throw new ArgumentException("Data cannot be null or empty");
        IsInitialized = true;
    }

    public async Task RunSingleTraderWithProgressAsync(CancellationToken cancellationToken = default)
    {
        int totalBars = 0;

        if (!IsInitialized) {
            throw new InvalidOperationException("AlgoTrader not initialized. Call Initialize() first.");
        }

        try
        {
            _timer!.RestartTimer("0");

            totalBars = GetDataCount();

            Log($"AlgoTrader '{Name}' started. Total bars: {totalBars}");

            // *****************************************************************************
            // Indicators - beg
            // *****************************************************************************
            if (indicators != null) {
                Log("Disposing previous indicators instance...");
                indicators.Dispose();
                indicators = null;
            }

            Log("\nCreating indicators...");

            indicators = new IndicatorManager(this.Data);
            if (indicators == null)
                throw new InvalidOperationException("indicators can not be created...");

            // *****************************************************************************
            // StrategyRegistry - beg
            // *****************************************************************************
            if (strategy != null)
            {
                Log("Disposing previous strategy instance...");
                strategy.Dispose();
                strategy = null;
            }

            Log("\nCreating strategy...");

            strategy = _strategyRegistry.CreateStrategy(this.Data, indicators, _logger, _currentStrategyName, _currentStrategyParams);
            if (strategy == null)
                throw new InvalidOperationException("strategy can not be created...");

            // *****************************************************************************
            // QueryRegistry - beg
            // *****************************************************************************
            if (query != null)
            {
                Log("Disposing previous query instance...");
                query.Dispose();
                query = null;
            }

            if (QueryIsEnabled)
            {
                if (string.IsNullOrWhiteSpace(_currentQueryName))
                    throw new InvalidOperationException("QueryIsEnabled is true but query name is not configured. Call ConfigureQuery(...) first.");

                Log("\nCreating query...");

                query = _queryRegistry.CreateQuery(this.Data, indicators, _logger, _currentQueryName, _currentQueryParams);
                if (query == null)
                    throw new InvalidOperationException("query can not be created...");
            }

            // *****************************************************************************
            // SingleTrader - beg
            // *****************************************************************************
            if (singleTrader != null) {
                Log("Disposing previous singleTrader instance...");
                singleTrader.Dispose();
                singleTrader = null;
            }

            Log("\nCreating singleTrader...");

            singleTrader = new SingleTrader(0, "singleTrader", this.Data, indicators, _logger);
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
            
            // Configure position sizing — AppConfig.SingleTrader.TradeParams
            if (_singleTraderTradeParamsConfig != null)
                singleTrader.initialTradeParams!.ApplyFrom(_singleTraderTradeParamsConfig);

            // Sıralama Onemli
            // Apply user flags — AppConfig'den okunur (SetSingleTraderSignalsConfig / SetSingleTraderSaveConfig)
            // OnApplyUserFlags(singleTrader);          // → AppConfig.SingleTrader.Signals ile değiştirildi
            // OnApplyUserFlags2(singleTrader, TraderApplyMode.SingleTrader); // → AppConfig.SingleTrader.Save ile değiştirildi
            ApplySingleTraderFlagsConfigs(singleTrader);

            // Configure equity curve filter
            SetSingleTraderConfigureEquityCurveFilter(singleTrader);

            // Assign runMode
            singleTrader.RunMode = SingleTraderRunMode;
            if (singleTrader.RunMode == TraderRunMode.TradeOnly)
            {
                // Assign strategy            
                singleTrader.SetStrategy(strategy);
                Log($"\nStrategy configured: {_currentStrategyName}");
            }
            else if (singleTrader.RunMode == TraderRunMode.TradeAndQuery)
            {
                // Assign strategy            
                singleTrader.SetStrategy(strategy);
                Log($"\nStrategy configured: {_currentStrategyName}");

                // Assign query    
                if (query is not null)
                {
                    singleTrader.SetQuery(query);
                    Log($"\nQuery configured: {_currentQueryName}");
                }
            }
            else if (singleTrader.RunMode == TraderRunMode.QueryOnly)
            {
                // Assign query    
                if (query is not null)
                {
                    singleTrader.SetQuery(query);
                    Log($"\nQuery configured: {_currentQueryName}");
                }
            }

            // Init
            singleTrader.Init();

            _timer!.RestartTimer("1");

            _timer!.RestartTimer("2");

            // *****************************************************************************
            // SingleTrader - Run
            // *****************************************************************************

            Log("\nRunning singleTrader...");

            IsRunning = true;
            await Task.Run(() =>
            {
                // Set state flags
                singleTrader.IsStarted = true;
                singleTrader.IsRunning = true;
                singleTrader.IsStopped = false;
                singleTrader.IsStopRequested = false;

                for (int i = 0; i < totalBars; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Check if stop is requested
                    if (singleTrader.IsStopRequested)
                    {
                        Log($"SingleTrader stopped by user request at bar {i}/{totalBars}");
                        break;
                    }

                    singleTrader.Run(i);

                    double percentage = (i + 1) / (double)totalBars * 100.0;
                    OnTraderProgress?.Invoke(i + 1, totalBars, percentage);
                }

            }, cancellationToken);
            IsRunning = false;

            _timer!.StopTimer("2");

            singleTrader.LastExecutionTimeStop = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
            singleTrader.LastExecutionTimeInMSec = _timer!.GetElapsedTime("2").ToString();

            if (this.SingleTraderRunMode == TraderRunMode.TradeOnly || this.SingleTraderRunMode == TraderRunMode.TradeAndQuery)
            {
                // Tarama bilgileri: (Finalize gerek kalmadan alinabilir)
                var yon           = singleTrader.SonYon;                    // "A"
                var kacBarOnce    = singleTrader.SonSinyaldenBeriBarSayisi; // 5
                var karZarar      = singleTrader.SonKarZararFiyat;          // 125.50
                var karZararYuzde = singleTrader.SonKarZararYuzde;          // 0.85
                var ozet          = singleTrader.TaramaOzeti;               // "A | Bar:5 | KZ:125.50 | %:0.85"

                Log($"\nScreening summary... : {ozet}");
            }

            Log("\nFinalizing singleTrader...");

            _timer!.RestartTimer("3");

            singleTrader.Finalize();

            // Pipe ile (default)
            var header = singleTrader.GetStatisticsHeaderRow();
            var data   = singleTrader.GetStatisticsDataRow();

            // Noktalı virgülle (CSV için)
            var csvHeader = singleTrader.GetStatisticsHeaderRow(";");
            var csvData   = singleTrader.GetStatisticsDataRow(";");

            // Dosyaya yazma: WriteTraderDataToFilesAsync metoduna taşındı.
            // Grafik açıkken arka planda çalışsın diye buradan kaldırıldı.
            // Orijinal kod:
            // if (!singleTrader.IsStopRequested && singleTrader.SaveStatisticsToFile)
            // {
            //     if (!singleTrader.OptimizationEnabled)
            //     {
            //         Log($"\nSaving statistics to files...");
            //         singleTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
            //     }
            //     else
            //     {
            //         Log($"\nSkipping full statistics write in optimization mode...");
            //     }
            // }

            _timer!.StopTimer("3");


            if (this.SingleTraderRunMode == TraderRunMode.TradeAndQuery || this.SingleTraderRunMode == TraderRunMode.QueryOnly)
            {
                var sorguOzeti = singleTrader.SorguOzeti;

                Log($"\nQuery summary... : {sorguOzeti}");
            }

            _timer!.StopTimer("1");

            _timer!.StopTimer("0");

            var t0 = _timer!.GetElapsedTime("0");
            var t1 = _timer!.GetElapsedTime("1");
            var t2 = _timer!.GetElapsedTime("2");
            var t3 = _timer!.GetElapsedTime("3");

            Log($"\nt0 = {t0} msec. <==> RunSingleTraderWithProgressAsync elapsed time");
            Log($"\nt1 = {t1} msec. <==> Running + Finalizing singleTrader elapsed time");
            Log($"\nt2 = {t2} msec. <==> Running singleTrader elapsed time");
            Log($"\nt3 = {t3} msec. <==> Finalizing singleTrader elapsed time");
        }
        catch (Exception ex)
        {
            Log($"An error occurred while running in RunSingleTraderWithProgressAsync(): {ex.Message}");
        }
        finally
        {
        }

        // Update state flags
        if (singleTrader is not null)
        {
            singleTrader.IsRunning = false;
            singleTrader.IsStopped = true;
            Log($"\nSingleTrader finished - IsRunning: {singleTrader.IsRunning}, IsStopped: {singleTrader.IsStopped}");
        }

        Log("");
        Log($"AlgoTrader '{Name}' completed. Processed {totalBars} bars.");
    }

    /// <summary>
    /// SingleTrader verilerini arka planda dosyalara yazar.
    /// RunSingleTraderWithProgressAsync içinden alındı; grafik açıkken
    /// paralel çalışması için ayrı metod yapıldı.
    /// Çağrı örneği:
    ///     var writeTask = algoTrader.WriteTraderDataToFilesAsync(algoTrader.SingleTrader);
    ///     await algoTrader.PlotSingleTraderData(algoTrader.SingleTrader);
    ///     await writeTask;
    /// </summary>
    public async Task WriteTraderDataToFilesAsync(SingleTrader trader)
    {
        if (trader is null)
        {
            Log("[WriteTraderDataToFilesAsync] trader is null, skipping.");
            return;
        }

        await Task.Run(() =>
        {
            if (!trader.IsStopRequested && trader.SaveStatisticsToFile)
            {
                if (!trader.OptimizationEnabled)
                {
                    Log($"\n[WriteTraderDataToFilesAsync] Saving statistics to files...");
                    trader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
                    Log($"\n[WriteTraderDataToFilesAsync] ✓ File writing completed.");
                }
                else
                {
                    Log($"\n[WriteTraderDataToFilesAsync] Skipping full statistics write in optimization mode.");
                }
            }
            else
            {
                Log($"\n[WriteTraderDataToFilesAsync] Skipped. IsStopRequested={trader.IsStopRequested}, SaveStatisticsToFile={trader.SaveStatisticsToFile}");
            }
        });
    }

    public async Task WriteTraderDataToFilesAsync(MultipleTrader trader)
    {
        if (trader is null)
        {
            Log("[WriteTraderDataToFilesAsync] trader is null, skipping.");
            return;
        }

        await Task.Run(() =>
        {
            if (!trader.IsStopRequested && trader.SaveStatisticsToFile)
            {
                // multipleTrader
                Log($"\n[WriteTraderDataToFilesAsync] Saving multipleTrader lists to files...");
                multipleTrader.WriteMultipleTraderListsToFiles(AppSettings.LogsDir);
                Log($"\n[WriteTraderDataToFilesAsync] ✓ multipleTrader file writing completed.");

                // mainTrader
                var mainTrader = trader.GetMainTrader();
                if (mainTrader is not null && mainTrader.SaveStatisticsToFile)
                {
                    Log($"\n[WriteTraderDataToFilesAsync] Saving mainTrader statistics to files...");
                    mainTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
                    Log($"\n[WriteTraderDataToFilesAsync] ✓ mainTrader file writing completed.");
                }

                // childTraders (optional)
                if (trader.WriteChildTradersDataToFiles)
                {
                    foreach (var childTrader in trader.Traders)
                    {
                        if (childTrader.SaveStatisticsToFile)
                        {
                            Log($"\n[WriteTraderDataToFilesAsync] Saving {childTrader.GetName()} statistics to files...");
                            childTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
                            Log($"\n[WriteTraderDataToFilesAsync] ✓ {childTrader.GetName()} file writing completed.");
                        }
                    }
                }
            }
            else
            {
                Log($"\n[WriteTraderDataToFilesAsync] Skipped. IsStopRequested={trader.IsStopRequested}");
            }
        });
    }

    void createChildTraders()
    {
        if (_childTraderConfigs.Count == 0)
            throw new InvalidOperationException("_childTraderConfigs boş. SetChildTraderCount() veya AddChildTraderConfig() çağrılmamış.");

        for (int i = 0; i < _childTraderConfigs.Count; i++)
        {
            var config  = _childTraderConfigs[i];
            int childId = config.ChildId;

            var childTrader = new SingleTrader(childId, $"childTrader_{childId}", this.Data, indicators, _logger);
            if (childTrader == null)
                throw new InvalidOperationException($"childTrader_{childId} can not be created...");

            childTrader.ClearCallbacks()
                       .SetCallbacks(OnSingleTraderReset, OnSingleTraderInit, OnSingleTraderRun, OnSingleTraderFinal, OnSingleTraderBeforeOrder, OnSingleTraderNotifySignal, OnSingleTraderAfterOrder, OnSingleTraderProgress);

            childTrader.Reset();

            // Set attributes
            childTrader.SymbolName             = this.SymbolName;
            childTrader.SymbolPeriod           = this.SymbolPeriod;
            childTrader.LastExecutionTime      = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
            childTrader.LastExecutionTimeStart = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

            // Configure position sizing — AppConfig.MultipleTrader.MainTrader.TradeParams (tüm child'lara uygulanır)
            childTrader.initialTradeParams!.ApplyFrom(config.TradeParams);

            // Sıralama Onemli
            // Apply per-child signals config (AppConfig.MultipleTrader.ChildTraders[i].Signals)
            // OnApplyUserFlags(childTrader); → AppConfig.MultipleTrader.ChildTraders[i].Signals ile değiştirildi
            if (config.Signals is { } s)
            {
                childTrader.ConfigureUserFlagsOnce();
                childTrader.signals.AlEnabled              = s.AlEnabled;
                childTrader.signals.SatEnabled             = s.SatEnabled;
                childTrader.signals.FlatOlEnabled          = s.FlatOlEnabled;
                childTrader.signals.PasGecEnabled          = s.PasGecEnabled;
                childTrader.signals.KarAlEnabled           = s.KarAlEnabled;
                childTrader.signals.ZararKesEnabled        = s.ZararKesEnabled;
                childTrader.signals.GunSonuPozKapatEnabled = s.GunSonuPozKapatEnabled;
                childTrader.signals.TimeFilteringEnabled   = s.TimeFilteringEnabled;
                childTrader.signals.TradeStartBarIndexEnabled = s.TradeStartBarIndexEnabled;
                childTrader.signals.TradeStartBarIndex        = s.TradeStartBarIndex;
                childTrader.signals.EquityCurveFilteringEnabled = false;
                childTrader.StartDateTimeStr = s.StartDateTime;
                childTrader.StopDateTimeStr  = s.StopDateTime;
                var startDt = System.DateTime.ParseExact(s.StartDateTime, "yyyy.MM.dd HH:mm:ss", null);
                childTrader.StartDateStr = startDt.ToString("yyyy.MM.dd");
                childTrader.StartTimeStr = startDt.ToString("HH:mm:ss");
                var stopDt = System.DateTime.ParseExact(s.StopDateTime, "yyyy.MM.dd HH:mm:ss", null);
                childTrader.StopDateStr = stopDt.ToString("yyyy.MM.dd");
                childTrader.StopTimeStr = stopDt.ToString("HH:mm:ss");
            }
            else
            {
                OnApplyUserFlags(childTrader); // fallback
            }

            // Apply user flags (2) — config.Save bloğu ile override edilecek
            OnApplyUserFlags2(childTrader, TraderApplyMode.MultipleTrader);

            // Apply per-child save config (AppConfig.MultipleTrader.Save'den üretilir)
            if (config.Save is { } sv)
            {
                childTrader.SaveStatisticsToFile                = sv.SaveStatisticsToFile;
                childTrader.SaveFullStatsTxtEnabled             = sv.SaveFullStatsTxtEnabled;
                childTrader.SaveFullStatsCsvEnabled             = sv.SaveFullStatsCsvEnabled;
                childTrader.SaveMinimalStatsTxtEnabled          = sv.SaveMinimalStatsTxtEnabled;
                childTrader.SaveMinimalStatsCsvEnabled          = sv.SaveMinimalStatsCsvEnabled;
                childTrader.SaveFullListsTxtEnabled             = sv.SaveFullListsTxtEnabled;
                childTrader.SaveFullListsCsvEnabled             = sv.SaveFullListsCsvEnabled;
                childTrader.SaveMinimalListsTxtEnabled          = sv.SaveMinimalListsTxtEnabled;
                childTrader.SaveMinimalListsCsvEnabled          = sv.SaveMinimalListsCsvEnabled;
                childTrader.SaveFullStatsTxtFormattedEnabled    = sv.SaveFullStatsTxtFormattedEnabled;
                childTrader.SaveMinimalStatsTxtFormattedEnabled = sv.SaveMinimalStatsTxtFormattedEnabled;
                childTrader.SavePerformansTxtEnabled            = sv.SavePerformansTxtEnabled;
                childTrader.SavePerformansCsvEnabled            = sv.SavePerformansCsvEnabled;

                if (!string.IsNullOrWhiteSpace(sv.FullStatsTxtFileName))             childTrader.FullStatsTxtFileName             = sv.FullStatsTxtFileName;
                if (!string.IsNullOrWhiteSpace(sv.FullStatsCsvFileName))             childTrader.FullStatsCsvFileName             = sv.FullStatsCsvFileName;
                if (!string.IsNullOrWhiteSpace(sv.MinimalStatsTxtFileName))          childTrader.MinimalStatsTxtFileName          = sv.MinimalStatsTxtFileName;
                if (!string.IsNullOrWhiteSpace(sv.MinimalStatsCsvFileName))          childTrader.MinimalStatsCsvFileName          = sv.MinimalStatsCsvFileName;
                if (!string.IsNullOrWhiteSpace(sv.FullListsTxtFileName))             childTrader.FullListsTxtFileName             = sv.FullListsTxtFileName;
                if (!string.IsNullOrWhiteSpace(sv.FullListsCsvFileName))             childTrader.FullListsCsvFileName             = sv.FullListsCsvFileName;
                if (!string.IsNullOrWhiteSpace(sv.MinimalListsTxtFileName))          childTrader.MinimalListsTxtFileName          = sv.MinimalListsTxtFileName;
                if (!string.IsNullOrWhiteSpace(sv.MinimalListsCsvFileName))          childTrader.MinimalListsCsvFileName          = sv.MinimalListsCsvFileName;
                if (!string.IsNullOrWhiteSpace(sv.FullStatsTxtFormattedFileName))    childTrader.FullStatsTxtFormattedFileName    = sv.FullStatsTxtFormattedFileName;
                if (!string.IsNullOrWhiteSpace(sv.MinimalStatsTxtFormattedFileName)) childTrader.MinimalStatsTxtFormattedFileName = sv.MinimalStatsTxtFormattedFileName;
                if (!string.IsNullOrWhiteSpace(sv.PerformansTxtFileName))            childTrader.PerformansTxtFileName            = sv.PerformansTxtFileName;
                if (!string.IsNullOrWhiteSpace(sv.PerformansCsvFileName))            childTrader.PerformansCsvFileName            = sv.PerformansCsvFileName;
            }

            if (config.Export is { } ex)
            {
                childTrader.ExportEnabled    = ex.ExportEnabled;
                childTrader.ExportConfigFile = ex.ExportConfigFile;
                childTrader.ExportVersion    = ex.ExportVersion;
            }

            // Configure multiple trader mode flag
            childTrader.MultipleTraderModeEnabled = true;

            int ecfId = config.EcfConfigId ?? childId;
            SetSingleTraderConfigureEquityCurveFilter(childTrader, ecfId);

            childTrader.RunMode = SingleTraderRunMode;

            if (childTrader.RunMode == TraderRunMode.TradeOnly || childTrader.RunMode == TraderRunMode.TradeAndQuery)
            {
                strategy = GetStrategy(config.StrategyId);
                childTrader.SetStrategy(strategy);
            }

            if (childTrader.RunMode == TraderRunMode.TradeAndQuery || childTrader.RunMode == TraderRunMode.QueryOnly)
            {
                int queryId = config.QueryId ?? childId;
                query = GetQuery(queryId);
                childTrader.SetQuery(query);
            }

            // Fallback: SaveStatisticsToFile'ı aktif et (config.Save yoksa)
            if (config.Save is null)
                childTrader.SaveStatisticsToFile = true;

            childTrader.Init();

            multipleTrader.AddTrader(childTrader);
        }
    }

    public async Task RunMultipleTraderWithProgressAsync(CancellationToken cancellationToken = default)
    {
        int totalBars = 0;

        if (!IsInitialized) {
            throw new InvalidOperationException("AlgoTrader not initialized. Call Initialize() first.");
        }

        try
        {
            _timer!.RestartTimer("0");

            totalBars = GetDataCount();

            Log($"AlgoTrader '{Name}' MultipleTrader started. Total bars: {totalBars}");

            // *****************************************************************************
            // Indicators
            // *****************************************************************************
            if (indicators != null) {
                Log("Disposing previous indicators instance...");
                indicators.Dispose();
                indicators = null;
            }

            Log("\nCreating indicators...");

            indicators = new IndicatorManager(this.Data);
            if (indicators == null)
                throw new InvalidOperationException("indicators can not be created...");

            // *****************************************************************************
            // MultipleTrader - Cleanup previous run
            // *****************************************************************************
            if (multipleTrader != null)
            {
                Log("Disposing previous multipleTrader instance...");
                multipleTrader.Dispose();
                multipleTrader = null;
            }

            Log("\nCreating multipleTrader...");

            multipleTrader = new MultipleTrader(0, this.Data, indicators, _logger);
            if (multipleTrader == null)
                throw new InvalidOperationException("multipleTrader can not be created...");

            multipleTrader.Reset();

            // MultipleTrader save config (AppConfig.MultipleTrader.Save)
            if (_multipleTraderSaveConfig is { } mts)
            {
                multipleTrader.SaveStatisticsToFile              = mts.SaveStatisticsToFile;
                multipleTrader.SaveMultipleTraderListsTxtEnabled = mts.SaveMultipleTraderListsTxtEnabled;
                multipleTrader.SaveMultipleTraderListsCsvEnabled = mts.SaveMultipleTraderListsCsvEnabled;
                if (!string.IsNullOrWhiteSpace(mts.MultipleTraderListsTxtFileName))
                    multipleTrader.MultipleTraderListsTxtFileName = mts.MultipleTraderListsTxtFileName;
                if (!string.IsNullOrWhiteSpace(mts.MultipleTraderListsCsvFileName))
                    multipleTrader.MultipleTraderListsCsvFileName = mts.MultipleTraderListsCsvFileName;
                multipleTrader.WriteChildTradersDataToFiles      = mts.WriteChildTradersDataToFiles;
            }
            else
            {
                // Fallback
                multipleTrader.SaveStatisticsToFile              = true;
                multipleTrader.SaveMultipleTraderListsTxtEnabled = true;
                multipleTrader.SaveMultipleTraderListsCsvEnabled = true;
                multipleTrader.MultipleTraderListsTxtFileName    = "MultipleTraderLists.txt";
                multipleTrader.MultipleTraderListsCsvFileName    = "MultipleTraderLists.csv";
                multipleTrader.WriteChildTradersDataToFiles      = false;
            }

            // MultipleTrader consensus config (AppConfig.MultipleTrader.Consensus)
            if (_multipleTraderConsensusConfig is { } mtc)
            {
                if (!string.IsNullOrWhiteSpace(mtc.Mode))
                    multipleTrader.ConsensusMode = mtc.Mode;
                multipleTrader.ConsensusMinNetCount = mtc.MinNetCount;
            }

            var mainTrader = multipleTrader.GetMainTrader();
            if (mainTrader == null)
                throw new InvalidOperationException("mainTrader can not be created...");

            // Assign callbacks to mainTrader
            mainTrader.ClearCallbacks()
                       .SetCallbacks(OnSingleTraderReset, OnSingleTraderInit, OnSingleTraderRun, OnSingleTraderFinal, OnSingleTraderBeforeOrder, OnSingleTraderNotifySignal, OnSingleTraderAfterOrder, OnSingleTraderProgress);

            mainTrader.Reset();

            // Set attributes
            mainTrader.SymbolName             = this.SymbolName;
            mainTrader.SymbolPeriod           = this.SymbolPeriod;
            mainTrader.SystemId               = this.SystemId;
            mainTrader.SystemName             = this.SystemName;
            mainTrader.StrategyId             = this.StrategyId;
            mainTrader.StrategyName           = this.StrategyName;
            mainTrader.QueryId                = this.QueryId;
            mainTrader.QueryName              = this.QueryName;
            mainTrader.LastExecutionTime      = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
            mainTrader.LastExecutionTimeStart = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
            
            // Configure position sizing — AppConfig.MultipleTrader.MainTrader.TradeParams
            if (_singleTraderTradeParamsConfig is not null)
                mainTrader.initialTradeParams!.ApplyFrom(_singleTraderTradeParamsConfig);

            // Apply flags from AppConfig (replaces OnApplyUserFlags + OnApplyUserFlags2)
            // OnApplyUserFlags(mainTrader);           → AppConfig.MultipleTrader.MainTrader.Signals ile değiştirildi
            // OnApplyUserFlags2(mainTrader, ...);     → AppConfig.MultipleTrader.MainTrader.Save ile değiştirildi
            ApplySingleTraderFlagsConfigs(mainTrader);

            // Configure multiple trader mode flag
            mainTrader.MultipleTraderModeEnabled = true;

            // Configure equity curve filter
            SetSingleTraderConfigureEquityCurveFilter(mainTrader);

            // Assign runMode
            mainTrader.RunMode = SingleTraderRunMode;

            mainTrader.Init();

            // *****************************************************************************
            // Create child SingleTraders and add to MultipleTrader
            // *****************************************************************************
            // TODO : createChildTraders leri Program.cs seviyesinde yapılmasını sağlamak...
            createChildTraders();

            // TODO : Aşağıdakini oku
            /*
                AlgoTrader.cs — createChildTraders()

              Sorun 1 — Hardcoded(3 fixed block), dinamik değil:

              _strategyConfigs listesi kaç eleman içeriyorsa o kadar child trader oluşturulmalı. Şu an hep 3 blok hardcoded. ConfigureStrategies() ile 2 strateji seçilirse 3.child trader GetStrategy(2) çağıracak ve
              exception fırlatacak.

              Çözüm: 3 ayrı block → _strategyConfigs üzerinden for döngüsü.
            */

            multipleTrader.Init();

            // *****************************************************************************
            // MultipleTrader - Run
            // *****************************************************************************

            Log("\nRunning multipleTrader...");

            _timer!.RestartTimer("1");
            _timer!.RestartTimer("2");

            IsRunning = true;
            await Task.Run(() =>
            {
                multipleTrader.IsStarted = true;
                multipleTrader.IsRunning = true;
                multipleTrader.IsStopped = false;
                multipleTrader.IsStopRequested = false;

                for (int i = 0; i < totalBars; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (multipleTrader.IsStopRequested)
                    {
                        Log($"MultipleTrader stopped by user request at bar {i}/{totalBars}");
                        break;
                    }

                    multipleTrader.Run(i);

                    double percentage = (i + 1) / (double)totalBars * 100.0;
                    OnTraderProgress?.Invoke(i + 1, totalBars, percentage);
                }

            }, cancellationToken);
            IsRunning = false;

            _timer!.StopTimer("2");

            var tradersCount = multipleTrader.Traders.Count;
            for (int i = 0; i < tradersCount; i++)
            {
                var singleTrader = multipleTrader.Traders[i];
                // Tarama bilgileri
                var _yon           = singleTrader.SonYon;                    // "A"
                var _kacBarOnce    = singleTrader.SonSinyaldenBeriBarSayisi; // 5
                var _karZarar      = singleTrader.SonKarZararFiyat;          // 125.50
                var _karZararYuzde = singleTrader.SonKarZararYuzde;          // 0.85
                var _ozet          = singleTrader.TaramaOzeti;               // "A | Bar:5 | KZ:125.50 | %:0.85"
            }

            // Tarama bilgileri: (Finalize gerek kalmadan alinabilir)
            var yon           = mainTrader.SonYon;                    // "A"
            var kacBarOnce    = mainTrader.SonSinyaldenBeriBarSayisi; // 5
            var karZarar      = mainTrader.SonKarZararFiyat;          // 125.50
            var karZararYuzde = mainTrader.SonKarZararYuzde;          // 0.85
            var ozet          = mainTrader.TaramaOzeti;               // "A | Bar:5 | KZ:125.50 | %:0.85"


            // *****************************************************************************
            // MultipleTrader - Finalize
            // *****************************************************************************

            Log("\nFinalizing multipleTrader...");

            _timer!.RestartTimer("3");

            multipleTrader.Finalize();

            // TODO : Asagisı singleTrader e benzer olarak yapılacak
            /*
            // Pipe ile (default)
            var header = multipleTrader.GetStatisticsHeaderRow();
            var data   = multipleTrader.GetStatisticsDataRow();

            // Noktalı virgülle (CSV için)
            var csvHeader = multipleTrader.GetStatisticsHeaderRow(";");
            var csvData   = multipleTrader.GetStatisticsDataRow(";");
            */

            // Dosyaya yazma: WriteTraderDataToFilesAsync metoduna taşındı.
            // TODO : Asağıdaki kodu uygun sekilde WriteTraderDataToFilesAsync(multipleTrader)'e taşı. 
            /*
            // Dosyaya yazma: stop edilmediyse yaz
            if (!multipleTrader.IsStopRequested)
            {
                if (mainTrader.SaveStatisticsToFile)
                {
                    Log($"\nSaving MultipleTraderListsToFile...");
                    multipleTrader.WriteMultipleTraderListsToFiles(AppSettings.LogsDir);
                }

                if (mainTrader.SaveStatisticsToFile)
                {
                    Log($"\nSaving mainTrader statistics to files...");
                    mainTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
                }

                foreach (var childTrader in multipleTrader.Traders)
                {
                    if (childTrader.SaveStatisticsToFile)
                        childTrader.WriteStatisticsToFile(AppSettings.LogsDir, AppSettings.ConfigsDir);
                }
            }
            */
            _timer!.StopTimer("3");

            _timer!.StopTimer("1");
            _timer!.StopTimer("0");

            var t0 = _timer!.GetElapsedTime("0");
            var t1 = _timer!.GetElapsedTime("1");
            var t2 = _timer!.GetElapsedTime("2");
            var t3 = _timer!.GetElapsedTime("3");

            Log($"\nt0 = {t0} msec. <==> RunMultipleTraderWithProgressAsync elapsed time");
            Log($"\nt1 = {t1} msec. <==> Running + Finalizing multipleTrader elapsed time");
            Log($"\nt2 = {t2} msec. <==> Running multipleTrader elapsed time");
            Log($"\nt3 = {t3} msec. <==> Finalizing multipleTrader elapsed time");
        }
        catch (Exception ex)
        {
            Log($"An error occurred while running in RunMultipleTraderWithProgressAsync(): {ex.Message}");
        }
        finally
        {
        }

        // Update state flags
        if (multipleTrader is not null)
        {
            multipleTrader.IsRunning = false;
            multipleTrader.IsStopped = true;
            Log($"\nMultipleTrader finished - IsRunning: {multipleTrader.IsRunning}, IsStopped: {multipleTrader.IsStopped}");
        }

        Log("");
        Log($"AlgoTrader '{Name}' MultipleTrader completed. Processed {totalBars} bars.");
        Log("");
    }


    public event Action<int, int, double>? OnTraderProgress;
    private void OnOptimizationProgress(SingleTraderOptimizer singleTraderOptimizer, int current, int total, double percentage)
    {
        /*if (_logger == null) return;

        var consoleLogger = LogManager.GetConsoleLogger();

        if (current >= total)
        {
            consoleLogger.Write($"\r\tProgress         : {current}/{total} ({percentage:F1}%)");
            consoleLogger.WriteLine("");
        }
        else
        {
            consoleLogger.Write($"\r\tProgress         : {current}/{total} ({percentage:F1}%)");
        }*/
    }

    private void OnOptimizationSingleTraderProgress(SingleTrader trader, int currentBar, int totalBars, double percentage)
    {
        /*if (_logger == null) return;

        var consoleLogger = LogManager.GetConsoleLogger();

        if (currentBar >= totalBars)
        {
            consoleLogger.Write($"\r\tProgress         : {currentBar}/{totalBars} ({percentage:F1}%)");
            consoleLogger.WriteLine("");
        }
        else
        {
            consoleLogger.Write($"\r\tProgress         : {currentBar}/{totalBars} ({percentage:F1}%)");
        }*/
    }
    public async Task RunSingleTraderOptWithProgressAsync(CancellationToken cancellationToken = default)
    {
        int totalBars = 0;

        if (!IsInitialized)
        {
            throw new InvalidOperationException("AlgoTrader not initialized. Call Initialize() first.");
        }

        try
        {
            _timer!.RestartTimer("0");

            totalBars = GetDataCount();

            Log($"AlgoTrader '{Name}' started. Total bars: {totalBars}");

            // *****************************************************************************
            // Indicators - beg
            // *****************************************************************************
            if (indicators != null)
            {
                Log("Disposing previous indicators instance...");
                indicators.Dispose();
                indicators = null;
            }

            Log("\nCreating indicators...");

            indicators = new IndicatorManager(this.Data);
            if (indicators == null)
                throw new InvalidOperationException("indicators can not be created...");

            // *****************************************************************************
            // SingleTraderOptimizer - beg
            // *****************************************************************************
            if (singleTraderOptimizer != null)
            {
                Log("Disposing previous singleTraderOptimizer instance...");
                singleTraderOptimizer.Dispose();
                singleTraderOptimizer = null;
            }

            Log("\nCreating singleTraderOptimizer...");

            singleTraderOptimizer = new SingleTraderOptimizer(0, this.Data, indicators, _logger);

            // Progress callback
            singleTraderOptimizer.OnOptimizationProgress += OnOptimizationProgress;                                     // singleTraderOptimizer.OnOptimizationProgress = (currentCombination, totalCombinations)
            singleTraderOptimizer.OnSingleTraderProgressCallback += OnOptimizationSingleTraderProgress;                 // (currentBar, totalBarsInner)

            // Reset
            singleTraderOptimizer.Reset();

            // Optimization log file settings
            if (_singleTraderOptLogConfig is { } lg)
            {
                singleTraderOptimizer.CsvFileLoggingEnabled  = lg.CsvFileLoggingEnabled;
                singleTraderOptimizer.CsvFilePath            = Path.Combine(AppSettings.OptLogsDir, lg.CsvFileName);
                singleTraderOptimizer.TxtFileLoggingEnabled  = lg.TxtFileLoggingEnabled;
                singleTraderOptimizer.TxtFilePath            = Path.Combine(AppSettings.OptLogsDir, lg.TxtFileName);
                singleTraderOptimizer.AppendEnabled          = lg.AppendEnabled;
                singleTraderOptimizer.FileFlushIntervalMs    = lg.FileFlushIntervalMs;
                singleTraderOptimizer.ConfigFilePath         = lg.StatisticsExporterConfigFileEnabled
                    ? Path.Combine(AppSettings.ConfigsDir, lg.StatisticsExporterConfigFile)
                    : string.Empty;
            }

            // Sorted output settings
            if (_singleTraderOptSortConfig is { } sr)
            {
                singleTraderOptimizer.SortField         = sr.SortField;
                singleTraderOptimizer.SortedCsvFilePath = Path.Combine(AppSettings.OptLogsDir, sr.SortedCsvFileName);
                singleTraderOptimizer.SortedTxtFilePath = Path.Combine(AppSettings.OptLogsDir, sr.SortedTxtFileName);
            }

            // Signals config (her test trader'ına uygulanır)
            singleTraderOptimizer.SignalsConfig = _singleTraderOptSignalsConfig;

            // Parametre range'leri (stored config'den)
            if (_optimizationParameterRanges.Count == 0)
                throw new InvalidOperationException("No optimization parameter ranges configured. Call AddOptimizationParameterRange() first.");

            foreach (var range in _optimizationParameterRanges)
            {
                singleTraderOptimizer.AddParameterRange(range.Name, range.Min, range.Max, range.Step);
            }

            // Kombinasyonlari uret
            singleTraderOptimizer.GenerateParameterCombinations();
            Log("");
            Log($"Total combinations: {singleTraderOptimizer.AllCombinations.Count}");

            // Strategy factory - stored config'den veya fallback
            if (_optimizationStrategyFactory != null)
            {
                singleTraderOptimizer.SetStrategyFactory(_optimizationStrategyFactory);
            }
            else
            {
                // Fallback: _currentStrategyName + parametrelerden otomatik factory
                var strategyName = _currentStrategyName;
                singleTraderOptimizer.SetStrategyFactory((data, ind, parameters) =>
                {
                    return _strategyRegistry.CreateStrategy(data, ind, _logger, strategyName, parameters);
                });
            }

            // Set optimization range (PartialOpt)
            if (_singleTraderOptRangeConfig is { } rng)
            {
                singleTraderOptimizer.OptimizationFrom = rng.OptimizationFrom;
                singleTraderOptimizer.OptimizationTo   = rng.OptimizationTo;
            }

            // Set trade params
            if (_singleTraderOptTradeParamsConfig is { } tp)
            {
                singleTraderOptimizer.IlkBakiye      = tp.IlkBakiye;
                singleTraderOptimizer.KontratSayisi  = tp.KontratSayisi;
                singleTraderOptimizer.KomisyonCarpan = tp.KomisyonCarpan;
                singleTraderOptimizer.KaymaMiktari   = tp.KaymaMiktari;
            }

            // Full InitialTradeParams (MarketType dahil) — createSingleTrader() içinde ApplyFrom ile kullanılır
            singleTraderOptimizer.TradeParamsOverride = _singleTraderTradeParamsConfig;

            // TODO: ECF bloğu da stored config pattern'e alınacak (_equityCurveFilterConfigs → kendi stored config'i).
            // Set equity curve filter config (id=0 varsa optimizer'a aktar)
            var ecfConfig = _equityCurveFilterConfigs.FirstOrDefault(c => c.Id == 0);
            singleTraderOptimizer.EquityCurveFilterConfig = ecfConfig;

            // Init
            singleTraderOptimizer.Init();

            // Run optimization
            _timer!.RestartTimer("1");

            await Task.Run(() =>
            {
                // Set state flags
                singleTraderOptimizer.IsStarted = true;
                singleTraderOptimizer.IsRunning = true;
                singleTraderOptimizer.IsStopped = false;
                singleTraderOptimizer.IsStopRequested = false;

                singleTraderOptimizer.Run(cancellationToken);
            }, cancellationToken);

            _timer!.StopTimer("1");

            var bestResult = singleTraderOptimizer.GetBestResult();
            if (bestResult != null)
            {
                Log($"\n=== BEST RESULT ===");
                foreach (var kvp in bestResult.Parameters)
                    Log($"  {kvp.Key}: {kvp.Value}");
                Log($"  NetProfit: {bestResult.NetProfit:F2}");
                Log($"  WinRate: {bestResult.WinRate:F2}%");
                Log($"  ProfitFactor: {bestResult.ProfitFactor:F2}");
                Log($"  ScoreFiyatNet: {bestResult.ScoreFiyatNet:F2}");
                Log($"  ScoreFiyat: {bestResult.ScoreFiyat:F2}");
                Log($"  ScorePuan: {bestResult.ScorePuan:F2}");
            }

            _timer!.StopTimer("0");
            var t0 = _timer!.GetElapsedTime("0");
            var t1 = _timer!.GetElapsedTime("1");
            Log($"\nt0 = {t0} msec. <==> RunSingleTraderOptWithProgressAsync total elapsed time");
            Log($"\nt1 = {t1} msec. <==> Optimization elapsed time");
        }
        catch (Exception ex)
        {
            Log($"An error occurred while running in RunSingleTraderOptWithProgressAsync(): {ex.Message}");
        }
        finally
        {
        }

        // Update state flags
        if (singleTraderOptimizer is not null)
        {
            singleTraderOptimizer.IsRunning = false;
            singleTraderOptimizer.IsStopped = true;
            Log($"\nSingleTraderOptimizer finished - IsRunning: {singleTraderOptimizer.IsRunning}, IsStopped: {singleTraderOptimizer.IsStopped}");
        }

        Log("");
        Log($"AlgoTrader '{Name}' completed. Processed {totalBars} bars.");
        Log("");
    }

    public event Action<string>? MessageReceived;

    private void Log(string message)
    {
        if (_logger == null) return;
        LogManager.LogRaw(message);
    }

    private void OnMessage(string message)
    {
        MessageReceived?.Invoke(message);
    }

    // ==========================================================================
    // Python Görselleştirme
    // ==========================================================================

    /// <summary>
    /// Python ortamını yapılandırır. PlotXxx metodlarından önce çağrılmalı.
    /// </summary>
    public bool SetupPython(bool runHello = true)
    {
        try
        {
            // Zaten kuruluysa tekrar başlatma
            if (_pythonPlotter != null && _pythonPlotter.IsInitialized)
            {
                Log("Python zaten kurulu, tekrar başlatma atlandı.");
                return true;
            }

            var envDll = Environment.GetEnvironmentVariable("PYTHONNET_PYDLL");
            if (!string.IsNullOrWhiteSpace(envDll) && File.Exists(envDll))
            {
                PythonDll = envDll;
                Log($"PYTHONNET_PYDLL bulundu: {PythonDll}");
            }
            else
            {
                string[] preferredDlls =
                {
                    // Python 3.12 (tercih edilen)
                    @"C:\Program Files\Python312\python312.dll",
                    @"C:\Python312\python312.dll",
                    $@"C:\Users\{Environment.UserName}\AppData\Local\Programs\Python\Python312\python312.dll"
                };

                var found = preferredDlls.FirstOrDefault(File.Exists);
                PythonDll = found ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(PythonDll))
            {
                Log("Python DLL bulunamadı. PYTHONNET_PYDLL ayarlayın veya Python 3.12 kurun.");
                return false;
            }

            if (_pythonPlotter != null)
            {
                _logger?.WriteRaw("Disposing previous pythonPlotter instance...");
                _pythonPlotter.Dispose();
                _pythonPlotter = null;
            }

            _pythonPlotter ??= new PythonPlotter();
            _pythonPlotter.SetLogger(_logger);
            _pythonPlotter.SetIndicators(indicators);

            if (!string.IsNullOrEmpty(PythonDll))
                _pythonPlotter.PythonDll = PythonDll;

            if (!_pythonPlotter.IsInitialized)
                _pythonPlotter.Initialize();

            _logger?.WriteRaw($"✓ Python DLL selected: {PythonDll}");

            // Basit doğrulama: main.py -> hello()
            if (runHello)
                _pythonPlotter.RunHello();

            return true;
        }
        catch (Exception ex)
        {
            Log($"SetupPython error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Verilen SingleTrader'ın koşum sonuçlarını Python/imgui_bundle ile görselleştirir.
    /// Plot penceresi ayrı thread'de çalışır; pencere kapanana dek bekler.
    /// </summary>
    /// <param name="singleTrader">
    /// Finalize() tamamlanmış SingleTrader instance'ı.
    /// algoTrader.SingleTrader (nullable) doğrudan geçilebilir; null ise exception fırlatılır.
    /// </param>
    public async Task PlotSingleTraderData(SingleTrader? singleTrader)
    {
        if (singleTrader == null)
            throw new ArgumentNullException(nameof(singleTrader),
                "SingleTrader null. RunSingleTraderWithProgressAsync() başarıyla tamamlandı mı?");

        if (_pythonPlotter == null || !_pythonPlotter.IsInitialized)
            throw new InvalidOperationException("Python ortamı hazır değil. Önce SetupPython() çağırın.");

        await Task.Run(() => _pythonPlotter.PlotSingleTraderData(singleTrader));
    }

    public async Task PlotMultipleTraderData(MultipleTrader? multipleTrader)
    {
        if (multipleTrader == null)
            throw new ArgumentNullException(nameof(multipleTrader));

        if (_pythonPlotter == null || !_pythonPlotter.IsInitialized)
            throw new InvalidOperationException("Python ortamı hazır değil. Önce SetupPython() çağırın.");

        await Task.Run(() => _pythonPlotter.PlotMultipleTraderData(multipleTrader));
    }

    public void Dispose()
    {
        strategy?.Dispose();
        strategy = null;

        query?.Dispose();
        query = null;

        singleTrader?.Dispose();
        singleTrader = null;

        multipleTrader?.Dispose();
        multipleTrader = null;

        singleTraderOptimizer?.Dispose();
        singleTraderOptimizer = null;

        indicators?.Dispose();
        indicators = null;

        _pythonPlotter?.Dispose();
        _pythonPlotter = null;
    }

}

// ==========================================================================
// Config entry types for MultipleTrader strategy/query/filter lists
// ==========================================================================

public class StrategyConfigEntry
{
    public int Id { get; }
    public string StrategyName { get; }
    public Dictionary<string, object> Parameters { get; }

    public StrategyConfigEntry(int id, string strategyName, Dictionary<string, object> parameters)
    {
        Id = id;
        StrategyName = strategyName;
        Parameters = parameters;
    }
}

public class QueryConfigEntry
{
    public int Id { get; }
    public string QueryName { get; }
    public Dictionary<string, object> Parameters { get; }

    public QueryConfigEntry(int id, string queryName, Dictionary<string, object> parameters)
    {
        Id = id;
        QueryName = queryName;
        Parameters = parameters;
    }
}

public class EquityCurveFilterConfigEntry
{
    public int Id { get; }
    public bool Enabled { get; }
    public bool ThresholdTypeIsPercent { get; }
    public double ProfitConfirmationThreshold { get; }
    public double LossConfirmationThreshold { get; }
    public ConfirmationTrigger ConfirmationTrigger { get; }

    public EquityCurveFilterConfigEntry(int id, bool enabled, bool thresholdTypeIsPercent, double profitThreshold, double lossThreshold, ConfirmationTrigger trigger)
    {
        Id = id;
        Enabled = enabled;
        ThresholdTypeIsPercent = thresholdTypeIsPercent;
        ProfitConfirmationThreshold = profitThreshold;
        LossConfirmationThreshold = lossThreshold;
        ConfirmationTrigger = trigger;
    }
}

public class OptimizationParameterRangeEntry
{
    public string Name { get; }
    public double Min { get; }
    public double Max { get; }
    public double Step { get; }

    public OptimizationParameterRangeEntry(string name, double min, double max, double step)
    {
        Name = name;
        Min = min;
        Max = max;
        Step = step;
    }
}

// ==========================================================================
// SingleTrader Signals & Save config (AlgoTrade.Core.Trading namespace'inde yaşar;
// AppConfigApplier bunları AppConfig verilerinden oluşturur ve AlgoTrader'a iletir.)
// ==========================================================================

/// <summary>OnApplyUserFlags (traderId==0 / SingleTrader) ayarlarının AppConfig karşılığı.</summary>
public class SingleTraderSignalsConfig
{
    public bool   AlEnabled              { get; set; } = true;
    public bool   SatEnabled             { get; set; } = true;
    public bool   FlatOlEnabled          { get; set; } = true;
    public bool   PasGecEnabled          { get; set; } = true;
    public bool   KarAlEnabled           { get; set; } = true;
    public bool   ZararKesEnabled        { get; set; } = true;
    public bool   GunSonuPozKapatEnabled { get; set; } = false;
    public bool   TimeFilteringEnabled       { get; set; } = false;
    public string StartDateTime              { get; set; } = "2025.05.25 09:35:00";
    public string StopDateTime               { get; set; } = "2025.06.02 17:55:00";
    public bool   TradeStartBarIndexEnabled  { get; set; } = false;
    public int    TradeStartBarIndex         { get; set; } = 0;
}

/// <summary>MultipleTrader nesnesinin kayıt ayarları (composite liste dosyaları).</summary>
public class MultipleTraderObjectSaveConfig
{
    public bool   SaveStatisticsToFile                { get; set; } = true;
    public bool   SaveMultipleTraderListsTxtEnabled   { get; set; } = true;
    public bool   SaveMultipleTraderListsCsvEnabled   { get; set; } = true;
    public string MultipleTraderListsTxtFileName      { get; set; } = "MultipleTraderLists.txt";
    public string MultipleTraderListsCsvFileName      { get; set; } = "MultipleTraderLists.csv";
    public bool   WriteChildTradersDataToFiles        { get; set; } = false;
}

/// <summary>MultipleTrader.BuildConsensusSignal() ayarları (AppConfig.MultipleTrader.Consensus).</summary>
public class MultipleTraderConsensusConfig
{
    public string Mode        { get; set; } = "Net";
    public int    MinNetCount { get; set; } = 1;
}

/// <summary>OnApplyUserFlags2 (traderId==0 / SingleTrader) ayarlarının AppConfig karşılığı.</summary>
public class SingleTraderSaveConfig
{
    public bool SaveStatisticsToFile                { get; set; } = true;
    public bool SaveFullStatsTxtEnabled             { get; set; } = true;
    public bool SaveFullStatsCsvEnabled             { get; set; } = true;
    public bool SaveMinimalStatsTxtEnabled          { get; set; } = true;
    public bool SaveMinimalStatsCsvEnabled          { get; set; } = true;
    public bool SaveFullListsTxtEnabled             { get; set; } = true;
    public bool SaveFullListsCsvEnabled             { get; set; } = true;
    public bool SaveMinimalListsTxtEnabled          { get; set; } = true;
    public bool SaveMinimalListsCsvEnabled          { get; set; } = true;
    public bool SaveFullStatsTxtFormattedEnabled    { get; set; } = true;
    public bool SaveMinimalStatsTxtFormattedEnabled { get; set; } = true;
    public bool SavePerformansTxtEnabled            { get; set; } = true;
    public bool SavePerformansCsvEnabled            { get; set; } = true;

    // Çıktı dosya adları
    public string FullStatsTxtFileName             { get; set; } = "SingleTraderStatistics.txt";
    public string FullStatsCsvFileName             { get; set; } = "SingleTraderStatistics.csv";
    public string MinimalStatsTxtFileName          { get; set; } = "SingleTraderStatisticsMinimal.txt";
    public string MinimalStatsCsvFileName          { get; set; } = "SingleTraderStatisticsMinimal.csv";
    public string FullListsTxtFileName             { get; set; } = "SingleTraderLists.txt";
    public string FullListsCsvFileName             { get; set; } = "SingleTraderLists.csv";
    public string MinimalListsTxtFileName          { get; set; } = "SingleTraderListsMinimal.txt";
    public string MinimalListsCsvFileName          { get; set; } = "SingleTraderListsMinimal.csv";
    public string FullStatsTxtFormattedFileName    { get; set; } = "SingleTraderStatisticsFormatted.txt";
    public string MinimalStatsTxtFormattedFileName { get; set; } = "SingleTraderStatisticsMinimalFormatted.txt";
    public string PerformansTxtFileName            { get; set; } = "SingleTraderPerformans.txt";
    public string PerformansCsvFileName            { get; set; } = "SingleTraderPerformans.csv";
}

public class SingleTraderPlotConfig
{
    public bool PlotEnabled { get; set; } = false;
}

public class SingleTraderOptimizationConfig
{
    public bool OptimizationEnabled { get; set; } = false;
}

public class SingleTraderExportConfig
{
    public bool   ExportEnabled    { get; set; } = false;
    public string ExportConfigFile { get; set; } = "StatisticsExporterConfig.json";
    public string ExportVersion    { get; set; } = "v1";
}

public class SingleTraderOptRangeConfig
{
    public int OptimizationFrom { get; set; } = -1;
    public int OptimizationTo   { get; set; } = -1;
}

public class SingleTraderOptTradeParamsConfig
{
    public double IlkBakiye      { get; set; } = 100_000.0;
    public int    KontratSayisi  { get; set; } = 1;
    public double KomisyonCarpan { get; set; } = 20.0;
    public double KaymaMiktari   { get; set; } = 0.5;
}

public class SingleTraderOptLogConfig
{
    public bool   CsvFileLoggingEnabled               { get; set; } = true;
    public string CsvFileName                         { get; set; } = "singleTraderOptLog.csv";
    public bool   TxtFileLoggingEnabled               { get; set; } = true;
    public string TxtFileName                         { get; set; } = "singleTraderOptLog.txt";
    public bool   AppendEnabled                       { get; set; } = true;
    public bool   StatisticsExporterConfigFileEnabled { get; set; } = true;
    public string StatisticsExporterConfigFile        { get; set; } = "StatisticsExporterConfig.json";
    public int    FileFlushIntervalMs                 { get; set; } = -1;
}

public class SingleTraderOptSortOutputConfig
{
    public string SortField         { get; set; } = "GetiriFiyatNet";
    public string SortedCsvFileName { get; set; } = "singleTraderOptLog_sorted.csv";
    public string SortedTxtFileName { get; set; } = "singleTraderOptLog_sorted.txt";
}

/// <summary>
/// MultipleTrader için her bir child trader'ın konfigürasyonu.
/// AppConfig.MultipleTrader.ChildTraders listesinden veya SetChildTraderCount() ile oluşturulur.
/// </summary>
public class ChildTraderConfigEntry
{
    /// <summary>Child trader'ın sırası (0-tabanlı).</summary>
    public int ChildId { get; }

    /// <summary>Kullanılacak stratejinin _strategyConfigs içindeki Id'si.</summary>
    public int StrategyId { get; set; }

    /// <summary>Kullanılacak query'nin _queryConfigs içindeki Id'si. null → ChildId kullanılır.</summary>
    public int? QueryId { get; set; }

    /// <summary>Kullanılacak ECF'nin _equityCurveFilterConfigs içindeki Id'si. null → ChildId kullanılır.</summary>
    public int? EcfConfigId { get; set; }

    /// <summary>
    /// Bu child trader için trade parametreleri.
    /// createChildTraders() içinde ApplyFrom() ile uygulanır (MainTrader'dan aktarılır).
    /// </summary>
    public InitialTradeParams TradeParams { get; } = new();

    /// <summary>
    /// Bu child trader için sinyal bayrakları.
    /// null → createChildTraders() içinde OnApplyUserFlags fallback kullanılır.
    /// </summary>
    public SingleTraderSignalsConfig? Signals { get; set; }

    /// <summary>
    /// Bu child trader için kayıt ayarları (dosya adları dahil).
    /// null → createChildTraders() içinde OnApplyUserFlags2 sonucu korunur.
    /// </summary>
    public SingleTraderSaveConfig? Save { get; set; }

    /// <summary>
    /// Bu child trader için export ayarları (versiyonlu sütun tanımları).
    /// null → ExportEnabled=false (export yapılmaz).
    /// </summary>
    public SingleTraderExportConfig? Export { get; set; }

    public ChildTraderConfigEntry(int childId, int strategyId, int? queryId = null, int? ecfConfigId = null)
    {
        ChildId     = childId;
        StrategyId  = strategyId;
        QueryId     = queryId;
        EcfConfigId = ecfConfigId;
    }
}
