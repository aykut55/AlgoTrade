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

- §1 — [AlgoTrader — Orkestratör/Facade](#1-algotrader--orkestratörfacade)
- §2 — [StockDataReader — Veri Okuma (Read Data, Menü [1])](#2-stockdatareader--veri-okuma-read-data-menü-1) — özet burada, tam referans [ayrı sayfada](classes/09-stockdatareader.md)
- §3 — [SingleTrader — Çekirdek Motor](#3-singletrader--çekirdek-motor) — özet burada, tam referans [ayrı sayfada](classes/02-singletrader.md)
- §4 — [MultipleTrader — Çoklu Strateji + Consensus](#4-multipletrader--çoklu-strateji--consensus) — özet burada, tam referans [ayrı sayfada](classes/03-multipletrader.md)
- §5 — [SingleTraderOptimizer — Grid-Search Optimizasyon](#5-singletraderoptimizer--grid-search-optimizasyon) — özet burada, tam referans [ayrı sayfada](classes/05-singletraderoptimizer.md)
- §6 — [ConfirmingSingleTrader — Sanal Pozisyon Konfirmasyonu](#6-confirmingsingletrader--sanal-pozisyon-konfirmasyonu) — özet burada, tam referans [ayrı sayfada](classes/04-confirmingsingletrader.md)
- §7 — [ConfirmingMultipleTrader — Consensus + Sanal Pozisyon Konfirmasyonu](#7-confirmingmultipletrader--consensus--sanal-pozisyon-konfirmasyonu) — özet burada, tam referans [ayrı sayfada](classes/04-confirmingmultipletrader.md)
- §8 — [VirtualPositionConfirmer — Ortak Konfirmasyon Motoru](#8-virtualpositionconfirmer--ortak-konfirmasyon-motoru) — özet burada, tam referans [ConfirmingSingleTrader sayfasında](classes/04-confirmingsingletrader.md#virtualpositionconfirmer--ortak-konfirmasyon-motoru)
- §9 — [PythonPlotter — pythonnet Tabanlı Görselleştirme (Eski/Varsayılan)](#9-pythonplotter--pythonnet-tabanlı-görselleştirme-eskivarsayılan) — özet burada, tam referans [ayrı sayfada](classes/python-plotter.md)
- §10 — [DearPyGuiDataPlotter — Ayrı Process Tabanlı Görselleştirme (Yeni, Geliştirilmekte)](#10-dearpyguidataplotter--ayrı-process-tabanlı-görselleştirme-yeni-geliştirilmekte) — özet burada, tam referans [ayrı sayfada](classes/dearpyguidataplotter.md)
- §11 — [IndicatorManager — İndikatör Merkezi Girişi](#11-indicatormanager--indikatör-merkezi-girişi)
- §12 — [StrategyRegistry / QueryRegistry — Auto-Discovery](#12-strategyregistry--queryregistry--auto-discovery)
- §13 — [Scanner Ailesi (12 sınıf) — Toplu Tarama](#13-scanner-ailesi-12-sınıf--toplu-tarama)
- §14 — [Script'ler — Numaralı Envanter](#14-scriptler--numaralı-envanter)

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
kurmak (bkz. [Bölüm 3](#3-singletrader--çekirdek-motor)/[Bölüm 4](#4-multipletrader--çoklu-strateji--consensus)) sadece scripting'te tam kontrol istendiğinde (örn.
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
(bkz. [Bölüm 3](#3-singletrader--çekirdek-motor)/[Bölüm 4](#4-multipletrader--çoklu-strateji--consensus)) doğrudan trader'ın kendi property'lerini set eder.

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

## 2. StockDataReader — Veri Okuma (Read Data, Menü [1])

**Dosyalar**: `src/AlgoTrade.Core/StockDataReader/StockDataReader.cs`,
`src/AlgoTrade.Core/DataProvider/MarketDataProvider.cs` (taban sınıf),
`src/AlgoTrade.Core/StockData/StockData.cs` (veri birimi `struct`).

**Rolü**: Disk üzerindeki `;`-ayraçlı CSV/TXT bar verisini okuyup `List<StockData>`'a çevirir.
Console `[1] Read Data` menüsü bu sınıfı iki aşamalı (`ReadMetaData` → `ReadDataFast`) çağırır;
sonucu tüm `AlgoTrader` tabanlı run'lar (`SingleTrader`/`MultipleTrader`/`SingleTraderOptimizer`/
Confirming*, bkz. yukarıdaki [Bölüm 1](#1-algotrader--orkestratörfacade) ve aşağıdaki §3-§7)
veri kaynağı olarak kullanır.

**Ne zaman kullanılır**: Her `AlgoTrader` akışından ÖNCE — Console `[1]` (sadece oku) veya
`[5]`-`[7]`/`[23]`/`[25]` ("Read Data + X" kombo menüleri).

**Detaylı referans** — sınıf iskeleti, tüm Public API, `readStockData()`'nın tam kaynağı,
Console callback metodları, `FilterMode`'un 7 kombinasyonu için bağımsız C#/JSON örnekleri,
ve tüm public üyelerin gerçek kullanım haritası (bu diğer sınıflardan çok daha derin işlendiği
için ayrı sayfada tutuluyor):

**[→ StockDataReader — Veri Okuma (ayrı sayfa)](classes/09-stockdatareader.md)**

---

## 3. SingleTrader — Çekirdek Motor

**Dosya**: `src/AlgoTrade.Core/Trading/Traders/SingleTrader.cs` (2693 satır)

**Rolü**: Tek stratejiyi bar-bar çalıştıran, gerçek emir açıp kapatan çekirdek motor. Projenin en
kritik sınıfı — `MultipleTrader`'ın her child'ı, `ConfirmingSingleTrader`'ın hem `signalTrader`'ı
hem `mainTrader`'ı, 12 Scanner sınıfının içindeki throwaway trader'lar, `MultipleQuery`'nin her
satırı hep birer `SingleTrader` (25 instantiation noktası). Kendi state'ini 9 kompozisyon
modülüne (`signals`/`status`/`flags`/`lists`/`initialTradeParams` vb.) böler.

**Ne zaman kullanılır**: Tek bir stratejiyi tek bir sembolde çalıştırmak istediğinde (Console
`[2]`/`[5]`). Ayrıca her "çoklu" sistemin (MultipleTrader, Confirming*, Scanner) içindeki asıl
işi yapan birim budur — onları anlamak için önce bunu anlamak gerekir.

**Detaylı referans** — sınıf iskeleti, tüm Public API, emir motorunun (`ExecuteOrders`) sinyal-
geçiş tablosu, `AlgoTrader.RunSingleTraderWithProgressAsync()`'in tam kaynağı, `AppConfig.
SingleTrader` eşlemesi, ve tüm public üyelerin gerçek kullanım haritası (bu diğer sınıflardan çok
daha derin işlendiği için ayrı sayfada tutuluyor — 2 önemli bulgu dahil: `ApplyTimingFilters`'ın
hardcoded `filterMode=1`'i ve hiç tetiklenmeyen `OnNotifySignal` event'i):

**[→ SingleTrader — Çekirdek Motor (ayrı sayfa)](classes/02-singletrader.md)**

---

## 4. MultipleTrader — Çoklu Strateji + Consensus

**Dosya**: `src/AlgoTrade.Core/Trading/Traders/MultipleTrader.cs` (833 satır)

**Rolü**: Birden fazla child `SingleTrader`'ı **her biri kendi sinyaliyle gerçekten trade
ederek** aynı bar üzerinde çalıştırır, sinyallerini bir "consensus" kuralıyla (`Net`/`Majority`/
`All`/`Any`, veya script'ten `CustomConsensusFunc`) birleştirip tek bir `mainTrader` (id=-1) ile
ayrı bir gerçek emir üretir. Child'lar sinyal üretip pasif kalmaz — her biri `SingleTrader.Run()`'ın
aynısını çalıştırıp **kendi defterinde** gerçek trade yapar; her child'ın kendi
`WriteStatisticsToFile()` çıktısı, o stratejiyi TEK BAŞINA çalıştırsaydın alacağın sonucun
birebir aynısıdır.

**Ne zaman kullanılır**: (a) Gerçekten birden fazla stratejiyi birleştirip TEK bir consensus
sinyaliyle trade etmek istediğinde (Console `[3]`/`[6]`), (b) Aynı sembolde birden fazla
stratejinin performansını YAN YANA karşılaştırmak istediğinde (`WriteMultipleTraderStatistics()`
ile), (c) hazır 4 consensus modu yetmiyorsa script'ten `CustomConsensusFunc` ile kendi kuralını
yazmak istediğinde.

**Detaylı referans** — sınıf iskeleti, `BuildConsensusSignal()`'ın tam kaynağı ve davranış
tablosu, `AlgoTrader.RunMultipleTraderWithProgressAsync()`'in tam kaynağı, `createChildTraders()`
akışı, `AppConfig.MultipleTrader` eşlemesi, `CustomConsensusExample.csx` üzerinden script'ten
manuel kurulum, ve tüm public üyelerin gerçek kullanım haritası (bu diğer sınıflardan çok daha
derin işlendiği için ayrı sayfada tutuluyor — dikkat çeken bulgular: `DynamicPositionSizeEnabled`
işlevsiz, mainTrader'ın `OnRun` event'i hiç tetiklenmiyor, `MultipleTrader::PlotEnabled` hiç
okunmuyor):

**[→ MultipleTrader — Çoklu Strateji + Consensus (ayrı sayfa)](classes/03-multipletrader.md)**

---

## 5. SingleTraderOptimizer — Grid-Search Optimizasyon

**Dosya**: `src/AlgoTrade.Core/Trading/Traders/SingleTraderOptimizer.cs` (935 satır)

**Rolü**: Bir stratejinin parametre uzayını (`ParameterRange` listesi) kartezyen çarpımla tarayıp
her kombinasyon için ayrı, throwaway bir `SingleTrader` çalıştırır, sonuçları sıralı dosyaya
yazar.

**Ne zaman kullanılır**: "Bu stratejinin en iyi period/multiplier kombinasyonu hangisi?" sorusuna
cevap ararken. Console `[4]`/`[7]`. Not: FARKLI stratejileri karşılaştırmaz (bkz. [Bölüm 4](#4-multipletrader--çoklu-strateji--consensus)'teki
`MultipleTrader` + `WriteMultipleTraderStatistics` — o iş için kullanılan yol).

**Detaylı referans** — sınıf iskeleti, `Run()`'ın tam kaynağı, `createSingleTrader()`'ın her
kombinasyon için trader kurulumu, `AlgoTrader.RunSingleTraderOptWithProgressAsync()`'in tam
kaynağı, `AppConfig.SingleTraderOptimizer` eşlemesi, ve tüm public üyelerin gerçek kullanım
haritası (dikkat çeken bulgular: optimizasyon ilerleme callback'leri tamamen yorum satırı,
`SaveEveryN` işlevsiz, `AppConfig.SingleTraderOptimizer.SingleTrader.*` — "best trader" ayarları —
hiçbir yeniden-koşum tarafından okunmuyor):

**[→ SingleTraderOptimizer — Grid-Search Optimizasyon (ayrı sayfa)](classes/05-singletraderoptimizer.md)**

---

## 6. ConfirmingSingleTrader — Sanal Pozisyon Konfirmasyonu

**Dosya**: `Traders/ConfirmingSingleTrader.cs` (469 satır) + `Trading/Core/VirtualPositionConfirmer.cs`
(bkz. [Bölüm 8](#8-virtualpositionconfirmer--ortak-konfirmasyon-motoru)).

**Rolü**: "Sanal pozisyon konfirmasyonu" — bir sinyal geldiğinde hemen gerçek emir açmak yerine,
önce **sanal** olarak takip edip belirli bir kâr/zarar eşiği geçilince gerçek pozisyona geçme.
`SingleTrader.ApplyEquityCurveFilter`'dan **farklı** bir mekanizma — o equity-curve tabanlı bir
soft-block, bu ise sinyal-bazlı bir virtual-then-real state machine. Bir **`signalTrader`**
(ham sinyali üreten, gerçek bir `SingleTrader`) ve bir **`mainTrader`** (sadece konfirme edilmiş
sinyali alıp gerçek emri açan) var, aradaki köprü [`VirtualPositionConfirmer`](#8-virtualpositionconfirmer--ortak-konfirmasyon-motoru).

**Ne zaman kullanılır**: Ham stratejinin ürettiği her sinyali hemen trade etmek yerine, "önce
biraz kâr/zarar potansiyelini gör, sonra karar ver" davranışı istediğinde. Console `[22]`-`[23]`.

**Detaylı referans** — sınıf iskeleti, `Run()`'ın konfirmasyon akışı, `AlgoTrader.RunConfirmingSingleTraderWithProgressAsync()`'in
tam kaynağı, `AppConfig` eşlemesi, ve tüm public üyelerin gerçek kullanım haritası (dikkat çeken
bulgu: `ConfirmingSingleTraderLists.txt/.csv` çıktısı, projedeki her diğer dosyanın kullandığı
`AppSettings.LogsDir` yerine yanlışlıkla derlenmiş exe'nin yanındaki bir `logs/` klasörüne
yazılıyor):

**[→ ConfirmingSingleTrader (ayrı sayfa)](classes/04-confirmingsingletrader.md)**

---

## 7. ConfirmingMultipleTrader — Consensus + Sanal Pozisyon Konfirmasyonu

**Dosya**: `Traders/ConfirmingMultipleTrader.cs` (483 satır) + `Trading/Core/VirtualPositionConfirmer.cs`
(bkz. [Bölüm 8](#8-virtualpositionconfirmer--ortak-konfirmasyon-motoru)).

**Rolü**: [ConfirmingSingleTrader](#6-confirmingsingletrader--sanal-pozisyon-konfirmasyonu)'ın
`MultipleTrader` karşılığı — tek bir stratejinin ham sinyali yerine **N child stratejinin
consensus (bileşke) sinyalini** sanal pozisyonla konfirme eder. `_signalMultipleTrader` — tam,
bağımsız çalışan gerçek bir [`MultipleTrader`](#4-multipletrader--çoklu-strateji--consensus)
(N child + kendi consensus mantığı, hiç değiştirilmeden reuse edilmiş) — onun kendi mainTrader'ı
ham sinyal kaynağı. Konfirmasyon state machine'i (`VirtualPositionConfirmer`) ConfirmingSingleTrader
ile ORTAK.

**Ne zaman kullanılır**: Birden fazla stratejinin CONSENSUS'unu (tek strateji değil) gerçek
pozisyona çevirmeden önce sanal pozisyonla teyit etmek istediğinde. Console `[24]`-`[25]`.

**Detaylı referans** — sınıf iskeleti, `Run()`'ın konfirmasyon akışı, `AlgoTrader.RunConfirmingMultipleTraderWithProgressAsync()`'in
tam kaynağı, `AppConfig` eşlemesi, ve tüm public üyelerin gerçek kullanım haritası (aynı `logs/`
klasör bulgusu burada da geçerli):

**[→ ConfirmingMultipleTrader (ayrı sayfa)](classes/04-confirmingmultipletrader.md)**

---

## 8. VirtualPositionConfirmer — Ortak Konfirmasyon Motoru

**Dosya**: `src/AlgoTrade.Core/Trading/Core/VirtualPositionConfirmer.cs` (175 satır).

**Rolü**: [ConfirmingSingleTrader](#6-confirmingsingletrader--sanal-pozisyon-konfirmasyonu) VE
[ConfirmingMultipleTrader](#7-confirmingmultipletrader--consensus--sanal-pozisyon-konfirmasyonu)
arasında PAYLAŞILAN, bağımsız bir state machine sınıfı — kod tekrarını önlemek için ayrı dosyaya
çıkarılmış. Sinyal geldiğinde sanal pozisyon açar (yön + giriş fiyatı + confirm durumu), kâr/zarar
eşiği (`ProfitThreshold`/`LossThreshold`, değer veya yüzde) geçilince mainTrader'a gerçek emri
tetikler, geçilmezse sanal pozisyonu iptal eder. `SignalConflictMode` enum'u (`CancelAndRestart`/
`LockAndIgnore`) sanal pozisyon beklerken ters yönlü bir ham sinyal gelirse ne olacağını belirler.

**Ne zaman kullanılır**: Doğrudan kullanılmaz — her iki Confirming* sınıfı bunu kompozisyonla
kullanır (`_confirmer` alanı), property'leri kendi üzerlerinden pass-through olarak da açarlar
(`trader.ProfitThreshold` gibi doğrudan erişim için).

**Detaylı referans** — bu sınıfın ayrı bir sayfası yok, tam kaynağı ve `Resolve()`'un adım adım
karar akışı [ConfirmingSingleTrader sayfasında](classes/04-confirmingsingletrader.md#virtualpositionconfirmer--ortak-konfirmasyon-motoru)
tam olarak ele alınıyor (iki sınıf arasında birebir ortak olduğu için tekrar edilmiyor):

**[→ VirtualPositionConfirmer (ConfirmingSingleTrader sayfasında)](classes/04-confirmingsingletrader.md#virtualpositionconfirmer--ortak-konfirmasyon-motoru)**

---

## 9. PythonPlotter — pythonnet Tabanlı Görselleştirme (Eski/Varsayılan)

**Dosya**: `src/AlgoTrade.Core/Python/PythonPlotter.cs` (692 satır).

**Rolü**: `AlgoTrader`'ın eski/varsayılan plot yolu. pythonnet ile aynı process içinde gömülü
bir Python yorumlayıcısı başlatır, `SingleTrader`/`MultipleTrader` koşum sonuçlarını doğrudan
`PyList`/`PyDict` nesnelerine çevirip Python tarafında (`src/PythonPlotter/`) bir plot penceresi
açtırır.

**Ne zaman kullanılır**: `AppConfig.json`'da ilgili trader'ın `Plot.PlotEnabled=true` olduğunda,
koşum bitince otomatik tetiklenir — Console `[2]`/`[3]`/`[22]`/`[24]`.

**Detaylı referans** — sınıf iskeleti, tam Public API, menüden çağrılma zinciri, ve kullanım
haritası (dikkat çeken bulgu: `PlotOptimizationResults` hiçbir yerden çağrılmıyor,
[SingleTraderOptimizer](#5-singletraderoptimizer--grid-search-optimizasyon) akışına hiç
bağlanmamış):

**[→ PythonPlotter (ayrı sayfa)](classes/python-plotter.md)**

---

## 10. DearPyGuiDataPlotter — Ayrı Process Tabanlı Görselleştirme (Yeni, Geliştirilmekte)

**Dosyalar**: `src/AlgoTrade.Core/Python/DearPyGuiDataPlotter/DearPyGuiDataPlotter.cs`
(276 satır), `TradeDataBundleConverter.cs`.

**Rolü**: [PythonPlotter](#9-pythonplotter--pythonnet-tabanlı-görselleştirme-eskivarsayılan)'ın
YERİNİ almayı hedefleyen, henüz geliştirilmekte olan alternatif. pythonnet ile in-process
ÇALIŞMAZ — ayrı bir Python PROCESS'i başlatır, veriyi `TradeDataBundleConverter` ile `.npz`
bundle + `.view.json` dosya çiftine dönüştürüp dosya-tabanlı runtime komutlarıyla ("load_bundle"
vb.) o process'e iletir.

**Ne zaman kullanılır**: Şu an için sadece Console `[9]` (demo/test menüsü, kod içi TODO'ya göre
silinmesi planlı) ve `[8] Run Script` ile `04_GenerateDearPyGuiDataPlotterBundle.csx`/
`05_RunDearPyGuiDataPlotterTest.csx` script çifti üzerinden. Ayrıca Console `[2]`'de
(`PlotEnabled=true` iken) `PythonPlotter`'a EK olarak, aynı `SingleTrader` verisiyle paralel
tetikleniyor (kod içi TODO: "gerçek `PlotBackend` switch'i gelince bu ikili çalışma
kaldırılacak").

**Detaylı referans** — sınıf iskeleti, tam Public API, menüden/script'ten çağrılma zincirleri,
ve kullanım haritası (dikkat çeken bulgu: `ClearAllPanels`/`ReloadCurrent`/`AddSeriesFromBundle`
komutları tanımlı ama hiçbir akıştan kullanılmıyor):

**[→ DearPyGuiDataPlotter (ayrı sayfa)](classes/dearpyguidataplotter.md)**

---

## 11. IndicatorManager — İndikatör Merkezi Girişi

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
optimizasyon taramalarında dikkat, bkz. [PROJECT_ANALYSIS.md §4.11](../PROJECT_ANALYSIS.md#411-dikkat-edilmesi-gerekenler)).

---

## 12. StrategyRegistry / QueryRegistry — Auto-Discovery

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
  var ama gerçek sınıf adı `SimpleQuery1` — güncel değil, bkz. [PROJECT_ANALYSIS.md §5.8](../PROJECT_ANALYSIS.md#58-query-altyapısı)), isim
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

## 13. Scanner Ailesi (12 sınıf) — Toplu Tarama

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

## 14. Script'ler — Numaralı Envanter

**Dosyalar**: `inputs/scripts/01_*.csx` — `19_*.csx`, Console `[8] Run Script` ile çalıştırılır.

**Rolü**: Bu tablo sadece bir **envanter/eşleme** — hangi numaralı script hangi sınıfı çalıştırıyor.
01-07 zaten ilgili sınıfın "Script'ten Çağrılma" bölümünde derinlemesine anlatıldı (aşağıda link
var). 08-19 (Scanner script'leri) henüz ayrı ayrı incelenip belgelenmedi — bilinçli olarak
ertelendi, burada sadece isim eşlemesi var.

| # | Script Dosyası | İlgili Sınıf |
|---|---|---|
| 01 | `01_RunSingleTraderWithProgressAsync.csx` | [SingleTrader (§3)](classes/02-singletrader.md#tipik-kullanım--scriptten-çağrılma-manuel-kurulum) |
| 02 | `02_RunMultipleTraderWithProgressAsync.csx` | [MultipleTrader (§4)](classes/03-multipletrader.md#tipik-kullanım--scriptten-çağrılma-customconsensusfunc-örneği) |
| 03 | `03_RunSingleTraderOptWithProgressAsync.csx` | [SingleTraderOptimizer (§5)](classes/05-singletraderoptimizer.md#tipik-kullanım--scriptten-çağrılma) |
| 04 | `04_GenerateDearPyGuiDataPlotterBundle.csx` | [DearPyGuiDataPlotter (§10)](classes/dearpyguidataplotter.md#tipik-kullanım--scriptten-çağrılma) |
| 05 | `05_RunDearPyGuiDataPlotterTest.csx` | [DearPyGuiDataPlotter (§10)](classes/dearpyguidataplotter.md#tipik-kullanım--scriptten-çağrılma) |
| 06 | `06_RunConfirmingSingleTraderWithProgressAsync.csx` | [ConfirmingSingleTrader (§6)](classes/04-confirmingsingletrader.md#tipik-kullanım--scriptten-çağrılma) |
| 07 | `07_RunConfirmingMultipleTraderWithProgressAsync.csx` | [ConfirmingMultipleTrader (§7)](classes/04-confirmingmultipletrader.md#tipik-kullanım--scriptten-çağrılma) |
| 08 | `08_RunSymbolScan.csx` | `SymbolScanner` ([§13](#13-scanner-ailesi-12-sınıf--toplu-tarama)) |
| 09 | `09_RunTimeframeScan.csx` | `TimeframeScanner` ([§13](#13-scanner-ailesi-12-sınıf--toplu-tarama)) |
| 10 | `10_RunMultiStrategyTimeframeScan.csx` | `MultiStrategyTimeframeScanner` ([§13](#13-scanner-ailesi-12-sınıf--toplu-tarama)) |
| 11 | `11_RunSymbolTimeframeScan.csx` | `SymbolTimeframeScanner` ([§13](#13-scanner-ailesi-12-sınıf--toplu-tarama)) |
| 12 | `12_RunMultiStrategySymbolScan.csx` | `MultiStrategySymbolScanner` ([§13](#13-scanner-ailesi-12-sınıf--toplu-tarama)) |
| 13 | `13_RunMultiStrategySymbolTimeframeScan.csx` | `MultiStrategySymbolTimeframeScanner` ([§13](#13-scanner-ailesi-12-sınıf--toplu-tarama)) |
| 14 | `14_RunQuerySymbolScan.csx` | `QuerySymbolScanner` ([§13](#13-scanner-ailesi-12-sınıf--toplu-tarama)) |
| 15 | `15_RunQueryTimeframeScan.csx` | `QueryTimeframeScanner` ([§13](#13-scanner-ailesi-12-sınıf--toplu-tarama)) |
| 16 | `16_RunMultiQueryTimeframeScan.csx` | `MultiQueryTimeframeScanner` ([§13](#13-scanner-ailesi-12-sınıf--toplu-tarama)) |
| 17 | `17_RunQuerySymbolTimeframeScan.csx` | `QuerySymbolTimeframeScanner` ([§13](#13-scanner-ailesi-12-sınıf--toplu-tarama)) |
| 18 | `18_RunMultiQuerySymbolScan.csx` | `MultiQuerySymbolScanner` ([§13](#13-scanner-ailesi-12-sınıf--toplu-tarama)) |
| 19 | `19_RunMultiQuerySymbolTimeframeScan.csx` | `MultiQuerySymbolTimeframeScanner` ([§13](#13-scanner-ailesi-12-sınıf--toplu-tarama)) |

Numaralanmamış diğer script'ler (`Config_*.csx`, `mainScript*.csx`, `paramSweep.csx`,
`runSingleTraderWithStrategy.csx`, `runMultiTraderWithStrategies.csx`, `CustomConsensusExample.csx`,
`console_scripts.csx`, `test_hello.csx`) için kategorize liste:
[inputs/scripts/readme.txt](../../inputs/scripts/readme.txt) ve
[03-scripting-guide.md §6](03-scripting-guide.md#6-mevcut-script-envanteri).

---

## İlgili Dosyalar

- [docs/PROJECT_ANALYSIS.md](../PROJECT_ANALYSIS.md) — daha derin davranışsal analiz: bilinen
  sorunlar, ölü kod adayları, tutarsızlıklar, dosya/satır sayıları.
- [docs/migration-guide.md](../migration-guide.md) — eski projeden taşıma durumu, roadmap
  maddelerinin gerçek durumu.
- [docs/todo.md](../todo.md) — açık işler, Scanner/Scripting/Confirming implementasyon notları.
- `inputs/scripts/readme.txt` — script dosyalarının kategorize edilmiş listesi.
