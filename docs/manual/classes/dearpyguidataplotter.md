# DearPyGuiDataPlotter — Ayrı Process Tabanlı Görselleştirme (Yeni, Geliştirilmekte)

> [Class Reference](../01-class-reference.md) setinin bir parçası — `01-class-reference.md`'nin
> §1-§9 numaralı sınıflarına dahil DEĞİL. Kardeş sayfa: [PythonPlotter](python-plotter.md)
> (eski/varsayılan, in-process pythonnet plotter — bu sınıf onun YERİNE geçmeyi hedefleyen,
> henüz üretim akışına tam bağlanmamış alternatif). Yöntem: [06-class-doc-method.md](../06-class-doc-method.md).

### Dosyalar

- `src/AlgoTrade.Core/Python/DearPyGuiDataPlotter/DearPyGuiDataPlotter.cs` (276 satır) — process
  yaşam döngüsü + dosya-tabanlı runtime komut gönderme.
- `src/AlgoTrade.Core/Python/DearPyGuiDataPlotter/TradeDataBundleConverter.cs` — `SingleTrader`
  sonuçlarını `.npz` bundle + `.view.json` çiftine dönüştürür (bu dokümanda `ConvertSingleTrader(...)`
  public yüzeyiyle ele alınıyor, `.npz` iç formatı kapsam dışı).
- `src/DearPyGuiDataPlotter/` — AYRI bir Python projesi (`main.py`, `src/plotting/runtimeCommandManager.py`
  dahil), bu C# sınıfının `Process.Start(...)` ile başlattığı gerçek DearPyGui uygulaması; bu
  dokümanın kapsamı dışında.
- `docs/InteractionManagerDavranisi.md`, `docs/ManualAxisSyncPlan.md` — DearPyGuiDataPlotter'ın
  Python tarafındaki panel/eksen etkileşim mantığı üzerine eski tasarım notları (mkdocs'ta
  "eskimiş" uyarısıyla işaretli, güncel kod davranışını tam yansıtmıyor — bu dokümanın kapsamı
  dışında).

### Rolü

- `PythonPlotter`'ın (bkz. [PythonPlotter dokümanı](python-plotter.md)) aksine **in-process
  pythonnet DEĞİL** — proje kökündeki ortak `.venv`'in `python.exe`'siyle **ayrı bir process**
  başlatır (`Process.Start(...)`) ve onunla **dosya tabanlı** (JSON komut dosyaları,
  `inputs/runtime_commands/`) iletişim kurar.
- Veri aktarımı gerçek zamanlı değil — önce `TradeDataBundleConverter.ConvertSingleTrader(...)`
  ile `SingleTrader` sonuçları bir `.npz` (NumPy array) + `.view.json` (panel yerleşimi) dosya
  çiftine "donduruluyor", sonra `LoadBundle(...)` komutuyla çalışan process'e "şu dosyaları oku"
  denilerek yükletiliyor.
- Sınıfın kendi XML doc yorumu bunu açıkça "ileride" olarak işaretliyor: *"Veri aktarımı ileride
  npz bundle + view.json dosyaları ve dosya tabanlı runtime command'lar üzerinden yapılacak."*
  — yani bu sınıf, projenin planladığı ama henüz `PlotSingleTraderData`/`PlotMultipleTraderData`
  akışının YERİNİ almamış bir sonraki nesil plotter.

### Ne zaman kullanılır

- **Şu an için**: sadece Console'daki `[9] DearPyGuiDataPlotter (Start/Stop Test)` menüsü (ve
  onun script hali) üzerinden — kod içi TODO'nun da belirttiği gibi bu menü **demo/test amaçlı**,
  "gerçek switch (PlotBackend seçimi) mevcut `PlotSingleTraderData`/`PlotMultipleTraderData`
  akışına taşındığında silinecek" (bkz. [Kullanım Haritası](#kullanım-haritası)).
- Ayrıca `runSingleTraderAlgoTrade()`'in (Console `[2]`) SONUNDA, `PlotEnabled=true` iken,
  gerçek `SingleTrader` verisiyle converter'ı test etmek için EK bir adım olarak (pythonnet
  akışına dokunmadan, paralel) çalıştırılıyor — bkz. [Çağrı
  Zinciri](#çağrı-zinciri--menüden-çağrılma-programcs--dearpyguidataplotter).
- Bundle'ı elle üretip DearPyGuiDataPlotter'ı bağımsız test etmek istediğinde: script `[4]` →
  `[5]` sırasıyla (bkz. [Tipik Kullanım](#tipik-kullanım--scriptten-çağrılma)).

### Sınıf İskeleti (ilk bakış)

```csharp linenums="1"
public class DearPyGuiDataPlotter : IDisposable
{
    public string ProjectDir { get; set; } = AppSettings.DearPyGuiDataPlotterDir;
    public string PythonExePath => Path.Combine(AppSettings.VenvDir, "Scripts", "python.exe");
    public string MainScriptPath => Path.Combine(ProjectDir, "main.py");
    public bool IsRunning => _process is { HasExited: false };
    public string CommandsDir => Path.Combine(ProjectDir, "inputs", "runtime_commands");

    private Process? _process;
    private LogManager? _logger;
    private bool _disposed;
    private int _commandSequence;

    public DearPyGuiDataPlotter();
    public void SetLogger(LogManager? logger);

    // ---- Process Lifecycle ----
    public void StartPlotter();
    public void StopPlotter(int gracefulTimeoutMs = 3000);

    // ---- Runtime Commands ----
    public void LoadBundle(string bundlePath, string? viewPath = null);
    private string ToInputsRelativePath(string path);
    public void ClearPanel(int panelId);
    public void ClearAllPanels();
    public void ReloadCurrent();
    public void AddSeriesFromBundle(int panelId, string source, string? name = null, int? dataId = null);
    public void Shutdown();
    private void WriteCommand(string commandName, object payload);

    // ---- IDisposable ----
    protected virtual void Dispose(bool disposing);
    public void Dispose();
}
```

### Üye İndeksi — Hangisi Nerede Anlatılıyor

| # | Üye | Tür | Detay |
|---|---|---|---|
| 3 | `DearPyGuiDataPlotter::ProjectDir` | public property | [Process Lifecycle](#process-lifecycle-startplotter--stopplotter) |
| 4 | `DearPyGuiDataPlotter::PythonExePath` | public property (computed) | [Process Lifecycle](#process-lifecycle-startplotter--stopplotter) |
| 5 | `DearPyGuiDataPlotter::MainScriptPath` | public property (computed) | [Process Lifecycle](#process-lifecycle-startplotter--stopplotter) |
| 6 | `DearPyGuiDataPlotter::IsRunning` | public property (computed) | [Process Lifecycle](#process-lifecycle-startplotter--stopplotter) |
| 7 | `DearPyGuiDataPlotter::CommandsDir` | public property (computed) | [Runtime Commands](#runtime-commands-dosya-tabanlı-komut-gönderme) |
| 9 | `DearPyGuiDataPlotter::_process` | private field | [Process Lifecycle](#process-lifecycle-startplotter--stopplotter) |
| 10 | `DearPyGuiDataPlotter::_logger` | private field | [Kurulum](#kurulum) |
| 11 | `DearPyGuiDataPlotter::_disposed` | private field | [Kurulum](#kurulum) |
| 12 | `DearPyGuiDataPlotter::_commandSequence` | private field | [Runtime Commands](#runtime-commands-dosya-tabanlı-komut-gönderme) — dosya adı sırası |
| 14 | `DearPyGuiDataPlotter::DearPyGuiDataPlotter()` | constructor | [Kurulum](#kurulum) |
| 15 | `DearPyGuiDataPlotter::SetLogger(logger)` | public method | [Kurulum](#kurulum) |
| 18 | `DearPyGuiDataPlotter::StartPlotter()` | public method | [Process Lifecycle](#process-lifecycle-startplotter--stopplotter) |
| 19 | `DearPyGuiDataPlotter::StopPlotter(...)` | public method | [Process Lifecycle](#process-lifecycle-startplotter--stopplotter) |
| 22 | `DearPyGuiDataPlotter::LoadBundle(...)` | public method | [Runtime Commands](#runtime-commands-dosya-tabanlı-komut-gönderme) |
| 23 | `DearPyGuiDataPlotter::ToInputsRelativePath(path)` | private method | [Runtime Commands](#runtime-commands-dosya-tabanlı-komut-gönderme) — iç yardımcı |
| 24 | `DearPyGuiDataPlotter::ClearPanel(panelId)` | public method | [Runtime Commands](#runtime-commands-dosya-tabanlı-komut-gönderme) |
| 25 | `DearPyGuiDataPlotter::ClearAllPanels()` | public method | [Runtime Commands](#runtime-commands-dosya-tabanlı-komut-gönderme) — bkz. Not, hiç kullanılmıyor |
| 26 | `DearPyGuiDataPlotter::ReloadCurrent()` | public method | [Runtime Commands](#runtime-commands-dosya-tabanlı-komut-gönderme) — bkz. Not, hiç kullanılmıyor |
| 27 | `DearPyGuiDataPlotter::AddSeriesFromBundle(...)` | public method | [Runtime Commands](#runtime-commands-dosya-tabanlı-komut-gönderme) — bkz. Not, hiç kullanılmıyor |
| 28 | `DearPyGuiDataPlotter::Shutdown()` | public method | [Process Lifecycle](#process-lifecycle-startplotter--stopplotter) — `StopPlotter()`'ın nazik-kapatma adımı |
| 29 | `DearPyGuiDataPlotter::WriteCommand(...)` | private method | [Runtime Commands](#runtime-commands-dosya-tabanlı-komut-gönderme) — iç yardımcı, TÜM public komutların ortak yazım noktası |
| 32 | `DearPyGuiDataPlotter::Dispose(disposing)` | protected virtual method | [Kurulum](#kurulum) |
| 33 | `DearPyGuiDataPlotter::Dispose()` | public method | [Kurulum](#kurulum) |

## Public API

### Kurulum

- `DearPyGuiDataPlotter()` — parametresiz constructor, hiçbir şey yapmaz (`ProjectDir` varsayılan
  değeriyle `AppSettings.DearPyGuiDataPlotterDir`'a işaret eder).
- `SetLogger(logger)` — basit setter, tüm `LogManager.WriteRaw(...)` çağrıları buna gider.
- `Dispose()` → `Dispose(disposing: true)` + `GC.SuppressFinalize`. `Dispose(bool)`, `PythonPlotter`'ın
  aksine (bkz. [PythonPlotter § Kurulum](python-plotter.md#kurulum) — orada `Dispose` bilerek
  hiçbir şey kapatmıyordu), burada **gerçekten `StopPlotter()`'ı çağırıyor** — yani bu sınıfı
  `using` bloğunda kullanmak (`handleDearPyGuiPlotterTest()`'in yaptığı gibi) process'in düzgün
  kapatılmasını garanti eder.

### Process Lifecycle: `StartPlotter()` / `StopPlotter()`

```csharp linenums="1"
public void StartPlotter()
{
    if (IsRunning) return;

    if (!File.Exists(PythonExePath))
        throw new FileNotFoundException($"DearPyGuiDataPlotter venv python.exe bulunamadı: {PythonExePath}");
    if (!File.Exists(MainScriptPath))
        throw new FileNotFoundException($"DearPyGuiDataPlotter main.py bulunamadı: {MainScriptPath}");

    var psi = new ProcessStartInfo
    {
        FileName = PythonExePath,          // .venv/Scripts/python.exe
        Arguments = $"\"{MainScriptPath}\"",   // src/DearPyGuiDataPlotter/main.py
        WorkingDirectory = ProjectDir,
        UseShellExecute = false,
    };

    _process = Process.Start(psi);
}
```

- `IsRunning` → `_process is { HasExited: false }` — `_process == null` veya `HasExited == true`
  ise `false` (C# pattern matching, `null` ve `HasExited=true` durumlarını tek ifadede kapsıyor).
- `StopPlotter(gracefulTimeoutMs = 3000)` — önce `Shutdown()` (bir "shutdown" JSON komutu yazar,
  Python tarafı `dpg.stop_dearpygui()` çağırıp kendi kendine kapanır), `WaitForExit(3000ms)`
  içinde kapanmazsa `_process.Kill(entireProcessTree: true)` ile zorla kapatır. `finally`
  bloğunda her durumda `_process.Dispose(); _process = null;`.
- `PythonExePath`/`MainScriptPath` her ikisi de COMPUTED property (`=>`), her çağrıda `ProjectDir`/
  `AppSettings.VenvDir`'den yeniden hesaplanır — `ProjectDir`'i `StartPlotter()`'dan ÖNCE
  değiştirirsen farklı bir proje kökünü hedefleyebilirsin (ama bunu yapan bir kod yok, her yerde
  varsayılan kullanılıyor).

### Runtime Commands: Dosya-Tabanlı Komut Gönderme

```csharp linenums="1"
private void WriteCommand(string commandName, object payload)
{
    Directory.CreateDirectory(CommandsDir);

    int sequence = System.Threading.Interlocked.Increment(ref _commandSequence);
    string fileName = $"{sequence:D6}_{commandName}.json";
    string finalPath = Path.Combine(CommandsDir, fileName);
    string tempPath = finalPath + ".tmp";

    string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
    File.WriteAllText(tempPath, json);
    File.Move(tempPath, finalPath);   // atomik rename — Python asla yarım yazılmış dosya okumaz
}
```

- Tüm public komut metodları (`LoadBundle`/`ClearPanel`/`ClearAllPanels`/`ReloadCurrent`/
  `AddSeriesFromBundle`/`Shutdown`) sonunda `WriteCommand(...)`'a düşer — sıralı, artan
  (`000001_load_bundle.json`, `000002_clear_panel.json`, ...) dosya adlarıyla `CommandsDir`'e
  (`{ProjectDir}/inputs/runtime_commands/`) yazılır. Python tarafındaki
  `runtimeCommandManager.py` bu klasörü **her frame poll ediyor** (kod içi yorumdan) — yani
  komutlar anlık değil, bir sonraki Python frame'inde işlenir.
  - `.tmp` → `.json` atomik rename deseni, Python'un yarım yazılmış bir JSON dosyasını
    okumasını engelliyor (`Interlocked.Increment` de aynı process'ten paralel `WriteCommand`
    çağrıları için sıra numarasının çakışmamasını garanti ediyor).
- `LoadBundle(bundlePath, viewPath?)` — `bundlePath` zorunlu (boşsa `ArgumentException`),
  `viewPath` opsiyonel. İkisi de `ToInputsRelativePath(...)` ile — proje (repo) kökü ALTINDAYSA
  relative path'e çevrilir (`/` ile, ters slash değil), DEĞİLSE mutlak yol olduğu gibi bırakılır
  — Python tarafının yazdığı `inputs/input.json`'un makineler arası (farklı proje kök yolları)
  taşınabilir kalması için (kod içi yorumda gerekçeli).
- `ClearPanel(panelId)`/`ClearAllPanels()`/`ReloadCurrent()`/`AddSeriesFromBundle(...)` — hepsi
  aynı `WriteCommand` deseniyle basit JSON payload'lar üretir (`{"command": "...", ...}`).

> **Not — `ClearAllPanels`/`ReloadCurrent`/`AddSeriesFromBundle` tanımlı ama şu an hiçbir Console/
> script akışından çağrılmıyor:** Grep taraması — `AlgoTrade.Console`, `inputs/scripts/*.csx`
> genelinde bu 3 metodun (`LoadBundle`/`ClearPanel`/`Shutdown`/`StartPlotter`/`StopPlotter`'ın
> aksine) hiçbir çağıranı yok. API yüzeyi olarak hazırlar (Python tarafındaki
> `runtimeCommandManager.py`'nin desteklediği komut kümesinin tamamını C#'a açmak için), ama
> `[9]` test menüsü/script'i sadece `StartPlotter`/`LoadBundle`/`ClearPanel`/`StopPlotter`
> dörtlüsünü egzersiz ediyor.

### `TradeDataBundleConverter.ConvertSingleTrader(...)` — Veri Hazırlama

```csharp linenums="1"
public (string bundlePath, string viewPath) ConvertSingleTrader(
    SingleTrader trader, string outputDir, string fileBaseName = "latest_bundle")
```

- `trader` — **`Finalize()` çağrılmış** olmalı (`trader.Data`/`trader.lists` dolu olmalı, aksi
  halde `ArgumentException`/`InvalidOperationException`). `outputDir` — `.npz`/`.view.json`'ın
  yazılacağı klasör (Console akışında `AppSettings.DearPyGuiDataPlotterDir/inputs`).
  `fileBaseName` (varsayılan `"latest_bundle"`) — HER ÇAĞRIDA aynı isim, üzerine yazılır (versiyon
  geçmişi tutulmuz).
- Üretilen panel seti (kod içi yoruma göre): OHLC, Signals (AL/SAT/FLAT), PnL, PnL %,
  Return+Net Return, Return %+Net Return %, ve stratejinin kendi indikatörleri (`trader.Strategy?.GetPlotIndicators()`
  — [SingleTrader](02-singletrader.md)'ın `strategyIndicators` kavramıyla aynı kaynak,
  [PythonPlotter](python-plotter.md#plotsingletraderdata--plotmultipletraderdata--veri-aktarım-akışı)'ın
  kullandığıyla birebir aynı API).
- `signal_codes` (SEYREK — sadece değişim barlarında event kodu, `TradeSignalRenderer` için) vs
  `signal_steps` (YOĞUN — her barda tekrar eden durum kodu, "Signals" paneli için) — iki farklı
  temsil, tek `lists.SinyalList`'ten türetiliyor.
- `.npz` iç formatı (NumPy array serialization) ve `.view.json`'ın panel yerleşim şeması bu
  dokümanın kapsamı dışında — `PadOrTrim`/`BuildSignalCodes` gibi private yardımcılar sadece
  isim olarak var, gövdeleri burada anlatılmıyor.

## Çağrı Zinciri — Menüden Çağrılma (Program.cs → DearPyGuiDataPlotter)

**İki AYRI, birbirinden bağımsız menü yolu var** — biri gerçek trader verisiyle converter'ı test
ediyor, diğeri process'i bağımsız (bundle üretiminden ayrı) başlatıp komut gönderiyor:

**1) `runSingleTraderAlgoTrade()` içindeki EK converter testi** (Console `[2]`, `PlotEnabled=true`
iken, `PlotSingleTraderData` çağrısından SONRA — bkz. [SingleTrader § Çağrı
Zinciri](02-singletrader.md#çağrı-zinciri--menüden-çağrılma-programcs--algotrader--singletrader)):

```csharp linenums="1"
// TODO: DearPyGuiDataPlotter converter/switch TESTİ - bkz. docs/yapilacak.md.
// Gerçek PlotBackend switch'i entegre olunca bu blok kaldırılıp yukarıdaki
// pythonnet/imgui_bundle çağrısıyla aynı yerde düzgünce (switch ile) sarılacak.
// Şimdilik pythonnet akışına dokunmadan, AYNI SingleTrader'dan npz bundle
// üretip DearPyGuiDataPlotter'da da açıyor (converter'ı gerçek veriyle test etmek için).
try
{
    var bundleConverter = new TradeDataBundleConverter();
    string bundleOutDir = Path.Combine(AppSettings.DearPyGuiDataPlotterDir, "inputs");
    var (bundlePath, viewPath) = bundleConverter.ConvertSingleTrader(
        algoTrader.SingleTrader, bundleOutDir);

    dearPyGuiTestPlotter ??= new DearPyGuiDataPlotter();   // Program.cs global — TODO: demo/test
    dearPyGuiTestPlotter.SetLogger(logger);
    dearPyGuiTestPlotter.StartPlotter();
    dearPyGuiTestPlotter.LoadBundle(bundlePath, viewPath);
}
catch (Exception ex)
{
    LogManager.LogError($"[DearPyGuiDataPlotter] Converter test hatası: {ex.Message}", ex);
}
```

- `dearPyGuiTestPlotter` — `Program.cs` üst-seviye DEĞİŞKENİ (`?? =` ile lazy-init, `Dispose`
  EDİLMİYOR — process açık kalır, sonraki koşumlarda `StartPlotter()`'ın `if (IsRunning) return;`
  guard'ı sayesinde yeniden başlatılmaz, aynı process'e yeni `LoadBundle` gönderilir).
- Bu blok **pythonnet akışına (`PlotSingleTraderData`) EK, onu değiştirmiyor** — aynı koşumda
  hem eski (`PythonPlotter`) hem yeni (`DearPyGuiDataPlotter`) plot penceresi açılabiliyor;
  kod içi TODO bunun geçici olduğunu, "gerçek switch" (hangi backend kullanılacağını seçen bir
  `PlotBackend` ayarı) geldiğinde bu ikili çalışmanın kaldırılacağını belirtiyor.

**2) `[9] DearPyGuiDataPlotter (Start/Stop Test)` — bağımsız demo menüsü** (`Program.cs:3928-3977`,
`handleDearPyGuiPlotterTest()`):

```csharp linenums="1"
using var plotter = new DearPyGuiDataPlotter();
plotter.SetLogger(logger);

plotter.StartPlotter();

string testBundlePath = Path.Combine(plotter.ProjectDir, "inputs", "latest_bundle.npz");
string testViewPath   = Path.Combine(plotter.ProjectDir, "inputs", "latest_bundle.view.json");

if (File.Exists(testBundlePath))
    plotter.LoadBundle(testBundlePath, File.Exists(testViewPath) ? testViewPath : null);

// [ENTER] Panel 0'ı temizle (clear_panel testi)   [ESC] Kapat ve ana menüye dön
if (ReadMenuInput() != null)
{
    plotter.ClearPanel(0);
    // [ENTER] Plotter'ı kapat ve ana menüye dön
    ReadMenuInput();
}

plotter.StopPlotter();   // using bloğu + Dispose() zaten garanti eder, burada açıkça de çağrılıyor
```

- `using var plotter` — bu blok `Dispose()`'un gerçekten `StopPlotter()`'ı çağırdığı davranıştan
  faydalanıyor (yukarıdaki `dearPyGuiTestPlotter`'ın aksine, burada process her seferinde temiz
  kapanıyor).
- Kod içi TODO (`Program.cs:3922-3927`) bu menünün TAMAMEN demo/test amaçlı olduğunu, "gerçek
  switch" gelince silineceğini açıkça belirtiyor — [Kullanım Haritası](#kullanım-haritası)'nda
  bu nedenle özel olarak işaretlendi.
- `testBundlePath`/`testViewPath` sabit `latest_bundle.npz`/`.view.json` — yani bu menü kendi
  bundle'ını ÜRETMEZ, önceden `04_GenerateDearPyGuiDataPlotterBundle.csx` (veya `[2]`'nin EK
  converter testi) tarafından üretilmiş olmasını bekler; dosya yoksa sessizce `LoadBundle`'ı
  atlar (uyarı loglar, hata fırlatmaz).

## Dönüş / Sonuç — Global State

| Değişken/Erişim | Tip | Kaynak |
|---|---|---|
| `dearPyGuiTestPlotter` (Program.cs global) | `DearPyGuiDataPlotter?` | `runSingleTraderAlgoTrade()`'in EK converter testi — lazy-init, hiç `Dispose()` edilmiyor |
| `src/DearPyGuiDataPlotter/inputs/latest_bundle.npz` + `.view.json` | dosya | `TradeDataBundleConverter.ConvertSingleTrader(...)` |
| `src/DearPyGuiDataPlotter/inputs/runtime_commands/NNNNNN_{command}.json` | dosya | `WriteCommand(...)` — her komut çağrısında bir yenisi |
| Plot penceresi (ayrı process) | ekran | `StartPlotter()` sonrası, `LoadBundle`'ın Python tarafında işlenmesiyle |

- `PythonPlotter`'dan farklı olarak burada `await` edilen, pencere kapanana kadar BLOKLAYAN bir
  çağrı YOK — `StartPlotter()` process'i başlatıp hemen döner, plot penceresi ARKA PLANDA kendi
  event loop'unda çalışır; C# tarafı `IsRunning`'i istediği zaman sorgulayabilir.

## Tipik Kullanım — Script'ten Çağrılma

İki script BİRLİKTE çalışacak şekilde tasarlanmış — önce bundle üret, sonra o bundle'ı yükleyip
test et:

**1) `04_GenerateDearPyGuiDataPlotterBundle.csx` — bundle üret**

`Config_01_SingleTrader.csx`'i `#load` eder, KENDİ (dispose'suz) minimal `SingleTrader` run
akışını çalıştırır (`01_RunSingleTraderWithProgressAsync.csx`'i `#load` ETMİYOR — o script
sonunda `singleTrader.Dispose()` çağırıp `trader.Data`'yı boşaltıyor,
`TradeDataBundleConverter` ise "Finalize() sonrası Data dolu olmalı" bekliyor, bu yüzden ayrı
bir minimal akış yazılmış):

```csharp linenums="1"
var singleTrader = new SingleTrader(0, "singleTrader", data, indicators, null);
singleTrader.Reset();
singleTrader.SymbolName = symbolName;
singleTrader.SymbolPeriod = symbolPeriod;
singleTrader.initialTradeParams!.Reset()
    .SetBakiyeParams(ilkBakiye: ilkBakiye)
    .SetKontratParamsViopEndex(kontratSayisi: kontratSayisi)
    .SetKomisyonParams(komisyonCarpan: komisyonCarpan)
    .SetKaymaParams(kaymaMiktari: kaymaMiktari);
singleTrader.ConfigureUserFlagsOnce();
singleTrader.signals.AlEnabled = true;   // + SatEnabled/FlatOlEnabled/PasGecEnabled/KarAlEnabled/ZararKesEnabled
singleTrader.RunMode = TraderRunMode.TradeOnly;
singleTrader.SetStrategy(strategy);
singleTrader.Init();

for (int i = 0; i < totalBars; i++)
    singleTrader.Run(i);

singleTrader.Finalize();   // ← Data hâlâ dolu, Dispose() henüz çağrılmadı
```

```csharp linenums="1"
var converter = new TradeDataBundleConverter();
string outputDir = Path.Combine(AppSettings.DearPyGuiDataPlotterDir, "inputs");

var (bundlePath, viewPath) = converter.ConvertSingleTrader(singleTrader, outputDir);

Log($"Bundle yazildi : {bundlePath}");
Log($"View yazildi   : {viewPath}");

strategy?.Dispose();
singleTrader?.Dispose();   // ← converter'dan SONRA dispose ediliyor
```

**2) `05_RunDearPyGuiDataPlotterTest.csx` — bundle'ı yükleyip test et**

Ana menüdeki `[9]`'un script hali — tek fark, `ReadMenuInput()` yerine (script'ler Program.cs'in
konsol input fonksiyonlarına erişemediği için) sabit süreli bekleme + `IsCancellationRequested`
kontrolü kullanılıyor:

```csharp linenums="1"
var plotter = new DearPyGuiDataPlotter();

plotter.StartPlotter();

string testBundlePath = Path.Combine(plotter.ProjectDir, "inputs", "latest_bundle.npz");
string testViewPath   = Path.Combine(plotter.ProjectDir, "inputs", "latest_bundle.view.json");

if (File.Exists(testBundlePath))
    plotter.LoadBundle(testBundlePath, File.Exists(testViewPath) ? testViewPath : null);

int waitSeconds = 10;
for (int s = 0; s < waitSeconds; s++)
{
    if (IsCancellationRequested) break;   // script'in ESC iptali
    await Task.Delay(1000);
}

plotter.ClearPanel(0);
await Task.Delay(500);

plotter.StopPlotter();
```

- `[8] Run Script` ile önce `04_...` sonra `05_...` çalıştırılırsa, `04_...`'ün ürettiği
  `latest_bundle.npz`/`.view.json` `05_...`'in yükleyeceği tam dosyalardır — iki script arasında
  DOSYA SİSTEMİ üzerinden (aynı `outputDir`) örtük bir bağ var, doğrudan bir fonksiyon çağrısı
  YOK.

## Console/JSON Eşleşmesi

`DearPyGuiDataPlotter`'ın kendi ayarlanabilir bir `AppConfig.json` bölümü YOK — `[9]` test
menüsü hiçbir config okumuyor, sabit `latest_bundle.npz` dosya adını arıyor. `runSingleTraderAlgoTrade()`
içindeki EK converter testi de `PlotEnabled` (SingleTrader'ın kendi bayrağı, bkz. [SingleTrader §
AppConfig Kaynağı](02-singletrader.md#appconfig-kaynağı--singletraderconfig)) `true` olduğunda
otomatik tetikleniyor — ayrı bir `DearPyGuiPlotEnabled` gibi bir bayrak yok, `PythonPlotter`'ın
kullandığı AYNI `Plot.PlotEnabled`'a bağlı.

## Kimler Kullanıyor — Instantiation Noktaları

`new DearPyGuiDataPlotter()` için tüm kod tabanında grep taraması:

| Dosya | Bağlam | Satır |
|---|---|---|
| `AlgoTrade.Console/Program.cs` | `runSingleTraderAlgoTrade()` içi — `dearPyGuiTestPlotter` (global, lazy-init) | 815 |
| `AlgoTrade.Console/Program.cs` | `handleDearPyGuiPlotterTest()` — `plotter` (`using`) | 3933 |
| `inputs/scripts/05_RunDearPyGuiDataPlotterTest.csx` | top-level akış — `plotter` | 16 |

- `new TradeDataBundleConverter()` için: `Program.cs:810` (`runSingleTraderAlgoTrade()` içi) ve
  `inputs/scripts/04_GenerateDearPyGuiDataPlotterBundle.csx:158` — sadece 2 nokta.
- `AlgoTrade.WinForms` bu sınıfa hiç dokunmuyor.

## Kullanım Haritası

| Üye | Durum | Nerede |
|---|---|---|
| `StartPlotter`, `StopPlotter`, `LoadBundle`, `ClearPanel`, `SetLogger`, `Dispose`, `IsRunning`, `ProjectDir` | ✅ | `[9]` test menüsü + script hali + `runSingleTraderAlgoTrade()`'in EK converter testi |
| `TradeDataBundleConverter.ConvertSingleTrader(...)` | ✅ | `04_GenerateDearPyGuiDataPlotterBundle.csx` + `runSingleTraderAlgoTrade()` |
| `ClearAllPanels`, `ReloadCurrent`, `AddSeriesFromBundle` | ❌ | Tanımlı, hiçbir Console/script akışından çağrılmıyor — bkz. [Not](#runtime-commands-dosya-tabanlı-komut-gönderme) |
| `Shutdown()` | ✅ (dolaylı) | `StopPlotter()`'ın nazik-kapatma adımı olarak — doğrudan dışarıdan çağıran yok |
| **`[9] DearPyGuiDataPlotter (Start/Stop Test)` menüsünün TAMAMI** (`handleDearPyGuiPlotterTest()`, `case "9"`, `dearPyGuiTestPlotter` global) | ⚠️ | Kod içi TODO'ya göre kasıtlı demo/test kodu — "gerçek switch (PlotBackend seçimi) mevcut akışa taşındığında silinecek" (`Program.cs:3922-3927`, `48`, `4077`) |
| `MultipleTrader`/`ConfirmingSingleTrader`/`ConfirmingMultipleTrader` sonuçlarının bundle'a çevrilmesi | ❌ | `TradeDataBundleConverter`'ın SADECE `ConvertSingleTrader(...)` overload'ı var — `MultipleTrader`/Confirming* için bir bundle converter henüz yazılmamış |

## İlgili Dosyalar

- [python-plotter.md](python-plotter.md) — bu sınıfın (henüz tamamlanmamış) yerini alması
  planlanan eski/varsayılan plotter, aynı derinlikte belgelenen kardeş sayfa.
- [02-singletrader.md](02-singletrader.md) — `TradeDataBundleConverter.ConvertSingleTrader(...)`'ın
  girdisi (`Finalize()` sonrası `SingleTrader`).
- [01-class-reference.md](../01-class-reference.md) — ana index (bu sınıf §1-§9'a dahil değil).
- [06-class-doc-method.md](../06-class-doc-method.md) — bu sayfanın yazıldığı yöntem.
- `docs/yapilacak.md` — "gerçek PlotBackend switch'i" (bu sayfanın birkaç yerinde referans
  verilen, henüz tamamlanmamış iş) için açık madde.
