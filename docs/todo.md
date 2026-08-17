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

## Done

- [x] [docs/roadmap.md](roadmap.md) güncellendi — Python entegrasyonu için 3 yaklaşımdan ikisinin (dosya+subprocess: `DearPyGuiDataPlotter`, pythonnet: `PythonPlotter.cs`) fiilen benimsendiği, REST/gRPC'nin kullanılmadığı belgeye yansıtıldı.
