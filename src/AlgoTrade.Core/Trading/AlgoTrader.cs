using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Timer;

namespace AlgoTrade.Core.Trading;

public class AlgoTrader : MarketDataProvider, IDisposable
{
    public string Name { get; }
    public bool IsRunning { get; private set; }
    public new bool IsInitialized { get; private set; }

    private LogManager? _logger;
    private TimeManager? _timer;
    private SingleTrader? singleTrader { get; set; }
    public IndicatorManager? indicators { get; private set; }

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

    private void OnSingleTraderProgress(SingleTrader trader, int currentBar, int totalBars)
    {

    }
    private void OnApplyUserFlags(SingleTrader trader)
    {
        // InitializeUserControlledFlags
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

    public void Reset()
    {
        _data = new();
        IsInitialized = false;
        IsRunning = false;
    }

    public void Initialize()
    {
        if (_data == null || _data.Count == 0)
            throw new ArgumentException("Data cannot be null or empty");
        IsInitialized = true;
    }

    public async Task RunSingleTraderWithProgressAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            //LogError("AlgoTrader not initialized!");
            throw new InvalidOperationException("AlgoTrader not initialized. Call Initialize() first.");
        }

        int totalBars = GetDataCount();

        Log($"AlgoTrader '{Name}' started. Total bars: {totalBars}");
        Log("");

        // *****************************************************************************
        // Indicators - beg
        // *****************************************************************************
        if (indicators != null)
        {
            Log("Disposing previous indicators instance...");
            indicators.Dispose();
            indicators = null;
        }

        indicators = new IndicatorManager(this.Data);
        if (indicators == null)
            return;
        // *****************************************************************************
        // Indicators - end
        // *****************************************************************************

        // *****************************************************************************
        // SingleTrader - beg
        // *****************************************************************************
        if (singleTrader != null)
        {
            Log("Disposing previous singleTrader instance...");
            singleTrader.Dispose();
            singleTrader = null;
        }
        singleTrader = new SingleTrader(0, "singleTraderQuery", this.Data, indicators, _logger);
        if (singleTrader == null) return;

        // Assign callbacks
        singleTrader.SetCallbacks(OnSingleTraderReset, OnSingleTraderInit, OnSingleTraderRun, OnSingleTraderFinal, 
                                  OnSingleTraderBeforeOrder, OnSingleTraderNotifySignal, OnSingleTraderAfterOrder, 
                                  OnSingleTraderProgress, OnApplyUserFlags);

        singleTrader.Reset();

        singleTrader.Init();
        // *****************************************************************************
        // SingleTrader - end
        // *****************************************************************************

        IsRunning = true;
        await Task.Run(() =>
        {
            for (int i = 0; i < totalBars; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bar = _data[i];

                singleTrader.Run(i);

                // TODO: Strategy evaluate here
                // EvaluateStrategy(i, bar);

                double percentage = (i + 1) / (double)totalBars * 100.0;
                OnTraderProgress?.Invoke(i + 1, totalBars, percentage);
            }
        }, cancellationToken);
        IsRunning = false;

        singleTrader.Finalize(false);

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
    }
}
