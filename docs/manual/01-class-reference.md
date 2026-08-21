# Class Reference — Kim Ne Yapar

> Bu doküman, AlgoTrade'in "SDK/User Manual" setinin ilk parçası. Amaç: proje büyüdükçe
> (özellikle Confirming* alt sistemi ve 12 Scanner sınıfı eklendikten sonra) hangi sınıfın ne işe
> yaradığını, hangi public API'yi sunduğunu ve nasıl kullanıldığını tek yerden takip edebilmek.
>
> **Ne zaman güncellenmeli**: yeni bir public metod/property eklendiğinde, bir sınıfın rolü
> değiştiğinde, veya [docs/PROJECT_ANALYSIS.md](../PROJECT_ANALYSIS.md) güncellenirken (o daha
> derin bir davranışsal analiz, bu doküman ona bakmadan da "hangi metodu çağırayım" sorusuna
> cevap verebilmeli). Analiz tarihi: 2026-08-21, kod tabanı 147 `.cs` dosyası üzerinden.
>
> Bu dosya SADECE public API yüzeyini (property + metod imzaları) ve kullanım akışını anlatır —
> iç implementasyon detayları, bilinen sorunlar, ölü kod adayları için
> [docs/PROJECT_ANALYSIS.md](../PROJECT_ANALYSIS.md)'ye bakın.

## İçindekiler

1. [AlgoTrader — Orkestratör/Facade](#1-algotrader--orkestratörfacade)
2. [SingleTrader — Çekirdek Motor](#2-singletrader--çekirdek-motor)
3. [MultipleTrader — Çoklu Strateji + Consensus](#3-multipletrader--çoklu-strateji--consensus)
4. [ConfirmingSingleTrader / ConfirmingMultipleTrader / VirtualPositionConfirmer](#4-confirmingsingletrader--confirmingmultipletrader--virtualpositionconfirmer)
5. [SingleTraderOptimizer — Grid-Search Optimizasyon](#5-singletraderoptimizer--grid-search-optimizasyon)
6. [IndicatorManager — İndikatör Merkezi Girişi](#6-indicatormanager--i̇ndikatör-merkezi-girişi)
7. [StrategyRegistry / QueryRegistry — Auto-Discovery](#7-strategyregistry--queryregistry--auto-discovery)
8. [Scanner Ailesi (12 sınıf) — Toplu Tarama](#8-scanner-ailesi-12-sınıf--toplu-tarama)

---

## 1. AlgoTrader — Orkestratör/Facade

**Dosya**: `src/AlgoTrade.Core/Trading/AlgoTrader.cs` (3374 satır)

**Rolü**: Tüm trading alt sisteminin tek giriş noktası. `MarketDataProvider`'dan türer (veri
tutma/okuma ortak), `IDisposable`. Strategy/Query/EquityCurveFilter/Optimization
konfigürasyonlarını toplar, altındaki `SingleTrader`/`MultipleTrader`/`ConfirmingSingleTrader`/
`ConfirmingMultipleTrader`/`SingleTraderOptimizer` instance'larını yaratıp çalıştırır. Console
menüleri ve `.csx` scriptler neredeyse her zaman bir `AlgoTrader` örneği üzerinden çalışır.

**Ne zaman kullanılır**: Neredeyse her zaman — Console'daki her menü ve her `.csx` script bir
`AlgoTrader` örneği yaratıp onun üzerinden çalışır. Doğrudan `SingleTrader`/`MultipleTrader`
kurmak (bkz. §2/§3) sadece scripting'te tam kontrol istendiğinde (örn.
`CustomConsensusExample.csx`) tercih edilir.

### Property Grupları

| Grup | Örnekler |
|---|---|
| Kimlik | `Name`, `SymbolName`, `SymbolPeriod`, `SystemId/Name`, `StrategyId/Name`, `QueryId/Name` |
| Çalışan alt-trader erişimi (private set + public getter) | `SingleTrader`, `MultipleTrader`, `ConfirmingSingleTrader`, `ConfirmingMultipleTrader`, `SingleTraderOptimizer` |
| Registry/keşif | `AvailableStrategies`, `AvailableQueries` (reflection ile bulunan tüm strateji/sorgu isimleri) |
| Config listeleri (MultipleTrader için) | `StrategyConfigs`, `QueryConfigs`, `EquityCurveFilterConfigs`, `ChildTraderConfigs`, `OptimizationParameterRanges` |
| EquityCurveFilter (legacy fallback) | `EquityCurveFilteringEnabled`, `ThresholdTypeIsPercent`, `ProfitConfirmationThreshold`, `LossConfirmationThreshold`, `ConfirmationTrigger` |
| Python | `PythonDll` |

### Public API — Konfigürasyon (tek strateji/sorgu)

- `ConfigureStrategy(strategyName, parameters)` / `ConfigureStrategyFromConfig(configFilePath, strategyName, version?)` — SingleTrader için tek strateji seçer.
- `ConfigureQuery(queryName, parameters)` / `ConfigureQueryFromConfig(...)` — SingleTrader için tek sorgu seçer, `QueryIsEnabled=true` yapar.
- `SetQueryEnabled(bool)` — sorgu katmanını manuel aç/kapat.
- `ConfigureEquityCurveFilterFromConfig(configFilePath, version, id=0)`.

### Public API — Çoklu konfigürasyon (MultipleTrader için)

- `AddStrategyConfig(id, name, parameters)` / `ClearStrategyConfigs()` / `ConfigureStrategiesFromConfig(configFilePath, selections)` — dosyadan seçilen (name, version) çiftlerini id sırasına göre ekler.
- `AddQueryConfig(...)` / `ClearQueryConfigs()` / `ConfigureQueriesFromConfig(...)` — Strategy ile birebir aynı desen.
- `AddEquityCurveFilterConfig(id, enabled, thresholdTypeIsPercent, profitThreshold, lossThreshold, trigger)` / `ClearEquityCurveFilterConfigs()`.
- `AddChildTraderConfig(entry)` / `ClearChildTraderConfigs()` / `SetChildTraderCount(count, configure?)` — N adet `ChildTraderConfigEntry` üretir; `_strategyConfigs.Count == count` ise 1-1 eşler, değilse hepsi ilk stratejiyi kullanır.

### Public API — Factory metodları (`.csx` script'ler için)

- `CreateIndicators()` → `IndicatorManager` yaratır (önceki varsa dispose eder).
- `CreateConfiguredStrategy(indicators)` — `ConfigureStrategy(...)` ile ayarlanmış stratejiyi örnekler.
- `CreateConfiguredQuery(indicators)` — aynısı sorgu için, `QueryIsEnabled=false` ise `null` döner.
- `CreateStrategyFromRegistry(data, indicators, strategyName, parameters)` — id/config listesine hiç dokunmadan, doğrudan registry'den herhangi bir stratejiyi anlık örnekler (bkz. `CustomConsensusExample.csx`'teki kullanım).
- `GetStrategy(id)` / `GetQuery(id)` — `_strategyConfigs`/`_queryConfigs` listesinden id ile örnekler (MultipleTrader child'ları için).

### Public API — Override config setleri (AppConfig'ten `AppConfigApplier` enjekte eder)

`SetSingleTraderTradeParams`, `SetSingleTraderSignalsConfig`, `SetSingleTraderSaveConfig`,
`SetSingleTraderPlotConfig`, `SetSingleTraderExportConfig`, `SetSingleTraderOptimizationConfig`,
`SetMultipleTraderSaveConfig`, `SetMultipleTraderConsensusConfig`,
`SetConfirmingSingleTraderSaveConfig`, `SetConfirmingSingleTraderConfirmationConfig`,
`SetConfirmingSignalTraderSignalsConfig/SaveConfig/PlotConfig/ExportConfig`,
`SetConfirmingMultipleTraderSaveConfig/ConfirmationConfig`,
`SetSingleTraderOptRangeConfig/OptTradeParamsConfig/OptLogConfig/OptSortOutputConfig`.
Script yazarken bunlara genelde gerek yok — bunlar `AppConfig.json` → `AppConfigApplier` →
`AlgoTrader` zincirinin parçası; script kendi `SingleTrader`/`MultipleTrader`'ını manuel kurarsa
(bkz. §2/§3) doğrudan trader'ın kendi property'lerini set eder.

### Public API — Optimizasyon

`AddOptimizationParameterRange(name, min, max, step)`, `ClearOptimizationParameterRanges()`,
`SetOptimizationStrategyFactory(factory)`, `ConfigureOptimizationFromConfig(configFilePath,
strategyName, version?)`.

### Public API — Çalıştırma (async, hepsi `CancellationToken` alır)

| Metod | Ne yapar |
|---|---|
| `RunSingleTraderWithProgressAsync()` | `SingleTrader(id=0)` yaratır, strateji/sorgu atar, bar-bar çalıştırır |
| `RunMultipleTraderWithProgressAsync()` | `MultipleTrader` + `createChildTraders()` (dinamik, `_childTraderConfigs.Count` kadar) |
| `RunConfirmingSingleTraderWithProgressAsync()` | `ConfirmingSingleTrader` kurar/çalıştırır |
| `RunConfirmingMultipleTraderWithProgressAsync()` | `ConfirmingMultipleTrader` kurar/çalıştırır |
| `RunSingleTraderOptWithProgressAsync()` | `SingleTraderOptimizer` kurar, grid-search çalıştırır |
| `WriteTraderDataToFilesAsync(trader)` | 5 overload (SingleTrader/MultipleTrader/ConfirmingSingleTrader/ConfirmingMultipleTrader) — dosya yazımını Run'dan ayırır, grafik açıkken paralel yazılabilsin diye |
| `PlotSingleTraderData(trader)` / `PlotMultipleTraderData(trader)` | Python'a (eski `PythonPlotter` mekanizması) veri gönderir |
| `SetupPython(runHello=true)` | Python entegrasyonunu başlatır |

### Tipik Kullanım Akışı (Console'un izlediği desen)

1. `new AlgoTrader("AlgoTrader")` → `RegisterLogger(...)` → `RegisterTimer(...)`
2. `SetData(stockDataList)` → `SymbolName`/`SymbolPeriod` ata
3. (AppConfig kullanılıyorsa) `AppConfigApplier.ApplySingleTrader/ApplyMultipleTrader/...` çağrılır — bu, yukarıdaki `Set*Config`/`Configure*FromConfig` metodlarını senin yerine çağırır
4. `Initialize()`
5. `await RunXxxWithProgressAsync()`
6. `await WriteTraderDataToFilesAsync(algoTrader.Xxx)`
7. (isteğe bağlı) `await PlotXxxTraderData(...)`

### DTO/Config Sınıfları (dosya sonunda tanımlı)

`StrategyConfigEntry`, `QueryConfigEntry`, `EquityCurveFilterConfigEntry`,
`OptimizationParameterRangeEntry`, `ChildTraderConfigEntry`, `SingleTraderSignalsConfig`,
`SingleTraderSaveConfig`, `SingleTraderPlotConfig`, `SingleTraderOptimizationConfig`,
`SingleTraderExportConfig`, `MultipleTraderObjectSaveConfig`, `MultipleTraderConsensusConfig`,
`ConfirmingSingleTraderObjectSaveConfig`, `ConfirmingSingleTraderConfirmationConfig`,
`ConfirmingMultipleTraderObjectSaveConfig`, `ConfirmingMultipleTraderConfirmationConfig`,
`SingleTraderOptRangeConfig`, `SingleTraderOptTradeParamsConfig`, `SingleTraderOptLogConfig`,
`SingleTraderOptSortOutputConfig` — bunlar `AppConfig.json`'daki alanlarla birebir eşleşir,
`AppConfigApplier.cs` bu köprüyü kurar.

---

## 2. SingleTrader — Çekirdek Motor

**Dosya**: `src/AlgoTrade.Core/Trading/Traders/SingleTrader.cs` (2692 satır)

**Rolü**: Tek stratejiyi bar-bar çalıştıran, gerçek emir açıp kapatan çekirdek motor. Projenin en
kritik sınıfı — `MultipleTrader`'ın her child'ı, `ConfirmingSingleTrader`'ın hem `signalTrader`'ı
hem `mainTrader`'ı, Scanner'ların içindeki throwaway trader'lar hep birer `SingleTrader`.

**Ne zaman kullanılır**: Tek bir stratejiyi tek bir sembolde çalıştırmak istediğinde (Console
`[2]`/`[5]`). Ayrıca her "çoklu" sistemin (MultipleTrader, Confirming*, Scanner) içindeki asıl
işi yapan birim budur — onları anlamak için önce bunu anlamak gerekir.

### Modül Kompozisyonu

`initialTradeParams`, `signals`, `status`, `flags`, `lists`, `timeUtils`, `karZarar`,
`karAlZararKes`, `statistics` — hepsi `private set + public get` property. `CreateModules()` /
`ResetModules()` / `InitModules()` / `DeleteModules()` dörtlüsüyle yönetilir (genelde elle
çağırmana gerek yok, `Reset()`/`Init()` bunları içeriden tetikler).

### Kimlik ve Kurulum

- `SingleTrader(id, name, data, indicators, logger?)` — constructor.
- `SetStrategy(strategy)` / `SetQuery(query)` — strateji/sorgu enjekte eder.
- `SetCallbacks(onReset, onInit, onRun, onFinal, onBeforeOrder, onNotifySignal, onAfterOrder, onProgress)` → `SingleTrader` döner (fluent), `ClearCallbacks()` de fluent.
- `is_son_yon_a()/_s()/_f()`, `is_prev_yon_a()/_s()/_f()` — son/önceki yönü sorgular (`MultipleTrader.BuildConsensusSignal()`'ın kullandığı API).

### Yaşam Döngüsü

1. `Reset()` — state sıfırlar.
2. attribute'ları set et (`SymbolName`, `initialTradeParams.Reset().SetBakiyeParams(...)...` vb.).
3. `ConfigureUserFlagsOnce()` → sinyal bayraklarını (`AlEnabled` vb.) ilklendirir.
4. `Init()`.
5. Bar-bar döngü: `Run(barIndex)` (RunMode'a göre `ExecuteStrategy → MapStrategyCommandsToTradeCommands → ApplyTimingFilters → ApplyEquityCurveFilter → ResolveFilterDecisions → ExecutePostOrderMethods`).
6. `Finalize()` → `CalculateStatistics()` → `WriteStatisticsToFile(outputDir, inputsDir)`.

### Run() ve Emir Motoru

- `Run(int barIndex)` — `RunMode` (`TradeOnly`/`TradeAndQuery`/`QueryOnly`) dalına göre yukarıdaki zinciri işletir.
- `ExecuteStrategy(barIndex)` → `TradeSignals` — stratejinin `OnStep()`'ini çağırır.
- `ExecuteQuery(barIndex)` → `IReadOnlyList<object>` — sorgunun ürettiği sütun değerleri.
- `MapStrategyCommandsToTradeCommands(strategySignal)` — enum'u `signals.Al/Sat/...` bool'larına çevirir.
- `ApplyTimingFilters(barIndex)` / `CheckOrderTimeEligibility(...)` — 6 FilterMode (saat/tarih/aralık × sadece-başlangıç).
- `ConfigureEquityCurveFilter(isPercent, profitThreshold, lossThreshold, trigger)` / `ApplyEquityCurveFilter(barIndex)` — equity curve tabanlı GİRİŞ sinyali soft-block (bkz. ConfirmingSingleTrader ile farkı için §4).
- `ResolveFilterDecisions(barIndex)` — öncelik sırası: PozKapat > GünSonuPozKapat > Timing hard block > TradeStartBarIndex warmup > EquityCurve soft block.
- `ExecuteOrders(barIndex)` → `int` — asıl emir yürütme (pyramiding, ters yön, slippage dahil, ~720 satır).
- `ExecutePreOrderMethods(barIndex)` / `ExecutePostOrderMethods(barIndex)` — Run() akışının parçalanmış hali (`MultipleTrader.Run()`, `ConfirmingSingleTrader.Run()` bu ikisini ayrı ayrı çağırır ki aradaki adımlara (consensus, konfirmasyon) müdahale edebilsin).
- `CalculateBalance(barIndex)`, `CalculateUnrealizedPnL(barIndex)`.
- `ClosePositionEOD(i, gunSonuPozKapatEnabled=true)` — kullanılıyor; `ClosePositionEOD_2(...)` çağrıldığı yer yok (bkz. PROJECT_ANALYSIS.md §8, ölü kod adayı).

### İstatistik/Rapor

- `CalculateStatistics()` / `CalculatePerformances(bakiyePuan, lotSayisi, varlikAdedCarpani)`.
- `WriteStatisticsToFile(outputDir, inputsDir)` — 12 çıktı türü (Full/Minimal × Stats/Lists × Txt/Csv + Formatted + Performans), `Save*Enabled` flag'leri + `*FileName` property'leriyle kontrol edilir (bkz. §1 `SingleTraderSaveConfig`).
- `TaramaOzeti` (property) → `"{SonYon} | Bar:{N} | KZ:{fiyat} | %:{yüzde}"` — Scanner'ların özet satırı bundan geliyor.
- `SonSinyaldenBeriBarSayisi`, `SonKarZararFiyat`, `SonKarZararYuzde` — hesaplanan property'ler.

### Export (versiyonlu, opsiyonel)

`ExportEnabled`, `ExportConfigFile` (`StatisticsExporterConfig.json`), `ExportVersion` (`"v1"`/`"v2"` vb.) — doluysa `StatisticsExporter` üzerinden ek, config-driven bir export daha yapılır (bkz. `docs/export-adimlar.md` — artık silinmiş, tamamlanmıştı).

---

## 3. MultipleTrader — Çoklu Strateji + Consensus

**Dosya**: `src/AlgoTrade.Core/Trading/Traders/MultipleTrader.cs` (832 satır)

**Rolü**: Birden fazla child `SingleTrader`'ı **her biri kendi sinyaliyle gerçekten trade
ederek** aynı bar üzerinde çalıştırır, sinyallerini bir "consensus" kuralıyla birleştirip tek bir
`mainTrader` (id=-1) ile ayrı bir gerçek emir üretir. Önemli: child'lar sinyal üretip pasif
kalmaz — her biri `SingleTrader.Run()`'ın aynısını çalıştırıp **kendi defterinde** gerçek trade
yapar (bkz. `MultipleTrader.Run()` → `trader.Run(i)`, `SingleTrader.cs:452`). Yani her child'ın
kendi `WriteStatisticsToFile()` çıktısı, o stratejiyi TEK BAŞINA çalıştırsaydın alacağın sonucun
birebir aynısıdır.

**Ne zaman kullanılır**: (a) Gerçekten birden fazla stratejiyi birleştirip TEK bir consensus
sinyaliyle trade etmek istediğinde (Console `[3]`/`[6]`), (b) Aynı sembolde birden fazla
stratejinin performansını YAN YANA karşılaştırmak istediğinde (`WriteMultipleTraderStatistics()`
ile — mainTrader satırını yok sayıp child satırlarına bakarsın; bkz.
[docs/todo.md](../todo.md) "Strateji Karşılaştırma" bölümü).

### Property Grupları

- `Id`, `Data`, `Indicators`, `Traders` (child `SingleTrader` listesi), `IsInitialized`, `CurrentIndex`.
- Consensus: `ConsensusMode` (`"Net"`/`"Majority"`/`"All"`/`"Any"`, varsayılan `"Net"`), `ConsensusMinNetCount` (Net modunda eşik, varsayılan 1).
- **`CustomConsensusFunc`** (`Func<List<SingleTrader>, TradeSignals>?`, **2026-08-21 eklendi**) — doluysa `BuildConsensusSignal()` hardcoded switch'i atlayıp bunu çağırır. Script'ten atanır (bkz. `inputs/scripts/CustomConsensusExample.csx`), `AppConfig.json`'dan set edilemez (Func serialize edilemez).
- `DynamicPositionSizeEnabled` — flag var ama gövdesi TODO, işlevsiz (PROJECT_ANALYSIS.md §8).
- Dosya adları: `MultipleTraderListsTxtFileName/CsvFileName` (bar-bar sinyal listesi), `MultipleTraderStatisticsTxtFileName/CsvFileName` (**2026-08-21 eklendi**, trader-bazlı özet karşılaştırma).

### Kurulum ve Çalıştırma

- `MultipleTrader(id, data, indicators, logger?)` — constructor (parametresiz overload da var).
- `AddTrader(SingleTrader trader)` — child ekler.
- `Reset()` → `Init()` → bar-bar `Run(int i)` → `Finalize()`.
- `Run(i)` akışı: her child için `trader.Run(i)` → sinyalleri say → `BuildConsensusSignal()` → mainTrader'da manuel olarak `ExecutePreOrderMethods → MapStrategyCommandsToTradeCommands → ApplyTimingFilters → ApplyEquityCurveFilter → ResolveFilterDecisions → ExecutePostOrderMethods` (SingleTrader'ın 6 adımlı pipeline'ının aynısı, elle tekrarlanmış).
- `BuildConsensusSignal()` → `TradeSignals` — public, dışarıdan da çağrılabilir (izole test için kullanışlı, bkz. aşağıdaki "Nasıl genişletilir").
- `GetMainTrader()` → `SingleTrader`.

### Dosyaya Yazma

- `WriteMultipleTraderListsToFiles(logDir)` — bar-bar rapor (her bar için tüm trader'ların Yön/Seviye/Sinyal'i yan yana). **Performans raporu DEĞİL.**
- `WriteMultipleTraderStatistics(logDir)` (**yeni**) — mainTrader + her child'ın `GetOptimizationSummary()` özetini (NetProfit/WinRate/ProfitFactor/MaxDrawdown vb.) satır=trader / kolon=metrik formatında tek dosyada listeler. `Finalize()` sonrası çağrılmalı.

### Nasıl Genişletilir: Kendi Consensus Kuralını Yazmak

`inputs/scripts/CustomConsensusExample.csx` çalışan, gerçek 7 referans method içerir
(`NetConsensusReference`/`MajorityConsensusReference`/`AllConsensusReference`/
`AnyConsensusReference` — 4 hazır modun script karşılığı; `FirstChildWinsConsensus`/
`WeightedConsensus`/`BothAgreeConsensus` — özel örnekler). Kendi kuralını yazmak için:

1. `List<SingleTrader> → TradeSignals` imzalı bir method/lambda yaz (`traders[i].is_son_yon_a()/_s()` ile her child'ın son yönüne bakabilirsin).
2. `multipleTrader.CustomConsensusFunc = MyRule;` ata.
3. `[8] Run Script` ile çalıştır.

`Run(i)`'yi hiç çağırmadan, her child'ı manuel `Run(i)` ile çalıştırıp tamamen kendi orkestrasyon
mantığını da yazabilirsin — script tam erişimli modda çalıştığı için (`Scripting/ScriptExecutor.cs`)
hiçbir sınır yok, `CustomConsensusFunc` sadece hazır bir "resmi" enjeksiyon noktası.

---

## 4. ConfirmingSingleTrader / ConfirmingMultipleTrader / VirtualPositionConfirmer

**Dosyalar**: `Traders/ConfirmingSingleTrader.cs` (469 satır), `Traders/ConfirmingMultipleTrader.cs`
(483 satır), `Trading/Core/VirtualPositionConfirmer.cs` (175 satır) — 2026-08-19 eklendi.

**Rolü**: "Sanal pozisyon konfirmasyonu" — bir sinyal geldiğinde hemen gerçek emir açmak yerine,
önce **sanal** olarak takip edip belirli bir kâr/zarar eşiği geçilince gerçek pozisyona geçme.
`SingleTrader.ApplyEquityCurveFilter`'dan **farklı** bir mekanizma — o equity-curve tabanlı bir
soft-block, bu ise sinyal-bazlı bir virtual-then-real state machine.

**Ne zaman kullanılır**: Ham stratejinin ürettiği her sinyali hemen trade etmek yerine, "önce
biraz kâr/zarar potansiyelini gör, sonra karar ver" davranışı istediğinde. Console `[22]`-`[25]`.

### Mimari

Her ikisinde de bir **signal katmanı** (ham sinyali üreten, gerçek stratejiyle çalışan) ve bir
**mainTrader** (sadece konfirme edilmiş sinyali alıp gerçek emri açan) var:

- `ConfirmingSingleTrader`: `_signalTrader` (tek `SingleTrader`) + `_mainTrader`.
- `ConfirmingMultipleTrader`: `_signalMultipleTrader` (tam bağımsız çalışan bir `MultipleTrader`,
  N child + consensus — §3'teki sınıfın kendisi, hiç değiştirilmeden reuse edilmiş) + `_mainTrader`.

Aradaki köprü `VirtualPositionConfirmer` — sinyal geldiğinde sanal pozisyon açar (yön + giriş
fiyatı + confirm durumu), kâr/zarar eşiği (`ProfitThreshold`/`LossThreshold`, değer veya yüzde)
geçilince mainTrader'a gerçek emri tetikler, geçilmezse sanal pozisyonu iptal eder.

### VirtualPositionConfirmer — Ortak Konfirmasyon Motoru

- `SignalConflictMode` enum: `CancelAndRestart` / `LockAndIgnore` (sanal pozisyon beklerken ters sinyal gelirse ne olur).
- Property'ler: `ThresholdIsPercentage`, `ProfitThreshold` (varsayılan 5000), `LossThreshold` (varsayılan -3000), `Trigger` (`ConfirmationTrigger`: ProfitOnly/LossOnly/Both), `ConflictMode`, `FlattenImmediatelyOnFlatSignal` (varsayılan true).
- `Reset()`, `Resolve(currentYon, rawSignal, currentPrice)` → `TradeSignals` — asıl karar mantığı.
- Hem `ConfirmingSingleTrader` hem `ConfirmingMultipleTrader` bu sınıfı kompozisyonla kullanır (`_confirmer` alanı), property'leri kendi üzerlerinden pass-through olarak da açarlar (`trader.ProfitThreshold` gibi doğrudan erişim için).

### ConfirmingSingleTrader

- `ConfirmingSingleTrader(id, data, indicators, logger?)`, `SetStrategy(strategy)`.
- `Reset()` → `Init()` → `Run(i)` → `Finalize()`.
- `GetMainTrader()` / `GetSignalTrader()` → `SingleTrader`.
- Çıktı: `ConfirmingSingleTraderLists.txt/.csv` (signalTrader/sanal/mainTrader kolonları yan yana, bar-bar).

### ConfirmingMultipleTrader

- `ConfirmingMultipleTrader(id, data, indicators, logger?)`, `AddTrader(trader)` (signal katmanına child ekler).
- `ConsensusMode`/`ConsensusMinNetCount` — `_signalMultipleTrader`'a pass-through.
- `VirtualSignals` → `_signalMultipleTrader.GetMainTrader().lists.SinyalList` (ham vs. konfirme edilmiş sinyali karşılaştırmak için).
- `GetMainTrader()` / `GetSignalMultipleTrader()` → sırasıyla `SingleTrader`/`MultipleTrader`.
- Çıktı: `ConfirmingMultipleTraderLists.txt/.csv` + (opsiyonel, `WriteSignalMultipleTraderListsToFiles=true` ise) signal katmanının kendi `MultipleTraderLists`/`MultipleTraderStatistics` dosyaları da (bkz. §3) — `FilePrefix` (varsayılan `"ConfirmingMultipleTrader"`) ile önekleniyor.

---

## 5. SingleTraderOptimizer — Grid-Search Optimizasyon

**Dosya**: `src/AlgoTrade.Core/Trading/Traders/SingleTraderOptimizer.cs` (934 satır)

**Rolü**: Bir stratejinin parametre uzayını (`ParameterRange` listesi) kartezyen çarpımla tarayıp
her kombinasyon için ayrı bir `SingleTrader` çalıştırır, sonuçları sıralı dosyaya yazar.

**Ne zaman kullanılır**: "Bu stratejinin en iyi period/multiplier kombinasyonu hangisi?" sorusuna
cevap ararken. Console `[4]`/`[7]`. Not: FARKLI stratejileri karşılaştırmaz (bkz. §3'teki
`MultipleTrader` + `WriteMultipleTraderStatistics` — o iş için kullanılan yol).

### Ana Tipler

- `ParameterRange(name, min, max, step)` → `GetValues()` (taranacak değer listesi).
- `OptimizationResult` — `Parameters` (o kombinasyonun değerleri) + `Values`
  (`GetOptimizationSummary()` map'i) + convenience getter'lar: `NetProfit`, `WinRate`,
  `ProfitFactor`, `ProfitFactorNet`, `MaxDrawdown`, `ScoreFiyatNet`, `ScoreFiyat`, `ScorePuan`,
  `StrategyName`.
- `StrategyFactory` delegate: `(data, indicators, parameters) → IStrategy`.

### Public API

- `SingleTraderOptimizer(id, data, indicators, logger?)`.
- `AddParameterRange(name, min, max, step)`, `SetStrategyFactory(factory)`.
- `Reset()` → `Init()`.
- `GenerateParameterCombinations()` → `List<Dictionary<string,object>>` — recursive backtracking, kombinasyon patlaması için sınır YOK (dikkat).
- `Run(cancellationToken?)` → `OptimizationResult?` — her kombinasyon için `createSingleTrader()` + `runSingleTrader(...)`, sonuçları biriktirir/dosyaya yazar (anlık append veya zaman-aralıklı buffer, `FileFlushIntervalMs`'e göre).
- `GetBestResult()` → `OptimizationResult?` — **sadece `NetProfit`'e göre** sıralar (dosya çıktısındaki `SortField`'den farklı olabilir — bilinen tutarsızlık, bkz. PROJECT_ANALYSIS.md §8).
- `WriteSortedFiles()` — CSV cache'den okuyup `SortField`'e göre sıralı ayrı dosya üretir.
- Event'ler: `OnOptimizationProgress`, `OnSingleTraderProgressCallback`, `OnReadOptimizationResultsFile`, `OnSaveResults`.
- `OptimizationFrom`/`OptimizationTo` — PartialOpt desteği (kesintiye uğrayan uzun taramaları parça parça devam ettirmek için).

---

## 6. IndicatorManager — İndikatör Merkezi Girişi

**Dosya**: `src/AlgoTrade.Core/Trading/Indicators/IndicatorManager.cs` (296 satır) — asıl
hesaplama kodu `Trading/Indicators/**` altında 50 dosyaya, ~6068 satıra yayılı.

**Rolü**: 111+ indikatör hesaplama metoduna (66 MA + ~45 diğer) tek noktadan erişim. `MarketDataProvider`'dan türer.

**Ne zaman kullanılır**: Bir strateji/sorgu yazarken (`OnInit()` içinde `Indicators.<Kategori>.<Method>(...)` çağırırsın) veya doğrudan script'ten indikatör değeri okumak istediğinde.

### Alt Yöneticiler (8 kategori, hepsi public property)

| Property | Sınıf | İçerik |
|---|---|---|
| `MA` | `MovingAverageCalculator` | 66 MA metodu (SMA/EMA/... → FRAMA/MAMA/... egzotik) |
| `Trend` | `TrendIndicators` | SuperTrend, MOST, ADX, ParabolicSAR, Aroon, Vortex, Ichimoku, AlphaTrend, OTT, PTT, HOTTLOTT, PMax, MavilimW |
| `Momentum` | `MomentumIndicators` | RSI, MACD, Stochastic, CCI, WilliamsR, ROC, OTTO, StochasticOTT |
| `Volatility` | `VolatilityIndicators` | ATR, BollingerBands, KeltnerChannel, DonchianChannel |
| `VolumeInd` | `VolumeIndicators` | OBV, VWAP, MFI, CMF |
| `PriceAction` | `PriceActionIndicators` | HigherHighLowerLow, SwingPoints, ZigZag, Fractals, HHV/LLV |
| `SupportResistance` | `SupportResistanceIndicators` | Fibonacci + 12 Pivot Points varyantı |
| `Utils` | `PriceUtils` | HHV, LLV, Sum, StdDev, Mean, Variance, TrueRange, Diff, PercentChange |

### Public API

- `IndicatorManager(config?)` / `IndicatorManager(data, config?)` — constructor.
- `SetData(data)` → `IndicatorManager` (fluent).
- `Reset()`.
- `GetCachedIndicators()` → `IReadOnlyDictionary<string,double[]>` — cache'teki tüm sonuçlar (Python plotter'a aktarım için kullanılıyor).
- `GetCacheStats()`, `ClearCache()`.
- `Dispose()`.

Cache: `CacheSize` (varsayılan 128) dolunca yeni sonuçlar cache'lenmez (LRU yok — uzun
optimizasyon taramalarında dikkat, bkz. PROJECT_ANALYSIS.md §11).

---

## 7. StrategyRegistry / QueryRegistry — Auto-Discovery

**Dosyalar**: `Trading/Strategies/StrategyRegistry.cs` (288 satır), `Trading/Queries/QueryRegistry.cs`
(287 satır) — birebir aynı desen, biri `IStrategy`/`BaseStrategy` için biri `IQuery`/`BaseQuery` için.

**Rolü**: Reflection tabanlı factory. Yeni bir strateji/sorgu sınıfı yazıp `BaseStrategy`/
`BaseQuery`'den türetmen yeterli — **elle kayıt gerekmiyor**.

**Ne zaman kullanılır**: Doğrudan çağırman nadiren gerekir (genelde `AlgoTrader` üzerinden
dolaylı kullanılır — `GetStrategy(id)`, `CreateConfiguredStrategy(...)` vb. içeriden bunu
kullanır). Yeni bir strateji/sorgu sınıfı eklerken bilmen gereken: registry seni otomatik bulur,
tek şart doğru base class'tan türetmek.

### Public API (ikisi de aynı)

- Constructor → `AutoRegister()`'ı otomatik çağırır.
- `AutoRegister(assembly?)` — verilen (veya current) assembly'de ilgili base class'tan türeyen
  non-abstract tüm class'ları `TypeName → Type` (case-insensitive) kaydeder.
- `GetStrategyNames()` / `GetQueryNames()` → `IReadOnlyCollection<string>`.
- `CreateStrategy(data, indicators, logger, name?, parameters?)` / `CreateQuery(...)` — isim
  boşsa varsayılana düşer (`"SimpleMAStrategy"`; Query tarafında `"SimpleMAQuery"` fallback'i
  var ama gerçek sınıf adı `SimpleQuery1` — güncel değil, bkz. PROJECT_ANALYSIS.md §5.8), isim
  bulunamazsa `ArgumentException`. Önce statik `Create(...)` factory method aranır (şu an hiçbir
  strateji/sorgu bunu tanımlamıyor), yoksa en çok parametre eşleşen constructor seçilir.

### Yeni Bir Strateji/Sorgu Eklemek İçin (adım adım)

1. `BaseStrategy` (`Trading/Strategy/BaseStrategy.cs`) veya `BaseQuery` (`Trading/Query/BaseQuery.cs`)'den türet.
2. İki constructor yaz: parametresiz-veri (sadece `Parameters` doldurur) ve parametreli-veri (`List<StockData> data, IndicatorManager indicators, ...` alıp `Initialize()` çağırır).
3. `OnInit()`'te `Indicators.<Kategori>.<Method>(...)` ile ilgili indikatörü hesapla, alanda sakla.
4. `OnStep(int currentIndex)`'te (Strategy) `TradeSignals` üret veya (Query) sütun değerlerini üret.
5. Derle — registry çalışma zamanında otomatik bulur, hiçbir yere elle kayıt eklemene gerek yok.
6. (Opsiyonel) `inputs/configs/StrategyConfig.txt`/`QueryConfig.txt`'e bir satır ekleyip config-dosyasından yükleme desteği kazandır.

---

## 8. Scanner Ailesi (12 sınıf) — Toplu Tarama

**Dosyalar**: `src/AlgoTrade.Core/Trading/Traders/*Scanner.cs` — 12 sınıf, ortak isimlendirme
deseni: `[Multi][Query|Strategy-implicit][Symbol][Timeframe]Scanner`. Ayrıca `MultipleQuery.cs`
(sorgu tarafının `MultipleTrader` karşılığı — birden fazla sorguyu birleştirmeden bağımsız
çalıştırır).

**Rolü**: Toplu sembol/zaman-dilimi taraması. Tek bir stratejiyi/sorguyu (veya "Multi" varyantında
birden fazlasının bağımsız sonucunu) N sembol × M zaman dilimi üzerinde otomatik çalıştırıp özet
tabloya yazar.

**Ne zaman kullanılır**: "Bu strateji hangi sembollerde/zaman dilimlerinde çalıştı, hangisinde
en iyi sonucu verdi?" sorusuna cevap ararken. Console `[10]`-`[21]`.

### Ortak Desen (SymbolScanner üzerinden örnek — Strateji tarafı, tek eksen)

- `Options` sınıfı (örn. `SymbolScanOptions`): `DataFolder`, `AutoDiscover`/`SymbolList`,
  `StrategyName` + `StrategyParameters`, `TradeParams` (`InitialTradeParams`), sinyal
  flag'leri (`AlEnabled` vb.), `ReadFilterMode`/`N1`/`N2`/`Dt1`/`Dt2`, `SortField`/`SortDescending`.
- `Result` sınıfı (örn. `ScanResult`): `Symbol`, `Success`, `ErrorMessage`, `BarCount`,
  `StatisticsDataRow`, `OptimizationSummary` (Dictionary), `SonYon`/`SonKarZararFiyat`/
  `SonKarZararYuzde`/`TaramaOzeti`, "Multi" varyantlarında ek olarak `ChildSignals`.
- `Scanner(logger)` → `Run(options, csvPath, txtPath, ct?)` (her sembol/TF için taze throwaway
  `AlgoTrader`+`SingleTrader` veya `AlgoTrader`+`MultipleTrader` kurup çalıştırır, fail-soft: bir
  sembolde hata olursa `Success=false` işaretlenip tarama devam eder) → `Results` (public liste) +
  `WriteSortedResults(...)` + `GetBestResult(...)`.
- `OnProgress` event: `(işlenen sıra, toplam, sembol adı)`.

**"Multi" varyantı örneği** (`MultiStrategySymbolScanner`, konsensüs tarafı): aynı desen ama tek
strateji yerine `MultipleTrader` (Yapı Taşı B — birden fazla stratejinin consensus'u) kullanır,
`ConfigureAlgoTrader` delege'iyle wiring dışarıdan (genelde `AppConfigApplier.ApplyMultipleTrader`)
yapılır — sınıf kendisi consensus/config detayını bilmez.

### 12 Sınıfın Tablosu

| Sınıf | Eksen(ler) | Kaç boyutlu | Strateji/Sorgu |
|---|---|---|---|
| `SymbolScanner` | Sembol | 1 | Tek strateji |
| `TimeframeScanner` | Zaman dilimi (aynı sembol) | 1 | Tek strateji |
| `SymbolTimeframeScanner` | Sembol × Zaman dilimi (ikisi de bağımsız) | 2 | Tek strateji |
| `MultiStrategySymbolScanner` | Sembol | 1 | `MultipleTrader` consensus |
| `MultiStrategyTimeframeScanner` | Zaman dilimi | 1 | `MultipleTrader` consensus |
| `MultiStrategySymbolTimeframeScanner` | Sembol × Zaman dilimi | 2 | `MultipleTrader` consensus |
| `QuerySymbolScanner` | Sembol | 1 | Tek sorgu |
| `QueryTimeframeScanner` | Zaman dilimi | 1 | Tek sorgu |
| `QuerySymbolTimeframeScanner` | Sembol × Zaman dilimi | 2 | Tek sorgu |
| `MultiQuerySymbolScanner` | Sembol | 1 | `MultipleQuery` (birleştirilmeden bağımsız) |
| `MultiQueryTimeframeScanner` | Zaman dilimi | 1 | `MultipleQuery` |
| `MultiQuerySymbolTimeframeScanner` | Sembol × Zaman dilimi | 2 | `MultipleQuery` |

Detaylı Console menü eşlemesi ve senaryo numaraları için [docs/todo.md](../todo.md) "Tarama
Motorları" bölümüne bakın.

---

## İlgili Dosyalar

- [docs/PROJECT_ANALYSIS.md](../PROJECT_ANALYSIS.md) — daha derin davranışsal analiz: bilinen
  sorunlar, ölü kod adayları, tutarsızlıklar, dosya/satır sayıları.
- [docs/migration-guide.md](../migration-guide.md) — eski projeden taşıma durumu, roadmap
  maddelerinin gerçek durumu.
- [docs/todo.md](../todo.md) — açık işler, Scanner/Scripting/Confirming implementasyon notları.
- `inputs/scripts/readme.txt` — script dosyalarının kategorize edilmiş listesi.
