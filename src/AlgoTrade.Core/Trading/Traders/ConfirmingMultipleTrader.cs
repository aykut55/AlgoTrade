using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// <see cref="ConfirmingSingleTrader"/>'ın MultipleTrader karşılığı — tek bir stratejinin ham
/// sinyali yerine N child stratejinin consensus (bileşke) sinyalini sanal pozisyonla konfirme
/// eder. Strateji/consensus "AL" dediğinde gerçek emir AÇILMAZ — o bar'ın fiyatından sanal bir
/// pozisyon takip edilmeye başlanır; eşik (<see cref="ProfitThreshold"/>/<see cref="LossThreshold"/>)
/// geçildiği ANDA gerçek sinyal <see cref="_mainTrader"/>'a iletilir.
///
/// Mimari — composition, ConfirmingSingleTrader'ın "signalTrader = tam çalışan bağımsız trader"
/// deseninin MultipleTrader karşılığı: <see cref="_signalMultipleTrader"/> tam, bağımsız çalışan
/// gerçek bir <see cref="MultipleTrader"/> (N child + kendi consensus mantığı, hiç değiştirilmeden
/// reuse ediliyor) — onun kendi mainTrader'ı bizim "ham sinyal kaynağımız". Konfirmasyon state
/// machine'i (<see cref="VirtualPositionConfirmer"/>) ConfirmingSingleTrader ile ORTAK — kod
/// tekrarı yok, aynı sınıf.
///
/// MultipleTrader'ın kendi lifecycle konvansiyonuna uyulur: <c>MultipleTrader.Reset()/Init()</c>
/// kendi mainTrader'ını/child'larını YÖNETMEZ (no-op'a yakın) — çağıran taraf (burada bu sınıf)
/// signalMultipleTrader'ın mainTrader'ını ve her child'ı (AddTrader'dan ÖNCE) kendisi
/// Reset/Init etmekle yükümlü, tıpkı AlgoTrader.createChildTraders()'ın yaptığı gibi.
///
/// Tasarım tartışması için bkz. docs/todo.md, "Getiri Eğrisi / KarZarar Eğrisi Konfirmasyonu (Madde 3)".
/// </summary>
public class ConfirmingMultipleTrader
{
    #region Properties

    public int Id { get; private set; }
    public List<StockData> Data { get; private set; }
    public IndicatorManager Indicators { get; private set; }
    public LogManager? Logger { get; private set; }

    public bool IsInitialized { get; private set; }

    // State flags
    public bool IsStarted { get; set; }
    public bool IsRunning { get; set; }
    public bool IsStopped { get; set; }
    public bool IsStopRequested { get; set; }

    private MultipleTrader _signalMultipleTrader;
    private SingleTrader _mainTrader;

    public Action<ConfirmingMultipleTrader, int, int>? OnProgress { get; set; }

    public bool SaveStatisticsToFile { get; set; } = true;

    // Output file settings (3-yönlü: signal-consensus / sanal / mainTrader)
    public string ConfirmingMultipleTraderListsTxtFileName { get; set; } = "ConfirmingMultipleTraderLists.txt";
    public string ConfirmingMultipleTraderListsCsvFileName { get; set; } = "ConfirmingMultipleTraderLists.csv";
    public bool SaveConfirmingMultipleTraderListsTxtEnabled { get; set; } = true;
    public bool SaveConfirmingMultipleTraderListsCsvEnabled { get; set; } = true;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSENSUS — signalMultipleTrader'a pass-through
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Net | Majority | All | Any — bkz. MultipleTrader.BuildConsensusSignal().</summary>
    public string ConsensusMode { get => _signalMultipleTrader.ConsensusMode; set => _signalMultipleTrader.ConsensusMode = value; }
    public int ConsensusMinNetCount { get => _signalMultipleTrader.ConsensusMinNetCount; set => _signalMultipleTrader.ConsensusMinNetCount = value; }

    // ═══════════════════════════════════════════════════════════════════════
    // CONFIRMATION — paylaşılan state machine, bkz. VirtualPositionConfirmer.cs
    // (ConfirmingSingleTrader ile ortak)
    // ═══════════════════════════════════════════════════════════════════════

    private readonly VirtualPositionConfirmer _confirmer = new();

    public bool ThresholdIsPercentage { get => _confirmer.ThresholdIsPercentage; set => _confirmer.ThresholdIsPercentage = value; }
    public double ProfitThreshold { get => _confirmer.ProfitThreshold; set => _confirmer.ProfitThreshold = value; }
    public double LossThreshold { get => _confirmer.LossThreshold; set => _confirmer.LossThreshold = value; }
    public ConfirmationTrigger Trigger { get => _confirmer.Trigger; set => _confirmer.Trigger = value; }
    public SignalConflictMode ConflictMode { get => _confirmer.ConflictMode; set => _confirmer.ConflictMode = value; }
    public bool FlattenImmediatelyOnFlatSignal { get => _confirmer.FlattenImmediatelyOnFlatSignal; set => _confirmer.FlattenImmediatelyOnFlatSignal = value; }

    // ═══════════════════════════════════════════════════════════════════════
    // VIRTUAL POSITION STATE (diagnostic)
    // ═══════════════════════════════════════════════════════════════════════

    private string[] _virtualYonHistory;
    private bool[] _confirmedHistory;

    public string? VirtualYon => _confirmer.VirtualYon;
    public double VirtualEntryPrice => _confirmer.VirtualEntryPrice;
    public bool IsConfirmed => _confirmer.IsConfirmed;

    // ═══════════════════════════════════════════════════════════════════════
    // PLOTTING — Signals (SingleTrader.lists.SinyalList ile aynı konvansiyon)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Consensus'un ham sinyal timeline'ı — signalMultipleTrader'ın kendi mainTrader'ının <c>lists.SinyalList</c>'i.</summary>
    public List<double> VirtualSignals => _signalMultipleTrader.GetMainTrader().lists.SinyalList;

    /// <summary>mainTrader'ın gerçek/konfirme edilmiş sinyal timeline'ı.</summary>
    public List<double> Signals => _mainTrader.lists.SinyalList;

    #endregion

    #region Constructor

    public ConfirmingMultipleTrader(int id, List<StockData> data, IndicatorManager indicators, LogManager? logger)
    {
        Id = id;
        Data = data;
        Indicators = indicators;
        Logger = logger;

        _signalMultipleTrader = new MultipleTrader(id, data, indicators, logger);

        // mainTrader'a MultipleTrader konvansiyonuyla tutarlı olarak Id = -1 veriliyor
        _mainTrader = new SingleTrader(-1, "mainTrader", data, indicators, logger)
        {
            RunMode = TraderRunMode.TradeOnly
        };

        IsInitialized = true;
    }

    #endregion

    #region Setup

    /// <summary>
    /// Sinyal katmanına (consensus üreten signalMultipleTrader'a) bir child trader ekler.
    /// MultipleTrader.AddTrader ile aynı sözleşme: child, çağrılmadan ÖNCE tamamen
    /// Reset/configure/Init edilmiş olmalı (bkz. AlgoTrader.createChildTraders()).
    /// </summary>
    public void AddTrader(SingleTrader trader)
    {
        _signalMultipleTrader.AddTrader(trader);
    }

    #endregion

    #region Lifecycle

    public void Reset()
    {
        _signalMultipleTrader.Reset();
        _signalMultipleTrader.GetMainTrader().Reset();   // MultipleTrader.Reset() kendi mainTrader'ını resetlemiyor
        _mainTrader.Reset();

        _confirmer.Reset();

        IsStarted = false;
        IsRunning = false;
        IsStopped = false;
        IsStopRequested = false;
    }

    public void Init()
    {
        var signalMain = _signalMultipleTrader.GetMainTrader();
        signalMain.RunMode = TraderRunMode.TradeOnly;
        signalMain.Init();   // MultipleTrader.Init() kendi mainTrader'ını init etmiyor

        // KRİTİK: SingleTrader.signals.AlEnabled/SatEnabled/FlatOlEnabled varsayılan olarak FALSE
        // (ConfigureUserFlagsOnce()/Signals.Reset() ile) — normal MultipleTrader akışında bunlar
        // ApplySingleTraderFlagsConfigs(mainTrader) ile AppConfig'den açılıyor, ama burada
        // signalMain'i biz kendimiz kuruyoruz, o çağrı hiç yapılmıyor. Açılmazsa
        // MapStrategyCommandsToTradeCommands() consensus Buy/Sell'i sessizce yok sayar (signals.Al/
        // Sat hiç true olmaz) — signalMain SonYon'u sonsuza kadar "F" kalır, konfirmasyon hiç
        // tetiklenmez. (Gerçek veride bulunmuş bir hata — bkz. docs/todo.md.)
        signalMain.signals.AlEnabled = true;
        signalMain.signals.SatEnabled = true;
        signalMain.signals.FlatOlEnabled = true;

        _signalMultipleTrader.Init();
        _mainTrader.Init();

        _virtualYonHistory = new string[Data.Count];
        _confirmedHistory = new bool[Data.Count];
    }

    #endregion

    #region Confirmation & Run

    /// <summary>
    /// Sanal pozisyon state'ini günceller ve bu bar'da mainTrader'a gönderilecek sinyali döner.
    /// Asıl mantık paylaşımlı <see cref="VirtualPositionConfirmer"/>'da.
    /// </summary>
    private TradeSignals ResolveConfirmedSignal(int i)
    {
        var signalMain = _signalMultipleTrader.GetMainTrader();
        string currentYon = signalMain.SonYon;              // "A" / "S" / "F"
        TradeSignals rawSignal = signalMain.strategySignal; // consensus'un o barki komutu
        double currentPrice = Data[i].Close;

        return _confirmer.Resolve(currentYon, rawSignal, currentPrice);
    }

    public void Run(int i)
    {
        if (i >= Data.Count)
            return;

        _signalMultipleTrader.Run(i);

        _mainTrader.ExecutePreOrderMethods(i);

        if (i < 1)
            return;

        TradeSignals signalForMainTrader = ResolveConfirmedSignal(i);

        _virtualYonHistory[i] = _confirmer.VirtualYon ?? "-";
        _confirmedHistory[i] = _confirmer.IsConfirmed;

        _mainTrader.strategySignal = signalForMainTrader;

        _mainTrader.MapStrategyCommandsToTradeCommands(_mainTrader.strategySignal);

        _mainTrader.ApplyTimingFilters(i);

        _mainTrader.ApplyEquityCurveFilter(i);

        _mainTrader.ResolveFilterDecisions(i);

        _mainTrader.ExecutePostOrderMethods(i);

        int totalBars = Data.Count;
        OnProgress?.Invoke(this, i + 1, totalBars);
    }

    #endregion

    #region Finalize

#pragma warning disable CS0465
    public void Finalize()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("ConfirmingMultipleTrader not initialized");

        _signalMultipleTrader.Finalize();   // child'ları finalize eder + kendi mainTrader'ının istatistiklerini hesaplar

        LogManager.LogRaw($"\nCalculating statistics...");

        _mainTrader.CalculateStatistics();

        LogManager.LogRaw($"\nCalculating performances...");

        _mainTrader.GetPerformansParams(out double bakiyePuan, out double lotSayisi, out double varlikAdedCarpani);
        _mainTrader.CalculatePerformances(bakiyePuan, lotSayisi, varlikAdedCarpani);

        if (SaveStatisticsToFile)
            WriteConfirmingMultipleTraderListsToFiles();
    }
#pragma warning restore CS0465

    #endregion

    #region Main/Signal Trader Access

    public SingleTrader GetMainTrader() => _mainTrader;

    /// <summary>
    /// Sinyal katmanına doğrudan erişim — child'ların kendi verisi, consensus ayarları, ve
    /// (istenirse) signalMultipleTrader'ın kendi composite lists dosyalarını yazmak için.
    /// </summary>
    public MultipleTrader GetSignalMultipleTrader() => _signalMultipleTrader;

    /// <summary>mainTrader ve signal katmanı (consensus mainTrader + tüm child'lar) için aynı callback setini bağlar.</summary>
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
        _signalMultipleTrader.SetCallbacks(onReset, onInit, onRun, onFinal, onBeforeOrders, onNotifySignal, onAfterOrders, onProgress, onApplyUserFlags);
    }

    public void Stop()
    {
        if (IsRunning)
        {
            IsStopRequested = true;
            LogManager.LogRaw($"Stop requested for ConfirmingMultipleTrader (Id: {Id})");
        }
    }

    #endregion

    #region Lists Export

    private void WriteConfirmingMultipleTraderListsToFiles()
    {
        if (SaveConfirmingMultipleTraderListsTxtEnabled)
            WriteConfirmingMultipleTraderListsToTxt();

        if (SaveConfirmingMultipleTraderListsCsvEnabled)
            WriteConfirmingMultipleTraderListsToCsv();
    }

    private void WriteConfirmingMultipleTraderListsToTxt()
    {
        if (_mainTrader == null || Data == null || Data.Count == 0)
            return;

        var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!System.IO.Directory.Exists(logDir))
            System.IO.Directory.CreateDirectory(logDir);

        var filePath = System.IO.Path.Combine(logDir, ConfirmingMultipleTraderListsTxtFileName);
        var signalMain = _signalMultipleTrader.GetMainTrader();

        using (var writer = new System.IO.StreamWriter(filePath, append: false, System.Text.Encoding.UTF8))
        {
            writer.WriteLine($"CONFIRMING MULTIPLE TRADER BAR-BY-BAR DATA");
            writer.WriteLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
            writer.WriteLine($"ConsensusMode: {ConsensusMode}, ConsensusMinNetCount: {ConsensusMinNetCount}, ChildCount: {_signalMultipleTrader.Traders.Count}");
            writer.WriteLine($"ThresholdIsPercentage: {ThresholdIsPercentage}");
            writer.WriteLine($"ProfitThreshold: {ProfitThreshold}, LossThreshold: {LossThreshold}, Trigger: {Trigger}");
            writer.WriteLine($"ConflictMode: {ConflictMode}, FlattenImmediatelyOnFlatSignal: {FlattenImmediatelyOnFlatSignal}");
            writer.WriteLine("".PadRight(300, '='));

            WriteHeaderTxt(writer);

            for (int i = 0; i < Data.Count; i++)
            {
                WriteBarDataTxt(writer, i, signalMain);
            }

            writer.WriteLine("".PadRight(300, '='));
        }

        Logger?.LogRawInstance($"ConfirmingMultipleTraderLists.txt written to: {filePath}");
    }

    private void WriteHeaderTxt(System.IO.StreamWriter writer)
    {
        var header = $"{"BarNo",7} | " +
                    $"{"Date",10} | " +
                    $"{"Time",8} | " +
                    $"{"Close",10} | " +
                    $"{"SigYon",6} | {"SigSvy",10} | {"SigSny",6} | " +
                    $"{"VirYon",6} | {"Confrm",6} | " +
                    $"{"MainYon",7} | {"MainSvy",10} | {"MainSny",7}";

        writer.WriteLine(header);
    }

    private void WriteBarDataTxt(System.IO.StreamWriter writer, int barIndex, SingleTrader signalMain)
    {
        var bar = Data[barIndex];

        var line = $"{barIndex,7} | " +
                  $"{bar.Date:yyyy.MM.dd} | " +
                  $"{bar.DateTime:HH:mm:ss} | " +
                  $"{bar.Close,10:F2} | " +
                  $"{GetYon(signalMain, barIndex),6} | {GetSeviye(signalMain, barIndex),10:F2} | {GetSinyal(signalMain, barIndex),6:F2} | " +
                  $"{GetVirtualYon(barIndex),6} | {GetConfirmed(barIndex),6} | " +
                  $"{GetYon(_mainTrader, barIndex),7} | {GetSeviye(_mainTrader, barIndex),10:F2} | {GetSinyal(_mainTrader, barIndex),7:F2}";

        writer.WriteLine(line);
    }

    private void WriteConfirmingMultipleTraderListsToCsv()
    {
        if (_mainTrader == null || Data == null || Data.Count == 0)
            return;

        var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!System.IO.Directory.Exists(logDir))
            System.IO.Directory.CreateDirectory(logDir);

        var filePath = System.IO.Path.Combine(logDir, ConfirmingMultipleTraderListsCsvFileName);
        var signalMain = _signalMultipleTrader.GetMainTrader();

        using (var writer = new System.IO.StreamWriter(filePath, append: false, System.Text.Encoding.UTF8))
        {
            WriteHeaderCsv(writer);

            for (int i = 0; i < Data.Count; i++)
            {
                WriteBarDataCsv(writer, i, signalMain);
            }
        }

        Logger?.LogRawInstance($"ConfirmingMultipleTraderLists.csv written to: {filePath}");
    }

    private void WriteHeaderCsv(System.IO.StreamWriter writer)
    {
        var header = "BarNo;Date;Time;Close;" +
                     "SignalConsensus_Yon;SignalConsensus_Seviye;SignalConsensus_Sinyal;" +
                     "Virtual_Yon;Virtual_Confirmed;" +
                     "MainTrader_Yon;MainTrader_Seviye;MainTrader_Sinyal";

        writer.WriteLine(header);
    }

    private void WriteBarDataCsv(System.IO.StreamWriter writer, int barIndex, SingleTrader signalMain)
    {
        var bar = Data[barIndex];

        var line = $"{barIndex};" +
                  $"{bar.Date:yyyy.MM.dd};" +
                  $"{bar.DateTime:HH:mm:ss};" +
                  $"{bar.Close:F2};" +
                  $"{GetYon(signalMain, barIndex)};{GetSeviye(signalMain, barIndex):F2};{GetSinyal(signalMain, barIndex):F2};" +
                  $"{GetVirtualYon(barIndex)};{GetConfirmed(barIndex)};" +
                  $"{GetYon(_mainTrader, barIndex)};{GetSeviye(_mainTrader, barIndex):F2};{GetSinyal(_mainTrader, barIndex):F2}";

        writer.WriteLine(line);
    }

    private string GetVirtualYon(int barIndex)
    {
        if (_virtualYonHistory == null || barIndex < 0 || barIndex >= _virtualYonHistory.Length)
            return "";

        return _virtualYonHistory[barIndex] ?? "";
    }

    private string GetConfirmed(int barIndex)
    {
        if (_confirmedHistory == null || barIndex < 0 || barIndex >= _confirmedHistory.Length)
            return "";

        return _confirmedHistory[barIndex] ? "1" : "0";
    }

    private string GetYon(SingleTrader trader, int barIndex)
    {
        if (trader == null || trader.lists == null || trader.lists.YonList == null)
            return "";

        if (barIndex < 0 || barIndex >= trader.lists.YonList.Count)
            return "";

        return trader.lists.YonList[barIndex] ?? "";
    }

    private double GetSeviye(SingleTrader trader, int barIndex)
    {
        if (trader == null || trader.lists == null || trader.lists.SeviyeList == null)
            return 0.0;

        if (barIndex < 0 || barIndex >= trader.lists.SeviyeList.Count)
            return 0.0;

        return trader.lists.SeviyeList[barIndex];
    }

    private double GetSinyal(SingleTrader trader, int barIndex)
    {
        if (trader == null || trader.lists == null || trader.lists.SinyalList == null)
            return 0.0;

        if (barIndex < 0 || barIndex >= trader.lists.SinyalList.Count)
            return 0.0;

        return trader.lists.SinyalList[barIndex];
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        _signalMultipleTrader?.Dispose();
        _signalMultipleTrader = null;

        _mainTrader?.Dispose();
        _mainTrader = null;
    }

    #endregion
}
