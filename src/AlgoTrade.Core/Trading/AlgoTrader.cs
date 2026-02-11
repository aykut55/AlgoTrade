using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Timer;
using AlgoTrade.Core.Trading.Indicators;

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

    private void OnSingleTraderProgress(SingleTrader trader, int currentBar, int totalBars, double percentage)
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
        int totalBars = 0;

        if (!IsInitialized) {
            throw new InvalidOperationException("AlgoTrader not initialized. Call Initialize() first.");
        }

        try
        {
            if (_timer != null)
                _timer.RestartTimer("0");

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

            indicators = new IndicatorManager(this.Data);
            if (indicators == null)
                return;


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
                throw new InvalidOperationException("singleTrader not not be created...");

            // Assign callbacks
            singleTrader.SetCallbacks(OnSingleTraderReset, OnSingleTraderInit, OnSingleTraderRun, OnSingleTraderFinal, OnSingleTraderBeforeOrder, OnSingleTraderNotifySignal, OnSingleTraderAfterOrder, OnSingleTraderProgress, OnApplyUserFlags);

            // Reset
            singleTrader.Reset();

            // Configure position sizing
            singleTrader.initialTradeParams.Reset().SetBakiyeParams(ilkBakiye: 100000.0).SetKontratParamsFxParite(lotSayisi: 0.01).SetKomisyonParams(komisyonCarpan: 3.0).SetKaymaParams(kaymaMiktari: 0.5);
            singleTrader.initialTradeParams.Reset().SetBakiyeParams(ilkBakiye: 100000.0).SetKontratParamsViopEndex(kontratSayisi: 1).SetKomisyonParams(komisyonCarpan: 20.0).SetKaymaParams(kaymaMiktari: 0.5);

            // Init
            singleTrader.Init();

            if (_timer != null)
                _timer.RestartTimer("1");

            // *****************************************************************************
            // SingleTrader - Run
            // *****************************************************************************

            Log("\nRunning singleTrader...");

            IsRunning = true;
            await Task.Run(() =>
            {
                for (int i = 0; i < totalBars; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    singleTrader.Run(i);

                    double percentage = (i + 1) / (double)totalBars * 100.0;
                    OnTraderProgress?.Invoke(i + 1, totalBars, percentage);
                }

            }, cancellationToken);
            IsRunning = false;

            Log("\nFinalizing singleTrader...");

            singleTrader.Finalize(false);

            if (_timer != null)
                _timer.StopTimer("1");

            if (_timer != null)
                _timer.StopTimer("0");

            var t0 = _timer!.GetElapsedTime("0");
            var t1 = _timer!.GetElapsedTime("1");

            Log($"\nt0 = {t0} msec. <==> RunSingleTraderWithProgressAsync elapsed time");
            Log($"\nt1 = {t1} msec. <==> Running singleTrader elapsed time");
        }
        catch (Exception ex)
        {
            Log($"An error occurred while running in RunSingleTraderWithProgressAsync(): {ex.Message}");
        }
        finally
        {
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
    }
}
