# SingleTrader — Çekirdek Motor (Trade Engine, Menü [2])

> [Class Reference](../01-class-reference.md) setinin bir parçası — bu sınıf ayrı dosyada,
> çünkü [StockDataReader](09-stockdatareader.md) gibi diğer 7 sınıftan (§1, §3-§8, hâlâ
> [01-class-reference.md](../01-class-reference.md)'de) çok daha derin işlendi (tam sınıf iskeleti,
> emir motorunun davranış tablosu, gerçek orkestrasyon kaynağı, instantiation envanteri,
> kullanım haritası). Yöntem: [06-class-doc-method.md](../06-class-doc-method.md).

### Dosyalar

- `src/AlgoTrade.Core/Trading/Traders/SingleTrader.cs` (2693 satır)
- `src/AlgoTrade.Core/DataProvider/MarketDataProvider.cs` (taban sınıf — `Data`/`GetData()`/
  `IsDataReady`/`GetLastBarIndex()` buradan gelir, bkz. [StockDataReader § Sonuç erişimi](09-stockdatareader.md#sonuç-erişimi))
- `src/AlgoTrade.Core/Trading/Core/InitialTradeParams.cs` (649 satır) — pozisyon büyüklüğü/market tipi modülü
- `src/AlgoTrade.Core/Trading/Core/Signals.cs` — sinyal/yön/fiyat/pozisyon state modülü
- `src/AlgoTrade.Core/Trading/Core/Status.cs` — sayaç/bakiye/getiri state modülü
- `src/AlgoTrade.Core/Trading/Core/Flags.cs` — iç bayrak modülü
- `src/AlgoTrade.Core/Trading/Core/Lists.cs` — bar-bar dizi (rapor) modülü
- `src/AlgoTrade.Core/Trading/Core/TimeUtils.cs`, `KarZarar.cs`, `KarAlZararKes.cs` — yardımcı modüller
- `src/AlgoTrade.Core/Trading/Statistics/Statistics.cs` — istatistik/rapor modülü (ayrı, derin bir sınıf — bu dokümanın kapsamı dışında)

### Rolü

- Tek stratejiyi bar-bar çalıştıran, gerçek emir açıp kapatan **çekirdek motor** —
  [01-class-reference.md](../01-class-reference.md#3-singletrader--çekirdek-motor)'ün de belirttiği gibi projenin en kritik sınıfı.
- `MarketDataProvider`'dan türer (`Data`/`GetData()`/`IsDataReady` oradan gelir), `IDisposable`.
- Kendi başına da kullanılabilir (Console `[2]`), ama asıl önemi **her "çoklu" sistemin içindeki
  gerçek işçi olması**: `MultipleTrader`'ın her child'ı, `ConfirmingSingleTrader`'ın hem
  `signalTrader`'ı hem `mainTrader`'ı, 12 Scanner sınıfının içindeki throwaway trader'lar,
  `MultipleQuery`'nin her satırı — hepsi birer `SingleTrader` instance'ı (bkz. [Kimler
  Kullanıyor](#kimler-kullanıyor--instantiation-noktaları), 25 instantiation noktası).
- Kompozisyon üzerine kurulu: kendi state'ini 9 ayrı modüle (`initialTradeParams`, `signals`,
  `status`, `flags`, `lists`, `timeUtils`, `karZarar`, `karAlZararKes`, `statistics`) böler —
  `CreateModules()`/`ResetModules()`/`InitModules()`/`DeleteModules()` dörtlüsüyle yönetilir.

### Ne zaman kullanılır

- Tek bir stratejiyi tek bir sembolde çalıştırmak istediğinde — doğrudan (Console `[2]`/`[5]`)
  veya `AlgoTrader.RunSingleTraderWithProgressAsync()` üzerinden dolaylı (bkz. [Çağrı Zinciri —
  Menüden Çağrılma](#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--singletrader)).
- Her "çoklu" sistemi (MultipleTrader, Confirming*, Scanner, MultipleQuery) anlamadan önce —
  onların hepsi bu sınıfın üzerine kurulu, önce bunu anlamak şart.
- Script'ten tam kontrol istendiğinde (`new SingleTrader(...)` + `SetStrategy`/`SetQuery` +
  manuel `Reset()`→`Init()`→bar-bar `Run(i)`→`Finalize()` döngüsü) — bkz. [Tipik Kullanım —
  Script'ten Çağrılma](#tipik-kullanım--scriptten-çağrılma-manuel-kurulum).

### Modül Kompozisyonu

`CreateModules()`/`ResetModules()`/`InitModules()`/`DeleteModules()` dörtlüsü tarafından
yönetilen 9 `private set + public get` property — hepsi kendi dosyasında, hepsi kendi
`Reset()`/`Init()` metoduna sahip:

| Modül | Sınıf | Rolü (region'lardan) |
|---|---|---|
| `initialTradeParams` | `InitialTradeParams` | Pozisyon büyüklüğü, market tipi, komisyon/kayma, pyramiding limiti (bkz. [AppConfig Kaynağı](#appconfig-kaynağı--singletraderconfig)) |
| `signals` | `Signals` | Signal Flags, Signal Info, Direction, Current/Previous Prices, Trailing Stop, Current/Previous Bar Numbers, Position Size, Order Status, Signal Status |
| `status` | `Status` | Trade/Command/Bar Counts, Profit/Loss, Commission, Slippage, Position Size, Balance, Returns, Net Values, KZ System, Return Type, Summary |
| `flags` | `Flags` | Update Flags, Calculation Flags, Enabled Flags, Execution Flags, Return Calculation Flags |
| `lists` | `Lists` | Bar-bar diziler — General, Signal Data, Time Filter Data, Profit/Loss Data, Trading Actions, Trade Counts, Position Size, Commission, Bar Counts, Balance Data (her biri `List<T>`, `InitOrReuse(barCount)` ile boyutlandırılır) |
| `timeUtils` | `TimeUtils` | `check_bar_time_with`/`check_bar_date_with`/`check_bar_date_time_with` — `CheckOrderTimeEligibility`'nin kullandığı bar-zaman karşılaştırıcıları |
| `karZarar` | `KarZarar` | Kar/zarar yardımcı hesaplamaları (`Init(this)` ile trader'a bağlanır) |
| `karAlZararKes` | `KarAlZararKes` | Kâr-al/zarar-kes yardımcı hesaplamaları |
| `statistics` | `Statistics.Statistics` | `CalculateStatistics()`/`WriteStatisticsToFile()`'ın asıl işçisi — ayrı, derin bir sınıf, bu dokümanın kapsamı dışında |

Genelde bu dörtlüyü elle çağırmana gerek yok — `Reset()` içeriden `ResetModules()`'u,
`Init()` içeriden `InitModules()`'u tetikler.

### Sınıf İskeleti (ilk bakış)

Aşağıdaki bloktaki metod gövdeleri kaldırılmış — sadece alan/property/event/metod imzaları
(public + private, hepsi), gerçek kaynağın (`SingleTrader.cs`) sırasıyla birebir aynı. Tek
istisna `ExecuteOrders(barIndex)` — 720 satırlık tek bir metod, gövdesi hiçbir yerde (ne burada
ne "Public API"de) tam reprodüksiyonla gösterilmiyor; bunun yerine [Emir Motoru — ExecuteOrders](#emir-motoru--executeordersbarindex-satır-790-1510)
altında sinyal-geçişi tablosu + 2 temsili dal (F→A açılış, A→A pyramiding) veriliyor.

```csharp linenums="1"
public enum TraderRunMode
{
    TradeOnly = 0, TradeAndQuery = 1, QueryOnly = 2
}
public enum ConfirmationTrigger
{
    ProfitOnly = 0, LossOnly = 1, Both = 2
}

public class SingleTrader : MarketDataProvider, IDisposable
{
    // ---- Kimlik ----
    public int Id { get; private set; }
    public void SetId(int id);
    public int GetId();
    public string Name { get; private set; }
    public void SetName(string name);
    public string GetName();
    public void SetData(List<StockData> data);

    // ---- Symbol / System / Strategy / Query kimliği ----
    public string SymbolName { get; set; }
    public string SymbolPeriod { get; set; }
    public string SystemId { get; set; }
    public string SystemName { get; set; }
    public string StrategyId { get; set; }
    public string StrategyName { get; set; }
    public string QueryId { get; set; }
    public string QueryName { get; set; }

    // ---- Execution Time Tracking ----
    public string LastExecutionId { get; set; }
    public string LastExecutionTime { get; set; }
    public string LastExecutionTimeStart { get; set; }
    public string LastExecutionTimeStop { get; set; }
    public string LastExecutionTimeInMSec { get; set; }
    public string LastResetTime { get; set; }
    public string LastStatisticsCalculationTime { get; set; }

    private LogManager? _logger;
    public void SetLogger(LogManager? logger);

    private IndicatorManager? _indicators;
    public void SetIndicators(IndicatorManager? indicators);

    public IStrategy? Strategy { get; private set; }
    public IQuery? Query { get; private set; }
    public List<string> QueryColumnNames { get; private set; }
    public List<object> LastQueryResult { get; private set; }
    public List<Dictionary<string, object>> QueryResults { get; private set; }
    public void SetStrategy(IStrategy strategy);
    public void SetQuery(IQuery query);

    // ---- Modül kompozisyonu (bkz. yukarıdaki tablo) ----
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

    // ---- Time Filter Properties ----
    public string StartDateTimeStr { get; set; }
    public string StopDateTimeStr { get; set; }
    public string StartDateStr { get; set; }
    public string StopDateStr { get; set; }
    public string StartTimeStr { get; set; }
    public string StopTimeStr { get; set; }

    // ---- Equity Curve Filter Properties (hepsi private) ----
    private bool thresholdTypeIsPercent;
    private double profitConfirmationThreshold;
    private double lossConfirmationThreshold;
    private ConfirmationTrigger confirmationTrigger;
    private bool _equityCurveConfirmed;

    // ---- Event'ler ----
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
    public bool OptimizationEnabled { get; set; }
    public bool MultipleTraderModeEnabled { get; set; }
    public bool PlotEnabled { get; set; }

    // ---- State Flags ----
    public bool IsStarted { get; set; }
    public bool IsRunning { get; set; }
    public bool IsStopped { get; set; }
    public bool IsStopRequested { get; set; }
    public bool SaveStatisticsToFile { get; set; }
    public TraderRunMode RunMode { get; set; }

    // ---- Screening (Tarama) Properties — signals/lists'ten türetilen computed property'ler ----
    public string SonYon => signals?.SonYon ?? "F";
    public int SonSinyalBarIndex => signals?.SonBarNo ?? -1;
    public int SonSinyaldenBeriBarSayisi => signals?.SonBarNo >= 0 ? (Data.Count - 1 - signals.SonBarNo) : -1;
    public bool AcikPozisyonVar => SonYon == "A" || SonYon == "S";
    public double SonKarZararFiyat => (lists?.KarZararFiyatList != null && Data.Count > 0) ? lists.KarZararFiyatList[Data.Count - 1] : 0.0;
    public double SonKarZararYuzde => (lists?.KarZararFiyatYuzdeList != null && Data.Count > 0) ? lists.KarZararFiyatYuzdeList[Data.Count - 1] : 0.0;
    public double SonSinyalFiyati => signals?.SonFiyat ?? 0.0;
    public string TaramaOzeti => $"{SonYon} | Bar:{SonSinyaldenBeriBarSayisi} | KZ:{SonKarZararFiyat:F2} | %:{SonKarZararYuzde:F2}";
    public string SorguOzeti { get; set; }

    // ---- Statistics: çıktı dosya adları (10) + per-output enable flag (12) + Export (3) ----
    public string FullStatsTxtFileName { get; set; }
    public string FullStatsCsvFileName { get; set; }
    public string MinimalStatsTxtFileName { get; set; }
    public string MinimalStatsCsvFileName { get; set; }
    public string FullListsTxtFileName { get; set; }
    public string FullListsCsvFileName { get; set; }
    public string MinimalListsTxtFileName { get; set; }
    public string MinimalListsCsvFileName { get; set; }
    public string FullStatsTxtFormattedFileName { get; set; }
    public string MinimalStatsTxtFormattedFileName { get; set; }
    public string PerformansTxtFileName { get; set; }
    public string PerformansCsvFileName { get; set; }
    public bool SaveFullStatsTxtEnabled { get; set; }
    public bool SaveFullStatsCsvEnabled { get; set; }
    public bool SaveMinimalStatsTxtEnabled { get; set; }
    public bool SaveMinimalStatsCsvEnabled { get; set; }
    public bool SaveFullListsTxtEnabled { get; set; }
    public bool SaveFullListsCsvEnabled { get; set; }
    public bool SaveMinimalListsTxtEnabled { get; set; }
    public bool SaveMinimalListsCsvEnabled { get; set; }
    public bool SaveFullStatsTxtFormattedEnabled { get; set; }
    public bool SaveMinimalStatsTxtFormattedEnabled { get; set; }
    public bool SavePerformansTxtEnabled { get; set; }
    public bool SavePerformansCsvEnabled { get; set; }
    public bool ExportEnabled { get; set; }
    public string ExportConfigFile { get; set; }
    public string ExportVersion { get; set; }

    // ---- Yön sorguları (MultipleTrader.BuildConsensusSignal()'ın kullandığı API) ----
    public bool is_son_yon_f();
    public bool is_son_yon_a();
    public bool is_son_yon_s();
    public bool is_prev_yon_f();
    public bool is_prev_yon_a();
    public bool is_prev_yon_s();

    // ---- Kurulum ----
    public SingleTrader(int id, string name, List<StockData> data, IndicatorManager indicators, LogManager? logger = null);
    public SingleTrader SetCallbacks(
        Action<SingleTrader, int>? onReset = null, Action<SingleTrader, int>? onInit = null,
        Action<SingleTrader, int>? onRun = null, Action<SingleTrader, int>? onFinal = null,
        Action<SingleTrader, int>? onBeforeOrders = null, Action<SingleTrader, string, int>? onNotifySignal = null,
        Action<SingleTrader, int>? onAfterOrders = null, Action<SingleTrader, int, int, double>? onProgress = null,
        Action<SingleTrader>? onApplyUserFlags = null);
    public SingleTrader ClearCallbacks();

    // ---- Yaşam döngüsü ----
    public void Reset();
    public void Init();
    public void Run(int barIndex);
    public void Finalize();   // #pragma warning disable/restore CS0465 ile sarılı — bkz. not

    // ---- Modül yönetimi ----
    public SingleTrader CreateModules();
    public SingleTrader ResetModules();
    public SingleTrader InitModules();
    public SingleTrader DeleteModules();

    // ---- Run() iç adımları ----
    public TradeSignals ExecuteStrategy(int barIndex);
    public IReadOnlyList<object> ExecuteQuery(int barIndex);
    public void MapStrategyCommandsToTradeCommands(TradeSignals strategySignal);
    public int ExecuteOrders(int barIndex);   // 720 satır — bkz. ayrı bölüm
    public void ResetVariablesOnNewIteration(int barIndex);
    public void UpdateVariablesOnNewIteration(int barIndex);
    public void ResetTradeCommands();

    // ---- Kar/Zarar ----
    public double CalculateUnrealizedPnL(int barIndex);
    public double _calculateUnrealizedPnLMicro(int barIndex, string type = "C");
    public double _calculateUnrealizedPnL(int barIndex, string type = "C");

    public void ExecutePreOrderMethods(int barIndex);
    public void ExecutePostOrderMethods(int barIndex);
    public double CalculateBalance(int barIndex);

    // ---- Filtreler ----
    public int CheckOrderTimeEligibility(int BarIndex, int FilterMode, ref bool IsTradeEnabled, ref bool IsPozKapatEnabled, ref int CheckResult);
    public void ApplyTimingFilters(int barIndex);   // bkz. Not — hardcoded filterMode
    public void ConfigureEquityCurveFilter(bool isPercent, double profitThreshold, double lossThreshold, ConfirmationTrigger trigger);
    public void ApplyEquityCurveFilter(int barIndex);
    public bool ClosePositionEOD(int i, bool gunSonuPozKapatEnabled = true);
    public bool ClosePositionEOD_2(int i, bool gunSonuPozKapatEnabled = true, int hour = 18, int minute = 0);   // ölü kod — bkz. not
    public void ResolveFilterDecisions(int barIndex);
    public SingleTrader ConfigureUserFlagsOnce();

    // ---- İstatistik/Rapor ----
    public void CalculateStatistics();
    public void WriteStatisticsToFile(string outputDir, string inputsDir);
    public void CalculatePerformances(double bakiyePuan = 100000, double lotSayisi = 1.0, double varlikAdedCarpani = 1.0);
    internal void GetPerformansParams(out double bakiyePuan, out double lotSayisi, out double varlikAdedCarpani);
    public StringBuilder GetStatisticsHeaderRow(string separator = "|");
    public StringBuilder GetStatisticsDataRow(string separator = "|");

    private void Log(string message);
    public void Dispose();
}
```

### Üye İndeksi — Hangisi Nerede Anlatılıyor

Yukarıdaki iskeletteki her üye, kaynak sırasıyla, `SingleTrader::Üye` notasyonuyla — aşağıdaki
Public API bölümlerinden hangisinde detaylandırıldığına link veriyor (private alanlar/yardımcı
metodlar için ayrı bir alt başlık yoksa, o üyenin fiilen kullanıldığı en yakın bölüme yönlendirir).
**#** kolonu, yukarıdaki kod bloğunun (`linenums="1"`) gerçek satır numarasıyla birebir eşleşiyor.
İki enum (`TraderRunMode`/`ConfirmationTrigger`) sınıfın dışında, dosya kapsamında tanımlı — bu
yüzden `SingleTrader::` öneki almıyor.

| # | Üye | Tür | Detay |
|---|---|---|---|
| 1 | `TraderRunMode` | enum (top-level) | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 5 | `ConfirmationTrigger` | enum (top-level) | [Equity Curve Filter](#equity-curve-filter-configureequitycurvefilter--applyequitycurvefilter) |
| 13 | `SingleTrader::Id` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 14 | `SingleTrader::SetId(id)` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 15 | `SingleTrader::GetId()` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 16 | `SingleTrader::Name` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 17 | `SingleTrader::SetName(name)` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 18 | `SingleTrader::GetName()` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 19 | `SingleTrader::SetData(data)` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 22 | `SingleTrader::SymbolName` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 23 | `SingleTrader::SymbolPeriod` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 24 | `SingleTrader::SystemId` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 25 | `SingleTrader::SystemName` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 26 | `SingleTrader::StrategyId` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 27 | `SingleTrader::StrategyName` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 28 | `SingleTrader::QueryId` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 29 | `SingleTrader::QueryName` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 32 | `SingleTrader::LastExecutionId` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 33 | `SingleTrader::LastExecutionTime` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 34 | `SingleTrader::LastExecutionTimeStart` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 35 | `SingleTrader::LastExecutionTimeStop` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 36 | `SingleTrader::LastExecutionTimeInMSec` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 37 | `SingleTrader::LastResetTime` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 38 | `SingleTrader::LastStatisticsCalculationTime` | public property | [Kullanım Haritası](#kullanım-haritası) — hiçbir yerde set edilmiyor |
| 40 | `SingleTrader::_logger` | private field | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 41 | `SingleTrader::SetLogger(logger)` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 43 | `SingleTrader::_indicators` | private field | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 44 | `SingleTrader::SetIndicators(indicators)` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 46 | `SingleTrader::Strategy` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 47 | `SingleTrader::Query` | public property | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 48 | `SingleTrader::QueryColumnNames` | public property | [Run() İç Akışı](#run-iç-akışı) |
| 49 | `SingleTrader::LastQueryResult` | public property | [Run() İç Akışı](#run-iç-akışı) |
| 50 | `SingleTrader::QueryResults` | public property | [Run() İç Akışı](#run-iç-akışı) |
| 51 | `SingleTrader::SetStrategy(strategy)` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 52 | `SingleTrader::SetQuery(query)` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 55 | `SingleTrader::initialTradeParams` | public property | [Modül Kompozisyonu](#modül-kompozisyonu) |
| 56 | `SingleTrader::signals` | public property | [Modül Kompozisyonu](#modül-kompozisyonu) |
| 57 | `SingleTrader::status` | public property | [Modül Kompozisyonu](#modül-kompozisyonu) |
| 58 | `SingleTrader::flags` | public property | [Modül Kompozisyonu](#modül-kompozisyonu) |
| 59 | `SingleTrader::lists` | public property | [Modül Kompozisyonu](#modül-kompozisyonu) |
| 60 | `SingleTrader::timeUtils` | public property | [Modül Kompozisyonu](#modül-kompozisyonu) |
| 61 | `SingleTrader::strategySignal` | public property | [Run() İç Akışı](#run-iç-akışı) |
| 62 | `SingleTrader::karZarar` | public property | [Modül Kompozisyonu](#modül-kompozisyonu) |
| 63 | `SingleTrader::karAlZararKes` | public property | [Modül Kompozisyonu](#modül-kompozisyonu) |
| 64 | `SingleTrader::statistics` | public property | [Modül Kompozisyonu](#modül-kompozisyonu) |
| 67 | `SingleTrader::StartDateTimeStr` | public property | [Timing Filter](#timing-filter-checkordertimeeligibility--applytimingfilters) |
| 68 | `SingleTrader::StopDateTimeStr` | public property | [Timing Filter](#timing-filter-checkordertimeeligibility--applytimingfilters) |
| 69 | `SingleTrader::StartDateStr` | public property | [Timing Filter](#timing-filter-checkordertimeeligibility--applytimingfilters) |
| 70 | `SingleTrader::StopDateStr` | public property | [Timing Filter](#timing-filter-checkordertimeeligibility--applytimingfilters) |
| 71 | `SingleTrader::StartTimeStr` | public property | [Timing Filter](#timing-filter-checkordertimeeligibility--applytimingfilters) |
| 72 | `SingleTrader::StopTimeStr` | public property | [Timing Filter](#timing-filter-checkordertimeeligibility--applytimingfilters) |
| 75 | `SingleTrader::thresholdTypeIsPercent` | private field | [Equity Curve Filter](#equity-curve-filter-configureequitycurvefilter--applyequitycurvefilter) |
| 76 | `SingleTrader::profitConfirmationThreshold` | private field | [Equity Curve Filter](#equity-curve-filter-configureequitycurvefilter--applyequitycurvefilter) |
| 77 | `SingleTrader::lossConfirmationThreshold` | private field | [Equity Curve Filter](#equity-curve-filter-configureequitycurvefilter--applyequitycurvefilter) |
| 78 | `SingleTrader::confirmationTrigger` | private field | [Equity Curve Filter](#equity-curve-filter-configureequitycurvefilter--applyequitycurvefilter) |
| 79 | `SingleTrader::_equityCurveConfirmed` | private field | [Equity Curve Filter](#equity-curve-filter-configureequitycurvefilter--applyequitycurvefilter) |
| 82 | `SingleTrader::OnReset` | public event | [Event'ler](#eventler) |
| 83 | `SingleTrader::OnInit` | public event | [Event'ler](#eventler) |
| 84 | `SingleTrader::OnRun` | public event | [Event'ler](#eventler) |
| 85 | `SingleTrader::OnFinal` | public event | [Event'ler](#eventler) |
| 86 | `SingleTrader::OnBeforeOrder` | public event | [Event'ler](#eventler) |
| 87 | `SingleTrader::OnNotifySignal` | public event | [Event'ler](#eventler) — hiç invoke edilmiyor, bkz. Not |
| 88 | `SingleTrader::OnAfterOrder` | public event | [Event'ler](#eventler) |
| 89 | `SingleTrader::OnProgress` | public event | [Event'ler](#eventler) |
| 90 | `SingleTrader::OnApplyUserFlags` | public event | [Event'ler](#eventler) — hiç invoke edilmiyor |
| 92 | `SingleTrader::ExecutionStepNumber` | public property | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 93 | `SingleTrader::BakiyeInitialized` | public property | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 94 | `SingleTrader::OptimizationEnabled` | public property | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 95 | `SingleTrader::MultipleTraderModeEnabled` | public property | [Kullanım Haritası](#kullanım-haritası) — hiçbir yerde okunmuyor/set edilmiyor |
| 96 | `SingleTrader::PlotEnabled` | public property | [Kullanım Haritası](#kullanım-haritası) — `runSingleTraderAlgoTrade()`'de kontrol ediliyor |
| 99 | `SingleTrader::IsStarted` | public property | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 100 | `SingleTrader::IsRunning` | public property | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 101 | `SingleTrader::IsStopped` | public property | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 102 | `SingleTrader::IsStopRequested` | public property | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 103 | `SingleTrader::SaveStatisticsToFile` | public property | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 104 | `SingleTrader::RunMode` | public property | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 107 | `SingleTrader::SonYon` | public property (computed) | [Tarama Özeti](#tarama-özeti-screening-properties) |
| 108 | `SingleTrader::SonSinyalBarIndex` | public property (computed) | [Tarama Özeti](#tarama-özeti-screening-properties) |
| 109 | `SingleTrader::SonSinyaldenBeriBarSayisi` | public property (computed) | [Tarama Özeti](#tarama-özeti-screening-properties) |
| 110 | `SingleTrader::AcikPozisyonVar` | public property (computed) | [Tarama Özeti](#tarama-özeti-screening-properties) |
| 111 | `SingleTrader::SonKarZararFiyat` | public property (computed) | [Tarama Özeti](#tarama-özeti-screening-properties) |
| 112 | `SingleTrader::SonKarZararYuzde` | public property (computed) | [Tarama Özeti](#tarama-özeti-screening-properties) |
| 113 | `SingleTrader::SonSinyalFiyati` | public property (computed) | [Tarama Özeti](#tarama-özeti-screening-properties) |
| 114 | `SingleTrader::TaramaOzeti` | public property (computed) | [Tarama Özeti](#tarama-özeti-screening-properties) |
| 115 | `SingleTrader::SorguOzeti` | public property | [Tarama Özeti](#tarama-özeti-screening-properties) — tek istisna, computed değil |
| 118 | `SingleTrader::FullStatsTxtFileName` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 119 | `SingleTrader::FullStatsCsvFileName` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 120 | `SingleTrader::MinimalStatsTxtFileName` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 121 | `SingleTrader::MinimalStatsCsvFileName` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 122 | `SingleTrader::FullListsTxtFileName` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 123 | `SingleTrader::FullListsCsvFileName` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 124 | `SingleTrader::MinimalListsTxtFileName` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 125 | `SingleTrader::MinimalListsCsvFileName` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 126 | `SingleTrader::FullStatsTxtFormattedFileName` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 127 | `SingleTrader::MinimalStatsTxtFormattedFileName` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 128 | `SingleTrader::PerformansTxtFileName` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 129 | `SingleTrader::PerformansCsvFileName` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 130 | `SingleTrader::SaveFullStatsTxtEnabled` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 131 | `SingleTrader::SaveFullStatsCsvEnabled` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 132 | `SingleTrader::SaveMinimalStatsTxtEnabled` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 133 | `SingleTrader::SaveMinimalStatsCsvEnabled` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 134 | `SingleTrader::SaveFullListsTxtEnabled` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 135 | `SingleTrader::SaveFullListsCsvEnabled` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 136 | `SingleTrader::SaveMinimalListsTxtEnabled` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 137 | `SingleTrader::SaveMinimalListsCsvEnabled` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 138 | `SingleTrader::SaveFullStatsTxtFormattedEnabled` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 139 | `SingleTrader::SaveMinimalStatsTxtFormattedEnabled` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 140 | `SingleTrader::SavePerformansTxtEnabled` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 141 | `SingleTrader::SavePerformansCsvEnabled` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 142 | `SingleTrader::ExportEnabled` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 143 | `SingleTrader::ExportConfigFile` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 144 | `SingleTrader::ExportVersion` | public property | [İstatistik/Rapor](#istatistikrapor) |
| 147 | `SingleTrader::is_son_yon_f()` | public method | [Yön Sorguları](#yön-sorguları) |
| 148 | `SingleTrader::is_son_yon_a()` | public method | [Yön Sorguları](#yön-sorguları) |
| 149 | `SingleTrader::is_son_yon_s()` | public method | [Yön Sorguları](#yön-sorguları) |
| 150 | `SingleTrader::is_prev_yon_f()` | public method | [Yön Sorguları](#yön-sorguları) |
| 151 | `SingleTrader::is_prev_yon_a()` | public method | [Yön Sorguları](#yön-sorguları) |
| 152 | `SingleTrader::is_prev_yon_s()` | public method | [Yön Sorguları](#yön-sorguları) |
| 155 | `SingleTrader::SingleTrader(...)` | constructor | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 156 | `SingleTrader::SetCallbacks(...)` | public method (fluent) | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 162 | `SingleTrader::ClearCallbacks()` | public method (fluent) | [Kimlik ve Kurulum](#kimlik-ve-kurulum) |
| 165 | `SingleTrader::Reset()` | public method | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 166 | `SingleTrader::Init()` | public method | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 167 | `SingleTrader::Run(barIndex)` | public method | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 168 | `SingleTrader::Finalize()` | public method | [Yaşam Döngüsü](#yaşam-döngüsü) — bkz. `CS0465` notu |
| 171 | `SingleTrader::CreateModules()` | public method (fluent) | [Modül Kompozisyonu](#modül-kompozisyonu) |
| 172 | `SingleTrader::ResetModules()` | public method (fluent) | [Modül Kompozisyonu](#modül-kompozisyonu) |
| 173 | `SingleTrader::InitModules()` | public method (fluent) | [Modül Kompozisyonu](#modül-kompozisyonu) |
| 174 | `SingleTrader::DeleteModules()` | public method (fluent) | [Modül Kompozisyonu](#modül-kompozisyonu) |
| 177 | `SingleTrader::ExecuteStrategy(barIndex)` | public method | [Run() İç Akışı](#run-iç-akışı) |
| 178 | `SingleTrader::ExecuteQuery(barIndex)` | public method | [Run() İç Akışı](#run-iç-akışı) |
| 179 | `SingleTrader::MapStrategyCommandsToTradeCommands(...)` | public method | [Run() İç Akışı](#run-iç-akışı) |
| 180 | `SingleTrader::ExecuteOrders(barIndex)` | public method | [Emir Motoru](#emir-motoru--executeordersbarindex-satır-790-1510) — ayrı bölüm |
| 181 | `SingleTrader::ResetVariablesOnNewIteration(barIndex)` | public method | [Run() İç Akışı](#run-iç-akışı) — `ExecutePreOrderMethods`'ın iç adımı |
| 182 | `SingleTrader::UpdateVariablesOnNewIteration(barIndex)` | public method | [Run() İç Akışı](#run-iç-akışı) — `ExecutePreOrderMethods`'ın iç adımı |
| 183 | `SingleTrader::ResetTradeCommands()` | public method | [Run() İç Akışı](#run-iç-akışı) — `ExecutePreOrderMethods`'ın iç adımı |
| 186 | `SingleTrader::CalculateUnrealizedPnL(barIndex)` | public method | [Kar/Zarar ve Bakiye](#karzarar-ve-bakiye-hesaplama) |
| 187 | `SingleTrader::_calculateUnrealizedPnLMicro(...)` | public method | [Kar/Zarar ve Bakiye](#karzarar-ve-bakiye-hesaplama) |
| 188 | `SingleTrader::_calculateUnrealizedPnL(...)` | public method | [Kar/Zarar ve Bakiye](#karzarar-ve-bakiye-hesaplama) |
| 190 | `SingleTrader::ExecutePreOrderMethods(barIndex)` | public method | [Run() İç Akışı](#run-iç-akışı) |
| 191 | `SingleTrader::ExecutePostOrderMethods(barIndex)` | public method | [Run() İç Akışı](#run-iç-akışı) |
| 192 | `SingleTrader::CalculateBalance(barIndex)` | public method | [Kar/Zarar ve Bakiye](#karzarar-ve-bakiye-hesaplama) |
| 195 | `SingleTrader::CheckOrderTimeEligibility(...)` | public method | [Timing Filter](#timing-filter-checkordertimeeligibility--applytimingfilters) |
| 196 | `SingleTrader::ApplyTimingFilters(barIndex)` | public method | [Timing Filter](#timing-filter-checkordertimeeligibility--applytimingfilters) — bkz. hardcoded `filterMode` notu |
| 197 | `SingleTrader::ConfigureEquityCurveFilter(...)` | public method | [Equity Curve Filter](#equity-curve-filter-configureequitycurvefilter--applyequitycurvefilter) |
| 198 | `SingleTrader::ApplyEquityCurveFilter(barIndex)` | public method | [Equity Curve Filter](#equity-curve-filter-configureequitycurvefilter--applyequitycurvefilter) |
| 199 | `SingleTrader::ClosePositionEOD(...)` | public method | [Gün Sonu Kapatma](#gün-sonu-kapatma-closepositioneod) |
| 200 | `SingleTrader::ClosePositionEOD_2(...)` | public method | Ölü kod — kaynakta tanımlı (`SingleTrader.cs:2376-2397`) ama hiçbir yerden çağrılmıyor, bkz. [Gün Sonu Kapatma](#gün-sonu-kapatma-closepositioneod) alt notu |
| 201 | `SingleTrader::ResolveFilterDecisions(barIndex)` | public method | [Filtre Öncelik Sırası](#filtre-öncelik-sırası-resolvefilterdecisions) |
| 202 | `SingleTrader::ConfigureUserFlagsOnce()` | public method (fluent) | [Yaşam Döngüsü](#yaşam-döngüsü) |
| 205 | `SingleTrader::CalculateStatistics()` | public method | [İstatistik/Rapor](#istatistikrapor) |
| 206 | `SingleTrader::WriteStatisticsToFile(...)` | public method | [İstatistik/Rapor](#istatistikrapor) |
| 207 | `SingleTrader::CalculatePerformances(...)` | public method | [İstatistik/Rapor](#istatistikrapor) |
| 208 | `SingleTrader::GetPerformansParams(...)` | internal method | [İstatistik/Rapor](#istatistikrapor) |
| 209 | `SingleTrader::GetStatisticsHeaderRow(separator)` | public method | [İstatistik/Rapor](#istatistikrapor) |
| 210 | `SingleTrader::GetStatisticsDataRow(separator)` | public method | [İstatistik/Rapor](#istatistikrapor) |
| 212 | `SingleTrader::Log(message)` | private method | [İstatistik/Rapor](#istatistikrapor) — `WriteStatisticsToFile()`'ın iç yardımcısı, ayrıca anlatılmıyor |
| 213 | `SingleTrader::Dispose()` | public method | [Kimlik ve Kurulum](#kimlik-ve-kurulum) — `DeleteModules()`'u çağırır |

## Public API

### Kimlik ve Kurulum

- `SingleTrader(id, name, data, indicators, logger?)` — constructor: `SetId`/`SetName`/`SetData`/
  `SetIndicators` çağırır, `logger` doluysa `SetLogger`, sonra **`CreateModules()`**'u tetikler
  (9 modül burada yaratılır — `Init()` beklemeden).
- `SetStrategy(strategy)` / `SetQuery(query)` — ikisi de önce `_data`/`_indicators` doluluğunu
  kontrol eder (`InvalidOperationException` fırlatır), sonra `strategy`/`query`'nin
  `BaseStrategy`/`BaseQuery`'den türediğini doğrular (`InvalidOperationException` — türemiyorsa),
  son olarak `baseStrategy.SetTrader(this)` / `baseQuery.SetTrader(this) + SetLogger(_logger)`.
  `SetQuery` ayrıca `QueryColumnNames`/`LastQueryResult`/`QueryResults`'ı temizler.
- `SetCallbacks(onReset, onInit, onRun, onFinal, onBeforeOrders, onNotifySignal, onAfterOrders,
  onProgress, onApplyUserFlags)` → `SingleTrader` (fluent) — her parametre `null` değilse ilgili
  event'e atanır (biriktirmez, **atar** — `+=` değil `=`). `ClearCallbacks()` hepsini `null`'a çeker.
- `Dispose()` → `DeleteModules()`'u çağırır (callback'leri temizler, `Strategy`/`Query`'i `null`
  yapar, 9 modülü `null`'a çeker — `IDisposable` deseni ama gerçek bir unmanaged resource yok).

### Yaşam Döngüsü

Beklenen sıra: `Reset()` → attribute set (`SymbolName` vb.) → `ConfigureUserFlagsOnce()` (veya
`AppConfigApplier` üzerinden `ApplySingleTraderFlagsConfigs`) → `SetStrategy`/`SetQuery` → `Init()`
→ bar-bar `Run(i)` → `Finalize()`.

- **`Reset()`** (`SingleTrader.cs:380-438`) — `_data` boşsa `ArgumentException`. Kimlik
  string'lerini `"..."` placeholder'ına çeker, query sonuçlarını temizler, `OnReset(this, 0)` →
  **`ResetModules()`** (9 modülün kendi `Reset()`'i) → `OnReset(this, 1)` → `ExecutionStepNumber`/
  `BakiyeInitialized`/`OptimizationEnabled`/`MultipleTraderModeEnabled`'ı sıfırlar, equity curve
  filter private alanlarını varsayılana döndürür, state flag'lerini `false` yapar,
  `LastResetTime`'ı şimdiki zamana ayarlar.
- **`Init()`** (`440-450`) — `_data` boşsa `ArgumentException`. `OnInit(this, 0)` →
  **`InitModules()`** (9 modülün kendi `Init()`'i, `lists.InitOrReuse(_data.Count)` dahil) →
  `OnInit(this, 1)`.
- **`Run(int barIndex)`** (`452-529`) — `IsInitialized` (taban sınıf) `false`'sa
  `InvalidOperationException`; `barIndex >= Data.Count` ise sessizce `return`. `OnRun(this, 0)` →
  `RunMode`'a göre 3 farklı zincir (aşağıdaki tablo) → `OnRun(this, 1)` → her `updateFreq=5`
  yüzdelik dilimde (veya son barda) `OnProgress(this, i+1, totalBars, percentage)`.

  | `RunMode` | Zincir |
  |---|---|
  | `TradeOnly` | `ExecutePreOrderMethods` → (i<1 ise return) → `ExecuteStrategy` → `MapStrategyCommandsToTradeCommands` → `ApplyTimingFilters` → `ApplyEquityCurveFilter` → `ResolveFilterDecisions` → `ExecutePostOrderMethods` |
  | `TradeAndQuery` | Yukarıdakiyle aynı + sonunda `ExecuteQuery` |
  | `QueryOnly` | (i<1 ise return) → doğrudan `ExecuteQuery` (strateji/emir adımları hiç çalışmaz) |

- **`Finalize()`** (`531-584`) — `#pragma warning disable/restore CS0465` ile sarılı: C#'ta bir
  metodu `Finalize` adıyla tanımlamak normalde CLR'ın destructor/finalizer deseniyle
  karıştırıldığı için derleyici `CS0465` uyarısı verir (`~SingleTrader()` değil, sıradan bir
  `public void Finalize()` metodu — gerçek bir finalizer/destructor override'ı DEĞİL, sadece isim
  çakışması); bu satırlar sadece o uyarıyı bastırıyor, davranışsal bir etkisi yok. `IsInitialized`
  değilse `InvalidOperationException`. `OnFinal(this, 0)` → `RunMode`'a göre `CalculateStatistics()`
  + (optimizasyon değilse) `CalculatePerformances(...)` + (Trade&Query/QueryOnly'de) `SorguOzeti`
  string'ini `QueryColumnNames`/`LastQueryResult`'tan oluşturur → `OnFinal(this, 1)`.
- **`ConfigureUserFlagsOnce()`** → `SingleTrader` (fluent) — `signals`'daki tüm `*Enabled`/
  `*Yapildi`/`PozAcilabilir*` bayraklarını baştan `false`'a çeker (Console tarafında
  `ApplySingleTraderFlagsConfigs()`'in ilk satırı budur, sonra `AppConfig.SingleTrader.Signals`'tan
  gerçek değerler üzerine yazılır).

### Tarama Özeti (Screening Properties)

`Finalize()` çağrılmadan, **her bar sonrası anlık** okunabilen 9 computed property (hepsi
`signals`/`lists`'ten türer, kendi state'ini tutmaz):

- `SonYon` → `signals.SonYon` (`"A"`/`"S"`/`"F"`), `AcikPozisyonVar` → `SonYon == "A" || "S"`.
- `SonSinyalBarIndex` → `signals.SonBarNo`, `SonSinyaldenBeriBarSayisi` → `Data.Count - 1 - SonBarNo`.
- `SonKarZararFiyat`/`SonKarZararYuzde` → `lists.KarZararFiyatList`/`KarZararFiyatYuzdeList`'in son elemanı.
- `SonSinyalFiyati` → `signals.SonFiyat`.
- `TaramaOzeti` → `"{SonYon} | Bar:{N} | KZ:{fiyat:F2} | %:{yüzde:F2}"` — 12 Scanner sınıfının
  `ScanResult.TaramaOzeti` alanı bundan geliyor (bkz. [01-class-reference.md § Scanner
  Ailesi](../01-class-reference.md#13-scanner-ailesi-12-sınıf--toplu-tarama)).
- `SorguOzeti` — tek istisna: computed DEĞİL, `Finalize()` içinde elle atanan bir `{ get; set; }`.

`AlgoTrader.RunSingleTraderWithProgressAsync()` bu 5 property'yi `Finalize()`'dan ÖNCE, run
bitince hemen okuyup loglar (`AlgoTrader.cs:1449-1455`, bkz. [Tam Kaynak](#runsingletraderwithprogressasync--tam-kaynak-algotradercs1252-1530) satır 43-48).

### Yön Sorguları

- `is_son_yon_f()`/`is_son_yon_a()`/`is_son_yon_s()` → `signals.SonYon == "F"/"A"/"S"`.
- `is_prev_yon_f()`/`is_prev_yon_a()`/`is_prev_yon_s()` → `signals.PrevYon == "F"/"A"/"S"`.
- Kullanıcı kodunun (özellikle `MultipleTrader.BuildConsensusSignal()` ve
  [`CustomConsensusExample.csx`](../03-scripting-guide.md)'nin) child trader'ların son/önceki
  yönünü sorgulamak için kullandığı asıl API — `signals.SonYon`'a doğrudan erişmek yerine bu
  metodlar tercih ediliyor.

### Run() İç Akışı

- **`ExecuteStrategy(barIndex)`** → `TradeSignals` — `Strategy is null` ise `TradeSignals.None`,
  değilse `Strategy.OnStep(i)`.
- **`ExecuteQuery(barIndex)`** → `IReadOnlyList<object>` — `Query is null` ise boş dizi, değilse
  `Query.OnExecute(i)`; sonucu `QueryColumnNames`/`LastQueryResult`'a yazar, `BarIndex`/`DateTime`
  + her sütun-değer çiftini `QueryResults` listesine (bir `Dictionary<string,object>` satırı
  olarak) ekler.
- **`MapStrategyCommandsToTradeCommands(strategySignal)`** — `TradeSignals` enum'unu (`None`/
  `Buy`/`Sell`/`TakeProfit`/`StopLoss`/`Flat`/`Skip`) `signals.Al`/`Sat`/`KarAl`/`ZararKes`/
  `FlatOl`/`PasGec` bool'larına çevirir — ama her biri kendi `*Enabled` bayrağı `true` ise
  (`signals.AlEnabled` vb., bkz. [AppConfig § Signals](#appconfig-kaynağı--singletraderconfig)).
- **`ExecutePreOrderMethods(barIndex)`** — `ResetVariablesOnNewIteration(i)` (bar'ın `lists`
  slotlarını sıfırlar) → `UpdateVariablesOnNewIteration(i)` (`initialTradeParams`'tan `status`'a
  güncel pozisyon/komisyon/kayma parametrelerini kopyalar, ilk barda `BakiyeInitialized` set
  edilir) → (i<1 ise return) → `CalculateUnrealizedPnL(i)` → `ResetTradeCommands()` (sinyal
  bool'larını temizler) → `IsTradeEnabled`/`IsTimingFiltersTradeEnabled`/
  `IsEquityCurveTradeEnabled`/`IsPozKapatEnabled`/`GunSonuPozKapatildi` bayraklarını sıfırlar →
  `PozAcilabilir=true` yapar.
- **`ExecutePostOrderMethods(barIndex)`** — `OnBeforeOrder(this, i)` → **`ExecuteOrders(i)`**
  (asıl emir motoru, bkz. aşağıda) → `OnAfterOrder(this, i)` → `KarAlindi`/`ZararKesildi`/
  `FlatOlundu` bayraklarını günceller → `signals.SonYon`'a göre `lists.SinyalList[i]`'yi
  `1.0`/`-1.0`/`0.0` yapar → sayaç listelerini (`IslemSayisiList` vb.) `status`'tan kopyalar →
  `CalculateBalance(i)` → `IsTradeEnabledList`/`IsPozKapatEnabledList`'i yazar.

### Emir Motoru — `ExecuteOrders(barIndex)` (satır 790-1510)

720 satırlık tek metod — `this.signals.Sinyal` (`"A"`/`"S"`/`"F"`/`"P"`/`""`) ve mevcut/önceki
yönün (`SonYon`/`PrevYon`) kombinasyonuna göre 8 dala ayrılan bir `if/else if` zinciri. Aşağıdaki
tablo her dalın **ne zaman tetiklendiğini** ve **ne yaptığını** özetliyor; kod tabanı `EmirStatus`
kodlarını (1-11) buradan üretiyor.

| Dal | Koşul (satır) | `EmirStatus` | Ne olur |
|---|---|---|---|
| F→A (yeni long) | `Sinyal=="A" && SonYon!="A"`, `PrevYon=="F"` (`854-916`) | 1 | 1 işlem, komisyon = `komisyonCarpan × komisyonVolume` |
| S→A (ters yön) | Aynı dal, `PrevYon=="S"` (`917-951`) | 2 | 2 işlem (kapat+aç), komisyon iki kere hesaplanır, kazandıran/kaybettiren/nötr satış sayaçları güncellenir |
| F→S / A→S (yeni/ters short) | `Sinyal=="S" && SonYon!="S"` (`977-1097`) | 3 / 4 | F→A/S→A ile simetrik (Sell tarafı) |
| A→F (long kapat) | `Sinyal=="F" && SonYon!="F"`, `PrevYon=="A"` (`1099-1240`) | 5 | 1 işlem (kapatma), kazandıran/kaybettiren/nötr alış sayaçları güncellenir |
| S→F (short kapat) | Aynı dal, `PrevYon=="S"` | 6 | 1 işlem (kapatma), satış sayaçları güncellenir |
| P / boş (PasGec) | `Sinyal=="P" \|\| Sinyal==""` (`1242-1267`) | 7/8/9 (mevcut yöne göre) | **`flags.BakiyeGuncelle=false`** — "P sinyali bakiye güncellemez, aksi halde yüzeysel kâr mükerrer eklenir" (satır 1262 yorumu) |
| A→A (long pyramiding) | `Sinyal=="A" && SonYon=="A"` (`1269-1365`) | 10 | `PyramidingEnabled` kapalıysa **no-op** (`return`); `MaxPositionSizeEnabled` açıksa ve `mevcutLot+yeniLot > maxLot` ise **no-op**; açıksa ağırlıklı ortalama giriş fiyatı hesaplanır (bkz. kod bloğu altta) |
| S→S (short pyramiding) | `Sinyal=="S" && SonYon=="S"` (`1367-1462`) | 11 | A→A ile simetrik (Sell tarafı) |
| F→F (zaten flat) | `Sinyal=="F" && SonYon=="F"` (`1464-1469`) | — | Tamamen no-op, yorum satırı dışında hiçbir şey yapılmaz |

Dalların hepsinde ortak son adım (satır `1471-1509`): `KazandiranIslemSayisi`/
`KaybettirenIslemSayisi`/`NotrIslemSayisi` toplamları güncellenir, trade olduysa
`KardaBarSayisi`/`ZarardaBarSayisi` sıfırlanır, `status.IslemSayisi>0` ise 6 hesaplama bayrağı
(`AnlikKarZararHesaplaEnabled` vb.) açılır, `EmirKomutList`/`EmirStatusList` yazılır, 4
`*Gerceklesti` bayrağı sıfırlanır.

**F→A açılışının tam kaynağı** (en basit dal, örnek olarak — diğerleri simetrik):

```csharp linenums="1"
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
```

**A→A pyramiding'in tam kaynağı** (`1269-1365`) — `PyramidingEnabled`/`MaxPositionSizeEnabled`
guard'ları + ağırlıklı ortalama giriş fiyatı:

```csharp linenums="1"
else if (this.signals.Sinyal == "A" && this.signals.SonYon == "A")
{
    if (!this.initialTradeParams.PyramidingEnabled)
    {
        // Pyramiding kapalı - işlem yapma, sinyali göz ardı et
        return result;
    }

    bool isMicroLot = this.initialTradeParams.MicroLotSizeEnabled;
    double mevcutLot = isMicroLot ? this.signals.SonVarlikAdedSayisiMicro : this.signals.SonVarlikAdedSayisi;
    double yeniLot    = isMicroLot ? this.initialTradeParams.VarlikAdedSayisiMicro : this.initialTradeParams.VarlikAdedSayisi;

    if (this.initialTradeParams.MaxPositionSizeEnabled)
    {
        double maxLot = isMicroLot ? this.initialTradeParams.MaxPositionSizeMicro : this.initialTradeParams.MaxPositionSize;
        if (mevcutLot + yeniLot > maxLot)
        {
            // Limit aşıldı - işlem yapma
            return result;
        }
    }

    // ... Prev* alanları kaydedilir, sonra ağırlıklı ortalama:
    double eskiFiyat = this.signals.SonAFiyat;
    double yeniFiyat = AnlikKapanisFiyati;
    if (this.flags.KaymayiDahilEt) yeniFiyat = AnlikYuksekFiyati;  // Long için yüksek fiyat (daha kötü)

    double toplamLot = mevcutLot + yeniLot;
    double agirlikliOrtalamaFiyat = (mevcutLot * eskiFiyat + yeniLot * yeniFiyat) / toplamLot;

    // SonVarlikAdedSayisi(Micro) = toplamLot, SonAFiyat = SonFiyat = agirlikliOrtalamaFiyat
    // EmirStatus = 10, BakiyeGuncelle = false (kapanmadı, kar/zarar gerçekleşmedi)
}
```

> **Not — Dört sinyal dalında (F→A, F→S, A→F, S→F) hesaplanıp hiç kullanılmayan `mevcutLot`/
> `yeniLot` değişkenleri var:** `ExecuteOrders`'ın F→A dalında satır `962-970`, F→S dalında
> `1085-1093`, A→F dalında `1227-1235` — her birinde `mevcutLot`/`yeniLot` ("Mevcut pozisyon
> büyüklüğü" / "Eklenecek lot büyüklüğü" yorumlarıyla) hesaplanıyor ama o blok içinde hiçbir yerde
> okunmuyor (blok `status.KomisyonFiyat`/`lists.KomisyonFiyatList` güncellemesiyle bitiyor, bu iki
> değişkene hiç dokunmuyor). Bu hesaplama sadece A→A/S→S pyramiding dallarında (satır 1281-1288,
> 1379-1386) gerçekten kullanılıyor — muhtemelen o kod parçası oradan kopyala-yapıştırla diğer
> dallara da taşınmış, işlevsel bir etkisi yok (derleyici de "unused variable" uyarısı vermiyor
> çünkü değişkenler `double` local'lar, sadece kullanılmıyorlar), performans/okunabilirlik
> açısından temizlenebilir bir kalıntı.

> **Not — `ApplyTimingFilters()`'ta `filterMode` her zaman `1` — `CheckOrderTimeEligibility`'nin
> 6 modundan 5'i hiçbir konfigürasyonla tetiklenemiyor:** `ApplyTimingFilters(barIndex)`
> (`SingleTrader.cs:2245-2262`) `CheckOrderTimeEligibility`'yi çağırırken `int filterMode = 1;`
> satırıyla **hardcoded** geçiyor:
> ```csharp linenums="1"
> public void ApplyTimingFilters(int barIndex)
> {
>     int i = barIndex;
>     bool useTimeFiltering = this.signals.TimeFilteringEnabled;
>     if (useTimeFiltering)
>     {
>         int filterMode = 1;   // ← hardcoded, hiçbir yerden parametrik değil
>         bool isTradeEnabled = false;
>         bool isPozKapatEnabled = false;
>         int checkResult = 0;
>         CheckOrderTimeEligibility(i, filterMode, ref isTradeEnabled, ref isPozKapatEnabled, ref checkResult);
>         this.signals.IsTimingFiltersTradeEnabled = isTradeEnabled;
>         this.signals.IsPozKapatEnabled = isPozKapatEnabled;
>     }
> }
> ```
> `CheckOrderTimeEligibility`'nin kendisi 7 mod destekliyor (`0`=devre dışı, `1`=saat aralığı,
> `2`=tarih aralığı, `3`=tarih-saat aralığı, `4`=sadece başlangıç saati, `5`=sadece başlangıç
> tarihi, `6`=sadece başlangıç tarih-saat — bkz. [Timing Filter](#timing-filter-checkordertimeeligibility--applytimingfilters)
> tablosu), ama `ApplyTimingFilters` içindeki tek çağrı noktası `filterMode`'u asla dışarıdan
> almıyor — ne `AppConfig.SingleTrader.Signals`'tan, ne `signals` modülünden, ne başka bir
> parametreden. Sonuç: `TimeFilteringEnabled=true` yapıldığında **her zaman mod 1 (saat aralığı,
> `StartTimeStr`/`StopTimeStr`)** çalışır; `AppConfig.json`'da `StartDateTime`/`StopDateTime` alanı
> doldurulsa bile (mod 2/3/5/6'nın kullanacağı tarih kısmı) bunlar `Program.cs`/`AlgoTrader`
> tarafından `StartDateStr`/`StopDateStr` olarak set edilir ama hiçbir zaman okunmaz — mod 2, 3,
> 5, 6 kod olarak var ama **hiçbir konfigürasyon yoluyla tetiklenemez** (bunu değiştirmenin tek
> yolu kaynağı elle düzenlemek). Bu muhtemelen bir eksik/bug — kaynak proje sahibinin daha önce
> ["Timing Filter mekanizması ... muhtemelen bug, netleştirilmeli"](../05-findings.md) notuyla
> işaretlediği bulgunun tam kaynak kanıtı bu.

### Kar/Zarar ve Bakiye Hesaplama

- **`CalculateUnrealizedPnL(barIndex)`** → `double` — `initialTradeParams.MicroLotSizeEnabled`'a
  göre `_calculateUnrealizedPnLMicro` veya `_calculateUnrealizedPnL`'e yönlenir (ikisi de aynı
  mantık, sadece `signals.SonVarlikAdedSayisiMicro` vs `signals.SonVarlikAdedSayisi` kullanır).
- **`_calculateUnrealizedPnLMicro`/`_calculateUnrealizedPnL(barIndex, type="C")`** — `type`
  `"O"`/`"H"`/`"L"`/`"C"` (Open/High/Low/Close) fiyatını seçer (varsayılan Close).
  `flags.AnlikKarZararHesaplaEnabled` kapalıysa `0.0` döner (ilk trade'den önce hep bu durumda).
  `SonYon=="A"` ise `(anlikFiyat - sonFiyat) × varlikAdedSayisi`, `SonYon=="S"` ise
  `(sonFiyat - anlikFiyat) × varlikAdedSayisi` — sonucu `status.KarZararPuan/Fiyat/PuanYuzde/
  FiyatYuzde` + karşılık gelen `lists.*List[i]`'ye yazar. Ayrıca `KardaBarSayisi`/
  `ZarardaBarSayisi` sayaçlarını günceller (kâr pozitifse `+1`/`-1`, negatifse tersi, sıfırsa
  ikisi de `0`'a çekilir).
- **`CalculateBalance(barIndex)`** (`1939-2077`) — `BakiyePuanList`/`BakiyeFiyatList`'i günceller
  (`önceki bakiye + bar'ın kâr/zararı`); `flags.BakiyeGuncelle` `true` ise (yani bu barda gerçek
  bir emir kapandıysa) `status.BakiyePuan/Fiyat`'ı kalıcı günceller ve `ToplamKarPuan`/
  `ToplamZararPuan`/`NetKarPuan` (ve Fiyat karşılıkları) toplamlarını biriktirir. Ayrıca net
  (komisyon düşülmüş) getiri serilerini (`GetiriFiyatNetList` vb.) hesaplar. Son barda (`i ==
  barCount-1`) `status`'un "final" alanlarını (`BakiyeFiyat`, `GetiriFiyatYuzdeNet` vb.) son
  bar değerleriyle senkronlar — `Statistics.Hesapla()`'nın kullandığı `status` bu adımdan sonraki
  hali.

### Timing Filter: `CheckOrderTimeEligibility` + `ApplyTimingFilters`

`CheckOrderTimeEligibility(BarIndex, FilterMode, ref IsTradeEnabled, ref IsPozKapatEnabled, ref
CheckResult)` (`2079-2244`) — `signals.TimeFilteringEnabled` açıkken çalışır, `timeUtils.
check_bar_time_with`/`check_bar_date_with`/`check_bar_date_time_with` ile bar zamanını
`Start*Str`/`Stop*Str`'le karşılaştırır:

| `FilterMode` | Karşılaştırma | Aralık dışındaysa |
|---|---|---|
| `0` | Filtre yok | Her zaman `IsTradeEnabled=true` |
| `1` | Saat aralığı (`StartTimeStr`/`StopTimeStr`) — **tek fiilen tetiklenen mod, bkz. yukarıdaki Not** | Flat değilse `IsPozKapatEnabled=true` |
| `2` | Tarih aralığı (`StartDateStr`/`StopDateStr`) | Aynı |
| `3` | Tarih-saat aralığı (`StartDateTimeStr`/`StopDateTimeStr`) | Aynı |
| `4` | Sadece başlangıç saati (`>= StartTimeStr`) | Aynı |
| `5` | Sadece başlangıç tarihi (`>= StartDateStr`) | Aynı |
| `6` | Sadece başlangıç tarih-saati (`>= StartDateTimeStr`) | Aynı |

`ApplyTimingFilters(barIndex)` (`2245-2262`) bu metodun tek çağıranı — ve **her zaman
`FilterMode=1`** geçiyor (bkz. yukarıdaki [Not](#emir-motoru--executeordersbarindex-satır-790-1510)).
Sonucu `signals.IsTimingFiltersTradeEnabled`/`IsPozKapatEnabled`'e yazar.

### Equity Curve Filter: `ConfigureEquityCurveFilter` + `ApplyEquityCurveFilter`

- `ConfigureEquityCurveFilter(isPercent, profitThreshold, lossThreshold, trigger)` — private
  alanları (`thresholdTypeIsPercent`/`profitConfirmationThreshold`/`lossConfirmationThreshold`/
  `confirmationTrigger`) set eder, `_equityCurveConfirmed=false` yapar.
- `ApplyEquityCurveFilter(barIndex)` (`2271-2360`) — `signals.EquityCurveFilteringEnabled`
  kapalıysa no-op. Yön değiştiyse (`SonYon != PrevYon`) `_equityCurveConfirmed` sıfırlanır.
  Flat ise filtre uygulanmaz (`_equityCurveConfirmed=false`, return). Long/Short'ta ve henüz
  confirm edilmemişse: `karTetiklendi`/`zararTetiklendi` eşik kontrolü (`ThresholdIsPercentage`'a
  göre yüzde veya mutlak değer), `ConfirmationTrigger` (`ProfitOnly`/`LossOnly`/`Both`) hangisine
  bakılacağını belirler; eşik geçildiyse `_equityCurveConfirmed=true` VE
  `signals.IsEquityCurveTradeEnabled=true` (bir kez confirm olunca sonraki barlarda tekrar
  kontrol edilmez — `!_equityCurveConfirmed` guard'ı).
- Bu, [ConfirmingSingleTrader](../01-class-reference.md#6-confirmingsingletrader--sanal-pozisyon-konfirmasyonu)'ın
  kullandığı `VirtualPositionConfirmer`'dan **farklı bir mekanizma** — o sinyal-bazlı bir
  virtual-then-real state machine, bu ise equity-curve tabanlı bir soft-block (giriş sinyalini
  iptal eder, pozisyonu kapatmaz).

### Gün Sonu Kapatma: `ClosePositionEOD`

- `ClosePositionEOD(int i, bool gunSonuPozKapatEnabled=true)` → `bool` — `i >= Data.Count-1`
  ise `false`; `Data[i].Date != Data[i+1].Date` ise `true` (yani sıradaki bar farklı bir güne
  aitse, bu barın gün sonu barı olduğu anlaşılır). `ResolveFilterDecisions`'ın Öncelik 2 dalında
  kullanılıyor.

> **Not — `ClosePositionEOD_2(...)` tanımlı ama hiçbir yerden çağrılmıyor:** `ClosePositionEOD_2`
> (`SingleTrader.cs:2376-2397`, saat/dakika bazlı alternatif bir gün-sonu-kapatma implementasyonu
> — `currentDateTime.Hour == hour && currentDateTime.Minute >= minute`) kodda tanımlı, ama tüm
> kod tabanında (`AlgoTrade.Console`, `.csx` scriptler, `AlgoTrade.WinForms`) hiçbir çağıranı yok
> — sadece kendi tanımı grep'te çıkıyor. `docs/PROJECT_ANALYSIS.md` bunu zaten "çağrıldığı yer
> bulunamadı" olarak işaretlemiş; bu doküman bunu doğruluyor. `ResolveFilterDecisions` her zaman
> `ClosePositionEOD` (tekli, `_2` olmayan) çağırıyor.

### Filtre Öncelik Sırası: `ResolveFilterDecisions`

`ResolveFilterDecisions(barIndex)` (`2398-2484`) — `Run()` zincirinin filtre-sonrası son adımı,
`ApplyTimingFilters`/`ApplyEquityCurveFilter`'ın ürettiği bayrakları **kesin sinyal kararına**
çevirir. Öncelik sırası (üstteki alttakini ezer):

1. **PozKapat** (`signals.IsPozKapatEnabled`) — hard override, timing filtresi aralık dışına
   çıktıysa devreye girer. Flat değilse tüm sinyalleri temizleyip `FlatOl=true` yapar, `return`.
2. **GünSonuPozKapat** (`signals.GunSonuPozKapatEnabled` + `ClosePositionEOD(i)==true`) — hard
   override, `FlatOl=true` + `GunSonuPozKapatildi=true`, `return`.
3. **Timing hard block** (`TimeFilteringEnabled && !IsTimingFiltersTradeEnabled`) — tüm
   sinyalleri öldürür (`None=true`), `return`.
4. **TradeStartBarIndex warmup** (`TradeStartBarIndexEnabled && i < TradeStartBarIndex`) — aynı
   şekilde hard block, `return`.
5. **EquityCurve soft block** (`EquityCurveFilteringEnabled && !IsEquityCurveTradeEnabled`) —
   sadece giriş sinyallerini (`Al`/`Sat`) iptal eder, `None=true` yapar (mevcut pozisyonu
   KAPATMAZ — "soft" olmasının sebebi bu).
6. Son olarak `signals.IsTradeEnabled` üç filtrenin (`filter1`=timing, `filter2`=equity curve,
   `filter3`=warmup) mantıksal VE'si olarak hesaplanır.

### İstatistik/Rapor

- `CalculateStatistics()` — `_data` boşsa `ArgumentException`; `statistics.Hesapla(GetLastBarIndex())`
  çağırır (taban sınıftan gelen `GetLastBarIndex()`, `Data.Count-1`).
- `WriteStatisticsToFile(outputDir, inputsDir)` (`2529-2642`) — 12 farklı çıktı türünü, her biri
  kendi `Save*Enabled` bayrağı + (bazılarında) `*FileName` property'siyle kontrol edilerek yazar:
  `FullListsCsv`/`FullListsTxt` (config-driven, `StatisticsExporterConfig.json` üzerinden),
  `FullStatsCsv`/`FullStatsTxt` (düz), `PerformansCsv`/`PerformansTxt` (config-driven,
  `StatisticsExporter` sınıfı üzerinden), `FullStatsTxtFormatted`/`MinimalStatsTxtFormatted`
  (kutu-çizimli), ve `ExportEnabled=true` ise ek bir versiyonlu export (`ExportVersion`,
  `FullListsTxt`+`PerformansTxt`'i `ExportConfigFile`'daki sütun tanımıyla tekrar yazar). Minimal
  CSV/TXT (Stats/Lists) blokları kaynakta **yorum satırı halinde devre dışı** (`2590-2614`) —
  ilgili `Save*Enabled` bayrakları property olarak var ve `AppConfig.json`'dan set edilebiliyor
  ama gerçek yazım kodu çalışmıyor (muhtemelen kasıtlı, formatlı versiyonlarla yer değiştirmiş).
- `CalculatePerformances(bakiyePuan=100000, lotSayisi=1.0, varlikAdedCarpani=1.0)` →
  `statistics.CalculatePerformances(...)`.
- `GetPerformansParams(out bakiyePuan, out lotSayisi, out varlikAdedCarpani)` (`internal`) —
  `initialTradeParams`'tan sırayla `LotSayisi` → (boşsa) `KontratSayisi` → (boşsa)
  `VarlikAdedSayisi` (bu durumda `varlikAdedCarpani=1.0`'a sabitlenir) → (hâlâ boşsa)
  `HisseSayisi` fallback zinciriyle "hangi pozisyon büyüklüğü alanı doluysa onu kullan" mantığı
  uygular — kod içinde `// TODO : ici yeniden duzenlenecek` notu var.
- `GetStatisticsHeaderRow(separator="|")`/`GetStatisticsDataRow(separator="|")` →
  `StatisticsExporter(statistics).GetStatisticsHeaderRow/DataRow(separator)` — `AlgoTrader.
  RunSingleTraderWithProgressAsync()` bunu hem `"|"` hem `";"` ayraçla çağırıyor (bkz. [Tam
  Kaynak](#runsingletraderwithprogressasync--tam-kaynak-algotradercs1252-1530) satır 51-54).

### Event'ler

| Event | İmza | Tetiklendiği yer |
|---|---|---|
| `OnReset` | `(SingleTrader, int mode)` | `Reset()` başında (`mode=0`) ve sonunda (`mode=1`) |
| `OnInit` | `(SingleTrader, int mode)` | `Init()` başında/sonunda |
| `OnRun` | `(SingleTrader, int mode)` | `Run(i)` başında/sonunda (her bar) |
| `OnFinal` | `(SingleTrader, int mode)` | `Finalize()` başında/sonunda |
| `OnBeforeOrder` | `(SingleTrader, int barIndex)` | `ExecutePostOrderMethods`, `ExecuteOrders`'tan HEMEN önce |
| `OnAfterOrder` | `(SingleTrader, int barIndex)` | `ExecutePostOrderMethods`, `ExecuteOrders`'tan HEMEN sonra |
| `OnProgress` | `(SingleTrader, int currentBar, int totalBars, double percentage)` | `Run(i)` sonunda, her %5'lik dilimde (veya son barda) |
| `OnNotifySignal` | `(SingleTrader, string signal, int barIndex)` | **Hiçbir zaman** — bkz. aşağıdaki Not |
| `OnApplyUserFlags` | `(SingleTrader)` | Hiçbir zaman `SingleTrader.cs` içinden invoke edilmiyor — `AlgoTrader.OnApplyUserFlags(trader)` adında AYRI bir private metod var (`AlgoTrader.cs:224`) ama bu, event'i tetiklemiyor, kendi içinde `trader.ConfigureUserFlagsOnce()` çağırıp bayrakları elle set ediyor; `RunSingleTraderWithProgressAsync()`'te de bu yol yorum satırı (`// OnApplyUserFlags(singleTrader); // → AppConfig.SingleTrader.Signals ile değiştirildi`) — güncel akış `ApplySingleTraderFlagsConfigs()`'i kullanıyor, bu event tamamen kullanım dışı |

> **Not — `OnNotifySignal` event'i deklare edilmiş, `SetCallbacks`/`ClearCallbacks`'te
> yönetiliyor, script katmanından abone olunabiliyor — ama `SingleTrader.cs` içinde HİÇBİR yerden
> `Invoke` edilmiyor:** Grep kanıtı — `SingleTrader.cs`'te `OnNotifySignal` geçen tüm satırlar:
> deklarasyon (`173`), `SetCallbacks`'teki atama (`358`), `ClearCallbacks`'teki `null` ataması
> (`372`). `?.Invoke(...)` çağrısı **sıfır**. Bunun yerine `ExecuteOrders`'ın 5 farklı yerinde
> (satır `960`, `1083`, `1225`, `1363`, `1461`) **yorum satırı halinde** şu satır duruyor:
> `//OnNotifyStrategySignal?.Invoke(this, this.signals.Sinyal, i);` — dikkat: bu satırın çağırdığı
> isim `OnNotifyStrategySignal`, sınıfta deklare edilen isim ise `OnNotifySignal` — **iki farklı
> isim**. `OnNotifyStrategySignal` diye bir event/alan `SingleTrader.cs`'te hiç tanımlı değil
> (muhtemelen event yeniden adlandırılırken bu 5 yorum satırı güncellenmeyi unutmuş, ya da tam
> tersi — event önce `OnNotifyStrategySignal` olarak tasarlanıp sonra `OnNotifySignal`'e
> yeniden adlandırılmış ama invoke satırları hiç yazılmamış/geri getirilmemiş). Sonuç: **kesin**
> olarak — `ScriptGlobals.cs:155`'teki `trader.OnNotifySignal += _signalHandler; Log("[SUBSCRIBED]
> OnSignal");` satırı script kullanıcısına "artık her sinyalde haberdar olacaksın" izlenimi
> veriyor, ama events yalnızca deklare edildikleri sınıfın içinden invoke edilebildiği için
> (C# erişim kuralı — `SingleTrader` dışından `OnNotifySignal.Invoke(...)` yazılamaz) bu abonelik
> **hiçbir zaman tetiklenmeyecek**. `AlgoTrader.OnSingleTraderNotifySignal` callback'i de
> (`AlgoTrader.cs:188-191`) aynı sebeple boş gövdeli — zaten hiç çağrılmadığı için içinin dolu
> olmasının bir önemi yok.

## Çağrı Zinciri — Menüden Çağrılma (Program.cs → AlgoTrader → SingleTrader)

`SingleTrader`'ın Console'daki tek başına kullanımı (Menü `[2]`) iki katmanlı: `Program.cs`
seviyesinde ince bir menü/wiring katmanı, asıl kurulum/çalıştırma `AlgoTrader`
(`RunSingleTraderWithProgressAsync()`) içinde.

1. `handleSingleTrader()` (`AlgoTrade.Console/Program.cs:2994`) — `reloadAppConfig()` →
   döngü: `showModeConfigSummary("SingleTrader")` basar, `[1]/[2]/[3]` ile `RunMode` seçtirir
   (boş girişte `AppConfig.SingleTrader.RunMode`'a düşer), `showSingleTraderRunPreview(...)` ile
   JSON önizleme + `[ENTER]/[E]/[R]/[B]` bekler (ortak Preview/Confirm deseni, bkz.
   [02-console-menu-guide.md § Preview/Confirm](../02-console-menu-guide.md#5-previewconfirm-ekranı-kısayolları)).
2. `[ENTER]` → **`runSingleTraderAlgoTrade()`** (`Program.cs:761-834`) — `stockDataReader`/
   `stockMetaData` doluluğunu kontrol eder (`[1] Read Data` daha önce çalıştırılmamışsa sessizce
   `return`) → `new AlgoTrader("AlgoTrader")` + logger/timer kaydı + `SetData(stockDataReader.
   GetData())` + `SymbolName`/`SymbolPeriod`'u `stockMetaData`'dan çeker →
   **`AppConfigApplier.ApplySingleTrader(algoTrader, appConfig.SingleTrader, AppSettings.ConfigsDir)`**
   (bkz. [AppConfig Kaynağı](#appconfig-kaynağı--singletraderconfig)) → kullanıcının menüde
   seçtiği `selectedRunMode` AppConfig'teki `RunMode`'un üzerine yazılır → `Initialize()` →
   **`await algoTrader.RunSingleTraderWithProgressAsync()`** (asıl motor, aşağıda tam kaynağı
   var) → `WriteTraderDataToFilesAsync(...)` (arka planda dosya yazımı, grafik açıksa paralel) →
   `PlotEnabled` ise Python/DearPyGuiDataPlotter'a veri gönderimi.
3. `AlgoTrader.RunSingleTraderWithProgressAsync()` (`AlgoTrader.cs:1252-1530`) — burada gerçek
   `SingleTrader` yaratılıyor, konfigüre ediliyor, bar-bar çalıştırılıyor, `Finalize()` ediliyor.
   Tam kaynağı aşağıda.

## AppConfig Kaynağı — `SingleTraderConfig`

`AppConfig.json`'daki `"SingleTrader"` bölümünü karşılayan C# sınıfı (`AppConfig.cs:229-243`) —
`AppConfigApplier.ApplySingleTrader(algoTrader, cfg, configsDir)`'nin `cfg` parametresinin gerçek
tipi bu, 7 alt-config'e bölünmüş:

```csharp linenums="1"
public class SingleTraderConfig
{
    public string RunMode { get; set; } = "TradeOnly";       // TradeOnly | TradeAndQuery | QueryOnly

    public StrategyRef         Strategy          { get; set; } = new();
    public QueryRef?           Query             { get; set; }
    public EcfRef?             EquityCurveFilter { get; set; }
    public TradeParamsConfig   TradeParams       { get; set; } = new();
    public TraderSignalsConfig      Signals      { get; set; } = new();
    public TraderPlotConfig         Plot         { get; set; } = new();
    public TraderOptimizationConfig Optimization { get; set; } = new();
    public TraderSaveConfig         Save         { get; set; } = new();
    public TraderExportConfig?      Export       { get; set; }
}

public class TradeParamsConfig
{
    public string MarketType      { get; set; } = "ViopEndex";  // 14 MarketTypes değerinden biri
    public double IlkBakiye       { get; set; } = 100_000.0;
    public double KontratSayisi   { get; set; } = 1.0;          // Viop piyasaları
    public double LotSayisi       { get; set; } = 0.01;         // Fx / Crypto piyasaları
    public double HisseSayisi     { get; set; } = 1000.0;       // Bist piyasaları
    public double KomisyonCarpan  { get; set; } = 20.0;
    public double KaymaMiktari    { get; set; } = 0.5;
    public bool   PyramidingEnabled { get; set; } = false;
}

public class TraderSignalsConfig
{
    public bool   AlEnabled              { get; set; } = true;
    public bool   SatEnabled             { get; set; } = true;
    public bool   FlatOlEnabled          { get; set; } = true;
    public bool   PasGecEnabled          { get; set; } = true;
    public bool   KarAlEnabled           { get; set; } = true;
    public bool   ZararKesEnabled        { get; set; } = true;
    public bool   GunSonuPozKapatEnabled { get; set; } = false;
    public bool   TimeFilteringEnabled       { get; set; } = false;
    public string StartDateTime              { get; set; } = "2025.05.25 09:35:00";
    public string StopDateTime               { get; set; } = "2025.06.02 17:55:00";
    public bool   TradeStartBarIndexEnabled  { get; set; } = false;
    public int    TradeStartBarIndex         { get; set; } = 0;
}

public class TraderPlotConfig         { public bool PlotEnabled { get; set; } = false; }
public class TraderOptimizationConfig { public bool OptimizationEnabled { get; set; } = false; }
public class TraderExportConfig
{
    public bool   ExportEnabled { get; set; } = false;
    public string ConfigFile    { get; set; } = "StatisticsExporterConfig.json";
    public string Version       { get; set; } = "v1";
}
// TraderSaveConfig: 12 Save*Enabled bool + 12 *FileName string — bkz. yukarıdaki İstatistik/Rapor bölümü
```

Bu sınıfın `AppConfig.json`'daki gerçek karşılığı (`inputs/configs/AppConfig/AppConfig.json:24-`):

```json linenums="1"
"SingleTrader": {
    "RunMode": "TradeOnly",
    "Strategy": {
      "ConfigFile": "StrategyConfig.txt",
      "Name": "SimpleMostStrategy",
      "Version": "v1"
    },
    "Query": {
      "ConfigFile": "QueryConfig.txt",
      "Name": "SimpleQuery1",
      "Version": "v1"
    },
    "EquityCurveFilter": {
      "ConfigFile": "EquityCurveFilterConfig.txt",
      "Name": "",
      "Version": "v1"
    },
    "TradeParams": {
      "MarketType": "FxCrypto",
      "IlkBakiye": 100000.0,
      "KontratSayisi": 1,
      "LotSayisi": 0.01,
      "HisseSayisi": 1000.0,
      "KomisyonCarpan": 0.0,
      "KaymaMiktari": 0.0,
      "PyramidingEnabled": false
    },
    "Signals": {
      "AlEnabled": true,
      "SatEnabled": true,
      "FlatOlEnabled": true,
      "PasGecEnabled": false,
      "KarAlEnabled": false,
      "ZararKesEnabled": false,
      "GunSonuPozKapatEnabled": false,
      "TimeFilteringEnabled": false,
      "StartDateTime": "2025.05.25 09:35:00",
      "StopDateTime": "2025.06.02 17:55:00",
      "TradeStartBarIndexEnabled": false,
      "TradeStartBarIndex": 0
    },
    "Plot": { "PlotEnabled": true },
    "Optimization": { "OptimizationEnabled": false }
}
```

- `TradeParams` → `AppConfigApplier.BuildInitialTradeParams(cfg.TradeParams)`
  (`AppConfigApplier.cs:1346-`) tarafından gerçek `InitialTradeParams`'a çevrilir: `Reset()` →
  `SetBakiyeParams(cfg.IlkBakiye)` → `SetKomisyonParams(cfg.KomisyonCarpan)` →
  `SetKaymaParams(cfg.KaymaMiktari)` → `PyramidingEnabled = cfg.PyramidingEnabled` → `MarketType`
  string'i `Enum.TryParse` ile 14 değerden birine parse edilir (geçersizse `ArgumentException`)
  → market tipine göre `SetKontratParamsXxx(...)` overload'larından biri çağrılır (örn.
  `FxCrypto` → `SetKontratParamsFxCrypto(lotSayisi: cfg.LotSayisi)`, bu da `MicroLotSizeEnabled=
  true` yapar ve `VarlikAdedCarpani=100` sabitler).
- `Signals` → doğrudan `SingleTraderSignalsConfig`'e map'lenir, `algoTrader.SetSingleTraderSignalsConfig(...)`
  ile saklanır; `RunSingleTraderWithProgressAsync()` içinde `ApplySingleTraderFlagsConfigs(singleTrader)`
  çağrıldığında gerçek `signals.AlEnabled` vb. alanlara yazılır (bkz. aşağıdaki tam kaynak, satır 42).
- **`MaxPositionSize`/`MaxPositionSizeEnabled` (pyramiding limiti) `AppConfig.json`'da HİÇ YOK** —
  `TradeParamsConfig`'te böyle bir alan tanımlı değil, `BuildInitialTradeParams` da bunları hiç
  set etmiyor; yani `InitialTradeParams.MaxPositionSizeEnabled` her zaman `default(bool)=false`
  kalıyor (Console/AppConfig yoluyla pyramiding'e limit koymak mümkün değil, sadece script'ten
  `trader.initialTradeParams.MaxPositionSizeEnabled = true; ... MaxPositionSize = X;` ile elle
  set edilebilir) — bu, [01-class-reference.md § SingleTraderOptimizer](../01-class-reference.md#5-singletraderoptimizer--grid-search-optimizasyon)
  civarındaki `05-findings.md`'de zaten "Pyramiding'in pozisyon limiti AppConfig katmanında hiç
  yok" olarak not edilmiş bulgunun kaynak kanıtı.

## `RunSingleTraderWithProgressAsync()` — Tam Kaynak (`AlgoTrader.cs:1252-1530`)

```csharp linenums="1" hl_lines="12 16 17 21 22 26 31 37 41 42 45 46 60 63 66 71 75 76 80 82 83 84 88 91 92 93 94 106 109 122 123 124 125 126 131 133 134 135 136 137 138 146 162 163"
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

        // Indicators
        if (indicators != null) { indicators.Dispose(); indicators = null; }
        indicators = new IndicatorManager(this.Data);
        if (indicators == null) throw new InvalidOperationException("indicators can not be created...");

        // Strategy (StrategyRegistry üzerinden)
        if (strategy != null) { strategy.Dispose(); strategy = null; }
        strategy = _strategyRegistry.CreateStrategy(this.Data, indicators, _logger, _currentStrategyName, _currentStrategyParams);
        if (strategy == null) throw new InvalidOperationException("strategy can not be created...");

        // Query (opsiyonel, QueryIsEnabled ise)
        if (query != null) { query.Dispose(); query = null; }
        if (QueryIsEnabled)
        {
            if (string.IsNullOrWhiteSpace(_currentQueryName))
                throw new InvalidOperationException("QueryIsEnabled is true but query name is not configured. Call ConfigureQuery(...) first.");
            query = _queryRegistry.CreateQuery(this.Data, indicators, _logger, _currentQueryName, _currentQueryParams);
            if (query == null) throw new InvalidOperationException("query can not be created...");
        }

        // SingleTrader yaratma
        if (singleTrader != null) { singleTrader.Dispose(); singleTrader = null; }
        singleTrader = new SingleTrader(0, "singleTrader", this.Data, indicators, _logger);
        if (singleTrader == null) throw new InvalidOperationException("singleTrader can not be created...");

        // Callback'leri bağla (hepsi private OnSingleTraderXxx — bkz. Callback'lerin Gerçek Gövdeleri)
        singleTrader.ClearCallbacks()
                    .SetCallbacks(OnSingleTraderReset, OnSingleTraderInit, OnSingleTraderRun, OnSingleTraderFinal,
                                  OnSingleTraderBeforeOrder, OnSingleTraderNotifySignal, OnSingleTraderAfterOrder, OnSingleTraderProgress);

        singleTrader.Reset();

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

        // TradeParams — AppConfig.SingleTrader.TradeParams
        if (_singleTraderTradeParamsConfig != null)
            singleTrader.initialTradeParams!.ApplyFrom(_singleTraderTradeParamsConfig);

        // Sıralama Önemli: Signals + Save + Plot + Export config'lerini tek çağrıda uygula
        ApplySingleTraderFlagsConfigs(singleTrader);

        // Equity curve filter (id=0)
        SetSingleTraderConfigureEquityCurveFilter(singleTrader);

        singleTrader.RunMode = SingleTraderRunMode;
        if (singleTrader.RunMode == TraderRunMode.TradeOnly)
        {
            singleTrader.SetStrategy(strategy);
        }
        else if (singleTrader.RunMode == TraderRunMode.TradeAndQuery)
        {
            singleTrader.SetStrategy(strategy);
            if (query is not null) singleTrader.SetQuery(query);
        }
        else if (singleTrader.RunMode == TraderRunMode.QueryOnly)
        {
            if (query is not null) singleTrader.SetQuery(query);
        }

        singleTrader.Init();

        _timer!.RestartTimer("1");
        _timer!.RestartTimer("2");

        IsRunning = true;
        await Task.Run(() =>
        {
            singleTrader.IsStarted = true;
            singleTrader.IsRunning = true;
            singleTrader.IsStopped = false;
            singleTrader.IsStopRequested = false;

            for (int i = 0; i < totalBars; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

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
            // Tarama bilgileri: Finalize gerek kalmadan alınabilir (bkz. Tarama Özeti)
            var yon           = singleTrader.SonYon;
            var kacBarOnce    = singleTrader.SonSinyaldenBeriBarSayisi;
            var karZarar      = singleTrader.SonKarZararFiyat;
            var karZararYuzde = singleTrader.SonKarZararYuzde;
            var ozet          = singleTrader.TaramaOzeti;
            Log($"\nScreening summary... : {ozet}");
        }

        _timer!.RestartTimer("3");
        singleTrader.Finalize();

        // Pipe ile (default)
        var header = singleTrader.GetStatisticsHeaderRow();
        var data   = singleTrader.GetStatisticsDataRow();
        // Noktalı virgülle (CSV için)
        var csvHeader = singleTrader.GetStatisticsHeaderRow(";");
        var csvData   = singleTrader.GetStatisticsDataRow(";");

        // Dosyaya yazma WriteTraderDataToFilesAsync'e taşındı — grafik açıkken arka planda çalışsın diye

        _timer!.StopTimer("3");

        if (this.SingleTraderRunMode == TraderRunMode.TradeAndQuery || this.SingleTraderRunMode == TraderRunMode.QueryOnly)
        {
            var sorguOzeti = singleTrader.SorguOzeti;
            Log($"\nQuery summary... : {sorguOzeti}");
        }

        _timer!.StopTimer("1");
        _timer!.StopTimer("0");
        // t0-t3 elapsed time logları...
    }
    catch (Exception ex)
    {
        Log($"An error occurred while running in RunSingleTraderWithProgressAsync(): {ex.Message}");
    }
    finally { }

    if (singleTrader is not null)
    {
        singleTrader.IsRunning = false;
        singleTrader.IsStopped = true;
    }
}
```

> **Not — hata olursa `catch` sadece loglar, yeniden fırlatmaz:** `catch (Exception ex)` bloğu
> `Log(...)` ile yazıp yutuyor — metod `Task` döndüğü için çağıran taraf (`runSingleTraderAlgoTrade()`)
> exception'ı hiç görmüyor, akış normal şekilde devam ediyor (`WriteTraderDataToFilesAsync`
> çağrısına kadar gidiyor). `singleTrader` `new SingleTrader(...)` aşamasında bile başarısız
> olsa `finally` sonrası `if (singleTrader is not null)` kontrolü `null` durumunu güvenle atlıyor,
> ama `WriteTraderDataToFilesAsync(algoTrader.SingleTrader)` çağrısı `runSingleTraderAlgoTrade()`
> içinde `singleTrader`'ın `null` olabileceğini varsaymıyor — pratikte `strategy`/`indicators`
> yaratma adımları `new`'den hemen sonra `throw` ettiği için `singleTrader` genelde ya tam kurulu
> ya da hiç yaratılmamış olur, ama bu, çağıranın örtük bir varsayımı, kod tarafından garanti
> edilmiyor.

## Callback'lerin Gerçek Gövdeleri (`AlgoTrader.cs:158-223`)

`SetCallbacks(...)`'in bağladığı 8 metodun (`OnApplyUserFlags` hariç, o hiç kullanılmıyor —
bkz. [Event'ler](#eventler) tablosu) gerçek gövdeleri:

**`OnSingleTraderReset`/`Init`/`Run`/`Final`** — dördü de tamamen boş gövde:

```csharp linenums="1"
private void OnSingleTraderReset(SingleTrader trader, int mode) { }
private void OnSingleTraderInit(SingleTrader trader, int mode) { }
private void OnSingleTraderRun(SingleTrader trader, int mode) { }
private void OnSingleTraderFinal(SingleTrader trader, int mode) { }
```

**`OnSingleTraderBeforeOrder`/`OnSingleTraderAfterOrder`/`OnSingleTraderNotifySignal`** — üçü de
boş, ikisinde açıklayıcı yorum var (genişletme noktası olarak bırakılmış):

```csharp linenums="1"
// Callback function to be assigned to SingleTrader.Callback
// Runs right after emirleri_uygula(i) for each bar
private void OnSingleTraderBeforeOrder(SingleTrader trader, int barIndex)
{
    // Example: you can inspect last signal/direction here
    // Logger?.Log($"CB | Bar={barIndex} Yon={trader.signals.SonYon} EmirStatus={trader.signals.EmirStatus}");
    // No-op by default
}

private void OnSingleTraderNotifySignal(SingleTrader trader, string signal, int barIndex) { }

private void OnSingleTraderAfterOrder(SingleTrader trader, int barIndex) { }
```

**`OnSingleTraderProgress`** — tek dolu gövdeli callback, Console'a `\r` ile üzerine-yazan bir
ilerleme satırı basar (`ProgressLoggingEnabled` bayrağı kapalıysa hiçbir şey yapmaz):

```csharp linenums="1"
private void OnSingleTraderProgress(SingleTrader trader, int currentBar, int totalBars, double percentage)
{
    if (_logger == null) return;
    if (!ProgressLoggingEnabled) return;

    var consoleLogger = LogManager.GetConsoleLogger();

    if (currentBar >= totalBars)
    {
        consoleLogger.Write($"\r\tProgress         : {currentBar}/{totalBars} ({percentage:F1}%)");
        consoleLogger.WriteLine("");
    }
    else
    {
        consoleLogger.Write($"\r\tProgress         : {currentBar}/{totalBars} ({percentage:F1}%)");
    }
}
```

- `OnSingleTraderNotifySignal` boş olsa da bunun bir önemi yok — zaten `SingleTrader.OnNotifySignal`
  hiçbir zaman `Invoke` edilmediği için (bkz. yukarıdaki [Not](#eventler)) bu callback zaten
  çağrılmıyor.
- `ApplySingleTraderFlagsConfigs(trader)` (`AlgoTrader.cs:1066-1141`) — `OnApplyUserFlags`/
  `OnApplyUserFlags2` adlı iki eski (artık kullanılmayan) private metodun yerini almış:
  `trader.ConfigureUserFlagsOnce()` çağırır, sonra sırasıyla `_singleTraderSignalsConfig`,
  `_singleTraderOptimizationConfig`, `_singleTraderSaveConfig` (12 flag + 12 dosya adı — boş
  string'ler orijinal varsayılanı korur), `_singleTraderPlotConfig`, `_singleTraderExportConfig`
  doluysa trader'ın ilgili alanlarına kopyalar — bkz. [AppConfig Kaynağı](#appconfig-kaynağı--singletraderconfig).

## Dönüş / Sonuç — Global State

`runSingleTraderAlgoTrade()` (Console) / `RunSingleTraderWithProgressAsync()` (AlgoTrader)
bittiğinde güncellenen state:

| Değişken/Erişim | Tip | Kaynak |
|---|---|---|
| `algoTrader` (Program.cs global) | `AlgoTrader` | `new AlgoTrader("AlgoTrader")` |
| `algoTrader.SingleTrader` | `SingleTrader` (public getter, `private set`) | `RunSingleTraderWithProgressAsync()` içinde yaratılan `singleTrader` |
| `algoTrader.SingleTrader.SonYon`/`SonKarZararFiyat`/`SonKarZararYuzde`/`TaramaOzeti` | string/double/string | `Finalize()`'dan ÖNCE bile okunabilir, bkz. [Tarama Özeti](#tarama-özeti-screening-properties) |
| 12+ istatistik dosyası | `outputs/logs/*.txt`/`*.csv` | `WriteTraderDataToFilesAsync(algoTrader.SingleTrader)` → `singleTrader.WriteStatisticsToFile(...)` |

- `stockDataReader`/`stockDataList`/`stockMetaData` (bkz. [StockDataReader § Dönüş/Sonuç](09-stockdatareader.md#dönüş--sonuç--global-state))
  bu akışın ÖN KOŞULU — `runSingleTraderAlgoTrade()`'in ilk satırı bunları kontrol ediyor.
- Hata (`RunSingleTraderWithProgressAsync`'in `catch` bloğu) sadece loglanıyor, `algoTrader.
  SingleTrader` yine de (kısmen kurulu) bir instance olarak kalabilir — bkz. yukarıdaki Not.

## Tipik Kullanım — Script'ten Çağrılma (Manuel Kurulum)

- Konum: `Program.cs`/`AlgoTrader` akışının DIŞINDA — bir `.csx` script'inde `SingleTrader`'ı
  doğrudan kurmak istediğinde (`AlgoTrader`'ın 25 satırlık config/callback wiring'ini atlayıp tam
  kontrol istediğinde). Gerçek örnek: `inputs/scripts/01_RunSingleTraderWithProgressAsync.csx`.
- `AlgoTrader.RunSingleTraderWithProgressAsync()`'in yaptığı adımların manuel (kısaltılmış) hali:

**1) Data + Indicators + Strategy hazırlığı**

```csharp linenums="1"
var reader = new StockDataReader();
reader.Clear();
reader.ReadMetaData(filePath);
var data = reader.ReadDataFast(filePath);

var indicators = new IndicatorManager(data);
var strategy = new SimpleMostStrategy(data, indicators, logger: null);
```

**2) SingleTrader oluşturma**

```csharp linenums="1"
var singleTrader = new SingleTrader(0, "singleTrader", data, indicators, null);
```

**3) Strateji atama + kurulum sırası**

```csharp linenums="1"
singleTrader.SetStrategy(strategy);
singleTrader.RunMode = TraderRunMode.TradeOnly;

singleTrader.initialTradeParams
    .Reset()
    .SetBakiyeParams(ilkBakiye: 100000.0)
    .SetKomisyonParams(komisyonCarpan: 0.0)
    .SetKaymaParams(kaymaMiktari: 0.0)
    .SetKontratParamsFxCrypto(lotSayisi: 0.01);

singleTrader.ConfigureUserFlagsOnce();
singleTrader.signals.AlEnabled = true;
singleTrader.signals.SatEnabled = true;
singleTrader.signals.FlatOlEnabled = true;
```

**4) Bar-bar çalıştırma**

```csharp linenums="1"
singleTrader.Init();

for (int i = 0; i < data.Count; i++)
{
    singleTrader.Run(i);
}

singleTrader.Finalize();
```

**5) Sonuçları okuma + dosyaya yazma**

```csharp linenums="1"
Log($"Özet: {singleTrader.TaramaOzeti}");

singleTrader.WriteStatisticsToFile(outputDir, inputsDir);
singleTrader.Dispose();
```

**6) `OnNotifySignal`'e abone olmak istersen** (bkz. yukarıdaki Not — bu abonelik hiçbir zaman
tetiklenmeyecek, sadece `ScriptGlobals.cs`'nin sunduğu API'nin gerçek davranışını göstermek için):

```csharp linenums="1"
trader.OnNotifySignal += _signalHandler;   // ScriptGlobals.cs:155 — asla çağrılmayacak
```

## Console/JSON Eşleşmesi

Yukarıdaki 6 adımlık script akışının Console karşılığı — kod yazmadan, `AppConfig.json`
düzenleyerek:

1. `inputs/configs/AppConfig/AppConfig.json` dosyasını aç.
2. `"SingleTrader"` bölümünü (bkz. [AppConfig Kaynağı](#appconfig-kaynağı--singletraderconfig)
   yukarıdaki tam örnek) düzenle: `Strategy.Name`/`Version` ile stratejiyi, `TradeParams` ile
   pozisyon büyüklüğünü, `Signals` ile hangi sinyallerin aktif olacağını seç.
3. Kaydet, `AlgoTrade.Console`'u çalıştır, menüden `[2] SingleTrader` (veya `[5]` "Read Data +
   SingleTrader") seç, `RunMode` için `[1]`/`[2]`/`[3]` gir (veya `[ENTER]` ile JSON'daki
   `RunMode`'u kullan).

Arkada `runSingleTraderAlgoTrade()` → `AppConfigApplier.ApplySingleTrader(...)` →
`RunSingleTraderWithProgressAsync()` içindeki `ApplySingleTraderFlagsConfigs(singleTrader)` bu
JSON'u senin yerine yukarıdaki adım-3'teki `singleTrader.signals.AlEnabled = true;` gibi
atamalara çevirir — alan adları değişir (`AlEnabled` ↔ `"AlEnabled"`, `LotSayisi` ↔ `"LotSayisi"`)
ama mantık birebir aynı.

## Kimler Kullanıyor — Instantiation Noktaları

`new SingleTrader(...)` için tüm kod tabanında (`AlgoTrade.Core`, `AlgoTrade.Console`,
`inputs/scripts/*.csx`) grep taraması — **25 çağırım noktası**, tamamı `AlgoTrade.Core` içindeki
diğer trader/scanner sınıflarından veya script'lerden; `Program.cs` **doğrudan** `new
SingleTrader(...)` yazmıyor (her zaman `AlgoTrader` üzerinden dolaylı).

**`AlgoTrade.Core` — diğer sınıfların içinde (14 nokta)**

| Dosya | Bağlam | Satır |
|---|---|---|
| `Trading/AlgoTrader.cs` | `RunSingleTraderWithProgressAsync()` — `singleTrader` (id=0) | 1332 |
| `Trading/AlgoTrader.cs` | `RunMultipleTraderWithProgressAsync()` içi — `childTrader` | 1633 |
| `Trading/AlgoTrader.cs` | `RunConfirmingMultipleTraderWithProgressAsync()` içi — `childTrader` | 1769 |
| `Trading/Traders/MultipleTrader.cs` | constructor — `_mainTrader` (id=-1) | 107 |
| `Trading/Traders/ConfirmingSingleTrader.cs` | constructor — `_signalTrader` (id) | 123 |
| `Trading/Traders/ConfirmingSingleTrader.cs` | constructor — `_mainTrader` (id=-1) | 129 |
| `Trading/Traders/ConfirmingMultipleTrader.cs` | constructor — `_mainTrader` (id=-1) | 116 |
| `Trading/Traders/SingleTraderOptimizer.cs` | her parametre kombinasyonu için — `singleTrader` | 209 |
| `Trading/Traders/MultipleQuery.cs` | her satır için — `trader` (id) | 42 |
| `Trading/Traders/SymbolScanner.cs` | her sembol için — `trader` (id=0) | 138 |
| `Trading/Traders/TimeframeScanner.cs` | her zaman dilimi için — `trader` (id=0) | 121 |
| `Trading/Traders/SymbolTimeframeScanner.cs` | sembol × TF için — `trader` (id=0) | 152 |
| `Trading/Traders/QuerySymbolScanner.cs` | her sembol için — `trader` (id=0) | 130 |
| `Trading/Traders/QueryTimeframeScanner.cs` | her zaman dilimi için — `trader` (id=0) | 109 |
| `Trading/Traders/QuerySymbolTimeframeScanner.cs` | sembol × TF için — `trader` (id=0) | 142 |

**`inputs/scripts/*.csx` — Scriptler (10 nokta)**

| Dosya | Değişken adı | Satır |
|---|---|---|
| `01_RunSingleTraderWithProgressAsync.csx` | `singleTrader` | 191 |
| `02_RunMultipleTraderWithProgressAsync.csx` | `childTrader` (× 3 — id 0/1/2) | 144, 187, 230 |
| `04_GenerateDearPyGuiDataPlotterBundle.csx` | `singleTrader` | 93 |
| `07_RunConfirmingMultipleTraderWithProgressAsync.csx` | `childTrader` | 137 |
| `CustomConsensusExample.csx` | `child` | 98 |

- `MultipleTrader`/`ConfirmingSingleTrader`/`ConfirmingMultipleTrader` hep `id=-1` ile bir
  "mainTrader" yaratıyor — Scanner ailesi ve `MultipleQuery` her zaman `id=0` (tekrar kullanılan,
  throwaway trader). Bu, `AlgoTrader.OnApplyUserFlags`'in eski `traderId==-1`/`0`/`1`/`2` switch'inin
  (artık kullanılmayan, bkz. [Callback'lerin Gerçek Gövdeleri](#callbacklerin-gerçek-gövdeleri-algotradercs158-223))
  neden var olduğunu açıklıyor — id'ye göre farklı zaman aralığı/bayrak ayarı öngörülmüştü.
- `docs/reference/old-project-confirming/ConfirmingSingleTrader.cs` (eski proje referansı,
  `not_in_nav` ile mkdocs'tan hariç tutuluyor) grep sonucunda çıktı ama bu tabloya dahil edilmedi
  — güncel kod değil, sadece tarihsel referans.

## Kullanım Haritası

`SingleTrader`'ın (kendi + `MarketDataProvider`'dan miras) public üyelerinden, **Console akışında
(`Program.cs` + `AlgoTrader.cs`) fiilen tetiklenenler** ile **hiç tetiklenmeyenler**:

| Üye | Durum | Nerede |
|---|---|---|
| Constructor, `SetStrategy`/`SetQuery`, `Reset`/`Init`/`Run`/`Finalize`, `RunMode`, `ClearCallbacks`/`SetCallbacks` | ✅ | `RunSingleTraderWithProgressAsync()` (yukarıda tam kaynağıyla var) |
| `SonYon`/`SonSinyaldenBeriBarSayisi`/`SonKarZararFiyat`/`SonKarZararYuzde`/`TaramaOzeti` | ✅ | `RunSingleTraderWithProgressAsync()` satır 138-142 (Finalize'dan önce) + tüm Scanner sınıflarının `ScanResult` doldurması |
| `SorguOzeti` | ✅ | `RunSingleTraderWithProgressAsync()` satır 163 (TradeAndQuery/QueryOnly'de) |
| `GetStatisticsHeaderRow`/`GetStatisticsDataRow` | ✅ | `RunSingleTraderWithProgressAsync()` (hem `"|"` hem `";"` ayraçla) |
| `WriteStatisticsToFile` | ✅ | `AlgoTrader.WriteTraderDataToFilesAsync(SingleTrader)` overload'ı üzerinden |
| `IsStarted`/`IsRunning`/`IsStopped`/`IsStopRequested` | ✅ | Bar döngüsü sırasında set/okunuyor (iptal desteği) |
| `is_son_yon_a/_s/_f`, `is_prev_yon_a/_s/_f` | ✅ | `MultipleTrader.BuildConsensusSignal()`, `ApplyEquityCurveFilter`, `ResolveFilterDecisions` (sınıfın kendi içinde de kullanılıyor) |
| `PlotEnabled` | ✅ | `runSingleTraderAlgoTrade()` — Python/DearPyGuiDataPlotter dalını kontrol eder |
| `OptimizationEnabled` | ✅ | `Finalize()` içinde `CalculatePerformances`'ı atlamak için; `SingleTraderOptimizer` kendi çalıştırdığı trader'larda `true` set eder |
| `OnProgress` (event) | ✅ | `OnSingleTraderProgress` — tek dolu gövdeli callback |
| `OnBeforeOrder`/`OnAfterOrder` (event) | ⚠️ | Bağlanıyor ama Console tarafındaki gövdeleri boş — sadece script'ten kendi callback'ini geçersen anlam kazanır |
| `OnReset`/`OnInit`/`OnRun`/`OnFinal` (event) | ⚠️ | Bağlanıyor ama Console tarafındaki gövdeleri boş |
| `OnNotifySignal` (event) | ❌ | Bağlanabiliyor (`ScriptGlobals.cs`'te bile abone olma API'si var) ama sınıf içinde HİÇBİR yerden `Invoke` edilmiyor — bkz. [Not](#eventler) |
| `OnApplyUserFlags` (event) | ❌ | Hiç `Invoke` edilmiyor, `AlgoTrader.ApplySingleTraderFlagsConfigs()` tamamen farklı bir yoldan aynı işi yapıyor |
| `ClosePositionEOD_2` | ❌ | Hiçbir yerden çağrılmıyor — bkz. [Not](#gün-sonu-kapatma-closepositioneod) |
| `MaxPositionSize`/`MaxPositionSizeEnabled` (Console/AppConfig yolundan) | ❌ | `AppConfig.json`'da karşılık alan yok, sadece script'ten elle set edilebilir — bkz. [AppConfig Kaynağı](#appconfig-kaynağı--singletraderconfig) alt notu |
| Minimal Stats/Lists CSV/TXT yazımı (`SaveMinimalStatsCsvEnabled` vb. bayrakların gerçek yazım kodu) | ❌ | `WriteStatisticsToFile()` içinde yorum satırı halinde devre dışı (`2590-2614`) — bayraklar AppConfig'ten set edilebiliyor ama hiçbir dosya üretmiyor |
| `LastStatisticsCalculationTime` | ❌ | Property tanımlı, hiçbir yerde set edilmiyor (`LastResetTime`/`LastExecutionTime*` set ediliyor ama bu alan hiç dokunulmuyor) |
| `MultipleTraderModeEnabled` | ❌ | `Reset()` içinde `false`'a çekiliyor, başka hiçbir yerde okunmuyor/set edilmiyor (`MultipleTrader` kendi ayrı mekanizmasını kullanıyor, bu bayrağı hiç kullanmıyor) |

## İlgili Dosyalar

- [01-class-reference.md § 3. SingleTrader](../01-class-reference.md#3-singletrader--çekirdek-motor) —
  bu sayfanın ait olduğu index, kısa özet.
- [09-stockdatareader.md](09-stockdatareader.md) — aynı derinlikte belgelenen kardeş sayfa,
  `SingleTrader`'ın verisinin kaynağı (`Data`/`GetData()` `MarketDataProvider`'dan ortak).
- [06-class-doc-method.md](../06-class-doc-method.md) — bu sayfanın yazıldığı yöntem.
- [02-console-menu-guide.md](../02-console-menu-guide.md) — Console menü rehberi, `[2]`/`[5]`
  satırları.
- `docs/SingleTraderAkis.md` — `SingleTrader`'ın eski (2026-08 öncesi) bir akış özeti; bazı
  detayları güncel değil (örn. `WriteStatisticsToFile(outputDir, bool...)` imzasını 10 bool
  parametreli olarak gösteriyor, güncel imza `WriteStatisticsToFile(outputDir, inputsDir)` —
  bkz. yukarıdaki [İstatistik/Rapor](#istatistikrapor)) — genel akış fikrini vermek için hâlâ
  faydalı ama satır/imza detaylarında bu sayfa esas alınmalı.
- `docs/PROJECT_ANALYSIS.md` — `ClosePositionEOD_2` ölü kod bulgusunun ilk kaynağı.
