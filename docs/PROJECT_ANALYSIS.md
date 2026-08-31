# AlgoTrade — Proje Analizi (Fonksiyonel / Davranışsal / Class-Method Envanteri)

> Bu doküman, projenin sıfırdan tekrar analiz edilmesine gerek kalmaması için hazırlanmış kalıcı bir
> referanstır. `D:\SageProjects\AlgoTrade` altındaki tüm C# kod tabanı (147 dosya) dört alt sisteme
> bölünerek satır satır okunmuş ve analiz edilmiştir. Analiz tarihi: 2026-08-18, **güncelleme:
> 2026-08-21** (Confirming* alt sistemi, Scanner ailesi, consensus düzeltmesi ve güncel satır
> sayıları eklendi — bkz. §2.9, §2.10, değişen bölümler).
>
> İlgili diğer belgeler: [migration-guide.md](migration-guide.md) (eski projeden taşıma durumu + yol
> haritası), [todo.md](todo.md), [roadmap.md](roadmap.md), [Indicators-TODO.md](Indicators-TODO.md),
> [yapilacak.md](yapilacak.md).

## İçindekiler

1. [Proje Yapısı ve Bağımlılıklar](#1-proje-yapısı-ve-bağımlılıklar)
2. [Core Trading Engine](#2-core-trading-engine)
3. [Strateji Sistemi](#3-strateji-sistemi)
4. [İndikatör Sistemi](#4-i̇ndikatör-sistemi)
5. [Altyapı Katmanı](#5-altyapı-katmanı)
6. [Uygulama Katmanı (Console / WinForms)](#6-uygulama-katmanı-console--winforms)
7. [Genel Özet — Migration Guide Roadmap Durumu](#7-genel-özet--migration-guide-roadmap-durumu)
8. [Doğrulanması/Temizlenmesi Önerilen Noktalar](#8-doğrulanmasıtemizlenmesi-önerilen-noktalar)

---

## 1. Proje Yapısı ve Bağımlılıklar

`AlgoTrade.sln`: `AlgoTrade.Core` (Class Library, net8.0) ← `AlgoTrade.Console` (Exe) ve
`AlgoTrade.WinForms` (WinExe, net8.0-windows, WinForms) — ikisi de sadece Core'a referans verir,
birbirlerine değil. **Test projesi yok.**

**Core NuGet paketleri**: `Microsoft.CodeAnalysis.CSharp.Scripting` (Roslyn), `Newtonsoft.Json(+Bson)`,
`OoplesFinance.StockIndicators`, `pythonnet`, `ScottPlot`, Serilog ailesi (**referanslı ama kodda
kullanılmıyor** — kendi `LogManager`'ı var), `Skender.Stock.Indicators`, `TALib.NETCore`,
`Tulip.NETCore` (3 ayrı 3.parti indikatör kütüphanesi referanslı — ama proje kendi indikatörlerini
de yazmış, bkz. §4).

**AlgoTrade.Console**: harici bağımlılık yok, top-level statements, **4169 satır** (2026-08-18'de
2076 satırdı — Scanner + Confirming menüleri `[10]-[25]` ile ~2x büyüdü, bkz. §6.1).
**AlgoTrade.WinForms**: `OpenTK/OpenTK.GLControl`, `ScottPlot.WinForms`, `SkiaSharp.Views.WindowsForms`
referanslı ama **hiçbiri kullanılmıyor** — proje büyük ölçüde iskelet (bkz. §6.2).

Toplam kod tabanı: **147** `.cs` dosyası (bin/obj hariç, tüm solution), ~33.000+ satır (Core Trading
Engine ~16.100 satır — Confirming* alt sistemi + Scanner ailesi dahil, İndikatörler ~6.068 satır,
Strateji dosyaları 29 dosya, Altyapı 24+ dosya).

---

## 2. Core Trading Engine

**Kapsam**: `AlgoTrader.cs`, `Trading/Traders/SingleTrader.cs`, `MultipleTrader.cs`,
`ConfirmingSingleTrader.cs`, `ConfirmingMultipleTrader.cs`, 12 Scanner sınıfı, `MultipleQuery.cs`,
`SingleTraderOptimizer.cs`, `Trading/Core/*.cs` (`VirtualPositionConfirmer.cs` dahil 11 dosya),
`Trading/EquityCurve/*`, `Trading/Utils/*` — ~34 dosya, ~16.100 satır.

### 2.1 AlgoTrader.cs (3365 satır)
**Rolü**: Tüm trading alt sisteminin orkestratörü/facade'i. `MarketDataProvider`'dan türer,
`IDisposable`. Strategy/Query/EquityCurveFilter/Optimization konfigürasyonlarını toplar,
`SingleTrader`/`MultipleTrader`/`SingleTraderOptimizer` instance'larını yaratıp çalıştırır.

**Property grupları**: Kimlik (Name, SymbolName, SystemId/Name, StrategyId/Name, QueryId/Name),
`SingleTraderRunMode`, EquityCurveFilter alanları (legacy fallback API — asıl yönetim
`_equityCurveFilterConfigs` id-listesi üzerinden), `SingleTrader`/`MultipleTrader`/
`SingleTraderOptimizer` (private setter+public getter), kendi `_strategyRegistry`/`_queryRegistry`,
config listeleri (`_strategyConfigs`, `_queryConfigs`, `_equityCurveFilterConfigs`,
`_childTraderConfigs`), Python entegrasyonu (`PythonDll`, `_pythonPlotter`).

**Public API grupları**:
1. Strategy/Query yapılandırma (`ConfigureStrategy`, `ConfigureStrategyFromConfig`,
   `ConfigureQuery`, `ConfigureEquityCurveFilterFromConfig`)
2. Factory metodları (.csx script kullanımı için): `CreateIndicators()`,
   `CreateConfiguredStrategy()`, `CreateConfiguredQuery()`
3. MultipleTrader için çoklu config: `AddStrategyConfig`, `AddQueryConfig`,
   `AddEquityCurveFilterConfig`, `AddChildTraderConfig`, `SetChildTraderCount()`
4. SingleTrader override config seti (AppConfig'den enjekte edilir):
   `SetSingleTraderTradeParams/SignalsConfig/SaveConfig/PlotConfig/ExportConfig/OptimizationConfig`,
   `SetMultipleTraderSaveConfig`
5. Optimization config seti: `AddOptimizationParameterRange`, `SetOptimizationStrategyFactory`,
   `SetSingleTraderOptRangeConfig/TradeParamsConfig/LogConfig/SortOutputConfig`,
   `ConfigureOptimizationFromConfig`
6. Çalıştırma metodları (async, CancellationToken destekli): `RunSingleTraderWithProgressAsync()`,
   `RunMultipleTraderWithProgressAsync()`, `RunSingleTraderOptWithProgressAsync()`,
   `WriteTraderDataToFilesAsync()` (dosya yazma Run'dan ayrılmış — grafik açıkken paralel
   yazılabilsin diye)
7. Python görselleştirme: `SetupPython()`, `PlotSingleTraderData()`, `PlotMultipleTraderData()`

**`RunSingleTraderWithProgressAsync()` akışı**: IndicatorManager yaratılır → StrategyRegistry ile
strateji → (QueryEnabled ise) QueryRegistry ile query → SingleTrader(id=0) yaratılır, callback'ler
bağlanır (hepsi AlgoTrader içinde tanımlı ama gövdeleri **boş/no-op** — extension noktası) →
Reset() → attribute set → `ApplySingleTraderFlagsConfigs()` → EquityCurveFilter config →
strateji/query atanır → Init() → `Task.Run` içinde senkron bar-bar `for` döngüsü → Finalize() →
4 timer (t0-t3) ile performans ölçümü.

**`RunMultipleTraderWithProgressAsync()`**: Aynı iskelet + `createChildTraders()` çağrısı. Kod
içinde eski bir TODO yorumu var ("Hardcoded 3 fixed block, dinamik değil") ama güncel
implementasyon `_childTraderConfigs.Count` kadar dönen bir `for` döngüsü — yorum muhtemelen eski,
silinmemiş.

**`RunSingleTraderOptWithProgressAsync()`**: SingleTraderOptimizer yaratılır, log/sort ayarları,
parametre range'leri aktarılır, `GenerateParameterCombinations()`, strategy factory set edilir,
Run() çağrılır, `GetBestResult()` loglanır.

**`OnApplyUserFlags`/`OnApplyUserFlags2`**: `RunSingleTraderWithProgressAsync()` içinde
**comment-out edilmiş** (yerini `ApplySingleTraderFlagsConfigs()` almış), kod hâlâ duruyor
(4 kopya, sabit tarih aralığı hardcoded) — **ölü kod adayı**.

Dosya sonunda 15 config DTO sınıfı tanımlı (StrategyConfigEntry, SingleTraderSignalsConfig,
ChildTraderConfigEntry, vb.) — `AppConfigApplier` tarafından AppConfig.json'dan doldurulup enjekte
ediliyor.

### 2.2 SingleTrader.cs (2692 satır)
**Rolü**: Tek stratejiyi bar-bar çalıştıran çekirdek motor. Projenin en büyük/kritik sınıfı.

**Modül kompozisyonu**: `initialTradeParams`, `signals`, `status`, `flags`, `lists`, `timeUtils`,
`karZarar`, `karAlZararKes`, `statistics` — `CreateModules()/ResetModules()/InitModules()/
DeleteModules()` dörtlüsüyle yönetilir.

**`Run(int barIndex)`**: RunMode'a göre 3 dal (TradeOnly/TradeAndQuery/QueryOnly). İlk bar
(`i<1`) trade mantığı atlanır (indikatör warm-up). Progress event %5 throttle ile.

**Sinyal işleme zinciri** (kritik akış, migration-guide.md'deki equity curve açıklamasıyla
birebir örtüşüyor):
1. `ExecuteStrategy(i)` → `Strategy.OnStep(i)` → `TradeSignals` enum
2. `MapStrategyCommandsToTradeCommands()` → enum'u `signals.Al/Sat/...` bool'larına çevirir,
   `*Enabled` flag kapalıysa yok sayılır
3. `ApplyTimingFilters(i)` → `CheckOrderTimeEligibility()` — 6 farklı FilterMode (saat/tarih/
   datetime aralığı × sadece-başlangıç varyantları)
4. `ApplyEquityCurveFilter(i)`
5. **`ResolveFilterDecisions(i)`** — net öncelik sırası: **PozKapat (hard) > GünSonuPozKapat
   (hard) > Timing hard block > TradeStartBarIndex warmup block > EquityCurve soft block**
   (sadece giriş sinyalleri iptal edilir, çıkış sinyalleri dokunulmaz — bilinçli tasarım,
   koruyucu mekanizmalar baskılanmasın diye)
6. `ExecutePostOrderMethods(i)` → OnBeforeOrder → `ExecuteOrders(i)` → OnAfterOrder →
   `CalculateBalance(i)`

**`ExecuteOrders()`** (~720 satır, en karmaşık method): `Sinyal`×`SonYon` kombinasyonuna göre
dallanır — F→A/S (yeni pozisyon), S→A/A→S (ters yön, **2 komisyonlu işlem**: kapat+aç), A→F/S→F
(kapama, 1 işlem), P/boş (BakiyeGuncelle=false, çift sayım önleniyor), **A→A/S→S pyramiding**
(PyramidingEnabled kapalıysa yok sayılır; açıksa MaxPositionSize limit kontrolü + ağırlıklı
ortalama giriş fiyatı hesabı). Slippage (`KaymayiDahilEt`) yön bazlı "daha kötü" fiyat seçimi
yapıyor. Micro/normal lot ayrı hesaplanıyor.

**Kar/Zarar hesaplama — kod tekrarı tespiti**: `SingleTrader._calculateUnrealizedPnL()/Micro()`
(private) ile `Core/KarZarar.cs`'in `anlikKarZararHesapla()` neredeyse birebir aynı. SingleTrader
kendi private kopyasını kullanıyor, `karZarar` modülünü (`CreateModules()`'da yaratılıp
bağlanmasına rağmen) **kullanmıyor gibi görünüyor** — doğrulanmalı (bkz. §8).

**`CalculateBalance()`**: Bakiye/getiri/net-getiri hesaplar, son barda status alanlarını
senkronize eder. İçinde "Silinecek" etiketli comment-out kalıntılar (eski GetiriKz sistemi).

**Diğer**: `ConfigureUserFlagsOnce()`, `is_son_yon_*/is_prev_yon_*()`, `ClosePositionEOD()`
(kullanılıyor) vs `ClosePositionEOD_2()` (çağrıldığı yer bulunamadı), `GetPerformansParams()`
(TODO etiketli), `WriteStatisticsToFile()` (12 farklı çıktı türü, Minimal* metodları comment-out).

### 2.3 MultipleTrader.cs (688 satır) — ✅ 2026-08-21 güncelleme: consensus artık tam implement
**Rolü**: Birden fazla child `SingleTrader`'ı aynı bar üzerinde çalıştırıp sinyal konsensüsü
üzerinden tek bir `mainTrader` (id=-1) ile emir üreten sınıf.

**`BuildConsensusSignal()`**: **Düzeltme (2026-08-18 commit f93dfb6'da zaten tamamlanmış,
önceki analizde yanlış "eksik" işaretlenmişti)** — `ConsensusMode` property'si (Net/Majority/
All/Any, varsayılan "Net") üzerinden 4 modun hepsi `switch` ile implement edilmiş, tanınmayan
mod değeri Net'e düşüp uyarı logluyor. `AppConfig.MultipleTraderConfig.ConsensusConfig` alanı
`AppConfigApplier` üzerinden bu property'e tam bağlı.

**`DynamicPositionSizeEnabled`**: flag var ama gövdesi TODO ("PozisyonBuyuklugu mevcut projede
yok") — **işlevsiz**.

**`Run(i)`**: Her child `Run(i)` → sinyal sayaçları toplanıyor (ama kullanılmıyor gibi,
`BuildConsensusSignal()` ayrı hesap yapıyor) → consensus → mainTrader üzerinde SingleTrader'daki
6 adımlı pipeline manuel tekrarlanıyor.

Dosya/rapor çıktıları: `WriteMultipleTraderListsToFiles()` (TXT+CSV, her bar için tüm
trader'ların Yön/Seviye/Sinyal'i yan yana — **bar-bar rapor, trade-bazlı performans raporu
değil**).

### 2.4 SingleTraderOptimizer.cs (934 satır) — ⚠️ roadmap ile çelişen bulgu
**Yapı**: `ParameterRange` (Min/Max/Step → değer listesi), `OptimizationResult` (parametreler +
`Statistics.GetOptimizationSummary()` map'i + convenience getter'lar: NetProfit/WinRate/
ProfitFactor/ScoreFiyatNet vb.), `StrategyFactory` delegate.

**`GenerateParameterCombinations()`**: Recursive backtracking ile kartezyen çarpım — kombinasyon
patlaması için sınır/uyarı yok.

**`Run()`**: Her kombinasyon için — PartialOpt desteği (`OptimizationFrom/To`, kesintiye uğrayan
uzun optimizasyonları parça parça devam ettirmek için) → strateji + yeni SingleTrader(id=0,
`OptimizationEnabled=true`, `SaveStatisticsToFile=false`) → bar-bar Run → Finalize →
`GetOptimizationSummary()` → **iki modlu dosya yazma** (anında append ya da zaman-aralıklı buffer
flush) → sıralı çıktı (`WriteSortedFiles`, CSV cache'den okunup sıralanıyor) → Dispose.

**Tutarsızlık**: `GetBestResult()` **sadece NetProfit'e göre** sıralıyor, ama dosya çıktısındaki
sıralama `SortField`'e göre (config'den farklı bir alan olabilir, ör. ProfitFactor) — **iki
sıralama kriteri arasında tutarsızlık olabilir**.

### 2.5 Core/*.cs — State modülleri
- **Flags.cs** (97 satır): basit bayrak DTO'su. `IdealGetiriHesapla` kullanılmıyor gibi.
- **Signals.cs** (279 satır): Son/Prev fiyat-bar çiftleri her sinyal türü için ayrı (flat
  tasarım). `TrailingStop` alanları var ama güncelleyen tek yer (`KarAlZararKes.
  IzleyenStopGuncelle`) otomatik pipeline'dan çağrılmıyor.
- **Status.cs** (251 satır): bar özet durumu. "KZ System" bölümü tamamen comment-out ("Silinecek").
- **InitialTradeParams.cs** (650 satır): `MarketTypes` enum 14 değer, her biri için ayrı
  `SetKontratParams<Type>()` metodu (13 tane) + genel `SetMarketType()` switch — **iki paralel
  API, kısmi kod tekrarı**. **Pyramiding desteği** tam implement (migration-guide.md'de hiç bahsi
  geçmiyor — sonradan eklenmiş). **2026-08-20 değişikliği**: `SetKontratParamsFxCrypto`'nun
  `varlikAdedCarpani` varsayılanı `1.0`'dan `100.0`'a çıkarıldı (kullanıcı onaylı; broker teyidi
  hâlâ bekleniyor — bkz. [todo.md](todo.md)).
- **Lists.cs** (462 satır): ~35 zaman-serisi listesi. `InitOrReuse()` — aynı bar sayısında
  realloc'suz sıfırlama (GC optimizasyonu; optimizer her seferinde yeni SingleTrader yarattığı
  için orada devreye girmiyor).
- **KarZarar.cs** (419 satır) — **muhtemelen ölü kod**: SingleTrader'ın private PnL
  metodlarıyla bit-bit aynı, hiç çağrılmıyor gibi. TP/SL metodları da (SetByPercentage,
  CheckKarAl vb.) çağrıldığı yer yok.
- **KarAlZararKes.cs** (559 satır): geniş Kar Al/Zarar Kes/Trailing-Stop kütüphanesi
  (yüzde/seviye bazlı, seviyeli varyantlar) — otomatik pipeline'dan çağrılmıyor ama stratejilerin
  ortak TP/SL kalıbı (`SonFiyataGoreKarAlSeviyeHesaplaSeviyeli`/
  `SonFiyataGoreZararKesSeviyeHesaplaSeviyeli`) buradan geliyor, aktif kullanılıyor (bkz. §3.2.4).
- **TimeUtils.cs** (219 satır): `check_bar_*_with()` metodları aktif kullanılıyor
  (CheckOrderTimeEligibility içinde). Ayrı bir "session tracking" API'si (`UpdateTime`,
  `IsWithinTradingHours`) var ama kullanılmıyor gibi — **iki paralel zaman filtreleme
  yaklaşımı**.
- **TradeSignals.cs** (16 satır): basit enum.
- **zTradeStatistics.cs** (162 satır) — **muhtemelen ölü/eski kod**: basit `TradeStatistics`
  sınıfı, dev `Statistics.Statistics`'in yanında kullanılmıyor gibi.
- **Statistics.cs** (2611 satır): dev istatistik motoru. Property grupları: kimlik/meta, bar
  aralığı, süre/sıklık, işlem sayıları, Max Kar/Zarar, komisyon toplamı, bakiye min/max,
  **Drawdown**, **Performans skorları** (ProfitFactor/ScoreFiyatNet vb. — optimizer sıralama
  kriterleri buradan), `PerformansRow` (trade-by-trade rapor), periyodik getiri
  (Ay/Hafta/Gün/Saat × BuAy/Ay1-5 pattern'i). Ana metodlar: `CalculatePerformances()`,
  `Hesapla()`, export metodları (Full/Minimal × Txt/Csv), `GetOptimizationSummary()`.

### 2.6 EquityCurveFilterConfigLoader.cs (180 satır)
Pipe-ayraçlı özel format okur. Parse hataları exception yerine `Console.WriteLine` ile loglanıp
atlanıyor (diğer loader'larla tutarlılığı kontrol edilmeli).

### 2.7 Utils/Utils.cs (296 satır)
Statik crossover/karşılaştırma kütüphanesi (`YukarıKesti`/`AsagiKesti`, `Buyuk`/`Kucuk`/`Esit` —
dizi-dizi ve dizi-skaler). Stratejiler tarafından yaygın kullanılan temel API.

### 2.8 Utils/StatisticsExporter.cs (1563 satır)
Config-driven export motoru. **Dikkat çekici**: JSON binding sınıflarında bilinçli
typo-tolerant alias property'ler (`SngleTrader`, `descrpton`, `wdth` vb.) — elle düzenlenen
config dosyalarındaki yazım hatalarını tolere etmek için.

### 2.9 Confirming* Alt Sistemi (2026-08-19 eklendi) — roadmap Madde 3'ün karşılığı

**Kapsam**: `Trading/Traders/ConfirmingSingleTrader.cs` (469 satır),
`Trading/Traders/ConfirmingMultipleTrader.cs` (483 satır),
`Trading/Core/VirtualPositionConfirmer.cs` (175 satır).

**Amaç**: migration-guide.md Madde 3'te tarif edilen "sanal pozisyon konfirmasyonu" — bir
sinyal üretildiğinde gerçek emir açmadan önce, sinyali bir süre/koşul boyunca **sanal** olarak
izleyip belirli bir konfirmasyon kriteri sağlanırsa gerçek pozisyona geçme mekanizması. Not:
mevcut `SingleTrader.ApplyEquityCurveFilter` mekanizmasından **farklı** — o equity-curve tabanlı
soft-block, bu ise sinyal-bazlı virtual-then-real state machine'i.

**Mimari**: `ConfirmingSingleTrader`/`ConfirmingMultipleTrader`, içlerinde bir "sinyal" trader
(`signalTrader`/`signalMultipleTrader` — asıl stratejiyi çalıştırıp ham sinyal üretir) ve bir
"ana" trader (`mainTrader` — gerçek emirleri açan) çiftini yönetir; ikisi arasındaki köprü
`VirtualPositionConfirmer` state machine'idir (sinyal geldiğinde sanal pozisyon açar, konfirmasyon
koşulu sağlanınca `mainTrader`'a gerçek emir tetikler, sağlanmazsa sanal pozisyonu iptal eder).

**AppConfig entegrasyonu**: `ConfirmingSingleTraderConfig`/`ConfirmingMultipleTraderConfig` (bkz.
§5.2), export tarafında `SetConfirmingSignalTraderExportConfig` ile ayrı versiyonlanmış export
desteği var (bkz. [export-adimlar.md](export-adimlar.md)).

**Console**: `[22]-[25]` menü aralığı (bkz. §6.1).

**Durum**: roadmap Madde 3 artık **TAMAMLANDI** sayılmalı — §7 tablosu buna göre güncellendi.

### 2.10 Scanner Ailesi ve MultipleQuery — Toplu Tarama Motorları

**Kapsam**: `Trading/Traders/{Symbol,Timeframe,SymbolTimeframe}Scanner.cs`,
`Trading/Traders/MultiStrategy{Timeframe,Symbol,SymbolTimeframe}Scanner.cs`,
`Trading/Traders/Query{Timeframe,Symbol,SymbolTimeframe}Scanner.cs`,
`Trading/Traders/MultiQuery{Timeframe,Symbol,SymbolTimeframe}Scanner.cs` (12 sınıf) +
`Trading/Traders/MultipleQuery.cs`.

Bu sınıflar §7 tablosundaki madde 8 ("Toplu sembol taraması") ve madde 9 ("Sorgu + toplu sembol")
için kod tabanındaki gerçek karşılıktır — Strateji ve Sorgu eksenlerini, Sembol/Timeframe/
Sembol×Timeframe boyutlarıyla çarpan tam bir tarama matrisi oluşturuyorlar (Strateji tarafı 6
sınıf: tekil + Multi varyantı × 3 boyut; Sorgu tarafı aynı desende 6 sınıf). Detaylı kullanım ve
Console menü haritası için bkz. [todo.md](todo.md) "Tarama Motorları" bölümü.

---

## 3. Strateji Sistemi

**Kapsam**: `Strategy/IStrategy.cs`, `Strategy/BaseStrategy.cs`, `Strategies/StrategyRegistry.cs`,
`Strategies/StrategyConfigLoader.cs`, `Strategies/OptimizationConfigLoader.cs` + 24 somut strateji
dosyası — toplam 29 dosya, tamamı okundu.

### 3.1 Altyapı Sınıfları

**`IStrategy.cs`** (46 satır): `IDisposable`'dan türeyen sözleşme: `Name` (string), `Parameters`
(Dictionary<string,object>), `OnInit()`, `OnStep(int currentIndex) → TradeSignals`, `Reset()`,
`GetPlotIndicators() → Dictionary<string,double[]>?`.

**`BaseStrategy.cs`** (150 satır): Soyut taban sınıf. Korunan property'ler: `Data`, `Indicators`,
`Trader` (SingleTrader, private set), `IsInitialized`, `Logger`.
- `Initialize(data, indicators)`: Data/Indicators set eder, `IsInitialized=true`, `OnInit()`
  çağırır. İkinci constructor'lar bunu tetikler.
- `OnInit()` virtual boş, `OnStep()` abstract, `Reset()` virtual sadece `IsInitialized=false`
  yapar — **hiçbir somut strateji override etmiyor**, yani strateji-özel state (`_rsiResult` vb.)
  Reset()'te temizlenmiyor.
- `SetTrader(SingleTrader)`: stratejiye trader enjekte eder → `Trader.karAlZararKes` gibi
  modüllere erişim sağlar.
- `SetLogger`/`Log`/`LogWarning`: logger yoksa statik `LogManager.LogRaw()`'a düşer.
- `Dispose()` → `Reset()` çağırır.

**`StrategyRegistry.cs`** (289 satır): Reflection tabanlı factory/registry.
- `AutoRegister()`: constructor'da otomatik çalışır, verilen assembly'de `BaseStrategy`'den
  türeyen non-abstract tüm class'ları `TypeName→Type` (case-insensitive) kaydeder. **Yeni
  strateji eklemek için elle kayıt gerekmiyor.**
- `CreateStrategy(data, indicators, logger, strategyName?, parameters?)`: isim boşsa
  `"SimpleMAStrategy"`'ye düşer; isim bulunamazsa `ArgumentException`; önce statik `Create(...)`
  factory method aranır (**şu an hiçbir strateji bunu tanımlamıyor, kullanılmıyor**); yoksa ilk
  iki parametresi `List<StockData>`/`IndicatorManager` olan constructor'lar arasından
  `parameters` ile en çok eşleşen (en yüksek skor) seçilir, eksik parametreler default değerle
  doldurulur.
- `ConvertToTargetType`: JsonElement, enum, Guid, string→sayısal dönüşümleri destekler
  (config/JSON değerlerini constructor tiplerine güvenli çevirir).

**`StrategyConfigLoader.cs`** (237 satır): Düz metin config dosyasından
(`StrategyName|Version|DisplayName|param:type:value|...`) strateji parametre setleri okur. `#`
yorum satırları ve boş satırlar atlanır; parse hatası olan satır loglanıp atlanır (fail-soft).
Aynı strateji için birden fazla "versiyon" (parametre seti) tanımlanabilir.

**`OptimizationConfigLoader.cs`** (239 satır): `StrategyConfigLoader`'a paralel ama tek değer
yerine aralık okur: `StrategyName|Version|DisplayName|param:min:max:step|...|fixed:param:type:
value|...`. `ParameterRanges` (taranacak) ve `FixedParameters` (sabit) ayrı tutulur. Bu,
`SingleTraderOptimizer`'ın (bkz. §2.4) parametre-tanımlama katmanıdır.

### 3.2 Ortak Strateji Deseni (24/24 strateji aynı iskeleti izliyor)

1. **İki constructor**: parametresiz-veri (sadece `Parameters` doldurur) ve parametreli-veri
   (`List<StockData> data, IndicatorManager indicators, ...` + `Initialize()` çağırır — registry
   bunu hedefler).
2. **`OnInit()`**: `Indicators.<Kategori>.<Method>(...)` ile ilgili indikatörü hesaplayıp alanda
   saklar.
3. **`OnStep()`**: yetersiz bar / null indikatör / NaN kontrolünde `TradeSignals.None`;
   **`choice` parametresi** neredeyse her stratejide var ama sadece `choice==0` dalı dolu, `else`
   dalı "İleride eklenecek alternatif sinyal mantığı" yorumuyla boş — **istisna:
   `SimpleMostStrategy`, choice=1 için gerçek EXMOV-MOST mantığı tam implement edilmiş**.
4. **Ortak TP/SL kalıbı** (neredeyse tüm stratejilerde birebir aynı, hardcoded):
   ```csharp
   takeProfit = Trader.karAlZararKes.SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(currentIndex, 5, 50, 1000) != 0;
   stopLoss   = Trader.karAlZararKes.SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(currentIndex, -1, -10, 1000) != 0;
   ```
   Parametreler strateji constructor'ından yapılandırılamıyor.
5. **Sinyal önceliklendirme** (hepsinde aynı): `Skip > Flat > TakeProfit > StopLoss > Buy > Sell
   > None`. **`skip`/`flat` bool'ları hiçbir stratejide hiçbir zaman `true` olmuyor** —
   tamamen ölü kod/gelecek genişleme iskeleti; bu sinyaller `SingleTrader` seviyesinde (zaman
   filtreleri, equity curve filtresi) enjekte ediliyor.
6. **`GetPlotIndicators()`**: hesaplanan dizileri grafik için döner.

**Sapmalar**:
- `SimpleMAStrategy` ve `SimpleSuperTrendStrategy`: `choice` alanı var ama `OnStep`'te hiç
  kullanılmıyor (if/else yok).
- `SimpleMAStrategy`: kullanılmayan `using ScottPlot.TickGenerators.Financial` ve `using static
  Nessos.LinqOptimizer.Core.QueryExpr` — kopyala-yapıştır kalıntısı.
- `SimpleMostStrategy`: MOST indikatörü implement edilmemişse `NotImplementedException`'ı
  yakalayıp uyarı loglayıp boş dizilerle devam ediyor (fail-soft) — bu davranışa sahip tek
  strateji.
- Minimum bar eşiği tutarsız: çoğu `period+1`; `SimpleADXStrategy` `period*2+1`;
  `SimpleTillsonT3Strategy` `period*6+1` (T3 6x EMA gerektirir); `SimpleMavilimWStrategy` sabit
  `100` (parametreye bağlı değil, yorum "Fibonacci periyotları 250'ye kadar" diyor ama sabit
  kullanılmış).
- `Trader==null` durumunda strateji sinyal üretmeye devam ediyor, sadece TP/SL atlanıyor —
  strateji `SingleTrader` bağlamı dışında da (test, bağımsız sinyal üretimi) çalışabilir
  tasarlanmış.

### 3.3 Strateji Özet Tablosu

| Strateji | İndikatör | Giriş Mantığı | Parametreler (varsayılan) |
|---|---|---|---|
| SimpleADXStrategy | ADX+DI | AL:+DI/-DI yukarı kesişim VE ADX>eşik; SAT: tersi | period=14, adxThreshold=25, choice=0 |
| SimpleATRStrategy | ATR+SMA bant | AL: fiyat üst bandı kırar; SAT: alt bandı kırar | atrPeriod=14, maPeriod=20, multiplier=2.0 |
| SimpleAlphaTrendStrategy | AlphaTrend | 2-bar offset crossover | atrPeriod=14, coefficient=1.0, momentumPeriod=14 |
| SimpleBollingerStrategy | Bollinger Bands | AL: alt bant yukarı kesişim; SAT: üst bant aşağı | period=20, multiplier=2.0 |
| SimpleCMFStrategy | CMF | AL: +0.1 eşik yukarı; SAT: -0.1 eşik aşağı | period=20, ±threshold=0.1 |
| SimpleDIStrategy | +DI/-DI (ADX filtresiz) | AL/SAT: DI kesişimi | period=14 |
| SimpleHHVLLVStrategy | HHV/LLV | AL: HHV breakout; SAT: LLV breakdown | period=20 |
| SimpleHYLYStrategy | HY/LY (hesaplanmış) | AL: HY>80; SAT: LY>80 | period=20, threshold=80 |
| SimpleIchimokuStrategy | Tenkan/Kijun | TK Cross | tenkan=9, kijun=26, senkou=52 |
| SimpleKairiStrategy | Kairi (hesaplanmış) | AL: +5 eşik; SAT: -5 eşik | period=20, ±threshold=5 |
| SimpleMACDStrategy | MACD | MACD/Signal kesişimi | fast=12, slow=26, signal=9 |
| SimpleMAStrategy | Fast/Slow SMA | Golden/Death Cross (choice etkisiz) | fast=10, slow=20 |
| SimpleMFIStrategy | MFI | AL: 20 yukarı; SAT: 80 aşağı | period=14, 20/80 |
| SimpleMavilimWStrategy | MavilimW | Fiyat-MavilimW kesişimi | param1=3, param2=5 (minbar=100 sabit) |
| SimpleMomentumStrategy | ROC | AL/SAT: 0 eşik kesişimi | period=12 |
| SimpleMostStrategy | MOST/EXMOV | choice=0: fiyat-MOST; choice=1: EXMOV-MOST (ikisi de dolu) | period=21, percent=1.0 |
| SimpleOTTStrategy | OTT/MA | MA-OTT kesişimi | period=2, percent=1.4 |
| SimplePMaxStrategy | PMax Direction | Direction -1↔1 dönüşü | atrPeriod=10, multiplier=3.0, maPeriod=10 |
| SimpleParabolicSARStrategy | SAR Trend | Trend bool dönüşü | step=0.02, max=0.2 |
| SimpleRSIStrategy | RSI | AL: 30 yukarı; SAT: 70 aşağı | period=14, 30/70 |
| SimpleStochasticStrategy | %K/%D | Kesişim + merkez çizgi (50) filtresi | kPeriod=14, dPeriod=3, centerLine=50 |
| SimpleSuperTrendStrategy | SuperTrend Direction | Direction -1↔1 dönüşü (choice etkisiz) | period=10, multiplier=3.0 |
| SimpleTillsonT3Strategy | T3 | Fiyat-T3 kesişimi | period=5 (minbar=period×6+1) |

Tüm stratejilerde "çıkış" ortak TP/SL kalıbı üzerinden işliyor (bkz. §3.2.4).

### 3.4 Öne Çıkanlar
1. Strateji ekleme düşük sürtünmeli: `BaseStrategy`'den türet + iki constructor +
   `OnInit`/`OnStep` → registry reflection ile otomatik bulur.
2. `choice` parametresi büyük ölçüde kullanılmayan bir genişletme noktası (24'ten sadece 1'i —
   MostStrategy — gerçek ikinci mantık implement etmiş).
3. `Skip`/`Flat` sinyalleri strateji katmanında hiç üretilmiyor.
4. TP/SL parametreleri (`5,50,1000` / `-1,-10,1000`) tüm stratejilerde hardcoded, strateji
   bazında yapılandırılamıyor.
5. `OptimizationConfigLoader` çalışır durumda, `SingleTraderOptimizer` da tamam (bkz. §2.4).
6. `Reset()` strateji-özel state'i temizlemiyor; her çalıştırmada registry yeni instance
   ürettiği için pratikte sorun değil gibi görünüyor.
7. Yüksek kod tekrarı: 24 dosya satır satır aynı iskeleti tekrarlıyor — bilinçli "her strateji
   kendi kendine yeten dosya" tercihi gibi duruyor.

---

## 4. İndikatör Sistemi

**Kapsam**: `Trading/Indicators/**` — 50 dosya, ~6068 satır.

### 4.1 Genel Mimari
`IndicatorManager` (`Indicators/IndicatorManager.cs`) merkezi giriş noktası; `MarketDataProvider`'dan
türer, `IDisposable` uygular. Constructor: `IndicatorManager(IndicatorConfig? config=null)` veya
`(List<StockData> data, IndicatorConfig? config=null)`. 8 alt yönetici constructor'da oluşturulur
ve public property olarak açılır: `MA`(MovingAverageCalculator), `Trend`, `Momentum`,
`Volatility`, `VolumeInd`, `PriceAction`, `SupportResistance`, `Utils`(PriceUtils). Her alt
yönetici `(IndicatorManager manager, IndicatorConfig config)` alır.

**Cache**: `_cache: Dictionary<string,double[]>`, `GetOrCalculate(key, calculator)` internal
helper. HIT/MISS `LogManager`'a loglanır; `EnablePerformanceTiming` açıksa `TimeManager` ile süre
ölçülür. `CacheSize` (varsayılan 128) dolunca yeni sonuçlar cache'lenmez — **LRU yok, basit
"dolunca durdur" politikası**. `GetCachedIndicators()` → `PythonPlotter`/
`TradeDataBundleConverter` tarafından `td.indicators`'a aktarım için kullanılıyor. `ClearCache()`,
`GetCacheStats()`, `Reset()`, `SetData()` (null/boş kontrolü var) mevcut.

**IndicatorConfig**: `FibonacciPeriods`(3,5,8,...,233), `CommonPeriods`(5,10,...,1000),
`CacheSize=128`, `EnableDebugLogging=false`, `EnablePerformanceTiming=true`, ve her aile için
varsayılan periyot/çarpan (RSI=14, MACD=12/26/9, ATR=14, BB=20/2.0, SuperTrend=10/3.0,
MOST=21/1.0).

**IndicatorTest.cs**: Gerçek unit-test suite değil — `LogManager` ile loglayan manuel
smoke-test/kullanım kılavuzu ("kaybolmasın" notuyla saklanmış). Momentum/Trend testleri hâlâ
TODO/yorum satırında (bkz. [Indicators-TODO.md](Indicators-TODO.md)).

### 4.2 Moving Averages — partial class, 7 dosya, **66 somut MA metodu**
+ 1 generic `Calculate(source, MAMethod, period)` dispatcher + 2 `CalculateBulk` overload
- **Temel (MovingAverageCalculator.cs, 11)**: SMA, EMA, WMA, HullMA, DEMA, TEMA, VWMA, LSMA,
  Triangular, Wilder, SMMA
- **Advanced (6)**: KAMA(period,fast=2,slow=30), VIDYA, ZLEMA, T3(period,vFactor=0.7),
  ALMA(period,sigma=6.0,offset=0.85), JMA(period,phase=0,power=2)
- **Advanced2 (4)**: COVWMA, COVWEMA, FAMA, TIME_SERIES
- **Compound (14)**: Double: DSMA/DWMA/DVWMA/DHULL/DZLEMA/DSMMA/DSSMA — Triple: TSMA/TWMA/
  TVWMA/THULL/TZLEMA/TSMMA/TSSMA
- **Exotic (17, en büyük dosya 549 satır)**: FRAMA, MAMA(fastLimit,slowLimit — period almaz),
  MCGINLEY, VAMA, ADEMA, EDMA, EDSMA, AHMA, EHMA, ALSMA, AARMA, MCMA, LEOMA, CMA, CORMA, AUTOL,
  XEMA
- **Specialized (11)**: SRWMA, SWMA, EVWMA, REGMA(period,lambda=0.1), REMA, REPMA,
  RSIMA(period,rsiPeriod=14), ETMA, TREMA, TRSMA, THMA
- **Statistical (3)**: MEDIAN, GMA, ZSMA

`MAMethod` enum (`Base/MAMethod.cs`) 70+ üye tanımlar; `MAMethodExtensions.IsImplemented()` ile
hangilerinin gerçekten kodlandığı kontrol edilebiliyor — enum üye sayısı ile 66 metod tam
örtüşmeyebilir.

**Dikkat**: `THULL` (Compound) ile `THMA` (Specialized, "Triple Hull alternative") aynı kavramın
iki ayrı implementasyonu — isim çakışması riski.

### 4.3 Momentum (`MomentumIndicators.cs`, 579 satır)
`RSI(source,period=14)`→RSIResult, `MACD(source,fast=12,slow=26,signal=9)`→MACDResult,
`Stochastic(kPeriod=14,dPeriod=3)`→StochasticResult, `CCI(period=20)`→double[],
`WilliamsR(period=14)`→double[], `ROC(source,period=12)`→double[],
`OTTO(fastPeriod=10,slowPeriod=25,correctionConstant=2.0)`→OTTOResult (VIDYA tabanlı çift-bant
osilatör), `StochasticOTT(kPeriod=14,smoothKPeriod=500,smoothDPeriod=200,...)`→
StochasticOTTResult.

Result sınıfları tipik desen izliyor: ham dizi(ler) + `Current*` hesaplanmış property +
`IsOverbought`/`IsBullish` gibi boolean sinyal property'leri + `Length`. Örn. MACDResult:
`IsBullish => CurrentHistogram>0`; StochasticResult: `IsOverbought => CurrentK>80`.

### 4.4 Trend (`TrendIndicators.cs`, 1294 satır — en büyük tekil dosya)
`SuperTrend(period=10,multiplier=3.0)`, `MOST(period=21,percent=1.0)`, `ADX(period=14)`→double[],
`ADXWithDI(period=14)`→ADXResult(ADX/PlusDI/MinusDI + `IsStrongTrend`(ADX>25)/
`IsWeakTrend`(ADX<20)/`IsUptrend`/`IsDowntrend`), `ParabolicSAR(step=0.02,max=0.2)`,
`Aroon(period=25)`→AroonResult(`IsStrongUptrend`: Up>70&Down<30, `IsConsolidating`: ikisi de
50±20), `Vortex(period=14)`, `Ichimoku(tenkan=9,kijun=26,senkou=52,displacement=26)`→5 çizgi +
`IsBullishCloud`/`IsBearishCloud`, `AlphaTrend(atrPeriod=14,coefficient=1.0,momentumPeriod=14,
useMFI=true)`, `OTT(period=2,percent=1.4,maMethod=VIDYA)`,
`PTT(fasterPeriod=5,period=5,maPeriod=2,slowerPeriod=10,stdDev=2.0)`,
`HOTTLOTT(period=2,percent=1.4,maMethod=VIDYA)`, `PMax(atrPeriod=10,multiplier=3.0,maPeriod=10,
maMethod=EMA)`, `MavilimW(param1=3,param2=5)`.

**Önemli tasarım noktası**: OTT ailesi (OTT/HOTTLOTT/PMax/PTT) `Base.MAMethod` parametresi
alıyor — 66 MA türünden herhangi biriyle kombinlenebiliyor, strateji tarafında çok sayıda
kombinasyon imkânı.

### 4.5 Volatility (`VolatilityIndicators.cs`, 189 satır)
`ATR(period=14)`→double[], `BollingerBands(source,period=20,stdDevMultiplier=2.0)`→
BollingerBandsResult(Upper/Middle/Lower/Bandwidth/PercentB), `KeltnerChannel(period=20,
multiplier=2.0)`, `DonchianChannel(period=20)` (source almaz, high/low manager'dan).

### 4.6 Volume (`VolumeIndicators.cs`, 275 satır)
`OBV()`, `VWAP()` (parametresiz), `MFI(period=14)`, `CMF(period=20)` — hepsi düz `double[]`,
Result sınıfı yok (diğer kategorilere göre daha az zengin API).

### 4.7 Price Action (`PriceActionIndicators.cs`, 360 satır)
`HigherHighLowerLow()`→HHLLResult(`IsUptrend`=HH&HL), `SwingPoints(leftBars=5,rightBars=5)`,
`ZigZag(deviation=5.0)`→ZigZagResult(`Pivots[]` int, `IsCurrentPivotHigh/Low`), `Fractals()`,
`HHVLLV(source,period=20)`→birleşik HHV+LLV, `HHV(source,period=20)`, `LLV(source,period=20)`.

**Duplikasyon**: `HHV`/`LLV` hem burada hem `PriceUtils` içinde ayrı tanımlı.

### 4.8 Support/Resistance (`SupportResistanceIndicators.cs`, 845 satır — 14 public metod)
`FibonacciRetracement(high,low,isUptrend=true)`, `FibonacciRetracementAuto(period=100)`→7 seviye
(Level_0/236/382/50/618/786/100), ve 12 farklı Pivot Points varyantı: `ClassicPivotPoints`,
`FibonacciPivotPoints`, `WoodiePivotPoints`, `DeMarkPivotPoints`, `FloorPivotPoints`,
`CamarillaPivotPoints`(R1-4/S1-4), `CPRPivotPoints`(Pivot/TC/BC/R1-3/S1-3/CPRWidth),
`ClassicExtendedPivotPoints`(R1-5/S1-5), `FibonacciExtensionPivotPoints`,
`TraditionalFloorPivotPoints`, `AlternativeClassicPivotPoints`, `MidPivotPoints`. Hepsi
`useDaily: bool=true` alıyor.

### 4.9 Utils (`PriceUtils.cs`, 327 satır)
`HHV`, `LLV`, `Sum`, `StdDev`, `Mean`, `Variance`, `TrueRange(high,low,close)`, `Diff`,
`PercentChange` — genel rolling-window matematik yardımcıları.

### 4.10 Toplam Envanter
50 dosya, ~6068 satır (Result sınıfları hariç). **66 MA metodu + ~45 diğer indikatör metodu ≈
111 public hesaplama metodu**, 8 kategori, 30+ Result sınıfı (tümü ortak desen: ham dizi +
Current* + boolean sinyal property'leri + Length).

### 4.11 Dikkat Edilmesi Gerekenler
1. Cache'te LRU yok — uzun optimizasyon taramalarında (`SingleTraderOptimizer`) `CacheSize`
   aşılırsa performans kazancı kayboluyor.
2. `HHV`/`LLV` iki yerde tanımlı (PriceAction + Utils).
3. `THULL` vs `THMA` isim çakışması riski.
4. `IndicatorTest.cs` gerçek test değil; Momentum/Trend testleri hâlâ TODO.
5. `MAMethod` enum üye sayısı ile gerçek implementasyon sayısı (66) tam örtüşmüyor olabilir.

---

## 5. Altyapı Katmanı

**Kapsam**: `StockData`, `StockDataReader`, `MarketDataProvider`, `AppConfig/*`, `AppSettings`,
`Logging/*`, `Timer/TimeManager`, `Utils/FileUtils`, `Scripting/*`, `Python/*`, `Trading/Query/*`,
`Trading/Queries/*` — 24+ dosya.

### 5.1 Veri Katmanı
- **`StockData`** (struct): ham OHLCV + `Id/DateTime/Date/Time/Size` alanları, hesaplanan
  readonly property'ler: `EpochTime, Diff, ChangePct, IsBullish/Bearish/Neutral(%0.01 eşik),
  Range, BodySize, UpperShadow, LowerShadow, MidPrice, TypicalPrice, WeightedClose`. Struct
  olması büyük listelerde value-copy maliyeti taşır.
- **`MarketDataProvider`** (base class, `DataProvider/`): `protected List<StockData> _data`,
  fiyat/zaman çıkarma metodları (`GetClosePrices` vb.), `IsInitialized/IsDataRead/IsDataReady`
  (üçü aynı), `GetData(start,end)`, `GetDataInfo()`. `StockDataReader` bunu extend ediyor.
- **`StockDataReader : MarketDataProvider, IDisposable`**: `#`-satırlı meta veri + `;`-ayraçlı
  veri formatı okur. `ReadDataFast()`: `File.ReadLines().AsParallel()` + `ConcurrentBag` +
  `Interlocked` progress, `FormatException` olan satırlar sessizce atlanır. `FilterMode` enum
  (`All/LastN/FirstN/IndexRange/AfterDateTime/BeforeDateTime/DateTimeRange`)
  `AppConfig.ReadDataConfig`'ten geliyor. Yazma metodları (`WriteToCsvFile/WriteToTxtFile`) hep
  `FileShare.ReadWrite`. Event'ler: `OnReadMetaData/OnReadData/OnProgress`.

### 5.2 AppConfig Sistemi (`AppConfig/*`)
Olgun, JSON tabanlı büyük bir katman:
- `AppConfig.cs`: kök model + `SingleTraderConfig/MultipleTraderConfig/SingleTraderOptConfig`
  ağaçları + **`ConfirmingSingleTraderConfig`/`ConfirmingMultipleTraderConfig`** (bkz. §2.9).
  `MultipleTraderConfig.ConsensusConfig` (`Net/Majority/All/Any` + `MinNetCount`) config modeli
  `MultipleTrader.BuildConsensusSignal()`'a **tam bağlı** (bkz. §2.3, düzeltildi 2026-08-21).
  `AppSettingsConfig.AutoRunMode` ile menüsüz otomatik çalıştırma var. Export tarafında versiyonlu
  (v1/v2) `SingleTraderExportConfig` + `SetSingleTraderExportConfig`/
  `SetConfirmingSignalTraderExportConfig` ile tüm Trader/ConfirmingTrader varyantlarına export
  desteği bağlanmış (bkz. [export-adimlar.md](export-adimlar.md), TAMAMLANDI).
- `AppConfigApplier.cs`: config → `AlgoTrader` köprüsü. `ApplyMultipleTrader()` child'ların
  benzersiz Strategy/Query/ECF kombinasyonlarını dedupe edip id-map kuruyor, dosya adlarına
  `{prefix}_Main_/{prefix}_Child{i}_` prefixi otomatik ekliyor. `BuildInitialTradeParams()`:
  `MarketType` enum → 14 farklı `SetKontratParamsXxx()` — **kırılgan nokta**: komisyon/kayma
  parametreleri `SetKontratParams*` tarafından resetlendiği için iki kez set ediliyor.
- `AppConfigLoader.cs`: dosya yoksa/parse hatasında **sessizce** default'a düşüyor (exception
  fırlatmıyor).
- `AppSettings.cs`: `AppContext.BaseDirectory`'den 4 seviye yukarı çıkarak proje kökünü
  buluyor; tüm klasör yollarını (`InputsDir/ConfigsDir/OutputsDir/LogsDir/...`) sağlıyor.

### 5.3 Logging (`Logging/*`)
Kendi yazılmış sistem (Serilog referanslı ama kullanılmıyor). `LogManager` singleton:
`ConcurrentQueue<LogEntry>` buffer (max 10000), `List<ILogSink>`. Çift API: static
(`LogManager.Log/LogInfo/...`) ve instance (`WriteLog/.../LogRawInstance`) — dosya sonundaki TODO
bloğu bu static→instance geçişinin **kısmen** tamamlandığını gösteriyor. `LogSinks` `[Flags]`
enum ile bitwise hedefleme (`Console|File|Debug|Network|Gui`). **Min-level filtreleme yok** — her
seviye her zaman gönderiliyor. Sink implementasyonları: `ConsoleSink` (level-renk), `DebugSink`,
`FileSink` (batch+timer, `FileShare.ReadWrite`), `NetworkSink` (UDP, fire-and-forget
`Task.Run`). **`LogSinks.RichTextBox/TextBox/ListBox` enum'da var ama karşılık gelen sink
sınıfları yazılmamış** — WinForms GUI logging entegrasyonu eksik.

### 5.4 Timer (`TimeManager.cs`)
Thread-safe singleton, `ConcurrentDictionary<string, Stopwatch>` ile isimli çoklu zamanlayıcı
(`StartTimer/StopTimer/ResetTimer/RestartTimer(id)`, toplu `StartAll/StopAll/ResetAll`,
`GetAllTimers()`).

### 5.5 FileUtils
Merkezi static I/O yardımcısı, tüm yazmalar `FileShare.ReadWrite`, hatalar exception değil
`FileOpResult` ile raporlanıyor (Debug'a düşüyor — **sessiz başarısızlık riski**).
`CreateWriter()` hata durumunda `null` döner, null-check yapılmazsa NRE riski. Eski API isimleri
(`WriteTextShared` vb.) geriye uyumluluk için korunmuş.

### 5.6 Scripting (Roslyn, `Scripting/*`)
`ScriptExecutor`: **sandbox YOK**, tam erişimli. Assembly'nin kendisi referans olarak eklenmiş →
script tüm proje sınıflarına erişebiliyor. `#load "x.csx"` **regex tabanlı elle yazılmış
inliner** (gerçek Roslyn `#load` değil), rekürsif, using'leri toplayıp başa taşıyor.
`CancellationToken` desteği var.
`ScriptGlobals`: script'e `algoTrader/stockData/Trader/Indicators/TotalBars` +
`Log/SendResult/OnProgress/OnSignal/RunAll/Setup` helper'ları enjekte ediyor.
`OnProgress/OnSignal` subscribe olduğu event'leri `Cleanup()` ile unsubscribe ediyor — **script
sonunda `Cleanup()` çağrılmazsa event handler leak riski**.

### 5.7 Python Köprüsü — İKİ PARALEL MEKANİZMA
1. **`PythonPlotter.cs`** (pythonnet, in-process): process-başına global singleton engine.
   Python DLL için **hardcoded yol listesi** (Python 3.11-3.13, `C:\Program Files\...`) —
   taşınabilirlik riski. `sys.path`'e `.venv/Lib/site-packages` ekleniyor.
   `PlotSingleTraderData/PlotMultipleTraderData`: `trader.lists` verilerini `PyList/PyDict`'e
   kopyalayıp `main.print_data_info()` (Python) çağırıyor; stdout `io.StringIO` ile yakalanıp C#
   loguna yönlendiriliyor. `Shutdown()` explicit çağrılmalı (`Dispose()` içinde değil).
2. **`DearPyGuiDataPlotter.cs` + `NpzWriter.cs` + `TradeDataBundleConverter.cs`**: pythonnet
   kullanmaz, **ayrı process** olarak `src/DearPyGuiDataPlotter` çalıştırılır; veri aktarımı
   dosya-tabanlı runtime-command protokolüyle (`.tmp`→atomik rename→`.json`, Python tarafı poll
   ediyor). `NpzWriter`: numpy'siz, saf C# ile `.npy`/`.npz` formatı üreten özel yazıcı (UTF-32LE
   `<U{N}` string encoding dahil). `TradeDataBundleConverter`: 6-7 panelli `view.json` dashboard
   tanımı üretiyor, dense sinyal listesini sparse event-koduna çeviriyor.

Bu ikinci mekanizma Console.Program.cs'de açıkça "TODO: demo/test, silinecek" işaretli — gerçek
"PlotBackend switch" (ikisinden birini seçme) henüz yok, ikisi de aynı anda çalıştırılıyor (ayrıntı:
[yapilacak.md](yapilacak.md), [todo.md](todo.md)).

### 5.8 Query Altyapısı
`IQuery`/`BaseQuery` (`IStrategy`/`BaseStrategy` ile paralel tasarım) + `QueryConfigLoader`
(`QueryConfig.txt`, pipe-ayraçlı) + `QueryRegistry` (reflection-based auto-discover + best-match
constructor bulma, statik `Create()` factory desteği) + tek somut implementasyon
**`SimpleQuery1`** (MA8/MA200 kesişimi + trader-state sorgusu). `AppConfig`'te
`RunMode: TradeOnly|TradeAndQuery|QueryOnly` ayrımı var. Not: `QueryRegistry` boş isimde
`"SimpleMAQuery"`e fallback yapıyor ama gerçek sınıf adı `SimpleQuery1` — bu fallback muhtemelen
güncel değil.

---

## 6. Uygulama Katmanı (Console / WinForms)

### 6.1 Console Uygulaması (4169 satır, top-level statements — 2026-08-18'de 2076 satırdı)
Menü: `[1] Read Data`, `[2-4] SingleTrader/MultipleTrader/SingleTraderOptimizer`, `[5-7]
Read+Run varyantları`, `[8] Run Script`, `[9] DearPyGuiDataPlotter Test` (kod içinde "silinecek"
notlu), `[10]-[21]` Scanner ailesi (Strateji/Sorgu × Sembol/Timeframe/SembolxTimeframe tarama
menüleri, bkz. §2.10), `[22]-[25]` Confirming* menüleri (bkz. §2.9), `[0] Exit`. Varsayılan seçim
`"5"`. `AutoRunMode` ile menüsüz otomatik çalıştırma var. Her mod için renkli JSON "Preview"
ekranı (`[ENTER]` çalıştır, `[E]` config düzenle+reload, `[R]` reload, `[T]` timer pause, `[B]`
geri). Dosya yazma (`WriteTraderDataToFilesAsync`) plot ile paralelleştiriliyor (async başlatılıp
sonra await). Kendi yazılmış geri-sayımlı/duraklatılabilir konsol input okuyucusu var
(`ReadMenuInputWithTimeout`).

### 6.2 WinForms Uygulaması
**Neredeyse tamamen iskelet.** `MainForm.cs` (52 satır): `AlgoTrader("MyStrategy")` +
`.Start()/.Stop()/.MessageReceived` kullanıyor. **Kritik tutarsızlık**: bu API şekli Console'un
kullandığı gerçek `AlgoTrader` akışıyla (`RegisterLogger/SetData/ConfigureStrategyFromConfig/
Initialize/RunSingleTraderWithProgressAsync`) örtüşmüyor — doğrulanmalı. Referanslı grafik
paketleri (`OpenTK`, `ScottPlot.WinForms`, `SkiaSharp`) hiç kullanılmıyor. GUI log sink'leri de
yazılmamış (bkz. §5.3).

---

## 7. Genel Özet — Migration Guide Roadmap Durumu

`migration-guide.md`'nin "Yol Haritası" bölümündeki 1-10 maddesinin bu analize göre **gerçek**
durumu (detaylı TODO listesi migration-guide.md'ye eklendi):

| # | Madde | Gerçek Durum |
|---|---|---|
| 1 | SingleTrader | TAMAMLANDI (temel çalışan yapı) |
| 2 | MultiTrader | **TAMAMLANDI** (`MultipleTrader.cs` çalışıyor, Consensus modu Net/Majority/All/Any hepsi implement — düzeltildi 2026-08-21, bkz. §2.3) |
| 3 | Sanal işlem / Getiri Eğrisi konfirmasyonu | **TAMAMLANDI** (2026-08-19, `ConfirmingSingleTrader`/`ConfirmingMultipleTrader` + `VirtualPositionConfirmer`, bkz. §2.9 — önceki analizde "YAPILMADI" işaretliydi, düzeltildi) |
| 4 | SingleTraderOptimization | **TAMAMLANDI** (`SingleTraderOptimizer.cs` tam grid-search motoru) |
| 5.1a | Scripting — tam erişimli mod | TAMAMLANDI (`ScriptExecutor`) |
| 5.1b | Scripting — sandbox mod | YAPILMADI |
| 5.1c | Scripting — dosyadan dinamik yükleme | TAMAMLANDI (.csx + `#load`); GUI'den hazırlama YAPILMADI (WinForms iskelet) |
| 5.2 | Optimization için scripting | KISMEN (config katmanı var, script entegrasyonu doğrulanmadı) |
| 5.3 | MultiTrader için scripting | YAPILMADI |
| 6 | Sorgu yapabilme | **KISMEN** (IQuery/BaseQuery/QueryRegistry iskeleti tam, tek örnek `SimpleQuery1`) |
| 7 | Performans hesaplaması | SingleTrader için TAMAMLANDI (`Statistics.PerformansRow`); MultiTrader için doğrulanmadı |
| 8 | Toplu sembol taraması | **TAMAMLANDI** (2026-08-18) — tüm tarama matrisi (12 Scanner sınıfı, bkz. §2.10), Console `[10]`-`[21]`, bkz. [todo.md](todo.md) "Tarama Motorları" bölümü |
| 9 | Sorgu + toplu sembol | **TAMAMLANDI** (2026-08-18) — `QuerySymbolScanner`, Console `[16]` |
| 10 | Strateji karşılaştırma raporu | YAPILMADI |

---

## 8. Doğrulanması/Temizlenmesi Önerilen Noktalar

Aşağıdakiler "muhtemelen ölü kod" veya "tutarsız/riskli" olarak işaretlendi, ileride tek tek
doğrulanıp temizlenmeli veya bilinçli olarak dokümante edilmeli:

1. `Core/KarZarar.cs` — `SingleTrader`'ın private PnL metodlarıyla neredeyse birebir aynı,
   çağrıldığı yer bulunamadı.
2. `Core/zTradeStatistics.cs` — `Statistics.cs`'in yanında kullanılmıyor gibi.
3. `SingleTrader.ClosePositionEOD_2()` — çağrıldığı yer bulunamadı.
4. `Core/TimeUtils.cs` session-tracking API'si (`UpdateTime`, `IsWithinTradingHours`) —
   `check_bar_*_with()` ile paralel, kullanılmıyor gibi.
5. `AlgoTrader.OnApplyUserFlags/OnApplyUserFlags2` — comment-out, `ApplySingleTraderFlagsConfigs()`
   ile değiştirilmiş.
6. `MultipleTrader.DynamicPositionSizeEnabled` — gövdesi TODO, işlevsiz.
7. `SingleTraderOptimizer.GetBestResult()` (NetProfit) vs dosya çıktı sıralaması (`SortField`) —
   olası tutarsızlık.
8. `MAMethod` enum (Base/MAMethod.cs) ile gerçek implement edilen 66 MA metodu arasında tam
   örtüşme kontrolü yapılmalı (`MAMethodExtensions.IsImplemented()` üzerinden).
9. `PriceAction.HHV/LLV` ile `PriceUtils.HHV/LLV` duplikasyonu.
10. `THULL` (Compound MA) ile `THMA` (Specialized MA) isim/kavram çakışması.
11. `WinForms/MainForm.cs`'in kullandığı `AlgoTrader.Start()/Stop()/MessageReceived` API'sinin
    hâlâ mevcut olup olmadığı ve Console'un kullandığı akışla ilişkisi doğrulanmalı.
12. `Logging/LogSinks` enum'undaki `RichTextBox/TextBox/ListBox` değerlerine karşılık gelen sink
    sınıfları yazılmamış.
13. `QueryRegistry`'nin boş-isim fallback'i (`"SimpleMAQuery"`) gerçek sınıf adıyla
    (`SimpleQuery1`) uyuşmuyor.
