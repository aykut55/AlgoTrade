using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Timer;
using AlgoTrade.Core.Trading.Indicators;

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

    // Internal
    private LogManager? _logger;
    private TimeManager? _timer;
    private SingleTrader? singleTrader { get; set; }
    public IndicatorManager? indicators { get; private set; }

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
            singleTrader.LastExecutionTime      = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
            singleTrader.LastExecutionTimeStart = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
            
            // Configure position sizing
            singleTrader.initialTradeParams!.Reset().SetBakiyeParams(ilkBakiye: 100000.0).SetKontratParamsFxParite(lotSayisi: 0.01).SetKomisyonParams(komisyonCarpan: 3.0).SetKaymaParams(kaymaMiktari: 0.5);
            singleTrader.initialTradeParams!.Reset().SetBakiyeParams(ilkBakiye: 100000.0).SetKontratParamsViopEndex(kontratSayisi: 1).SetKomisyonParams(komisyonCarpan: 20.0).SetKaymaParams(kaymaMiktari: 0.5);

            // Init
            OnApplyUserFlags(singleTrader);

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

            // Tarama bilgileri: (Finalize gerek kalmadan alinabilir)
            var yon           = singleTrader.SonYon;                    // "A"
            var kacBarOnce    = singleTrader.SonSinyaldenBeriBarSayisi; // 5
            var karZarar      = singleTrader.SonKarZararFiyat;          // 125.50
            var karZararYuzde = singleTrader.SonKarZararYuzde;          // 0.85
            var ozet          = singleTrader.TaramaOzeti;               // "A | Bar:5 | KZ:125.50 | %:0.85"

            Log($"\nScreening summary... : {ozet}");

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
        singleTrader.IsRunning = false;
        singleTrader.IsStopped = true;
        Log($"\nSingleTrader finished - IsRunning: {singleTrader.IsRunning}, IsStopped: {singleTrader.IsStopped}");

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
