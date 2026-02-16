using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Multiple trader - manages and runs multiple SingleTraders in parallel
/// Collects signals from all traders and creates a consensus signal for mainTrader
///
/// Pozisyon Büyüklüğü Modları:
/// - Sabit Lot (DynamicPositionSizeEnabled=false): mainTrader sabit pozisyon büyüklüğü kullanır (varsayılan)
/// - Dinamik Lot (DynamicPositionSizeEnabled=true): Consensus sinyalinden gelen lot büyüklüğü kullanılır
/// </summary>
public class MultipleTrader
{
    #region Properties

    public int Id { get; private set; }
    public List<StockData> Data { get; private set; }
    public IndicatorManager Indicators { get; private set; }
    public LogManager? Logger { get; private set; }

    public List<SingleTrader> Traders { get; private set; }
    public bool IsInitialized { get; private set; }
    public int CurrentIndex { get; private set; }

    // State flags
    public bool IsStarted { get; internal set; }
    public bool IsRunning { get; internal set; }
    public bool IsStopped { get; internal set; }
    public bool IsStopRequested { get; internal set; }

    private SingleTrader _mainTrader;

    /// <summary>
    /// Dinamik pozisyon büyüklüğü desteği
    /// true: Consensus sinyalinden gelen lot büyüklüğü kullanılır (her pozisyon farklı büyüklükte olabilir)
    /// false: mainTrader'ın sabit pozisyon büyüklüğü kullanılır (varsayılan)
    /// </summary>
    public bool DynamicPositionSizeEnabled { get; set; } = false;

    public Action<MultipleTrader, int, int>? OnProgress { get; set; }

    #endregion

    #region Constructor

    public MultipleTrader()
    {
        Traders = new List<SingleTrader>();
        IsInitialized = false;
    }

    /// <summary>
    /// Parametreli constructor - mainTrader ile birlikte oluşturulur
    /// </summary>
    public MultipleTrader(int id, List<StockData> data, IndicatorManager indicators, LogManager? logger)
    {
        Id = id;
        Data = data;
        Indicators = indicators;
        Logger = logger;

        Traders = new List<SingleTrader>();

        // Create mainTrader with ID = -1 to distinguish from other traders
        _mainTrader = new SingleTrader(-1, "mainTrader", data, indicators, logger);

        IsInitialized = true;
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initialize with market data
    /// </summary>
    public void Initialize(List<StockData> data)
    {
        if (data == null || data.Count == 0)
            throw new ArgumentException("Data cannot be null or empty");

        Data = data;
        CurrentIndex = 0;
        IsInitialized = true;
    }

    /// <summary>
    /// Add a trader
    /// </summary>
    public void AddTrader(SingleTrader trader)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("MultipleTrader not initialized");

        Traders.Add(trader);

        // Initialize trader with same data
        if (!trader.IsInitialized)
        {
            trader.SetData(Data);
        }
    }

    /// <summary>
    /// Reset all traders
    /// </summary>
    public void Reset()
    {
        CurrentIndex = 0;
        foreach (var trader in Traders)
        {

        }

        // Reset state flags
        IsStarted = false;
        IsRunning = false;
        IsStopped = false;
        IsStopRequested = false;
    }

    public void Init()
    {
        CurrentIndex = 0;
        foreach (var trader in Traders)
        {
            // trader'ların initleri bağımsız bir sekilde, daha onceden cagriliyor, burada tekrar cagrilmasina gerek yok,
            // cunku her trader kendi initini zaten yapacak, eger burada tekrar cagrilirsa, her trader'in init metodu 2 kere cagrilmis olur, bu da gereksiz ve potansiyel olarak hatalara yol acabilir

            // trader.Init();
        }
    }

    #endregion

    #region Consensus & Run

    /// <summary>
    /// Build consensus signal from all traders (sinyal sayisi bazli - her trader = 1 oy)
    /// </summary>
    public TradeSignals BuildConsensusSignal()
    {
        int buyCount = 0;
        int sellCount = 0;
        int flatCount = 0;

        foreach (var trader in Traders)
        {
            if (trader.is_son_yon_a())
                buyCount++;
            else if (trader.is_son_yon_s())
                sellCount++;
            else if (trader.is_son_yon_f())
                flatCount++;
        }

        int netSignal = buyCount - sellCount;

        if (netSignal > 0)
            return TradeSignals.Buy;
        else if (netSignal < 0)
            return TradeSignals.Sell;
        else
            return TradeSignals.Flat;
    }

    /// <summary>
    /// Run all traders for bar index i, build consensus, execute mainTrader
    /// </summary>
    public void Run(int i)
    {
        int noneSignalCount = 0;
        int alSignalCount = 0;
        int satSignalCount = 0;
        int flatOlSignalCount = 0;
        int passGecSignalCount = 0;
        int karAlSignalCount = 0;
        int zararKesSignalCount = 0;

        //if (!IsInitialized)
        //throw new InvalidOperationException("Trader not initialized");

        if (i >= Data.Count)
            return;

        //_mainTrader.OnRun?.Invoke(_mainTrader, 0);

        // --- Run each child trader ---
        foreach (var trader in Traders)
        {
            trader.Run(i);

            TradeSignals signal = trader.strategySignal;

            if (signal == TradeSignals.None)
            {
                noneSignalCount++;
            }

            if (signal == TradeSignals.Buy)
            {
                alSignalCount++;
            }

            if (signal == TradeSignals.Sell)
            {
                satSignalCount++;
            }

            if (signal == TradeSignals.TakeProfit)
            {
                karAlSignalCount++;
            }

            if (signal == TradeSignals.StopLoss)
            {
                zararKesSignalCount++;
            }

            if (signal == TradeSignals.Flat)
            {
                flatOlSignalCount++;
            }

            if (signal == TradeSignals.Skip)
            {
                passGecSignalCount++;
            }
        }

        // --- Build consensus signal ---
        TradeSignals consensusSignal = BuildConsensusSignal();

        // TODO: DynamicPositionSizeEnabled - lot büyüklüğü güncelleme
        // PozisyonBuyuklugu mevcut projede yok, ileride eklendiğinde burada güncelleme yapılacak

        // --- Execute mainTrader with consensus signal ---
        _mainTrader.ExecutePreOrderMethods(i);

        if (i < 1)
            return;

        _mainTrader.strategySignal = consensusSignal;

        _mainTrader.MapStrategyCommandsToTradeCommands(_mainTrader.strategySignal);

        _mainTrader.ApplyTimingFilters(i);

        _mainTrader.ApplyEquityCurveFilter(i);

        _mainTrader.ResolveFilterDecisions(i);

        _mainTrader.ExecutePostOrderMethods(i);

        //_mainTrader.OnRun?.Invoke(_mainTrader, 1);
    }

    #endregion

    #region Finalize

    public void Finalize(bool saveStatisticsToFile = true)
    {
        CurrentIndex = 0;
        foreach (var trader in Traders)
        {
            trader.Finalize(false);
        }

        if (!IsInitialized)
            throw new InvalidOperationException("Trader not initialized");

        //_mainTrader.OnFinal?.Invoke(_mainTrader, 0);

        _mainTrader.CalculateStatistics();

        // Write MultipleTrader lists to file (both TXT and CSV formats) (TODO)
        //if (saveStatisticsToFile)
            //WriteMultipleTraderListsToFiles();

        if (saveStatisticsToFile)
            _mainTrader.WriteStatisticsToFile(AppSettings.LogsDir);

        //_mainTrader.OnFinal?.Invoke(_mainTrader, 1);
    }

    #endregion

    #region Main Trader Methods

    /// <summary>
    /// Get the main trader that will execute consensus signals (ID = -1)
    /// </summary>
    public SingleTrader GetMainTrader()
    {
        return _mainTrader;
    }

    /// <summary>
    /// Set callbacks for mainTrader and all traders in the list
    /// </summary>
    public void SetCallbacks(
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
        _mainTrader.SetCallbacks(onReset, onInit, onRun, onFinal, onBeforeOrders, onNotifySignal, onAfterOrders, onProgress, onApplyUserFlags);

        foreach (var trader in Traders)
        {
            trader.SetCallbacks(onReset, onInit, onRun, onFinal, onBeforeOrders, onNotifySignal, onAfterOrders, onProgress, onApplyUserFlags);
        }
    }

    /// <summary>
    /// Request stop
    /// </summary>
    public void Stop()
    {
        if (IsRunning)
        {
            IsStopRequested = true;
            LogManager.LogRaw($"Stop requested for MultipleTrader (Id: {Id})");
        }
    }

    /// <summary>
    /// Dispose mainTrader and all traders
    /// </summary>
    public void Dispose()
    {
        _mainTrader?.Dispose();
        _mainTrader = null;

        foreach (var trader in Traders)
        {
            trader?.Dispose();
        }
        Traders.Clear();
    }

    #endregion
}
