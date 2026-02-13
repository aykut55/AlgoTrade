using AlgoTrade.Core;
using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Query;
using AlgoTrade.Core.Trading.Strategy;
// using AlgoTrade.Core.Trading.Core; // already included above
using MathNet.Numerics.Statistics;
using static AlgoTrade.Core.StockDataReader.StockDataReader;

namespace AlgoTrade.Core.Trading;

public enum TraderRunMode
{
    TradeOnly = 0,
    TradeAndQuery = 1,
    QueryOnly = 2
}
public enum ConfirmationTrigger
{
    ProfitOnly = 0,
    LossOnly = 1,
    Both = 2
}

public class SingleTrader : MarketDataProvider, IDisposable
{
    #region Properties

    // Identification
    public int Id { get; private set; }
    public void SetId(int id) => Id = id;
    public int GetId() => Id;

    public string Name { get; private set; } = string.Empty;
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
    public string QueryId { get; set; }
    public string QueryName { get; set; }

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

    public IStrategy? Strategy { get; private set; }
    public IQuery? Query { get; private set; }
    public List<string> QueryColumnNames { get; private set; } = new();
    public List<object> LastQueryResult { get; private set; } = new();
    public List<Dictionary<string, object>> QueryResults { get; private set; } = new();

    public void SetStrategy(IStrategy strategy)
    {
        if (strategy is null)
            throw new ArgumentNullException(nameof(strategy));

        if (_data == null || _data.Count == 0)
            throw new InvalidOperationException("Trader data is not initialized.");

        if (_indicators is null)
            throw new InvalidOperationException("IndicatorManager is not initialized.");

        Strategy = strategy;

        if (strategy is BaseStrategy baseStrategy)
        {
            baseStrategy.SetTrader(this);
            baseStrategy.Initialize(_data, _indicators);
        }
        else
        {
            throw new InvalidOperationException("Strategy must inherit from BaseStrategy.");
        }
    }

    public void SetQuery(IQuery query)
    {
        if (query is null)
            throw new ArgumentNullException(nameof(query));

        if (_data == null || _data.Count == 0)
            throw new InvalidOperationException("Trader data is not initialized.");

        if (_indicators is null)
            throw new InvalidOperationException("IndicatorManager is not initialized.");

        Query = query;
        QueryColumnNames.Clear();
        LastQueryResult.Clear();
        QueryResults.Clear();

        if (query is BaseQuery baseQuery)
        {
            baseQuery.SetTrader(this);
            baseQuery.SetLogger(_logger);
            baseQuery.Initialize(_data, _indicators);
        }
        else
        {
            throw new InvalidOperationException("Query must inherit from BaseQuery.");
        }
    }

    public InitialTradeParams? initialTradeParams { get; private set; }
    public Signals? signals { get; private set; }
    public Status? status { get; private set; }
    public Flags? flags { get; private set; }
    public Lists? lists { get; private set; }
    public TimeUtils timeUtils { get; private set; }
    public TradeSignals strategySignal { get; set; }
    public KarZarar karZarar { get; private set; }
    public KarAlZararKes karAlZararKes { get; private set; }
    public Statistics.Statistics statistics { get; private set; }
    

    #endregion

    #region TimeFilterProperties
    // Time Filter Properties
    public string StartDateTimeStr { get; set; }
    public string StopDateTimeStr { get; set; }
    public string StartDateStr { get; set; }
    public string StopDateStr { get; set; }
    public string StartTimeStr { get; set; }
    public string StopTimeStr { get; set; }
    #endregion

    #region EquityCurveFilterProperties
    // Equity Curve Filter Properties
    private bool thresholdTypeIsPercent = false;               // false = Value, true = Percent
    private double profitConfirmationThreshold = 10.0;         // Profit threshold
    private double lossConfirmationThreshold = 5.0;            // Loss threshold
    private ConfirmationTrigger confirmationTrigger = ConfirmationTrigger.Both;
    private bool _equityCurveConfirmed = false;
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

    public int ExecutionStepNumber { get; set; }
    public bool BakiyeInitialized { get; set; }

    #region StateFlags
    // State flags
    public bool IsStarted { get; internal set; }
    public bool IsRunning { get; internal set; }
    public bool IsStopped { get; internal set; }
    public bool IsStopRequested { get; internal set; }
    public TraderRunMode RunMode { get; set; } = TraderRunMode.TradeAndQuery;
    #endregion

    #region ScreeningProperties
    // ═══════════════════════════════════════════════════════════════════════
    // SCREENING (TARAMA) PROPERTIES - Son sinyal ve açık pozisyon bilgileri
    // Mevcut signals ve lists yapısından türetilir
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Son yön: "A" (Al/Long), "S" (Sat/Short), "F" (Flat)
    /// signals.SonYon'dan alınır
    /// </summary>
    public string SonYon => signals?.SonYon ?? "F";

    /// <summary>
    /// Son sinyalin geldiği bar index'i
    /// signals.SonBarNo'dan alınır
    /// </summary>
    public int SonSinyalBarIndex => signals?.SonBarNo ?? -1;

    /// <summary>
    /// Son sinyalden bu yana kaç bar geçti (0 = bu bar'da sinyal geldi)
    /// </summary>
    public int SonSinyaldenBeriBarSayisi => signals?.SonBarNo >= 0 ? (Data.Count - 1 - signals.SonBarNo) : -1;

    /// <summary>
    /// Açık pozisyon var mı? (A veya S durumunda)
    /// </summary>
    public bool AcikPozisyonVar => SonYon == "A" || SonYon == "S";

    /// <summary>
    /// Son bar için Kar/Zarar (fiyat olarak - puan)
    /// lists.KarZararFiyatList'ten alınır
    /// </summary>
    public double SonKarZararFiyat => (lists?.KarZararFiyatList != null && Data.Count > 0)
        ? lists.KarZararFiyatList[Data.Count - 1] : 0.0;

    /// <summary>
    /// Son bar için Kar/Zarar (yüzde olarak)
    /// lists.KarZararPuanYuzdeList'ten alınır
    /// </summary>
    public double SonKarZararYuzde => (lists?.KarZararPuanYuzdeList != null && Data.Count > 0)
        ? lists.KarZararPuanYuzdeList[Data.Count - 1] : 0.0;

    /// <summary>
    /// Son sinyal fiyatı (giriş fiyatı)
    /// signals.SonFiyat'tan alınır
    /// </summary>
    public double SonSinyalFiyati => signals?.SonFiyat ?? 0.0;

    /// <summary>
    /// Tarama özet bilgisi - tek satırda tüm bilgiler
    /// </summary>
    public string TaramaOzeti => $"{SonYon} | Bar:{SonSinyaldenBeriBarSayisi} | KZ:{SonKarZararFiyat:F2} | %:{SonKarZararYuzde:F2}";

    /// <summary>
    /// Sorgu özet bilgisi - son query çıktısının tek satır özeti
    /// </summary>
    public string SorguOzeti { get; set; } = "...";

    // ═══════════════════════════════════════════════════════════════════════
    #endregion

    #region Statistics

    #endregion

    public bool is_son_yon_f()
    {
        return this.signals.SonYon == "F";
    }

    /// <summary>
    /// Check if last direction is Buy (A)
    /// </summary>
    public bool is_son_yon_a()
    {
        return this.signals.SonYon == "A";
    }

    /// <summary>
    /// Check if last direction is Sell (S)
    /// </summary>
    public bool is_son_yon_s()
    {
        return this.signals.SonYon == "S";
    }

    public bool is_prev_yon_f()
    {
        return this.signals.PrevYon == "F";
    }

    public bool is_prev_yon_a()
    {
        return this.signals.PrevYon == "A";
    }

    public bool is_prev_yon_s()
    {
        return this.signals.PrevYon == "S";
    }

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

    public SingleTrader ClearCallbacks()
    {
        OnReset = null;
        OnInit = null;
        OnRun = null;
        OnFinal = null;
        OnBeforeOrder = null;
        OnNotifySignal = null;
        OnAfterOrder = null;
        OnProgress = null;
        OnApplyUserFlags = null;

        return this;
    }

    public void Reset()
    {
        if (_data == null || _data.Count == 0)
            throw new ArgumentException("Data cannot be null or empty");

        //CurrentIndex = 0;

        SymbolName = "...";
        SymbolPeriod = "...";
        SystemId = "...";
        SystemName = "...";
        StrategyId = "...";
        StrategyName = "...";
        QueryId = "...";
        QueryName = "...";
        LastResetTime = "...";
        LastExecutionId = "...";
        LastExecutionTime = "...";
        LastExecutionTimeStart = "...";
        LastExecutionTimeStop = "...";
        LastExecutionTimeInMSec = "...";
        QueryColumnNames.Clear();
        LastQueryResult.Clear();
        QueryResults.Clear();
        SorguOzeti = "...";

        OnReset?.Invoke(this, 0);

        // Reset internal modules (state only)
        ResetModules();

        // Re-apply user-defined flags after internal resets
        // OnApplyUserFlags?.Invoke(this);

        OnReset?.Invoke(this, 1);

        ExecutionStepNumber = 0;
        BakiyeInitialized = false;

        // Reset equity curve filter properties
        thresholdTypeIsPercent = false;
        profitConfirmationThreshold = 10.0;
        lossConfirmationThreshold = 5.0;
        confirmationTrigger = ConfirmationTrigger.Both;
        _equityCurveConfirmed = false;

        // Reset state flags
        IsStarted = false;
        IsRunning = false;
        IsStopped = false;
        IsStopRequested = false;

        this.LastResetTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
    }

    public void Init()
    {
        if (_data == null || _data.Count == 0)
            throw new ArgumentException("Data cannot be null or empty");

        OnInit?.Invoke(this, 0);

        InitModules();

        OnInit?.Invoke(this, 1);
    }

    public void Run(int barIndex)
    {
        int i = barIndex;

        //if (!IsInitialized)
        //throw new InvalidOperationException("Trader not initialized");

        if (i >= Data.Count)
            return;

        OnRun?.Invoke(this, 0);

        if (RunMode == TraderRunMode.TradeOnly)
        {
            ExecutePreOrderMethods(i);

            if (i < 1)
                return;

            this.strategySignal = ExecuteStrategy(i);

            MapStrategyCommandsToTradeCommands(this.strategySignal);

            // Sıralama önemli : TODO : ApplyTimingFilters ve ApplyEquityCurveFilter leri analiz ederek,  this.signals.IsTradeEnabled'e karar verilecek 
            ApplyTimingFilters(i);

            ApplyEquityCurveFilter(i);
            // Sıralama önemli : TODO : ApplyTimingFilters ve ApplyEquityCurveFilter leri analiz ederek,  this.signals.IsTradeEnabled'e karar verilecek

            ExecutePostOrderMethods(i);

        }
        else if (RunMode == TraderRunMode.TradeAndQuery)
        {
            ExecutePreOrderMethods(i);

            if (i < 1)
                return;

            this.strategySignal = ExecuteStrategy(i);

            MapStrategyCommandsToTradeCommands(this.strategySignal);

            // Sıralama önemli : TODO : ApplyTimingFilters ve ApplyEquityCurveFilter leri analiz ederek,  this.signals.IsTradeEnabled'e karar verilecek
            ApplyTimingFilters(i);

            ApplyEquityCurveFilter(i);
            // Sıralama önemli : TODO : ApplyTimingFilters ve ApplyEquityCurveFilter leri analiz ederek,  this.signals.IsTradeEnabled'e karar verilecek

            ExecutePostOrderMethods(i);

            ExecuteQuery(i);
        }
        else if (RunMode == TraderRunMode.QueryOnly)
        {
            if (i < 1)
                return;

            ExecuteQuery(i);
        }
        else
        {

        }

        OnRun?.Invoke(this, 1);

        int totalBars = GetDataCount();
        double percentage = (i + 1) / (double)totalBars * 100.0;
        OnProgress?.Invoke(this, i + 1, totalBars, percentage);
    }

    public void Finalize(bool saveStatisticsToFile = true, string? outputDir = null)
    {
        OnFinal?.Invoke(this, 0);

        if (this.RunMode == TraderRunMode.TradeOnly)
        {
            Log($"\nCalculating statistics...");

            CalculateStatistics();

            if (saveStatisticsToFile)
            {
                Log($"\nSaving statistics to files...");
                WriteStatisticsToFile(outputDir ?? AppSettings.LogsDir);
            }
        }
        else if (this.RunMode == TraderRunMode.TradeAndQuery)
        {
            Log($"\nCalculating statistics...");

            CalculateStatistics();

            if (saveStatisticsToFile)
            {
                Log($"\nSaving statistics to files...");
                WriteStatisticsToFile(outputDir ?? AppSettings.LogsDir);
            }

            if (this.QueryColumnNames.Count > 0 && this.LastQueryResult.Count == this.QueryColumnNames.Count)
            {
                this.SorguOzeti = string.Join(", ", this.QueryColumnNames.Zip(this.LastQueryResult, (col, val) => $"{col}={val}"));
            }
        }
        else if (this.RunMode == TraderRunMode.QueryOnly)
        {
            if (this.QueryColumnNames.Count > 0 && this.LastQueryResult.Count == this.QueryColumnNames.Count)
            {
                this.SorguOzeti = string.Join(", ", this.QueryColumnNames.Zip(this.LastQueryResult, (col, val) => $"{col}={val}"));
            }
        }

        OnFinal?.Invoke(this, 1);
    }
    
    public SingleTrader CreateModules()
    {
        /*signals = new Signals();
        status = new Status();
        flags = new Flags();
        lists = new Lists();

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

        signals = new Signals();

        status = new Status();

        flags = new Flags();

        lists = new Lists();

        timeUtils = new TimeUtils();
        timeUtils.SetTrader(this); 
        
        karZarar = new KarZarar(this);

        karAlZararKes = new KarAlZararKes();
        karAlZararKes.SetTrader(this);

        statistics = new Statistics.Statistics();

        return this;
    }
    
    public SingleTrader ResetModules()
    {
        initialTradeParams.Reset();

        signals.Reset();

        status.Reset();

        flags.Reset();

        lists.Reset();

        timeUtils.Reset();

        karZarar.Reset();

        karAlZararKes.Reset();

        statistics.Reset();

        return this;
    }
    
    public SingleTrader InitModules()
    {
        initialTradeParams.Init();

        signals.Init();

        status.Init();

        flags.Init();

        lists.InitOrReuse(_data.Count);

        timeUtils.Init();

        karZarar.Init(this);

        karAlZararKes.Init();

        statistics.Init(this);

        return this;
    }
    
    public SingleTrader DeleteModules()
    {
        ClearCallbacks();
        Strategy = null;
        Query = null;
        QueryColumnNames.Clear();
        LastQueryResult.Clear();
        QueryResults.Clear();

        initialTradeParams = null;

        signals = null;

        status = null;

        flags = null;

        lists = null;

        timeUtils = null;

        karZarar = null;

        karAlZararKes = null;

        statistics = null;

        return this;
    }

    public TradeSignals ExecuteStrategy(int barIndex)
    {
        int i = barIndex;

        if (Strategy is null)
            return TradeSignals.None;

        return Strategy.OnStep(i);
    }

    public IReadOnlyList<object> ExecuteQuery(int barIndex)
    {
        int i = barIndex;

        if (Query is null)
            return Array.Empty<object>();

        var values = Query.OnExecute(i);

        QueryColumnNames = new List<string>(Query.ColumnNames);
        LastQueryResult = new List<object>(values);

        if (values.Count > 0 && QueryColumnNames.Count == values.Count)
        {
            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["BarIndex"] = i,
                ["DateTime"] = Data[i].DateTime
            };

            for (int idx = 0; idx < QueryColumnNames.Count; idx++)
            {
                row[QueryColumnNames[idx]] = values[idx];
            }

            QueryResults.Add(row);
        }

        return values;
    }
    
    public void MapStrategyCommandsToTradeCommands(TradeSignals strategySignal)
    {
        if (strategySignal == TradeSignals.None)
        {
            this.signals.None = true;
        }

        if (strategySignal == TradeSignals.Buy)
        {
            if (this.signals.AlEnabled)
                this.signals.Al = true;
        }

        if (strategySignal == TradeSignals.Sell)
        {
            if (this.signals.SatEnabled)
                this.signals.Sat = true;
        }

        if (strategySignal == TradeSignals.TakeProfit)
        {
            if (this.signals.KarAlEnabled)
                this.signals.KarAl = true;
        }

        if (strategySignal == TradeSignals.StopLoss)
        {
            if (this.signals.ZararKesEnabled)
                this.signals.ZararKes = true;
        }

        if (strategySignal == TradeSignals.Flat)
        {
            if (this.signals.FlatOlEnabled)
                this.signals.FlatOl = true;
        }

        if (strategySignal == TradeSignals.Skip)
        {
            if (this.signals.PasGecEnabled)
                this.signals.PasGec = true;
        }
    }

    public int ExecuteOrders(int barIndex)
    {
        int result = 0;

        int i = barIndex;

        // ------------------------------------------------------------------------------
        this.flags.AGerceklesti = false;
        this.flags.SGerceklesti = false;
        this.flags.FGerceklesti = false;
        this.flags.PGerceklesti = false;

        double AnlikKapanisFiyati = this.Data[i].Close;
        double AnlikYuksekFiyati = this.Data[i].High;
        double AnlikDusukFiyati = this.Data[i].Low;

        // ------------------------------------------------------------------------------
        if (this.signals.None)
        {

        }
        if (this.signals.Al)
        {
            this.signals.Sinyal = "A";
            this.signals.EmirKomut = 1;
            this.status.AlKomutSayisi += 1;
        }
        if (this.signals.Sat)
        {
            this.signals.Sinyal = "S";
            this.signals.EmirKomut = 2;
            this.status.SatKomutSayisi += 1;
        }
        if (this.signals.PasGec)
        {
            this.signals.Sinyal = "P";
            this.signals.EmirKomut = 3;
            this.status.PasGecKomutSayisi += 1;
        }
        if (this.signals.KarAl)
        {
            this.signals.Sinyal = "F";
            this.signals.EmirKomut = 4;
            this.status.KarAlKomutSayisi += 1;
        }
        if (this.signals.ZararKes)
        {
            this.signals.Sinyal = "F";
            this.signals.EmirKomut = 5;
            this.status.ZararKesKomutSayisi += 1;
        }
        if (this.signals.FlatOl)
        {
            this.signals.Sinyal = "F";
            this.signals.EmirKomut = 6;
            this.status.FlatOlKomutSayisi += 1;
        }

        // ------------------------------------------------------------------------------
        this.status.KarAlSayisi = this.status.KarAlKomutSayisi;
        this.status.ZararKesSayisi = this.status.ZararKesKomutSayisi;

        // ------------------------------------------------------------------------------
        // Process "A" (Al/Buy) signal
        if (this.signals.Sinyal == "A" && this.signals.SonYon != "A")
        {
            this.signals.PrevAFiyat = this.signals.SonAFiyat;
            this.signals.PrevABarNo = this.signals.SonABarNo;
            this.signals.PrevYon = this.signals.SonYon;
            this.signals.PrevFiyat = this.signals.SonFiyat;
            this.signals.PrevBarNo = this.signals.SonBarNo;

            // Pozisyon büyüklüğünü kaydet (dinamik lot desteği için)
            this.signals.PrevVarlikAdedSayisi = this.signals.SonVarlikAdedSayisi;
            this.signals.PrevVarlikAdedSayisiMicro = this.signals.SonVarlikAdedSayisiMicro;

            if (this.signals.PrevYon == "F")
            {
                // pass
            }
            if (this.signals.PrevYon == "S")
            {
                // pass
            }

            this.lists.YonList[i] = "A";
            this.signals.SonYon = this.lists.YonList[i];
            this.signals.SonFiyat = AnlikKapanisFiyati;

            if (this.flags.KaymayiDahilEt)
            {
                this.signals.SonFiyat = AnlikYuksekFiyati;
            }

            this.lists.SeviyeList[i] = this.signals.SonFiyat;
            this.signals.SonBarNo = i;
            this.signals.SonAFiyat = this.signals.SonFiyat;
            this.signals.SonABarNo = this.signals.SonBarNo;

            // Yeni pozisyon büyüklüğünü kaydet (hem normal hem micro)
            this.signals.SonVarlikAdedSayisi = this.initialTradeParams.VarlikAdedSayisi;
            this.signals.SonVarlikAdedSayisiMicro = this.initialTradeParams.VarlikAdedSayisiMicro;

            this.lists.SonVarlikAdedSayisiList[i] = this.signals.SonVarlikAdedSayisi;
            this.lists.SonVarlikAdedSayisiMicroList[i] = this.signals.SonVarlikAdedSayisiMicro;

            bool isMicroLot = this.initialTradeParams.MicroLotSizeEnabled;
            double komisyonVolume = 0.0;
            double totalCommission = 0.0;
            double komisyonCarpan = this.status.KomisyonCarpan;

            if (this.signals.PrevYon == "F")
            {
                // F → A: Yeni pozisyon açma (1 işlem)
                // İşlem hacmi: SonVarlikAdedSayisi
                this.status.KomisyonIslemSayisi += 1;
                this.signals.EmirStatus = 1;

                komisyonVolume = isMicroLot
                    ? this.status.KomisyonVarlikAdedSayisiMicro
                    : this.status.KomisyonVarlikAdedSayisi;

                // komisyon hesapla
                double openCommission = komisyonCarpan * komisyonVolume;

                totalCommission = openCommission;
            }
            if (this.signals.PrevYon == "S")
            {
                // S → A: Ters yön değişimi (2 ayrı işlem)
                // İşlem 1: Short pozisyonu KAPAT (PrevVarlikAdedSayisi lot)
                // İşlem 2: Long pozisyon AÇ (SonVarlikAdedSayisi lot)
                // Toplam işlem hacmi: PrevVarlikAdedSayisi + SonVarlikAdedSayisi

                double fark = this.signals.SonFiyat - this.signals.SonSFiyat;
                if (fark < 0)
                {
                    this.status.KazandiranSatisSayisi += 1;
                }
                else if (fark > 0)
                {
                    this.status.KaybettirenSatisSayisi += 1;
                }
                else
                {
                    this.status.NotrSatisSayisi += 1;
                }

                // 2 işlem: Kapatma + Açma
                this.status.KomisyonIslemSayisi += 2;
                this.signals.EmirStatus = 2;

                komisyonVolume = isMicroLot
                    ? this.status.KomisyonVarlikAdedSayisiMicro
                    : this.status.KomisyonVarlikAdedSayisi;

                // Her iki işlem için de ayrı komisyon hesapla
                double closeCommission = komisyonCarpan * komisyonVolume;
                double openCommission = komisyonCarpan * komisyonVolume;

                totalCommission = closeCommission + openCommission;
            }

            this.flags.BakiyeGuncelle = true;
            this.flags.KomisyonGuncelle = true;
            this.flags.DonguSonuIstatistikGuncelle = true;
            this.status.IslemSayisi += 1;
            this.status.AlisSayisi += 1;
            this.flags.AGerceklesti = true;

            //OnNotifyStrategySignal?.Invoke(this, this.signals.Sinyal, i);

            // Mevcut pozisyon büyüklüğü
            double mevcutLot = isMicroLot
                ? this.signals.SonVarlikAdedSayisiMicro
                : this.signals.SonVarlikAdedSayisi;

            // Eklenecek lot büyüklüğü
            double yeniLot = isMicroLot
                ? this.initialTradeParams.VarlikAdedSayisiMicro
                : this.initialTradeParams.VarlikAdedSayisi;

            this.status.KomisyonFiyat += totalCommission;
            this.lists.KomisyonFiyatList[i] = this.status.KomisyonFiyat;

        }
        // Process "S" (Sat/Sell) signal
        else if (this.signals.Sinyal == "S" && this.signals.SonYon != "S")
        {
            this.signals.PrevSFiyat = this.signals.SonSFiyat;
            this.signals.PrevSBarNo = this.signals.SonSBarNo;
            this.signals.PrevYon = this.signals.SonYon;
            this.signals.PrevFiyat = this.signals.SonFiyat;
            this.signals.PrevBarNo = this.signals.SonBarNo;

            // Pozisyon büyüklüğünü kaydet (dinamik lot desteği için)
            this.signals.PrevVarlikAdedSayisi = this.signals.SonVarlikAdedSayisi;
            this.signals.PrevVarlikAdedSayisiMicro = this.signals.SonVarlikAdedSayisiMicro;

            if (this.signals.PrevYon == "F")
            {
                // pass
            }
            if (this.signals.PrevYon == "A")
            {
                // pass
            }

            this.lists.YonList[i] = "S";
            this.signals.SonYon = this.lists.YonList[i];
            this.signals.SonFiyat = AnlikKapanisFiyati;

            if (this.flags.KaymayiDahilEt)
            {
                this.signals.SonFiyat = AnlikDusukFiyati;
            }

            this.lists.SeviyeList[i] = this.signals.SonFiyat;
            this.signals.SonBarNo = i;
            this.signals.SonSFiyat = this.signals.SonFiyat;
            this.signals.SonSBarNo = this.signals.SonSBarNo;

            // Yeni pozisyon büyüklüğünü kaydet (hem normal hem micro)
            this.signals.SonVarlikAdedSayisi = this.initialTradeParams.VarlikAdedSayisi;
            this.signals.SonVarlikAdedSayisiMicro = this.initialTradeParams.VarlikAdedSayisiMicro;

            this.lists.SonVarlikAdedSayisiList[i] = this.signals.SonVarlikAdedSayisi;
            this.lists.SonVarlikAdedSayisiMicroList[i] = this.signals.SonVarlikAdedSayisiMicro;

            bool isMicroLot = this.initialTradeParams.MicroLotSizeEnabled;
            double komisyonVolume = 0.0;
            double totalCommission = 0.0;
            double komisyonCarpan = this.status.KomisyonCarpan;

            if (this.signals.PrevYon == "F")
            {
                // F → S: Yeni pozisyon açma (1 işlem)
                // İşlem hacmi: SonVarlikAdedSayisi
                this.status.KomisyonIslemSayisi += 1;
                this.signals.EmirStatus = 3;

                komisyonVolume = isMicroLot
                    ? this.status.KomisyonVarlikAdedSayisiMicro
                    : this.status.KomisyonVarlikAdedSayisi;

                // komisyon hesapla
                double closeCommission = komisyonCarpan * komisyonVolume;

                totalCommission = closeCommission;
            }
            if (this.signals.PrevYon == "A")
            {
                // A → S: Ters yön değişimi (2 ayrı işlem)
                // İşlem 1: Long pozisyonu KAPAT (PrevVarlikAdedSayisi lot)
                // İşlem 2: Short pozisyon AÇ (SonVarlikAdedSayisi lot)
                // Toplam işlem hacmi: PrevVarlikAdedSayisi + SonVarlikAdedSayisi

                double fark = this.signals.SonFiyat - this.signals.SonAFiyat;
                if (fark > 0)
                {
                    this.status.KazandiranAlisSayisi += 1;
                }
                else if (fark < 0)
                {
                    this.status.KaybettirenAlisSayisi += 1;
                }
                else
                {
                    this.status.NotrAlisSayisi += 1;
                }

                // 2 işlem: Kapatma + Açma
                this.status.KomisyonIslemSayisi += 2;
                this.signals.EmirStatus = 4;

                komisyonVolume = isMicroLot
                    ? this.status.KomisyonVarlikAdedSayisiMicro
                    : this.status.KomisyonVarlikAdedSayisi;

                // Her iki işlem için de ayrı komisyon hesapla
                double closeCommission = komisyonCarpan * komisyonVolume;
                double openCommission = komisyonCarpan * komisyonVolume;

                totalCommission = closeCommission + openCommission;
            }

            this.flags.BakiyeGuncelle = true;
            this.flags.KomisyonGuncelle = true;
            this.flags.DonguSonuIstatistikGuncelle = true;
            this.status.IslemSayisi += 1;
            this.status.SatisSayisi += 1;
            this.flags.SGerceklesti = true;

            //OnNotifyStrategySignal?.Invoke(this, this.signals.Sinyal, i);

            // Mevcut pozisyon büyüklüğü
            double mevcutLot = isMicroLot
                ? this.signals.SonVarlikAdedSayisiMicro
                : this.signals.SonVarlikAdedSayisi;

            // Eklenecek lot büyüklüğü
            double yeniLot = isMicroLot
                ? this.initialTradeParams.VarlikAdedSayisiMicro
                : this.initialTradeParams.VarlikAdedSayisi;

            this.status.KomisyonFiyat += totalCommission;
            this.lists.KomisyonFiyatList[i] = this.status.KomisyonFiyat;
        }
        // Process "F" (Flat) signal
        else if (this.signals.Sinyal == "F" && this.signals.SonYon != "F")
        {
            this.signals.PrevFFiyat = this.signals.SonFFiyat;
            this.signals.PrevFBarNo = this.signals.SonFBarNo;
            this.signals.PrevYon = this.signals.SonYon;
            this.signals.PrevFiyat = this.signals.SonFiyat;
            this.signals.PrevBarNo = this.signals.SonBarNo;

            // Pozisyon büyüklüğünü kaydet (dinamik lot desteği için)
            this.signals.PrevVarlikAdedSayisi = this.signals.SonVarlikAdedSayisi;
            this.signals.PrevVarlikAdedSayisiMicro = this.signals.SonVarlikAdedSayisiMicro;

            if (this.signals.PrevYon == "A")
            {
                // pass
            }
            if (this.signals.PrevYon == "S")
            {
                // pass
            }

            this.lists.YonList[i] = "F";
            this.signals.SonYon = this.lists.YonList[i];
            this.signals.SonFiyat = AnlikKapanisFiyati;

            if (this.flags.KaymayiDahilEt)
            {
                if (this.signals.PrevYon == "A")
                {
                    this.signals.SonFiyat = AnlikDusukFiyati;
                }
                if (this.signals.PrevYon == "S")
                {
                    this.signals.SonFiyat = AnlikYuksekFiyati;
                }
            }

            this.lists.SeviyeList[i] = this.signals.SonFiyat;
            this.signals.SonBarNo = i;
            this.signals.SonFFiyat = this.signals.SonFiyat;
            this.signals.SonFBarNo = this.signals.SonFBarNo;

            // Flat durumunda pozisyon yok (hem normal hem micro)
            this.signals.SonVarlikAdedSayisi = 0.0;
            this.signals.SonVarlikAdedSayisiMicro = 0.0;

            this.lists.SonVarlikAdedSayisiList[i] = this.signals.SonVarlikAdedSayisi;
            this.lists.SonVarlikAdedSayisiMicroList[i] = this.signals.SonVarlikAdedSayisiMicro;

            bool isMicroLot = this.initialTradeParams.MicroLotSizeEnabled;
            double komisyonVolume = 0.0;
            double totalCommission = 0.0;
            double komisyonCarpan = this.status.KomisyonCarpan;

            if (this.signals.PrevYon == "A")
            {
                // A → F: Long pozisyonu KAPAT (1 işlem)
                // İşlem hacmi: PrevVarlikAdedSayisi

                double fark = this.signals.SonFiyat - this.signals.SonAFiyat;
                if (fark > 0)
                {
                    this.status.KazandiranAlisSayisi += 1;
                }
                else if (fark < 0)
                {
                    this.status.KaybettirenAlisSayisi += 1;
                }
                else
                {
                    this.status.NotrAlisSayisi += 1;
                }

                // 1 işlem: Kapatma
                this.status.KomisyonIslemSayisi += 1;
                this.signals.EmirStatus = 5;

                komisyonVolume = isMicroLot
                    ? this.status.KomisyonVarlikAdedSayisiMicro
                    : this.status.KomisyonVarlikAdedSayisi;

                // komisyon hesapla
                double closeCommission = komisyonCarpan * komisyonVolume;

                totalCommission = closeCommission;
            }
            if (this.signals.PrevYon == "S")
            {
                // S → F: Short pozisyonu KAPAT (1 işlem)
                // İşlem hacmi: PrevVarlikAdedSayisi

                double fark = this.signals.SonFiyat - this.signals.SonSFiyat;
                if (fark < 0)
                {
                    this.status.KazandiranSatisSayisi += 1;
                }
                else if (fark > 0)
                {
                    this.status.KaybettirenSatisSayisi += 1;
                }
                else
                {
                    this.status.NotrSatisSayisi += 1;
                }

                // 1 işlem: Kapatma
                this.status.KomisyonIslemSayisi += 1;
                this.signals.EmirStatus = 6;

                komisyonVolume = isMicroLot
                    ? this.status.KomisyonVarlikAdedSayisiMicro
                    : this.status.KomisyonVarlikAdedSayisi;

                // komisyon hesapla
                double closeCommission = komisyonCarpan * komisyonVolume;

                totalCommission = closeCommission;
            }

            this.flags.BakiyeGuncelle = true;
            this.flags.KomisyonGuncelle = true;
            this.flags.DonguSonuIstatistikGuncelle = true;
            this.status.IslemSayisi += 1;
            this.status.FlatSayisi += 1;
            this.flags.FGerceklesti = true;

            //OnNotifyStrategySignal?.Invoke(this, this.signals.Sinyal, i);

            // Mevcut pozisyon büyüklüğü
            double mevcutLot = isMicroLot
                ? this.signals.SonVarlikAdedSayisiMicro
                : this.signals.SonVarlikAdedSayisi;

            // Eklenecek lot büyüklüğü
            double yeniLot = isMicroLot
                ? this.initialTradeParams.VarlikAdedSayisiMicro
                : this.initialTradeParams.VarlikAdedSayisi;

            this.status.KomisyonFiyat += totalCommission;
            this.lists.KomisyonFiyatList[i] = this.status.KomisyonFiyat;

        }
        // Process "P" (PasGec/Skip) or empty signal
        else if (this.signals.Sinyal == "P" || this.signals.Sinyal == "")
        {
            this.signals.PrevPFiyat = this.signals.SonPFiyat;
            this.signals.PrevPBarNo = this.signals.SonPBarNo;
            this.signals.SonPFiyat = AnlikKapanisFiyati;
            this.signals.SonPBarNo = i;

            if (this.signals.SonYon == "A")
            {
                this.signals.EmirStatus = 7;
            }
            if (this.signals.SonYon == "S")
            {
                this.signals.EmirStatus = 8;
            }
            if (this.signals.SonYon == "F")
            {
                this.signals.EmirStatus = 9;
            }

            this.flags.BakiyeGuncelle = false; // "P" sinyali bakiye güncellemez, aksi halde yüzeysel kâr mükerrer eklenir (double-counting)
            this.flags.KomisyonGuncelle = true;
            this.flags.DonguSonuIstatistikGuncelle = true;
            this.status.PassSayisi += 1;
            this.flags.PGerceklesti = true;
        }
        // Process "A" (Al/Buy) signal - Pyramiding (Long pozisyon artırma)
        else if (this.signals.Sinyal == "A" && this.signals.SonYon == "A")
        {
            // Pyramiding etkin mi kontrol et
            if (!this.initialTradeParams.PyramidingEnabled)
            {
                // Pyramiding kapalı - işlem yapma, sinyali göz ardı et
                return result;
            }

            bool isMicroLot = this.initialTradeParams.MicroLotSizeEnabled;

            // Mevcut pozisyon büyüklüğü
            double mevcutLot = isMicroLot
                ? this.signals.SonVarlikAdedSayisiMicro
                : this.signals.SonVarlikAdedSayisi;

            // Eklenecek lot büyüklüğü
            double yeniLot = isMicroLot
                ? this.initialTradeParams.VarlikAdedSayisiMicro
                : this.initialTradeParams.VarlikAdedSayisi;

            // Maksimum pozisyon kontrolü
            if (this.initialTradeParams.MaxPositionSizeEnabled)
            {
                double maxLot = isMicroLot
                    ? this.initialTradeParams.MaxPositionSizeMicro
                    : this.initialTradeParams.MaxPositionSize;

                if (mevcutLot + yeniLot > maxLot)
                {
                    // Limit aşıldı - işlem yapma
                    return result;
                }
            }

            this.signals.PrevAFiyat = this.signals.SonAFiyat;
            this.signals.PrevABarNo = this.signals.SonABarNo;
            this.signals.PrevYon = this.signals.SonYon;
            this.signals.PrevFiyat = this.signals.SonFiyat;
            this.signals.PrevBarNo = this.signals.SonBarNo;

            // Prev değerlerini kaydet (komisyon hesabı için)
            this.signals.PrevVarlikAdedSayisi = this.signals.SonVarlikAdedSayisi;
            this.signals.PrevVarlikAdedSayisiMicro = this.signals.SonVarlikAdedSayisiMicro;

            // Yön ve sinyal bilgilerini güncelle
            this.lists.YonList[i] = "A";
            this.signals.SonYon = this.lists.YonList[i];

            // Ağırlıklı ortalama giriş fiyatı hesapla
            double eskiFiyat = this.signals.SonAFiyat;
            double yeniFiyat = AnlikKapanisFiyati;

            // Kayma (slippage) kontrolü - Long için yüksek fiyat (daha kötü)
            if (this.flags.KaymayiDahilEt)
            {
                yeniFiyat = AnlikYuksekFiyati;
            }

            double toplamLot = mevcutLot + yeniLot;
            double agirlikliOrtalamaFiyat = (mevcutLot * eskiFiyat + yeniLot * yeniFiyat) / toplamLot;

            // Pozisyon büyüklüğünü güncelle (toplama)
            if (isMicroLot)
                this.signals.SonVarlikAdedSayisiMicro = toplamLot;
            else
                this.signals.SonVarlikAdedSayisi = toplamLot;

            this.lists.SonVarlikAdedSayisiList[i] = this.signals.SonVarlikAdedSayisi;
            this.lists.SonVarlikAdedSayisiMicroList[i] = this.signals.SonVarlikAdedSayisiMicro;

            // Giriş fiyatını ağırlıklı ortalama ile güncelle
            this.signals.SonAFiyat = agirlikliOrtalamaFiyat;
            this.signals.SonABarNo = i;
            this.signals.SonFiyat = agirlikliOrtalamaFiyat;
            this.signals.SonBarNo = i;

            // Seviye listesini güncelle
            this.lists.SeviyeList[i] = this.signals.SonFiyat;

            // Komisyon işlem sayısı (sadece 1 işlem - ekleme)
            this.status.KomisyonIslemSayisi += 1;

            // EmirStatus = 10 (A→A: Long pozisyon artırma)
            this.signals.EmirStatus = 10;

            // Flags ve Status güncellemeleri
            this.flags.BakiyeGuncelle = false;  // Pozisyon kapatılmadı, kar/zarar gerçekleşmedi
            this.flags.KomisyonGuncelle = true;  // Komisyon ödendi
            this.flags.DonguSonuIstatistikGuncelle = true;  // İstatistik güncelle
            this.status.IslemSayisi += 1;  // Yeni işlem yapıldı
            this.status.AlisSayisi += 1;  // Alış işlemi
            this.flags.AGerceklesti = true;

            //OnNotifyStrategySignal?.Invoke(this, this.signals.Sinyal, i);

        }
        // Process "S" (Sat/Sell) signal - Pyramiding (Short pozisyon artırma)
        else if (this.signals.Sinyal == "S" && this.signals.SonYon == "S")
        {
            // Pyramiding etkin mi kontrol et
            if (!this.initialTradeParams.PyramidingEnabled)
            {
                // Pyramiding kapalı - işlem yapma, sinyali göz ardı et
                return result;
            }

            bool isMicroLot = this.initialTradeParams.MicroLotSizeEnabled;

            // Mevcut pozisyon büyüklüğü
            double mevcutLot = isMicroLot
                ? this.signals.SonVarlikAdedSayisiMicro
                : this.signals.SonVarlikAdedSayisi;

            // Eklenecek lot büyüklüğü
            double yeniLot = isMicroLot
                ? this.initialTradeParams.VarlikAdedSayisiMicro
                : this.initialTradeParams.VarlikAdedSayisi;

            // Maksimum pozisyon kontrolü
            if (this.initialTradeParams.MaxPositionSizeEnabled)
            {
                double maxLot = isMicroLot
                    ? this.initialTradeParams.MaxPositionSizeMicro
                    : this.initialTradeParams.MaxPositionSize;

                if (mevcutLot + yeniLot > maxLot)
                {
                    // Limit aşıldı - işlem yapma
                    return result;
                }
            }

            this.signals.PrevSFiyat = this.signals.SonSFiyat;
            this.signals.PrevSBarNo = this.signals.SonSBarNo;
            this.signals.PrevYon = this.signals.SonYon;
            this.signals.PrevFiyat = this.signals.SonFiyat;
            this.signals.PrevBarNo = this.signals.SonBarNo;

            // Prev değerlerini kaydet (komisyon hesabı için)
            this.signals.PrevVarlikAdedSayisi = this.signals.SonVarlikAdedSayisi;
            this.signals.PrevVarlikAdedSayisiMicro = this.signals.SonVarlikAdedSayisiMicro;

            // Yön ve sinyal bilgilerini güncelle
            this.lists.YonList[i] = "S";
            this.signals.SonYon = this.lists.YonList[i];

            // Ağırlıklı ortalama giriş fiyatı hesapla
            double eskiFiyat = this.signals.SonSFiyat;
            double yeniFiyat = AnlikKapanisFiyati;

            // Kayma (slippage) kontrolü - Short için düşük fiyat (daha kötü)
            if (this.flags.KaymayiDahilEt)
            {
                yeniFiyat = AnlikDusukFiyati;
            }

            double toplamLot = mevcutLot + yeniLot;
            double agirlikliOrtalamaFiyat = (mevcutLot * eskiFiyat + yeniLot * yeniFiyat) / toplamLot;

            // Pozisyon büyüklüğünü güncelle (toplama)
            if (isMicroLot)
                this.signals.SonVarlikAdedSayisiMicro = toplamLot;
            else
                this.signals.SonVarlikAdedSayisi = toplamLot;

            this.lists.SonVarlikAdedSayisiList[i] = this.signals.SonVarlikAdedSayisi;
            this.lists.SonVarlikAdedSayisiMicroList[i] = this.signals.SonVarlikAdedSayisiMicro;

            // Giriş fiyatını ağırlıklı ortalama ile güncelle
            this.signals.SonSFiyat = agirlikliOrtalamaFiyat;
            this.signals.SonSBarNo = i;
            this.signals.SonFiyat = agirlikliOrtalamaFiyat;
            this.signals.SonBarNo = i;

            // Seviye listesini güncelle
            this.lists.SeviyeList[i] = this.signals.SonFiyat;

            // Komisyon işlem sayısı (sadece 1 işlem - ekleme)
            this.status.KomisyonIslemSayisi += 1;

            // EmirStatus = 11 (S→S: Short pozisyon artırma)
            this.signals.EmirStatus = 11;

            // Flags ve Status güncellemeleri
            this.flags.BakiyeGuncelle = false;  // Pozisyon kapatılmadı, kar/zarar gerçekleşmedi
            this.flags.KomisyonGuncelle = true;  // Komisyon ödendi
            this.flags.DonguSonuIstatistikGuncelle = true;  // İstatistik güncelle
            this.status.IslemSayisi += 1;  // Yeni işlem yapıldı
            this.status.SatisSayisi += 1;  // Satış işlemi
            this.flags.SGerceklesti = true;

            //OnNotifyStrategySignal?.Invoke(this, this.signals.Sinyal, i);
        }
        // Process "F" (Flat) signal - Zaten Flat
        else if (this.signals.Sinyal == "F" && this.signals.SonYon == "F")
        {
            // Zaten Flat durumdayız ve yeni sinyal de Flat
            // Hiçbir işlem yapılmaz, EmirStatus güncellenmez
            // Bu durum normal akış içinde yer alır
        }

        // ------------------------------------------------------------------------------
        // Reset flags
        this.flags.AGerceklesti = false;
        this.flags.SGerceklesti = false;
        this.flags.FGerceklesti = false;
        this.flags.PGerceklesti = false;

        return result;
    }

    public void ResetVariablesOnNewIteration(int barIndex)
    {
        int i = barIndex;

        if (this.ExecutionStepNumber == 0)
        {

        }
        this.lists.BarIndexList[i] = i;
        this.lists.YonList[i] = "";
        this.lists.SeviyeList[i] = 0.0;
        this.lists.SinyalList[i] = 0.0;
        this.lists.KarZararPuanList[i] = 0.0;
        this.lists.KarZararFiyatList[i] = 0.0;
        this.lists.KarZararPuanYuzdeList[i] = 0.0;
        this.lists.KarZararFiyatYuzdeList[i] = 0.0;
        this.status.KarZararPuan = 0.0;
        this.status.KarZararFiyat = 0.0;
        this.status.KarZararPuanYuzde = 0.0;
        this.status.KarZararFiyatYuzde = 0.0;
        this.lists.KarAlList[i] = 0.0;
        this.lists.ZararKesList[i] = 0.0;
        this.lists.IzleyenStopList[i] = 0.0;
        this.lists.IslemSayisiList[i] = 0;
        this.lists.AlisSayisiList[i] = 0;
        this.lists.SatisSayisiList[i] = 0;
        this.lists.FlatSayisiList[i] = 0;
        this.lists.PassSayisiList[i] = 0;
        this.lists.KontratSayisiList[i] = 0;
        this.lists.VarlikAdedSayisiList[i] = 0;
        this.lists.VarlikAdedSayisiMicroList[i] = 0;
        this.lists.SonVarlikAdedSayisiList[i] = 0;
        this.lists.SonVarlikAdedSayisiMicroList[i] = 0;
        this.lists.KomisyonVarlikAdedSayisiList[i] = 0;
        this.lists.KomisyonVarlikAdedSayisiMicroList[i] = 0;
        this.lists.KomisyonIslemSayisiList[i] = 0;
        this.lists.KomisyonFiyatList[i] = 0.0;
        this.lists.KardaBarSayisiList[i] = 0;
        this.lists.ZarardaBarSayisiList[i] = 0;
        this.lists.BakiyeFiyatList[i] = this.status.BakiyeFiyat;
        this.lists.GetiriFiyatList[i] = this.lists.BakiyeFiyatList[i] - this.status.BakiyeFiyat;
        this.lists.BakiyePuanList[i] = this.status.BakiyePuan;
        this.lists.GetiriPuanList[i] = this.lists.BakiyePuanList[i] - this.status.BakiyePuan;
        this.lists.EmirKomutList[i] = 0;
        this.lists.EmirStatusList[i] = 0;
        this.ExecutionStepNumber += 1;
        this.lists.IsTradeEnabledList[i] = 0;
        this.lists.IsPozKapatEnabledList[i] = 0;
    }

    public void UpdateVariablesOnNewIteration(int barIndex)
    {
        int i = barIndex;

        this.status.KomisyonVarlikAdedSayisi = this.initialTradeParams.KomisyonVarlikAdedSayisi;
        this.status.KomisyonVarlikAdedSayisiMicro = this.initialTradeParams.KomisyonVarlikAdedSayisiMicro;
        this.status.KomisyonCarpan = this.initialTradeParams.KomisyonCarpan;
        this.flags.KomisyonuDahilEt = this.initialTradeParams.KomisyonuDahilEt;
        this.status.KaymaMiktari = this.initialTradeParams.KaymaMiktari;
        this.flags.KaymayiDahilEt = this.initialTradeParams.KaymayiDahilEt;
        this.status.VarlikAdedSayisi = this.initialTradeParams.VarlikAdedSayisi;
        this.status.VarlikAdedSayisiMicro = this.initialTradeParams.VarlikAdedSayisiMicro;
        this.status.VarlikAdedCarpani = this.initialTradeParams.VarlikAdedCarpani;
        this.status.KontratSayisi = this.initialTradeParams.KontratSayisi;
        this.status.HisseSayisi = this.initialTradeParams.HisseSayisi;
        this.status.IlkBakiyeFiyat = this.initialTradeParams.IlkBakiyeFiyat;
        this.status.IlkBakiyePuan = this.initialTradeParams.IlkBakiyePuan;
        this.status.GetiriFiyatTipi = this.initialTradeParams.GetiriFiyatTipi;
        this.status.MicroLotSizeEnabled = this.initialTradeParams.MicroLotSizeEnabled;
        if (this.BakiyeInitialized == false)
        {
            this.BakiyeInitialized = true;
            this.status.BakiyeFiyat = this.status.IlkBakiyeFiyat;
            this.status.BakiyePuan = this.status.IlkBakiyePuan;
            this.lists.BakiyeFiyatList[i] = this.status.BakiyeFiyat;
            this.lists.GetiriFiyatList[i] = this.lists.BakiyeFiyatList[i] - this.status.BakiyeFiyat;
            this.lists.BakiyePuanList[i] = this.status.BakiyePuan;
            this.lists.GetiriPuanList[i] = this.lists.BakiyePuanList[i] - this.status.BakiyePuan;
        }
    }

    public void ResetTradeCommands()
    {
        this.signals.None = this.signals.Al = this.signals.Sat = this.signals.FlatOl = this.signals.KarAl = this.signals.ZararKes = this.signals.PasGec = false;
        this.signals.Sinyal = "";
    }
    
    public double CalculateUnrealizedPnL(int barIndex)
    {
        double result = 0.0;

        int i = barIndex;
        string type = "C";

        if (this.initialTradeParams.MicroLotSizeEnabled)
        {
            result = _calculateUnrealizedPnLMicro(barIndex, type);
        }
        else
        {
            result = _calculateUnrealizedPnL(barIndex, type);
        }

        return result;
    }
    /// <summary>
    /// Anlık kar/zarar hesaplama - Micro version (FX, Crypto için)
    /// Kesirli varlık adedi (0.01 lot, 0.1 lot, vb.) kullanır
    /// </summary>
    public double _calculateUnrealizedPnLMicro(int barIndex, string type = "C")
    {
        // Validate dependencies
        if (Data == null || flags == null || signals == null || status == null || lists == null || initialTradeParams == null)
            return 0.0;

        // Validate bar index
        if (barIndex < 0 || barIndex >= Data.Count)
            return 0.0;

        double result = 0.0;
        int i = barIndex;

        // Get current price based on type
        double anlikFiyat = Data[i].Close;
        bool anlikKarZararHesaplaEnabled = flags.AnlikKarZararHesaplaEnabled;
        string sonYon = signals.SonYon;
        double sonFiyat = signals.SonFiyat;
        // Dinamik lot desteği: Son açılan pozisyonun büyüklüğünü kullan (MICRO)
        double varlikAdedSayisi = signals.SonVarlikAdedSayisiMicro;

        if (!anlikKarZararHesaplaEnabled)
            return result;

        // Select price based on type
        if (type != "C")
        {
            if (type == "O")
                anlikFiyat = Data[i].Open;
            else if (type == "H")
                anlikFiyat = Data[i].High;
            else if (type == "L")
                anlikFiyat = Data[i].Low;
        }

        // Calculate profit/loss based on position direction
        if (sonYon == "A")  // Long position (Al - Buy)
        {
            status.KarZararPuan = anlikFiyat - sonFiyat;
            status.KarZararFiyat = status.KarZararPuan * varlikAdedSayisi;
            lists.KarZararPuanList[i] = status.KarZararPuan;
            lists.KarZararFiyatList[i] = status.KarZararFiyat;

            if (sonFiyat != 0)
                status.KarZararFiyatYuzde = 100.0 * status.KarZararPuan / sonFiyat;
            else
                status.KarZararFiyatYuzde = 0.0;

            lists.KarZararFiyatYuzdeList[i] = status.KarZararFiyatYuzde;

            status.KarZararPuanYuzde = status.KarZararFiyatYuzde;
            lists.KarZararPuanYuzdeList[i] = status.KarZararPuanYuzde;
        }
        else if (sonYon == "S")  // Short position (Sat - Sell)
        {
            status.KarZararPuan = sonFiyat - anlikFiyat;
            status.KarZararFiyat = status.KarZararPuan * varlikAdedSayisi;
            lists.KarZararPuanList[i] = status.KarZararPuan;
            lists.KarZararFiyatList[i] = status.KarZararFiyat;

            if (sonFiyat != 0)
                status.KarZararFiyatYuzde = 100.0 * status.KarZararPuan / sonFiyat;
            else
                status.KarZararFiyatYuzde = 0.0;

            lists.KarZararFiyatYuzdeList[i] = status.KarZararFiyatYuzde;

            status.KarZararPuanYuzde = status.KarZararFiyatYuzde;
            lists.KarZararPuanYuzdeList[i] = status.KarZararPuanYuzde;
        }

        // Update bar count statistics
        if (status.KarZararPuan > 0)
        {
            status.KardaBarSayisi += 1;
            status.ZarardaBarSayisi -= 1;
        }
        else if (status.KarZararPuan == 0)
        {
            status.KardaBarSayisi = 0;
            status.ZarardaBarSayisi = 0;
        }
        else  // KarZararPuan < 0
        {
            status.KardaBarSayisi -= 1;
            status.ZarardaBarSayisi += 1;
        }

        return result;
    }
    /// <summary>
    /// Anlık kar/zarar hesaplama - Normal version (BIST, VIOP için)
    /// Integer varlık adedi kullanır
    /// </summary>
    public double _calculateUnrealizedPnL(int barIndex, string type = "C")
    {
        // Validate dependencies
        if (Data == null || flags == null || signals == null || status == null || lists == null || initialTradeParams == null)
            return 0.0;

        // Validate bar index
        if (barIndex < 0 || barIndex >= Data.Count)
            return 0.0;

        double result = 0.0;
        int i = barIndex;

        // Get current price based on type
        double anlikFiyat = Data[i].Close;
        bool anlikKarZararHesaplaEnabled = flags.AnlikKarZararHesaplaEnabled;
        string sonYon = signals.SonYon;
        double sonFiyat = signals.SonFiyat;
        // Dinamik lot desteği: Son açılan pozisyonun büyüklüğünü kullan
        double varlikAdedSayisi = signals.SonVarlikAdedSayisi;

        if (!anlikKarZararHesaplaEnabled)
            return result;

        // Select price based on type
        if (type != "C")
        {
            if (type == "O")
                anlikFiyat = Data[i].Open;
            else if (type == "H")
                anlikFiyat = Data[i].High;
            else if (type == "L")
                anlikFiyat = Data[i].Low;
        }

        // Calculate profit/loss based on position direction
        if (sonYon == "A")  // Long position (Al - Buy)
        {
            status.KarZararPuan = anlikFiyat - sonFiyat;
            status.KarZararFiyat = status.KarZararPuan * varlikAdedSayisi;
            lists.KarZararPuanList[i] = status.KarZararPuan;
            lists.KarZararFiyatList[i] = status.KarZararFiyat;

            if (sonFiyat != 0)
                status.KarZararFiyatYuzde = 100.0 * status.KarZararPuan / sonFiyat;
            else
                status.KarZararFiyatYuzde = 0.0;

            lists.KarZararFiyatYuzdeList[i] = status.KarZararFiyatYuzde;

            status.KarZararPuanYuzde = status.KarZararFiyatYuzde;
            lists.KarZararPuanYuzdeList[i] = status.KarZararPuanYuzde;
        }
        else if (sonYon == "S")  // Short position (Sat - Sell)
        {
            status.KarZararPuan = sonFiyat - anlikFiyat;
            status.KarZararFiyat = status.KarZararPuan * varlikAdedSayisi;
            lists.KarZararPuanList[i] = status.KarZararPuan;
            lists.KarZararFiyatList[i] = status.KarZararFiyat;

            if (sonFiyat != 0)
                status.KarZararFiyatYuzde = 100.0 * status.KarZararPuan / sonFiyat;
            else
                status.KarZararFiyatYuzde = 0.0;

            lists.KarZararFiyatYuzdeList[i] = status.KarZararFiyatYuzde;

            status.KarZararPuanYuzde = status.KarZararFiyatYuzde;
            lists.KarZararPuanYuzdeList[i] = status.KarZararPuanYuzde;
        }

        // Update bar count statistics
        if (status.KarZararPuan > 0)
        {
            status.KardaBarSayisi += 1;
            status.ZarardaBarSayisi -= 1;
        }
        else if (status.KarZararPuan == 0)
        {
            status.KardaBarSayisi = 0;
            status.ZarardaBarSayisi = 0;
        }
        else  // KarZararPuan < 0
        {
            status.KardaBarSayisi -= 1;
            status.ZarardaBarSayisi += 1;
        }

        return result;
    }
    
    public void ExecutePreOrderMethods(int barIndex)
    {
        int i = barIndex;

        ResetVariablesOnNewIteration(i);

        UpdateVariablesOnNewIteration(i);

        if (i < 1)
            return;

        CalculateUnrealizedPnL(i);

        ResetTradeCommands();

        if (this.signals.IsTradeEnabled)
            this.signals.IsTradeEnabled = false;

        if (this.signals.IsTimingFiltersTradeEnabled)
            this.signals.IsTimingFiltersTradeEnabled = false;

        if (this.signals.IsEquityCurveTradeEnabled)
            this.signals.IsEquityCurveTradeEnabled = false;

        if (this.signals.IsPozKapatEnabled)
            this.signals.IsPozKapatEnabled = false;

        if (this.signals.GunSonuPozKapatildi)
            this.signals.GunSonuPozKapatildi = false;

        if (this.signals.KarAlindi || this.signals.ZararKesildi || this.signals.FlatOlundu)
        {
            this.signals.KarAlindi = false;
            this.signals.ZararKesildi = false;
            this.signals.FlatOlundu = false;
            this.signals.PozAcilabilir = false;
        }

        if (this.signals.PozAcilabilir == false)
        {
            this.signals.PozAcilabilir = true;
            this.signals.PozAcildi = false;
        }

        this.signals.IsTradeEnabled = true;
    }

    public void ExecutePostOrderMethods(int barIndex)
    {
        int i = barIndex;

        // ----------------------------------------------------------------------------
        // TODO: İki filtre birlikte çalışınca aktif et
        // this.signals.IsTradeEnabled = this.signals.IsTimingFiltersTradeEnabled && this.signals.IsEquityCurveTradeEnabled;

        // ----------------------------------------------------------------------------
        OnBeforeOrder?.Invoke(this, barIndex);

        // ----------------------------------------------------------------------------
        ExecuteOrders(i);   // TODO: Strategy evaluate, sinyal üret, emir uygula

        // ----------------------------------------------------------------------------
        OnAfterOrder?.Invoke(this, barIndex);

        // ----------------------------------------------------------------------------
        if (this.signals.KarAlindi == false && this.signals.KarAl)
            this.signals.KarAlindi = true;

        if (this.signals.ZararKesildi == false && this.signals.ZararKes)
            this.signals.ZararKesildi = true;

        if (this.signals.FlatOlundu == false && this.signals.FlatOl)
            this.signals.FlatOlundu = true;

        // ----------------------------------------------------------------------------
        // Sistem.Yon[i] = self.Lists.YonList[i]
        // Sistem.Seviye[i] = self.Lists.SeviyeList[i]

        // ----------------------------------------------------------------------------
        if (this.signals.SonYon == "A")
        {
            this.lists.SinyalList[i] = 1.0;
        }
        else if (this.signals.SonYon == "S")
        {
            this.lists.SinyalList[i] = -1.0;
        }
        else if (this.signals.SonYon == "F")
        {
            this.lists.SinyalList[i] = 0.0;
        }

        // ----------------------------------------------------------------------------
        this.lists.IslemSayisiList[i] = this.status.IslemSayisi;
        this.lists.AlisSayisiList[i] = this.status.AlisSayisi;
        this.lists.SatisSayisiList[i] = this.status.SatisSayisi;
        this.lists.FlatSayisiList[i] = this.status.FlatSayisi;
        this.lists.PassSayisiList[i] = this.status.PassSayisi;
        this.lists.VarlikAdedSayisiList[i] = this.status.VarlikAdedSayisi;
        this.lists.VarlikAdedSayisiMicroList[i] = this.status.VarlikAdedSayisiMicro;
        this.lists.KontratSayisiList[i] = this.status.KontratSayisi;
        this.lists.KomisyonVarlikAdedSayisiList[i] = this.status.KomisyonVarlikAdedSayisi;
        this.lists.KomisyonVarlikAdedSayisiMicroList[i] = this.status.KomisyonVarlikAdedSayisiMicro;
        this.lists.KomisyonIslemSayisiList[i] = this.status.KomisyonIslemSayisi;
        this.lists.KomisyonFiyatList[i] = this.status.KomisyonFiyat;
        this.lists.KardaBarSayisiList[i] = this.status.KardaBarSayisi;
        this.lists.ZarardaBarSayisiList[i] = this.status.ZarardaBarSayisi;

        // ----------------------------------------------------------------------------
        CalculateBalance(i);

        // ----------------------------------------------------------------------------
        if (this.signals.IsTradeEnabled)
        {
            this.signals.IsTradeEnabled = true;
        }
        else
        {
            this.signals.IsTradeEnabled = false;
        }
        this.lists.IsTradeEnabledList[i] = this.signals.IsTradeEnabled ? 1 : 0;
        this.lists.IsPozKapatEnabledList[i] = this.signals.IsPozKapatEnabled ? 1 : 0;
    }
    
    public double CalculateBalance(int barIndex)
    {
        double result = 0.0;

        int i = barIndex;

        // Bakiye (Puan)
        this.lists.BakiyePuanList[i] = this.status.BakiyePuan + this.lists.KarZararPuanList[i];
        this.lists.GetiriPuanList[i] = this.lists.BakiyePuanList[i] - this.status.IlkBakiyePuan;

        if (this.flags.BakiyeGuncelle)
        {
            this.status.BakiyePuan = this.lists.BakiyePuanList[i];
            this.status.GetiriPuan = this.lists.GetiriPuanList[i];

            if (this.lists.KarZararPuanList[i] >= 0)
            {
                this.status.ToplamKarPuan += this.lists.KarZararPuanList[i];
            }
            else if (this.lists.KarZararPuanList[i] < 0)
            {
                this.status.ToplamZararPuan += this.lists.KarZararPuanList[i];
            }

            this.status.NetKarPuan = this.status.ToplamKarPuan + this.status.ToplamZararPuan;
        }

        // Bakiye (Fiyat)
        this.lists.BakiyeFiyatList[i] = this.status.BakiyeFiyat + this.lists.KarZararFiyatList[i];
        this.lists.GetiriFiyatList[i] = this.lists.BakiyeFiyatList[i] - this.status.IlkBakiyeFiyat;

        if (this.flags.BakiyeGuncelle)
        {
            this.status.BakiyeFiyat = this.lists.BakiyeFiyatList[i];
            this.status.GetiriFiyat = this.lists.GetiriFiyatList[i];

            if (this.lists.KarZararFiyatList[i] >= 0)
            {
                this.status.ToplamKarFiyat += this.lists.KarZararFiyatList[i];
            }
            else if (this.lists.KarZararFiyatList[i] < 0)
            {
                this.status.ToplamZararFiyat += this.lists.KarZararFiyatList[i];
            }

            this.status.NetKarFiyat = this.status.ToplamKarFiyat + this.status.ToplamZararFiyat;
        }

        // Yüzde hesaplamaları (Puan)
        if (this.status.IlkBakiyePuan != 0.0)
        {
            this.lists.GetiriPuanYuzdeList[i] = 100.0 * this.lists.GetiriPuanList[i] / this.status.IlkBakiyePuan;
        }
        else
        {
            this.lists.GetiriPuanYuzdeList[i] = 0.0;
        }

        // Yüzde hesaplamaları (Fiyat)
        if (this.status.IlkBakiyeFiyat != 0.0)
        {
            this.lists.GetiriFiyatYuzdeList[i] = 100.0 * this.lists.GetiriFiyatList[i] / this.status.IlkBakiyeFiyat;
        }
        else
        {
            this.lists.GetiriFiyatYuzdeList[i] = 0.0;
        }

        if (this.flags.BakiyeGuncelle)
        {
            this.status.GetiriPuanYuzde = this.lists.GetiriPuanYuzdeList[i];
            this.status.GetiriFiyatYuzde = this.lists.GetiriFiyatYuzdeList[i];
        }

        // Net hesaplamalar (komisyon dahil)
        double k = this.status.KomisyonCarpan != 0.0 ? 1.0 : 0.0;

        this.lists.GetiriFiyatNetList[i] = this.lists.GetiriFiyatList[i] - this.lists.KomisyonFiyatList[i] * k;
        this.lists.BakiyeFiyatNetList[i] = this.lists.GetiriFiyatNetList[i] + this.status.IlkBakiyeFiyat;

        this.lists.GetiriFiyatYuzdeNetList[i] = 0.0;
        if (this.status.IlkBakiyeFiyat != 0.0)
        {
            this.lists.GetiriFiyatYuzdeNetList[i] = 100.0 * this.lists.GetiriFiyatNetList[i] / this.status.IlkBakiyeFiyat;
        }

        // Dinamik lot büyüklüğünü kullan
        bool isMicroLot = this.initialTradeParams.MicroLotSizeEnabled;
        double varlikAdedSayisi = isMicroLot
            ? this.signals.SonVarlikAdedSayisiMicro
            : this.signals.SonVarlikAdedSayisi;

        // Sıfıra bölme kontrolü
        if (varlikAdedSayisi != 0)
        {
            this.lists.GetiriKz[i] = this.lists.GetiriFiyatList[i] / varlikAdedSayisi;
            this.lists.GetiriKzNet[i] = this.lists.GetiriFiyatNetList[i] / varlikAdedSayisi;
        }
        else
        {
            // Pozisyon yoksa (Flat), getiri var ama lot yok
            // Bir önceki lot büyüklüğünü kullan (eğer varsa)
            double prevVolume = isMicroLot
                ? this.signals.PrevVarlikAdedSayisiMicro
                : this.signals.PrevVarlikAdedSayisi;

            if (prevVolume != 0)
            {
                this.lists.GetiriKz[i] = this.lists.GetiriFiyatList[i] / prevVolume;
                this.lists.GetiriKzNet[i] = this.lists.GetiriFiyatNetList[i] / prevVolume;
            }
            else
            {
                this.lists.GetiriKz[i] = 0.0;
                this.lists.GetiriKzNet[i] = 0.0;
            }
        }

        // Son bar kontrolü
        int barCount = this.Data.Count;
        if (i == barCount - 1)
        {
            this.status.BakiyeFiyat = this.lists.BakiyeFiyatList[barCount - 1];
            this.status.GetiriFiyat = this.lists.GetiriFiyatList[barCount - 1];
            this.status.GetiriKz = this.lists.GetiriKz[barCount - 1];
            this.status.GetiriFiyatYuzde = this.lists.GetiriFiyatYuzdeList[barCount - 1];
            this.status.BakiyeFiyatNet = this.lists.BakiyeFiyatNetList[barCount - 1];
            this.status.GetiriFiyatNet = this.lists.GetiriFiyatNetList[barCount - 1];
            this.status.GetiriKzNet = this.lists.GetiriKzNet[barCount - 1];
            this.status.GetiriFiyatYuzdeNet = this.lists.GetiriFiyatYuzdeNetList[barCount - 1];
            this.status.BakiyePuan = this.lists.BakiyePuanList[barCount - 1];
            this.status.GetiriPuan = this.lists.GetiriPuanList[barCount - 1];
            this.status.BakiyePuanNet = this.lists.BakiyePuanNetList[barCount - 1];
            this.status.GetiriPuanNet = this.lists.GetiriPuanNetList[barCount - 1];
            this.status.GetiriPuanYuzdeNet = this.lists.GetiriPuanYuzdeNetList[barCount - 1];
        }

        return result;
    }

    public int CheckOrderTimeEligibility(int BarIndex, int FilterMode, ref bool IsTradeEnabled, ref bool IsPozKapatEnabled, ref int CheckResult)
    {
        int i = BarIndex;
        DateTime BarDateTime = this.Data[i].DateTime;
        string startDateTime = this.StartDateTimeStr ?? "";
        string stopDateTime = this.StopDateTimeStr ?? "";
        string startDate = this.StartDateStr ?? "";
        string stopDate = this.StopDateStr ?? "";
        string startTime = this.StartTimeStr ?? "";
        string stopTime = this.StopTimeStr ?? "";

        DateTime now = DateTime.Now;
        string nowDateTime = now.ToString("yyyy.MM.dd HH:mm:ss");
        string nowDate = now.ToString("yyyy.MM.dd");
        string nowTime = now.ToString("HH:mm:ss");

        bool useTimeFiltering = this.signals.TimeFilteringEnabled;

        if (useTimeFiltering)
        {
            if (i == this.Data.Count - 1)
            {
                string s = "";
                s += $"  {startDateTime}\n";
                s += $"  {stopDateTime}\n";
                s += $"  {startDate}\n";
                s += $"  {stopDate}\n";
                s += $"  {startTime}\n";
                s += $"  {stopTime}\n";
                s += $"  {nowDateTime}\n";
                s += $"  {nowDate}\n";
                s += $"  {nowTime}\n";
                s += $"  FilterMode = {FilterMode}\n";
                s += "  CTrader::IslemZamanFiltresiUygula\n";
                // Log if needed
            }

            if (FilterMode == 0)
            {
                IsTradeEnabled = true;
                CheckResult = 0;
            }
            else if (FilterMode == 1)
            {
                if (this.timeUtils.check_bar_time_with(i, startTime) >= 0 && this.timeUtils.check_bar_time_with(i, stopTime) < 0)
                {
                    IsTradeEnabled = true;
                    CheckResult = 0;
                }
                else if (this.timeUtils.check_bar_time_with(i, startTime) < 0)
                {
                    if (!this.is_son_yon_f())
                    {
                        IsPozKapatEnabled = true;
                    }
                    CheckResult = -1;
                }
                else if (this.timeUtils.check_bar_time_with(i, stopTime) >= 0)
                {
                    if (!this.is_son_yon_f())
                    {
                        IsPozKapatEnabled = true;
                    }
                    CheckResult = 1;
                }
            }
            else if (FilterMode == 2)
            {
                if (this.timeUtils.check_bar_date_with(i, startDate) >= 0 && this.timeUtils.check_bar_date_with(i, stopDate) < 0)
                {
                    IsTradeEnabled = true;
                    CheckResult = 0;
                }
                else if (this.timeUtils.check_bar_date_with(i, startDate) < 0)
                {
                    if (!this.is_son_yon_f())
                    {
                        IsPozKapatEnabled = true;
                    }
                    CheckResult = -1;
                }
                else if (this.timeUtils.check_bar_date_with(i, stopDate) >= 0)
                {
                    if (!this.is_son_yon_f())
                    {
                        IsPozKapatEnabled = true;
                    }
                    CheckResult = 1;
                }
            }
            else if (FilterMode == 3)
            {
                if (this.timeUtils.check_bar_date_time_with(i, startDateTime) >= 0 && this.timeUtils.check_bar_date_time_with(i, stopDateTime) < 0)
                {
                    IsTradeEnabled = true;
                    CheckResult = 0;
                }
                else if (this.timeUtils.check_bar_date_time_with(i, startDateTime) < 0)
                {
                    if (!this.is_son_yon_f())
                    {
                        IsPozKapatEnabled = true;
                    }
                    CheckResult = -1;
                }
                else if (this.timeUtils.check_bar_date_time_with(i, stopDateTime) >= 0)
                {
                    if (!this.is_son_yon_f())
                    {
                        IsPozKapatEnabled = true;
                    }
                    CheckResult = 1;
                }
            }
            else if (FilterMode == 4)
            {
                if (this.timeUtils.check_bar_time_with(i, startTime) >= 0)
                {
                    IsTradeEnabled = true;
                    CheckResult = 0;
                }
                else if (this.timeUtils.check_bar_time_with(i, startTime) < 0)
                {
                    if (!this.is_son_yon_f())
                    {
                        IsPozKapatEnabled = true;
                    }
                    CheckResult = -1;
                }
            }
            else if (FilterMode == 5)
            {
                if (this.timeUtils.check_bar_date_with(i, startDate) >= 0)
                {
                    IsTradeEnabled = true;
                    CheckResult = 0;
                }
                else if (this.timeUtils.check_bar_date_with(i, startDate) < 0)
                {
                    if (!this.is_son_yon_f())
                    {
                        IsPozKapatEnabled = true;
                    }
                    CheckResult = -1;
                }
            }
            else if (FilterMode == 6)
            {
                if (this.timeUtils.check_bar_date_time_with(i, startDateTime) >= 0)
                {
                    IsTradeEnabled = true;
                    CheckResult = 0;
                }
                else if (this.timeUtils.check_bar_date_time_with(i, startDateTime) < 0)
                {
                    if (!this.is_son_yon_f())
                    {
                        IsPozKapatEnabled = true;
                    }
                    CheckResult = -1;
                }
            }
        }

        return 0;
    }
    
    public void ApplyTimingFilters(int barIndex)
    {
        int i = barIndex;

        bool useTimeFiltering = this.signals.TimeFilteringEnabled;
        if (useTimeFiltering)
        {
            int filterMode = 1;
            bool isTradeEnabled = false;
            bool isPozKapatEnabled = false;
            int checkResult = 0;

            CheckOrderTimeEligibility(i, filterMode, ref isTradeEnabled, ref isPozKapatEnabled, ref checkResult);

            this.signals.IsTimingFiltersTradeEnabled = isTradeEnabled;
            this.signals.IsTradeEnabled = isTradeEnabled;
            this.signals.IsPozKapatEnabled = isPozKapatEnabled;
        }

        // --------------------------------------------------------------------------------------------------------------------------------------------
        var isSonYonA = is_son_yon_a();

        var isSonYonS = is_son_yon_s();

        var isSonYonF = is_son_yon_f();

        // --------------------------------------------------------------------------------------------------------------------------------------------
        if (this.signals.IsPozKapatEnabled || this.signals.GunSonuPozKapatEnabled)
        {
            if (this.signals.IsPozKapatEnabled)
            {
                if (isSonYonF)
                {
                    this.signals.None = this.signals.Al = this.signals.Sat = this.signals.FlatOl = this.signals.KarAl = this.signals.ZararKes = this.signals.PasGec = false;
                    this.signals.None = true;
                }
                else
                {
                    this.signals.None = this.signals.Al = this.signals.Sat = this.signals.FlatOl = this.signals.KarAl = this.signals.ZararKes = this.signals.PasGec = false;
                    this.signals.FlatOl = true;
                }
            }
            else if (this.signals.GunSonuPozKapatEnabled)
            {
                bool pozKapat = this.ClosePositionEOD(i, this.signals.GunSonuPozKapatEnabled);
                if (pozKapat)
                {
                    if (isSonYonF)
                    {
                        this.signals.None = this.signals.Al = this.signals.Sat = this.signals.FlatOl = this.signals.KarAl = this.signals.ZararKes = this.signals.PasGec = false;
                        this.signals.None = true;
                    }
                    else
                    {
                        this.signals.None = this.signals.Al = this.signals.Sat = this.signals.FlatOl = this.signals.KarAl = this.signals.ZararKes = this.signals.PasGec = false;
                        this.signals.FlatOl = true;
                        this.signals.GunSonuPozKapatildi = true;
                    }
                }
            }
        }
        else
        {
            if (!this.signals.IsTradeEnabled)
            {
                // Zaman filtresi trade'e izin vermiyor, emirleri iptal et
                this.signals.None = this.signals.Al = this.signals.Sat = this.signals.FlatOl = this.signals.KarAl = this.signals.ZararKes = this.signals.PasGec = false;
                this.signals.None = true;
            }
        }
    }
    public void ConfigureEquityCurveFilter(bool isPercent, double profitThreshold, double lossThreshold, ConfirmationTrigger trigger)
    {
        this.thresholdTypeIsPercent = isPercent;
        this.profitConfirmationThreshold = profitThreshold;
        this.lossConfirmationThreshold = lossThreshold;
        this.confirmationTrigger = trigger;
        this._equityCurveConfirmed = false;
    }

    public void ApplyEquityCurveFilter(int barIndex)
    {
        int i = barIndex;

        bool useEquityCurveFilteringEnabled = this.signals.EquityCurveFilteringEnabled;
        if (!useEquityCurveFilteringEnabled)
            return;

        // Adım 1:
        // Mevcut yön belirleme
        var isSonYonA = this.is_son_yon_a();
        var isSonYonS = this.is_son_yon_s();
        var isSonYonF = this.is_son_yon_f();

        // Önceki yön belirleme
        var isPrevYonA = this.is_prev_yon_a();
        var isPrevYonS = this.is_prev_yon_s();
        var isPrevYonF = this.is_prev_yon_f();

        string currentYon = "F";
        if (isSonYonA) currentYon = "A";
        else if (isSonYonS) currentYon = "S";

        // Adım 2: Önceki yön ile karşılaştır - yön değişti mi?
        string previousYon = "F";
        if (is_prev_yon_a()) previousYon = "A";
        else if (is_prev_yon_s()) previousYon = "S";

        bool yonDegisti = (currentYon != previousYon);

        // Adım 3: Yön değiştiyse confirmed durumunu sıfırla
        if (yonDegisti)
        {
            _equityCurveConfirmed = false;
        }

        // Adım 4: FLAT ise - direkt geç, confirmed = false
        if (isSonYonF)
        {
            _equityCurveConfirmed = false;
            return;
        }

        // Adım 5: LONG veya SHORT - Eşik kontrolü (sadece confirmed değilse)
        if ((isSonYonA || isSonYonS) && !_equityCurveConfirmed)
        {
            double karZararFiyat = this.status.KarZararFiyat;
            double karZararYuzde = this.status.KarZararFiyatYuzde;

            bool karTetiklendi = false;
            bool zararTetiklendi = false;

            if (thresholdTypeIsPercent)
            {
                karTetiklendi = karZararYuzde >= profitConfirmationThreshold;
                zararTetiklendi = karZararYuzde <= lossConfirmationThreshold;
            }
            else
            {
                karTetiklendi = karZararFiyat >= profitConfirmationThreshold;
                zararTetiklendi = karZararFiyat <= lossConfirmationThreshold;
            }

            bool esikGecildi = false;
            switch (confirmationTrigger)
            {
                case ConfirmationTrigger.ProfitOnly:
                    esikGecildi = karTetiklendi;
                    break;
                case ConfirmationTrigger.LossOnly:
                    esikGecildi = zararTetiklendi;
                    break;
                case ConfirmationTrigger.Both:
                    esikGecildi = karTetiklendi || zararTetiklendi;
                    break;
            }

            if (esikGecildi)
            {
                // Eşik geçildi - sinyal onaylandı
                _equityCurveConfirmed = true;
                this.signals.IsEquityCurveTradeEnabled = true;
            }
            else
            {
                // Eşik geçilmedi - sadece giriş sinyallerini iptal et
                // Çıkış sinyallerine (FlatOl/KarAl/ZararKes) dokunma
                this.signals.Al = false;
                this.signals.Sat = false;
                this.signals.None = true;
                this.signals.IsEquityCurveTradeEnabled = false;
            }

            // TODO: İki filtre birlikte çalışınca aktif et
            // this.signals.IsTradeEnabled = this.signals.IsTimingFiltersTradeEnabled && this.signals.IsEquityCurveTradeEnabled;

        }
    }
    public bool ClosePositionEOD(int i, bool gunSonuPozKapatEnabled = true)
    {
        // Optimizasyon: Önce disabled kontrolü yap
        if (!gunSonuPozKapatEnabled)
            return false;

        // Optimizasyon: Sınır kontrolü
        if (i >= Data.Count - 1)
            return false;

        // Optimizasyon: GetDates() yerine direkt Data'ya eriş
        // GetDates() her çağrıda 1.8M+ bar iterate ediyor - O(n) maliyeti var!
        // Direkt erişim O(1) maliyetli
        return Data[i].Date != Data[i + 1].Date;
    }

    public bool ClosePositionEOD_2(int i, bool gunSonuPozKapatEnabled = true, int hour = 18, int minute = 0)
    {
        // Optimizasyon: Önce disabled kontrolü yap
        if (!gunSonuPozKapatEnabled)
            return false;

        // Optimizasyon: Sınır kontrolü
        if (i >= Data.Count)
            return false;

        // Optimizasyon: GetDateTimes() yerine direkt Data'ya eriş
        // GetDateTimes() her çağrıda 1.8M+ bar iterate ediyor - O(n) maliyeti var!
        var currentDateTime = Data[i].DateTime;

        if (currentDateTime.Hour == hour && currentDateTime.Minute >= minute)
        {
            //this.signals.FlatOl = true;
            return true;
        }

        return false;
    }

    public SingleTrader ConfigureUserFlagsOnce()
    {
        if (_data == null || _data.Count == 0)
            throw new ArgumentException("Data cannot be null or empty");

        // First All Reset
        this.signals.AlEnabled = false;
        this.signals.SatEnabled = false;
        this.signals.FlatOlEnabled = false;
        this.signals.PasGecEnabled = false;
        this.signals.KarAlEnabled = false;
        this.signals.ZararKesEnabled = false;
        this.signals.Alindi = false;
        this.signals.Satildi = false;
        this.signals.FlatOlundu = false;
        this.signals.PasGecildi = false;
        this.signals.KarAlindi = false;
        this.signals.ZararKesildi = false;
        this.signals.PozAcilabilir = false;
        this.signals.PozAcildi = false;
        this.signals.PozKapatilabilir = false;
        this.signals.PozKapatildi = false;
        this.signals.PozAcilabilirAlis = false;
        this.signals.PozAcilabilirSatis = false;
        this.signals.PozAcildiAlis = false;
        this.signals.PozAcildiSatis = false;
        this.signals.GunSonuPozKapatEnabled = false;
        this.signals.GunSonuPozKapatildi = false;
        this.signals.TimeFilteringEnabled = false;
        this.signals.EquityCurveFilteringEnabled = false; 
        this.signals.IsTradeEnabled = false;
        this.signals.IsPozKapatEnabled = false;

        // Then Needed Set
        this.signals.AlEnabled = true;
        this.signals.SatEnabled = true;
        this.signals.FlatOlEnabled = true;
        this.signals.PasGecEnabled = false;
        this.signals.KarAlEnabled = false;
        this.signals.ZararKesEnabled = false;
        this.signals.GunSonuPozKapatEnabled = false;            // DEFAULT = False, Ek maliyet getirir : BackTest icin anlamli 
        this.signals.TimeFilteringEnabled = true;               // DEFAULT = False, Ek maliyet getirir : 
        this.signals.EquityCurveFilteringEnabled = false;

        return this;
    }

    public void CalculateStatistics()
    {
        if (_data == null || _data.Count == 0)
            throw new ArgumentException("Data cannot be null or empty");
        int lastBarIndex = GetLastBarIndex();
        statistics.Hesapla(lastBarIndex);
    }

    public void WriteStatisticsToFile(
        string outputDir,
        bool saveFullStatsTxt = true,
        bool saveFullStatsCsv = true,
        bool saveMinimalStatsTxt = true,
        bool saveMinimalStatsCsv = true,
        bool saveFullListsTxt = true,
        bool saveFullListsCsv = true,
        bool saveMinimalListsTxt = true,
        bool saveMinimalListsCsv = true,
        bool saveFullStatsTxtFormatted = true,
        bool saveMinimalStatsTxtFormatted = true)
    {
        if (saveFullStatsTxt)
        {
            Log($"\n\tSaving statistics to SingleTraderStatistics.txt...");
            statistics.SaveToTxt(Path.Combine(outputDir, "SingleTraderStatistics.txt"));
        }

        if (saveFullStatsCsv)
        {
            Log($"\n\tSaving statistics to SingleTraderStatistics.csv...");
            statistics.SaveToCsv(Path.Combine(outputDir, "SingleTraderStatistics.csv"));
        }

        if (saveMinimalStatsTxt)
        {
            Log($"\n\tSaving statistics to SingleTraderStatisticsMinimal.txt...");
            statistics.SaveToTxtMinimal(Path.Combine(outputDir, "SingleTraderStatisticsMinimal.txt"));
        }

        if (saveMinimalStatsCsv)
        {
            Log($"\n\tSaving statistics to SingleTraderStatisticsMinimal.csv...");
            statistics.SaveToCsvMinimal(Path.Combine(outputDir, "SingleTraderStatisticsMinimal.csv"));
        }

        if (saveFullListsTxt)
        {
            Log($"\n\tSaving statistics to SingleTraderLists.txt...");
            statistics.SaveListsToTxt(Path.Combine(outputDir, "SingleTraderLists.txt"));
        }

        if (saveFullListsCsv)
        {
            Log($"\n\tSaving statistics to SingleTraderLists.csv...");
            statistics.SaveListsToCsv(Path.Combine(outputDir, "SingleTraderLists.csv"));
        }

        if (saveMinimalListsTxt)
        {
            Log($"\n\tSaving statistics to SingleTraderListsMinimal.txt...");
            statistics.SaveListsToTxtMinimal(Path.Combine(outputDir, "SingleTraderListsMinimal.txt"));
        }

        if (saveMinimalListsCsv)
        {
            Log($"\n\tSaving statistics to SingleTraderListsMinimal.csv...");
            statistics.SaveListsToCsvMinimal(Path.Combine(outputDir, "SingleTraderListsMinimal.csv"));
        }

        if (saveFullStatsTxtFormatted)
        {
            Log($"\n\tSaving statistics to SingleTraderStatisticsFormatted.txt...");
            statistics.SaveToTxtFormatted(Path.Combine(outputDir, "SingleTraderStatisticsFormatted.txt"));
        }

        if (saveMinimalStatsTxtFormatted)
        {
            Log($"\n\tSaving statistics to SingleTraderStatisticsFormattedMinimal.txt...");
            statistics.SaveToTxtMinimalFormatted(Path.Combine(outputDir, "SingleTraderStatisticsFormattedMinimal.txt"));
        }
    }
    private void Log(string message)
    {
        if (_logger == null) return;
        _logger.LogRawInstance(message);
    }

    public void Dispose()
    {
        DeleteModules();
    }
}
