# SingleTraderOptimizer — Grid-Search Optimizasyon (Menü [4])

> [Class Reference](../01-class-reference.md) setinin bir parçası — bu sınıf ayrı dosyada,
> [SingleTrader](02-singletrader.md)/[MultipleTrader](03-multipletrader.md)/[StockDataReader](09-stockdatareader.md)
> gibi diğer sınıflardan çok daha derin işlendi. Yöntem: [06-class-doc-method.md](../06-class-doc-method.md).

### Dosyalar

- `src/AlgoTrade.Core/Trading/Traders/SingleTraderOptimizer.cs` (935 satır) — `StrategyFactory`
  delegate + `ParameterRange`/`OptimizationResult` yardımcı tipleri de aynı dosyada tanımlı.
- `src/AlgoTrade.Core/Trading/Traders/SingleTrader.cs` — her parametre kombinasyonu için
  `createSingleTrader()`'ın yarattığı throwaway trader, bkz. [SingleTrader dokümanı](02-singletrader.md)
- `src/AlgoTrade.Core/Trading/Utils/StatisticsExporter.cs` — `LoadOptimizationColumns(...)`,
  CSV/TXT sütun tanımlarını `StatisticsExporterConfig.json`'dan okur (bu dokümanın kapsamı dışında)

### Rolü

- Bir stratejinin parametre uzayını (`ParameterRange` listesi) **kartezyen çarpımla** tarayıp
  her kombinasyon için ayrı, throwaway bir `SingleTrader` çalıştırır, sonuçları sıralı dosyaya
  yazar.
- FARKLI stratejileri karşılaştırmaz — tek bir stratejinin parametrelerini tarar (farklı
  stratejileri karşılaştırmak için bkz. [MultipleTrader § Trader-bazlı Özet
  Karşılaştırma](03-multipletrader.md#trader-bazlı-özet-karşılaştırma-writemultipletraderstatistics)).
- `MarketDataProvider`'dan TÜREMEZ (kendi `Data`/`Indicators` property'lerini doğrudan tutar,
  SingleTrader'ın aksine) — `IDisposable`'ı gerçekten implement eder (`: IDisposable`).

### Ne zaman kullanılır

- "Bu stratejinin en iyi period/multiplier kombinasyonu hangisi?" sorusuna cevap ararken.
  Console `[4]`/`[7]`.
- `PartialOpt` (`OptimizationFrom`/`OptimizationTo`) ile çok büyük bir taramayı parçalara bölüp
  kesintiye uğrarsa kaldığı yerden devam ettirmek istediğinde.

### Sınıf İskeleti (ilk bakış)

Aşağıdaki bloktaki metod gövdeleri kaldırılmış — sadece alan/property/event/metod imzaları
(public + private, hepsi), gerçek kaynağın (`SingleTraderOptimizer.cs`) sırasıyla birebir aynı.
`Run()` ve `createSingleTrader()` istisna: gövdeleri [Run() — Optimizasyon Döngüsü](#run--optimizasyon-döngüsü)
ve [`createSingleTrader()`](#createsingletrader--her-kombinasyon-için-throwaway-trader) altında
ayrıca gösteriliyor.

```csharp linenums="1"
public delegate IStrategy StrategyFactory(List<StockData> data, IndicatorManager indicators, Dictionary<string, object> parameters);

public class ParameterRange
{
    public string Name { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Step { get; set; }

    public ParameterRange(string name, double min, double max, double step);
    public List<double> GetValues();
}

public class OptimizationResult
{
    public Dictionary<string, string> Parameters { get; set; }
    public Dictionary<string, string> Values { get; set; }

    public double NetProfit       => TryGetD("NetProfit");
    public double WinRate         => TryGetD("WinRate");
    public double ProfitFactor    => TryGetD("ProfitFactor");
    public double ProfitFactorNet => TryGetD("ProfitFactorNet");
    public double MaxDrawdown     => TryGetD("MaxDrawdown");
    public double ScoreFiyatNet   => TryGetD("ScoreFiyatNet");
    public double ScoreFiyat      => TryGetD("ScoreFiyat");
    public double ScorePuan       => TryGetD("ScorePuan");
    public string StrategyName    => Values.GetValueOrDefault("StrategyName", "");

    public OptimizationResult();
    private double TryGetD(string key);
}

public class SingleTraderOptimizer : IDisposable
{
    public int Id { get; private set; }
    public List<StockData> Data { get; private set; }
    public IndicatorManager Indicators { get; private set; }
    public StrategyFactory? StrategyFactoryMethod { get; private set; }
    public List<ParameterRange> ParameterRanges { get; private set; }
    public List<OptimizationResult> Results { get; private set; }
    public List<Dictionary<string, object>> AllCombinations { get; private set; }
    public bool IsInitialized { get; private set; }

    private LogManager? Logger { get; set; }

    // ---- Progress callbacks ----
    public event Action<SingleTraderOptimizer, int, int, double>? OnOptimizationProgress;
    public Action<SingleTrader, int, int, double>? OnSingleTraderProgressCallback { get; set; }
    public event Action<SingleTraderOptimizer, SingleTrader, int>? OnReadOptimizationResultsFile;

    // ---- State flags ----
    public bool IsStarted { get; internal set; }
    public bool IsRunning { get; internal set; }
    public bool IsStopped { get; internal set; }
    public bool IsStopRequested { get; internal set; }

    // ---- Optimization range (PartialOpt) ----
    public int OptimizationFrom { get; set; } = -1;
    public int OptimizationTo { get; set; } = -1;

    // ---- Save intermediate results ----
    public int SaveEveryN { get; set; }
    public event Action<List<OptimizationResult>, int>? OnSaveResults;

    // ---- Optimization log file settings ----
    public bool CsvFileLoggingEnabled { get; set; }
    public string CsvFilePath { get; set; } = "";
    public bool TxtFileLoggingEnabled { get; set; }
    public string TxtFilePath { get; set; } = "";
    public bool AppendEnabled { get; set; }
    public string ConfigFilePath { get; set; } = "";

    // ---- Sorted output ----
    public string SortField { get; set; } = "ProfitFactor";
    public string SortedCsvFilePath { get; set; } = "";
    public string SortedTxtFilePath { get; set; } = "";

    public int FileFlushIntervalMs { get; set; } = -1;

    private readonly HashSet<string> _initializedFiles = new HashSet<string>();
    private List<(int CombNo, OptimizationResult Result)>? _cachedOptResults = null;
    private readonly List<(int CombNo, OptimizationResult Result)> _pendingFlushResults = new();
    private readonly System.Diagnostics.Stopwatch _flushStopwatch = new();

    // ---- Kurulum ----
    public SingleTraderOptimizer(int id, List<StockData> data, IndicatorManager indicators, LogManager? logger);

    // ---- Configuration ----
    public void AddParameterRange(string name, double min, double max, double step);
    public void SetStrategyFactory(StrategyFactory factory);

    // ---- Run ----
    public void Reset();
    public void Init();
    public void Stop();

    public SingleTrader createSingleTrader();
    public void runSingleTrader(SingleTrader singleTrader, int totalBars, CancellationToken cancellationToken = default);
    public OptimizationResult? Run(CancellationToken cancellationToken = default);
    public OptimizationResult? GetBestResult();

    private void AppendSingleOptSummaryToFiles(OptimizationResult optResult, int currentCombination);
    private void WriteResultToFiles(OptimizationResult optResult, int currentCombination);
    private void WriteSortedFilesIfEnabled();
    private void FlushPendingToFiles();
    private void AppendSingleOptSummaryCsvFromConfig(OptimizationResult optResult, int currentCombination, string filePath);
    private void AppendSingleOptSummaryTxtFromConfig(OptimizationResult optResult, int currentCombination, string filePath);
    private static double ParseD(Dictionary<string, string> map, string key);
    private string GetOptColumnValue(string field, Dictionary<string, string> optResultsMap);
    private void LoadOptCsvToCache();
    public void WriteSortedFiles();
    private void WriteSortedCsv(List<(int CombNo, OptimizationResult Result)> sorted, string filePath);
    private void WriteSortedTxt(List<(int CombNo, OptimizationResult Result)> sorted, string filePath);

    // ---- Parameter Combinations ----
    public List<Dictionary<string, object>> GenerateParameterCombinations();
    private void GenerateCombinationsRecursive(int paramIndex, Dictionary<string, object> current, List<Dictionary<string, object>> results);

    // ---- SingleTrader Callbacks (no-op) ----
    private void OnSingleTraderReset(SingleTrader trader, int mode);
    private void OnSingleTraderInit(SingleTrader trader, int mode);
    private void OnSingleTraderRun(SingleTrader trader, int mode);
    private void OnSingleTraderFinal(SingleTrader trader, int mode);
    private void OnSingleTraderBeforeOrder(SingleTrader trader, int barIndex);
    private void OnSingleTraderNotifySignal(SingleTrader trader, string signal, int barIndex);
    private void OnSingleTraderAfterOrder(SingleTrader trader, int barIndex);
    private void OnSingleTraderProgress(SingleTrader trader, int currentBar, int totalBars, double percentage);

    public EquityCurveFilterConfigEntry? EquityCurveFilterConfig { get; set; }
    public SingleTraderSignalsConfig?    SignalsConfig           { get; set; }

    private void ApplyConfigsToTrader(SingleTrader trader);
    private void SetSingleTraderConfigureEquityCurveFilter(SingleTrader trader);

    // ---- Attributes (SingleTrader'a atanacak bilgiler) ----
    public string SymbolName { get; set; } = "";
    public string SymbolPeriod { get; set; } = "";
    public string SystemId { get; set; } = "";
    public string SystemName { get; set; } = "";
    public string StrategyId { get; set; } = "";
    public string StrategyName { get; set; } = "";
    public string QueryId { get; set; } = "";
    public string QueryName { get; set; } = "";

    public double IlkBakiye      { get; set; } = 100000.0;
    public int    KontratSayisi  { get; set; } = 1;
    public double KomisyonCarpan { get; set; } = 20.0;
    public double KaymaMiktari   { get; set; } = 0.5;

    public InitialTradeParams? TradeParamsOverride { get; set; }

    // ---- Dispose ----
    public void Dispose();
}
```

### Üye İndeksi — Hangisi Nerede Anlatılıyor

Yukarıdaki iskeletteki her üye, kaynak sırasıyla — aşağıdaki Public API bölümlerinden hangisinde
detaylandırıldığına link veriyor. **#** kolonu yukarıdaki kod bloğunun gerçek satır numarasıyla
birebir eşleşiyor. `ParameterRange`/`OptimizationResult` yardımcı tipleri `SingleTraderOptimizer::`
öneki almıyor (ayrı sınıflar, sınıfın dışında tanımlı).

| # | Üye | Tür | Detay |
|---|---|---|---|
| 1 | `StrategyFactory` (delegate) | delegate | [Kurulum ve Konfigürasyon](#kurulum-ve-konfigürasyon) |
| 3-12 | `ParameterRange` (sınıf, `Name`/`Min`/`Max`/`Step`, constructor, `GetValues()`) | class | [Parametre Kombinasyonları](#parametre-kombinasyonları) |
| 14-31 | `OptimizationResult` (sınıf, `Parameters`/`Values` + 9 convenience getter, constructor, `TryGetD`) | class | [`OptimizationResult` — Sonuç Tipi](#optimizationresult--sonuç-tipi) |
| 35 | `SingleTraderOptimizer::Id` | public property | [Kurulum ve Konfigürasyon](#kurulum-ve-konfigürasyon) |
| 36 | `SingleTraderOptimizer::Data` | public property | [Kurulum ve Konfigürasyon](#kurulum-ve-konfigürasyon) |
| 37 | `SingleTraderOptimizer::Indicators` | public property | [Kurulum ve Konfigürasyon](#kurulum-ve-konfigürasyon) |
| 38 | `SingleTraderOptimizer::StrategyFactoryMethod` | public property | [Kurulum ve Konfigürasyon](#kurulum-ve-konfigürasyon) |
| 39 | `SingleTraderOptimizer::ParameterRanges` | public property | [Parametre Kombinasyonları](#parametre-kombinasyonları) |
| 40 | `SingleTraderOptimizer::Results` | public property | [Run() — Optimizasyon Döngüsü](#run--optimizasyon-döngüsü) |
| 41 | `SingleTraderOptimizer::AllCombinations` | public property | [Parametre Kombinasyonları](#parametre-kombinasyonları) |
| 42 | `SingleTraderOptimizer::IsInitialized` | public property | [Kurulum ve Konfigürasyon](#kurulum-ve-konfigürasyon) |
| 44 | `SingleTraderOptimizer::Logger` | private property | [Kurulum ve Konfigürasyon](#kurulum-ve-konfigürasyon) |
| 47 | `SingleTraderOptimizer::OnOptimizationProgress` | public event | [Event'ler](#eventler) — bkz. Not, callback boş |
| 48 | `SingleTraderOptimizer::OnSingleTraderProgressCallback` | public property (delegate) | [Event'ler](#eventler) — bkz. Not, callback boş |
| 49 | `SingleTraderOptimizer::OnReadOptimizationResultsFile` | public event | [Event'ler](#eventler) |
| 52 | `SingleTraderOptimizer::IsStarted` | public property (`internal set`) | [Run() — Optimizasyon Döngüsü](#run--optimizasyon-döngüsü) |
| 53 | `SingleTraderOptimizer::IsRunning` | public property (`internal set`) | [Run() — Optimizasyon Döngüsü](#run--optimizasyon-döngüsü) |
| 54 | `SingleTraderOptimizer::IsStopped` | public property (`internal set`) | [Run() — Optimizasyon Döngüsü](#run--optimizasyon-döngüsü) |
| 55 | `SingleTraderOptimizer::IsStopRequested` | public property (`internal set`) | [Run() — Optimizasyon Döngüsü](#run--optimizasyon-döngüsü) — `Stop()` ile set edilir |
| 58 | `SingleTraderOptimizer::OptimizationFrom` | public property | [PartialOpt](#partialopt-optimizationfrom--optimizationto) |
| 59 | `SingleTraderOptimizer::OptimizationTo` | public property | [PartialOpt](#partialopt-optimizationfrom--optimizationto) |
| 62 | `SingleTraderOptimizer::SaveEveryN` | public property | Ölü kod — bkz. [Not](#run--optimizasyon-döngüsü), kontrol edildiği yerde gövde tamamen yorum satırı |
| 63 | `SingleTraderOptimizer::OnSaveResults` | public event | Ölü kod — hiçbir yerden `Invoke` edilmiyor |
| 66 | `SingleTraderOptimizer::CsvFileLoggingEnabled` | public property | [Dosyaya Yazma](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) |
| 67 | `SingleTraderOptimizer::CsvFilePath` | public property | [Dosyaya Yazma](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) |
| 68 | `SingleTraderOptimizer::TxtFileLoggingEnabled` | public property | [Dosyaya Yazma](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) |
| 69 | `SingleTraderOptimizer::TxtFilePath` | public property | [Dosyaya Yazma](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) |
| 70 | `SingleTraderOptimizer::AppendEnabled` | public property | [Dosyaya Yazma](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) |
| 71 | `SingleTraderOptimizer::ConfigFilePath` | public property | [Dosyaya Yazma](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) — `StatisticsExporterConfig.json` yolu |
| 74 | `SingleTraderOptimizer::SortField` | public property | [Sıralı Çıktı](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) |
| 75 | `SingleTraderOptimizer::SortedCsvFilePath` | public property | [Sıralı Çıktı](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) |
| 76 | `SingleTraderOptimizer::SortedTxtFilePath` | public property | [Sıralı Çıktı](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) |
| 78 | `SingleTraderOptimizer::FileFlushIntervalMs` | public property | [Dosyaya Yazma](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) |
| 80 | `SingleTraderOptimizer::_initializedFiles` | private field | [Dosyaya Yazma](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) — iç yardımcı state |
| 81 | `SingleTraderOptimizer::_cachedOptResults` | private field | [Dosyaya Yazma](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) — iç yardımcı state |
| 82 | `SingleTraderOptimizer::_pendingFlushResults` | private field | [Dosyaya Yazma](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) — iç yardımcı state |
| 83 | `SingleTraderOptimizer::_flushStopwatch` | private field | [Dosyaya Yazma](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) — iç yardımcı state |
| 86 | `SingleTraderOptimizer::SingleTraderOptimizer(...)` | constructor | [Kurulum ve Konfigürasyon](#kurulum-ve-konfigürasyon) |
| 89 | `SingleTraderOptimizer::AddParameterRange(...)` | public method | [Parametre Kombinasyonları](#parametre-kombinasyonları) |
| 90 | `SingleTraderOptimizer::SetStrategyFactory(factory)` | public method | [Kurulum ve Konfigürasyon](#kurulum-ve-konfigürasyon) |
| 93 | `SingleTraderOptimizer::Reset()` | public method | [Kurulum ve Konfigürasyon](#kurulum-ve-konfigürasyon) |
| 94 | `SingleTraderOptimizer::Init()` | public method | [Kurulum ve Konfigürasyon](#kurulum-ve-konfigürasyon) — bkz. Not, boş gövde |
| 95 | `SingleTraderOptimizer::Stop()` | public method | [Run() — Optimizasyon Döngüsü](#run--optimizasyon-döngüsü) |
| 97 | `SingleTraderOptimizer::createSingleTrader()` | public method | [`createSingleTrader()`](#createsingletrader--her-kombinasyon-için-throwaway-trader) |
| 98 | `SingleTraderOptimizer::runSingleTrader(...)` | public method | [Run() — Optimizasyon Döngüsü](#run--optimizasyon-döngüsü) |
| 99 | `SingleTraderOptimizer::Run(...)` | public method | [Run() — Optimizasyon Döngüsü](#run--optimizasyon-döngüsü) |
| 100 | `SingleTraderOptimizer::GetBestResult()` | public method | [Run() — Optimizasyon Döngüsü](#run--optimizasyon-döngüsü) — bkz. Not, `SortField`'den bağımsız |
| 102-113 | `AppendSingleOptSummaryToFiles`…`WriteSortedTxt` (12 private/public dosya-yazma metodu) | method | [Dosyaya Yazma](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı) — iç yardımcılar, tek tek anlatılmıyor |
| 116 | `SingleTraderOptimizer::GenerateParameterCombinations()` | public method | [Parametre Kombinasyonları](#parametre-kombinasyonları) |
| 117 | `SingleTraderOptimizer::GenerateCombinationsRecursive(...)` | private method | [Parametre Kombinasyonları](#parametre-kombinasyonları) — iç yardımcı (recursive backtracking) |
| 120-127 | 8 `OnSingleTraderXxx` callback (hepsi no-op, `OnSingleTraderProgress` hariç) | private method | [Callback'ler](#callbackler-8-adet-hepsi-no-op-tek-istisna-onsingletraderprogress) |
| 129 | `SingleTraderOptimizer::EquityCurveFilterConfig` | public property | [`createSingleTrader()`](#createsingletrader--her-kombinasyon-için-throwaway-trader) |
| 130 | `SingleTraderOptimizer::SignalsConfig` | public property | [`createSingleTrader()`](#createsingletrader--her-kombinasyon-için-throwaway-trader) |
| 132 | `SingleTraderOptimizer::ApplyConfigsToTrader(trader)` | private method | [`createSingleTrader()`](#createsingletrader--her-kombinasyon-için-throwaway-trader) |
| 133 | `SingleTraderOptimizer::SetSingleTraderConfigureEquityCurveFilter(trader)` | private method | [`createSingleTrader()`](#createsingletrader--her-kombinasyon-için-throwaway-trader) |
| 136-143 | `SymbolName`…`QueryName` (8 kimlik property'si) | public property | [`createSingleTrader()`](#createsingletrader--her-kombinasyon-için-throwaway-trader) — her test trader'ına kopyalanır |
| 145-148 | `IlkBakiye`/`KontratSayisi`/`KomisyonCarpan`/`KaymaMiktari` | public property | [`createSingleTrader()`](#createsingletrader--her-kombinasyon-için-throwaway-trader) — `TradeParamsOverride` `null` ise fallback |
| 150 | `SingleTraderOptimizer::TradeParamsOverride` | public property | [`createSingleTrader()`](#createsingletrader--her-kombinasyon-için-throwaway-trader) |
| 153 | `SingleTraderOptimizer::Dispose()` | public method | [Kurulum ve Konfigürasyon](#kurulum-ve-konfigürasyon) |

## Public API

### Kurulum ve Konfigürasyon

- `SingleTraderOptimizer(id, data, indicators, logger)` — constructor: `Id`/`Data`/`Indicators`/
  `Logger`'ı atar, `ParameterRanges`/`Results`/`AllCombinations` listelerini yaratır,
  `IsInitialized = true`. **Parametresiz overload yok** — SingleTrader/MultipleTrader'ın aksine
  tek bir constructor var.
- `AddParameterRange(name, min, max, step)` → `ParameterRanges.Add(new ParameterRange(...))`.
- `SetStrategyFactory(factory)` — `factory` `null` ise `ArgumentNullException`.
- `Reset()` — state flag'lerini sıfırlar, `_initializedFiles.Clear()`, `_cachedOptResults = null`
  (bir sonraki `WriteSortedFiles()` çağrısı dosyayı yeniden okuyacak).
- `Init()` — **tamamen boş gövde**, hiçbir şey yapmıyor. `RunSingleTraderOptWithProgressAsync()`
  yine de çağırıyor (SingleTrader/MultipleTrader'daki `Init()` deseniyle simetri için, ama burada
  gerçek bir işlevi yok).
- `Dispose()` — `Results?.Clear()`, `ParameterRanges?.Clear()`, 4 event/delegate'i `null`'a çeker
  (`OnOptimizationProgress`/`OnSingleTraderProgressCallback`/`OnReadOptimizationResultsFile`/
  `OnSaveResults`). `_cachedOptResults`/`_pendingFlushResults`/`_initializedFiles`'a dokunmuyor.

### `OptimizationResult` — Sonuç Tipi

- `Parameters` (`Dictionary<string,string>`) — test edilen kombinasyon (örn.
  `{"Period":"20","StopLoss":"50"}`).
- `Values` (`Dictionary<string,string>`) — `singleTrader.statistics.GetOptimizationSummary()`'nin
  ürettiği TÜM istatistik anahtar-değer çiftleri (`Statistics.Statistics` sınıfından, bu
  dokümanın kapsamı dışında).
- 9 convenience getter (`NetProfit`/`WinRate`/`ProfitFactor`/`ProfitFactorNet`/`MaxDrawdown`/
  `ScoreFiyatNet`/`ScoreFiyat`/`ScorePuan`/`StrategyName`) — `Values`'tan `TryGetD(key)` ile
  `double.TryParse(..., NumberStyles.Any, CultureInfo.InvariantCulture, ...)` yapar, parse
  başarısızsa `0.0` döner (exception fırlatmaz).

### Parametre Kombinasyonları

- `AddParameterRange(name, min, max, step)` → `ParameterRanges` listesine ekler.
- `ParameterRange.GetValues()` → `List<double>` — `Min`'den `Max`'e `Step` adımlarla
  (`v <= Max + Step*0.001` — kayan nokta yuvarlama hatalarına karşı tolerans), her değer
  `Math.Round(v, 10)` ile yuvarlanır.
- `GenerateParameterCombinations()` → `List<Dictionary<string,object>>` — `ParameterRanges`
  boşsa boş liste; değilse `GenerateCombinationsRecursive(0, {}, results)` ile **kartezyen
  çarpım** üretir (recursive backtracking — her parametre için her değeri dener, N parametre ×
  M değer varsa M₁×M₂×...×Mₙ kombinasyon). Sonucu hem döner hem `AllCombinations`'a atar.

> **Not — kombinasyon patlaması için hiçbir sınır YOK:** `GenerateCombinationsRecursive` kaç
> kombinasyon üreteceğine dair bir üst sınır kontrolü yapmıyor — 5 parametre × 20 değer/parametre
> = 3.2M kombinasyon gibi bir tarama hiçbir uyarı vermeden `AllCombinations`'a yüklenir (bellekte).
> `Run()` her kombinasyon için ayrı bir `SingleTrader` yaratıp tüm bar'ları koşturduğu için,
> büyük parametre uzayları × büyük veri setleri kombinasyonu pratikte çok uzun sürebilir —
> dikkatli girilmesi gereken bir alan, kod tarafında bir koruma yok.

### `createSingleTrader()` — Her Kombinasyon İçin Throwaway Trader

```csharp linenums="1"
public SingleTrader createSingleTrader()
{
    var singleTrader = new SingleTrader(0, "singleTrader", this.Data, Indicators, Logger);
    if (singleTrader == null)
        throw new InvalidOperationException("singleTrader can not be created...");

    singleTrader.ClearCallbacks()
                .SetCallbacks(OnSingleTraderReset, OnSingleTraderInit, OnSingleTraderRun, OnSingleTraderFinal,
                              OnSingleTraderBeforeOrder, OnSingleTraderNotifySignal, OnSingleTraderAfterOrder, OnSingleTraderProgress);

    singleTrader.Reset();

    singleTrader.SymbolName             = this.SymbolName;
    singleTrader.SymbolPeriod           = this.SymbolPeriod;
    // ... SystemId/SystemName/StrategyId/StrategyName/QueryId/QueryName/LastExecutionTime* (Attributes'tan)

    // Configure position sizing — AppConfig.SingleTraderOptimizer.TradeParams
    if (TradeParamsOverride is not null)
        singleTrader.initialTradeParams!.ApplyFrom(TradeParamsOverride);
    else
        singleTrader.initialTradeParams!.Reset().SetBakiyeParams(ilkBakiye: IlkBakiye).SetKontratParamsViopEndex(kontratSayisi: KontratSayisi).SetKomisyonParams(komisyonCarpan: KomisyonCarpan).SetKaymaParams(kaymaMiktari: KaymaMiktari);

    ApplyConfigsToTrader(singleTrader);
    SetSingleTraderConfigureEquityCurveFilter(singleTrader);

    singleTrader.RunMode = TraderRunMode.TradeOnly;   // HER ZAMAN TradeOnly — query desteği yok

    singleTrader.Init();

    return singleTrader;
}
```

- `TradeParamsOverride` (`InitialTradeParams?`, `AppConfig.SingleTraderOptimizer.TradeParams`'ın
  TAMAMI, `MarketType` dahil) doluysa `ApplyFrom(...)` ile kopyalanır; `null` ise
  `IlkBakiye`/`KontratSayisi`/`KomisyonCarpan`/`KaymaMiktari` (4 basit alan) ile **her zaman
  `ViopEndex`** market tipinde bir fallback kurulur (`SetKontratParamsViopEndex`). Yani
  `TradeParamsOverride` set edilmezse, `MarketType` seçimi hiç uygulanmaz — her zaman ViopEndex.
- `ApplyConfigsToTrader(trader)` (`845-877`) — `trader.ConfigureUserFlagsOnce()` → **hardcoded**
  `trader.OptimizationEnabled = true; trader.SaveStatisticsToFile = false;` (her test trader için
  sabit — bkz. aşağıdaki Not) → `signals.EquityCurveFilteringEnabled = false` (asıl değer ECF
  config'den) → `SignalsConfig` (`AppConfig.SingleTraderOptimizer.Signals`) doluysa
  `AlEnabled`/`SatEnabled`/... + `StartDateTime`/`StopDateTime` uygulanır.
- `SetSingleTraderConfigureEquityCurveFilter(trader)` (`879-891`) — `EquityCurveFilterConfig`
  (`id=0` olan `EquityCurveFilterConfigEntry`) `null` ise no-op; doluysa
  `trader.ConfigureEquityCurveFilter(...)` çağrılır.
- `RunMode` her zaman `TraderRunMode.TradeOnly` — `TradeAndQuery`/`QueryOnly` desteklenmiyor,
  optimizasyon sırasında sorgu çalıştırılamaz.

> **Not — `createSingleTrader()`'ın en üstündeki TODO yorumu hâlâ açık bir soru:**
> `SingleTraderOptimizer.cs:205-207` — "`RunSingleTraderWithProgressAsync(...)` içindeki
> `singleTrader = new SingleTrader(..)` ile başlayan ve `singleTrader.Init();` ile biten kısımlar
> arasını birebir kontrol et. SingleTrader kısmı en son halini aldı, oradakilerin buraya map'i
> tamam mı?" — karşılaştırınca [SingleTrader'ın kendi orkestrasyon
> fonksiyonu](02-singletrader.md#runsingletraderwithprogressasync--tam-kaynak-algotradercs1252-1530)
> ile gerçek bir fark var: `createSingleTrader()` hiçbir zaman `SetStrategy`/`SetQuery` çağırmıyor
> (strateji `Run()` içinde, `createSingleTrader()`'dan SONRA, ayrı olarak atanıyor — bkz.
> [Run()](#run--optimizasyon-döngüsü)) ve `RunMode` her zaman `TradeOnly`'ye sabit — bu, kasıtlı
> bir tasarım farkı (optimizer query desteklemiyor), TODO'nun işaret ettiği "eksik map" değil.

### Callback'ler (8 adet, hepsi no-op, tek istisna `OnSingleTraderProgress`)

```csharp linenums="1"
private void OnSingleTraderReset(SingleTrader trader, int mode) { }
private void OnSingleTraderInit(SingleTrader trader, int mode) { }
private void OnSingleTraderRun(SingleTrader trader, int mode) { }
private void OnSingleTraderFinal(SingleTrader trader, int mode) { }
private void OnSingleTraderBeforeOrder(SingleTrader trader, int barIndex) { }
private void OnSingleTraderNotifySignal(SingleTrader trader, string signal, int barIndex) { }
private void OnSingleTraderAfterOrder(SingleTrader trader, int barIndex) { }
private void OnSingleTraderProgress(SingleTrader trader, int currentBar, int totalBars, double percentage)
{
    OnSingleTraderProgressCallback?.Invoke(trader, currentBar, totalBars, percentage);
}
```

- 7'si tamamen boş — [SingleTrader](02-singletrader.md#callbacklerin-gerçek-gövdeleri-algotradercs158-223)'daki
  Console callback'lerinin aksine, burada `AlgoTrader` seviyesinde EK bir dolaylama yok, doğrudan
  bu private metodlar boş.
- `OnSingleTraderProgress` tek istisna — `OnSingleTraderProgressCallback` (public delegate
  property) doluysa ona pass-through yapar. Bu, `AlgoTrader.OnOptimizationSingleTraderProgress`'e
  bağlanıyor (bkz. [Callback'lerin Gerçek Gövdeleri](#callbacklerin-gerçek-gövdeleri) altında) —
  ama o da tamamen yorum satırı, bkz. aşağıdaki Not.

### Run() — Optimizasyon Döngüsü

```csharp linenums="1"
public OptimizationResult? Run(CancellationToken cancellationToken = default)
{
    if (!IsInitialized) throw new InvalidOperationException("Optimizer not initialized");
    if (StrategyFactoryMethod == null) throw new InvalidOperationException("StrategyFactory must be set before running. Use SetStrategyFactory().");
    if (ParameterRanges.Count == 0) throw new InvalidOperationException("No parameter ranges defined. Use AddParameterRange().");
    if (AllCombinations == null || AllCombinations.Count == 0) throw new InvalidOperationException("No combinations generated. Call GenerateParameterCombinations() first.");

    IsStarted = true; IsRunning = true; IsStopped = false; IsStopRequested = false;

    int totalBars = Data.Count;
    Results.Clear();
    int totalCombinations = AllCombinations.Count;
    int currentCombination = 0;

    // PartialOpt: -1 → baştan/sona
    int effectiveFrom = OptimizationFrom == -1 ? 1 : OptimizationFrom;
    int effectiveTo   = OptimizationTo   == -1 ? totalCombinations : OptimizationTo;

    foreach (var paramCombo in AllCombinations)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsStopRequested) break;

        currentCombination++;
        if (currentCombination < effectiveFrom) continue;
        if (currentCombination > effectiveTo) break;

        OnOptimizationProgress?.Invoke(this, currentCombination, totalCombinations, /* percentage */ 0.0);

        var strategy = StrategyFactoryMethod!(this.Data, Indicators, paramCombo);
        SingleTrader singleTrader = createSingleTrader();
        singleTrader.SetStrategy(strategy);   // ← strateji burada, createSingleTrader() SONRASI atanıyor

        singleTrader.IsStarted = true; singleTrader.IsRunning = true;
        singleTrader.IsStopped = false; singleTrader.IsStopRequested = false;

        runSingleTrader(singleTrader, totalBars, cancellationToken);   // bar-bar Run(i) döngüsü
        singleTrader.Finalize();
        singleTrader.IsRunning = false; singleTrader.IsStopped = true;

        var optResultsMap = singleTrader.statistics.GetOptimizationSummary();
        var optResult = new OptimizationResult();
        foreach (var kvp in paramCombo)
            optResult.Parameters[kvp.Key] = Convert.ToString(kvp.Value, CultureInfo.InvariantCulture) ?? "";
        optResult.Values = new Dictionary<string, string>(optResultsMap);
        Results.Add(optResult);

        AppendSingleOptSummaryToFiles(optResult, currentCombination);   // her kombinasyon sonrası dosyaya yaz

        OnReadOptimizationResultsFile?.Invoke(this, singleTrader, currentCombination);

        // SaveEveryN kontrolü var ama gövdesi tamamen yorum satırı — bkz. Not

        strategy?.Dispose();
        singleTrader.Dispose();   // ← her kombinasyon sonunda trader'ı at, bir sonrakine taze başla
    }

    if (_pendingFlushResults.Count > 0) FlushPendingToFiles();

    IsRunning = false; IsStopped = true;
    return GetBestResult();
}
```

- `runSingleTrader(singleTrader, totalBars, cancellationToken)` (`253-270`) — `SingleTrader.Run()`'ı
  `AlgoTrader.RunSingleTraderWithProgressAsync()`'in bar döngüsünden FARKLI şekilde çağırır: her
  1000 barda bir (`i % 1000 == 0`) `OnSingleTraderProgressCallback`'i tetikler (SingleTrader'ın
  kendi `%5`'lik dilim mantığı burada kullanılmıyor), `IsStopRequested` her bar kontrol edilir.
- Her kombinasyon TAM BAĞIMSIZ: yeni `SingleTrader`, yeni `strategy` instance'ı, koşum bitince
  ikisi de `Dispose()` edilir — bellek birikmesin diye (büyük taramalarda kritik).
- `AppendSingleOptSummaryToFiles(...)` her kombinasyon sonrası çağrılır — `Results` listesi
  bellekte de tutulur AMA asıl kalıcı çıktı bu satırdaki dosya yazımı (bkz. [Dosyaya
  Yazma](#dosyaya-yazma-appendsingleoptsummarytofiles-ve-sıralı-çıktı)).

> **Not — `OnOptimizationProgress`/`OnSingleTraderProgressCallback` bağlandığı yerde tamamen
> yorum satırı, hiçbir ilerleme Console'a basılmıyor:** `AlgoTrader.OnOptimizationProgress`/
> `OnOptimizationSingleTraderProgress` (`AlgoTrader.cs:2711-2743`) — SingleTrader'ın kendi
> `OnSingleTraderProgress`'inin aksine (bkz. [SingleTrader §
> Callback'ler](02-singletrader.md#callbacklerin-gerçek-gövdeleri-algotradercs158-223), orada
> gövde DOLU) buradaki iki callback'in gövdesi **TAMAMEN yorum bloğu içinde** — `/* ... */` ile
> sarılı, tek bir aktif satır yok. Sonuç: optimizasyon sırasında ne kombinasyon ilerlemesi
> (`X/Y`) ne bar ilerlemesi Console'a basılıyor — kullanıcı optimizasyonun ilerleyişini SADECE
> `Run()`'ın `LogManager.LogRaw($"{progressLine}")` satırından (her kombinasyon sonunda,
> `GetOptimizationProgressLine(...)`'dan gelen özet satırı) takip edebiliyor, gerçek zamanlı bir
> "%" göstergesi yok.

> **Not — `SaveEveryN` tanımlı ama kontrolü işlevsiz:** `Run()` içinde
> `if (SaveEveryN > 0 && currentCombination % SaveEveryN == 0) { ... }` bloğunun içi
> (`OnSaveResults?.Invoke(...)`) **tamamen yorum satırı** (`SingleTraderOptimizer.cs:400-404`).
> `SaveEveryN`'i herhangi bir değere set etmenin hiçbir gözlemlenebilir etkisi yok — asıl
> "ara sonuç kaydetme" ihtiyacı zaten her kombinasyondan sonra çalışan
> `AppendSingleOptSummaryToFiles(...)` tarafından karşılanıyor, `SaveEveryN`/`OnSaveResults`
> muhtemelen ondan önceki bir tasarımdan kalma, artık gereksiz kod.

- `GetBestResult()` → `Results.OrderByDescending(r => r.NetProfit).FirstOrDefault()` —
  **SADECE `NetProfit`'e göre** sıralar. Bu, dosya çıktısındaki `SortField`'den (varsayılan
  `"GetiriFiyatNet"`, `AppConfig.SingleTraderOptimizer.Sort.SortField`) **farklı bir metrik**
  olabilir — yani Console'da loglanan "BEST RESULT" ile `singleTraderOptLog_sorted.csv`'nin EN
  ÜST satırı farklı kombinasyonları gösterebilir (bilinen bir tutarsızlık, zaten
  `docs/PROJECT_ANALYSIS.md`'de not edilmiş).

### PartialOpt: `OptimizationFrom` / `OptimizationTo`

- `-1` (varsayılan her ikisi de) → tüm kombinasyonlar (`effectiveFrom=1`,
  `effectiveTo=totalCombinations`).
- Kombinasyonlar **1-tabanlı** sırayla numaralandırılır (`currentCombination` her iterasyonda
  `++`); `[From-To]` aralığı DIŞINDAKİLER `continue`/`break` ile atlanır — ama `AllCombinations`
  listesinin TAMAMI yine de `foreach` ile taranır (aralık dışındakiler sadece iş yapılmadan
  atlanıyor, üretim/iterasyon maliyeti hâlâ var).
- Kesintiye uğrayan uzun taramaları parça parça devam ettirmek için: `[100-200]` çalıştır, sonra
  `[201-300]`, ... — `AppendEnabled=true` ile dosyalar sona eklenir, `false` ile her yeni `Run()`
  çağrısı dosyayı SIFIRLAR (bkz. aşağıda).

### Dosyaya Yazma: `AppendSingleOptSummaryToFiles` ve Sıralı Çıktı

- `AppendSingleOptSummaryToFiles(optResult, currentCombination)` — `FileFlushIntervalMs < 0`
  (varsayılan `-1`) ise **her kombinasyonda hemen** `WriteResultToFiles(...)` + sıralı dosyaları
  günceller; `>= 0` ise sonucu `_pendingFlushResults`'a ekler, `_flushStopwatch` o süreyi
  geçtiyse toplu flush eder (`FlushPendingToFiles()`) — büyük taramalarda disk I/O'yu azaltmak
  için bellekte biriktirme opsiyonu.
- `WriteResultToFiles` → `CsvFileLoggingEnabled`/`TxtFileLoggingEnabled`'a göre
  `AppendSingleOptSummaryCsvFromConfig`/`TxtFromConfig`'i çağırır — ikisi de `ConfigFilePath`
  (`StatisticsExporterConfig.json`) doluysa `StatisticsExporter.LoadOptimizationColumns(...)`'tan
  sütun tanımlarını (`Field`/`Header`/`Width`) okur, boşsa sütun listesi boş kalır (sadece
  `CombNo` + parametre kolonları yazılır).
- `AppendEnabled` davranışı: `false` ise bu `Run()` koşumunda dosyayı İLK yazımda `FileMode.Create`
  ile sıfırlar (`_initializedFiles` HashSet'i ile "bu run'da bu dosyaya daha önce yazdım mı"
  takip edilir), sonraki her yazım `Append`; `true` ise HER ZAMAN `Append` (dosya yoksa yeni
  oluşturur, header sadece dosya yoksa yazılır) — PartialOpt'ta parça parça devam etmek için
  `AppendEnabled=true` şart.
- **Sıralı çıktı** (`WriteSortedFiles()`) — `_cachedOptResults` `null` ise ÖNCE `LoadOptCsvToCache()`
  ile `CsvFilePath`'i baştan okur (CSV header'ından hangi sütunların parametre, hangilerinin
  config-column olduğunu `configHeaderSet` ile ayırt eder), sonra `SortField`'e göre
  `OrderByDescending` (parse hatasında `double.MinValue` — yani parse edilemeyen satırlar sona
  düşer) → `WriteSortedCsv`/`WriteSortedTxt` ile TAMAMEN yeniden yazar (`FileMode.Create`, append
  değil). `WriteSortedFilesIfEnabled()` her `AppendSingleOptSummaryToFiles` çağrısından sonra
  bunu tetikler — yani sıralı dosya HER kombinasyonda (veya flush aralığında) baştan yeniden
  üretilir, kombinasyon sayısı arttıkça bu adım giderek pahalılaşır (O(n log n) sıralama, her
  kombinasyonda tekrar).

### Event'ler

| Event/Delegate | İmza | Tetiklendiği yer |
|---|---|---|
| `OnOptimizationProgress` | `(SingleTraderOptimizer, int current, int total, double percentage)` | `Run()`, her kombinasyon başında — `AlgoTrader`'daki callback'i tamamen yorum satırı, bkz. Not |
| `OnSingleTraderProgressCallback` | `(SingleTrader, int currentBar, int totalBars, double percentage)` | `runSingleTrader(...)`, her 1000 barda — aynı şekilde etkisiz |
| `OnReadOptimizationResultsFile` | `(SingleTraderOptimizer, SingleTrader, int currentCombination)` | `Run()`, her kombinasyon sonunda, dosyaya yazımdan SONRA |
| `OnSaveResults` | `(List<OptimizationResult>, int currentCombination)` | Ölü kod — hiçbir yerden `Invoke` edilmiyor (bkz. `SaveEveryN` Notu) |

## Çağrı Zinciri — Menüden Çağrılma (Program.cs → AlgoTrader → SingleTraderOptimizer)

1. `handleSingleTraderOpt()` (`Program.cs:3225-`) — [SingleTrader'daki
   `handleSingleTrader()`](02-singletrader.md#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--singletrader)
   ile birebir aynı desen: `reloadAppConfig()` → `showModeConfigSummary("SingleTraderOptimizer")`
   → `[ENTER]/[E]/[R]/[B]` → `showSingleTraderOptRunPreview()` → `[ENTER]/[E]/[R]/[B]` →
   `runSingleTraderOptimization()`.
2. `runSingleTraderOptimization()` (`Program.cs:1016-1051`) — `stockDataReader`/`IsDataReady`
   kontrolü → `new AlgoTrader(...)` + logger/timer + `SetData(...)` + `SymbolName`/`SymbolPeriod`
   → **`AppConfigApplier.ApplySingleTraderOpt(algoTrader, appConfig.SingleTraderOptimizer,
   AppSettings.ConfigsDir)`** (bkz. [AppConfig
   Kaynağı](#appconfig-kaynağı--singletraderoptconfig)) → `Initialize()` → **`await
   algoTrader.RunSingleTraderOptWithProgressAsync()`** (tam kaynağı aşağıda). SingleTrader/
   MultipleTrader'ın aksine burada `WriteTraderDataToFilesAsync(...)` çağrısı YOK — optimizer
   kendi dosyalarını `Run()` sırasında zaten yazmış oluyor, PlotEnabled/Python plot dalı da yok.
3. `AlgoTrader.RunSingleTraderOptWithProgressAsync()` (`AlgoTrader.cs:2744-2934`) içinde gerçek
   `SingleTraderOptimizer` yaratılıp konfigüre ediliyor, tam kaynağı aşağıda.

## AppConfig Kaynağı — `SingleTraderOptConfig`

`AppConfig.json`'daki `"SingleTraderOptimizer"` bölümünü karşılayan C# sınıfları (`AppConfig.cs:473-`):

```csharp linenums="1"
public class SingleTraderOptConfig
{
    public OptRef                      Optimization      { get; set; } = new();   // OptimizationConfig.txt'den parametre range'leri
    public StrategyRef                 Strategy          { get; set; } = new();
    public SingleTraderOptRangeConfig  Range             { get; set; } = new();   // PartialOpt
    public TradeParamsConfig           TradeParams       { get; set; } = new();   // bkz. SingleTrader doc — TÜM kombinasyonlar aynı pozisyon büyüklüğünü paylaşır
    public EcfRef?                     EquityCurveFilter { get; set; }
    public TraderSignalsConfig         Signals           { get; set; } = new();
    public SingleTraderOptSaveConfig   Save              { get; set; } = new();
    public TraderExportConfig?         Export            { get; set; }
    public SingleTraderOptSortConfig   Sort              { get; set; } = new();
    public SingleTraderOptTraderConfig SingleTrader      { get; set; } = new();   // "Best trader" ayarları — bkz. Not, ölü kod
}

public class SingleTraderOptRangeConfig
{
    public int OptimizationFrom { get; set; } = -1;
    public int OptimizationTo   { get; set; } = -1;
}

public class SingleTraderOptSaveConfig
{
    public bool   CsvFileLoggingEnabled               { get; set; } = true;
    public bool   TxtFileLoggingEnabled               { get; set; } = true;
    public bool   StatisticsExporterConfigFileEnabled { get; set; } = true;
    public string CsvFileName                         { get; set; } = "singleTraderOptLog.csv";
    public string TxtFileName                         { get; set; } = "singleTraderOptLog.txt";
    public string StatisticsExporterConfigFile        { get; set; } = "StatisticsExporterConfig.json";
    public bool   AppendEnabled                       { get; set; } = true;
    public int    FileFlushIntervalMs                 { get; set; } = -1;
}

public class SingleTraderOptSortConfig
{
    public string SortField         { get; set; } = "GetiriFiyatNet";
    public string SortedCsvFileName { get; set; } = "singleTraderOptLog_sorted.csv";
    public string SortedTxtFileName { get; set; } = "singleTraderOptLog_sorted.txt";
}

/// <summary>Best trader (optimizasyon sonucu) için ayarlar.</summary>
public class SingleTraderOptTraderConfig
{
    public TraderPlotConfig         Plot         { get; set; } = new();
    public TraderOptimizationConfig Optimization { get; set; } = new();
    public TraderSaveConfig         Save         { get; set; } = new();
    public TraderExportConfig?      Export       { get; set; }
}
```

`AppConfig.json`'daki gerçek karşılığı (`inputs/configs/AppConfig/AppConfig.json:702-`, kısaltılmış
— `TradeParams`/`Signals` alt-nesneleri [SingleTrader § AppConfig
Kaynağı](02-singletrader.md#appconfig-kaynağı--singletraderconfig)'nda birebir aynı şema):

```json linenums="1"
"SingleTraderOptimizer": {
    "Optimization": { "ConfigFile": "OptimizationConfig.txt", "Name": "SimpleMostStrategy", "Version": "v1" },
    "Strategy": { "ConfigFile": "StrategyConfig.txt", "Name": "SimpleMostStrategy", "Version": "v1" },
    "Range": { "OptimizationFrom": -1, "OptimizationTo": -1 },
    "TradeParams": {
      "MarketType": "FxCrypto", "IlkBakiye": 100000.0, "KontratSayisi": 1,
      "LotSayisi": 0.01, "HisseSayisi": 1000.0, "KomisyonCarpan": 0.0,
      "KaymaMiktari": 0.0, "PyramidingEnabled": false
    },
    "EquityCurveFilter": { "ConfigFile": "EquityCurveFilterConfig.txt", "Name": "", "Version": "v1" },
    "Signals": { "AlEnabled": true, "SatEnabled": true, "...": "... (12 alan, SingleTrader ile aynı şema)" },
    "Save": {
      "CsvFileLoggingEnabled": true,
      "TxtFileLoggingEnabled": true,
      "StatisticsExporterConfigFileEnabled": true,
      "CsvFileName": "singleTraderOptLog.csv",
      "TxtFileName": "singleTraderOptLog.txt",
      "StatisticsExporterConfigFile": "StatisticsExporterConfig.json",
      "AppendEnabled": true,
      "FileFlushIntervalMs": -1
    },
    "Export": { "ExportEnabled": true, "ConfigFile": "StatisticsExporterConfig.json", "Version": "v1" },
    "Sort": {
      "SortField": "GetiriFiyatNet",
      "SortedCsvFileName": "singleTraderOptLog_sorted.csv",
      "SortedTxtFileName": "singleTraderOptLog_sorted.txt"
    }
}
```

- `Optimization` (`OptRef`) → `algoTrader.ConfigureOptimizationFromConfig(...)` →
  `OptimizationConfig.txt`'den `AddOptimizationParameterRange(...)` çağrılarını üretir (dosya
  formatı bu dokümanın kapsamı dışında, bkz. `OptimizationConfigLoader`).
- `Range.OptimizationFrom`/`To` → doğrudan `SingleTraderOptimizer.OptimizationFrom`/`OptimizationTo`
  (PartialOpt).
- `Sort.SortField` (varsayılan `"GetiriFiyatNet"`) → `singleTraderOptLog_sorted.*` dosyalarının
  sıralama kriteri — `GetBestResult()`'ın kullandığı `NetProfit` ile AYNI ALAN DEĞİL (bkz. yukarıdaki
  Not).

> **Not — `SingleTrader` alt-bölümü (`SingleTraderOptTraderConfig`: `Plot`/`Optimization`/`Save`/
> `Export`) uygulanıyor ama hiçbir "best trader" yeniden-koşumu tarafından okunmuyor:**
> `AppConfigApplier.ApplySingleTraderOpt()` (`AppConfigApplier.cs:922-973`) bu 4 alt-config'i
> `SetSingleTraderPlotConfig`/`SetSingleTraderOptimizationConfig`/`SetSingleTraderSaveConfig`/
> `SetSingleTraderExportConfig` ile `algoTrader`'a set ediyor — bunlar [SingleTrader
> menüsüyle](02-singletrader.md#appconfig-kaynağı--singletraderconfig) PAYLAŞILAN slotlar. Ama
> `SingleTraderOptimizer.Run()`'ın (yukarıda tam kaynağı var) hiçbir yerinde "en iyi kombinasyonu
> tekrar çalıştır, gerçek dosyalara yaz" gibi bir adım YOK — `RunSingleTraderOptWithProgressAsync()`'te
> `singleTraderOptimizer.GetBestResult()` sadece `Log(...)` ile 6 metriği ekrana basıyor
> (`AlgoTrader.cs:2895-2906`), bir `SingleTrader` yaratıp bu 4 config'i ona uygulamıyor. Ayrıca
> `SingleTraderOptimizer.createSingleTrader()`'ın kendisi de (her kombinasyon için) `ApplyConfigsToTrader`
> içinde `SaveStatisticsToFile = false`'ü HARDCODED set ediyor, `SingleTraderSaveConfig`'i hiç
> okumuyor. Sonuç: `AppConfig.json`'daki `SingleTraderOptimizer.SingleTrader.*` bölümünün TAMAMI
> — sınıf yorumunun "Best trader (optimizasyon sonucu) için ayarlar" açıklamasına rağmen —
> şu anki kod tabanında hiçbir gözlemlenebilir etkisi olmayan, muhtemelen planlanıp
> tamamlanmamış bir özellik.

## `RunSingleTraderOptWithProgressAsync()` — Tam Kaynak (`AlgoTrader.cs:2744-2934`)

```csharp linenums="1" hl_lines="20 45 46 68 70 71"
public async Task RunSingleTraderOptWithProgressAsync(CancellationToken cancellationToken = default)
{
    int totalBars = 0;

    if (!IsInitialized)
        throw new InvalidOperationException("AlgoTrader not initialized. Call Initialize() first.");

    try
    {
        _timer!.RestartTimer("0");
        totalBars = GetDataCount();
        Log($"AlgoTrader '{Name}' started. Total bars: {totalBars}");

        // Indicators
        if (indicators != null) { indicators.Dispose(); indicators = null; }
        indicators = new IndicatorManager(this.Data);
        if (indicators == null) throw new InvalidOperationException("indicators can not be created...");

        // SingleTraderOptimizer — cleanup + create
        if (singleTraderOptimizer != null) { singleTraderOptimizer.Dispose(); singleTraderOptimizer = null; }
        singleTraderOptimizer = new SingleTraderOptimizer(0, this.Data, indicators, _logger);

        // Progress callback (bkz. Not — ikisi de fiilen etkisiz)
        singleTraderOptimizer.OnOptimizationProgress += OnOptimizationProgress;
        singleTraderOptimizer.OnSingleTraderProgressCallback += OnOptimizationSingleTraderProgress;

        singleTraderOptimizer.Reset();

        // Optimization log file settings — AppConfig.SingleTraderOptimizer.Save
        if (_singleTraderOptLogConfig is { } lg)
        {
            singleTraderOptimizer.CsvFileLoggingEnabled  = lg.CsvFileLoggingEnabled;
            singleTraderOptimizer.CsvFilePath            = Path.Combine(AppSettings.OptLogsDir, lg.CsvFileName);
            singleTraderOptimizer.TxtFileLoggingEnabled  = lg.TxtFileLoggingEnabled;
            singleTraderOptimizer.TxtFilePath            = Path.Combine(AppSettings.OptLogsDir, lg.TxtFileName);
            singleTraderOptimizer.AppendEnabled          = lg.AppendEnabled;
            singleTraderOptimizer.FileFlushIntervalMs    = lg.FileFlushIntervalMs;
            singleTraderOptimizer.ConfigFilePath         = lg.StatisticsExporterConfigFileEnabled
                ? Path.Combine(AppSettings.ConfigsDir, lg.StatisticsExporterConfigFile)
                : string.Empty;
        }

        // Sorted output settings — AppConfig.SingleTraderOptimizer.Sort
        if (_singleTraderOptSortConfig is { } sr)
        {
            singleTraderOptimizer.SortField         = sr.SortField;
            singleTraderOptimizer.SortedCsvFilePath = Path.Combine(AppSettings.OptLogsDir, sr.SortedCsvFileName);
            singleTraderOptimizer.SortedTxtFilePath = Path.Combine(AppSettings.OptLogsDir, sr.SortedTxtFileName);
        }

        singleTraderOptimizer.SignalsConfig = _singleTraderOptSignalsConfig;

        // Parametre range'leri
        if (_optimizationParameterRanges.Count == 0)
            throw new InvalidOperationException("No optimization parameter ranges configured. Call AddOptimizationParameterRange() first.");
        foreach (var range in _optimizationParameterRanges)
            singleTraderOptimizer.AddParameterRange(range.Name, range.Min, range.Max, range.Step);

        singleTraderOptimizer.GenerateParameterCombinations();
        Log($"Total combinations: {singleTraderOptimizer.AllCombinations.Count}");

        // Strategy factory — stored config'den veya fallback (_currentStrategyName + registry)
        if (_optimizationStrategyFactory != null)
        {
            singleTraderOptimizer.SetStrategyFactory(_optimizationStrategyFactory);
        }
        else
        {
            var strategyName = _currentStrategyName;
            singleTraderOptimizer.SetStrategyFactory((data, ind, parameters) =>
                _strategyRegistry.CreateStrategy(data, ind, _logger, strategyName, parameters));
        }

        // PartialOpt
        if (_singleTraderOptRangeConfig is { } rng)
        {
            singleTraderOptimizer.OptimizationFrom = rng.OptimizationFrom;
            singleTraderOptimizer.OptimizationTo   = rng.OptimizationTo;
        }

        // Trade params (basit 4 alan + tam InitialTradeParams override)
        if (_singleTraderOptTradeParamsConfig is { } tp)
        {
            singleTraderOptimizer.IlkBakiye      = tp.IlkBakiye;
            singleTraderOptimizer.KontratSayisi  = tp.KontratSayisi;
            singleTraderOptimizer.KomisyonCarpan = tp.KomisyonCarpan;
            singleTraderOptimizer.KaymaMiktari   = tp.KaymaMiktari;
        }
        singleTraderOptimizer.TradeParamsOverride = _singleTraderTradeParamsConfig;

        // ECF (id=0)
        var ecfConfig = _equityCurveFilterConfigs.FirstOrDefault(c => c.Id == 0);
        singleTraderOptimizer.EquityCurveFilterConfig = ecfConfig;

        singleTraderOptimizer.Init();

        _timer!.RestartTimer("1");

        await Task.Run(() =>
        {
            singleTraderOptimizer.IsStarted = true;
            singleTraderOptimizer.IsRunning = true;
            singleTraderOptimizer.IsStopped = false;
            singleTraderOptimizer.IsStopRequested = false;

            singleTraderOptimizer.Run(cancellationToken);   // ← TÜM optimizasyon burada, senkron
        }, cancellationToken);

        _timer!.StopTimer("1");

        var bestResult = singleTraderOptimizer.GetBestResult();
        if (bestResult != null)
        {
            Log($"\n=== BEST RESULT ===");
            foreach (var kvp in bestResult.Parameters) Log($"  {kvp.Key}: {kvp.Value}");
            Log($"  NetProfit: {bestResult.NetProfit:F2}");
            Log($"  WinRate: {bestResult.WinRate:F2}%");
            Log($"  ProfitFactor: {bestResult.ProfitFactor:F2}");
            Log($"  ScoreFiyatNet: {bestResult.ScoreFiyatNet:F2}");
            Log($"  ScoreFiyat: {bestResult.ScoreFiyat:F2}");
            Log($"  ScorePuan: {bestResult.ScorePuan:F2}");
        }

        _timer!.StopTimer("0");
        // t0/t1 elapsed time logları...
    }
    catch (Exception ex)
    {
        Log($"An error occurred while running in RunSingleTraderOptWithProgressAsync(): {ex.Message}");
    }
    finally { }

    if (singleTraderOptimizer is not null)
    {
        singleTraderOptimizer.IsRunning = false;
        singleTraderOptimizer.IsStopped = true;
    }
}
```

- Diğer `RunXxxWithProgressAsync()`'lerin aksine (SingleTrader/MultipleTrader'da bar-bar döngü
  `await Task.Run(() => { for (...) ... })` içinde AlgoTrader seviyesinde), burada `Task.Run`'ın
  içindeki tek satır `singleTraderOptimizer.Run(cancellationToken)` — TÜM kombinasyon×bar döngüsü
  `SingleTraderOptimizer.Run()`'ın kendi içinde, tek senkron blokta çalışıyor.

## Callback'lerin Gerçek Gövdeleri

`OnOptimizationProgress`/`OnOptimizationSingleTraderProgress` (`AlgoTrader.cs:2711-2743`) —
**ikisi de tamamen yorum bloğu**, [yukarıdaki Not](#run--optimizasyon-döngüsü)'ta detaylı
açıklandı:

```csharp linenums="1"
private void OnOptimizationProgress(SingleTraderOptimizer singleTraderOptimizer, int current, int total, double percentage)
{
    /*if (_logger == null) return;
    var consoleLogger = LogManager.GetConsoleLogger();
    if (current >= total) { consoleLogger.Write($"\r\tProgress         : {current}/{total} ({percentage:F1}%)"); consoleLogger.WriteLine(""); }
    else { consoleLogger.Write($"\r\tProgress         : {current}/{total} ({percentage:F1}%)"); }*/
}

private void OnOptimizationSingleTraderProgress(SingleTrader trader, int currentBar, int totalBars, double percentage)
{
    /*if (_logger == null) return;
    ... (aynı desen, currentBar/totalBars için) ...*/
}
```

- SingleTrader'ın kendi `OnSingleTraderProgress`'i (bkz. [SingleTrader §
  Callback'ler](02-singletrader.md#callbacklerin-gerçek-gövdeleri-algotradercs158-223)) bu ikisiyle
  BİREBİR AYNI kod bloğunu içeriyor — tek fark SingleTrader'daki aktif (yorum dışı), buradaki
  ikisi de yorum içinde. Muhtemelen optimizasyon sırasında (binlerce kombinasyon × yüz binlerce
  bar) senkron Console yazımının performansı ciddi ölçüde yavaşlatması nedeniyle kapatılmış —
  SingleTrader'ın kendi progress callback'inde de benzer bir not var ("Cok yavasladigi icin
  kapatildi").

## Dönüş / Sonuç — Global State

| Değişken/Erişim | Tip | Kaynak |
|---|---|---|
| `algoTrader.SingleTraderOptimizer` | `SingleTraderOptimizer` (public getter, `private set`) | `RunSingleTraderOptWithProgressAsync()` içinde yaratılan `singleTraderOptimizer` |
| `algoTrader.SingleTraderOptimizer.Results` | `List<OptimizationResult>` | Her kombinasyonun sonucu — koşum bitince TAMAMI bellekte |
| `algoTrader.SingleTraderOptimizer.GetBestResult()` | `OptimizationResult?` | `NetProfit`'e göre en iyi (bkz. Not — `SortField`'den farklı olabilir) |
| `{CsvFileName}`/`{TxtFileName}` (`singleTraderOptLog.csv/.txt`) | dosya | Her kombinasyon sonrası `AppendSingleOptSummaryToFiles` |
| `{SortedCsvFileName}`/`{SortedTxtFileName}` (`singleTraderOptLog_sorted.csv/.txt`) | dosya | `SortField`'e göre sıralı, her yazımda TAMAMEN yeniden üretilir |

- `stockDataReader`/`stockDataList`/`stockMetaData` (bkz. [StockDataReader §
  Dönüş/Sonuç](09-stockdatareader.md#dönüş--sonuç--global-state)) bu akışın ÖN KOŞULU.
- SingleTrader/MultipleTrader'ın aksine **hiçbir "best trader" `SingleTrader` instance'ı
  saklanmıyor** — `Results`/`GetBestResult()` sadece `Dictionary<string,string>` tabanlı özet
  veri, gerçek bir trader nesnesi değil (her test trader'ı `Run()` sonunda `Dispose()` ediliyor).

## Tipik Kullanım — Script'ten Çağrılma

- **SingleTrader/MultipleTrader'daki "manuel kurulum" (Seviye B) deseni burada YOK** —
  `new SingleTraderOptimizer(...)` için tüm kod tabanında (`AlgoTrade.Core`, `AlgoTrade.Console`,
  `inputs/scripts/*.csx`) **tek bir instantiation noktası** var: `AlgoTrader.cs:2789`. Hiçbir
  script `SingleTraderOptimizer`'ı doğrudan kurmuyor.
- Script katmanının kullandığı tek yol **Seviye A** (`algoTrader.RunSingleTraderOptWithProgressAsync()`)
  — gerçek örnek: `inputs/scripts/03_RunSingleTraderOptWithProgressAsync.csx`
  (`#load "Config_03_SingleTraderOpt.csx"` ile ayrı bir konfig dosyasından parametre range'lerini/
  sabit parametrelerini okuyor).

**1) Veri oku + AlgoTrader'ı hazırla**

```csharp linenums="1"
stockDataReader = new StockDataReader();
stockDataReader.ReadMetaData(stockDataFullFileName);
stockDataReader.ReadDataFast(stockDataFullFileName);
var data = stockDataReader.GetData();

algoTrader.SetData(data);
algoTrader.RegisterLogger(LogManager.GetInstance());
algoTrader.RegisterTimer(TimeManager.GetInstance());
algoTrader.SymbolName = symbolName;
algoTrader.SymbolPeriod = symbolPeriod;
```

**2) Parametre range'leri + strateji factory (range param'ları + sabit param'ları birleştirir)**

```csharp linenums="1"
algoTrader.ClearOptimizationParameterRanges();
foreach (var range in optimizationRanges)
    algoTrader.AddOptimizationParameterRange(range.name, range.min, range.max, range.step);

algoTrader.SetOptimizationStrategyFactory((factoryData, ind, parameters) =>
{
    var merged = new Dictionary<string, object>(fixedParams, StringComparer.OrdinalIgnoreCase);
    foreach (var kvp in parameters)
        merged[kvp.Key] = kvp.Value;
    return algoTrader.CreateStrategyFromRegistry(factoryData, ind, optimizationStrategyName, merged);
});
```

**3) Trade params + PartialOpt aralığı**

```csharp linenums="1"
algoTrader.SetSingleTraderOptTradeParamsConfig(new SingleTraderOptTradeParamsConfig
{
    IlkBakiye      = ilkBakiye,
    KontratSayisi  = kontratSayisi,
    KomisyonCarpan = komisyonCarpan,
    KaymaMiktari   = kaymaMiktari,
});

algoTrader.SetSingleTraderOptRangeConfig(new SingleTraderOptRangeConfig
{
    OptimizationFrom = optimizationFrom,
    OptimizationTo   = optimizationTo,
});
```

**4) Initialize + Run**

```csharp linenums="1"
algoTrader.Initialize();
await algoTrader.RunSingleTraderOptWithProgressAsync();
```

**5) Sonuçları oku**

```csharp linenums="1"
var optimizer = algoTrader.SingleTraderOptimizer;
if (optimizer != null && optimizer.Results.Count > 0)
{
    var bestResult = optimizer.GetBestResult();
    if (bestResult != null)
    {
        foreach (var kvp in bestResult.Parameters)
            Log($"  {kvp.Key}: {kvp.Value}");
        Log($"  NetProfit      : {bestResult.NetProfit:F2}");
        Log($"  ProfitFactor   : {bestResult.ProfitFactor:F2}");
        Log($"  IslemSayisi    : {bestResult.Values.GetValueOrDefault("IslemSayisi", "N/A")}");
    }
}
```

- Script `algoTrader.SingleTraderOptimizer` (public getter) üzerinden `RunSingleTraderOptWithProgressAsync()`'in
  içeride yarattığı instance'a erişiyor — kendi `new SingleTraderOptimizer(...)`'ını YARATMIYOR.
  Bu, SingleTrader/MultipleTrader script'lerinin "Seviye B, `CustomConsensusFunc` gibi bir
  genişletme noktasına erişmek için manuel kurulum" ihtiyacının burada olmadığını gösteriyor —
  `SetOptimizationStrategyFactory`/`SetSingleTraderOptXxxConfig` metodları zaten script'ten
  Seviye A üzerinden erişilebilir genişletme noktaları.

## Console/JSON Eşleşmesi

Yukarıdaki script akışının Console karşılığı:

1. `inputs/configs/AppConfig/AppConfig.json` dosyasını aç.
2. `"SingleTraderOptimizer"` bölümünü düzenle (bkz. yukarıdaki [AppConfig
   Kaynağı](#appconfig-kaynağı--singletraderoptconfig) tam örnek): `Optimization.ConfigFile`
   (`OptimizationConfig.txt`) ile parametre range'lerini, `TradeParams` ile pozisyon büyüklüğünü,
   `Range` ile PartialOpt aralığını, `Sort.SortField` ile sıralı çıktının kriterini seç.
3. Kaydet, Console'u çalıştır, menüden `[4] SingleTraderOptimizer` (veya `[7]` "Read Data +
   SingleTraderOptimizer") seç.

Range'ler script'te `AddOptimizationParameterRange(...)` ile kod içinden verilirken, Console
akışında `Optimization.ConfigFile`'daki `OptimizationConfig.txt` dosyasından
(`OptimizationConfigLoader`, bu dokümanın kapsamı dışında) okunuyor — iki farklı giriş yolu,
aynı `_optimizationParameterRanges` listesine varıyor.

## Kimler Kullanıyor — Instantiation Noktaları

`new SingleTraderOptimizer(...)` için tüm kod tabanında grep taraması — **tek bir çağırım
noktası**:

| Dosya | Bağlam | Satır |
|---|---|---|
| `AlgoTrade.Core/Trading/AlgoTrader.cs` | `RunSingleTraderOptWithProgressAsync()` — `singleTraderOptimizer` (id=0) | 2789 |

- SingleTrader'ın 25, MultipleTrader'ın 4 instantiation noktasına kıyasla — hiçbir Scanner,
  hiçbir Confirming* sınıfı, hiçbir script `SingleTraderOptimizer`'ı kendi içinde kullanmıyor.
  Tamamen bağımsız, tek-amaçlı bir alt sistem.

## Kullanım Haritası

| Üye | Durum | Nerede |
|---|---|---|
| Constructor, `AddParameterRange`, `SetStrategyFactory`, `Reset`, `GenerateParameterCombinations`, `Run`, `GetBestResult`, `Dispose` | ✅ | `RunSingleTraderOptWithProgressAsync()` (yukarıda tam kaynağıyla var) |
| `createSingleTrader`, `runSingleTrader`, `ApplyConfigsToTrader`, `SetSingleTraderConfigureEquityCurveFilter` | ✅ | `Run()`'ın kendi içinden, her kombinasyon için |
| `CsvFileLoggingEnabled`/`TxtFileLoggingEnabled`/`AppendEnabled`/`FileFlushIntervalMs`/`SortField`/`SortedCsvFilePath`/`SortedTxtFilePath` | ✅ | `RunSingleTraderOptWithProgressAsync()` + `AppConfig.SingleTraderOptimizer.Save`/`Sort` |
| `OptimizationFrom`/`OptimizationTo` | ✅ | PartialOpt, `AppConfig.SingleTraderOptimizer.Range` |
| `TradeParamsOverride`, `IlkBakiye`/`KontratSayisi`/`KomisyonCarpan`/`KaymaMiktari` | ✅ | `createSingleTrader()` — override doluysa tercih edilir, değilse 4 alan fallback |
| `EquityCurveFilterConfig`, `SignalsConfig` | ✅ | `RunSingleTraderOptWithProgressAsync()`'ten atanır, `createSingleTrader()`'da okunur |
| `OnReadOptimizationResultsFile` | ✅ | `Run()`'ın her kombinasyon sonunda tetiklediği event — ama `AlgoTrader` tarafında hiçbir abone yok (bağlanmıyor bile) |
| `Init()` | ⚠️ | Çağrılıyor ama tamamen boş gövde |
| `OnOptimizationProgress`, `OnSingleTraderProgressCallback` | ❌ | Bağlanıyor ama `AlgoTrader`'daki gövdeleri tamamen yorum satırı |
| `SaveEveryN`, `OnSaveResults` | ❌ | Kontrol ediliyor ama gövdesi yorum satırı, hiçbir yerden set/invoke edilmiyor |
| `AppConfig.SingleTraderOptimizer.SingleTrader.*` (Plot/Optimization/Save/Export) | ❌ | Uygulanıyor (paylaşılan slotlara yazılıyor) ama hiçbir "best trader" yeniden-koşumu bunları okumuyor — bkz. [Not](#appconfig-kaynağı--singletraderoptconfig) |
| `SingleTrader.RunMode` (`TradeAndQuery`/`QueryOnly`) | ❌ | `createSingleTrader()` her zaman `TradeOnly` set ediyor, query desteği yok |

## İlgili Dosyalar

- [01-class-reference.md § 5. SingleTraderOptimizer](../01-class-reference.md#5-singletraderoptimizer--grid-search-optimizasyon) —
  bu sayfanın ait olduğu index, kısa özet.
- [02-singletrader.md](02-singletrader.md) — her kombinasyonun throwaway trader'ı, aynı derinlikte
  belgelenen kardeş sayfa.
- [03-multipletrader.md](03-multipletrader.md) — farklı stratejileri (parametre değil) karşılaştırma
  için alternatif yol.
- [06-class-doc-method.md](../06-class-doc-method.md) — bu sayfanın yazıldığı yöntem.
- [02-console-menu-guide.md](../02-console-menu-guide.md) — Console menü rehberi, `[4]`/`[7]`
  satırları.
- `docs/PROJECT_ANALYSIS.md` — `GetBestResult()` vs `SortField` tutarsızlığının ilk kaynağı.
