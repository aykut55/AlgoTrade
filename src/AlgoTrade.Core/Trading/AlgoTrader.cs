using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Timer;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Queries;
using AlgoTrade.Core.Trading.Query;
using AlgoTrade.Core.Trading.Strategies;
using AlgoTrade.Core.Trading.Strategy;

namespace AlgoTrade.Core.Trading;

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

    }
    private void OnApplyUserFlags(SingleTrader trader)
    {
        // InitializeUserControlledFlags
        trader.ConfigureUserFlagsOnce();

        int traderId = trader.GetId();
        if (traderId == 0)
        {
            // 0 id'li trader icin
        }
        else if (traderId == 1)
        {
            // 1 id'li trader icin
        }

        var dateTimes = new string[] { "2025.05.25 09:35:00", "2025.06.02 17:55:00" };

        trader.StartDateTimeStr = dateTimes[0];
        trader.StopDateTimeStr = dateTimes[1];

        var startDateTime = System.DateTime.ParseExact(dateTimes[0], "yyyy.MM.dd HH:mm:ss", null);
        trader.StartDateStr = startDateTime.ToString("yyyy.MM.dd");  // "2025.05.25"
        trader.StartTimeStr = startDateTime.ToString("HH:mm:ss");    // "14:30:00"

        var stopDateTime = System.DateTime.ParseExact(dateTimes[1], "yyyy.MM.dd HH:mm:ss", null);
        trader.StopDateStr = stopDateTime.ToString("yyyy.MM.dd");    // "2025.06.02"
        trader.StopTimeStr = stopDateTime.ToString("HH:mm:ss");      // "14:00:00"
    }

    private void setSingleTraderConfigureEquityCurveFilter(SingleTrader trader)
    {
        trader.signals.EquityCurveFilteringEnabled = this.EquityCurveFilteringEnabled;
        trader.ConfigureEquityCurveFilter(
            isPercent: this.ThresholdTypeIsPercent,
            profitThreshold: this.ProfitConfirmationThreshold,
            lossThreshold: this.LossConfirmationThreshold,
            trigger: this.ConfirmationTrigger
        );
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

            // Sıralama Onemli
            // Apply user flags
            OnApplyUserFlags(singleTrader);
            
            // Configure equity curve filter
            setSingleTraderConfigureEquityCurveFilter(singleTrader);

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

            if (singleTrader.IsStopRequested)
            {
                singleTrader.Finalize(false);
            }
            else
            {
                singleTrader.Finalize(true);
            }

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
        Log("");

    }

    public event Action<int, int, double>? OnTraderProgress;

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

    public void Dispose()
    {
        strategy?.Dispose();
        strategy = null;

        query?.Dispose();
        query = null;

        singleTrader?.Dispose();
        singleTrader = null;

        indicators?.Dispose();
        indicators = null;
    }

}
