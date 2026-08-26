# PythonPlotter — pythonnet Tabanlı Görselleştirme (Eski/Varsayılan Plotter)

> [Class Reference](../01-class-reference.md) setinin bir parçası — `01-class-reference.md`'nin
> §1-§9 numaralı sınıflarına dahil DEĞİL (ayrı bir alt sistem, "SDK" değil "görselleştirme
> altyapısı"), ama [SingleTrader](02-singletrader.md)/[MultipleTrader](03-multipletrader.md) gibi
> aynı derinlikte işlendi. Yöntem: [06-class-doc-method.md](../06-class-doc-method.md). Kardeş
> sayfa: [DearPyGuiDataPlotter](dearpyguidataplotter.md) (daha yeni, geliştirilmekte olan
> alternatif plotter).

### Dosyalar

- `src/AlgoTrade.Core/Python/PythonPlotter.cs` (692 satır)
- `src/PythonPlotter/main.py` — `hello()`/`print_data_info(trade_data)`/`print_multiple_trader_data(trader_list)`
  giriş noktaları (isimler yanıltıcı — ikisi de gerçekten PLOT açıyor, sadece isimlendirme eski)
- `src/PythonPlotter/data_plotter.py` (`DataPlotter.plot()`), `src/PythonPlotter/multiple_data_plotter.py`
  (`MultipleDataPlotter.plot()`), `src/PythonPlotter/trade_data.py` (`TradeData` — C#'tan doldurulan
  veri sözleşmesi) — Python tarafı, bu dokümanın kapsamı dışında.

### Rolü

- [pythonnet](https://github.com/pythonnet/pythonnet) üzerinden, aynı process içinde gömülü bir
  CPython yorumlayıcısı başlatıp (`PythonEngine.Initialize()`), `AlgoTrader.SingleTrader`/
  `MultipleTrader` koşum sonuçlarını Python'a (JSON değil, doğrudan `PyList`/`PyDict`
  nesnelerine dönüştürerek) aktarır ve orada matplotlib/imgui_bundle tabanlı bir plot penceresi
  açtırır.
- `AlgoTrader`'ın **varsayılan/eski** plot yolu — proje içinde [DearPyGuiDataPlotter](dearpyguidataplotter.md)
  adında daha yeni, DearPyGui tabanlı bir alternatif de geliştiriliyor (ayrı süreç, npz bundle
  dosyası üzerinden iletişim); ikisi de `SingleTrader`'dan aynı veriyi okuyor ama tamamen farklı
  mekanizmalarla.
- `AlgoTrader` bu sınıfı doğrudan kullanmaz — kendi `_pythonPlotter` (private field) üzerinden
  sarmalar: `SetupPython()`/`PlotSingleTraderData(trader)`/`PlotMultipleTraderData(multipleTrader)`
  (bkz. [Çağrı Zinciri](#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--pythonplotter)).

### Ne zaman kullanılır

- `AppConfig.json`'da ilgili trader'ın `Plot.PlotEnabled=true` olduğunda — Console `[2]`/`[3]`/
  `[5]`/`[6]` ve ConfirmingSingleTrader/ConfirmingMultipleTrader menüleri (`[22]`-`[25]`) koşum
  bitince otomatik tetikler.
- `RunMode == TraderRunMode.QueryOnly` ise HİÇ tetiklenmez (`runSingleTraderAlgoTrade()`'deki
  `if (RunMode != QueryOnly && plotEnabled)` guard'ı — bkz. [SingleTrader §
  Çağrı Zinciri](02-singletrader.md#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--singletrader)).

### Sınıf İskeleti (ilk bakış)

Aşağıdaki bloktaki metod gövdeleri kaldırılmış — sadece alan/property/metod imzaları (public +
private, hepsi), gerçek kaynağın (`PythonPlotter.cs`) sırasıyla birebir aynı.

```csharp linenums="1"
public class PythonPlotter : IDisposable
{
    public bool IsInitialized { get; private set; }
    public string PythonDll { get; set; } = "";
    public string PythonScriptsDir { get; set; } = AppSettings.PythonScriptsDir;

    private static bool _engineStarted = false;
    private static readonly object _engineLock = new();

    private bool _disposed;
    private LogManager? _logger;
    private IndicatorManager? _indicators;

    // ---- Data Fields (trader'dan çıkarılan diziler, iki Plot çağrısı arasında paylaşılan geçici state) ----
    private List<DateTime> _dateTimes = new();
    private List<DateTime> _dates = new();
    private List<TimeSpan> _times = new();
    private List<double> _opens = new();
    private List<double> _highs = new();
    private List<double> _lows = new();
    private List<double> _closes = new();
    private List<long> _volumes = new();
    private List<long> _lots = new();
    private List<double> _sinyalList = new();
    private List<double> _karZararFiyatList = new();
    private List<double> _bakiyeFiyatList = new();
    private List<double> _getiriFiyatList = new();
    private List<double> _komisyonFiyatList = new();
    private List<double> _bakiyeFiyatNetList = new();
    private List<double> _getiriFiyatNetList = new();
    private List<double> _karZararFiyatYuzdeList = new();
    private List<double> _getiriFiyatYuzdeList = new();
    private List<double> _getiriFiyatNetYuzdeList = new();
    private Dictionary<string, double[]>? _strategyIndicators;
    private string _title = "AlgoTrade";
    private string _periyot = "1H";

    // ---- Kurulum ----
    public PythonPlotter();
    public PythonPlotter(string pythonDll);
    public void SetLogger(LogManager? logger);
    public void SetIndicators(IndicatorManager? ind);

    // ---- Initialization ----
    public void Initialize();
    public static void Shutdown();

    // ---- Plot Methods ----
    public void RunHello();
    public void PlotOptimizationResults(List<OptimizationResult> results);
    public void PlotSingleTraderData(SingleTrader trader);
    public void PlotMultipleTraderData(MultipleTrader multipleTrader);

    private void ExtractTraderData(SingleTrader trader, Lists lists);
    private dynamic BuildPyTradeData();
    private bool CallPlotDataImgBundleNew(dynamic tradeData);
    private void CallPlotMultipleTraderData(PyList traderList);

    // ---- Private ----
    private static void SetPyIndicators(dynamic tradeData, Dictionary<string, double[]?> indicators);
    private void EnsureInitialized();
    private string? FindPythonDll();

    // ---- IDisposable ----
    protected virtual void Dispose(bool disposing);
    public void Dispose();
}
```

### Üye İndeksi — Hangisi Nerede Anlatılıyor

| # | Üye | Tür | Detay |
|---|---|---|---|
| 3 | `PythonPlotter::IsInitialized` | public property | [Initialize()](#initialize--python-engine-başlatma) |
| 4 | `PythonPlotter::PythonDll` | public property | [Initialize()](#initialize--python-engine-başlatma) |
| 5 | `PythonPlotter::PythonScriptsDir` | public property | [Initialize()](#initialize--python-engine-başlatma) |
| 7 | `PythonPlotter::_engineStarted` | private static field | [Initialize()](#initialize--python-engine-başlatma) — process başına tek seferlik |
| 8 | `PythonPlotter::_engineLock` | private static field | [Initialize()](#initialize--python-engine-başlatma) |
| 10 | `PythonPlotter::_disposed` | private field | [Dispose](#kurulum) |
| 11 | `PythonPlotter::_logger` | private field | [Kurulum](#kurulum) |
| 12 | `PythonPlotter::_indicators` | private field | [Kurulum](#kurulum) |
| 15-33 | 19 private veri listesi (`_dateTimes`…`_getiriFiyatNetYuzdeList`) | private field | [`ExtractTraderData`/`BuildPyTradeData`](#plotsingletraderdata--plotmultipletraderdata--veri-aktarım-akışı) — iç yardımcı state, tek tek anlatılmıyor |
| 34 | `PythonPlotter::_strategyIndicators` | private field | [Veri Aktarım Akışı](#plotsingletraderdata--plotmultipletraderdata--veri-aktarım-akışı) |
| 35 | `PythonPlotter::_title` | private field | [Veri Aktarım Akışı](#plotsingletraderdata--plotmultipletraderdata--veri-aktarım-akışı) |
| 36 | `PythonPlotter::_periyot` | private field | [Veri Aktarım Akışı](#plotsingletraderdata--plotmultipletraderdata--veri-aktarım-akışı) |
| 39 | `PythonPlotter::PythonPlotter()` | constructor (parametresiz) | [Kurulum](#kurulum) |
| 40 | `PythonPlotter::PythonPlotter(pythonDll)` | constructor | [Kurulum](#kurulum) — bkz. Not, hiç kullanılmıyor |
| 41 | `PythonPlotter::SetLogger(logger)` | public method | [Kurulum](#kurulum) |
| 42 | `PythonPlotter::SetIndicators(ind)` | public method | [Kurulum](#kurulum) |
| 45 | `PythonPlotter::Initialize()` | public method | [Initialize()](#initialize--python-engine-başlatma) |
| 46 | `PythonPlotter::Shutdown()` | public static method | [Initialize()](#initialize--python-engine-başlatma) — bkz. Not, hiçbir yerden çağrılmıyor |
| 49 | `PythonPlotter::RunHello()` | public method | [Initialize()](#initialize--python-engine-başlatma) — `SetupPython()`'ın doğrulama adımı |
| 50 | `PythonPlotter::PlotOptimizationResults(results)` | public method | [`PlotOptimizationResults`](#plotoptimizationresults--bkz-not-hiçbir-yerden-çağrılmıyor) — bkz. Not, hiçbir yerden çağrılmıyor |
| 51 | `PythonPlotter::PlotSingleTraderData(trader)` | public method | [Veri Aktarım Akışı](#plotsingletraderdata--plotmultipletraderdata--veri-aktarım-akışı) |
| 52 | `PythonPlotter::PlotMultipleTraderData(multipleTrader)` | public method | [Veri Aktarım Akışı](#plotsingletraderdata--plotmultipletraderdata--veri-aktarım-akışı) |
| 54 | `PythonPlotter::ExtractTraderData(trader, lists)` | private method | [Veri Aktarım Akışı](#plotsingletraderdata--plotmultipletraderdata--veri-aktarım-akışı) — iç yardımcı |
| 55 | `PythonPlotter::BuildPyTradeData()` | private method | [Veri Aktarım Akışı](#plotsingletraderdata--plotmultipletraderdata--veri-aktarım-akışı) — iç yardımcı |
| 56 | `PythonPlotter::CallPlotDataImgBundleNew(tradeData)` | private method | [Veri Aktarım Akışı](#plotsingletraderdata--plotmultipletraderdata--veri-aktarım-akışı) — bkz. Not, isim yanıltıcı |
| 57 | `PythonPlotter::CallPlotMultipleTraderData(traderList)` | private method | [Veri Aktarım Akışı](#plotsingletraderdata--plotmultipletraderdata--veri-aktarım-akışı) — iç yardımcı |
| 60 | `PythonPlotter::SetPyIndicators(tradeData, indicators)` | private static method | [Veri Aktarım Akışı](#plotsingletraderdata--plotmultipletraderdata--veri-aktarım-akışı) — iç yardımcı |
| 61 | `PythonPlotter::EnsureInitialized()` | private method | Tüm public Plot metodlarının başında guard |
| 62 | `PythonPlotter::FindPythonDll()` | private method | [Initialize()](#initialize--python-engine-başlatma) |
| 65 | `PythonPlotter::Dispose(disposing)` | protected virtual method | [Kurulum](#kurulum) — bkz. Not, `PythonEngine.Shutdown()` çağırmıyor |
| 66 | `PythonPlotter::Dispose()` | public method | [Kurulum](#kurulum) |

## Public API

### Kurulum

- `PythonPlotter()` — parametresiz constructor, hiçbir şey yapmaz.
- `PythonPlotter(pythonDll)` — `PythonDll`'i set eder. **Hiçbir yerde kullanılmıyor** — `AlgoTrader.SetupPython()`
  her zaman parametresiz `new PythonPlotter()` yaratıp `PythonDll`'i AYRI bir satırda set ediyor
  (bkz. [Çağrı Zinciri](#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--pythonplotter)).
- `SetLogger(logger)`/`SetIndicators(ind)` — basit setter'lar, `_indicators` sadece
  `PlotSingleTraderData`/`PlotMultipleTraderData` içinde MA(5/8/13/.../200) hesaplamak için
  kullanılıyor (trader'ın kendi stratejisinden bağımsız, ek MA'lar).
- `Dispose()` → `Dispose(disposing: true)` + `GC.SuppressFinalize(this)`. `Dispose(bool)`'un
  gövdesi sadece `_disposed` bayrağını set ediyor — **`PythonEngine.Shutdown()`'ı KASITLI OLARAK
  çağırmıyor** (kod içi yorum: "PythonEngine.Shutdown() global/static state olduğu için Dispose
  içinde çağrılmaz. Uygulama sonunda explicit olarak `PythonPlotter.Shutdown()` çağrılmalı").

> **Not — `PythonPlotter.Shutdown()` (static) hiçbir yerden çağrılmıyor:** Yukarıdaki yorum
> "uygulama sonunda explicit olarak çağrılmalı" diyor, ama `AlgoTrade.Console`/`AlgoTrade.WinForms`
> genelinde grep taramasında `PythonPlotter.Shutdown()`'a tek bir çağrı yok. Pratik etkisi: process
> sonlanana kadar Python engine açık kalıyor (muhtemelen zararsız — process zaten kapanınca OS
> temizler), ama belgelenen "temizce kapat" akışı fiilen hiç tetiklenmiyor.

### `Initialize()` — Python Engine Başlatma

- `IsInitialized` `true` ise fast-path `return` (aynı instance'ta tekrar çağrı no-op).
- `_engineStarted` (static, process geneli) `true` ise sadece `IsInitialized=true` set edip döner
  — yani **tek process içinde `PythonEngine.Initialize()` sadece BİR KEZ** çalışır, farklı
  `PythonPlotter` instance'ları (örn. `SetupPython()`'ın "disposing previous" + yeni instance
  yaratma deseni) aynı alttaki engine'i paylaşır.
- DLL çözümü: önce `PythonDll` property'si (elle set edilmişse), yoksa `FindPythonDll()` →
  `AppSettings.ResolvePythonDll()` (proje kökündeki ortak `.venv`'in `pyvenv.cfg`'sinden — hangi
  Python sürümüyle kurulduysa otomatik onu bulur, sistem geneli kurulumlara BİLEREK bakmıyor —
  ABI uyuşmazlığı riskinden kaçınmak için, kod içi yorumda detaylı gerekçelendirilmiş).
- `Py.GIL()` içinde: `sys.path`'e `PythonScriptsDir` (`src/PythonPlotter/`) + venv `site-packages`
  eklenir; `imgui_bundle` klasörü varsa `os.add_dll_directory(...)` ile native DLL arama yoluna
  eklenir (imgui_bundle'ın kendi native bağımlılıkları için).
- `RunHello()` — `main.hello()`'yu çağırır (`SetupPython(runHello: true)`'nun varsayılan
  doğrulama adımı — Python engine'in gerçekten çalıştığını Console'a `"Hello Python"` basarak
  kanıtlar).

### `PlotSingleTraderData` / `PlotMultipleTraderData` — Veri Aktarım Akışı

1. `EnsureInitialized()` guard — `IsInitialized=false` ise `InvalidOperationException`.
2. `ExtractTraderData(trader, lists)` — trader'ın `Data`/`lists`'inden 19 diziyi (`_dateTimes`…
   `_getiriFiyatNetYuzdeList`, bkz. iskelet satır 15-33) + `_strategyIndicators`
   (`trader.Strategy?.GetPlotIndicators()`, stratejinin kendi çizilecek indikatörleri) +
   `_title`/`_periyot` (`SymbolName`/`SymbolPeriod`) alanlarını instance field'larına kopyalar.
3. `BuildPyTradeData()` — `Py.GIL()` altında, her diziyi `PyList`/`PyFloat`/`PyString`'e çevirip
   `src/PythonPlotter/trade_data.py`'daki `TradeData()` nesnesinin (`td.date_times`, `td.opens`, ...
   19+ alan) property'lerine atar. `td.indicators` her zaman BOŞ `PyDict()` — asıl
   `IndicatorManager.GetCachedIndicators()`'tan gelen indikatörler **performans sorunu nedeniyle
   yorum satırı** (kod içi not: "Performans sorunu yasatır gibi duruyor, o yuzden commentledim").
4. `SingleTrader` için: `SetPyIndicators(tradeData, {ma5..ma200})` — kapanış fiyatından 8 SMA
   (5/8/13/21/34/50/100/200) hesaplanıp `tradeData.indicators`'a eklenir (Fibonacci benzeri
   periyotlar) → `CallPlotDataImgBundleNew(tradeData)`.
5. `MultipleTrader` için: mainTrader + her child için AYRI AYRI `ExtractTraderData`/
   `BuildPyTradeData` çağrılır (4 SMA: 5/20/50/200, SingleTrader'dakinden farklı periyot seti),
   hepsi bir `PyList` (`pyTraderList`) içinde toplanır → `CallPlotMultipleTraderData(pyTraderList)`.

> **Not — `CallPlotDataImgBundleNew` ismi yanıltıcı, gerçek çağrı `main.print_data_info(...)`'ya
> gidiyor:** Metod adı "ImgBundleNew" (yeni bir imgui_bundle tabanlı plot yolu ima ediyor), ama
> gövdesindeki asıl plot satırları (`dynamic plotModule = Py.Import("plotDataImgBundleNew");
> plotModule.plot_data_img_bundle_new(tradeData);`) **yorum satırı** (`PythonPlotter.cs:543-546`).
> Fiilen çalışan satır `mainModule.print_data_info(tradeData)` — isim "print" olsa da, Python
> tarafında (`src/PythonPlotter/main.py:4-6`) bu fonksiyon `DataPlotter(trade_data).plot()`'u
> çağırıyor, yani GERÇEKTEN bir plot penceresi açıyor — sadece C# tarafındaki metod/import
> isimleri (`CallPlotDataImgBundleNew`, `plotDataImgBundleNew` modülü) artık kullanılmayan,
> muhtemelen denenip vazgeçilmiş bir ALTERNATIF plot yoluna ait kalıntılar. `imgui_bundle`'ın
> kendisi de bu fonksiyonda sadece `Py.Import("imgui_bundle")` ile "yüklü mü" diye test ediliyor
> (import başarısızsa açıklayıcı bir hata fırlatılıyor), asıl çizim onun ÜZERİNDEN değil.

### `PlotOptimizationResults` — bkz. Not, hiçbir yerden çağrılmıyor

```csharp linenums="1"
public void PlotOptimizationResults(List<OptimizationResult> results)
{
    EnsureInitialized();
    var payload = results.Select(r => new { parameters = r.Parameters, values = r.Values, net_profit = r.NetProfit, /* ... */ });
    string jsonStr = JsonConvert.SerializeObject(payload);
    using var gil = Py.GIL();
    dynamic pyData = Py.Import("json").loads(jsonStr);
    dynamic plotter = Py.Import("plotter");
    plotter.show_optimization_results(pyData);
}
```

> **Not — `PlotOptimizationResults` [SingleTraderOptimizer](05-singletraderoptimizer.md) ile hiç
> bağlanmamış:** Bu metod, [SingleTraderOptimizer](05-singletraderoptimizer.md)'ın ürettiği
> `List<OptimizationResult>` sonuçlarını Python'a aktarıp görselleştirmek için tasarlanmış
> (imza bunu açıkça gösteriyor), ve tek diğer public Plot metodlarından FARKLI olarak JSON
> serialize/deserialize yoluyla veri aktarıyor (diğerleri doğrudan `PyList`/`PyFloat`). Ama
> `AlgoTrader.RunSingleTraderOptWithProgressAsync()`'in (bkz. [SingleTraderOptimizer § Tam
> Kaynak](05-singletraderoptimizer.md#runsingletraderoptwithprogressasync--tam-kaynak-algotradercs2744-2934))
> hiçbir yerinde `SetupPython()`/`PlotOptimizationResults(...)` çağrısı yok — optimizasyon
> koşumu bittiğinde sonuçlar sadece Console'a loglanıyor ve dosyaya yazılıyor, hiçbir zaman
> Python'a görselleştirme için gönderilmiyor. `src/PythonPlotter/plotter.py`'deki
> `show_optimization_results(data)` fonksiyonu da muhtemelen yazılmış ama entegre edilmemiş.

## Çağrı Zinciri — Menüden Çağrılma (Program.cs → AlgoTrader → PythonPlotter)

1. Her trader menüsünün (`runSingleTraderAlgoTrade()`, `runMultipleTraderAlgoTrade()`,
   `runConfirmingSingleTraderAlgoTrade()`, `runConfirmingMultipleTraderAlgoTrade()`) koşum
   bittikten SONRA, `RunMode != QueryOnly && trader.PlotEnabled` ise:
   ```csharp linenums="1"
   if (algoTrader.SetupPython())
       await algoTrader.PlotSingleTraderData(algoTrader.SingleTrader);   // veya PlotMultipleTraderData(...)
   else
       LogManager.LogError("Python setup failed. PlotSingleTraderData skipped.");
   ```
   4 çağırım noktası var (`Program.cs:798`, `877`, `940`, `1001`) — dördü de aynı desen,
   Confirming* olanlar bile `PlotSingleTraderData(mainTrader)` çağırıyor (Confirming'e özel bir
   Plot metodu yok, sadece mainTrader'ı normal `SingleTrader` gibi çiziyor).
2. `AlgoTrader.SetupPython(runHello=true)` (`AlgoTrader.cs:2956-3018`) — tam kaynağı yukarıda
   [Initialize()](#initialize--python-engine-başlatma) bölümünde anlatıldı; `_pythonPlotter`
   zaten `IsInitialized` ise no-op (`true` döner), değilse yeniden yaratıp `Initialize()` çağırır.
3. `AlgoTrader.PlotSingleTraderData(singleTrader)`/`PlotMultipleTraderData(multipleTrader)`
   (`AlgoTrader.cs:3028-3049`) — `null` kontrolü + `_pythonPlotter`/`IsInitialized` kontrolü,
   sonra `await Task.Run(() => _pythonPlotter.PlotSingleTraderData(singleTrader))` — GERÇEK plot
   çağrısı ayrı bir thread'de (`Task.Run`), ama içeride `Py.GIL()` ile senkronize ediliyor; UI
   thread'i bloklamasın diye.

## Dönüş / Sonuç — Global State

`PythonPlotter`'ın kendisi kalıcı bir dosya/state üretmez — çıktısı EKRANA açılan bir plot
penceresidir (`DataPlotter(...).plot()`/`MultipleDataPlotter(...).plot()`, Python tarafında).

| Değişken/Erişim | Tip | Kaynak |
|---|---|---|
| `algoTrader` içindeki `_pythonPlotter` (private field, dışarıya açılmıyor) | `PythonPlotter?` | `SetupPython()` içinde yaratılır |
| Plot penceresi | ekran (Python `matplotlib`/benzeri) | `PlotSingleTraderData`/`PlotMultipleTraderData` — `await` edilen çağrı pencere kapanana kadar bloklar |
| Python stdout yakalama | `_logger?.WriteRaw(...)` | `CallPlotDataImgBundleNew`/`CallPlotMultipleTraderData` — Python'un `print(...)` çıktısı `sys.stdout` geçici olarak `io.StringIO()`'ya yönlendirilip Console'a aktarılıyor |

## Tipik Kullanım — Script'ten Çağrılma

**Hiçbir `.csx` script'i `PythonPlotter`/`SetupPython`/`PlotSingleTraderData`/`PlotMultipleTraderData`
kullanmıyor** — `inputs/scripts/*.csx` genelinde grep taraması sıfır sonuç veriyor. Script'ten tam
kontrol istendiğinde bile ([SingleTrader § Script'ten
Çağrılma](02-singletrader.md#tipik-kullanım--scriptten-çağrılma-manuel-kurulum),
[MultipleTrader'ın `CustomConsensusExample.csx`](03-multipletrader.md#tipik-kullanım--scriptten-çağrılma-customconsensusfunc-örneği)
örneklerinde) koşum sonunda sadece `WriteStatisticsToFile(...)`/`TaramaOzeti` loglanıyor, plot
tetiklenmiyor.

Script'ten tetiklemek istersen — mevcut örneklerin desenine uyarak (kavramsal, gerçek bir
script'te doğrulanmamış):

```csharp linenums="1"
if (algoTrader.SetupPython())
    await algoTrader.PlotSingleTraderData(singleTrader);   // singleTrader.Finalize() sonrası
else
    Log("Python setup failed.");
```

- `algoTrader.SetData(data)` + `Initialize()` daha önce çağrılmış olmalı (`SetupPython()` `indicators`'ı
  `_pythonPlotter.SetIndicators(indicators)` ile geçiyor — `algoTrader.indicators` `null` ise
  MA hesaplamaları da `null` kalır, `SetPyIndicators` boş/null değerleri sessizce atlar).
- `PlotOptimizationResults(...)` script'ten de dene(n)memiş — bkz. yukarıdaki Not, entegre
  edilmemiş bir özellik.

## Console/JSON Eşleşmesi

Kod yazmaya gerek yok — Console akışında plot tetiklenmesi tamamen `AppConfig.json`'daki
ilgili trader'ın `Plot.PlotEnabled` bayrağına bağlı (ayrı bir `"PythonPlotter"` JSON bölümü
YOK — `PythonPlotter`'ın kendi ayarlanabilir bir config'i yok, `PythonDll`/`PythonScriptsDir`
bile AppConfig'ten değil, `AppSettings`/ortam değişkeninden geliyor):

```json linenums="1"
"SingleTrader": {
    "Plot": { "PlotEnabled": true }
}
```

`true` → koşum bitince otomatik `SetupPython()` + `PlotSingleTraderData(...)` çağrılır; `false` →
Python engine'e hiç dokunulmaz. `MultipleTrader`/`ConfirmingSingleTrader`/`ConfirmingMultipleTrader`
için de aynı `Plot.PlotEnabled` deseni (ilgili menünün kendi `AppConfig` bölümünde) geçerli.

## Kimler Kullanıyor — Instantiation Noktaları

`new PythonPlotter(...)` için tüm kod tabanında grep taraması — **tek bir çağırım noktası**:

| Dosya | Bağlam | Satır |
|---|---|---|
| `AlgoTrade.Core/Trading/AlgoTrader.cs` | `SetupPython()` — `_pythonPlotter` (parametresiz `new PythonPlotter()`, sonra `PythonDll` ayrıca set edilir) | 2995 |

- `AlgoTrade.WinForms` bu sınıfa hiç dokunmuyor (Console'a özgü bir akış).
- 4 farklı Console menü fonksiyonu (`runSingleTraderAlgoTrade`/`runMultipleTraderAlgoTrade`/
  `runConfirmingSingleTraderAlgoTrade`/`runConfirmingMultipleTraderAlgoTrade`) `AlgoTrader.SetupPython()`
  üzerinden DOLAYLI olarak bu tek instance'ı paylaşıyor — her çağrıda `SetupPython()` "zaten
  kuruluysa" kontrolüyle aynı `_pythonPlotter`'ı yeniden kullanabilir (ya da bir öncekini
  `Dispose()` edip yeniden yaratır, `IsInitialized` durumuna göre).

## Kullanım Haritası

| Üye | Durum | Nerede |
|---|---|---|
| `Initialize()`, `RunHello()`, `PlotSingleTraderData`, `PlotMultipleTraderData`, `SetLogger`, `SetIndicators` | ✅ | `AlgoTrader.SetupPython()`/`PlotSingleTraderData`/`PlotMultipleTraderData` üzerinden, 4 Console menü akışında |
| `PythonDll` (property) | ✅ | `SetupPython()`'dan set ediliyor (venv'den çözümlenmiş DLL yolu) |
| `PythonScriptsDir` | ✅ (varsayılan değeriyle) | Hiçbir yerden elle override edilmiyor, her zaman `AppSettings.PythonScriptsDir` |
| `PythonPlotter(string pythonDll)` (parametreli constructor) | ❌ | Hiçbir yerde kullanılmıyor — her zaman parametresiz + ayrı `PythonDll=` ataması |
| `Shutdown()` (static) | ❌ | Hiçbir yerden çağrılmıyor — bkz. [Not](#kurulum) |
| `PlotOptimizationResults(...)` | ❌ | `SingleTraderOptimizer` akışına hiç bağlanmamış — bkz. [Not](#plotoptimizationresults--bkz-not-hiçbir-yerden-çağrılmıyor) |
| `CallPlotDataImgBundleNew`'in yorum satırındaki `plotDataImgBundleNew` modül çağrısı | ❌ | Kullanılmayan alternatif plot yolu — bkz. [Not](#plotsingletraderdata--plotmultipletraderdata--veri-aktarım-akışı) |
| `IndicatorManager.GetCachedIndicators()`'ın `td.indicators`'a aktarımı | ❌ | Performans nedeniyle yorum satırı — `td.indicators` her zaman boş `PyDict()`, sadece `td.strategy_indicators` (stratejinin kendi indikatörleri) doluyor |

## İlgili Dosyalar

- [01-class-reference.md](../01-class-reference.md) — ana index (bu sınıf §1-§9'a dahil değil,
  ayrı bir alt sistem olarak burada).
- [dearpyguidataplotter.md](dearpyguidataplotter.md) — daha yeni, geliştirilmekte olan alternatif
  plotter; aynı `SingleTrader`/`MultipleTrader` verisini farklı bir mekanizmayla çiziyor.
- [02-singletrader.md](02-singletrader.md) / [03-multipletrader.md](03-multipletrader.md) —
  `PlotEnabled` bayrağının tanımlı olduğu sınıflar.
- [06-class-doc-method.md](../06-class-doc-method.md) — bu sayfanın yazıldığı yöntem.
