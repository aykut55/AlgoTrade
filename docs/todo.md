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

## Tarama Matrisi Analizi

Kullanıcının 2026-08-18'de tarif ettiği 8 senaryo (Sembol × Strateji × Zaman Dilimi, her biri
Tek/Çoklu) temiz bir kombinasyon matrisi — gerçek bir tekrar yok, ama 8'i de ayrı ayrı inşa
etmeye gerek yok. Kod tabanına göre bunlar 3 bağımsız yapı taşının bileşimi:

| # | Sembol | Strateji | Zaman Dilimi | Durum |
|---|--------|----------|---------------|-------|
| 1 | Tek | Tek | Tek | ✅ Mevcut — `SingleTrader` |
| 2 | Tek | Tek | Çoklu | ✅ TAMAMLANDI — `TimeframeScanner` + Console `[11] Tarama (Timeframe Scan)` (yapı taşı **A**, 2026-08-18) |
| 3 | Tek | Çoklu (bileşke) | Tek | ✅ TAMAMLANDI — `MultipleTrader` + Net/Majority/All/Any consensus modları (yapı taşı **B**, 2026-08-18) |
| 4 | Tek | Çoklu (bileşke) | Çoklu | ✅ TAMAMLANDI — `MultiStrategyTimeframeScanner` + Console `[12] Tarama (Multi-Strategy Timeframe Scan)` (2026-08-18). Hem bileşke (mainTrader) hem her child'ın bağımsız sinyali (`ChildSignals`) raporlanıyor — bkz. `docs/tarama-motoru-plan.md` "✅ DÜZELTİLDİ" |
| 5 | Çoklu | Tek | Tek | ✅ TAMAMLANDI — `SymbolScanner` + Console `[10] Tarama (Symbol Scan)` (yapı taşı **C**, roadmap madde 8, 2026-08-18) |
| 6 | Çoklu | Tek | Çoklu | ❌ Yok — `SymbolScanner` içinde her sembol için `TimeframeScanner`'ı da çalıştırmak gerekiyor (iç içe iki bağımsız tarama) |
| 7 | Çoklu | Çoklu (bileşke) | Tek | ❌ Yok — **C**'nin `MultipleTrader` üzerinde çalışan bir varyantı gerekiyor (senaryo 4'teki `MultiStrategyTimeframeScanner`'ın "AlgoTrader'ı TF yerine sembol başına taze kurma" mantığı doğrudan uyarlanabilir) |
| 8 | Çoklu | Çoklu (bileşke) | Çoklu | ❌ Yok — 6 ve 7'nin bileşimi |

**"Tek Sembol" sütunu (1/2/3/4) ve senaryo 5 tamamlandı** (bkz.
[docs/tarama-motoru-plan.md](tarama-motoru-plan.md) — mimari, kritik bir bug ve düzeltmesi,
tasarım sapmaları ve doğrulama sonuçları dahil). Kalan: 6, 7, 8 — hepsi "Çoklu Sembol" sütununda.

**Önemli düzeltme (A için)**: İlk analizde zaman dilimi ekseni için de `MultipleTrader`'daki
gibi bir "konsensüs/bileşke" gerektiği varsayılmıştı (sürücü TF + zaman-hizalama). Kullanıcı bu
niyetin hiç olmadığını belirtti — istenen, aynı sembolü seçili zaman dilimlerinde **bağımsız
bağımsız** çalıştırıp sonuçlara ayrı ayrı bakmaktı. "Bileşke" kelimesi sadece strateji ekseni
için kullanılmıştı. Bu yüzden A, `SymbolScanner`'a (C) yapısal olarak neredeyse özdeş bağımsız
bir sınıf (`TimeframeScanner`) olarak kuruldu — konsensüs/zaman-hizalama yok.

**Sonraki adımlar — sıra kesinleşti: 6 → 7 → 8** (kullanıcı git commit sırasının karışmasını
istemediği için orijinal sıra korunuyor; A/B/C/4 tamamlandıktan sonra kalan,
`docs/tarama-motoru-plan.md`'deki "Kapsam Dışı" listeleriyle aynı):
- **Sırada: Senaryo 6** (Çoklu Sembol, Tek Strateji, Çoklu TF) — tasarım taslağı
  `docs/tarama-motoru-plan.md`'nin sonunda ("Senaryo 6 — SIRADA" bölümü), henüz uygulanmadı.
- Senaryo 7 (Çoklu Sembol, Çoklu Strateji-bileşke, Tek TF) — senaryo 4'teki
  `MultiStrategyTimeframeScanner`'ın "AlgoTrader'ı taze taze kurup at" tekniği doğrudan
  uyarlanabilir, döngü değişkeni TF yerine sembol olur.
- Senaryo 8 — 6 ve 7'nin bileşimi.
- Zengin JSON preview ekranı (SingleTrader/MultipleTrader'daki gibi), Time filtering /
  TradeStartBarIndex desteği, buffered flush / partial-resume, otomatik TF keşfi

**Not — bu matris sadece Strateji ekseninde**: 6/7/8 bitince "Sembol × Strateji × Zaman Dilimi"
matrisi tamamlanmış olacak (8/8), ama proje ayrıca `IStrategy` ile birebir aynı desende bağımsız
bir **Sorgu** alt sistemi barındırıyor (`IQuery`/`BaseQuery`/`QueryRegistry`,
`SingleTrader.RunMode = QueryOnly`) — hiçbir tarama sınıfı bunu desteklemiyor. Bu, migration-
guide.md madde 6 (zengin sorgu tipleri) ve madde 9'un (Sorgu + Toplu Sembol Uygulama) birebir
tarif ettiği, **ayrı ve henüz başlanmamış** bir iş.

### Sorgu Tarama Matrisi (Strateji matrisinin Sorgu karşılığı — 2026-08-18)

Aynı 8 senaryo, "Strateji" yerine "Sorgu":

| # | Sembol | Sorgu | Zaman Dilimi | Durum |
|---|--------|-------|---------------|-------|
| 1 | Tek | Tek | Tek | ✅ Mevcut — `SingleTrader.RunMode = QueryOnly` |
| 2 | Tek | Tek | Çoklu | ❌ Yok — `TimeframeScanner`'ın QueryOnly-varyantı gerekiyor |
| 3 | Tek | Çoklu | Tek | ❌ Yok — N sorguyu aynı sembol/TF'de çalıştırıp N ayrı sonuç kolonu raporlamak (bkz. aşağıdaki karar) |
| 4 | Tek | Çoklu | Çoklu | ❌ Yok — 2 ve 3'ün bileşimi |
| 5 | Çoklu | Tek | Tek | ❌ Yok — `SymbolScanner`'ın QueryOnly-varyantı, **madde 9'un birebir istediği şey** |
| 6 | Çoklu | Tek | Çoklu | ❌ Yok — 2 ve 5'in bileşimi |
| 7 | Çoklu | Çoklu | Tek | ❌ Yok — 3'ün çoklu sembol hali |
| 8 | Çoklu | Çoklu | Çoklu | ❌ Yok — hepsinin bileşimi |

**Karar (kullanıcı ile netleşti, 2026-08-18)**: "Çoklu Sorgu" (3/4/7/8) **hiçbir zaman
birleştirilmiyor** — Strateji'deki "bileşke" (MultipleTrader, tek bir Al/Sat kararı) kavramının
Sorgu karşılığı yok. N sorgu çalıştırılır, N sonuç **ayrı ayrı** raporlanır (ayrı kolonlar/
satırlar), kullanıcı kendisi yorumlar. Bu, mimariyi ciddi şekilde basitleştiriyor: Strateji
tarafındaki gibi yeni bir "MultipleQuery" consensus sınıfına gerek yok — mevcut tarama
sınıflarının (`SymbolScanner`/`TimeframeScanner` vb.) her hücresinde tek sorgu yerine bir
**sorgu listesi** çalıştırıp sonuçları ek kolonlar olarak eklemesi yeterli. Detay:
`docs/tarama-motoru-plan.md`'nin sonunda "Sorgu Tarama Matrisi" bölümü.

Kaynak: [docs/PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md), [docs/migration-guide.md](migration-guide.md)
(madde 2, 4, 8), [docs/tarama-motoru-plan.md](tarama-motoru-plan.md).

## Done

- [x] [docs/roadmap.md](roadmap.md) güncellendi — Python entegrasyonu için 3 yaklaşımdan ikisinin (dosya+subprocess: `DearPyGuiDataPlotter`, pythonnet: `PythonPlotter.cs`) fiilen benimsendiği, REST/gRPC'nin kullanılmadığı belgeye yansıtıldı.
