using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Utils;

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
    public bool IsStarted { get; set; }
    public bool IsRunning { get; set; }
    public bool IsStopped { get; set; }
    public bool IsStopRequested { get; set; }

    private SingleTrader _mainTrader;

    /// <summary>
    /// Dinamik pozisyon büyüklüğü desteği
    /// true: Consensus sinyalinden gelen lot büyüklüğü kullanılır (her pozisyon farklı büyüklükte olabilir)
    /// false: mainTrader'ın sabit pozisyon büyüklüğü kullanılır (varsayılan)
    /// </summary>
    public bool DynamicPositionSizeEnabled { get; set; } = false;

    /// <summary>
    /// BuildConsensusSignal() modu: Net | Majority | All | Any (varsayılan: Net).
    /// AppConfig.json → MultipleTrader.Consensus.Mode üzerinden set edilir
    /// (bkz. AlgoTrader.SetMultipleTraderConsensusConfig).
    /// </summary>
    public string ConsensusMode { get; set; } = "Net";

    /// <summary>Net modunda minimum net oy farkı eşiği (varsayılan: 1).</summary>
    public int ConsensusMinNetCount { get; set; } = 1;

    /// <summary>
    /// Script'ten (veya kod'dan) atanabilen özel consensus kuralı. Doluysa
    /// <see cref="BuildConsensusSignal"/> hardcoded Net/Majority/All/Any switch'ini atlar ve
    /// doğrudan bu fonksiyonun döndürdüğü sinyali kullanır (migration-guide.md Madde 5.3 —
    /// "MultiTrader için Scripting"). AppConfig.json'dan set edilemez (Func serialize edilemez);
    /// sadece [8] Run Script üzerinden `algoTrader.MultipleTrader.CustomConsensusFunc = ...`
    /// şeklinde atanması beklenir. Örnek: inputs/scripts/CustomConsensusExample.csx.
    /// </summary>
    public Func<List<SingleTrader>, TradeSignals>? CustomConsensusFunc { get; set; }

    public Action<MultipleTrader, int, int>? OnProgress { get; set; }

    public bool SaveStatisticsToFile { get; set; } = true;
    public bool WriteChildTradersDataToFiles { get; set; } = false;

    // Output file settings
    public string MultipleTraderListsTxtFileName { get; set; } = "MultipleTraderLists.txt";
    public string MultipleTraderListsCsvFileName { get; set; } = "MultipleTraderLists.csv";
    public bool SaveMultipleTraderListsTxtEnabled { get; set; } = true;
    public bool SaveMultipleTraderListsCsvEnabled { get; set; } = true;

    // TODO: "MultipleTraderStatistics" ismi yerine daha aciklayici bir isim (orn.
    // "MultipleTraderPerformanceSummary") tercih edilebilir - kullanici ile konusulup karar verilecek.
    public string MultipleTraderStatisticsTxtFileName { get; set; } = "MultipleTraderStatistics.txt";
    public string MultipleTraderStatisticsCsvFileName { get; set; } = "MultipleTraderStatistics.csv";
    public bool SaveMultipleTraderStatisticsTxtEnabled { get; set; } = true;
    public bool SaveMultipleTraderStatisticsCsvEnabled { get; set; } = true;

    // Grid versiyonlar: satir=trader/kolon=metrik yerine kolon=trader (Main + her child kendi
    // dikey grid raporuyla yan yana) - bkz. WriteMultipleTraderStatistics.
    public string MultipleTraderStatisticsGridTxtFileName { get; set; } = "MultipleTraderStatisticsGrid.txt";
    public string MultipleTraderStatisticsMinimalGridTxtFileName { get; set; } = "MultipleTraderStatisticsMinimalGrid.txt";
    public bool SaveMultipleTraderStatisticsGridTxtEnabled { get; set; } = true;
    public bool SaveMultipleTraderStatisticsMinimalGridTxtEnabled { get; set; } = true;

    public bool PlotEnabled { get; set; } = false;

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
    /// Build consensus signal from all traders (sinyal sayisi bazli - her trader = 1 oy).
    ///
    /// Mod, <see cref="ConsensusMode"/> (AppConfig.json → MultipleTrader.Consensus.Mode) ile
    /// belirlenir:
    ///
    ///   Net (varsayılan)
    ///     buyCount - sellCount &gt;= ConsensusMinNetCount  → Buy
    ///     buyCount - sellCount &lt;= -ConsensusMinNetCount → Sell
    ///     aksi                                            → Flat
    ///
    ///   Majority
    ///     buyCount  &gt; Traders.Count/2 → Buy
    ///     sellCount &gt; Traders.Count/2 → Sell
    ///     aksi                        → Flat
    ///
    ///   All
    ///     tüm child'lar aynı yönde → o yön
    ///     aksi                    → Flat
    ///
    ///   Any
    ///     en az 1 Buy  → Buy
    ///     en az 1 Sell → Sell  (ikisi de varsa → Flat)
    ///     aksi         → Flat
    ///
    /// Tanınmayan bir ConsensusMode değeri gelirse Net'e düşülür ve bir uyarı loglanır.
    /// Buy/Sell sonucu üreten barlarda Debug seviyesinde bir satır loglanır (oy dağılımını
    /// görmek için); Flat sonuçlar loglanmaz — büyük backtest'lerde log şişmesin diye.
    /// </summary>
    public TradeSignals BuildConsensusSignal()
    {
        if (CustomConsensusFunc != null)
        {
            TradeSignals customResult = CustomConsensusFunc(Traders);
            if (customResult == TradeSignals.Buy || customResult == TradeSignals.Sell)
                LogManager.Log(LogLevel.Debug, LogSinks.File, $"MultipleTrader consensus [Custom]: -> {customResult}");
            return customResult;
        }

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

        TradeSignals result;

        switch (ConsensusMode?.Trim().ToLowerInvariant())
        {
            case "majority":
                int half = Traders.Count / 2;
                if (buyCount > half)
                    result = TradeSignals.Buy;
                else if (sellCount > half)
                    result = TradeSignals.Sell;
                else
                    result = TradeSignals.Flat;
                break;

            case "all":
                if (Traders.Count > 0 && buyCount == Traders.Count)
                    result = TradeSignals.Buy;
                else if (Traders.Count > 0 && sellCount == Traders.Count)
                    result = TradeSignals.Sell;
                else
                    result = TradeSignals.Flat;
                break;

            case "any":
                if (buyCount > 0 && sellCount > 0)
                    result = TradeSignals.Flat;
                else if (buyCount > 0)
                    result = TradeSignals.Buy;
                else if (sellCount > 0)
                    result = TradeSignals.Sell;
                else
                    result = TradeSignals.Flat;
                break;

            case "net":
            case null:
            case "":
                result = BuildNetConsensus(buyCount, sellCount);
                break;

            default:
                LogManager.LogWarning($"MultipleTrader: taninmayan ConsensusMode '{ConsensusMode}', 'Net' moduna dusuluyor.");
                result = BuildNetConsensus(buyCount, sellCount);
                break;
        }

        if (result == TradeSignals.Buy || result == TradeSignals.Sell)
        {
            // Sadece File'a yazılıyor (Console DEĞİL) — bar başına senkron Console.WriteLine,
            // büyük veri setlerinde (yüz binlerce bar × çoğunlukla Buy/Sell konsensüs) koşum
            // süresini dakikalarca uzatan bir darboğazdı. Bilgi app.log'da kalmaya devam ediyor.
            LogManager.Log(LogLevel.Debug, LogSinks.File, $"MultipleTrader consensus [{ConsensusMode}]: Buy={buyCount} Sell={sellCount} Flat={flatCount} -> {result}");
        }

        return result;
    }

    private TradeSignals BuildNetConsensus(int buyCount, int sellCount)
    {
        int netSignal = buyCount - sellCount;

        if (netSignal >= ConsensusMinNetCount)
            return TradeSignals.Buy;
        else if (netSignal <= -ConsensusMinNetCount)
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

    #pragma warning disable CS0465
    public void Finalize()
    {
        CurrentIndex = 0;
        foreach (var trader in Traders)
        {
            trader.Finalize();
        }

        if (!IsInitialized)
            throw new InvalidOperationException("Trader not initialized");

        LogManager.LogRaw($"\nCalculating statistics...");

        _mainTrader.CalculateStatistics();

        LogManager.LogRaw($"\nCalculating performances...");

        _mainTrader.GetPerformansParams(out double bakiyePuan, out double lotSayisi, out double varlikAdedCarpani); // TODO : ici yeniden duzenlenecek
        _mainTrader.CalculatePerformances(bakiyePuan, lotSayisi, varlikAdedCarpani);
    }
    #pragma warning restore CS0465

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

    #endregion

    #region MultipleTrader Lists Export

    /// <summary>
    /// Write MultipleTrader lists to both TXT and CSV files
    /// </summary>
    public void WriteMultipleTraderListsToFiles(string logDir)
    {
        if (this.SaveMultipleTraderListsTxtEnabled)
        {
            LogManager.LogRaw($"\n\tSaving MultipleTrader lists to {MultipleTraderListsTxtFileName}...");
            WriteMultipleTraderListsToTxt(logDir);
        }
        if (this.SaveMultipleTraderListsCsvEnabled)
        {
            LogManager.LogRaw($"\n\tSaving MultipleTrader lists to {MultipleTraderListsCsvFileName}...");
            WriteMultipleTraderListsToCsv(logDir);
        }
    }

    /// <summary>
    /// Write MultipleTrader lists to TXT file (tabular format with fixed-width columns)
    /// </summary>
    private void WriteMultipleTraderListsToTxt(string logDir)
    {
        if (_mainTrader == null || Data == null || Data.Count == 0)
            return;

        if (!System.IO.Directory.Exists(logDir))
            System.IO.Directory.CreateDirectory(logDir);

        var filePath = System.IO.Path.Combine(logDir, MultipleTraderListsTxtFileName);

        var fs1 = new System.IO.FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
        using (var writer = new System.IO.StreamWriter(fs1, System.Text.Encoding.UTF8))
        {
            // Title
            writer.WriteLine($"MULTIPLE TRADER BAR-BY-BAR DATA");
            writer.WriteLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
            writer.WriteLine($"BarSayisi: {Data.Count}  |  {Data[0].DateTime:yyyy.MM.dd HH:mm:ss} — {Data[^1].DateTime:yyyy.MM.dd HH:mm:ss}");
            writer.WriteLine("".PadRight(300, '='));

            // Header
            WriteHeaderTxt(writer);

            writer.WriteLine("".PadRight(300, '-'));

            // Data rows
            for (int i = 0; i < Data.Count; i++)
            {
                WriteBarDataTxt(writer, i);
            }

            //writer.WriteLine("".PadRight(300, '='));
        }

        LogManager.LogRaw($"{MultipleTraderListsTxtFileName} written to: {filePath}");
    }

    private void WriteHeaderTxt(System.IO.StreamWriter writer)
    {
        var header = $"{"BarNo",7} | " +
                    $"{"Date",10} | " +
                    $"{"Time",8} | " +
                    $"{"Open",10} | " +
                    $"{"High",10} | " +
                    $"{"Low",10} | " +
                    $"{"Close",10} | " +
                    $"{"Volume",10} | " +
                    $"{"Size",10} | " +
                    $"{"Change",10} | " +
                    $"{"Change%",10}";

        // MainTrader columns
        header += $" | {"MainYon",7} | {"MainSvy",10} | {"MainSny",7}";

        // Other trader columns
        for (int t = 0; t < Traders.Count; t++)
        {
            header += $" | {$"T{t}Yon",7} | {$"T{t}Svy",10} | {$"T{t}Sny",7}";
        }

        writer.WriteLine(header);
    }

    private void WriteBarDataTxt(System.IO.StreamWriter writer, int barIndex)
    {
        var bar = Data[barIndex];

        var line = $"{barIndex,7} | " +
                  $"{bar.Date:yyyy.MM.dd} | " +
                  $"{bar.DateTime:HH:mm:ss} | " +
                  $"{bar.Open,10:F2} | " +
                  $"{bar.High,10:F2} | " +
                  $"{bar.Low,10:F2} | " +
                  $"{bar.Close,10:F2} | " +
                  $"{bar.Volume,10:F0} | " +
                  $"{bar.Size,10:F0} | " +
                  $"{bar.Diff,10:F4} | " +
                  $"{bar.ChangePct,10:F2}";

        // MainTrader data
        line += $" | {GetYon(_mainTrader, barIndex),7} | " +
               $"{GetSeviye(_mainTrader, barIndex),10:F2} | " +
               $"{GetSinyal(_mainTrader, barIndex),7:F2}";

        // Other traders data
        foreach (var trader in Traders)
        {
            line += $" | {GetYon(trader, barIndex),7} | " +
                   $"{GetSeviye(trader, barIndex),10:F2} | " +
                   $"{GetSinyal(trader, barIndex),7:F2}";
        }

        writer.WriteLine(line);
    }

    /// <summary>
    /// Write MultipleTrader lists to CSV file (semicolon separated)
    /// </summary>
    private void WriteMultipleTraderListsToCsv(string logDir)
    {
        if (_mainTrader == null || Data == null || Data.Count == 0)
            return;

        if (!System.IO.Directory.Exists(logDir))
            System.IO.Directory.CreateDirectory(logDir);

        var filePath = System.IO.Path.Combine(logDir, MultipleTraderListsCsvFileName);

        var fs2 = new System.IO.FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
        using (var writer = new System.IO.StreamWriter(fs2, System.Text.Encoding.UTF8))
        {
            // Header
            WriteHeaderCsv(writer);

            // Data rows
            for (int i = 0; i < Data.Count; i++)
            {
                WriteBarDataCsv(writer, i);
            }
        }

        LogManager.LogRaw($"{MultipleTraderListsCsvFileName} written to: {filePath}");
    }

    private void WriteHeaderCsv(System.IO.StreamWriter writer)
    {
        var header = "BarNo;Date;Time;Open;High;Low;Close;Volume;Size;Change;Change%";

        // MainTrader columns
        header += ";MainTrader_Yon;MainTrader_Seviye;MainTrader_Sinyal";

        // Other trader columns
        for (int t = 0; t < Traders.Count; t++)
        {
            header += $";Trader{t}_Yon;Trader{t}_Seviye;Trader{t}_Sinyal";
        }

        writer.WriteLine(header);
    }

    private void WriteBarDataCsv(System.IO.StreamWriter writer, int barIndex)
    {
        var bar = Data[barIndex];

        var line = $"{barIndex};" +
                  $"{bar.Date:yyyy.MM.dd};" +
                  $"{bar.DateTime:HH:mm:ss};" +
                  $"{bar.Open:F2};" +
                  $"{bar.High:F2};" +
                  $"{bar.Low:F2};" +
                  $"{bar.Close:F2};" +
                  $"{bar.Volume:F0};" +
                  $"{bar.Size:F0};" +
                  $"{bar.Diff:F4};" +
                  $"{bar.ChangePct:F2}";

        // MainTrader data
        line += $";{GetYon(_mainTrader, barIndex)};" +
               $"{GetSeviye(_mainTrader, barIndex):F2};" +
               $"{GetSinyal(_mainTrader, barIndex):F2}";

        // Other traders data
        foreach (var trader in Traders)
        {
            line += $";{GetYon(trader, barIndex)};" +
                   $"{GetSeviye(trader, barIndex):F2};" +
                   $"{GetSinyal(trader, barIndex):F2}";
        }

        writer.WriteLine(line);
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

    #region MultipleTrader Statistics Export

    // TODO: "MultipleTraderStatistics" ismi yerine daha aciklayici bir isim (orn.
    // "MultipleTraderPerformanceSummary") tercih edilebilir - kullanici ile konusulup karar verilecek.
    //
    // Amac: mainTrader ve her childTrader'in WriteStatisticsToFile() ile zaten ayri ayri uretilen
    // tam performans raporlarini (bkz. SingleTraderStatistics.txt/.csv, SingleTraderPerformans.txt/.csv)
    // tek bir dosyada, satir=trader / kolon=metrik seklinde yan yana ozetler. Var olan trade-bazli
    // raporlarin YERINE gecmez, onlarin konsolide bir karsilastirma gorunumudur.

    /// <summary>
    /// mainTrader + tum childTrader'larin GetOptimizationSummary() ozetini tek bir dosyada
    /// (satir=trader, kolon=metrik) yazar. WriteMultipleTraderListsToFiles() (bar-bar sinyal
    /// listesi) ile karistirilmamali - bu method performans/istatistik ozetidir.
    /// Finalize() cagrildiktan (istatistikler hesaplandiktan) sonra cagrilmalidir.
    /// </summary>
    public void WriteMultipleTraderStatistics(string logDir)
    {
        if (this.SaveMultipleTraderStatisticsTxtEnabled)
        {
            LogManager.LogRaw($"\n\tSaving MultipleTrader statistics to {MultipleTraderStatisticsTxtFileName}...");
            WriteMultipleTraderStatisticsToTxt(logDir);
        }
        if (this.SaveMultipleTraderStatisticsCsvEnabled)
        {
            LogManager.LogRaw($"\n\tSaving MultipleTrader statistics to {MultipleTraderStatisticsCsvFileName}...");
            WriteMultipleTraderStatisticsToCsv(logDir);
        }
        if (this.SaveMultipleTraderStatisticsGridTxtEnabled)
        {
            LogManager.LogRaw($"\n\tSaving MultipleTrader statistics to {MultipleTraderStatisticsGridTxtFileName}...");
            WriteMultipleTraderStatisticsGridToTxt(logDir, minimal: false);
        }
        if (this.SaveMultipleTraderStatisticsMinimalGridTxtEnabled)
        {
            LogManager.LogRaw($"\n\tSaving MultipleTrader statistics to {MultipleTraderStatisticsMinimalGridTxtFileName}...");
            WriteMultipleTraderStatisticsGridToTxt(logDir, minimal: true);
        }
    }

    /// <summary>
    /// mainTrader + tum childTrader'larin KENDI (StatisticsMap/StatisticsMapMinimal - SaveToTxtGrid/
    /// SaveToTxtMinimalGrid'in de kaynagi) haritalarini toplar. BuildStatisticsRows()'daki
    /// GetOptimizationSummary()'den KASITLI olarak FARKLI bir kaynak: boylece buradaki her kolon,
    /// o trader'in kendi MultipleTrader_Main_/Child{i}_StatisticsGrid.txt dosyasiyla BIREBIR ayni
    /// degerleri gosterir (GridReportSections/GridMinimalReportSections zaten bu haritalardan
    /// turetildigi icin key uyusmazligi riski yok).
    /// </summary>
    private List<(string Name, Dictionary<string, string> Map)> BuildGridStatisticsRows(bool minimal)
    {
        var rows = new List<(string Name, Dictionary<string, string> Map)>();

        void Add(SingleTrader? trader)
        {
            if (trader == null) return;
            if (minimal)
            {
                trader.statistics.AssignToMapMinimalForExport();
                rows.Add((trader.Name, new Dictionary<string, string>(trader.statistics.StatisticsMapMinimal)));
            }
            else
            {
                trader.statistics.AssignToMapForExport();
                rows.Add((trader.Name, new Dictionary<string, string>(trader.statistics.StatisticsMap)));
            }
        }

        Add(_mainTrader);
        foreach (var trader in Traders)
            Add(trader);

        return rows;
    }

    private void WriteMultipleTraderStatisticsGridToTxt(string logDir, bool minimal)
    {
        var rows = BuildGridStatisticsRows(minimal);
        if (rows.Count == 0)
            return;

        if (!System.IO.Directory.Exists(logDir))
            System.IO.Directory.CreateDirectory(logDir);

        var fileName = minimal ? MultipleTraderStatisticsMinimalGridTxtFileName : MultipleTraderStatisticsGridTxtFileName;
        var filePath = System.IO.Path.Combine(logDir, fileName);
        var title = minimal
            ? "MULTIPLE TRADER STATISTICS - MINIMAL GRID REPORT"
            : "MULTIPLE TRADER STATISTICS - GRID REPORT";

        var content = StatisticsExporter.BuildMultipleTraderGridReport(title, rows, minimal);

        var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
        using (var writer = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8))
            writer.Write(content);

        LogManager.LogRaw($"{fileName} written to: {filePath}");
    }

    private List<(string Name, Dictionary<string, string> Summary)> BuildStatisticsRows()
    {
        var rows = new List<(string Name, Dictionary<string, string> Summary)>();

        if (_mainTrader != null)
            rows.Add((_mainTrader.Name, _mainTrader.statistics.GetOptimizationSummary()));

        foreach (var trader in Traders)
            rows.Add((trader.Name, trader.statistics.GetOptimizationSummary()));

        return rows;
    }

    private void WriteMultipleTraderStatisticsToTxt(string logDir)
    {
        var rows = BuildStatisticsRows();
        if (rows.Count == 0)
            return;

        if (!System.IO.Directory.Exists(logDir))
            System.IO.Directory.CreateDirectory(logDir);

        var filePath = System.IO.Path.Combine(logDir, MultipleTraderStatisticsTxtFileName);
        var keys = rows[0].Summary.Keys.ToList();

        var traderColWidth = Math.Max("Trader".Length, rows.Max(r => r.Name.Length)) + 1;
        var colWidths = keys.ToDictionary(
            k => k,
            k => Math.Max(k.Length, rows.Max(r => r.Summary.TryGetValue(k, out var v) ? v.Length : 0)) + 1);

        var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
        using (var writer = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8))
        {
            writer.WriteLine("MULTIPLE TRADER STATISTICS SUMMARY");
            writer.WriteLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
            writer.WriteLine("".PadRight(300, '='));

            var headerParts = new List<string> { "Trader".PadRight(traderColWidth) };
            foreach (var key in keys)
                headerParts.Add(key.PadLeft(colWidths[key]));
            writer.WriteLine(string.Join(" | ", headerParts));

            writer.WriteLine("".PadRight(300, '-'));

            foreach (var row in rows)
            {
                var dataParts = new List<string> { row.Name.PadRight(traderColWidth) };
                foreach (var key in keys)
                    dataParts.Add((row.Summary.TryGetValue(key, out var v) ? v : "").PadLeft(colWidths[key]));
                writer.WriteLine(string.Join(" | ", dataParts));
            }
        }

        LogManager.LogRaw($"{MultipleTraderStatisticsTxtFileName} written to: {filePath}");
    }

    private void WriteMultipleTraderStatisticsToCsv(string logDir)
    {
        var rows = BuildStatisticsRows();
        if (rows.Count == 0)
            return;

        if (!System.IO.Directory.Exists(logDir))
            System.IO.Directory.CreateDirectory(logDir);

        var filePath = System.IO.Path.Combine(logDir, MultipleTraderStatisticsCsvFileName);
        var keys = rows[0].Summary.Keys.ToList();

        var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
        using (var writer = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8))
        {
            var headerParts = new List<string> { "Trader" };
            headerParts.AddRange(keys);
            writer.WriteLine(string.Join(";", headerParts));

            foreach (var row in rows)
            {
                var dataParts = new List<string> { row.Name };
                dataParts.AddRange(keys.Select(k => row.Summary.TryGetValue(k, out var v) ? v : ""));
                writer.WriteLine(string.Join(";", dataParts));
            }
        }

        LogManager.LogRaw($"{MultipleTraderStatisticsCsvFileName} written to: {filePath}");
    }

    #endregion

    #region Dispose

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
