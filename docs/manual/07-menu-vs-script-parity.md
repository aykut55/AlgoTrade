# Menü ↔ Script Paritesi (Senkronizasyon Takibi)

> Amaç: Console'un interaktif `handleXxx()`/`runXxxAlgoTrade()` menü çiftleri ([2]/[5],
> [3]/[6], [4]/[7], ...) ile bunların "tek seferlik, döngüsüz" script hali olan
> `inputs/scripts/0N_RunXxxWithProgressAsync.csx` dosyaları arasında **davranış farkı**
> bırakmamak. `readme.txt`'teki tanıma göre (satır 12-14) script'ler "ilgili
> handleXxx()/runXxx() fonksiyon çiftinin interaktif döngüsü olmadan çalışan hali" olmalı —
> yani mantığın kendisi aynı kalmalı, sadece E/R/ENTER/B menü döngüsü ve config-özet ekranı
> çıkarılmış olmalı. Pratikte script'ler zamanla menüdeki değişiklikleri (özellikle plot ve
> dosya-yazma adımlarını) kaçırıyor. Bu dosya her çift için bulunan farkları, hangilerinin
> düzeltildiğini ve hangilerinin hâlâ açık olduğunu takip eder.
>
> Kural: bir fark kapatıldığında burada "✅ Düzeltildi (tarih, dosya:satır)" olarak işaretlenir,
> silinmez — ileride "bu neden böyleydi" sorusuna cevap kalsın diye.
>
> Sembol lejantı: **🔴 Kritik** = sonucu geçersiz kılabilecek davranış/correctness hatası (örn.
> optimizasyonun hiç işlem açmaması) · **🟡 Açık fark** = gerçek eksiklik ama sonucu geçersiz
> kılmaz (örn. bir dosyanın yazılmaması) · **⚪ Kasıtlı/kozmetik** = tasarım gereği, düzeltilmesi
> önerilmez · **✅ Düzeltildi** = fix uygulandı.
>
> Başlangıç tarihi: 2026-08-24.

## İçindekiler

1. [SingleTrader — \[5\] vs 01_RunSingleTraderWithProgressAsync.csx](#1-singletrader--5-vs-01_runsingletraderwithprogressasynccsx)
2. [MultipleTrader — \[6\] vs 02_RunMultipleTraderWithProgressAsync.csx](#2-multipletrader--6-vs-02_runmultipletraderwithprogressasynccsx)
3. [SingleTraderOptimizer — \[7\] vs 03_RunSingleTraderOptWithProgressAsync.csx](#3-singletraderoptimizer--7-vs-03_runsingletraderoptwithprogressasynccsx)

---

## 1. SingleTrader — [5] vs 01_RunSingleTraderWithProgressAsync.csx

**Menü tarafı:** `handleSingleTrader()` → `runSingleTraderAlgoTrade()`
(`AlgoTrade.Console/Program.cs:761-834`), içeride `algoTrader.RunSingleTraderWithProgressAsync()`
(`AlgoTrade.Core/Trading/AlgoTrader.cs:1260-1538`) ve config uygulaması
`AppConfigApplier.ApplySingleTrader()` (`AppConfigApplier.cs:32-130`).

**Script tarafı:** `inputs/scripts/01_RunSingleTraderWithProgressAsync.csx`, config'i
`Config_01_SingleTrader.csx`'den `#load` ile alıyor; `SingleTrader`'ı elle kurup elle
`for` döngüsüyle çalıştırıyor (menüdeki gibi `AlgoTrader.RunSingleTraderWithProgressAsync()`
sarmalayıcısını kullanmıyor).

### ✅ Düzeltildi (2026-08-24)

- **Plot hiç çalışmıyordu.** Script'te ne pythonnet/imgui_bundle plot'u (`PlotSingleTraderData`)
  ne de `DearPyGuiDataPlotter` bundle+load adımı yoktu — `Finalize()` + `WriteStatisticsToFile()`
  sonrası direkt dispose'a geçiyordu. Menüdeki karşılığı `Program.cs:793-825`.
  **Fix:** `01_RunSingleTraderWithProgressAsync.csx`'e "9b. Plot" bölümü eklendi (satır ~380-410)
  — `algoTrader.SetupPython()` + `algoTrader.PlotSingleTraderData(singleTrader)` ve
  `TradeDataBundleConverter.ConvertSingleTrader(...)` + `DearPyGuiDataPlotter.StartPlotter()/
  LoadBundle(...)` ikisi de eklendi, `using AlgoTrade.Core.Python;` /
  `using AlgoTrade.Core.Python.DearPyGuiDataPlotter;` satırları da (satır 21-22) eklendi.
  Gate şartı menüdekiyle aynı mantık: `selectedRunMode != TraderRunMode.QueryOnly` (menüde ayrıca
  `singleTrader.PlotEnabled` de kontrol ediliyor — script bunu bilerek atlıyor, bkz. aşağıdaki not).

### 🟡 Açık farklar — gerçek eksiklik (fonksiyonu etkiler)

1. **Veri okuma filtreleme yok.** Menü tarafı `readStockData()`
   (`Program.cs:610-692`, özellikle satır 665) `AppConfig.json`'daki `ReadData` bölümünden
   (`FilterMode`, `N1`, `N2`, `Dt1`, `Dt2`) gelen parametrelerle
   `stockDataReader.ReadDataFast(filePath, filterMode, n1, n2, dt1, dt2)` çağırıyor — yani veriyi
   tarih aralığına veya son-N-bar'a göre kısıtlayabiliyor. Script'te parametresiz
   `stockDataReader.ReadDataFast(stockDataFullFileName)` çağrılıyor — **her zaman dosyanın
   tamamı** okunuyor. AppConfig'de bir filtre tanımlıysa [5] ile script farklı bar
   sayısı/aralığıyla çalışır, sonuçlar örtüşmez.
2. **Export config eksik.** `AppConfigApplier.ApplySingleTrader()` içinde `cfg.Export` varsa
   `algoTrader.SetSingleTraderExportConfig(...)` çağrılıyor (`AppConfigApplier.cs:121-129`).
   Script'te export adımının karşılığı hiç yok.
3. **Head/Tail log yok.** `readStockData()`'da `addHeadTailInfo` açıksa ilk/son satırlar
   loglanıyor (`Program.cs:680-686`). Script bunu yapmıyor (sadece bar sayısını basıyor).
4. **Okuma sırasında progress event'i yok.** Menü `OnReadMetaData`/`OnReadData`/`OnProgress`'e
   abone oluyor (`Program.cs:621-623`). Script okuma bitince tek satır log basıyor, ara ilerleme
   yok (küçük dosyalarda önemsiz, büyük dosyalarda fark eder).
5. **t0-t3 zaman metrikleri eksik.** `RunSingleTraderWithProgressAsync()` içinde `TimeManager`
   ile 4 ayrı elapsed-time loglanıyor: t0=toplam, t1=run+finalize, t2=run, t3=finalize
   (`AlgoTrade.cs:1515-1518`). Script sadece `runElapsed`/`finalizeElapsed` (2 metrik) hesaplıyor.

### ⚪ Kasıtlı/kozmetik farklar (bug değil — tasarım gereği)

- **Config kaynağı:** [5] her şeyi (Signals/Save/Plot/TradeParams) canlı `AppConfig.json`'dan
  okuyor, `[E]` ile menüden anında değiştirilebiliyor (`AppConfigApplier.ApplySingleTrader`,
  `AppConfigApplier.cs:32-130`). Script `Config_01_SingleTrader.csx` + script içine hardcode
  edilmiş `OnApplyUserFlags`/`OnApplyUserFlags2` local fonksiyonlarını kullanıyor — script
  dosyasını elle değiştirmeden ayar değişmiyor. Bu, script'lerin "bağımsız/tekrarlanabilir
  deney" amacına hizmet ediyor, birleştirilmesi önerilmiyor.
- **Progress UI:** [5] `algoTrader.OnTraderProgress` event'i üzerinden Program.cs'in kendi görsel
  handler'ını tetikliyor; script her %5'te düz `Log(...)` satırı basıyor.
- **Dosya yazma sırası:** [5]'te `WriteTraderDataToFilesAsync` (`AlgoTrade.cs:1549-1578`) plot
  penceresiyle **paralel** (background `Task`) çalışıyor; script'te önce senkron
  `WriteStatisticsToFile` çağrılıyor, sonra plot açılıyor. Üretilen dosyalar aynı, sadece
  sıralama/hız farklı.
- **Csv/txt dosya seti:** Her iki tarafta da `SingleTrader`'ın `SaveFullStatsTxtEnabled` /
  `SaveFullStatsCsvEnabled` / `SaveMinimalStats*` / `SaveFullLists*` / `SaveMinimalLists*` /
  `SaveFullStatsTxtFormattedEnabled` / `SavePerformans*` flag'leri varsayılan `true`
  (`SingleTrader.cs:293-306`) ve hiçbiri kapatılmıyor — yani **[5] ve 01 script'i aynı dosya
  setini üretiyor** (full/minimal stats+lists txt/csv, formatted txt, performans txt/csv).

---

## 2. MultipleTrader — [6] vs 02_RunMultipleTraderWithProgressAsync.csx

**Menü tarafı:** `handleMultipleTrader()` → `runMultipleTraderAlgoTrade()`
(`AlgoTrade.Console/Program.cs:836-915`).

**Script tarafı:** `inputs/scripts/02_RunMultipleTraderWithProgressAsync.csx`, config'i
`Config_02_MultipleTrader.csx`'den alıyor; `MultipleTrader` + child `SingleTrader`'ları elle
kurup elle `for` döngüsüyle çalıştırıyor.

### ✅ Düzeltildi (2026-08-24)

- **Plot hiç çalışmıyordu.** Menüdeki karşılığı `Program.cs:872-904`
  (`PlotMultipleTraderData` + `TradeDataBundleConverter.ConvertMultipleTrader` +
  `DearPyGuiDataPlotter`). **Fix:** script'e "8b. Plot" bölümü eklendi (satır ~366-396), aynı iki
  plot çağrısı (`algoTrader.PlotMultipleTraderData(multipleTrader)` ve
  `bundleConverter.ConvertMultipleTrader(multipleTrader, bundleOutDir)` +
  `DearPyGuiDataPlotter.StartPlotter()/LoadBundle(...)`) eklendi, ilgili `using`'ler eklendi.

### 🟡 Açık farklar — gerçek eksiklik (fonksiyonu etkiler)

1. **MultipleTrader özet/karşılaştırma dosyası (grid) hiç üretilmiyor.**
   `AlgoTrader.WriteTraderDataToFilesAsync(MultipleTrader)` (`AlgoTrade.cs:1579-1628`) içinde
   `trader.WriteMultipleTraderStatistics(AppSettings.LogsDir)` çağrılıyor (satır 1598) — bu,
   mainTrader + tüm child'ları yan yana karşılaştıran tek dosyayı (MultipleTraderStatisticsGrid.txt
   / MinimalGrid.txt, bkz. `a37d950` commit) üretiyor. Script sadece
   `multipleTrader.WriteMultipleTraderListsToFiles(...)` ve her trader için ayrı ayrı
   `WriteStatisticsToFile(...)` çağırıyor (satır 346-360) — **grid karşılaştırma dosyası
   script'te hiç oluşmuyor.**
2. **`WriteChildTradersDataToFiles` flag'i kontrol edilmiyor.** Menü tarafında child istatistik
   yazımı `trader.WriteChildTradersDataToFiles` flag'ine bağlı (`AlgoTrade.cs:1611`). Script
   doğrudan her child'ın kendi `SaveStatisticsToFile`'ına bakıyor (satır 356-360), üst seviye
   flag'i hiç sormuyor — AppConfig'de bu flag kapalıyken bile script child dosyalarını yazar.
3. **Aynı 1-5 madde (SingleTrader bölümündeki ReadData filtreleme / Export config / Head-Tail /
   okuma progress / t0-t3 metrikleri) burada da geçerli** — `readStockData()` ve
   `AppConfigApplier.ApplyMultipleTrader()` yolunun tamamı SingleTrader ile aynı mekanizmayı
   kullanıyor.

### ⚪ Kasıtlı/kozmetik farklar

- **Csv/txt dosya seti:** main + her child trader için flag'ler yine varsayılan `true`, dolayısıyla
  (grid dosyası hariç, bkz. madde 1) [6] ve 02 script'i aynı per-trader dosya setini üretiyor.
- Config kaynağı / progress UI / dosya yazma sırası farkları SingleTrader bölümündekiyle aynı
  gerekçeyle kasıtlı.

---

## 3. SingleTraderOptimizer — [7] vs 03_RunSingleTraderOptWithProgressAsync.csx

**Menü tarafı:** `handleSingleTraderOpt()` → `runSingleTraderOptimization()`
(`AlgoTrade.Console/Program.cs:1039-1074`), config uygulaması
`AppConfigApplier.ApplySingleTraderOpt()` (`AppConfigApplier.cs:872-998`), çalıştırma
`algoTrader.RunSingleTraderOptWithProgressAsync()` (`AlgoTrade.Core/Trading/AlgoTrader.cs:2767-2957`).

**Script tarafı:** `inputs/scripts/03_RunSingleTraderOptWithProgressAsync.csx`, config'i
`Config_03_SingleTraderOpt.csx`'den alıyor. **Not:** bu script 01/02'den farklı olarak
`algoTrader.RunSingleTraderOptWithProgressAsync()`'i (aynı menünün kullandığı sarmalayıcıyı)
**doğrudan çağırıyor** — kendi elle bar-loop'u yazmıyor. Bu yüzden progress/best-result
loglama/timer (t0/t1) metrikleri otomatik olarak birebir aynı; parametre range + fixed-param +
strategy-factory mekanizması da (`SetOptimizationStrategyFactory` ile fixed+range merge) menünün
`ConfigureOptimizationFromConfig()`'inin (`AlgoTrade.cs:1210-1223`) yaptığının **JSON yerine
hardcoded .csx değerleriyle** birebir aynısı — burada yapısal bir fark yok.

### 🔴 Kritik hata — script'in optimizasyon sonuçları muhtemelen anlamsız

**Test trader'larda AL/SAT/FlatOl/... sinyalleri hiç açılmıyor.** `SingleTraderOptimizer.
ApplyConfigsToTrader()` (`SingleTraderOptimizer.cs:845-877`) her kombinasyon için önce
`trader.ConfigureUserFlagsOnce()` çağırıyor — bu metod **tüm** sinyal flag'lerini (`AlEnabled`,
`SatEnabled`, `FlatOlEnabled`, ... ) `false`'a resetliyor (`SingleTrader.cs:2508-2542`, hiçbir
yerde tekrar `true` yapmıyor). Hemen ardından `if (SignalsConfig is { } s) { ... }`
(`SingleTraderOptimizer.cs:855-876`) bloğu bu flag'leri (ve `StartDateTimeStr`/`StopDateTimeStr`
backtest aralığını) gerçek değerlerine set ediyor — **ama sadece `SignalsConfig` null değilse.**

Menü tarafı `AppConfigApplier.ApplySingleTraderOpt()` içinde
`algoTrader.SetSingleTraderOptSignalsConfig(...)` çağrısıyla (AppConfig.json'dan) bu config'i
dolduruyor. **Script bu çağrıyı hiç yapmıyor** — `algoTrader.SingleTraderOptimizer.SignalsConfig`
null kalıyor, yukarıdaki `if` bloğu atlanıyor, ve `MapStrategyCommandsToTradeCommands()`
(`SingleTrader.cs:769-786`) içindeki `if (this.signals.AlEnabled) this.signals.Al = true;` /
`if (this.signals.SatEnabled) this.signals.Sat = true;` gate'leri hep `false` olduğu için
**strateji Buy/Sell sinyali üretse bile hiçbir pozisyon açılmıyor.** Sonuç: script'teki her
kombinasyon muhtemelen 0 işlemli/flat sonuç veriyor — optimizasyonun kendisi fiilen çalışmıyor
olabilir. **Bunu koda bir kombinasyon çalıştırıp `IslemSayisi`'ı kontrol ederek doğrulamanı
öneririm** (script çıktısında `BEST RESULT` altındaki `IslemSayisi` alanı 0 ise teyit olur).

**Fix (henüz uygulanmadı):** `Config_03_SingleTraderOpt.csx`'e 01 script'indeki `OnApplyUserFlags`
ile aynı mantıkta bir Signals bloğu (AL/SAT/FlatOl/PasGec/KarAl/ZararKes enable flag'leri +
StartDateTime/StopDateTime) eklenip, ana script'te `Initialize()`'dan önce
`algoTrader.SetSingleTraderOptSignalsConfig(new SingleTraderSignalsConfig { ... })` çağrısı
eklenmeli.

### 🟡 Açık farklar — gerçek eksiklik (fonksiyonu etkiler)

1. **CSV/TXT optimizasyon log dosyaları hiç yazılmıyor.** Script `SetSingleTraderOptLogConfig(...)`
   ve `SetSingleTraderOptSortOutputConfig(...)` çağırmıyor, dolayısıyla
   `SingleTraderOptimizer.CsvFileLoggingEnabled`/`TxtFileLoggingEnabled` varsayılan `false`,
   `CsvFilePath`/`TxtFilePath`/`SortedCsvFilePath`/`SortedTxtFilePath` varsayılan `""` kalıyor
   (`SingleTraderOptimizer.cs:120-134`). Sonuç: her kombinasyonun satırı hiçbir dosyaya
   yazılmıyor, sıralı (best-to-worst) sonuç dosyası da hiç üretilmiyor — script sadece konsola
   `Log(...)` ile en iyi sonucu basıyor, hiçbir kalıcı çıktı dosyası yok. Menü tarafında bunlar
   `AppConfig.json`'daki `SingleTraderOptimizer.Save`/`Sort` bölümünden
   `AppConfigApplier.ApplySingleTraderOpt()` (satır 926-944) ile otomatik geliyor.
2. **Veri okuma filtreleme yok** — §1'deki 1. madde ile aynı (`ReadDataFast(stockDataFullFileName)`
   parametresiz çağrılıyor, `AppConfig.json`'daki `ReadData.FilterMode/N1/N2/Dt1/Dt2` script'te
   karşılığı yok).

### ⚪ Kasıtlı/kozmetik farklar — ve bir "dead code" notu

- Config kaynağı (canlı `AppConfig.json` vs hardcoded `Config_03_SingleTraderOpt.csx`) farkı
  §1'deki gerekçeyle aynı, kasıtlı.
- **Not (menü tarafında da fiilen kullanılmıyor gibi görünüyor):** `ApplySingleTraderOpt()`
  içindeki "Best trader — Plot/Save/Export" blokları (`AppConfigApplier.cs:946-997`)
  `SetSingleTraderPlotConfig`/`SetSingleTraderSaveConfig`/`SetSingleTraderExportConfig`
  çağırıyor, ama bunlar `SingleTrader`'ın normal akışında kullanılan **aynı** setter'lar —
  `SingleTraderOptimizer` sınıfında (`SingleTraderOptimizer.cs`) bu config'leri okuyan/tüketen
  hiçbir kod yok (grep sonucu boş), ne de `RunSingleTraderOptWithProgressAsync()`
  (`AlgoTrade.cs:2767-2957`) içinde en iyi sonucun bir `SingleTrader`'a dönüştürülüp
  plot/export edildiği bir adım var. Yani bu üç config bloğu **menü tarafında da fiilen ölü
  kod** olabilir — script'in bunları atlaması bir "script eksikliği" değil, muhtemelen menünün
  kendisinde bitmemiş bir özellik. Doğrulamaya değer ama bu dosyanın kapsamı dışında (script↔menü
  paritesi değil, menünün kendi iç tutarlılığı).
