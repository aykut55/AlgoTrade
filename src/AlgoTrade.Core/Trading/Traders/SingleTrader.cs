using AlgoTrade.Core;
using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using MathNet.Numerics.Statistics;

namespace AlgoTrade.Core.Trading;

public class SingleTrader : MarketDataProvider, IDisposable
{
    #region Properties

    // Identification
    public int Id { get; private set; }
    public void SetId(int id) => Id = id;
    public int GetId() => Id;

    public string Name { get; private set; }
    public void SetName(string name) => Name = name;
    public string GetName() => Name;

    public void SetData(List<StockData> data)
    {
        _data = data;
    }

    // Symbol and System Id
    public string SymbolName { get; set; }
    public string SymbolPeriod { get; set; }
    public string SystemId { get; set; }
    public string SystemName { get; set; }
    public string StrategyId { get; set; }
    public string StrategyName { get; set; }

    // Execution Time Tracking
    public string LastExecutionId { get; set; }
    public string LastExecutionTime { get; set; }
    public string LastExecutionTimeStart { get; set; }
    public string LastExecutionTimeStop { get; set; }
    public string LastExecutionTimeInMSec { get; set; }
    public string LastResetTime { get; set; }
    public string LastStatisticsCalculationTime { get; set; }

    // Logger
    private LogManager? _logger;
    public void SetLogger(LogManager? logger)
    {
        _logger = logger;
    }

    private IndicatorManager? _indicators;
    public void SetIndicators(IndicatorManager? indicators)
    {
        _indicators = indicators;
    }

    public InitialTradeParams initialTradeParams { get; private set; }

    #endregion

    public event Action<SingleTrader, int>? OnReset;
    public event Action<SingleTrader, int>? OnInit;
    public event Action<SingleTrader, int>? OnRun;
    public event Action<SingleTrader, int>? OnFinal;
    public event Action<SingleTrader, int>? OnBeforeOrder;
    public event Action<SingleTrader, string, int>? OnNotifySignal;
    public event Action<SingleTrader, int>? OnAfterOrder;
    public event Action<SingleTrader, int, int, double>? OnProgress;
    public event Action<SingleTrader>? OnApplyUserFlags;    

    public SingleTrader(int id, string name, List<StockData> data, IndicatorManager indicators, LogManager? logger = null)
    {
        SetId(id);
        SetName(name);
        SetData(data);
        SetIndicators(indicators);

        _logger = null;
        if (logger is not null)
            SetLogger(logger);

        CreateModules();
    }
    public SingleTrader SetCallbacks(
        Action<SingleTrader, int>? onReset = null,
        Action<SingleTrader, int>? onInit = null,
        Action<SingleTrader, int>? onRun = null,
        Action<SingleTrader, int>? onFinal = null,
        Action<SingleTrader, int>? onBeforeOrders = null,
        Action<SingleTrader, string, int>? onNotifySignal = null,
        Action<SingleTrader, int>? onAfterOrders = null,
        Action<SingleTrader, int, int, double>? onProgress = null,
        Action<SingleTrader>? onApplyUserFlags = null)
    {
        if (onReset != null) OnReset = onReset;
        if (onInit != null) OnInit = onInit;
        if (onRun != null) OnRun = onRun;
        if (onFinal != null) OnFinal = onFinal;
        if (onBeforeOrders != null) OnBeforeOrder = onBeforeOrders;
        if (onAfterOrders != null) OnAfterOrder = onAfterOrders;
        if (onNotifySignal != null) OnNotifySignal = onNotifySignal;
        if (onProgress != null) OnProgress = onProgress;
        if (onApplyUserFlags != null) OnApplyUserFlags = onApplyUserFlags;

        return this;
    }

    public void Reset()
    {
        OnReset?.Invoke(this, 0);

        // Reset internal modules (state only)
        ResetModules();

        OnReset?.Invoke(this, 1);
    }

    public void Init()
    {
        OnInit?.Invoke(this, 0);

        InitModules();

        OnInit?.Invoke(this, 1);
    }

    public void Run(int barIndex)
    {
        int i = barIndex;

        if (!IsInitialized)
            //throw new InvalidOperationException("Trader not initialized");

        if (i >= Data.Count)
            return;

        OnRun?.Invoke(this, 0);

        {
            OnBeforeOrder?.Invoke(this, barIndex);

            // TODO: Strategy evaluate, sinyal üret, emir uygula

            OnAfterOrder?.Invoke(this, barIndex);
        }

        OnRun?.Invoke(this, 1);

        int totalBars = GetDataCount();
        double percentage = (i + 1) / (double)totalBars * 100.0;
        OnProgress?.Invoke(this, i+1, totalBars, percentage);
    }

    public void Finalize(bool dispose)
    {
        OnFinal?.Invoke(this, dispose ? 1 : 0);
    }
    public SingleTrader CreateModules()
    {
        /*signals = new Signals();
        status = new Status();
        flags = new Flags();
        lists = new Lists();
        timeUtils = new TimeUtils();
        timeUtils.SetTrader(this);
        karZarar = new KarZarar(this);
        karAlZararKes = new KarAlZararKes();
        karAlZararKes.SetTrader(this);
        komisyon = new Komisyon();
        komisyon.SetTrader(this);
        Bakiye = new Bakiye();
        Bakiye.SetTrader(this);
        bakiye = new Bakiye();
        bakiye.SetTrader(this);
        pozisyonBuyuklugu = new PozisyonBuyuklugu();
        Position = new Position();
        statistics = new AlgoTradeWithOptimizationSupportWinFormsApp.Trading.Statistics.Statistics();*/

        initialTradeParams = new InitialTradeParams();

        return this;
    }
    public SingleTrader ResetModules()
    {
        initialTradeParams.Reset();

        return this;
    }
    public SingleTrader InitModules()
    {
        initialTradeParams.Init();

        return this;
    }
    public SingleTrader DeleteModules()
    {
        initialTradeParams = null;

        return this;
    }

    public void Dispose()
    {
        DeleteModules();
    }
}
