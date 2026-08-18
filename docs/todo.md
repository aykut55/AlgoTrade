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
| 2 | Tek | Tek | Çoklu | ❌ Yok — yeni yapı taşı **A** |
| 3 | Tek | Çoklu (bileşke) | Tek | ✅ TAMAMLANDI — `MultipleTrader` + Net/Majority/All/Any consensus modları (yapı taşı **B**, 2026-08-18) |
| 4 | Tek | Çoklu (bileşke) | Çoklu | ❌ Yok — **A**'nın tamamlanmasını bekliyor |
| 5 | Çoklu | Tek | Tek | ✅ TAMAMLANDI — `SymbolScanner` + Console `[10] Tarama` (yapı taşı **C**, roadmap madde 8, 2026-08-18) |
| 6 | Çoklu | Tek | Çoklu | ❌ Yok — **C**'nin içine **A** sarılması gerekiyor |
| 7 | Çoklu | Çoklu (bileşke) | Tek | ❌ Yok — **C**'nin `MultipleTrader` üzerinde çalışan bir varyantı gerekiyor (şu an sadece `SingleTrader` bazlı) |
| 8 | Çoklu | Çoklu (bileşke) | Çoklu | ❌ Yok — **C** + **A** + MultipleTrader-varyantının bileşimi |

**B ve C tamamlandı** (bkz. [docs/tarama-motoru-plan.md](tarama-motoru-plan.md) — mimari,
kritik bir bug ve düzeltmesi, doğrulama sonuçları dahil). Kalan yapı taşı:

- **A — Çoklu zaman dilimi desteği (resampling GEREKMİYOR)**: Diskte
  (`C:\data\csvFiles\CRP\<tf>\`) her sembol için zaten ayrı ayrı üretilmiş zaman dilimi
  klasörleri var (`01/05/10/15/20/30/60/120/240` dakika + `A/G/H`), aynı sembolün dosyası (örn.
  `BTCUSDT_BNC.csv`) her klasörde ayrı ayrı mevcut — doğrulandı (2026-08-18). Yani A, resampling
  motoru değil, şu basit adımlardan oluşuyor: (1) sembol için N farklı zaman dilimi dosyasını
  `StockDataReader` ile ayrı ayrı oku, (2) seçili stratejiyi her zaman dilimi verisi üzerinde
  bağımsız çalıştır, (3) `MultipleTrader.BuildConsensusSignal()`'a benzer bir "zaman-dilimi
  bileşkesi" ile sonuçları birleştir.

**Sonraki adımlar** (C tamamlandıktan sonra ortaya çıkan, `docs/tarama-motoru-plan.md`'deki
"Kapsam Dışı" listesiyle aynı):
- Yapı taşı A (çoklu zaman dilimi) — senaryo 2/4/6/8'i açar
- `SymbolScanner`'ın `MultipleTrader` üzerinde çalışan bir varyantı — senaryo 7/8'i açar
- Zengin JSON preview ekranı (SingleTrader/MultipleTrader'daki gibi), Time filtering /
  TradeStartBarIndex desteği, buffered flush / partial-resume

Kaynak: [docs/PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md), [docs/migration-guide.md](migration-guide.md)
(madde 2, 4, 8), [docs/tarama-motoru-plan.md](tarama-motoru-plan.md).

## Done

- [x] [docs/roadmap.md](roadmap.md) güncellendi — Python entegrasyonu için 3 yaklaşımdan ikisinin (dosya+subprocess: `DearPyGuiDataPlotter`, pythonnet: `PythonPlotter.cs`) fiilen benimsendiği, REST/gRPC'nin kullanılmadığı belgeye yansıtıldı.
