# TODO

## Todo

- [ ] **[7] SingleTraderOptimizer ↔ `03_RunSingleTraderOptWithProgressAsync.csx` senkronize
  edilecek** (2026-08-24, bkz. [docs/manual/07-menu-vs-script-parity.md](manual/07-menu-vs-script-parity.md)
  §3 — [5]/01 ve [6]/02 için aynı iş zaten yapıldı). İki bilinen açık madde:
  1. **🔴 Kritik**: script `algoTrader.SetSingleTraderOptSignalsConfig(...)` çağırmıyor →
     `SingleTraderOptimizer.ApplyConfigsToTrader()` içinde her test trader `ConfigureUserFlagsOnce()`
     ile tüm AL/SAT sinyallerini `false`'a resetliyor ve bir daha `true` yapılmıyor →
     optimizasyon muhtemelen hiçbir kombinasyonda işlem açmıyor (§2'de MultipleTrader script'inde
     bulunup düzeltilen hatayla birebir aynı kalıp — orada gerçekten sıfır işlem olduğu doğrulanmıştı).
  2. **🟡**: `SetSingleTraderOptLogConfig`/`SetSingleTraderOptSortOutputConfig` çağrılmadığı için
     CSV/TXT optimizasyon log + sıralı sonuç dosyaları hiç yazılmıyor, script sadece konsola
     en iyi sonucu basıyor.
  Ayrıca §1/§2'de yapılan ReadData filtreleme senkronizasyonunun aynısı buraya da taşınabilir.

- [x] ~~venv'ler merkezileştirilecek~~ — **YAPILMIŞ (doğrulandı 2026-08-25)**: `inputs/python/.venv`,
  `src/DearPyGuiDataPlotter/.venv`, `src/DearImGuiBundleDataPlotter/.venv` artık yok, sadece
  proje kökünde tek `D:\SageProjects\AlgoTrade\.venv` var (`AppSettings.VenvDir`). Bu satır
  stale kalmıştı, düzeltildi.

- [ ] **Kalıntı "çift ROOT" yapısı (2026-08-25 tespit edildi, bkz. altta madde 23-25'in geçmişi)**:
  `src/DearPyGuiDataPlotter` bağımsız bir projeden kopyalandığı için, o projede kendi ROOT'u olan
  `inputs/` klasörü hâlâ AYNEN duruyor (`src/DearPyGuiDataPlotter/inputs/input.json`,
  `latest_bundle.npz`, `latest_bundle.view.json`) — AlgoTrade'in KENDİ kök `inputs/`'undan
  (`D:\SageProjects\AlgoTrade\inputs\`, `python/`/`scripts/`/`configs/` içeren) TAMAMEN AYRI ve
  paralel bir yapı. Somut asimetri: eski tip plotter'ın yeni Python kodu (`bundle_loader.py`,
  2026-08-25'te "Geçmiş (Offline)... Hızlı Sinyal Plot'u" işi sırasında eklendi) AlgoTrade'in kendi
  `inputs/python/`'una gitti, ama okuduğu/yazdığı bundle dosyaları hâlâ
  `src/DearPyGuiDataPlotter/inputs/`'ta — iki farklı "inputs" kökü bir arada kullanılıyor.
  Path çözümleme (`ROOT_DIR`, bkz. `src/DearPyGuiDataPlotter/docs/InputConfig.md`) zaten AlgoTrade'in
  DIŞ köküne (`AlgoTrade.sln`'in olduğu yer) göre çözülüyor — yani mantıksal olarak tek ROOT kabul
  ediliyor, sadece fiziksel dosya konumu hâlâ eski (nested) klasörde kalmış. Aşağıdaki madde 29
  (`inputs/python/`'un `src/` altına taşınması) ile birlikte ele alınmalı — muhtemelen ikisi de aynı
  büyük "proje yapısını sadeleştirme" refactor'ünün parçası.

  **Kullanıcı denemesi (2026-08-26) → karar verildi, implement edildi**: `inputs/python/
  dearImGuiBundleDataPlotter/`, `inputs/python/dearPyGuiDataPlotter/`, `inputs/python/
  pythonPlotter/` klasörleri her plotter'ın **kendi AlgoTrade-native runtime bundle klasörü**
  olacak (Python kaynak kodu taşınmıyor — sadece runtime çıktısı; `dearImGuiBundleDataPlotter/`
  şimdilik boş, o plotter henüz implement edilmedi). Netleşen kararlar:
  - **Ek 3. kopya** (önceki maddedeki "yerine mi geçsin" sorusunun cevabı) — `src/DearPyGuiDataPlotter/
    inputs/` (bağımsız projeden kalma "normal" konum, DOKUNULMADI) hâlâ yazılıyor, `outputs/logs/`
    (görünürlük) hâlâ yazılıyor, bunlara EK olarak artık `inputs/python/dearPyGuiDataPlotter/` ve
    `inputs/python/pythonPlotter/`'a da yazılıyor — her run'da toplam 4 konum.
  - **Ayrı fiziksel kopyalar** — `dearPyGuiDataPlotter/` ve `pythonPlotter/` aynı içeriğin iki ayrı
    kopyasını tutuyor (sembolik link/paylaşım YOK), her plotter kendi klasöründen bağımsız okuyor.
  - `AppSettings.cs`'e `DearPyGuiPlotterBundleDir`/`PythonPlotterBundleDir` eklendi
    (`inputs/python/{dearPyGuiDataPlotter,pythonPlotter}`).
  - `01_RunSingleTraderWithProgressAsync.csx`, `02_RunMultipleTraderWithProgressAsync.csx`,
    `Program.cs` (Menü [5]/[6]) — hepsi bu 2 yeni konuma da `fileBaseName: "latest_bundle"` ile
    yazacak şekilde güncellendi (4 script/menü noktası, hepsi paralel).
  - `TestOldPlotterFromBundle.csx` artık `AppSettings.PythonPlotterBundleDir`'den okuyor (eskiden
    `src/DearPyGuiDataPlotter/inputs/`'tan okuyordu).
  - Dış-proje senkron riski (madde 27) hâlâ geçerli ama SADECE runtime veri kopyası olduğu için
    (kaynak kod değil) etkisi sınırlı — dış projenin kendi `inputs/` klasörüyle bir çakışma yok.

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

- [ ] **MultipleTrader → DearPyGuiDataPlotter (yeni plotter) veri aktarımı hiç yapılmıyor** (2026-08-21
  tespit edildi). `AlgoTrader.PlotMultipleTraderData()` (`AlgoTrader.cs:3031-3039`) sadece eski
  `_pythonPlotter.PlotMultipleTraderData(multipleTrader)` (pythonnet tabanlı, §5.7 mekanizma 1) çağırıyor.
  Yeni plotter tarafındaki `TradeDataBundleConverter`/`DearPyGuiDataPlotter` test hook'u
  (`Program.cs:803-823`) sadece **SingleTrader** verisini bundle'a çeviriyor — MultipleTrader
  (mainTrader + child'lar) için eşdeğer bir `.npz` bundle üretimi/`load_bundle` çağrısı yok.
  Yani MultipleTrader çalıştırıldığında sadece ilk (eski) plotter'da veri görünüyor, ikinci
  (yeni, DearPyGuiDataPlotter) plotter'a hiç veri çizdirilmiyor. Yukarıdaki madde 1'deki
  `PlotBackend` switch'i implement edilirken bu boşluk da kapatılmalı — muhtemelen
  `TradeDataBundleConverter`'a bir `ConvertMultipleTrader(...)` overload'ı (mainTrader + her
  child için ayrı panel/bundle) eklenmesi gerekecek.

## Strateji Karşılaştırma — Getiri Eğrisi Görselleştirme (migration-guide.md Madde 10, 2026-08-21)

`MultipleTrader` + yeni `WriteMultipleTraderStatistics()` (bkz. "Done" bölümü) sayesinde
migration-guide.md Madde 10'un ("Farklı Stratejilerin Aynı Sembol İçin Karşılaştırması") sayısal
özet kısmı dolaylı yoldan zaten karşılanıyor — `_strategyConfigs`'e farklı stratejiler (örn.
SimpleRSIStrategy + SimpleMACDStrategy) tanımlanırsa her biri aynı sembol/veri üzerinde bağımsız
childTrader olarak standalone sonuç üretiyor ve `MultipleTraderStatistics.txt/.csv` bunları
satır=strateji / kolon=metrik (NetProfit/WinRate/ProfitFactor/MaxDD) şeklinde karşılaştırıyor.

Gerçekten eksik kalan iki nokta:

- [ ] **Getiri eğrisi (equity curve) görselleştirme**: Her stratejinin (childTrader'ın) bar-bar
  bakiye/getiri verisi kendi liste dosyasında zaten var (`trader.lists` → `WriteMultipleTraderListsToFiles`),
  ama bunları TEK bir grafikte üst üste bindirip görsel karşılaştırma yapan bir mekanizma yok.
  Muhtemel yaklaşım: `TradeDataBundleConverter`'a (bkz. yukarıdaki madde, DearPyGuiDataPlotter
  entegrasyonu) her childTrader'ın equity curve'ünü ayrı bir seri olarak ekleyen bir
  `ConvertMultipleTraderComparison(...)` fonksiyonu — N strateji = N çizgi, tek panelde.

- [ ] **Dedike/hafif bir "strateji karşılaştır" menüsü yok**: Şu an bu işlevi kullanmak için
  kullanıcının tam `MultipleTrader` kurulumunu (consensus mode dahil, sonucu umursanmasa bile
  `ConsensusMode`/`mainTrader` config'i gerekiyor) yapması lazım. Madde 10'un hayal ettiği
  "N strateji seç, aynı sembolde karşılaştır" akışı için Console'a ayrı, daha basit bir menü
  seçeneği (consensus/mainTrader kavramını gizleyen, sadece childTrader sonuçlarını
  raporlayan) eklenebilir — Scanner ailesindeki (`[10]-[21]`) desene benzer, tek sembol +
  çoklu strateji varyantı.

### Yeni Özellik İhtiyacı: GEÇMİŞ (offline) Çalıştırmaların Karşılaştırılması (2026-08-21, kullanıcı talebi)

Yukarıdaki `MultipleTrader` tabanlı çözüm **canlı/tek oturumda birlikte çalışan** stratejileri
karşılaştırıyor — hepsi aynı anda, aynı `MultipleTrader` içinde child olarak koşuyor olmalı.
Kullanıcının gerçek kullanım şekli farklı: örn. son 3 gün içinde **aynı sembol** için 20 farklı
strateji/parametre ile **ayrı ayrı SingleTrader çalıştırmaları** yapılmış, her biri kaydedilmiş
(muhtemelen sonuç dosyaları elle farklı isimlerle taşınmış/yeniden adlandırılmış — çünkü düz
`SingleTraderConfig`'te `MultipleTraderConfig`/`ConfirmingSingleTraderConfig`'teki gibi otomatik
bir `FilePrefix` alanı YOK, `SingleTraderStatistics.csv` her koşuda üzerine yazılıyor). Şimdi bu
**20 ayrı, geçmişte kaydedilmiş** sonuç dosyasını sonradan (post-hoc) tek bir karşılaştırma
tablosunda görmek istiyor — hiçbirini yeniden çalıştırmadan.

**İki senaryo birbirini tamamlıyor, ikisi de gerekli:**
1. Canlı: tek `MultipleTrader` run'ı içindeki child stratejileri karşılaştır (yukarıdaki madde,
   `WriteMultipleTraderStatistics()` ile büyük ölçüde tamam).
2. **Offline (bu madde, YENİ, henüz yazılmadı):** diskte zaten var olan N adet ayrı
   `SingleTraderStatistics.csv` (veya `SingleTraderPerformans.csv`) dosyasını okuyup tek bir
   konsolide karşılaştırma tablosu üreten bağımsız bir araç/menü.

**Taslak yaklaşım:**
- Yeni bir sınıf (örn. `StatisticsComparisonTool` veya `SavedRunsComparer`) — bir klasör yolu
  (veya dosya listesi/glob pattern) alır, içindeki her `*_SingleTraderStatistics.csv` dosyasını
  okur (zaten CSV formatında tek satırlık özet — `GetOptimizationSummary()` çıktısıyla aynı
  kolon seti), satırları birleştirip `MultipleTraderStatistics.csv` ile aynı desende
  (satır=dosya/run, kolon=metrik) tek bir çıktı üretir.
- **Ön koşul/bağımlılık:** Bu işin pratik olması için önce standalone `SingleTraderConfig`'e de
  `MultipleTraderConfig`'teki gibi bir `FilePrefix` alanı eklenmeli (her run'ın kendine özgü,
  otomatik isimlendirilmiş bir çıktı dosyası bırakması için) — aksi halde kullanıcı hâlâ elle
  dosya adlandırıp taşımak zorunda kalıyor, araç sadece "zaten doğru adlandırılmış dosyaları"
  toplayabilir.
- Console'a yeni bir menü seçeneği: bir klasör/pattern seçtir → eşleşen dosyaları listele →
  konsolide rapor üret.

## Yeni Özellik Fikri: Geçmiş (Offline) Trader Verilerinden Hızlı Sinyal Plot'u (2026-08-25, kullanıcı talebi — 2026-08-25 revize edildi, bkz. altta "Revize plan")

**Yukarıdaki "GEÇMİŞ (offline) Çalıştırmaların Karşılaştırılması" maddesinden farkı**: o madde
**sayısal özet** (tek satırlık `SingleTraderStatistics.csv`) karşılaştırmasıyla ilgili; bu madde
**bar-bar tam görsel replay** — önceden üretilmiş 1 veya daha fazla trader'ın sinyal/PnL/getiri
verisini, trader'ı **hiç yeniden çalıştırmadan** hem eski tip (pythonnet/imgui_bundle) hem yeni
tip (DearPyGuiDataPlotter) plotter'da çizdirmek.

**Somut kullanım senaryosu (netleştirildi, 2026-08-25)**: kullanıcı bunu, **farklı zamanlarda ve
farklı stratejilerle bağımsız çalıştırılmış N adet `SingleTrader` run'ının** verilerini **toplu**
çizdirmek için kullanacak. Gerçek bir `MultipleTrader` koşumuna gerek yok — zaten
`MultipleTrader.GetMainTrader()` de bir `SingleTrader`, yani N bağımsız run "main/child"
hiyerarşisi olmadan eşit N girdi olarak overlay edilecek.

### Mevcut durum tespiti (araştırıldı, 2026-08-25)

- Bir trader run'ı bittiğinde `Lists.csv`/`.txt` (insan-odaklı, `StatisticsExporterConfig.json`
  versiyonuna göre değişen kolon seti) yazılıyor ama OHLC içermiyor ve kod tabanında onu geri okuyan
  bir reader yok.
- **Ayrıca**, yeni tip plotter'ın (`DearPyGuiDataPlotter`) her plot öncesi ürettiği
  `.npz`/`.view.json` bundle'ı (`TradeDataBundleConverter`) zaten OHLC + sinyal + PnL/Return +
  strateji indikatörlerini TAM ve export-config'den BAĞIMSIZ olarak içeriyor.
- **Yeni bulgu (2026-08-25)**: eski tip plotter (pythonnet) da in-process çalıştığı için (`Py.GIL()`
  içinde) bu `.npz`'yi `numpy.load(...)` ile okuyup aynı `trade_data.TradeData()` PyDict'ini
  doldurup mevcut `CallPlotDataImgBundleNew(tradeData)`'yı **hiç değiştirmeden** çağırabilir.
  Yani `.npz` bundle, HER İKİ plotter için de ortak "ham dump" formatı olarak kullanılabilir.

### Revize plan (2026-08-25): Option C — yeni format icat etme, `.npz` bundle'ı reuse et

Aşağıdaki eski "Option A / Option B" tartışması (Lists için yeni bir Save/Load formatı icat etmek
ya da Lists.csv'yi kırılgan şekilde geri okumak) **artık gündemde değil** — `.npz` bundle zaten
tam-sadakatli bir dump, üstüne bir de OHLC içeriyor (Option A'nın "OHLC'yi CSV'den ayrıca oku"
adımına bile gerek kalmıyor). Kapatılması gereken küçük boşluklar:

1. ~~Bundle writer'da eksik 3 seri~~ — **YAPILDI (2026-08-25)**: `TradeDataBundleConverter.
   ConvertCore`'a `AddSeries("Balance", lists.BakiyeFiyatList)` / `AddSeries("Commission",
   lists.KomisyonFiyatList)` / `AddSeries("Net Balance", lists.BakiyeFiyatNetList)` eklendi.
   `PythonPlotter.ExtractBundleData`'daki switch ve `inputs/python/bundle_loader.py`'deki
   `_KNOWN_SERIES` bu 3 ismi `bakiye_fiyat_list`/`komisyon_fiyat_list`/`bakiye_fiyat_net_list`'e
   eşleyecek şekilde güncellendi (her iki okuma yolunda da — memory/`NpzReader` ve Python/
   `numpy.load`). Henüz gerçek bir run ile ([5] → `TestOldPlotterFromBundle.csx`) uçtan uca
   doğrulanmadı, sadece derleme+kod incelemesiyle doğrulandı.
2. ~~`meta_json`'da `SymbolPeriod` yok~~ — **YAPILDI (2026-08-25)**: `meta["periyot"] =
   trader.SymbolPeriod ?? "1H"` eklendi (`ExtractBundleData`/`bundle_loader.py` zaten bu alanı
   okuyordu, sadece yazan taraf eksikti).
3. **`fileBaseName` hep `"latest_bundle"`** — her run bir öncekini siliyor. Kalıcı depolama için
   per-run benzersiz konum gerekiyor (bkz. aşağıdaki depolama planı). **Henüz yapılmadı.**

### Depolama: klasör-bazlı, sabit dosya adı

Her run kendi klasörüne yazar (örn. `runs/2026-08-20_MOST_BTCUSDT-60/bundle.npz` +
`bundle.view.json`), klasör içindeki dosya adı hep aynı kalır. Neden: klasör adı zaten doğal bir
run-kimliği oluyor, dosya adı çakışması hiç sorun olmuyor (her klasör kendi namespace'i), ve run'a
ait diğer çıktılar (`Lists.csv`, `Statistics.txt`) muhtemelen zaten aynı klasörde birlikte duruyor.

### Playlist/index formatı — "hangi sinyal hangi plota" sorusunun cevabı

Klasör/dosya isminden OTOMATİK çıkarmaya çalışmak kırılgan (isimlendirme kuralı değişirse kırılır).
Bunun yerine bir playlist JSON'da her girdiye **açık `label` (+ `color`)** verilir:

```json
{
  "entries": [
    { "bundle": "runs/2026-08-20_MOST_BTCUSDT/bundle.npz",  "label": "MOST 2026-08-20",  "color": [51,204,255,255] },
    { "bundle": "runs/2026-08-21_EXMOV_BTCUSDT/bundle.npz", "label": "EXMOV 2026-08-21", "color": [255,204,0,255] }
  ]
}
```

Girdilerde `view.json` yok — her kaynağın kendi panel yerleşimi değil, sadece verisi (OHLC/signal/
PnL/indikatör) kullanılıyor; birleştirilmiş çıktının view'ı yeniden inşa edilecek.

### Pipeline (taslak — adım 1 implement edildi ve doğrulandı, gerisi henüz değil)

1. ~~`NpzReader` (C#) yazılmalı~~ — **YAPILDI ve DOĞRULANDI (2026-08-25)**:
   `src/AlgoTrade.Core/Python/DearPyGuiDataPlotter/NpzReader.cs` yazıldı (`NpzWriter`'ın format
   simetriği). Ayrıca eski tip plotter'a (`PythonPlotter.cs`) üç yeni metod eklendi:
   - `PlotBundleFile(bundlePath, viewPath)` — memory/`NpzReader` yolu (aktif kullanılan).
   - `PlotBundleFileFromDisk(bundlePath, viewPath)` — Python/`numpy.load` yolu (`inputs/python/
     bundle_loader.py`, `default.py:stage2LoadPreparedData` ile kısmen örtüşüyor ama farklı hedef
     objeye (`TradeData` vs `PreparedData`) map ettiği ve farklı sys.path'te olduğu için ayrı
     tutuldu — bilinçli, küçük bir kod tekrarı).
   - `SaveBundleToDisk(SingleTrader/MultipleTrader, outputDir, fileBaseName)` — `TradeDataBundleConverter`'a
     ince sarmalayıcı.
   **Uçtan uca doğrulandı**: `01_RunSingleTraderWithProgressAsync.csx`'in ([5]) ürettiği gerçek
   `latest_bundle.npz`/`.view.json`, yeni test script'i (`inputs/scripts/TestOldPlotterFromBundle.csx`,
   [8] Run Script ile) üzerinden eski tip plotter'a verildi — pencere açıldı, grafik doğru
   göründü. Yani `.npz` bundle'ın gerçekten plotter-agnostic olduğu (Option C'nin temel önermesi)
   kanıtlandı. `PlotBundleFileFromDisk` (Python/numpy.load alternatifi) henüz eski plotter
   üzerinden GUI'de test edilmedi (sadece `python -c` ile izole smoke test edildi, bkz. commit
   öncesi konuşma) — istenirse ayrı bir test script'iyle doğrulanabilir.
2. Playlist okunur, her entry `NpzReader` ile geri okunur (open/high/low/close/volume/timestamps/
   signal_steps/indicator_names+values).
3. **Yeni tip plotter için**: N entry tek bir `combined.npz` + `combined.view.json`'a birleştirilir
   — OHLC referans entry'den, her entry'nin sinyal/PnL serisi `"{label} Signal"` gibi isimlerle
   indikatör matrisine eklenir (`ConvertMultipleTrader`'ın child overlay eklediği yöntemin aynısı,
   ama kaynak canlı `SingleTrader` değil, diskten okunan npz).
4. **Eski tip plotter için**: her entry'den bir `tradeData` PyDict kurulup `PlotMultipleTraderData`'nın
   beklediği PyList'e eklenir (önceki inceleme: o fonksiyon sadece OHLC+Lists alanlarını kullanıyor,
   gerçek `MultipleTrader` nesnesine ihtiyacı yok).
5. Yeni script(ler): playlist dosyası verilip hem eski hem yeni tip plotter'da çizdiren script(ler)
   — `04`/`05` script çiftinin doğal devamı, farkı: `04` trader'ı GERÇEKTEN çalıştırıp bundle
   üretiyor, bu yeni script hiç çalıştırmadan geçmiş bundle'ları okuyup birleştiriyor.

### Bu revizyonun elediği eski tasarım yükü

- Eski "Option A" (Lists için yeni Save/Load formatı) ve "Option B" (Lists.csv'yi kırılgan şekilde
  geri okuma) artık gündemde değil.
- "Sentetik `SingleTrader` kabuğu kur" hack'ine gerek yok — her iki plotter da doğrudan npz/tradeData
  PyDict üzerinden besleniyor.

### Açık kalan tasarım soruları (implementasyondan önce netleşmeli)

- Combined bundle'da OHLC hangi entry'den referans alınacak — farklı sembol/timeframe/tarih
  aralığındaki run'lar overlay edilebilir mi, yoksa hep aynı sembol/timeframe varsayımı mı
  yapılacak?
- Playlist dosyasının konumu/adı ve nasıl üretileceği — elle mi yazılacak, yoksa bir klasörü
  tarayıp otomatik playlist üreten bir script/menü mü olacak?
- Ayrı bir console menü numarası mı (script-only mu kalsın) — muhtemelen script-only yeterli,
  bu bir geliştirici/analiz aracı, üretim akışının parçası değil.

**Durum: implementasyona başlandı (2026-08-25) — pipeline adım 1 (NpzReader + eski plotter'ın
bundle'dan çizim yapabilmesi) yapıldı ve gerçek veriyle doğrulandı, adım 2-5 (playlist/merge/N-run
overlay) henüz yapılmadı.**

## Çok Uzun Vadeli Fikir: DearPyGuiDataPlotter'ı da In-Process (pythonnet) Çalıştırmak — Plotter Parity (2026-08-25, sadece not)

> **ÇOK ÇOK İLERİDE yapılacak bir özellik — şu an sadece bir fikir/not, implementasyon YOK,
> öncelik verilmiyor.** Amaç yalnızca iki plotter'ın davranış ve özelliklerini birbirine
> yaklaştırmak (parity); acil bir ihtiyaçtan doğmuyor.

Eski tip plotter (`PythonPlotter.cs`) pythonnet ile **in-process** çalışıyor — `Py.GIL()` içinde
aynı .NET process'inin belleğinde bir Python yorumlayıcısı açılıyor, `imgui_bundle` doğrudan
oradan çağrılıyor. Yeni tip plotter (`DearPyGuiDataPlotter.cs`) ise bilinçli olarak **ayrı bir OS
process'i** olarak çalışıyor (`Process.Start`, proje kökündeki ortak `.venv`'i kullanarak), veri
aktarımı dosya tabanlı (`.npz`/`.view.json` bundle + `inputs/runtime_commands/` altında JSON
komutlar) — bkz. `DearPyGuiDataPlotter.cs` sınıf yorumu.

**Fikir**: yeni tip plotter'ı da (mümkünse) pythonnet ile in-process çalışacak şekilde uyarlamak —
eski tip ile aynı `Py.GIL()`/`Py.Import(...)` modeline geçmek, ayrı process + dosya tabanlı komut
mekanizmasını (en azından opsiyonel bir mod olarak) ortadan kaldırmak.

**Neden şimdi değil / dikkat edilmesi gerekenler (araştırılmadı, sadece ön-not)**:
- Yeni tip plotter'ın ayrı process olması **kasıtlı bir tasarım kararı** gibi duruyor (class
  yorumunda "pythonnet ile in-process çalışmaz" diye özellikle belirtilmiş) — muhtemelen DearPyGui
  ile imgui_bundle'ın aynı process'te birlikte yaşayamaması (ikisi de kendi render/event loop'unu
  istiyor) ya da bağımsız pencere/çökme izolasyonu gibi bir sebebi var; bu sebep doğrulanmadan
  in-process'e geçmek riskli.
- Bugün eklenen `blockUntilClosed`/`WaitForExit()` mekanizması (bkz. yukarıdaki "Geçmiş
  (Offline)... Hızlı Sinyal Plot'u" maddesi) process-ayrılığını varsayarak tasarlandı — in-process'e
  geçilirse muhtemelen gereksizleşir (eski tip zaten Python çağrısı doğal olarak senkron bloklar).
- Kapsamı büyük: pythonnet entegrasyonu, GIL yönetimi, DearPyGui'nin pythonnet altında davranışı,
  process-izolasyonunun kaybı (bir DearPyGui çökmesi artık tüm .NET process'ini düşürebilir) gibi
  konular ayrıca araştırılmalı.

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

## Getiri Eğrisi / KarZarar Eğrisi Konfirmasyonu (Madde 3) — `ConfirmingSingleTrader` (2026-08-19)

**Durum**: ✅ **`ConfirmingSingleTrader` VE `ConfirmingMultipleTrader` implement edildi ve console
menüsüne eklendi** (`[22]`-`[25]`, `AlgoTrade.Console/Program.cs`), ikisi de tam config-driven
(`AppConfig.json` → `ConfirmingSingleTrader`/`ConfirmingMultipleTrader` bölümleri,
`AppConfigApplier.ApplyConfirmingSingleTrader`/`ApplyConfirmingMultipleTrader`,
`AlgoTrader.RunConfirmingSingleTraderWithProgressAsync`/`RunConfirmingMultipleTraderWithProgressAsync`).
Gerçek veride (BTCUSDT_BNC, 30.000 bar, `SimpleMostStrategy`) uçtan uca doğrulandı — bkz. aşağıdaki
"Implementasyon Notları" bölümü. `ConfirmingMultipleTrader`/`ConfirmingSingleTraderOptimizer`/
`ConfirmingMultipleTraderOptimizer` ve Confirming tarama (scanner) varyantları henüz yapılmadı
(bkz. "Fast-Follow: Tarama (Scanner) Versiyonları" ve "Optimizer Varyantları" bölümleri aşağıda —
hâlâ geçerli, sıradaki adımlar).

Aşağıdaki tasarım tartışması (2026-08-18, kullanıcı ile birlikte) hâlâ implementasyonun temelini
anlatan geçerli bir kayıt — kod bu tartışmadaki kararları birebir uyguluyor.

**Kullanıcı sorusu üzerine** (`migration-guide.md` Madde 3, "hiç mi yapılmamış?") netleşti:
implementasyon başlamadan önce kod tabanında **gerçekten hiç iz yoktu**
(`ConfirmationMode`/`sanal`/`VirtualTrade` aramaları tamamen boş dönüyordu).

### Ne İşe Yarıyor (mevcut `ApplyEquityCurveFilter`'dan farkı)

Mevcut `ApplyEquityCurveFilter` (`SingleTrader.cs:2271-2360`) **tek aşamalı** — trader'ın **zaten
gerçekten açık olan** pozisyonunun canlı P&L'ine bakıp yeni giriş sinyallerini (Al/Sat) bastırıyor/
izin veriyor. İstenen özellik **iki aşamalı**: strateji "AL" dediğinde önce gerçek emir açılmıyor,
önce **sanal (virtual/paper)** bir pozisyon açılıp izleniyor; sanal pozisyon belirli bir eşiğe
(kâr veya zarar) ulaşınca *o zaman* gerçek emir açılıyor. Ayrı bir sanal P&L state'i ve "gerçeğe
terfi ettirme" mantığı gerekiyor — `KarZarar.cs` şu an sadece gerçek pozisyonu (`Signals.SonYon`/
`SonFiyat`) takip ediyor, sanal moda hiç destek yok.

### Eski Projedeki Emsal (`AlgoTradeWithOptimizationSupport`) — İncelendi, 2026-08-18

**Referans kopyalar**: Eski proje diğer bilgisayarda mevcut olmayabileceği için, tek çalışan
implementasyonun (`ConfirmingSingleTrader.cs`) tam kaynak kodu ve orijinal tasarım planı
(`ConfirmationMode_Implementation_Plan.md`) `docs/reference/old-project-confirming/` altına
kopyalandı (2026-08-18) — bkz. o klasördeki `README.md`.

Kullanıcının isteğiyle eski proje (`D:\Aykut\Projects\AlgoTradeWithOptimizationSupport\
AlgoTradeWithOptimizationSupportWinFormsApp`) incelendi — WinForms'ta soldan sağa 3 sekme var:
"ConfirmingSingleTrader", "ConfirmingSingleTrader2", "ConfirmingMultipleTrader". **Önemli bulgu:
üçü de aynı işi yapmıyor, hatta ikisi hiç çalışmıyor:**

| Sekme | Çalıştırdığı backend | Konfirmasyon mantığı çalışıyor mu? |
|---|---|:---:|
| ConfirmingSingleTrader (1.) | düz `SingleTrader` | ❌ **ÖLÜ KOD** — mantık yazılmış ama çağrı noktası yorum satırı içinde bırakılmış |
| ConfirmingSingleTrader2 (2.) | `ConfirmingSingleTrader` sınıfı | ✅ **TEK ÇALIŞAN implementasyon** |
| ConfirmingMultipleTrader (3.) | düz `MultipleTrader` | ❌ **ÖLÜ KOD** — `buildConsensusSignal()`'da eşik/konfirmasyon mantığı hiç yok, düz oy çoğunluğu |

**Tek çalışan mekanizma (`ConfirmingSingleTrader.cs`, `buildConsensusSignal()`) nasıl işliyor**:
`MultipleTrader`'a yapısal olarak çok benzer (child `SingleTrader` listesi + ayrı bir `_mainTrader`)
ama farkı: her child'ın **kendi gerçek/tam simüle edilmiş pozisyonunun** canlı P&L'ine
(`trader.status.KarZararFiyat(Yuzde)`) bakıp, eşik geçilene kadar o child'ın sinyalini **Flat'e
zorluyor** (bastırıyor, tersine çevirmiyor). Eşik geçilince (`_traderConfirmed[id]=true`) child'ın
**olduğu gibi, değiştirilmeden** Al/Sat sinyali konsensüs oyuna dahil oluyor — onaylanmış durum,
yön değişene kadar kalıcı. `_mainTrader` bu konsensüsü gerçek hesaba uygulayan tek trader.

**En kritik bulgu — "zarar konfirmasyonu" belirsizliği çözüldü**: Kodda kâr ve zarar
konfirmasyonu **mekanik olarak birebir aynı** — ikisi de sadece "kapıyı aç, child'ın zaten
verdiği kararı olduğu gibi geçir" yapıyor. **Hiçbir ters yön (kontrarian) veya "stop-and-retry"
mantığı yok** — `ConfirmationTrigger.cs`'deki yorum satırları ("ZararOnly = reversal beklentisi")
sadece **hedef/niyet notu**, hiç implement edilmemiş. Yani eski projede "zarar konfirmasyonu"
aslında "kâr konfirmasyonu"yla aynı davranışı üretiyor, sadece tetikleyen eşiğin işareti farklı.
Yeni tasarımda bunu ya aynen taşımalıyız (basit ama az anlamlı) ya da gerçekten farklı bir
davranış (örn. ters yön girişi) tanımlamalıyız — **hâlâ kullanıcıyla netleşmesi gereken bir karar**.

**Diğer bulgular**:
- İki farklı eşik işaret konvansiyonu aynı eski projede bile tutarsız: `ConfirmingSingleTrader`da
  `ZararEsigi` negatif (örn. `-3000`, `<=` ile karşılaştırılıyor); ölü kod (`SingleTrader`
  içindeki) `ZararKonfirmasyonEsigi` pozitif (örn. `5.0`, `<= -esik` ile karşılaştırılıyor). Yeni
  tasarımda tek bir konvansiyon net seçilmeli.
- `ConfirmingSingleTrader2` sekmesindeki "Confirmation Mode Enabled" checkbox'ı bile no-op —
  `ConfirmingSingleTrader` sınıfının "kapalı" durumu hiç yok, konfirmasyon her zaman aktif.
- Konfirmasyon, `SingleTrader.Run()`'ın İÇİNDE değil (ölü koddaki yaklaşım), child'ların
  `Run(i)`'ı bittikten SONRA, `_mainTrader`'a sinyal verilmeden ÖNCE, ayrı bir `buildConsensusSignal()`
  adımında oluyor — yani "wrapper/orchestrator" seviyesinde, tek bir trader'ın kendi içinde değil.
  Bu, kullanıcının "AlgoTrader'dan bağımsız ayrı sınıf" önerisiyle de örtüşüyor.
- **Ders çıkarılacak nokta**: eski projede 3 sekmeden 2'si UI'da eşik alanları gösterip
  kullanıcıya "çalışıyor" izlenimi verirken aslında hiçbir etkisi olmayan ölü koda bağlıydı — bu
  proje boyunca izlediğimiz "her yeni sınıfı gerçek veride scratch test ile uçtan uca doğrula"
  disiplini (senaryo 6/7/8, sorgu tarama motorları) bu hatayı tam olarak önlemek için var; yeni
  `Confirming*` sınıfları da aynı şekilde doğrulanmalı — UI'da bir checkbox/eşik alanı görünmesi
  "çalışıyor" anlamına gelmiyor, gerçek P&L farkının eşik açık/kapalıyken farklı çıktığı
  gösterilmeli.
- Kaynak: `ConfirmationMode_Implementation_Plan.md` (eski proje kök dizininin bir üstünde,
  `D:\Aykut\Projects\AlgoTradeWithOptimizationSupport\`) — orijinal tasarım niyetini anlatan bir
  plan belgesi, ölü kod onunla birebir eşleşiyor ama hiç bitirilmemiş/bağlanmamış.

### Mimari Fikir (kullanıcı önerisi + tamamlayıcı notlar)

Kullanıcının önerisi: **`SymbolScanner` gibi `AlgoTrader`'dan bağımsız** bir sınıf ailesi olarak
ele alınmalı, üç varyantı olmalı — `SingleTrader`, `MultipleTrader` ve `SingleTraderOptimizer`
karşılıkları. Bu mantıklı bir yaklaşım ama gerekçesi `SymbolScanner`'dan farklı: `SymbolScanner`
`AlgoTrader`'dan bağımsız çünkü **çoklu veri seti** (N sembol) üzerinde dönüyor, `AlgoTrader` tek
veri seti varsayımıyla kurulu. Bu özellik ise tek bir veri seti üzerinde çalışıyor — bağımsızlık
gerekçesi burada **`AlgoTrader`'ı (zaten karmaşık) 4. bir "mod" ile şişirmemek**, sinyal→emir
dönüşümüne ekstra bir aşama ekleyen bu mantığı ayrı, kendi başına yeten bir katmanda tutmak
(`MultipleQuery`'nin `MultipleTrader`'a bağlı olmaması gibi bir gerekçe — karmaşık iç mantığı
kopyalamadan/bozmadan yeni bir davranış eklemek).

### Karar: Kâr/Zarar Konfirmasyonu Ne Anlama Geliyor (kullanıcı ile netleşti, 2026-08-18)

**Yön asla ters çevrilmiyor** — gerçek pozisyon her zaman stratejinin orijinal sinyal yönünde
açılıyor (Long dediyse Long, Short dediyse Short). Fark sadece **hangi fiyattan/ne zaman**
gerçek pozisyona geçildiği — iki farklı giriş-zamanlama felsefesi:

- **Kâr konfirmasyonu** = momentum/trend teyidi. Sanal pozisyon lehe gidip eşiği geçerse, o anki
  (artık lehte hareket etmiş) fiyattan gerçek pozisyon açılır — "sinyal doğru çıktı, gücünü
  teyit ettim, şimdi trend'e katılıyorum."
- **Zarar konfirmasyonu** = dip/geri çekilme teyidi. Sanal pozisyon aleyhe gidip eşiği geçerse, o
  anki (artık düşmüş/aleyhte hareket etmiş) fiyattan gerçek pozisyon açılır — "önce bir geri
  çekilme oldu mu bekledim, oldu, şimdi daha iyi bir fiyattan giriyorum."

**Somut örnek** (Long sinyali, zarar eşiği 5 TL): Bar 100'de strateji "AL" dedi, fiyat 100 TL —
gerçek pozisyon HEMEN açılmıyor, sadece "sanki 100 TL'den Long açmışım gibi" sanal bir kayıt
tutuluyor. Fiyat düşüp bar 110'da 95 TL'ye inince (sanal zarar = 5 TL = eşik), **o an (bar 110,
95 TL'den)** gerçek Long pozisyonu açılıyor — 100 yerine 95'ten girilmiş oluyor. Risk: fiyat hiç
95'e inmeden yükselmeye başlarsa (eşik hiç geçilmezse) gerçek pozisyon hiç açılmayabilir, fırsat
kaçar — bu, stratejinin bilinçli olarak göze aldığı bir risk.

Bu, eski projedeki tek çalışan implementasyonun (yukarıdaki bölüme bkz.) yaptığı şeyle mekanik
olarak aynı (ikisinde de yön aynı kalıyor) — ama orada bunun NEDEN böyle olduğu belirsizdi
(muhtemelen bitirilmemiş); burada artık bilinçli bir tasarım kararı: iki farklı giriş-zamanlama
stratejisi (trend-teyitli vs. dip-teyitli giriş), ters yön/kontrarian mantığı YOK.

### Config Alanları (eski projeden — birebir taşınabilir)

Eski projedeki 4 alan yeni tasarıma doğrudan uyuyor, sadece anlamları artık net:

- **Kar Eşiği** (`KarEsigi`): Sanal pozisyonun ne kadar kâra geçtiğinde konfirme sayılacağı —
  "trend teyidi" eşiği.
- **Zarar Eşiği** (`ZararEsigi`): Sanal pozisyonun ne kadar zarara düştüğünde konfirme sayılacağı
  — "dip teyidi" eşiği.
- **Eşik Tipi** (Değer/Yüzde): İki eşiğin mutlak değer (puan/fiyat farkı) mi yoksa yüzde mi
  olarak yorumlanacağı.
- **Tetikleyici** (Both/KarOnly/ZararOnly): Hangi eşik(ler) aktif — `Both` ikisi de tetikleyebilir
  (hangisi önce gelirse), `KarOnly` sadece kâr eşiği sayılır (zarar eşiği hiç kontrol edilmez),
  `ZararOnly` sadece zarar eşiği sayılır (sadece dip bekler).

### Davranış Detayları (kullanıcı ile netleşti, 2026-08-18)

- **Konfirmasyon sonrası**: Gerçek pozisyon açıldıktan sonra kullanıcının zaten planladığı normal
  trade yönetimine (kâr al/zarar kes/poz kapat, stratejinin kendi çıkış sinyalleri) devrediliyor
  — konfirmasyon mekanizması sadece GİRİŞ anını geciktiriyor, çıkış tarafına hiç karışmıyor.
- **Süresiz bekleme**: Sanal pozisyon hiçbir eşiğe ulaşmazsa **sonsuza kadar** bekler — bilinçli
  olarak bir timeout/"N bar sonra vazgeç" mekanizması YOK (basitlik tercih edildi).
- **Sinyal değişirse**: Sanal pozisyon beklerken (henüz eşik geçilmedi) strateji ters bir sinyal
  verirse (örn. sanal Long beklerken strateji "SAT" derse), **yeni sinyal görmezden gelinir** —
  sanal pozisyon kendi orijinal yönünde, kendi eşiğine ulaşana kadar aynen devam eder. (İlk
  öneri "sinyal değişince sanal pozisyon iptal olup sıfırdan başlasın" idi, kullanıcı bunun işi
  çok karıştıracağını belirtip reddetti — basit/sabit sanal pozisyon tercih edildi.)
- **Aynı bar'da iki eşik birden geçilirse**: Nadir bir durum (büyük gap/sıçrama) — basit bir
  varsayılan kabul edildi: kod içinde hangi kontrol önce yazılıyorsa o kazanır (örn. her zaman
  önce kâr kontrolü). Kullanıcı bunu **ileride gözden geçirmek istiyor**, şimdilik bu kabul.
- **Console entegrasyonu — karar verildi (2026-08-18)**: Mevcut `[2]/[3]/[4]` hiç değişmeyecek,
  yeni `Confirming*` sınıfları için **yeni menü numaraları** eklenecek (örn. `[22]
  ConfirmingSingleTrader`, `[23] ConfirmingMultipleTrader`, `[24] ConfirmingSingleTraderOptimizer`
  — kesin numaralar implementasyon sırasında o anki son menü numarasına göre belirlenecek, şu an
  `[21]`'e kadar dolu). Bu, projede tarama motorlarında izlenen desenle tutarlı — her yeni yetenek
  kendi menü numarasını aldı, mevcut menüler hiç bozulmadı.
- **Açık kalan soru — henüz cevaplanamadı (kullanıcı `ApplyEquityCurveFilter`'ı hatırlamadığı için,
  2026-08-18)**: Mevcut `ApplyEquityCurveFilter` ile ilişkisi — aynı anda ikisi de aktif olabilir
  mi (önce sanal-konfirmasyon, sonra gerçek pozisyon üstünde tekrar equity-curve-filtresi
  çalışır), yoksa birbirini dışlayan iki ayrı mod mu? İki seçenek masada: (a) birbirini dışlayan
  iki ayrı mod (daha basit, davranışı anlamak/debug etmek kolay) — (b) üst üste çalışabilir (daha
  esnek ama iki mekanizma aynı bar'da etkileşebilir, anlaşılması zor).

  **Hatırlatma — `ApplyEquityCurveFilter` ne yapıyor**: Trader'ın **zaten gerçek bir pozisyonu
  açıkken** devreye giriyor — yani yeni "sanal konfirmasyon" mekanizmasından tamamen farklı bir
  zaman diliminde çalışıyor. Yeni mekanizma **ilk gerçek girişi** geciktiriyor; bu filtre ise
  pozisyon **zaten açıldıktan sonra** devreye giriyor, açık pozisyonun canlı kâr/zararına bakıp
  eşik geçilmeden o pozisyona gelen **yeni** Al/Sat sinyallerini (örn. piramitleme/ek giriş)
  bastırıyor — pozisyonu kapatmıyor, sadece ek girişi engelliyor. Yön değişince/flat'e düşünce
  "konfirme" durumu sıfırlanıyor. Özetle: yeni mekanizma "ilk girişi ne zaman yapayım" sorusuna,
  `ApplyEquityCurveFilter` ise "zaten açık pozisyona ek giriş sinyaline izin vereyim mi" sorusuna
  cevap veriyor — iki farklı olay, teorik olarak çakışmadan birlikte de çalışabilirler.

  **Kullanıcı notu**: Bu açıklamaya rağmen kullanıcı şu an net bir karar veremedi — özelliği
  gerçekten test edip anladıktan sonra net cevap verebileceğini belirtti. Yani implementasyon
  sırasında/sonrasında, gerçek kullanımla tekrar gündeme gelecek, şimdiden varsayılan bir
  davranışa kilitlenmiyoruz.

### Önerilen Sınıf İsimleri

Eski projede tam bu kavram için **`ConfirmingSingleTrader`** adında bir sınıf vardı (bkz.
migration-guide.md, `ConfirmingSingleTrader.buildConsensusSignal()` — o zamanki farklı bir
kullanım için ama isim hazır ve isabetli duruyor). Bu adlandırmayı sürdürmeyi öneriyorum, projenin
genel "İngilizce sınıf adı" konvansiyonuyla da tutarlı:

- **`ConfirmingSingleTrader`** — tek strateji, virtual→real staging
- **`ConfirmingMultipleTrader`** — MultipleTrader consensus'u, virtual→real staging
- **`ConfirmingSingleTraderOptimizer`** — bkz. aşağıdaki "Optimizer Varyantları" bölümü (tanımı düzeltildi)
- **`ConfirmingMultipleTraderOptimizer`** — aynı fikrin MultipleTrader karşılığı, bkz. aşağı

Alternatif (daha açıklayıcı ama daha uzun): `EquityCurveConfirmingSingleTrader`/`...MultipleTrader`/
`...SingleTraderOptimizer` — "hangi filtre/mekanizma" sorusuna daha net cevap veriyor ama isim
uzunluğu artıyor. `Confirming*` daha kısa ve eski projeyle bir bağ kuruyor, onu tercih ederim.

### Optimizer Varyantları — Eski Projede Emsal YOK, Tanım Düzeltmesi (2026-08-18)

Kullanıcı hatırlıyordu ama eski projede araştırıldı, **bulunamadı**: `KarEsigi`/`ZararEsigi` eski
projede sadece **tek sabit değer** olarak giriliyordu (düz `TextBox`, min/max/step yok);
`SingleTraderOptimizer` sadece strateji parametrelerini (MA periyotları vb.) tarıyordu,
`ConfirmingSingleTrader`'dan tamamen habersizdi. İkisi arasında hiçbir köprü yoktu — konfirmasyon
eşiklerini tarayan bir optimizer eski projede **hiç yazılmamış**. Yani bu, eski projeden taşınacak
bir şey değil, **sıfırdan yeni bir fikir**.

**Tanım düzeltmesi**: `ConfirmingSingleTraderOptimizer`/`ConfirmingMultipleTraderOptimizer`,
"strateji parametrelerini + konfirmasyon eşiklerini BİRLİKTE optimize eden" bir şey **değil** —
asıl fikir, strateji parametreleri **sabit kalırken** sadece **Kar Eşiği/Zarar Eşiği** (giriş
seviyesi) değerlerini grid-search ile taramak.

**Açık karar (implementasyondan önce netleşmeli)** — strateji parametreleri de dahil edilsin mi:

- **Sadece eşikler taransın, strateji parametreleri sabit (Claude'un önerisi)**: Küçük/hızlı arama
  uzayı (birkaç sayısal eşik kombinasyonu), yorumlaması kolay ("bu strateji için hangi
  konfirmasyon eşiği en iyi"), overfitting riski düşük.
- **Strateji parametreleri de dahil edilsin**: Arama uzayı **çarpımsal** büyür (strateji
  kombinasyonu × eşik kombinasyonu) — çok daha yavaş, `SingleTraderOptimizer`'da zaten bilinen
  overfitting riskini katlıyor. Avantajı, teorik olarak strateji parametreleriyle eşiklerin
  birbirini etkilediği "gerçek global optimum"u bulabilmek — ama bu avantajın artan risk/maliyeti
  karşılayıp karşılamadığı belirsiz.

Kullanıcı: "ileride bunun seçimi yapılacak" — şimdilik karar verilmedi, sadece iki seçenek not
edildi. Claude'un varsayılan önerisi: sadece eşikleri tara (strateji parametreleri sabit),
"strateji parametrelerini de dahil et" ileride opsiyonel/gelişmiş bir mod olarak eklenebilir.

### Implementasyon Notları — `ConfirmingSingleTrader` (2026-08-19)

**Dosyalar**:
- `src/AlgoTrade.Core/Trading/Traders/ConfirmingSingleTrader.cs` — asıl sınıf.
- `src/AlgoTrade.Core/AppConfig/AppConfig.cs` — `ConfirmingSingleTraderConfig` ve alt config sınıfları.
- `src/AlgoTrade.Core/AppConfig/AppConfigApplier.cs` — `ApplyConfirmingSingleTrader(...)`.
- `src/AlgoTrade.Core/Trading/AlgoTrader.cs` — `RunConfirmingSingleTraderWithProgressAsync(...)`,
  `WriteTraderDataToFilesAsync(ConfirmingSingleTrader)`, ilgili `Set*`/`Apply*` metodları.
- `AlgoTrade.Console/Program.cs` — menü `[22]`, `handleConfirmingSingleTrader()`,
  `showConfirmingSingleTraderRunPreview()`, `runConfirmingSingleTraderAlgoTrade()`,
  `AutoRunMode: "ConfirmingSingleTrader"` desteği.
- `inputs/configs/AppConfig/AppConfig.json` — `ConfirmingSingleTrader` bölümü (varsayılan:
  `SimpleMostStrategy v1`, `CancelAndRestart`, `FlattenImmediatelyOnFlatSignal=true` — yani eski
  projenin davranışının birebir aynısı).

**Mimari — tasarım tartışmasındaki "2 SingleTrader mı?" sorusunun cevabı**: Evet ama simetrik
değil. `_signalTrader` (tam çalışan bir `SingleTrader`, kendi stratejisiyle gerçekten pozisyon
açıp kapatıyor — bir strateji position-aware olabildiği için bu şart) ham Al/Sat/Flat sinyalini
üretiyor; `_mainTrader` (yine tam bir `SingleTrader`, ama kendi stratejisi yok) sadece konfirme
edilmiş sinyali alıp gerçek işlemi yapıyor — MultipleTrader'ın mainTrader'ına enjekte edilen
consensus sinyali gibi (`ExecutePreOrderMethods → strategySignal set → MapStrategyCommandsToTradeCommands
→ ApplyTimingFilters → ApplyEquityCurveFilter → ResolveFilterDecisions → ExecutePostOrderMethods`,
`Run(i)` bypass edilip elle çağrılıyor). Aradaki karar verici katman `_signalTrader`'ın kendi K/Z'i
DEĞİL, ayrı ve hafif bir `_virtualYon`/`_virtualEntryPrice`/`_confirmed` state'i (`VirtualPositionState`
fikri) — Design A (2 tam bağımsız SingleTrader, eski proje gibi) yerine Design B seçildi, çünkü
kullanıcının "sinyal değişince görmezden gel" (LockAndIgnore) davranışı Design A'da child'ın kendi
otonom trade motoruyla çakışıyordu.

**`SignalConflictMode`** (Al↔Sat çakışması) ve **`FlattenImmediatelyOnFlatSignal`** (Flat çakışması)
birbirinden bağımsız iki switch — kullanıcının istediği 2×2 matris (bkz. yukarıki tartışma).
Varsayılan `CancelAndRestart` + `true` eski projenin davranışının birebir aynısı.

**Gerçek veride bulunup düzeltilen kritik hata**: İlk implementasyonda, eşik geçilip konfirme
olduğu anda `_mainTrader`'a `_signalTrader.strategySignal` (o bar'ın HAM stratejik komutu)
gönderiliyordu. Ama sanal pozisyon genelde birkaç/çok sayıda bar önce (ilk sinyal geldiğinde)
açıldığı için, konfirme olduğu bar'da strateji artık yeni bir emir yayınlamıyor — `strategySignal`
o an `None` oluyor. Sonuç: `_mainTrader` hiçbir zaman gerçek pozisyon açmıyordu (30.000 barlık
BTCUSDT testinde `Virtual_Confirmed=1` olan ~23.811 bar'ın büyük kısmında `MainTrader_Sinyal=0`
kalıyordu). **Düzeltme**: konfirme anında ham sinyali değil, sanal pozisyonun yönüne göre
`TradeSignals.Buy`/`Sell`'i **biz kendimiz üretip** gönderiyoruz — konfirme anının kendisi giriş
komutu. Konfirme SONRASI (yani `_confirmed==true` iken her bar) ise `rawSignal` hâlâ doğru
kaynak — o noktada signalTrader'ın kendi exit/reversal kararlarını olduğu gibi mainTrader'a
yansıtmak istiyoruz (`docs/todo.md`'deki "Konfirmasyon sonrası" kararıyla tutarlı). Bu, sadece
gerçek veride koşturarak (CSV'deki `Virtual_Confirmed`/`MainTrader_Sinyal` kolonlarını karşılaştırarak)
yakalanabilecek türden bir hataydı — projenin "her yeni sınıfı gerçek veride doğrula" disiplininin
tam olarak neden var olduğunun bir örneği.

**Doğrulama**: `AppConfig.json`'da geçici olarak `AutoRunMode=ConfirmingSingleTrader` +
`ReadData.FilterMode=LastN, N1=30000` set edilip konsol uygulaması gerçek BTCUSDT_BNC verisiyle
uçtan uca koşturuldu (test sonrası config orijinaline geri alındı). `ConfirmingSingleTraderLists.csv`
(signalTrader/sanal/mainTrader kolonları yan yana) ile doğrulandı: sanal pozisyon strateji sinyalinden
çok sonra (farklı fiyattan, hatta bazen farklı yönde — CancelAndRestart nedeniyle) konfirme oluyor,
mainTrader tam o an gerçek pozisyon açıyor.

**Bilinen eksikler / sonraki adımlar**:
- **Plot overlay henüz yok — MultipleTrader'da da aynı eksik var, ortak bir fast-follow** (2026-08-19):
  - `ConfirmingSingleTrader.VirtualSignals`/`.Signals` (public `List<double>`, `SingleTrader.lists.SinyalList`
    ile aynı konvansiyon) hazır ve CSV export'ta zaten görünüyor, ama Python'a hiç gönderilmiyor —
    `runConfirmingSingleTraderAlgoTrade()` (`Program.cs`) `PlotMultipleTraderData` gibi çoklu-trader'lı
    bir fonksiyon değil, **tek-trader'lı** `PlotSingleTraderData(mainTrader)`'ı reuse ediyor
    (`PythonPlotter.cs:266`) — `signalTrader` bu çağrıya hiç dahil değil.
  - `MultipleTrader` bir adım ileride ama yine de eksik: `PlotMultipleTraderData` (`PythonPlotter.cs:311`)
    mainTrader + tüm child'ları Python'a gönderiyor (veri ulaşıyor), ama `multiple_data_plotter.py`'de
    Signals paneli (`setTradeSignals`, `:103`) **sadece `main.sinyal_list`** ile çiziliyor — `ShowChildsData`
    flag'i (`:19`) sadece Return/Return% panellerini (4/5) etkiliyor, Signals paneli hiç çoklu seri
    desteklemiyor.
  - **Ortak çözüm fikri**: `PlotSingleTraderData`'yı da (MultipleTrader'daki gibi) bir trader listesi
    kabul edecek şekilde genişletmek (ConfirmingSingleTrader için `[mainTrader, signalTrader]`), sonra
    Python tarafında Signals panelini (`multiple_data_plotter.py` VE tekli `data_plotter*.py`) çoklu
    seri/overlay çizecek hale getirmek — iki kullanım senaryosunu (child'lar, sanal/gerçek) aynı
    mekanizma çözer. Kullanıcı ihtiyacı ("ilk sinyal ne zaman geldi vs ne zaman konfirme oldu" analizi)
    şimdilik CSV üzerinden karşılanıyor.
- `ApplyEquityCurveFilter` ile etkileşim kararı hâlâ açık (yukarıdaki "Açık kalan soru" bölümü) —
  `MainTrader.EquityCurveFilter` config'de opsiyonel olarak bağlı, ama birlikte kullanımı gerçek
  veride henüz test edilmedi.
- `ConfirmingSingleTraderOptimizer`/`ConfirmingMultipleTraderOptimizer` ve Confirming tarama
  (scanner) varyantları henüz yazılmadı (aşağıdaki iki bölüm hâlâ geçerli — `ConfirmingMultipleTrader`
  artık tamamlandı, bkz. aşağıdaki bölüm).

### Implementasyon Notları — `ConfirmingMultipleTrader` (2026-08-19)

**Dosyalar** (ConfirmingSingleTrader'ın dosyalarına ek olarak):
- `src/AlgoTrade.Core/Trading/Traders/ConfirmingMultipleTrader.cs` — asıl sınıf.
- `src/AlgoTrade.Core/Trading/Core/VirtualPositionConfirmer.cs` — **yeni**, konfirmasyon state
  machine'i (`SignalConflictMode` enum dahil) buraya çıkarıldı — `ConfirmingSingleTrader` de bunu
  kullanacak şekilde refactor edildi (kod tekrarı yok, tek yerde bakım). Bu refactor sonrası
  ConfirmingSingleTrader gerçek veride yeniden koşturulup davranışın birebir aynı kaldığı doğrulandı.
- `AlgoTrader.cs` → `createConfirmingChildTraders(...)` (createChildTraders()'ın Confirming karşılığı),
  `RunConfirmingMultipleTraderWithProgressAsync(...)`, `WriteTraderDataToFilesAsync(ConfirmingMultipleTrader)`.
- `AppConfig.json` → `ConfirmingMultipleTrader` bölümü — `ChildTraders`/`Consensus` şeması
  MultipleTrader'la, `Confirmation`/`MainTrader` şeması ConfirmingSingleTrader'la birebir aynı
  (mevcut `ConsensusConfig`/`ConfirmationConfig`/`ConfirmingMainTraderConfig`/`ChildTraderEntry`
  sınıfları reuse edildi, yeni sınıf sadece `ConfirmingMultipleTraderSaveConfig` + root config).

**Mimari — composition**: `ConfirmingSingleTrader`'ın "signalTrader = tam çalışan bağımsız trader"
deseninin MultipleTrader karşılığı — `_signalMultipleTrader` tam, bağımsız çalışan **gerçek bir
`MultipleTrader`** (N child + kendi consensus mantığı, MultipleTrader kodunun kendisi HİÇ
değiştirilmeden reuse edildi), onun kendi mainTrader'ı bizim ham sinyal kaynağımız. Konfirmasyon
katmanı (`VirtualPositionConfirmer`) ConfirmingSingleTrader ile birebir aynı kod.

**Gerçek veride bulunup düzeltilen ikinci kritik hata**: İlk implementasyonda `_signalMultipleTrader`
30.000 barlık BTCUSDT testinde **hiçbir zaman** Buy/Sell üretmiyordu (`SignalConsensus_Sinyal` sürekli
`0.00`) — bar-by-bar incelemede iki child'ın **%94 oranında hemfikir olduğu** (`agree_buy=14249,
agree_sell=14089`, sadece ~1585/30000 barda anlaşmazlık) ortaya çıktı, yani "Net" consensus'un
gerçekte Flat dönmesi gereken bir durum değildi — asıl sebep, `MultipleTrader`'ın kendi mainTrader'ının
(`signalMain`) `signals.AlEnabled`/`SatEnabled`/`FlatOlEnabled` bayraklarının **varsayılan `false`**
olması (`Signals.cs:181-183`, `ConfigureUserFlagsOnce()`/`Reset()` ile sıfırlanıyor) — normal
`RunMultipleTraderWithProgressAsync()` akışında bunlar `ApplySingleTraderFlagsConfigs(mainTrader)`
ile AppConfig'den açılıyor, ama `ConfirmingMultipleTrader` kendi `signalMain`'ini kurarken bu çağrı
hiç yapılmıyordu. Sonuç: `MapStrategyCommandsToTradeCommands()` consensus'un ürettiği Buy/Sell'i
sessizce yok sayıyordu (`signals.Al`/`Sat` hiç `true` olmuyordu) — `signalMain.SonYon` sonsuza kadar
"F" kalıyor, konfirmasyon hiç tetiklenmiyordu. **Düzeltme**: `ConfirmingMultipleTrader.Init()`
içinde `signalMain.signals.AlEnabled/SatEnabled/FlatOlEnabled = true` doğrudan set ediliyor (dosya
adı çakışmasını önlemek için `ApplySingleTraderFlagsConfigs`'in tamamını reuse etmek yerine sadece
gerekli 3 bayrak açıldı). Düzeltme sonrası aynı testte consensus 14093 Sell / 1658 Flat / 14249 Buy
üretti (manuel bar-by-bar sayımla birebir eşleşiyor) ve `mainTrader` 788 barda gerçek pozisyon açtı.

**Doğrulama**: Aynı yöntemle (`AutoRunMode=ConfirmingMultipleTrader`, `LastN=30000`, `SimpleMostStrategy`
v1/v2, 2 child, `Net` consensus) gerçek veride uçtan uca koşturuldu, `ConfirmingMultipleTraderLists.csv`
incelendi — bar 73'te consensus ilk kez yön değiştiriyor (sanal pozisyon başlıyor), bar 6189'da
(94744.43 fiyattan, Long) ilk gerçek konfirme pozisyon açılıyor — ConfirmingSingleTrader testindeki
davranış deseniyle tutarlı.

**Bilinen eksikler**: ConfirmingSingleTrader'la aynı (plot overlay yok, `ApplyEquityCurveFilter`
etkileşimi test edilmedi). Ek olarak: `WriteSignalMultipleTraderListsToFiles`/`WriteSignalChildTradersDataToFiles`
(opsiyonel, varsayılan kapalı) açıldığında signal katmanının dosyaları **`AppSettings.LogsDir`**'e
(`outputs/logs/`) yazılıyor, `ConfirmingMultipleTraderLists.txt/csv` ise `AppDomain.CurrentDomain.BaseDirectory/logs`'a
(`bin/Debug/net8.0/logs/`) — iki farklı log dizini konvansiyonu aynı anda kullanılıyor (bu, zaten
var olan `WriteMultipleTraderListsToFiles` vs `ConfirmingSingleTrader`'ın kendi list-writer'ı
arasındaki mevcut tutarsızlığın devamı, yeni bir sorun değil — ama debug ederken nereye bakılacağını
bilmek gerekiyor).

**MultipleTrader'da bulunup düzeltilen performans hatası (2026-08-19, kullanıcı onayıyla)**:
`ConfirmingMultipleTrader`'ı tam veri setinde (900K bar) test ederken koşumun bitmediği (aslında
çok yavaş ilerlediği) fark edildi. Sebep `MultipleTrader.BuildConsensusSignal()`'daki
(`MultipleTrader.cs:255`) `LogManager.LogDebug(...)` çağrısıydı — Buy/Sell konsensüs üretilen HER
barda (bizim test verimizde barların ~%94'ü) senkron `Console.WriteLine` tetikliyordu; 900K bar ×
%94 ≈ 850K senkron konsol yazımı, koşumu dakikalarca uzatıyordu (SingleTrader'ın aynı veri setinde
saniyeler içinde bitmesiyle tam tersi orantısız bir yavaşlık — çocuk sayısından değil, bu tek log
satırından kaynaklanıyordu). **Düzeltme**: `LogManager.LogDebug(...)` → `LogManager.Log(LogLevel.Debug,
LogSinks.File, ...)` — artık sadece `app.log`'a yazılıyor, Console'a gitmiyor (bilgi kaybı yok,
sadece hedef daraltıldı). 200.000 barlık gerçek veri testinde doğrulandı: önce konsolu tıkayan
akış tamamen kesildi, koşum **20 saniyede** bitti (önceden dakikalarca sürüyordu), `app.log`'da
190.387 consensus satırı hâlâ mevcut (bilgi korunuyor). Kullanıcı "MultipleTrader'a dokunma"
kuralına bu spesifik performans düzeltmesi için açıkça istisna verdi (kod commit'li, gerekirse
revert edilecek).

### Fast-Follow: Tarama (Scanner) Versiyonları (2026-08-18)

`ConfirmingSingleTrader`/`ConfirmingMultipleTrader` (tıpkı `SingleTrader`/`MultipleTrader` gibi)
**sadece tek bir veri seti** (tek sembol, tek TF) üzerinde çalışır — bu yüzden Strateji ve Sorgu
eksenlerinde yaptığımız gibi, bir **Confirming tarama matrisi** de anlamlı olur:
`ConfirmingSymbolScanner`, `ConfirmingTimeframeScanner`, `ConfirmingSymbolTimeframeScanner` (ve
`ConfirmingMultipleTrader` tabanlı `MultiStrategy*` benzerleri) — bugüne kadar 16 kez kurduğumuz
aynı iskelet (nested loop, AutoDiscover/SymbolList, dinamik CSV header) doğrudan reuse edilebilir,
yeni bir mimari gerekmez.

**Sıralama**: Önce temel `ConfirmingSingleTrader`/`ConfirmingMultipleTrader`/
`ConfirmingSingleTraderOptimizer`/`ConfirmingMultipleTraderOptimizer` sınıfları gerçek veride
yazılıp doğrulanmalı — tarama (scanner) versiyonları bunlardan SONRA, doğal bir fast-follow
olarak gelmeli (tıpkı Strateji tarafında B/C'den sonra 4/6/7/8'in gelmesi gibi). Şimdiden
planlamaya gerek yok, ama bu genişleme **kesinlikle gündemde**.

**Sıralama — kullanıcı ile netleşti (2026-08-19)**, `ConfirmingSingleTrader` bitince şu sıra:
1. ✅ `ConfirmingMultipleTrader` — TAMAMLANDI (2026-08-19), bkz. yukarıdaki "Implementasyon Notları"
   bölümü. ConfirmingSingleTrader'ın kurduğu mimari (VirtualPositionConfirmer, ConflictMode,
   threshold mantığı, config-driven menü entegrasyonu deseni) reuse edilerek yazıldı.
2. ✅ **`ConfirmingSingleTrader`/`ConfirmingMultipleTrader` script versiyonları — TAMAMLANDI (2026-08-19)**.
   Beklenenin aksine `ScriptGlobals`'a hiçbir ekleme gerekmedi — `ScriptExecutor` zaten tüm
   assembly'yi ve `AlgoTrade.Core.Trading` namespace'ini script'e açıyor, `algoTrader.ConfirmingSingleTrader`/
   `.ConfirmingMultipleTrader` property'leri de zaten vardı. İş, mevcut `ProgramsMultipleTrader.csx` +
   `02_RunMultipleTraderWithProgressAsync.csx` desenini takip eden örnek script'ler yazmaktan ibaretti:
   - `inputs/scripts/ProgramsConfirmingSingleTrader.csx` + `05_RunConfirmingSingleTraderWithProgressAsync.csx`
   - `inputs/scripts/ProgramsConfirmingMultipleTrader.csx` + `06_RunConfirmingMultipleTraderWithProgressAsync.csx`
3. ✅ **`[9]` DearPyGuiDataPlotter (Start/Stop Test) script versiyonu — TAMAMLANDI (2026-08-19)**:
   `inputs/scripts/05_RunDearPyGuiDataPlotterTest.csx` — `handleDearPyGuiPlotterTest()`'in yaptığını
   yapıyor (process başlat → test bundle yükle → bekle, ESC ile iptal edilebilir → clear_panel → durdur).
   İnteraktif `ReadMenuInput()` yerine cancellable bekleme kullanıldı (script'ler Program.cs'in konsol
   input fonksiyonlarına erişemiyor).
4. **Tarama (scanner) script'leri ŞİMDİLİK BEKLEYECEK** — kullanıcı açıkça erteledi, henüz ele alınmadı.

**Script dosya numaralandırması (son hali, 2026-08-19)**: `04`=bundle üretimi, `05`=plotter test,
`06`=ConfirmingSingleTrader, `07`=ConfirmingMultipleTrader (kullanıcı isteğiyle bu sıraya getirildi —
bundle üretimi test'ten mantıksal olarak önce gelmeli):
- `04_GenerateDearPyGuiDataPlotterBundle.csx` (yeni)
- `05_RunDearPyGuiDataPlotterTest.csx`
- `06_RunConfirmingSingleTraderWithProgressAsync.csx`
- `07_RunConfirmingMultipleTraderWithProgressAsync.csx`

**Bulunan iki ek hata (2026-08-19)**:
- **Eski/silinmiş dosya adı referansı**: Hem orijinal `[9]` menüsü (`Program.cs`,
  `handleDearPyGuiPlotterTest()`) hem de ilk script versiyonum `full_pipeline_bundle.npz` adını
  arıyordu — bu dosya artık üretilmiyor (`.gitignore`'da bile listeli). Gerçekte var olan/commit
  edilen dosya `latest_bundle.npz`/`.view.json` (bkz. `src/DearPyGuiDataPlotter/inputs/input.json`).
  **Her iki yerde de düzeltildi** — kullanıcının kendi testinde "GUI açıldı ama hiçbir şey load
  etmedi" şikayetiyle bulundu.
- **`TradeDataBundleConverter` + `#load` sırası çakışması**: İlk `04_GenerateDearPyGuiDataPlotterBundle.csx`
  denemesi `01_RunSingleTraderWithProgressAsync.csx`'i `#load` edip `singleTrader`'ı reuse etmeye
  çalışıyordu — ama `01`'in kendi sonunda `singleTrader.Dispose()` çağrısı var (`DeleteModules()` ile
  `trader.Data` boşalıyor), bu da `ConvertSingleTrader`'ın "Finalize() sonrası çağırın, Data boş
  olmamalı" kontrolüne takılıyordu. **Düzeltme**: `01`'e dokunmadan (dış davranışını bozmamak için),
  `04` kendi minimal (query'siz, TradeOnly) SingleTrader çalıştırma akışını kendi içinde tutuyor,
  bundle dönüşümünü `Dispose()`'dan ÖNCE yapıyor.

**Doğrulama yöntemi**: `[8] Run Script` menüsü `Console.ReadKey` kullandığı için redirected/piped
stdin ile hiç çalışmıyor (headless test edilemiyor) — bunun yerine `ScriptExecutor`+`ScriptGlobals`'ı
doğrudan kullanan küçük, geçici bir headless harness yazıldı (`[8]`'in içeride yaptığı tam olarak bu),
dört script de bu harness üzerinden **gerçek veride, tam veri setinde** uçtan uca koşturulup
doğrulandı, sonra harness silindi (repoya hiç commit edilmedi). `04`'ün test koşumu commit edilmiş
`latest_bundle.npz`/`.view.json`'ı geçici olarak değiştirdi — test sonrası `git checkout` ile
orijinaline geri alındı (bilerek regenerate etmek isterlerse kullanıcı kendisi `04`'ü çalıştırabilir).
Sonuçlar:
- DearPyGuiDataPlotter test script'i (`05`): process gerçekten başladı/durdu, `[9]` ile birebir aynı davrandı.
- Bundle üretim script'i (`04`): 1.911.603 barlık VIP-X030-T verisinde ~8sn'de bundle üretti (kullanıcının
  kendi ortamında gerçek kullanım sırasında doğrulandı).
- ConfirmingSingleTrader script'i (`06`): 904.437 barda çalıştı (~51sn, çoğu dosya yazma), `VirtualSignals`
  (476K Buy/428K Sell) vs `Signals` (278K Buy/264K Sell) — konfirmasyon filtresi bekleneni yaptı.
- ConfirmingMultipleTrader script'i (`07`): 2 child, 904.437 barda çalıştı (~57sn), consensus `VirtualSignals`
  (337K Buy/285K Sell) vs `Signals` (441 Buy/187 Sell) — consensus + confirmation çift filtresi
  (daha az konfirme giriş, ConfirmingSingleTrader'a göre beklenen bir fark) doğru çalıştı.

## Crypto/FX Lot Büyüklüğü Çarpanı (`VarlikAdedCarpani`) — Uygulandı, Broker Teyidi Bekleniyor (2026-08-20)

**Potansiyel yanlış**: `InitialTradeParams.cs`'deki piyasa-tipi kurulum metotlarının (`SetKontratParamsX`)
hepsinde `varlikAdedCarpani` gerçek bir broker sabiti olarak hardcoded — **sadece Crypto/FxCrypto'da
`1.0`'da (no-op) kalmış**, muhtemelen crypto desteği eklenirken hiç ayarlanmadan unutulmuş:

```csharp
SetKontratParamsViopEndex  (kontratSayisi=1.0, varlikAdedCarpani=10.0)
SetKontratParamsViopHisse  (kontratSayisi=1.0, varlikAdedCarpani=100.0)
SetKontratParamsViopParite (kontratSayisi=1.0, varlikAdedCarpani=1000.0)
SetKontratParamsFxEndex/FxHisse/FxParite/FxMetal (lotSayisi=1.0, varlikAdedCarpani=100000.0)  // standart forex: 1 lot = 100.000 birim
SetKontratParamsFxCrypto (lotSayisi=1.0, varlikAdedCarpani=1.0)   ← şüpheli
SetKontratParamsCrypto   (lotSayisi=1.0, varlikAdedCarpani=1.0)   ← şüpheli
```

(`AppConfigApplier.cs:1375`, `MarketType: "FxCrypto"` için sadece `lotSayisi: cfg.LotSayisi` geçiliyor,
`varlikAdedCarpani` hiç geçilmiyor → her zaman varsayılan `1.0` kullanılıyor.)

**Nasıl fark edildi**: Kullanıcı gerçek broker/platform deneyiminde "0.01 lot ile pozisyon açıp fiyat
$10 artarsa, canlı hesapta ~$10 kâr yazıyor" diyor (yaklaşık 1:1 oran). `outputs/logs/SingleTraderLists.txt`
üzerinden gerçek veri kontrol edildi (BarNo 904436): `KarZararPuan=309.63` iken `KarZararFiyat=3.10`
çıkıyor — oran tam `LotSayisi=0.01` ile eşleşiyor (`VarlikAdedCarpani=1.0` olduğu için). Kullanıcının
tarif ettiği 1:1 oranı bu kodda yakalamak için `LotSayisi=0.01` (gerçekte girdiği gibi) sabit kalıp
`VarlikAdedCarpani=100` olması gerekirdi — ama bu sadece kullanıcının hafızasına dayanıyor ("yanlış
hatırlamıyorsam" dedi), kesin değil.

**Kullanıcı broker'ından/platformundan kesin sabiti teyit edecek.**

**Test edildi ve uygulandı (2026-08-20)**: `SetKontratParamsFxCrypto`'nun varsayılan `varlikAdedCarpani`
değeri `1.0` → **`100.0`** yapıldı (`InitialTradeParams.cs:588`). Geçici olarak `AppConfig.json`'da
`ReadData.FilterMode=LastN, N1=5000` + `AppSettings.AutoRunMode=SingleTrader` set edilip gerçek
BTCUSDT_BNC verisiyle koşturuldu (test sonrası config orijinaline geri alındı, `InitialTradeParams.cs`
değişikliği kalıcı bırakıldı). `SingleTraderLists.txt`'te birden fazla bar'da doğrulandı —
`KarZararFiyat` artık `KarZararPuan`'a **birebir eşit** çıkıyor (örn. Bar 132: ikisi de `-165.64`;
Bar 419: ikisi de `2632.72`), yani kullanıcının "$X hareket → $X kâr/bakiye değişimi" kriteri karşılandı.

Kullanıcı notu: *"100 olarak bırak, test ettim beklentimi karşıladı; broker'dan farklı bir teyit
gelirse o zaman 100'ü güncellerim."* — yani bu değer **kesinleşmiş broker doğrulaması değil**,
kullanıcının hafızasına dayalı en iyi tahmini; broker/platform üzerinden kesin sabit teyit edildiğinde
farklıysa güncellenecek.

**Öğrenildiğinde (veya farklı çıkarsa) neresi güncellenecek**:
- `src/AlgoTrade.Core/Trading/Core/InitialTradeParams.cs`, `SetKontratParamsFxCrypto` (satır 588,
  şu an `varlikAdedCarpani=100.0`) — asıl kullanılan metot (`AppConfig.json`'da `MarketType: "FxCrypto"`).
- `SetKontratParamsCrypto` (satır 604, plain `"Crypto"` market type) **hâlâ `1.0`'da bırakıldı** —
  kullanıcı şu an `FxCrypto` kullanıyor, bu metot hiç test edilmedi/dokunulmadı. Aynı sabit
  uygulanmak istenirse ayrıca güncellenmeli.
- `AppConfig.json`'a veya `TradeParamsConfig`'e yeni bir alan eklemeye **gerek yok**, kullanıcı
  zaten sadece `LotSayisi` giriyor (Viop/Fx'teki diğer piyasa tiplerinde olduğu gibi,
  `VarlikAdedCarpani` kavramını hiç bilmeden).

## Done

- [x] [docs/roadmap.md](roadmap.md) güncellendi — Python entegrasyonu için 3 yaklaşımdan ikisinin (dosya+subprocess: `DearPyGuiDataPlotter`, pythonnet: `PythonPlotter.cs`) fiilen benimsendiği, REST/gRPC'nin kullanılmadığı belgeye yansıtıldı.
