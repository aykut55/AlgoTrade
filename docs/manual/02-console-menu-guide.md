# Console Menü Rehberi

> Bu doküman `AlgoTrade.Console/Program.cs`'teki `[1]`-`[25]` menüsünün her bir seçeneğinin ne
> yaptığını ve **yeni bir menü/özellik eklerken hangi dosyalara, hangi sırayla dokunulacağını**
> anlatır. `Program.cs` büyüdükçe (2076 → 4169 satır, bkz.
> [PROJECT_ANALYSIS.md](../PROJECT_ANALYSIS.md) §6.1) menüler arası deseni takip etmek
> zorlaşıyor — bu doküman o deseni tek yerde sabitliyor. Menü sayısı değiştiğinde (yeni `[26]`
> eklenince) buradaki tabloyu da güncelle. Yazım tarihi: 2026-08-21.

## İçindekiler

1. [Menü Haritası](#1-menü-haritası)
2. [Ortak Desen: handleXxx() + runXxxAlgoTrade() İkilisi](#2-ortak-desen-handlexxx--runxxxalgotrade-i̇kilisi)
3. [Yeni Bir Menü Öğesi Nasıl Eklenir (Adım Adım)](#3-yeni-bir-menü-öğesi-nasıl-eklenir-adım-adım)
4. [AutoRunMode — Menüsüz Otomatik Çalıştırma](#4-autorunmode--menüsüz-otomatik-çalıştırma)
5. [Preview/Confirm Ekranı Kısayolları](#5-previewconfirm-ekranı-kısayolları)

---

## 1. Menü Haritası

| # | Menü | Handler | Bir-shot Runner | Config Kaynağı |
|---|---|---|---|---|
| `[1]` | Read Data | `handleReadData()` | — | `AppConfig.ReadData` |
| `[2]` | SingleTrader | `handleSingleTrader()` | `runSingleTraderAlgoTrade()` | `AppConfig.SingleTrader` |
| `[3]` | MultipleTrader | `handleMultipleTrader()` | `runMultipleTraderAlgoTrade()` | `AppConfig.MultipleTrader` |
| `[4]` | SingleTraderOptimizer | `handleSingleTraderOpt()` | `runSingleTraderOptimization()` | `AppConfig.SingleTraderOptimizer` |
| `[5]` | Read Data + SingleTrader | `handleReadData()` sonra `handleSingleTrader()` | — | ReadData + SingleTrader |
| `[6]` | Read Data + MultipleTrader | aynı desen | — | ReadData + MultipleTrader |
| `[7]` | Read Data + SingleTraderOptimizer | aynı desen | — | ReadData + SingleTraderOptimizer |
| `[8]` | **Run Script** | `runFullScript()` | — | `inputs/scripts/*.csx` dosya seçimi |
| `[9]` | DearPyGuiDataPlotter (Test) | `handleDearPyGuiPlotterTest()` | — | Hardcoded test hook — **TODO: demo/test, silinecek** (bkz. [yapilacak.md](../yapilacak.md)) |
| `[10]` | Symbol Scan | `handleSymbolScan()` | — | `AppConfig.SymbolScan` |
| `[11]` | Timeframe Scan | `handleTimeframeScan()` | — | `AppConfig.TimeframeScan` |
| `[12]` | Multi-Strategy Timeframe Scan | `handleMultiStrategyTimeframeScan()` | — | `AppConfig.MultiStrategyTimeframeScan` |
| `[13]` | Symbol-Timeframe Scan | `handleSymbolTimeframeScan()` | — | `AppConfig.SymbolTimeframeScan` |
| `[14]` | Multi-Strategy Symbol Scan | `handleMultiStrategySymbolScan()` | — | `AppConfig.MultiStrategySymbolScan` |
| `[15]` | Multi-Strategy Symbol-Timeframe Scan | `handleMultiStrategySymbolTimeframeScan()` | — | `AppConfig.MultiStrategySymbolTimeframeScan` |
| `[16]` | Query Symbol Scan | `handleQuerySymbolScan()` | — | `AppConfig.QuerySymbolScan` |
| `[17]` | Query Timeframe Scan | `handleQueryTimeframeScan()` | — | `AppConfig.QueryTimeframeScan` |
| `[18]` | Multi-Query Timeframe Scan | `handleMultiQueryTimeframeScan()` | — | `AppConfig.MultiQueryTimeframeScan` |
| `[19]` | Query Symbol-Timeframe Scan | `handleQuerySymbolTimeframeScan()` | — | `AppConfig.QuerySymbolTimeframeScan` |
| `[20]` | Multi-Query Symbol Scan | `handleMultiQuerySymbolScan()` | — | `AppConfig.MultiQuerySymbolScan` |
| `[21]` | Multi-Query Symbol-Timeframe Scan | `handleMultiQuerySymbolTimeframeScan()` | — | `AppConfig.MultiQuerySymbolTimeframeScan` |
| `[22]` | ConfirmingSingleTrader | `handleConfirmingSingleTrader()` | `runConfirmingSingleTraderAlgoTrade()` | `AppConfig.ConfirmingSingleTrader` |
| `[23]` | Read Data + ConfirmingSingleTrader | aynı desen | — | ReadData + ConfirmingSingleTrader |
| `[24]` | ConfirmingMultipleTrader | `handleConfirmingMultipleTrader()` | `runConfirmingMultipleTraderAlgoTrade()` | `AppConfig.ConfirmingMultipleTrader` |
| `[25]` | Read Data + ConfirmingMultipleTrader | aynı desen | — | ReadData + ConfirmingMultipleTrader |
| `[0]` | Exit | — | — | — |

`[10]`-`[21]` (12 Scanner menüsü) kendi `handleXxx()`'i içinde hem config okuma hem çalıştırmayı
yapıyor — ayrı bir `runXxxAlgoTrade()` çifti yok, çünkü Scanner sınıfları `AlgoTrader`'dan
bağımsız, kendi başına yeten sınıflar (bkz. [PROJECT_ANALYSIS.md](../PROJECT_ANALYSIS.md) §2.10).

Varsayılan seçim `"5"` (`MenuInput("5")`), yani boş `[ENTER]` = Read Data + SingleTrader.

## 2. Ortak Desen: handleXxx() + runXxxAlgoTrade() İkilisi

`[2]`-`[4]` ve `[22]`/`[24]` (yani "gerçek Trader çalıştıran" menüler) hep aynı iki katmanlı
yapıyı kullanır:

**`handleXxx()`** — *interaktif kabuk*. Kullanıcıyla konuşan taraf:
1. `reloadAppConfig()` — `AppConfig.json`'ı diskten tekrar okur (her menüye girişte güncel).
2. `showModeConfigSummary("Xxx")` — o modun mevcut config özetini renkli JSON olarak basar.
3. Kullanıcıdan giriş bekler: `[ENTER]` çalıştır, `[E]` config dosyasını editörde aç + reload,
   `[R]` sadece reload, `[B]`/`ESC` geri.
4. `showXxxRunPreview(...)` — çalıştırmadan hemen önce son bir önizleme + aynı E/R/B/ENTER seçimi.
5. `[ENTER]`'da **asıl işi yapan** `runXxxAlgoTrade()`'i `await` eder.
6. Run bitince "`[ENTER]` ana menü / `[R]` tekrar çalıştır / `[ESC]` çıkış" ekranı.

**`runXxxAlgoTrade()`** — *bir-shot çalıştırıcı*, kullanıcıyla hiç konuşmaz (script'lerin
[`03-scripting-guide.md`](03-scripting-guide.md) içindeki tek-seferlik akışıyla birebir aynı
iskelet):
1. `new AlgoTrader("AlgoTrader")` yarat, `RegisterLogger`/`RegisterTimer`, `Reset()`,
   `SetData(stockDataReader.GetData())`.
2. `SymbolName`/`SymbolPeriod` meta veriden set edilir.
3. `AppConfigApplier.ApplyXxx(algoTrader, appConfig.Xxx, AppSettings.ConfigsDir)` — **asıl config
   → nesne köprüsü burada**, bkz. [01-class-reference.md](01-class-reference.md) `AppConfigApplier`
   bölümü.
4. `algoTrader.Initialize()`.
5. `await algoTrader.RunXxxWithProgressAsync()`.
6. `algoTrader.WriteTraderDataToFilesAsync(...)` — dosyaya yazma (paralel başlatılabilir, plot ile
   birlikte).
7. `PlotEnabled` ise `algoTrader.PlotXxxData(...)`.

Bu ikili yapı sayesinde `runXxxAlgoTrade()` fonksiyonları **Console UI'dan bağımsız** —
`inputs/scripts/*.csx` script'leri de aynı adımları tekrarlıyor (örn.
`02_RunMultipleTraderWithProgressAsync.csx`), sadece config'i dosyadan/hardcoded okuyorlar,
interaktif menü döngüsü yok.

## 3. Yeni Bir Menü Öğesi Nasıl Eklenir (Adım Adım)

Örnek senaryo: yeni bir "XyzTrader" tipi eklendiğini varsayalım (yeni bir Trader sınıfı zaten
`src/AlgoTrade.Core/Trading/Traders/XyzTrader.cs` olarak yazılmış olsun).

1. **`AppConfig.cs`**: kök `AppConfig` sınıfına yeni bir `public XyzTraderConfig XyzTrader { get; set; } = new();`
   property'si + `XyzTraderConfig` DTO sınıfı ekle (diğer `*Config` sınıflarının yanına,
   `TradeParams`/`Signals`/`Plot`/`Optimization`/`Save` alt gruplarını taklit ederek — bkz.
   satır 237-241 deseni).
2. **`AppConfigApplier.cs`**: `ApplyXyzTrader(AlgoTrader algoTrader, XyzTraderConfig cfg, string configsDir)`
   metodu ekle — `ApplyMultipleTrader`/`ApplyConfirmingSingleTrader` metodlarından birini şablon
   al, config alanlarını `algoTrader.SetXyzTraderXxxConfig(...)` çağrılarına çevir.
3. **`AlgoTrader.cs`**: `XyzTrader` property'si (private setter+public getter, `SingleTrader`/
   `MultipleTrader` property'leri gibi) + `SetXyzTraderXxxConfig(...)` config-injection metodları +
   `RunXyzTraderWithProgressAsync()` (mevcut `RunMultipleTraderWithProgressAsync()`'in iskeletini
   kopyala: indicators yarat → trader yarat → config uygula → bar-bar `for` döngüsü → Finalize).
4. **`Program.cs`**:
   - `runXyzTraderAlgoTrade()` — §2'deki 7 adımlık deseni takip eden bir-shot runner.
   - `handleXyzTrader()` — §2'deki interaktif kabuk (mevcut `handleMultipleTrader()`'ı kopyala).
   - `showXxxRunPreview`/`showModeConfigSummary` çağrılarını `"XyzTrader"` etiketiyle genişlet
     (bu fonksiyonlar zaten mod-adı parametreli, muhtemelen sadece yeni case eklemek yeterli).
   - Ana menü `switch` bloğuna yeni case: `case "26": await handleXyzTrader(); break;` (ve
     istenirse `[27] Read Data + XyzTrader`).
   - Menü metnini basan `showMainMenu()`'a yeni satırı ekle.
5. **`inputs/scripts/readme.txt`** ve **bu dosyadaki tablo**: yeni menü numarasını ve varsa yeni
   `NN_RunXyzTraderWithProgressAsync.csx` + `Config_NN_XyzTrader.csx` çiftini kaydet (script
   tarafında nasıl yazılacağı: [03-scripting-guide.md](03-scripting-guide.md)).
6. **`dotnet build`** ile derle, `[8] Run Script` üzerinden ya da doğrudan menüden gerçek veriyle
   test et.

Scanner tipi bir menü ekliyorsan (AlgoTrader'dan bağımsız, kendi başına yeten sınıf) adım 2-3
gerekmiyor — doğrudan Scanner sınıfını + `Config` DTO'sunu + `handleXxx()` içinde `RunAsync(...)`
çağrısını yazman yeterli, mevcut 12 Scanner sınıfından biri (örn. `SymbolScanner.cs`) iyi bir
şablon.

## 4. AutoRunMode — Menüsüz Otomatik Çalıştırma

`AppConfig.AppSettings.AutoRunMode` doluysa (`Program.cs` içinde ~satır 4013-4041) menü hiç
gösterilmeden ilgili `runXxxAlgoTrade()` doğrudan çağrılır. Geçerli değerler (case-insensitive):
`SingleTrader`, `MultipleTrader`, `SingleTraderOptimizer`, `ConfirmingSingleTrader`,
`ConfirmingMultipleTrader`. Yeni bir Trader tipi eklersen bu switch'e de bir `case` eklemeyi
unutma (adım 4'ün doğal uzantısı).

## 5. Preview/Confirm Ekranı Kısayolları

Her `handleXxx()` ekranında ortak kısayollar:

| Tuş | Anlamı |
|---|---|
| `[ENTER]` | Devam et / çalıştır |
| `[E]` | Config dosyasını (`AppConfig.json`) editörde aç, kapanınca otomatik reload |
| `[R]` | Sadece reload (dosyayı elle değiştirdiysen) |
| `[B]` | Bir önceki ekrana dön |
| `[ESC]` (Read timeout sırasında) | Uygulamadan çık |
| `[T]` | Geri-sayım timer'ını duraklat (kendi yazılmış `ReadMenuInputWithTimeout` özelliği) |

---

## İlgili Dosyalar

- [01-class-reference.md](01-class-reference.md) — `AlgoTrader`/`AppConfigApplier` vb. sınıfların API referansı
- [03-scripting-guide.md](03-scripting-guide.md) — aynı `runXxxAlgoTrade()` iskeletinin `.csx` script karşılığı
- [../PROJECT_ANALYSIS.md](../PROJECT_ANALYSIS.md) §6.1 — Console uygulamasının genel envanteri
- [../todo.md](../todo.md) "Tarama Motorları" — 12 Scanner sınıfının detaylı listesi
