# TODO

## Todo

- [ ] venv'ler merkezileştirilecek. Şu an projede 3 ayrı `.venv` klasörü var, toplam ~624 MB:
  - `inputs/python/.venv` — 356 MB — Ana AlgoTrade Python entegrasyonu (pythonnet vb.)
  - `src/DearPyGuiDataPlotter/.venv` — 134 MB — Aktif kullanılan DearPyGui plotter alt-projesi
  - `src/DearImGuiBundleDataPlotter/.venv` — 134 MB — Terk edilmiş imgui_bundle prototipi (hiçbir yerden çağrılmıyor)

- [ ] `D:\Aykut\Projects\Python ImGui Denemeleri\PythonImGuiProjects` ayrı bir Python projesi ve VS Code ile geliştiriliyor. Bu projedeki şu klasörler AlgoTrade'e kopyalanmış:
  - `DearImGuiBundleDataPlotter` → `src/DearImGuiBundleDataPlotter` (birebir aynı, hiç değiştirilmemiş)
  - `DearPyGuiDataPlotter` → `src/DearPyGuiDataPlotter` (kopyalandıktan sonra AlgoTrade tarafında üzerine geliştirme yapılmış)

- [ ] `src/DearPyGuiDataPlotter` içinde AlgoTrade tarafında yapılan değişikliklerin (panelManager.py, guiManager.py, panel.py, scriptPanel.py, tradeSignalRenderer.py, scripts/default.py, ve yeni eklenen src/plotting/runtimeCommandManager.py) `D:\Aykut\Projects\Python ImGui Denemeleri\PythonImGuiProjects\DearPyGuiDataPlotter` tarafına da yansıtılması gerekiyor (kaynak proje geride kaldı, senkronize değil)

- [ ] `inputs/python/` altındaki kodlar (data_plotter.py, data_plotter_img_bundle.py, panel.py, panel_data.py, plotter.py, trade_data.py, multiple_data_plotter.py, main.py) `src/` altında uygun bir klasöre taşınacak ve proje bu yeni dizindeki kodlarla çalışacak şekilde güncellenecek. Not: `data_plotter_img_bundle.py` (154 KB, imgui_bundle tabanlı tam gelişmiş multi-panel plotter) `src/DearImGuiBundleDataPlotter`'ın (şu an sadece boş "Hello World" iskeleti) hedeflediği içerik gibi duruyor.

- [ ] `inputs/python/` altındaki kodların orijinal olarak geliştirildiği ayrı Python projesi bulunacak ve buraya (bu maddeye) linklenecek. Şimdiye kadarki aramada kesin kaynak bulunamadı; `D:\Aykut\Projects\AlgoTradeWithPaython\src\data_plotter\data_plotter.py` aynı soydan ama birebir kaynak değil (imgui_bundle kullanmıyor, içerik farklı).

- [ ] [docs/migration-guide.md](migration-guide.md) — eski projeden (AlgoTradeWithOptimizationSupport) taşıma durumunu ve "Yol Haritası" (madde 1-10: MultiTrader, Getiri Eğrisi konfirmasyonu, Script yeteneği, Sorgu yapabilme, Performans raporu, Toplu sembol taraması, Strateji karşılaştırma vb.) henüz kod tabanında var mı yok mu diye periyodik kontrol et — belge zaman zaman stale kalabiliyor (StrategyFactory maddesi böyle bulunup güncellendi).

- [ ] İndikatör kütüphanesi (bkz. [docs/Indicators-TODO.md](Indicators-TODO.md)) fonksiyonel olarak tamam (109+ indikatör), kalan işler kalite/altyapı:
  - Kod kalitesi: XML doc, unit test (NUnit/xUnit), performans benchmark'ları, error handling
  - Özellikler: sinyal üretimi (crossover/divergence), indikatör karşılaştırma, backtesting entegrasyonu, multi-timeframe desteği
  - Performans: bulk paralel hesaplama, bellek optimizasyonu, SIMD
  - `IndicatorTest.cs`'e kapsamlı test metodları (bilerek ertelenmiş - "kullanıcı tarafından belirtildi")

- [ ] [docs/yapilacak.md](yapilacak.md) — DearPyGuiDataPlotter entegrasyonunda 3 açık iş kaldı:
  1. Gerçek `PlotBackend` switch'i (`Program.cs`/`AlgoTrader.SetupPython()` seviyesinde `{ ImguiBundle, DearPyGui }` seçimi) — şu an sadece test hook üzerinden çalışıyor
  2. Switch bitince `[9]` demo menüsü + geçici test hook'u silinecek
  3. X ekseni datetime formatını tek satıra indirmek (`panelManager.py:38`, `_dayChangeFormat`) — mekanizma hazır, sadece varsayılan değer değişecek

## Tarama Motorları — TAMAMLANDI (16/16, 2026-08-18)

Kullanıcının 2026-08-18'de tarif ettiği 8 senaryoluk matris (Sembol × Strateji × Zaman Dilimi,
her biri Tek/Çoklu) **hem Strateji hem Sorgu ekseninde tamamlandı** (8/8 + 8/8 = 16/16). Console
menüsünde `[10]`-`[21]` aralığında karşılıkları var. Tasarım kararları, sınıf/tablo eşlemeleri ve
kalan işler aşağıda; ayrıntılı mimari/bug postmortem'leri içeren `docs/tarama-motoru-plan.md`
artık silindi (içeriği buraya ve ilgili sınıfların XML doc comment'lerine taşındı).

Kaynak: [docs/PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md), [docs/migration-guide.md](migration-guide.md)
(madde 2, 4, 8, 9).

### Strateji Tarama Matrisi (8/8) — Sınıf ve Bağımsız/Bileşke Sinyal Tablosu

| # | Sembol | Strateji | Zaman Dilimi | Durum |
|---|--------|----------|---------------|-------|
| 1 | Tek | Tek | Tek | ✅ Mevcut — `SingleTrader` |
| 2 | Tek | Tek | Çoklu | ✅ `TimeframeScanner` — Console `[11]` |
| 3 | Tek | Çoklu (bileşke) | Tek | ✅ `MultipleTrader` (Net/Majority/All/Any consensus) — Console `[3]`/`[6]` |
| 4 | Tek | Çoklu (bileşke) | Çoklu | ✅ `MultiStrategyTimeframeScanner` — Console `[12]` |
| 5 | Çoklu | Tek | Tek | ✅ `SymbolScanner` — Console `[10]` |
| 6 | Çoklu | Tek | Çoklu | ✅ `SymbolTimeframeScanner` — Console `[13]` |
| 7 | Çoklu | Çoklu (bileşke) | Tek | ✅ `MultiStrategySymbolScanner` — Console `[14]` |
| 8 | Çoklu | Çoklu (bileşke) | Çoklu | ✅ `MultiStrategySymbolTimeframeScanner` — Console `[15]` |

"Bileşke" kavramı sadece **3/4/7/8**'de var (sadece onlarda "Çoklu Strateji" = `MultipleTrader`).
Hangi senaryoda bağımsız sinyal / bileşke sinyal raporlanıyor:

| # | Sembol | Strateji | Zaman Dilimi | Bağımsız sinyal | Bileşke sinyal |
|---|--------|----------|---------------|:---:|:---:|
| 1 | Tek | Tek | Tek | — (tek strateji, kavram yok) | — |
| 2 | Tek | Tek | Çoklu | ✅ her TF bağımsız (tasarım gereği) | — (TF'ler hiç birleşmiyor) |
| 3 | Tek | Çoklu | Tek | ✅ (child list dosyaları + debug log) | ✅ (mainTrader consensus) |
| 4 | Tek | Çoklu | Çoklu | ✅ (`TimeframeScanResult.ChildSignals`) | ✅ (her TF'nin kendi mainTrader'ı) |
| 5 | Çoklu | Tek | Tek | ✅ her sembol bağımsız | — (tek strateji) |
| 6 | Çoklu | Tek | Çoklu | ✅ her (sembol,TF) hücresi bağımsız (tek strateji → bileşke kavramı yok) | — |
| 7 | Çoklu | Çoklu | Tek | ✅ (`ScanResult.ChildSignals`) | ✅ (her sembolün kendi mainTrader'ı) |
| 8 | Çoklu | Çoklu | Çoklu | ✅ (`SymbolTimeframeScanResult.ChildSignals`) | ✅ (her hücrenin kendi mainTrader'ı) |

### Sorgu Tarama Matrisi (8/8) — Sınıf Tablosu

Aynı 8 senaryo, "Strateji" yerine "Sorgu" — "Çoklu Sorgu" hiçbir zaman birleştirilmiyor (bkz.
aşağıdaki karar), bu yüzden "Bağımsız/Bileşke" ayrımı yok, hepsi zaten bağımsız:

| # | Sembol | Sorgu | Zaman Dilimi | Durum |
|---|--------|-------|---------------|-------|
| 1 | Tek | Tek | Tek | ✅ Mevcut — `SingleTrader.RunMode = QueryOnly` |
| 2 | Tek | Tek | Çoklu | ✅ `QueryTimeframeScanner` — Console `[17]` |
| 3 | Tek | Çoklu | Tek | ✅ `MultipleQuery` (yeni primitive, consensus YOK) |
| 4 | Tek | Çoklu | Çoklu | ✅ `MultiQueryTimeframeScanner` — Console `[18]` |
| 5 | Çoklu | Tek | Tek | ✅ `QuerySymbolScanner` — Console `[16]`, **madde 9'un birebir istediği şey** |
| 6 | Çoklu | Tek | Çoklu | ✅ `QuerySymbolTimeframeScanner` — Console `[19]` |
| 7 | Çoklu | Çoklu | Tek | ✅ `MultiQuerySymbolScanner` — Console `[20]` |
| 8 | Çoklu | Çoklu | Çoklu | ✅ `MultiQuerySymbolTimeframeScanner` — Console `[21]` |

**Karar (kullanıcı ile netleşti, 2026-08-18)**: "Çoklu Sorgu" (3/4/7/8) **hiçbir zaman
birleştirilmiyor** — Strateji'deki "bileşke" (MultipleTrader, tek bir Al/Sat kararı) kavramının
Sorgu karşılığı yok. N sorgu çalıştırılır, N sonuç **ayrı ayrı** raporlanır (ayrı kolonlar),
kullanıcı kendisi yorumlar. `MultipleQuery` bu yüzden bir "consensus" sınıfı değil — N bağımsız
`SingleTrader` (QueryOnly) çalıştırıp sonuçları hiç birleştirmeden topluyor.

### Kalan işler (fast-follow, henüz başlanmadı)

- [ ] **TF'ler arası / semboller arası konsensüs** — kullanıcı şimdilik istemedi ama ileride
  isteyebilir. Şu an her tarama sınıfı TF/sembol eksenlerinde tamamen bağımsız çalışıyor
  (`TimeframeScanner`, `SymbolScanner` vb. hiçbir konsensüs/zaman-hizalama yapmıyor) —
  "bileşke" kavramı sadece strateji ekseninde (`MultipleTrader`) var. İleride istenirse: TF'ler
  arası bir konsensüs, bar index'lerin farklı granülerlikte aynı anı temsil etmediği için
  timestamp bazlı hizalama gerektirecek (ilk tasarım denemesinde bu karmaşıklık yüzünden
  vazgeçilmişti — kullanıcı bunun hiç niyeti olmadığını belirtmişti).
- [ ] Ek özellikler (tüm tarama sınıfları için — v1'de hiçbirinde yok):
  - Buffered flush / partial-resume (Optimizer'daki `FileFlushIntervalMs`/`PartialOpt` benzeri)
  - Zengin JSON preview ekranı (SingleTrader/MultipleTrader menülerindeki gibi) — şu an sadece kutu-stili özet var
  - `[T]` Pause/Resume Timer satırı tarama menülerinde yok
  - Time filtering / TradeStartBarIndex desteği — hiçbir tarama sınıfının options'ına eklenmedi, tüm semboller/TF'ler `TimeFilteringEnabled=false` ile taranıyor
  - Senaryo 8 sınıflarında (N×M×sorgu/strateji büyüklüğü kritik) kullanıcıya önceden çalışma süresi tahmini gösterme

**Madde 6 (zengin sorgu tipleri) — şimdilik gerek yok**: Sorgu tarama motorları tek somut sorgu
türüyle (`SimpleQuery1`, v1/v2 parametre varyasyonu) uçtan uca doğrulandı — mimari (dinamik
kolonlar, `MultipleQuery`, tüm 8 sınıf) çalıştığı için yeni bir sorgu türü (`IQuery` implementasyonu)
eklendiğinde aynı tarama sınıfları üzerinden otomatik çalışır, ekstra iş gerekmez. Gerçek çeşitlilik
(fiyat-indikatör kesişimi, indikatör-indikatör kesişimi vb.) ayrı, ileride istenirse ele alınacak.

## Script Yeteneği (Scripting) — Durum Analizi (2026-08-18)

Kullanıcı sorusu üzerine (`MultiTrader'da hiç script yeteneği yok mu?`) yapılan araştırma —
Console menülerinden hangilerinin `ScriptExecutor`/`.csx` script çalıştırma ile ilişkisi var,
hangilerinin yok, buraya kayıt altına alındı ki unutulmasın.

### `ScriptExecutor` ne yapıyor (`src/AlgoTrade.Core/Scripting/ScriptExecutor.cs`)

Roslyn (`Microsoft.CodeAnalysis.CSharp.Scripting`) tabanlı, **sandbox YOK** — script'e
`AlgoTrade.Core.dll`'in **tamamı** referans olarak veriliyor (`Assembly.GetExecutingAssembly()`),
yani script içinden projedeki herhangi bir public sınıfa (`SingleTrader`, `MultipleTrader`,
`SingleTraderOptimizer`, hatta bugün yazdığımız tarama sınıfları) doğrudan erişilebilir/
örneklenebilir. `ScriptGlobals` (`src/AlgoTrade.Core/Scripting/ScriptGlobals.cs`) script'e tek
bir global nesne (`algoTrader`) veriyor; `algoTrader.SingleTrader`/`.MultipleTrader`/
`.SingleTraderOptimizer` property'leri üzerinden üçüne de erişilebiliyor — ama hazır kolaylık
metodları (`Trader`, `OnProgress`, `OnSignal`, `RunAll()`, `Setup()`) **sadece `SingleTrader`
için** yazılmış.

### `MultiTrader` script'lenebilir mi? — Evet (nesne düzeyinde), Hayır (consensus kuralı düzeyinde)

- ✅ `inputs/scripts/ProgramsMultipleTrader.csx`, `runMultiTraderWithStrategies.csx`,
  `mainScriptMultipleTrader.csx` gibi gerçek örnek script'ler `MultipleTrader` kurup
  çalıştırıyor, sonuç okuyor — yani **MultipleTrader nesnesinin kendisi tam script'lenebilir**.
- ❌ Ama `MultipleTrader.BuildConsensusSignal()` (`MultipleTrader.cs:191-271`) tamamen hardcoded
  bir `switch` — sadece 4 sabit mod (Net/Majority/All/Any), `ConsensusMode` düz bir `string`
  property (delegate/hook değil). **Script'ten özel/farklı bir birleştirme kuralı tanımlamanın
  ilk sınıf (first-class) bir yolu yok** — migration-guide.md Madde 5.3'ün dediği tam olarak bu
  (script'ten TAMAMEN ÖZEL bir consensus kuralı tanımlanamıyor), "MultiTrader'da hiç script yok"
  değil.
- Aynı durum `SingleTraderOptimizer` için de geçerli: nesne script'ten kurulup çalıştırılabiliyor
  (`ProgramsSingleTraderOpt.csx`), ama grid-search algoritmasının kendisi (nested-loop parametre
  taraması) script'ten değiştirilemiyor/hook'lanamıyor.

### Console Menüleri — Hangisinde Script Yeteneği Var

| Menü | Ne yapıyor | Script yeteneği | Not |
|---|---|:---:|---|
| `[1]` Read Data | Veri yükler | — | Scripting'le ilgisiz |
| `[2]` SingleTrader | İnteraktif çalıştırma | — (dolaylı: `[8]`'den erişilebilir) | Kendisi script çalıştırmıyor |
| `[3]` MultipleTrader | İnteraktif çalıştırma | — (dolaylı: `[8]`'den erişilebilir) | Kendisi script çalıştırmıyor |
| `[4]` SingleTraderOptimizer | İnteraktif çalıştırma | — (dolaylı: `[8]`'den erişilebilir) | Kendisi script çalıştırmıyor |
| `[5]-[7]` | Read Data + [2]/[3]/[4] kombinasyonu | — | Aynı, sadece veri yükleme eklenmiş |
| `[8]` **Run Script** | `ScriptExecutor.ExecuteAsync(...)` | ✅ **ASIL SCRIPT GİRİŞ NOKTASI** | `algoTrader` global'i üzerinden SingleTrader/MultipleTrader/SingleTraderOptimizer'a erişebiliyor (ama sadece SingleTrader için hazır kolaylık metodları var) |
| `[9]` DearPyGuiDataPlotter (Test) | Demo/test hook | — | `ScriptExecutor`'la hiç ilgisi yok, geçici test menüsü (silinecek, bkz. yapilacak.md) |
| `[10]-[15]` Strateji Tarama (Symbol/Timeframe/Multi-Strategy Scan) | Kendi içinde tam C# akışı | — | `ScriptExecutor` hiç çağrılmıyor; teorik olarak aynı assembly'de oldukları için elle script yazılabilir ama hiçbir menü/örnek script bunu yapmıyor |
| `[16]-[21]` Sorgu Tarama (Query Symbol/Timeframe/Multi-Query Scan) | Kendi içinde tam C# akışı | — | Aynı — scripting'le hiç bağlantısı yok |

**Kullanıcının tahmini doğru**: `[9]`'dan itibaren (9, 10-15, 16-21) hiçbir menüde script
yeteneği yok — hepsi kendi başına yeten, `ScriptExecutor`'ı hiç çağırmayan düz C# akışları.
Script yeteneği fiilen sadece `[8]`'de var; `[2]/[3]/[4]` (ve `[5]-[7]`) kendileri script
çalıştırmıyor ama ürettikleri `algoTrader` nesnesi `[8]`'den sonra script'e aktarılabiliyor
(aynı oturumda, aynı `algoTrader` referansı).

### Fast-follow fikri: `MultipleTrader` consensus'unu script'ten tanımlanabilir yapmak

Şu an `BuildConsensusSignal()` (`MultipleTrader.cs:191-271`) `ConsensusMode` string'ine göre
hardcoded bir `switch` (Net/Majority/All/Any) — script'in enjekte edebileceği bir "giriş kapısı"
yok. Küçük, kontrollü bir değişiklikle script'lenebilir hale gelir:

1. `MultipleTrader`'a bir delegate property eklenir:
   `public Func<List<SingleTrader>, TradeSignals>? CustomConsensusFunc { get; set; }`
2. `BuildConsensusSignal()`'ın başına: `if (CustomConsensusFunc != null) return CustomConsensusFunc(Traders);`
   — doluysa hardcoded switch'i atlar.
3. Script (`[8]` üzerinden) `algoTrader.MultipleTrader.CustomConsensusFunc = traders => { ... };`
   atayarak child trader'ların (`traders[i].strategySignal`/`SonYon` vb.) sinyallerine bakıp
   kendi kuralını dönebilir.

**İleride bu implement edilmek istenirse kullanılacak prompt**: *"docs/todo.md'deki 'Fast-follow
fikri: MultipleTrader consensus'unu script'ten tanımlanabilir yapmak' bölümünü oku ve uygula —
MultipleTrader'a CustomConsensusFunc adında bir Func<List<SingleTrader>, TradeSignals>? property
ekle, BuildConsensusSignal()'ın başına bu doluysa onu çağırıp hardcoded switch'i atlayan bir
early-return ekle, sonra inputs/scripts/ altına script'ten CustomConsensusFunc atayan küçük bir
örnek .csx yaz ve gerçek veride uçtan uca doğrula (örn. child'lardan biri Al diğeri Sat derken
özel kuralın — mesela 'ilk child'ın dediği olsun' — doğru çalıştığını göster)."*

## Done

- [x] [docs/roadmap.md](roadmap.md) güncellendi — Python entegrasyonu için 3 yaklaşımdan ikisinin (dosya+subprocess: `DearPyGuiDataPlotter`, pythonnet: `PythonPlotter.cs`) fiilen benimsendiği, REST/gRPC'nin kullanılmadığı belgeye yansıtıldı.
