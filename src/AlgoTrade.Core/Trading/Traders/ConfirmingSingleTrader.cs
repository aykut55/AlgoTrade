using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Strategy;

namespace AlgoTrade.Core.Trading;

/// <summary>
/// Sanal pozisyon beklerken çakışan (ters yönlü) bir ham sinyal geldiğinde ne yapılacağını belirler.
/// </summary>
public enum SignalConflictMode
{
    /// <summary>
    /// Çakışan sinyal, bekleyen sanal pozisyonu iptal edip yeni yönde sıfırdan takip başlatır.
    /// Eski projenin (AlgoTradeWithOptimizationSupport) ConfirmingSingleTrader davranışıyla aynı.
    /// </summary>
    CancelAndRestart = 0,

    /// <summary>
    /// Çakışan sinyal tamamen görmezden gelinir — sanal pozisyon orijinal yönünde, orijinal
    /// giriş fiyatında, kendi eşiğine ulaşana kadar değişmeden beklemeye devam eder.
    /// </summary>
    LockAndIgnore = 1
}

/// <summary>
/// Tek bir stratejinin Al/Sat sinyallerini gerçek pozisyona çevirmeden önce sanal (paper)
/// bir pozisyonla "konfirme" eder. Strateji Al/Sat dediğinde gerçek emir AÇILMAZ — o bar'ın
/// fiyatından sanal bir pozisyon takip edilmeye başlanır. Sanal pozisyonun anlık K/Z'i
/// <see cref="ProfitThreshold"/>/<see cref="LossThreshold"/> eşiğini (<see cref="Trigger"/>'a
/// göre) geçtiği ANDA — orijinal sinyal fiyatından değil, o barın fiyatından — gerçek sinyal
/// <see cref="_mainTrader"/>'a iletilir. Yön hiçbir zaman ters çevrilmez, mekanizma sadece giriş
/// zamanlamasını geciktirir. Konfirmasyondan sonra çıkış tamamen normal trade yönetimine
/// bırakılır — bu katman bir daha filtre uygulamaz.
///
/// SymbolScanner gibi AlgoTrader'dan bağımsız, kendi başına yeten bir sınıf. MultipleTrader'ın
/// (bir sinyal-kaynağı SingleTrader + ayrı bir mainTrader, dışarıdan çözümlenmiş sinyal alır)
/// yapısına benziyor, ama consensus yerine sanal pozisyon + eşik konfirmasyonu var.
///
/// Tasarım tartışması ve eski projeyle karşılaştırma için bkz. docs/todo.md,
/// "Getiri Eğrisi / KarZarar Eğrisi Konfirmasyonu (Madde 3)".
/// </summary>
public class ConfirmingSingleTrader
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

    private SingleTrader _signalTrader;
    private SingleTrader _mainTrader;

    public Action<ConfirmingSingleTrader, int, int>? OnProgress { get; set; }

    public bool SaveStatisticsToFile { get; set; } = true;

    // Output file settings
    public string ConfirmingSingleTraderListsTxtFileName { get; set; } = "ConfirmingSingleTraderLists.txt";
    public string ConfirmingSingleTraderListsCsvFileName { get; set; } = "ConfirmingSingleTraderLists.csv";
    public bool SaveConfirmingSingleTraderListsTxtEnabled { get; set; } = true;
    public bool SaveConfirmingSingleTraderListsCsvEnabled { get; set; } = true;

    // ═══════════════════════════════════════════════════════════════════════
    // CONFIRMATION SETTINGS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>false: Değer bazlı eşik (puan/fiyat farkı), true: Yüzde bazlı.</summary>
    public bool ThresholdIsPercentage { get; set; } = false;

    /// <summary>Sanal pozisyonun konfirme sayılması için ulaşması gereken kâr eşiği (trend teyidi).</summary>
    public double ProfitThreshold { get; set; } = 5000.0;

    /// <summary>Sanal pozisyonun konfirme sayılması için ulaşması gereken zarar eşiği (dip teyidi). NEGATİF girilmeli.</summary>
    public double LossThreshold { get; set; } = -3000.0;

    /// <summary>Hangi eşik(ler) konfirmasyonu tetikleyebilir.</summary>
    public ConfirmationTrigger Trigger { get; set; } = ConfirmationTrigger.Both;

    /// <summary>Sanal pozisyon beklerken çakışan (ters yönlü) bir ham sinyal geldiğinde izlenecek davranış.</summary>
    public SignalConflictMode ConflictMode { get; set; } = SignalConflictMode.CancelAndRestart;

    /// <summary>
    /// true: Flat ham sinyali bekleyen sanal pozisyonu anında iptal eder (eski proje davranışı).
    /// false: Flat görmezden gelinir — sanal pozisyon kendi eşiğine ulaşana kadar beklemeye devam eder.
    /// </summary>
    public bool FlattenImmediatelyOnFlatSignal { get; set; } = true;

    // ═══════════════════════════════════════════════════════════════════════
    // VIRTUAL POSITION STATE
    // ═══════════════════════════════════════════════════════════════════════

    private string? _virtualYon;          // "A" / "S" / null (bekleyen sanal pozisyon yok)
    private double _virtualEntryPrice;
    private bool _confirmed;

    private string[] _virtualYonHistory;
    private bool[] _confirmedHistory;

    /// <summary>Sanal pozisyonun o anki yönü ("A"/"S"), bekleyen sanal pozisyon yoksa null.</summary>
    public string? VirtualYon => _virtualYon;
    public double VirtualEntryPrice => _virtualEntryPrice;
    public bool IsConfirmed => _confirmed;

    // ═══════════════════════════════════════════════════════════════════════
    // PLOTTING — Signals (SingleTrader.lists.SinyalList ile aynı konvansiyon: 1.0=Al, -1.0=Sat, 0.0=Flat)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sinyal-kaynağı trader'ın ham sinyal timeline'ı — strateji konfirmasyon beklemeden
    /// "hemen girseydim" davranışıyla ürettiği sinyal (signalTrader zaten kendi başına tam
    /// çalışan bir trader olduğu için bu doğrudan onun kendi <c>lists.SinyalList</c>'i).
    /// <see cref="Signals"/> ile aynı plota çizdirilip ilk ham sinyalin ne zaman geldiği ile
    /// bizim kurallarımıza göre ne zaman konfirme olup gerçek pozisyona dönüştüğü karşılaştırılabilir.
    /// </summary>
    public List<double> VirtualSignals => _signalTrader.lists.SinyalList;

    /// <summary>mainTrader'ın gerçek/konfirme edilmiş sinyal timeline'ı (1.0=Al, -1.0=Sat, 0.0=Flat).</summary>
    public List<double> Signals => _mainTrader.lists.SinyalList;

    #endregion

    #region Constructor

    public ConfirmingSingleTrader(int id, List<StockData> data, IndicatorManager indicators, LogManager? logger)
    {
        Id = id;
        Data = data;
        Indicators = indicators;
        Logger = logger;

        _signalTrader = new SingleTrader(id, "signalTrader", data, indicators, logger)
        {
            RunMode = TraderRunMode.TradeOnly
        };

        // mainTrader'a MultipleTrader konvansiyonuyla tutarlı olarak Id = -1 veriliyor
        _mainTrader = new SingleTrader(-1, "mainTrader", data, indicators, logger)
        {
            RunMode = TraderRunMode.TradeOnly
        };

        IsInitialized = true;
    }

    #endregion

    #region Setup

    /// <summary>Stratejiyi sinyal-kaynağı trader'a bağlar — mainTrader kendi stratejisini hiç çalıştırmaz, sadece konfirme edilmiş sinyalleri alır.</summary>
    public void SetStrategy(IStrategy strategy)
    {
        _signalTrader.SetStrategy(strategy);
    }

    #endregion

    #region Lifecycle

    public void Reset()
    {
        _signalTrader.Reset();
        _mainTrader.Reset();

        _virtualYon = null;
        _virtualEntryPrice = 0.0;
        _confirmed = false;

        IsStarted = false;
        IsRunning = false;
        IsStopped = false;
        IsStopRequested = false;
    }

    public void Init()
    {
        _signalTrader.Init();
        _mainTrader.Init();

        _virtualYonHistory = new string[Data.Count];
        _confirmedHistory = new bool[Data.Count];
    }

    #endregion

    #region Confirmation & Run

    /// <summary>
    /// Sanal pozisyon state'ini günceller ve bu bar'da mainTrader'a gönderilecek sinyali döner.
    /// </summary>
    private TradeSignals ResolveConfirmedSignal(int i)
    {
        string currentYon = _signalTrader.SonYon;              // "A" / "S" / "F"
        TradeSignals rawSignal = _signalTrader.strategySignal;
        double currentPrice = Data[i].Close;

        // ── Zaten konfirme: bu katman artık devrede değil, ham sinyal olduğu gibi geçer ──
        if (_confirmed)
        {
            if (currentYon == "F")
            {
                _confirmed = false;
                _virtualYon = null;
            }

            return rawSignal;
        }

        // ── Henüz konfirme değil ──
        if (currentYon == "F")
        {
            if (_virtualYon != null && !FlattenImmediatelyOnFlatSignal)
            {
                // Flat görmezden gelinir - sanal pozisyon değişmeden bekler
                return TradeSignals.None;
            }

            _virtualYon = null;
            return TradeSignals.None;
        }

        // currentYon == "A" veya "S"
        if (_virtualYon == null)
        {
            // Yeni sanal pozisyon başlat
            _virtualYon = currentYon;
            _virtualEntryPrice = currentPrice;
            return TradeSignals.None;
        }

        if (_virtualYon != currentYon)
        {
            // Çakışan yön değişikliği (Al<->Sat)
            if (ConflictMode == SignalConflictMode.CancelAndRestart)
            {
                _virtualYon = currentYon;
                _virtualEntryPrice = currentPrice;
            }
            // LockAndIgnore: hiçbir şey değişmez, sanal pozisyon orijinal yönünde beklemeye devam eder

            return TradeSignals.None;
        }

        // Aynı yönde bekliyor - eşik kontrolü
        if (IsThresholdReached(_virtualYon, _virtualEntryPrice, currentPrice))
        {
            _confirmed = true;

            // rawSignal DEĞİL — sanal pozisyon genelde birkaç bar önce (ilk sinyal geldiğinde)
            // açıldığı için signalTrader'ın konfirme anındaki ham komutu artık "None" (strateji
            // yeni bir emir tekrar yayınlamıyor). Gerçek girişi biz burada, sanal pozisyonun
            // yönüne göre kendimiz üretiyoruz — konfirme anının kendisi giriş komutudur.
            return _virtualYon == "A" ? TradeSignals.Buy : TradeSignals.Sell;
        }

        return TradeSignals.None;
    }

    private bool IsThresholdReached(string yon, double entryPrice, double currentPrice)
    {
        double karZararFiyat = yon == "A"
            ? currentPrice - entryPrice
            : entryPrice - currentPrice;

        double karZararYuzde = entryPrice != 0.0
            ? 100.0 * karZararFiyat / entryPrice
            : 0.0;

        double karZarar = ThresholdIsPercentage ? karZararYuzde : karZararFiyat;

        bool karTetiklendi = karZarar >= ProfitThreshold;
        bool zararTetiklendi = karZarar <= LossThreshold;

        return Trigger switch
        {
            ConfirmationTrigger.ProfitOnly => karTetiklendi,
            ConfirmationTrigger.LossOnly => zararTetiklendi,
            _ => karTetiklendi || zararTetiklendi
        };
    }

    public void Run(int i)
    {
        if (i >= Data.Count)
            return;

        _signalTrader.Run(i);

        _mainTrader.ExecutePreOrderMethods(i);

        if (i < 1)
            return;

        TradeSignals signalForMainTrader = ResolveConfirmedSignal(i);

        _virtualYonHistory[i] = _virtualYon ?? "-";
        _confirmedHistory[i] = _confirmed;

        _mainTrader.strategySignal = signalForMainTrader;

        _mainTrader.MapStrategyCommandsToTradeCommands(_mainTrader.strategySignal);

        _mainTrader.ApplyTimingFilters(i);

        _mainTrader.ApplyEquityCurveFilter(i);

        _mainTrader.ResolveFilterDecisions(i);

        _mainTrader.ExecutePostOrderMethods(i);

        int totalBars = Data.Count;
        double percentage = (i + 1) / (double)totalBars * 100.0;
        OnProgress?.Invoke(this, i + 1, totalBars);
        _ = percentage;
    }

    #endregion

    #region Finalize

#pragma warning disable CS0465
    public void Finalize()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("ConfirmingSingleTrader not initialized");

        _signalTrader.Finalize();

        LogManager.LogRaw($"\nCalculating statistics...");

        _mainTrader.CalculateStatistics();

        LogManager.LogRaw($"\nCalculating performances...");

        _mainTrader.GetPerformansParams(out double bakiyePuan, out double lotSayisi, out double varlikAdedCarpani);
        _mainTrader.CalculatePerformances(bakiyePuan, lotSayisi, varlikAdedCarpani);

        if (SaveStatisticsToFile)
            WriteConfirmingSingleTraderListsToFiles();
    }
#pragma warning restore CS0465

    #endregion

    #region Main/Signal Trader Access

    public SingleTrader GetMainTrader() => _mainTrader;
    public SingleTrader GetSignalTrader() => _signalTrader;

    /// <summary>mainTrader ve signalTrader için aynı callback setini bağlar.</summary>
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
        _signalTrader.SetCallbacks(onReset, onInit, onRun, onFinal, onBeforeOrders, onNotifySignal, onAfterOrders, onProgress, onApplyUserFlags);
    }

    public void Stop()
    {
        if (IsRunning)
        {
            IsStopRequested = true;
            LogManager.LogRaw($"Stop requested for ConfirmingSingleTrader (Id: {Id})");
        }
    }

    #endregion

    #region Lists Export

    private void WriteConfirmingSingleTraderListsToFiles()
    {
        if (SaveConfirmingSingleTraderListsTxtEnabled)
            WriteConfirmingSingleTraderListsToTxt();

        if (SaveConfirmingSingleTraderListsCsvEnabled)
            WriteConfirmingSingleTraderListsToCsv();
    }

    private void WriteConfirmingSingleTraderListsToTxt()
    {
        if (_mainTrader == null || Data == null || Data.Count == 0)
            return;

        var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!System.IO.Directory.Exists(logDir))
            System.IO.Directory.CreateDirectory(logDir);

        var filePath = System.IO.Path.Combine(logDir, ConfirmingSingleTraderListsTxtFileName);

        using (var writer = new System.IO.StreamWriter(filePath, append: false, System.Text.Encoding.UTF8))
        {
            writer.WriteLine($"CONFIRMING SINGLE TRADER BAR-BY-BAR DATA");
            writer.WriteLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
            writer.WriteLine($"ThresholdIsPercentage: {ThresholdIsPercentage}");
            writer.WriteLine($"ProfitThreshold: {ProfitThreshold}, LossThreshold: {LossThreshold}, Trigger: {Trigger}");
            writer.WriteLine($"ConflictMode: {ConflictMode}, FlattenImmediatelyOnFlatSignal: {FlattenImmediatelyOnFlatSignal}");
            writer.WriteLine("".PadRight(300, '='));

            WriteHeaderTxt(writer);

            for (int i = 0; i < Data.Count; i++)
            {
                WriteBarDataTxt(writer, i);
            }

            writer.WriteLine("".PadRight(300, '='));
        }

        Logger?.LogRawInstance($"ConfirmingSingleTraderLists.txt written to: {filePath}");
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

    private void WriteBarDataTxt(System.IO.StreamWriter writer, int barIndex)
    {
        var bar = Data[barIndex];

        var line = $"{barIndex,7} | " +
                  $"{bar.Date:yyyy.MM.dd} | " +
                  $"{bar.DateTime:HH:mm:ss} | " +
                  $"{bar.Close,10:F2} | " +
                  $"{GetYon(_signalTrader, barIndex),6} | {GetSeviye(_signalTrader, barIndex),10:F2} | {GetSinyal(_signalTrader, barIndex),6:F2} | " +
                  $"{GetVirtualYon(barIndex),6} | {GetConfirmed(barIndex),6} | " +
                  $"{GetYon(_mainTrader, barIndex),7} | {GetSeviye(_mainTrader, barIndex),10:F2} | {GetSinyal(_mainTrader, barIndex),7:F2}";

        writer.WriteLine(line);
    }

    private void WriteConfirmingSingleTraderListsToCsv()
    {
        if (_mainTrader == null || Data == null || Data.Count == 0)
            return;

        var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!System.IO.Directory.Exists(logDir))
            System.IO.Directory.CreateDirectory(logDir);

        var filePath = System.IO.Path.Combine(logDir, ConfirmingSingleTraderListsCsvFileName);

        using (var writer = new System.IO.StreamWriter(filePath, append: false, System.Text.Encoding.UTF8))
        {
            WriteHeaderCsv(writer);

            for (int i = 0; i < Data.Count; i++)
            {
                WriteBarDataCsv(writer, i);
            }
        }

        Logger?.LogRawInstance($"ConfirmingSingleTraderLists.csv written to: {filePath}");
    }

    private void WriteHeaderCsv(System.IO.StreamWriter writer)
    {
        var header = "BarNo;Date;Time;Close;" +
                     "SignalTrader_Yon;SignalTrader_Seviye;SignalTrader_Sinyal;" +
                     "Virtual_Yon;Virtual_Confirmed;" +
                     "MainTrader_Yon;MainTrader_Seviye;MainTrader_Sinyal";

        writer.WriteLine(header);
    }

    private void WriteBarDataCsv(System.IO.StreamWriter writer, int barIndex)
    {
        var bar = Data[barIndex];

        var line = $"{barIndex};" +
                  $"{bar.Date:yyyy.MM.dd};" +
                  $"{bar.DateTime:HH:mm:ss};" +
                  $"{bar.Close:F2};" +
                  $"{GetYon(_signalTrader, barIndex)};{GetSeviye(_signalTrader, barIndex):F2};{GetSinyal(_signalTrader, barIndex):F2};" +
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
        _signalTrader?.Dispose();
        _signalTrader = null;

        _mainTrader?.Dispose();
        _mainTrader = null;
    }

    #endregion
}
