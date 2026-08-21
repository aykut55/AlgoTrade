# Scripting Rehberi

> `[8] Run Script` menüsünün arkasındaki mekanizmayı ve kendi script'ini nasıl yazacağını anlatır.
> Bu proje için scripting **çok güçlü** bir özellik — script'in proje assembly'sinin tamamına
> (sandbox YOK) erişimi var, yani neredeyse her şeyi (yeni Trader tipleri manuel kurmak, özel
> consensus kuralları, tek seferlik deneyler) menüye hiç dokunmadan yapabilirsin. Bu güç henüz
> tam kullanılmadı — şu ana kadarki örnek script'ler bilinçli olarak basit tutuldu (bkz.
> [inputs/scripts/readme.txt](../../inputs/scripts/readme.txt)). Yazım tarihi: 2026-08-21.

## İçindekiler

1. [Mekanizma: ScriptExecutor + ScriptGlobals](#1-mekanizma-scriptexecutor--scriptglobals)
2. [Script'e Neler Enjekte Ediliyor](#2-scripte-neler-enjekte-ediliyor)
3. [Script Yazarken Bilmen Gerekenler](#3-script-yazarken-bilmen-gerekenler)
4. [Üç Kullanım Seviyesi](#4-üç-kullanım-seviyesi)
5. [Worked Example: CustomConsensusExample.csx](#5-worked-example-customconsensusexamplecsx)
6. [Mevcut Script Envanteri](#6-mevcut-script-envanteri)
7. [Sınırlamalar ve Riskler](#7-sınırlamalar-ve-riskler)

---

## 1. Mekanizma: ScriptExecutor + ScriptGlobals

`src/AlgoTrade.Core/Scripting/ScriptExecutor.cs` — Roslyn (`Microsoft.CodeAnalysis.CSharp.Scripting`)
tabanlı çalıştırıcı:

- `CompileScript(code, sourceDirectory?)` — önce `#load "x.csx"` direktiflerini kendi yazdığı bir
  regex-tabanlı inliner ile açar (gerçek Roslyn `#load` değil — recursive, using'leri toplayıp
  başa taşıyor), sonra `CSharpScript.Create<object>(code, options, globalsType: typeof(ScriptGlobals))`
  ile derler.
- `RunCompiledAsync(globals, cancellationToken)` — derlenmiş script'i çalıştırır,
  `CancellationTokenSource` ile ESC'den iptal edilebilir.
- `ExecuteAsync(...)` — ikisini birleştiren convenience metod (Console `[8]` menüsü bunu kullanır).

**Sandbox yok**: constructor'da `.AddReferences(..., Assembly.GetExecutingAssembly())` ile
projenin **tüm** derlenmiş sınıfları script'e referans olarak veriliyor. Script içinden
`SingleTrader`, `MultipleTrader`, `IndicatorManager`, hatta `StatisticsExporter` gibi "iç" sınıflara
bile doğrudan erişilebiliyor — tek şart `public` olmaları (internal üyeler görünmez, script ayrı
bir derlenen assembly).

## 2. Script'e Neler Enjekte Ediliyor

`ScriptGlobals` (`src/AlgoTrade.Core/Scripting/ScriptGlobals.cs`) script'in gördüğü **tek** global
nesne — property'leri ve metodları script içinde prefix'siz kullanılabiliyor:

| Üye | Ne işe yarar |
|---|---|
| `algoTrader` | O anki `AlgoTrader` instance'ı — `.SingleTrader`/`.MultipleTrader`/`.SingleTraderOptimizer`/`.ConfirmingSingleTrader`/`.ConfirmingMultipleTrader` property'leriyle her şeye erişim |
| `stockData` | `List<StockData>` — Console'da daha önce okunmuş veri (varsa) |
| `Trader` | `algoTrader?.SingleTrader` kısayolu |
| `Indicators` | `algoTrader?.indicators` kısayolu |
| `TotalBars` | `stockData.Count` kısayolu |
| `IsCancellationRequested` | ESC ile iptal edildiyse `true` — uzun döngülerde kontrol et |
| `Log(msg)` / `Log(format, args)` | Script çıktısını Console'a yazar (zaman damgalı) |
| `SendResult(key, value)` / `SendMessage(msg)` | Script → host sonuç bildirimi (`[RESULT] key: value` olarak basılır) |
| `ClearOutput()` | Çıktı ekranını temizler |
| `OnProgress(callback, intervalBars=1000)` | **Sadece `algoTrader.SingleTrader` için** — ilerleme event'ine abone olur |
| `OnSignal(callback)` | **Sadece `algoTrader.SingleTrader` için** — sinyal event'ine abone olur |
| `Setup(strategyName, parameters?)` | `SetData` + `Initialize()` + `ConfigureStrategy(...)` — SingleTrader için hızlı kurulum |
| `RunAll(progressInterval=0)` | `algoTrader.SingleTrader`'ı tüm barlarda çalıştırır (`trader.Run(i)` döngüsü) |
| `Cleanup()` | `OnProgress`/`OnSignal` abonelerini kaldırır |

**Dikkat**: `OnProgress`/`OnSignal`/`RunAll`/`Setup` sadece **SingleTrader** için yazılmış —
`MultipleTrader`/`ConfirmingXxx` ile çalışırken bu kolaylıkları kullanamazsın, kendi döngünü
`multipleTrader.Run(i)` ile elle yazman gerekir (bkz. §4, §5). Bu, kolaylık metodlarının
sadeleştirilmiş/genişletilmesi gereken bir alan — istersen `OnProgress`/`RunAll`'ın
`MultipleTrader` karşılıklarını (`OnMultipleTraderProgress`, `RunAllMultiple` gibi) eklemek
küçük bir iş olur.

`Cleanup()` script sonunda **elle** çağrılmazsa, `OnProgress`/`OnSignal` abone olduğu event
handler'ları unsubscribe olmaz — uzun ömürlü `algoTrader` nesnesi varsa event handler leak riski
(bkz. [PROJECT_ANALYSIS.md](../PROJECT_ANALYSIS.md) §5.6).

## 3. Script Yazarken Bilmen Gerekenler

`inputs/scripts/readme.txt`'in NOT bölümü (satır 122-144) kesin referans, özetle:

- Otomatik import edilen namespace'ler: `System`, `System.Collections.Generic`, `System.Linq`,
  `System.Threading.Tasks`, `AlgoTrade.Core`, `AlgoTrade.Core.Trading`,
  `AlgoTrade.Core.Trading.Core`, `AlgoTrade.Core.Trading.Strategies`,
  `AlgoTrade.Core.Trading.Strategy`, `AlgoTrade.Core.Trading.Indicators`,
  `AlgoTrade.Core.Trading.Queries`, `AlgoTrade.Core.Trading.Query`,
  `AlgoTrade.Core.StockDataReader`, `AlgoTrade.Core.Logging`, `AlgoTrade.Core.Scripting`.
- **Otomatik İMPORT EDİLMEYENLER**: `System.IO`, `AlgoTrade.Core.AppConfig`,
  `AlgoTrade.Core.Timer` — `Path`/`File`, `AppConfigLoader`/`AppConfigApplier`,
  `TimeManager` kullanan her script bunları kendi başına `using` ile eklemeli.
- `algoTrader.RunXxxWithProgressAsync()` çağıran her script, çağırmadan önce **mutlaka**
  `algoTrader.RegisterLogger(LogManager.GetInstance())` ve
  `algoTrader.RegisterTimer(TimeManager.GetInstance())` yapmalı — yoksa AlgoTrader'ın iç `_timer`
  alanı `null` kalıp `NullReferenceException` verir.
- Top-level statement stili (using'ler hariç `class`/`namespace` sarmalayıcısı yok), local
  function tanımlamak (`TradeSignals MyFunc(...) { ... }`) serbest ve çalışıyor (bkz. §5).
- `Log`/`SendResult` çıktıları hem `[8]` menüsündeki canlı akışta hem de programatik test
  harness'lerinde (`ScriptExecutor` doğrudan çağrılarak) görülebilir.

## 4. Üç Kullanım Seviyesi

**A) `algoTrader.RunXxxWithProgressAsync()` ile — en az kod, en az kontrol.**
`AlgoTrader`'ın kendi orkestrasyonunu kullanırsın (`Configure*`/`AddXxxConfig` ile besleyip
`RunSingleTraderWithProgressAsync()`/`RunMultipleTraderWithProgressAsync()` çağırırsın). Bkz.
`inputs/scripts/runSingleTraderWithStrategy.csx`, `runMultiTraderWithStrategies.csx`. **Kısıtlama**:
`AlgoTrader` nesneyi (`multipleTrader` gibi) kendi içinde yaratıp aynı çağrıda çalıştırdığı için,
oluşturulduktan hemen sonra ama Run döngüsünden önce nesneye elle bir şey enjekte edemezsin (örn.
`CustomConsensusFunc` atayamazsın) — bkz. [02-console-menu-guide.md](02-console-menu-guide.md) §2.

**B) Manuel kurulum, `AlgoTrader`'ı sadece factory olarak kullanarak — orta kontrol.**
`algoTrader.CreateStrategyFromRegistry(data, indicators, name, parameters)` ile strateji
yarat, `new SingleTrader(...)`/`new MultipleTrader(...)`'ı **kendin** kur, `Init()`'ten sonra
istediğin property'yi (örn. `CustomConsensusFunc`) ata, sonra kendi `for` döngünle çalıştır. Bkz.
`inputs/scripts/02_RunMultipleTraderWithProgressAsync.csx` ve §5'teki örnek. Bu seviye, hazır
Trader sınıflarının **tasarlanmış genişletme noktalarını** (delegate property'ler, config
alanları) kullanmak istediğinde gerekiyor.

**C) Tamamen serbest — tam kontrol.**
`SingleTrader`/`MultipleTrader`'ın hiç kullanmadığı bir akış istiyorsan (örn. iki ayrı
`MultipleTrader`'ı kendi yazdığın bir üst-seviye mantıkla birleştirmek, ya da hiç var olmayan bir
Trader kombinasyonu denemek), script içinde bu sınıfların public API'sini serbestçe
kompoze edebilirsin — sandbox olmadığı için tek sınır C# dil kuralları ve sınıfların `public`
yüzeyi. Henüz kimse bu seviyede bir örnek yazmadı; ihtiyaç doğduğunda buraya eklenmeli.

## 5. Worked Example: CustomConsensusExample.csx

[`inputs/scripts/CustomConsensusExample.csx`](../../inputs/scripts/CustomConsensusExample.csx)
— Seviye B'nin tam bir örneği, `MultipleTrader.CustomConsensusFunc` genişletme noktasını
(bkz. [01-class-reference.md](01-class-reference.md) `MultipleTrader` bölümü) kullanıyor:

1. Veri okunur, `IndicatorManager` yaratılır.
2. `algoTrader.CreateStrategyFromRegistry(...)` ile 2 ayrı strateji instance'ı yaratılır (child'lar
   için).
3. `MultipleTrader` **manuel** kurulur (`new MultipleTrader(...)`, mainTrader + `AddTrader(child)`
   x2, `Init()`).
4. Tam burada, Run döngüsünden ÖNCE: `multipleTrader.CustomConsensusFunc = FirstChildWinsConsensus;`
   atanır — Seviye A'da bu adım imkansız olurdu.
5. 7 farklı hazır referans method (`NetConsensusReference`, `MajorityConsensusReference`,
   `AllConsensusReference`, `AnyConsensusReference`, `FirstChildWinsConsensus`,
   `WeightedConsensus`, `BothAgreeConsensus`) local function olarak tanımlı — hangisinin aktif
   olacağı tek satır değiştirilerek seçiliyor. İlk 4'ü `MultipleTrader.cs`'teki hardcoded switch'in
   birebir script karşılığı (kendi kuralını yazarken şablon olarak kullanılabilir), son 3'ü hazır
   modların üretemeyeceği özel örnekler.
6. Kendi `for (int i = 0; i < totalBars; i++) multipleTrader.Run(i);` döngüsü elle yazılıyor.
7. Sonuçlar `multipleTrader.WriteMultipleTraderStatistics(...)` ile karşılaştırmalı tek dosyaya
   yazılıyor.

Bu script hem izole bir birim testiyle (gerçek veriden bağımsız, `CustomConsensusFunc` set/unset
döngüsü) hem gerçek veriyle (1.9M bar) uçtan uca doğrulandı — doğrulama metodolojisi için bu
konuşmanın ilgili bölümüne bkz. (test harness: proje dışı scratchpad'de, `AlgoTrade.Core`'a
`ProjectReference` veren tek-dosyalık bir konsol uygulaması; `ScriptExecutor.CompileScript()` ile
sadece derleme kontrolü, `ExecuteAsync()` ile tam koşum yapıldı).

## 6. Mevcut Script Envanteri

Tam liste ve kategoriler: [`inputs/scripts/readme.txt`](../../inputs/scripts/readme.txt) (elle
güncelleniyor — yeni script eklerken/silerken orayı da güncelle). Özet kategoriler:

1. **01-19**: Console menülerinin `[8]`'den çalıştırılabilen tek-seferlik script hali (her
   `handleXxx()`/`runXxxAlgoTrade()` çiftinin interaktif döngüsüz versiyonu).
2. **Config_*.csx**: `#load` ile çağrılan, sadece değişken tanımlarından oluşan config dosyaları.
3. **mainScript*.csx**: en eski/en büyük "hepsi bir arada" demo scriptleri (01/02/03'ün atası).
4. **Bağımsız küçük örnekler**: `paramSweep.csx`, `runSingleTraderWithStrategy.csx`,
   `runMultiTraderWithStrategies.csx`, artık **`CustomConsensusExample.csx`**.
5. **Test/örnek amaçlı**: `console_scripts.csx` (syntax örneği), `test_hello.csx` (sağlık kontrolü).

## 7. Sınırlamalar ve Riskler

- **Sandbox yok** — script proje içindeki her şeye erişebilir, dosya sistemi/network dahil.
  Güvenilmeyen script çalıştırma senaryosu için Madde 5.1b ("Sandbox Mod") hâlâ yazılmadı (bkz.
  [migration-guide.md](../migration-guide.md)) — şu an bilinçli olarak ertelenmiş durumda.
- `#load` inliner'ı gerçek Roslyn `#load` değil, elle yazılmış regex — çok iç içe/döngüsel
  `#load` zincirlerinde beklenmedik davranış riski taşıyabilir (henüz rapor edilmiş bir sorun yok).
- `AppSettings` sınıfı (`RootDir`/`ConfigsDir`/`LogsDir` vb.) çalışan **executable**'ın
  `AppContext.BaseDirectory`'sinden 4 seviye yukarı çıkarak proje kökünü buluyor
  (`src/AlgoTrade.Core/AppSettings.cs:8-9`) — script'i `AlgoTrade.Console`'un kendi `[8]` menüsü
  dışında (örn. ayrı bir test harness'ten) çalıştırırsan bu yol yanlış çözülür, `inputs/configs/`
  altındaki dosyalar bulunamaz (bu doküman hazırlanırken karşılaşılan gerçek bir durum — çözümü:
  fake bir `inputs/`+`outputs/` klasör yapısını doğru göreli derinlikte oluşturmak).

---

## İlgili Dosyalar

- [01-class-reference.md](01-class-reference.md) — script'ten erişilen sınıfların API referansı
- [02-console-menu-guide.md](02-console-menu-guide.md) — `[8]` menüsünün Console tarafındaki yeri
- [04-variant-catalog.md](04-variant-catalog.md) — script ile denenebilecek, henüz denenmemiş varyantlar
- [../migration-guide.md](../migration-guide.md) Madde 5 — scripting yol haritası durumu
- [../todo.md](../todo.md) "Script Yeteneği" bölümü — bu rehberin kaynaklandığı ilk analiz
