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

- **Veri okuma filtreleme yoktu.** Menü tarafı `readStockData()`
  (`Program.cs:610-692`, özellikle satır 665) `AppConfig.json`'daki `ReadData` bölümünden
  (`FilterMode`, `N1`, `N2`, `Dt1`, `Dt2`) gelen parametrelerle
  `stockDataReader.ReadDataFast(filePath, filterMode, n1, n2, dt1, dt2)` çağırıyor — yani veriyi
  tarih aralığına veya son-N-bar'a göre kısıtlayabiliyor. Script'te parametresiz
  `stockDataReader.ReadDataFast(stockDataFullFileName)` çağrılıyordu — her zaman dosyanın tamamı
  okunuyordu. **Fix:** `Config_01_SingleTrader.csx`'e `ReadDataConfig` ile birebir aynı 5 alan
  eklendi (`readDataFilterMode`, `readDataN1`, `readDataN2`, `readDataDt1`, `readDataDt2` —
  Ayarlar bölümünün hemen altı). `01_RunSingleTraderWithProgressAsync.csx`'te "1. Veri Oku"
  bölümünde (satır ~129-133) `Enum.TryParse<StockDataReader.FilterMode>(...)` +
  `Dt1`/`Dt2` boşsa `null` olacak şekilde `DateTime.Parse(...)` yapılıp
  `ReadDataFast(stockDataFullFileName, filterMode, readDataN1, readDataN2, dt1, dt2)` 6
  parametreli haliyle çağrılıyor artık — menünün `readStockData()`'daki mantığının birebir
  aynısı.

- **Head/Tail log yoktu.** `readStockData()`'da `addHeadTailInfo` açıksa ilk/son satırlar
  loglanıyor (`Program.cs:680-686`). **Not:** `addHeadTailInfo` (`Program.cs:52`) kod tabanında
  **hiçbir yerde `true` yapılmıyor** — [5] de fiilen bu logu hiç basmıyor (ölü flag). **Fix:**
  yine de `Config_01_SingleTrader.csx`'e aynı isimde bir `addHeadTailInfo = false` toggle'ı
  eklendi (satır ~26-28) ve script'te veri okunduktan sonra (satır ~159-166)
  `if (addHeadTailInfo) { Log(stockDataReader.Head()); Log(stockDataReader.Tail()); }` bloğu
  eklendi — varsayılan davranış [5] ile aynı (kapalı), script'te debug amaçlı açılabilir bir
  esneklik olarak duruyor.
- **Okuma sırasında progress event'i yoktu.** Menü `OnReadMetaData`/`OnProgress`'e abone oluyor
  (`Program.cs:64-92`) — `OnReadData` handler'ı ise boş (`Program.cs:94`, no-op), o yüzden
  sync'lenmedi. **Fix:** `01_RunSingleTraderWithProgressAsync.csx`'te `stockDataReader`
  oluşturulduktan hemen sonra (satır ~116-131) aynı iki event'e (`OnReadMetaData`/`OnProgress`)
  Program.cs'teki handler'larla birebir aynı içerikte (Record Time/Chart Symbol/Chart
  Period/Bar Count/Start Date/End Date/Format + her 1000 kayıtta bir "Record no") abone olundu.
- **t0-t3 zaman metrikleri eksikti.** `RunSingleTraderWithProgressAsync()` içinde `TimeManager`
  ile 4 ayrı elapsed-time loglanıyor: t0=toplam, t1=run+finalize, t2=run, t3=finalize
  (`AlgoTrade.cs:1515-1518`). Script sadece `Stopwatch` ile `runElapsed`/`finalizeElapsed`
  (2 metrik) hesaplıyordu. **Fix:** script'teki `Stopwatch` tamamen kaldırıldı,
  `using AlgoTrade.Core.Timer;` eklenip aynı `TimeManager.GetInstance()` singleton'ı ve aynı
  "0"/"1"/"2"/"3" timer ID'leri, menüdeki **birebir aynı sınır noktalarında**
  (`RestartTimer("0")` indicators'tan önce, `RestartTimer("1")`+`RestartTimer("2")` Init
  sonrası run loop'tan önce, `StopTimer("2")` run loop sonrası, `RestartTimer("3")`/`StopTimer("3")`
  sadece `Finalize()` çağrısını sarıyor, `StopTimer("1")`/`StopTimer("0")` query summary'den
  sonra) kullanılarak eklendi. **Ayrıca fark edilen bir sıralama sorunu da düzeltildi:** Plot
  bölümü ("9b") başta t0/t1 ölçümünün İÇİNE düşüyordu (plot penceresi açık kaldığı sürece t0/t1
  şişerdi) — menüde Plot, `RunSingleTraderWithProgressAsync()` tamamen bittikten (t0-t3
  hesaplandıktan) SONRA tetikleniyor (`Program.cs:793-825`), bu yüzden Plot bölümü script'te de
  t0-t3 loglandıktan sonraya (bölüm 12 Temizle'den hemen önceye) taşındı.

- **Export config eksikti.** `AppConfigApplier.ApplySingleTrader()` içinde `cfg.Export` varsa
  `algoTrader.SetSingleTraderExportConfig(...)` çağrılıyor (`AppConfigApplier.cs:121-129`) —
  bu, `SingleTrader.WriteStatisticsToFile()` içinde (`SingleTrader.cs:2662-2675`)
  `ExportEnabled=true` ise `FullListsTxtFileName`/`PerformansTxtFileName`'i `ExportConfigFile`
  (`StatisticsExporterConfig.json`) içindeki `ExportVersion` (örn. "v2") sütun tanımıyla
  **üzerine tekrar yazan** aktif bir özellik (dead code değil — varsayılan `ExportEnabled=false`
  olduğu için AppConfig'de açılmadıkça görünmez). Script'te bu adımın karşılığı hiç yoktu.
  **Fix:** `Config_01_SingleTrader.csx`'e `exportEnabled`/`exportConfigFile`/`exportVersion`
  (varsayılan `false`/`"StatisticsExporterConfig.json"`/`"v1"` — AppConfig'in kendi
  varsayılanlarıyla aynı) eklendi; `01_RunSingleTraderWithProgressAsync.csx`'te
  `OnApplyUserFlags2(SingleTrader trader)` içine (satır ~100-102)
  `trader.ExportEnabled/ExportConfigFile/ExportVersion` atamaları eklendi.

**§1 durumu: artık tüm 🟡/🔴 farklar kapatıldı — [5] ile 01 script'i arasında sadece ⚪ (kasıtlı/
kozmetik) farklar kaldı.**

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

### 🔴 Kritik hata (2026-08-24) — mainTrader ve child'lar muhtemelen hiç işlem açmıyordu

**Zincir:** `mainTrader.ConfigureUserFlagsOnce()` (eski satır 128) ve her
`childTrader.ConfigureUserFlagsOnce()` (eski satır 175/218/261) çağrılıyordu ama **hiçbirinde
ardından `AlEnabled`/`SatEnabled`/... `true` yapılmıyordu** — 01 script'teki `OnApplyUserFlags`'ın
karşılığı hiç yoktu. `ConfigureUserFlagsOnce()` tüm sinyal flag'lerini `false`'a resetliyor
(`SingleTrader.cs:2508-2542`); `MapStrategyCommandsToTradeCommands()` (`SingleTrader.cs:769-786`)
`AlEnabled`/`SatEnabled` false iken strateji Buy/Sell üretse bile `signals.Al`/`.Sat`'ı hiç `true`
yapmıyor → `signals.Sinyal` hiç "A"/"S" olmuyor → `SonYon` hiç değişmiyor →
`MultipleTrader.BuildConsensusSignal()`'daki `is_son_yon_a()/_s()` (`MultipleTrader.cs:233-238`)
hep `false` → consensus hep Flat → mainTrader'ın kendi `AlEnabled`'ı da false olduğu için zaten
pozisyon açmıyor. **Yani hem child'lar hem mainTrader muhtemelen hiçbir zaman tek bir işlem bile
açmamıştı** — script'in ürettiği tüm istatistikler (varsa) 0 işlemli/flat olmalıydı. Bu, §3'teki
optimizer `SignalsConfig` hatasıyla birebir aynı kalıp.

**Fix:** `02_RunMultipleTraderWithProgressAsync.csx`'e 01 script'teki `OnApplyUserFlags`'ın
karşılığı olan `ApplyUserFlags(SingleTrader trader)` local fonksiyonu eklendi (satır ~39-62) ve
`mainTrader.ConfigureUserFlagsOnce()` ile her 3 `childTrader.ConfigureUserFlagsOnce()` çağrısının
hemen ardına `ApplyUserFlags(...)` çağrısı eklendi.

### 🔴 Kritik hata (2026-08-24) — main/child dosya adı çakışması, sessiz veri kaybı

`mainTrader` ve 3 `childTrader`'ın hiçbiri dosya adlarını override etmiyordu — hepsi
`SingleTrader`'ın varsayılan adlarını (`SingleTraderStatistics.txt` vb., `SingleTrader.cs:275-306`)
kullanıyordu. `WriteStatisticsToFile` main→child0→child1→child2 sırasıyla çağrıldığı için **her
biri bir öncekinin dosyasının üzerine yazıyordu** — sadece son çalışan trader'ın (child2) çıktısı
hayatta kalıyordu, mainTrader ve child0/child1'in istatistikleri sessizce kayboluyordu. [6] bunu
`FilePrefix` ile (`AppConfigApplier.cs:214-215,236-249,379,397-410`,
`{prefix}_Main_{file}`/`{prefix}_Child{i}_{file}`) çözüyor.

**Fix:** `Config_02_MultipleTrader.csx`'e `filePrefix = "MultipleTrader"` eklendi.
`02_RunMultipleTraderWithProgressAsync.csx`'e `ApplyFileNamesAndExport(SingleTrader trader, string
cp)` local fonksiyonu eklendi (satır ~64-84, tüm `Save*FileName` alanlarını `{cp}_...` ile
ayrıştırıyor) ve mainTrader için `ApplyFileNamesAndExport(mainTrader, $"{filePrefix}_Main")`,
her child için `ApplyFileNamesAndExport(childTrader, $"{filePrefix}_Child{childId}")` çağrıları
eklendi.

### ✅ Düzeltildi (2026-08-24)

- **Plot hiç çalışmıyordu.** Menüdeki karşılığı `Program.cs:872-904`
  (`PlotMultipleTraderData` + `TradeDataBundleConverter.ConvertMultipleTrader` +
  `DearPyGuiDataPlotter`). **Fix:** script'e "8b. Plot" bölümü eklendi, aynı iki
  plot çağrısı (`algoTrader.PlotMultipleTraderData(multipleTrader)` ve
  `bundleConverter.ConvertMultipleTrader(multipleTrader, bundleOutDir)` +
  `DearPyGuiDataPlotter.StartPlotter()/LoadBundle(...)`) eklendi, ilgili `using`'ler eklendi.
- **MultipleTrader özet/karşılaştırma dosyası (grid) hiç üretilmiyordu.**
  `AlgoTrader.WriteTraderDataToFilesAsync(MultipleTrader)` (`AlgoTrade.cs:1579-1628`) içinde
  `trader.WriteMultipleTraderStatistics(AppSettings.LogsDir)` çağrılıyor (satır 1598) — bu,
  mainTrader + tüm child'ları yan yana karşılaştıran tek dosyayı (MultipleTraderStatisticsGrid.txt
  / MinimalGrid.txt, bkz. `a37d950` commit) üretiyor. **Fix:** script'te Finalize bloğuna aynı
  `multipleTrader.WriteMultipleTraderStatistics(AppSettings.LogsDir)` çağrısı eklendi.
- **`WriteChildTradersDataToFiles` flag'i kontrol edilmiyordu.** Menü tarafında child istatistik
  yazımı `trader.WriteChildTradersDataToFiles` flag'ine bağlı (`AlgoTrade.cs:1611`). **Fix:**
  `Config_02_MultipleTrader.csx`'e `writeChildTradersDataToFiles = true` eklendi,
  `multipleTrader.WriteChildTradersDataToFiles` property'sine atandı ve child dosya yazma döngüsü
  bu flag'e bağlandı.
- **Export config eksikti** — §1'deki gibi `Config_02_MultipleTrader.csx`'e ortak
  `exportEnabled`/`exportConfigFile`/`exportVersion` eklendi, `ApplyFileNamesAndExport(...)`
  içinde her trader'a (main + 3 child) uygulanıyor.
- **§1'deki 4 fix buraya taşındı**: ReadData filtreleme (`readDataFilterMode`/`N1`/`N2`/`Dt1`/`Dt2`
  + filtreli `ReadDataFast` çağrısı), `addHeadTailInfo` toggle, okuma sırasında
  `OnReadMetaData`/`OnProgress` event'leri, ve `Stopwatch` yerine `TimeManager` ile menüdeki
  birebir aynı sınır noktalarında (`RunMultipleTraderWithProgressAsync()`'in `AlgoTrade.cs:1899`
  `RestartTimer("0")`, `:2038-2039` `RestartTimer("1")`/`("2")`, `:2068` `StopTimer("2")`, `:2096`/
  `:2136` `RestartTimer("3")`/`StopTimer("3")`, `:2138-2139` `StopTimer("1")`/`("0")`) t0-t3
  zaman metrikleri. Plot bölümü de (§1'deki gibi) t0-t3 loglandıktan sonraya taşındı — menüde
  Plot, `RunMultipleTraderWithProgressAsync()` tamamen bittikten sonra tetikleniyor
  (`Program.cs:872-904`), plot penceresi açık kaldıkça t0/t1 şişmesin diye.

**§2 durumu: artık tüm 🔴/🟡 farklar kapatıldı — [6] ile 02 script'i arasında sadece ⚪ (kasıtlı/
kozmetik) farklar kaldı.**

### ⚪ Kasıtlı/kozmetik farklar

- **Csv/txt dosya seti:** main + her child trader için flag'ler yine varsayılan `true`, dolayısıyla
  [6] ve 02 script'i aynı per-trader dosya setini üretiyor (artık grid dosyası dahil).
- Config kaynağı / progress UI / dosya yazma sırası farkları SingleTrader bölümündekiyle aynı
  gerekçeyle kasıtlı. (Not: [6]'nın per-child farklı Export ayarı desteği script'te tek ortak
  export config'e sadeleştirildi — script zaten tüm child'lar için ortak hardcoded ayar setini
  paylaşıyor, bu bilinçli bir sadeleştirme.)

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

**Fix (2026-08-25) — uygulandı:** `Config_03_SingleTraderOpt.csx`'e 01 script'indeki
`OnApplyUserFlags` ile aynı mantıkta bir Signals bloğu eklendi (`alEnabled`/`satEnabled`/
`flatOlEnabled`/`pasGecEnabled`/`karAlEnabled`/`zararKesEnabled`/`gunSonuPozKapatEnabled`/
`timeFilteringEnabled`/`signalsStartDateTime`/`signalsStopDateTime`/
`tradeStartBarIndexEnabled`/`tradeStartBarIndex`, varsayılanlar `SingleTraderSignalsConfig`'in
kendi varsayılanlarıyla aynı). `03_RunSingleTraderOptWithProgressAsync.csx`'te `Initialize()`'dan
önce (bölüm 3'ün sonu) `algoTrader.SetSingleTraderOptSignalsConfig(new SingleTraderSignalsConfig
{ ... })` çağrısı eklendi — `AppConfigApplier.ApplySingleTraderOpt()` (satır 909-923) ile birebir
aynı alan eşlemesi.

### ✅ Düzeltildi (2026-08-25)

- **CSV/TXT optimizasyon log dosyaları hiç yazılmıyordu.** Script `SetSingleTraderOptLogConfig(...)`
  ve `SetSingleTraderOptSortOutputConfig(...)` çağırmıyordu, dolayısıyla
  `SingleTraderOptimizer.CsvFileLoggingEnabled`/`TxtFileLoggingEnabled` varsayılan `false`,
  `CsvFilePath`/`TxtFilePath`/`SortedCsvFilePath`/`SortedTxtFilePath` varsayılan `""` kalıyordu
  (`SingleTraderOptimizer.cs:120-134`) — hiçbir kombinasyonun satırı dosyaya yazılmıyor, sıralı
  (best-to-worst) sonuç dosyası da üretilmiyordu. Menü tarafında bunlar `AppConfig.json`'daki
  `SingleTraderOptimizer.Save`/`Sort` bölümünden `AppConfigApplier.ApplySingleTraderOpt()`
  (satır 926-944) ile otomatik geliyor. **Fix:** `Config_03_SingleTraderOpt.csx`'e Log bloğu
  (`csvFileLoggingEnabled`/`csvFileName`/`txtFileLoggingEnabled`/`txtFileName`/`appendEnabled`/
  `statisticsExporterConfigFileEnabled`/`statisticsExporterConfigFile`/`fileFlushIntervalMs`) ve
  Sort bloğu (`sortField`/`sortedCsvFileName`/`sortedTxtFileName`) eklendi — varsayılanlar
  `SingleTraderOptLogConfig`/`SingleTraderOptSortOutputConfig`'in kendi varsayılanlarıyla aynı
  (`outputs/opt/singleTraderOptLog.csv` vb.). Ana script'te `SetSingleTraderOptLogConfig(...)` ve
  `SetSingleTraderOptSortOutputConfig(...)` çağrıları eklendi (Signals çağrısının hemen ardına,
  `Initialize()`'dan önce) — `AppConfigApplier.ApplySingleTraderOpt()` (satır 926-944) ile birebir
  aynı alan eşlemesi. Dosyalar `AlgoTrader.cs:2822-2841`'de `AppSettings.OptLogsDir` altına
  yazılıyor, script tarafında bu yol için ek bir şey gerekmiyor.
- **Veri okuma filtreleme yoktu** — §1'de SingleTrader script'i için düzeltilen fix
  (`readDataFilterMode`/`N1`/`N2`/`Dt1`/`Dt2`) buraya da taşındı. **Fix:**
  `Config_03_SingleTraderOpt.csx`'e aynı 5 alan eklendi,
  `03_RunSingleTraderOptWithProgressAsync.csx`'te `ReadDataFast(stockDataFullFileName)` çağrısı
  §1'deki 6 parametreli haliyle (`filterMode`/`readDataN1`/`readDataN2`/`dt1`/`dt2`) değiştirildi.

**§3 durumu: 🔴 kritik hata, her iki orijinal 🟡 açık fark, ve checklist sırasında bulunan iki YENİ
fark (MarketType, EquityCurveFilter) — hepsi düzeltildi. Artık kalan tek fark ⚪ (kasıtlı)
config-kaynağı farkı ve altındaki "muhtemel dead code" notu.**

### ✅ Düzeltildi (2026-08-25, parite kontrol listesi eklenirken bulunup aynı gün kapatıldı)

- **TradeParams'ın `MarketType`'ı dahil TAM hali script'e hiç aktarılmıyordu.** Menü tarafı
  `AppConfigApplier.ApplySingleTraderOpt()` içinde hem `SetSingleTraderOptTradeParamsConfig(...)`
  (sadece `IlkBakiye`/`KontratSayisi`/`KomisyonCarpan`/`KaymaMiktari`) HEM DE
  `algoTrader.SetSingleTraderTradeParams(BuildInitialTradeParams(cfg.TradeParams))` (`MarketType`
  dahil TAM `InitialTradeParams`) çağırıyor (`AppConfigApplier.cs:890,892-898`). Script sadece
  birincisini yapıyordu — `SetSingleTraderTradeParams()` hiç çağrılmıyordu, dolayısıyla
  `SingleTraderOptimizer.TradeParamsOverride` null kalıyor ve `SingleTraderOptimizer.cs:233-236`
  içindeki **"ViopEndex fallback"** (`SetKontratParamsViopEndex`) devreye giriyordu —
  `MarketType`/`HisseSayisi`/`PyramidingEnabled` AppConfig.json'daki değerden BAĞIMSIZ, script HER
  ZAMAN ViopEndex-tarzı kontrat bazlı pozisyon büyüklüğü hesabı kullanıyordu. **Fix:**
  `AppConfigApplier.BuildInitialTradeParams(TradeParamsConfig)` zaten `public static` olduğu için
  script'ten doğrudan çağrılabiliyor — `Config_03_SingleTraderOpt.csx`'e `marketType`/`lotSayisi`/
  `hisseSayisi`/`pyramidingEnabled` eklendi (varsayılan `marketType="ViopEndex"` — önceki fallback
  davranışıyla aynı, yani varsayılan çalışma şekli DEĞİŞMEDİ, sadece artık config'ten kontrol
  edilebiliyor), `03_RunSingleTraderOptWithProgressAsync.csx`'te
  `algoTrader.SetSingleTraderTradeParams(AppConfigApplier.BuildInitialTradeParams(new
  TradeParamsConfig { ... }))` çağrısı eklendi (`using AlgoTrade.Core.AppConfig;` ile) —
  `AppConfigApplier.ApplySingleTraderOpt()` (satır 890) ile birebir aynı yol.

- **EquityCurveFilter script'te hiç yoktu.** Menü tarafı `cfg.EquityCurveFilter is not null` ise
  `ConfigureEquityCurveFilterFromConfig(...)` çağırıyor (`AppConfigApplier.cs:900-906`) — yani
  `AppConfig.json`'da bu bölüm doluysa optimizasyon test trader'ları ECF filtreli çalışıyor.
  Script'te ECF'e dair hiçbir kod yoktu — optimizasyon her zaman ECF'siz çalışıyordu. **Fix:**
  `Config_03_SingleTraderOpt.csx`'e `ecfEnabled`/`ecfConfigFile`/`ecfVersion` eklendi (varsayılan
  `ecfEnabled=false` — önceki davranışla aynı, ECF hiç yüklenmez).
  `03_RunSingleTraderOptWithProgressAsync.csx`'te `ecfEnabled` ise
  `ClearEquityCurveFilterConfigs()` + `ConfigureEquityCurveFilterFromConfig(ecfPath, ecfVersion,
  id: 0)` çağrıları eklendi — `AppConfigApplier.ApplySingleTraderOpt()` (satır 900-906) ile birebir
  aynı yol. Not: SS1/SS2'deki basit `ecfEnabled`/`ecfThresholdTypeIsPercent`/... alanlarından FARKLI
  bir mekanizma — optimizer ECF'yi doğrudan değerlerden değil, `EquityCurveFilterConfig.txt`
  dosyasından `Id=0` üzerinden "stored config" olarak okuyor (`AlgoTrader.cs:2894-2896`), bu yüzden
  script de dosya+versiyon tabanlı yükleme kullanıyor.

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
