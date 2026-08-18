# Tarama Motorları (Yapı Taşı A ve C) — Mimari ve Durum

> Bu belge, `docs/todo.md`'deki "Tarama Matrisi Analizi" bölümünde tanımlanan 3 yapı taşından
> (A/B/C) ikisinin — C (Sembol Tarama) ve A (Zaman Dilimi Tarama) — tasarımını ve uygulanmış
> durumunu kaydeder. B (MultipleTrader consensus modları) ayrı, daha küçük bir iş olduğu için
> burada değil, doğrudan `MultipleTrader.cs`/`AppConfig.cs` içinde belgelendi. Kaynak: plan-mode
> oturumları (2026-08-18), kullanıcı ile soru-cevap şeklinde netleştirildi.

## Durum: TAMAMLANDI (v1 — Console entegrasyonu dahil)

İlk taslakta (bkz. eski plan) Console menü entegrasyonu "sonraki adım" olarak kapsam dışı
bırakılmıştı; kullanıcı sonradan doğrudan menüye eklenmesini istedi, o yüzden v1 hem çekirdek
motoru hem de Console `[10] Tarama` menü seçeneğini içeriyor.

## Neden Sıfırdan Bir Mimari (SingleTraderOptimizer'dan Bağımsız)

`SingleTraderOptimizer` aynı veri üzerinde **parametre** değiştirerek çok sayıda backtest
çalıştırır (kombinasyon üretimi: `GenerateParameterCombinations`). Sembol taraması ise aynı
strateji/parametrelerle **veriyi (dosyayı)** değiştirerek çok sayıda backtest çalıştırır. Bu
yapısal fark — I/O-ağırlıklı vs CPU-ağırlıklı, kombinasyon üretimi gereksiz — kod paylaşımını
anlamsız kılıyor. Ortak olan sadece dış iskelet: "N bağımsız çalıştırma → sonuç satırı topla →
sırala/yaz". `SymbolScanner`, `AlgoTrader`'a da bilinçli olarak bağlı değil (AlgoTrader tek veri
seti varsayımıyla kurulu); her sembol için kendi `StockDataReader`/`IndicatorManager`/
`SingleTrader` nesnelerini kurup sembol bazında `Dispose` eder.

## Önemli Keşifler (tasarımı şekillendirdi)

- Diskte veri zaten `C:\data\csvFiles\<VarlıkSınıfı>\<ZamanDilimi>\<Sembol>.csv` şeklinde organize
  (örn. `CRP\05\BTCUSDT_BNC.csv`) — resampling gerekmiyor, sadece çoklu dosya okuma (bkz. Yapı
  Taşı A notu aşağıda).
- `SingleTrader.cs` (satır ~200-245) zaten `SonYon`, `SonKarZararFiyat`, `SonKarZararYuzde`,
  `SonSinyaldenBeriBarSayisi` ve literal olarak **`TaramaOzeti`** adlı property'ler içeriyor —
  "şu an bu sembolde ne oluyor" (canlı durum) bilgisini hazır veriyor. Tarama sonucu satırına
  hem backtest performansı hem bu canlı durum bilgisi birlikte ekleniyor.
- `SingleTrader.GetStatisticsHeaderRow(";")`/`GetStatisticsDataRow(";")` (satır ~2670-2680) zaten
  `StatisticsExporterConfig.json`'a göre config-driven, tek satırlık bir performans özeti
  üretiyor — Optimizer'ın `OptimizationResult`/`GetOptimizationSummary()` dictionary'sinden daha
  basit; doğrudan kullanılıyor. `statistics.GetOptimizationSummary()` (public field, ekstra
  passthrough method'a gerek kalmadı) `SortField` çözümlemesi için ayrıca kullanılıyor.

## ⚠️ Kritik Bug (uçtan uca testte bulundu ve düzeltildi)

İlk implementasyonda taranan her sembol **hiçbir zaman pozisyon açmıyordu** (sürekli Flat,
`SonSinyaldenBeriBarSayisi` = toplam bar sayısı - 1). Kök neden: `SingleTrader.Reset()` /
`ConfigureUserFlagsOnce()` **tüm sinyal etkinleştirme bayraklarını (`AlEnabled`, `SatEnabled`,
`FlatOlEnabled`, `PasGecEnabled`, `KarAlEnabled`, `ZararKesEnabled`, `GunSonuPozKapatEnabled`)
varsayılan olarak `false` yapar** — `AlgoTrader.RunSingleTraderWithProgressAsync()` bunları
private `ApplySingleTraderFlagsConfigs()` metoduyla `AppConfig.SingleTrader.Signals`'tan enable
ediyor, ama `SymbolScanner` `AlgoTrader`'ı hiç kullanmadığı için bu adım atlanmış oldu.

**Düzeltme**: `SymbolScanOptions`'a `AlEnabled`/`SatEnabled`/vb. 7 bayrak eklendi (varsayılanlar
`AppConfig.TraderSignalsConfig` ile aynı: hepsi `true`, `GunSonuPozKapatEnabled` hariç), ve
`SymbolScanConfig`'e de bir `Signals` (`TraderSignalsConfig`) alanı eklendi.
`SymbolScanner.RunSingleSymbol()` her sembol için `trader.ConfigureUserFlagsOnce()` çağırıp
ardından bu bayrakları açıkça set ediyor — `AlgoTrader`'ın yaptığının aynısı, sadece bağımsız bir
kopyası.

**Doğrulama**: Gerçek veri üzerinde (BTCUSDT_BNC + ETHUSDT_BNC, ~904K bar/sembol) uçtan uca test
edildi. Düzeltme öncesi: 0 işlem, sabit Flat. Düzeltme sonrası: BTCUSDT 25522 alış + 12761 satış
işlemi, gerçek NetProfit/ProfitFactor/DrawDown rakamları, `WriteSortedResults` doğru sıralama
(ETHUSDT NetProfit=+10.7 > BTCUSDT NetProfit=-1216.71).

## Dosyalar

| Dosya | Değişiklik |
|---|---|
| `src/AlgoTrade.Core/Trading/Traders/SymbolScanner.cs` | YENİ — `SymbolScanner`, `SymbolScanOptions`, `ScanResult` |
| `src/AlgoTrade.Core/AppSettings.cs` | `ScanLogsDir` eklendi (`outputs/scan`) |
| `src/AlgoTrade.Core/AppConfig/AppConfig.cs` | `SymbolScanConfig` + alt config sınıfları (`SymbolScanSortConfig`, `SymbolScanSaveConfig`), root `AppConfig.SymbolScan` alanı |
| `src/AlgoTrade.Core/AppConfig/AppConfigApplier.cs` | `BuildSymbolScanOptions(SymbolScanConfig, configsDir)` — AlgoTrader'a değil, doğrudan `SymbolScanOptions`'a çevirir |
| `inputs/configs/AppConfig/AppConfig.json` | `SymbolScan` bölümü eklendi (varsayılan: `CRP\05` klasörü, AutoDiscover, SimpleMostStrategy v1) |
| `AlgoTrade.Console/Program.cs` | `[10] Tarama` menü seçeneği, `handleSymbolScan()`, `runSymbolScan()`, `showModeConfigSummary("SymbolScan")` |

## SymbolScanner API Özeti

```csharp
public void Run(SymbolScanOptions options, string csvPath, string txtPath, CancellationToken ct = default);
public void WriteSortedResults(SymbolScanOptions options, string sortedCsvPath, string sortedTxtPath);
public ScanResult? GetBestResult(SymbolScanOptions options);
public List<ScanResult> Results { get; }
public Action<int,int,string>? OnProgress { get; set; }
```

Her sembol için: dosya oku (fail-soft — dosya yok/veri boş/strateji hatası → `Success=false`,
tarama devam eder) → `IndicatorManager` → `StrategyRegistry.CreateStrategy` → `SingleTrader` →
sinyal bayraklarını enable et → bar-bar `Run()` → `Finalize()` → sonuç satırını topla (performans
kolonları + `TaramaOzeti`) → sembol başına `Dispose`.

## Kapsam Dışı (C için, fast-follow, `docs/todo.md`'ye eklendi)

- MultipleTrader-bazlı tarama (senaryo 7 — sembol başına consensus)
- Buffered flush / partial-resume (Optimizer'daki `FileFlushIntervalMs`/`PartialOpt` benzeri)
- Zengin JSON preview ekranı (SingleTrader/MultipleTrader'daki gibi) — v1'de sadece kutu-stili
  özet var, [T] Pause/Resume Timer satırı da v1'de yok (davranışı doğrulanamadığı için eklenmedi)
- Time filtering / TradeStartBarIndex desteği (`SymbolScanOptions`'a eklenmedi, v1'de tüm
  semboller `TimeFilteringEnabled=false` ile çalışıyor)

---

# Zaman Dilimi Tarama Motoru (Yapı Taşı A) — Mimari ve Durum

## Durum: TAMAMLANDI (v1 — Console entegrasyonu dahil, 2026-08-18)

## ⚠️ Önemli Düzeltme (plan-mode sırasında netleşti)

İlk tasarım A'yı, farklı zaman dilimlerinin sinyallerini `MultipleTrader`'daki gibi bir
konsensüs ile birleştiren, "sürücü TF seçimi + zaman-hizalama" (bar index'leri farklı
granülerlikte aynı anı temsil etmiyor, timestamp bazlı hizalama gerekir) gerektiren karmaşık bir
motor olarak kurgulamıştı. **Kullanıcı bunun hiç niyeti olmadığını belirtti**: gerçek istek, aynı
sembolü seçili zaman dilimlerinde **bağımsız bağımsız** çalıştırıp sonuçlara ayrı ayrı
bakabilmekti — "bileşke" kelimesi kullanıcı tarafından sadece **strateji ekseni** için
kullanılmıştı (3. senaryo, `MultipleTrader`), zaman dilimi ekseni için hiç değil. Bu yanlış
anlama, orijinal 8-senaryo matrisindeki "Zaman Dilimi: Çoklu" ifadesinin "bileşke" ile aynı
kategoride genellenmesinden kaynaklandı — kullanıcı bunu asla söylemedi.

Bu düzeltmeyle A, **`SymbolScanner`'a (Yapı Taşı C) yapısal olarak neredeyse özdeş** hale geldi:
fark sadece hangi liste üzerinde dönüldüğü — C bir klasördeki tüm sembol dosyalarını tarar, A
aynı sembolün N farklı zaman-dilimi klasöründeki dosyasını tarar. Konsensüs/zaman-hizalama YOK,
her TF tamamen bağımsız bir backtest, N ayrı sonuç satırı.

## Tasarım Kararı

`SymbolScanner`'ı genelleştirmek yerine **yeni, bağımsız bir sınıf** (`TimeframeScanner`)
yazıldı — projenin "her biri kendi başına yeten dosya" tarzına uygun (bkz. 24 strateji
dosyasının aynı iskeleti tekrarlaması), küçük kod tekrarı pahasına. Zaman dilimleri config'te
**açık bir liste** (`Timeframes: ["01","05","15","60"]`), otomatik keşif yok.

## Doğrulanmış Zemin

- Veri yapısı doğrulandı: `BTCUSDT_BNC.csv`, `C:\data\csvFiles\CRP\` altındaki `01/05/10/15/20/
  30/60/120/240/A/G/H` klasörlerinin **hepsinde** mevcut, satır sayıları granülariteyle tutarlı
  (01→4.5M, 05→904K, 15→301K, 60→75K bar).
- `SymbolScanner`'daki kritik bug (sinyal bayrakları `ConfigureUserFlagsOnce()` sonrası
  varsayılan kapalı kalıyor, açıkça enable edilmezse trader hiç işlem açmıyor) **baştan doğru
  yazıldı** — `TimeframeScannerOptions`'ta 7 bayrak (`AlEnabled` vb.) ilk günden var.

**Doğrulama**: Gerçek veri üzerinde (BTCUSDT_BNC, 01/05/15/60 dakika) uçtan uca test edildi —
her TF farklı bar sayısı (4.5M/904K/301K/75K) ve farklı NetProfit üretti, sıralama (`SortField`)
doğru çalıştı. Sinyal bayrakları baştan `true` olduğu için ilk turdaki "sürekli Flat" bug'ı hiç
oluşmadı (`KarliIslemOrani` gibi alanlar sıfırdan farklı çıktı, işlem gerçekten alındığını
doğruladı).

## Dosyalar

| Dosya | Değişiklik |
|---|---|
| `src/AlgoTrade.Core/Trading/Traders/TimeframeScanner.cs` | YENİ — `TimeframeScanner`, `TimeframeScannerOptions`, `TimeframeScanResult`. `SymbolScanner.cs`'in yapısal kopyası. |
| `src/AlgoTrade.Core/AppConfig/AppConfig.cs` | `TimeframeScanConfig` + alt config sınıfları (`TimeframeScanSortConfig`, `TimeframeScanSaveConfig`), root `AppConfig.TimeframeScan` alanı |
| `src/AlgoTrade.Core/AppConfig/AppConfigApplier.cs` | `BuildTimeframeScanOptions(TimeframeScanConfig, configsDir)` — `BuildSymbolScanOptions`'ın birebir kopyası |
| `inputs/configs/AppConfig/AppConfig.json` | `TimeframeScan` bölümü eklendi (varsayılan: `CRP` base klasörü, `BTCUSDT_BNC`, `["01","05","15","60"]`, SimpleMostStrategy v1, `FxCrypto`) |
| `AlgoTrade.Console/Program.cs` | `[11] Tarama (Timeframe Scan)` menü seçeneği, `handleTimeframeScan()`, `runTimeframeScan()`, `showModeConfigSummary("TimeframeScan")` |

`AppSettings.cs`'e yeni bir dizin eklenmedi — çıktılar `SymbolScan` ile aynı `AppSettings.
ScanLogsDir` (`outputs/scan`) altına, farklı dosya adlarıyla (`TimeframeScanResults*`) yazılıyor.

## Kapsam Dışı (fast-follow)

- Zaman dilimleri arası konsensüs/bileşke (kullanıcı açıkça istemedi, ileride ayrı bir istek
  olarak gelebilir ama bu v1'in parçası değil)
- Otomatik TF keşfi (kullanıcı açık liste istedi)
- Senaryo 6 (Çoklu sembol, tek strateji, çoklu TF) — tasarım taslağı aşağıda, "Senaryo 6" bölümü
- Senaryo 7 (Çoklu sembol, çoklu strateji-bileşke, tek TF) — tasarım taslağı aşağıda, "Senaryo 7" bölümü
- Senaryo 8 — 6 ve 7'nin bileşimi, tasarım taslağı aşağıda, "Senaryo 8" bölümü

---

# Senaryo 4 (Tek Sembol, Çoklu Strateji-Bileşke, Çoklu TF) — TAMAMLANDI (2026-08-18)

Bu bölüm, uygulamaya başlamadan önce tasarım kararı olarak kaydedilmişti (kullanıcı isteği —
iki farklı bilgisayarda çalışıldığı için). Şimdi uygulanmış ve doğrulanmış durumda; "Tek Sembol"
sütunu (senaryo 1/2/3/4) tamamlandı.

## Doğrulama

Gerçek `AppConfig.json`'daki `MultiStrategyTimeframeScan` bölümü (2 child `SimpleMostStrategy`
v1/v2, `Consensus.Mode=Net`) ile BTCUSDT_BNC üzerinde TF=15 ve TF=60'ta uçtan uca test edildi —
`AppConfigApplier.ApplyMultipleTrader()` hiç değiştirilmeden reuse edildi, iki TF farklı sonuç
üretti (TF=15: yön A, NetProfit≈-10.05; TF=60: yön S, NetProfit≈-199.84), `GetBestResult()`
doğru TF'yi seçti.

## Amaç

Kullanıcı, 4/6/7/8'den önce **4'ü** yapmayı seçti: "Tek Sembol" sütununu (senaryo 1/2/3/4)
tamamlamak. Senaryo 4 = tek bir sembolde, birden fazla stratejinin **consensus/bileşkesini**
(`MultipleTrader`, yapı taşı B) her zaman diliminde **bağımsız bağımsız** çalıştırmak — TF'ler
arası hâlâ konsensüs YOK (A'daki düzeltmeyle tutarlı), sadece MultipleTrader'ın kendisi zaten
strateji-ekseninde konsensüs alıyor.

## ⚠️ Tasarım Sapması (bilinçli, gerekçeli)

`SymbolScanner`/`TimeframeScanner` bilinçli olarak `AlgoTrader`'ı bypass edip `SingleTrader`'ı
elle kuruyordu (basit: 1 strateji, 1 TradeParams). **`MultipleTrader` için aynı şeyi yapmak
uygun değil** — `AlgoTrader.createChildTraders()` (satır ~1462) çok daha karmaşık bir kurulum
içeriyor: strateji cache'i (`GetStrategy(config.StrategyId)`, isim+versiyona göre dedupe edilmiş
`_strategyConfigs` listesi), her child için ayrı Signals/Save/Export config'i,
`ConfigureUserFlagsOnce()` + 7 bayrak + TimeFiltering + TradeStartBarIndex, EquityCurveFilter id
eşlemesi. Bunu `TimeframeScanner`'a benzer yeni bir dosyada elle tekrar yazmak hem riskli (küçük
bir farkla üçüncü bir "unutulan bayrak" bug'ı yaratabilir) hem de zaten var olan, test edilmiş
`AppConfigApplier.ApplyMultipleTrader()` + `AlgoTrader.RunMultipleTraderWithProgressAsync()`
akışını anlamsızca kopyalamak olurdu.

**Karar**: Bu senaryoda `AlgoTrader`'ı **her TF için bir kere, tek kullanımlık (throwaway)**
olarak kullanacağız — tıpkı `TimeframeScanner`'ın her TF için taze bir `SingleTrader` kurup atması
gibi, ama bu sefer taze bir `AlgoTrader` kurup atıyoruz (`AlgoTrader` zaten `IDisposable`,
`SetData`/`RegisterLogger`/`RegisterTimer`/`Reset`/`Initialize` ile kendi kendine yeten bir API
sunuyor). Bu, "AlgoTrader tek veri seti varsayımıyla kurulu, N veri seti üzerinde döngü kurmaya
uygun değil" ilkesini bozmuyor — AlgoTrader hâlâ hiçbir zaman birden fazla dataset görmüyor,
sadece scanner sınıfı N tane *ayrı* AlgoTrader örneği kurup atıyor.

## Akış (her TF için)

1. `Path.Combine(BaseFolder, tf, Symbol + ".csv")` oku (`StockDataReader`, `TimeframeScanner` ile
   aynı).
2. `var algoTrader = new AlgoTrader("scan"); algoTrader.RegisterLogger(logger);
   algoTrader.RegisterTimer(TimeManager.GetInstance()); algoTrader.Reset();
   algoTrader.SetData(data);`
3. `AppConfigApplier.ApplyMultipleTrader(algoTrader, options.MultipleTraderConfig, configsDir);`
   — **mevcut, test edilmiş yol, hiç değiştirilmiyor**.
4. `algoTrader.Initialize(); await algoTrader.RunMultipleTraderWithProgressAsync();`
5. `var mainTrader = algoTrader.MultipleTrader!.GetMainTrader();` → `GetStatisticsHeaderRow`/
   `DataRow`, `SonYon`/`TaramaOzeti`, `statistics.GetOptimizationSummary()` — `TimeframeScanner`
   ile birebir aynı toplama deseni, kaynak sadece `mainTrader`.
6. `algoTrader.Dispose();` → sıradaki TF.

## ⚠️ Uygulama Sırasında Değişen Tasarım Detayı

Tasarım notunda `MultiStrategyTimeframeScannerOptions`'ın doğrudan bir `MultipleTraderConfig`
alanı taşıyacağı yazılmıştı — uygulama sırasında bunun `SymbolScanner`/`TimeframeScanner`'ın
kurduğu "Trading katmanı AppConfig namespace'ini bilmez" ayrımını boz acağı görüldü
(`MultipleTraderConfig`, `AlgoTrade.Core.AppConfig` namespace'inde). Bunun yerine
`Options.ConfigureAlgoTrader : Action<AlgoTrader>?` delegate'i eklendi — çağıran taraf (Console)
bu delegate içinde `AppConfigApplier.ApplyMultipleTrader(...)` çağırıyor. Bu, katman ayrımını
koruyor ve `AlgoTrader.SetCallbacks`/`OnProgress` gibi zaten var olan callback-tabanlı
genişletme desenine de uyuyor.

## Dosyalar

| Dosya | Değişiklik |
|---|---|
| `src/AlgoTrade.Core/Trading/Traders/MultiStrategyTimeframeScanner.cs` | YENİ — `MultiStrategyTimeframeScanner`, `MultiStrategyTimeframeScannerOptions`. Sonuç tipi olarak `TimeframeScanner.cs`'teki `TimeframeScanResult` reuse edildi (birebir aynı şekil). |
| `src/AlgoTrade.Core/AppConfig/AppConfig.cs` | `MultiStrategyTimeframeScanConfig` (+ `Sort`/`Save` alt config'leri) — `MultipleTrader` alanı mevcut `MultipleTraderConfig` tipini birebir reuse ediyor |
| `inputs/configs/AppConfig/AppConfig.json` | `MultiStrategyTimeframeScan` bölümü — `MultipleTrader` alt bloğu mevcut `"MultipleTrader"` bölümünün (2 child, Consensus.Mode=Net) bir kopyası |
| `AlgoTrade.Console/Program.cs` | `[12] Tarama (Multi-Strategy Timeframe Scan)` menü seçeneği, `handleMultiStrategyTimeframeScan()`, `runMultiStrategyTimeframeScan()` (`ConfigureAlgoTrader` delegate'i burada `ApplyMultipleTrader` + child/list dosyalarını kapatan bir `SetMultipleTraderSaveConfig` override'ı içeriyor — TF'ler arası dosya çakışmasını önlemek için), `showModeConfigSummary("MultiStrategyTimeframeScan")` |

`AppConfigApplier.cs`'e yeni bir `Build*` metodu **eklenmedi** — `ConfigureAlgoTrader` delegate
tasarımı sayesinde Console doğrudan mevcut `ApplyMultipleTrader`'ı çağırıyor.

## ✅ DÜZELTİLDİ — Bağımsız Child Sinyalleri (2026-08-18)

Kullanıcı doğru bir şüpheyle sordu: her senaryoda hem bağımsız sinyaller hem (mümkünse) bileşke
sinyali birlikte raporlanmalı mıydı? Kontrol edildi, **tutarsızdı**, senaryo 4 için düzeltildi.

### Net Tablo — Hangi Senaryoda Bağımsız/Bileşke Var

| # | Sembol | Strateji | Zaman Dilimi | Bağımsız sinyal | Bileşke sinyal |
|---|--------|----------|---------------|:---:|:---:|
| 1 | Tek | Tek | Tek | — (tek strateji, kavram yok) | — |
| 2 | Tek | Tek | Çoklu | ✅ her TF bağımsız (tasarım gereği) | — (TF'ler hiç birleşmiyor) |
| 3 | Tek | Çoklu | Tek | ✅ (child list dosyaları + debug log) | ✅ (mainTrader consensus) |
| 4 | Tek | Çoklu | Çoklu | ✅ **DÜZELTİLDİ** (`TimeframeScanResult.ChildSignals`) | ✅ (her TF'nin kendi mainTrader'ı) |
| 5 | Çoklu | Tek | Tek | ✅ her sembol bağımsız | — (tek strateji) |
| 6 | Çoklu | Tek | Çoklu | ✅ (henüz yazılmadı; tek strateji → bileşke kavramı yok) | — |
| 7 | Çoklu | Çoklu | Tek | ✅ (henüz yazılmadı — aşağıdaki desen doğrudan uygulanacak) | ✅ |
| 8 | Çoklu | Çoklu | Çoklu | ✅ (henüz yazılmadı — aynı desen) | ✅ |

"Bileşke" kavramı sadece **3/4/7/8**'de var (sadece onlarda "Çoklu Strateji" = `MultipleTrader`).

### Ne Yapıldı (senaryo 4)

- **Kaynak**: `Senaryo 3`'te (`MultipleTrader`, Console `[3]`) zaten ikisi de vardı —
  `WriteMultipleTraderListsToFiles()` bar-bar hem mainTrader'ın (bileşke) hem her child'ın
  (bağımsız) Yön/Seviye/Sinyal'ini yazıyor, `LogDebug` oy dağılımını gösteriyor. `Senaryo 4`
  (`MultiStrategyTimeframeScanner`) ise sadece `GetMainTrader()`'ı okuyup child'ları hiç
  raporlamıyordu — dosya çakışmasını önlemek için `SaveMultipleTraderListsTxtEnabled`/
  `CsvEnabled` de kapatılmıştı.
- **Düzeltme**: `TimeframeScanResult`'a `ChildSignals: List<ChildSignalInfo>` alanı eklendi
  (`ChildId`/`SonYon`/`TaramaOzeti`). `MultiStrategyTimeframeScanner.RunSingleTimeframeAsync()`
  artık `algoTrader.MultipleTrader.Traders`'ı da geziyor, her child'ı `result.ChildSignals`'a
  ekliyor. CSV/TXT çıktısına `Child{Id}_SonYon;Child{Id}_TaramaOzeti` kolonları eklendi (child
  sayısı kadar, header ilk başarılı TF'den dinamik kuruluyor — `TimeframeScanner`, senaryo 2,
  hiç child üretmediği için bu alan orada her zaman boş kalıyor, format değişmedi). Console
  özetine de `Bileşke: ...` / `Child{Id} (bağımsız): ...` satırları eklendi.
- **Doğrulama**: BTCUSDT_BNC, TF=15/60, 2 child (`SimpleMostStrategy` v1/v2) ile gerçek veride
  test edildi — her TF'de hem `Bileske=...` hem `Child0 (bagimsiz)=...`/`Child1 (bagimsiz)=...`
  ayrı ayrı, doğru değerlerle görünüyor.

**Senaryo 7/8 için**: Henüz yazılmadılar ama aynı deseni (child'ları harvest edip
`ChildSignals`'a ekleme) baştan uygulayacak şekilde tasarım notlarına işlendi (bkz. ilgili
bölümler) — artık bu eksiği miras almayacaklar.

## Sonraki Adım

"Tek Sembol" sütunu (1/2/3/4) tamamlandı. **Sıra kesinleşti: 6 → 7 → 8** (kullanıcı git commit
sırasının karışmasını istemedi, orijinal sıra korunuyor — bir sonraki oturumda evden devam
edilecek).

---

# Senaryo 6 (Çoklu Sembol, Tek Strateji, Çoklu TF) — SIRADA (henüz uygulanmadı)

**Durum**: Tasarım taslağı, uygulama başlamadı. Bir sonraki oturumda buradan devam edilecek.

## Amaç

Tek bir strateji, hem sembol hem zaman dilimi ekseninde **tamamen bağımsız** taransın — N sembol
× M zaman dilimi = N×M ayrı backtest, hiçbir eksende konsensüs/bileşke yok (A'daki ve
todo.md'deki düzeltmeyle tutarlı: "bileşke" sadece strateji ekseninde, B/4'te).

## Tasarım Taslağı

- **Yeni sınıf**: `SymbolTimeframeScanner` (isim tartışmaya açık) — `SymbolScanner`/
  `TimeframeScanner` ile aynı aile, iç içe iki döngü. Per-item mantık (dosya oku →
  `IndicatorManager` → `StrategyRegistry.CreateStrategy` → `SingleTrader` →
  `ConfigureUserFlagsOnce()` + 7 bayrak → bar-bar `Run` → `Finalize` → sonuç topla → Dispose)
  `TimeframeScanner.RunSingleTimeframe()`'in birebir aynısı — üçüncü kopya, aynı iskelet.
- **Sembol listesi nasıl belirlenir**: `SymbolScanner`'daki gibi iki mod (`AutoDiscover` +
  referans bir TF klasöründeki `*.csv` dosyalarını listele, ya da açık `SymbolList`) — hangisi
  seçilirse seçilsin, sembol adları TÜM `Timeframes` klasörlerinde aranacak
  (`Path.Combine(BaseFolder, tf, symbol + ".csv")`).
- **Seçenekler**: `BaseFolder`, `AutoDiscover` + `ReferenceTimeframe` (sembol keşfi için hangi TF
  klasörü taranacak, örn. `"05"`) veya `SymbolList`, `Timeframes: List<string>`, Strategy,
  TradeParams, Signals (7 bayrak), ReadData filtresi, Sort — hepsi mevcut `SymbolScanOptions`/
  `TimeframeScannerOptions` alanlarının bileşimi.
- **Sonuç satırı**: `Symbol;Timeframe;<StatisticsDataRow kolonları>;SonYon;...;TaramaOzeti` — iki
  kimlik kolonu (Symbol + Timeframe), N×M satır. Sıralama global `SortField`'e göre (gruplama
  yok, v1 için basit tutulacak — kullanıcı Excel'de filtreleyebilir).
- **Config**: `AppConfig.cs`'e `SymbolTimeframeScanConfig` (+ `Sort`/`Save`), `AppConfig.json`'a
  `SymbolTimeframeScan` bölümü, Console'a muhtemelen `[13]` menü seçeneği.
- **Dikkat**: N×M büyüklüğüne göre çalışma süresi çok uzayabilir (örn. 10 sembol × 9 TF = 90
  backtest) — ilk testte küçük bir alt küme (2 sembol × 2-3 TF) ile doğrulanmalı, tıpkı
  önceki senaryolarda yapıldığı gibi.

## Yapılacaklar (bir sonraki oturum)

1. `SymbolTimeframeScanner.cs` yaz (yukarıdaki taslağa göre).
2. `AppConfig.cs` + `AppConfig.json`'a config ekle.
3. Küçük bir alt kümeyle (örn. 2 sembol × 2 TF) scratch test ile uçtan uca doğrula.
4. Console'a `[13]` menü seçeneği + handler/runner ekle, gerçek Console üzerinden dene.
5. `docs/tarama-motoru-plan.md` ve `docs/todo.md`'yi TAMAMLANDI olarak güncelle.
6. Commit, sonra senaryo 7'ye geç.

---

# Senaryo 7 (Çoklu Sembol, Çoklu Strateji-Bileşke, Tek TF) — SIRADA (henüz uygulanmadı)

**Durum**: Tasarım taslağı, uygulama başlamadı. Senaryo 6'dan sonra sırada.

## Amaç

`MultipleTrader` consensus'unu (birden fazla stratejinin bileşkesi), **tek bir zaman diliminde**,
birden fazla sembolde **bağımsız bağımsız** çalıştırmak — sembol ekseninde konsensüs yok, sadece
her sembolün kendi içinde strateji-ekseni consensus'u var (tıpkı senaryo 4'ün TF ekseni yerine
burada sembol ekseninde olması gibi).

## Tasarım Taslağı — Senaryo 4'ün Doğrudan Uyarlanmışı

Bu senaryo, senaryo 4'te (`MultiStrategyTimeframeScanner`) kurulan tekniğin **neredeyse birebir
kopyası** — sadece dış döngü değişkeni TF yerine sembol:

- **Yeni sınıf**: `MultiStrategySymbolScanner` (isim tartışmaya açık) — `MultiStrategyTimeframeScanner`
  ile aynı iskelet: her sembol için taze bir `AlgoTrader` kurup (`SetData`/`RegisterLogger`/
  `RegisterTimer`/`Reset`/`Initialize`), `Options.ConfigureAlgoTrader : Action<AlgoTrader>?`
  delegate'i ile çağıran tarafın (Console) `AppConfigApplier.ApplyMultipleTrader(...)` çağırmasına
  izin verip, `RunMultipleTraderWithProgressAsync()`'i çalıştırıyor, `GetMainTrader()`'dan sonucu
  topluyor, `Dispose()` edip sıradaki sembole geçiyor. `AlgoTrader.createChildTraders()`'ı elle
  tekrar yazmama gerekçesi (karmaşıklık + zaten test edilmiş olması) burada da aynen geçerli.
- **Sembol listesi nasıl belirlenir**: `SymbolScanner`'daki gibi `DataFolder` (tek TF klasörü,
  örn. `CRP\05`) + `AutoDiscover`/`SymbolList` — `SymbolScanner.ResolveSymbols()` mantığı
  doğrudan reuse edilebilir.
- **Sonuç tipi**: `SymbolScanner.cs`'teki `ScanResult` (Symbol-anahtarlı) reuse edilir — senaryo
  4'ün `TimeframeScanResult`'ı (Timeframe-anahtarlı) reuse etmesiyle aynı mantık.
- **Config**: `AppConfig.cs`'e `MultiStrategySymbolScanConfig` (`DataFolder`, `AutoDiscover`,
  `SymbolList`, `MultipleTrader: MultipleTraderConfig` — mevcut tip birebir reuse, `Sort`/`Save`).
  `AppConfig.json`'a yeni bölüm (mevcut `"MultipleTrader"` bölümünün bir kopyası + `DataFolder`/
  `AutoDiscover`/`SymbolList`). Console'a `[14]` menü seçeneği (13 senaryo 6'da kullanılacak).
- **Dikkat**: Senaryo 4'te olduğu gibi, her sembol çalıştırmasından sonra `MultipleTrader`'ın
  kendi dosya yazımlarını (`SetMultipleTraderSaveConfig` ile `SaveMultipleTraderListsTxtEnabled`
  vb. `false`) kapatmak gerekiyor — aksi halde her sembol aynı dosya adına yazıp bir öncekini
  ezer.
- **Child sinyallerini de raporla (senaryo 4'te DÜZELTİLEN desen, bkz. yukarıdaki "✅ DÜZELTİLDİ"
  bölümü)**: `RunSingleTimeframeAsync()`'in bu senaryodaki karşılığı `algoTrader.MultipleTrader.
  Traders`'ı gezip her child'ı `TimeframeScanResult.ChildSignals`'a eklemeli (aynı `ChildId`/
  `SonYon`/`TaramaOzeti` alanları, `TimeframeScanResult` zaten reuse ediliyor) — senaryo 4'te
  yapılan `Child{Id}_SonYon;Child{Id}_TaramaOzeti` kolon deseni birebir buraya da uygulanmalı.

## Yapılacaklar (senaryo 6 bitince)

1. `MultiStrategySymbolScanner.cs` yaz — `MultiStrategyTimeframeScanner.cs`'i taban al, TF yerine
   sembol döngüsü.
2. `AppConfig.cs` + `AppConfig.json`'a config ekle (mevcut `"MultipleTrader"` bölümünü kopyala).
3. Küçük bir alt kümeyle (örn. 2-3 sembol) scratch test ile uçtan uca doğrula.
4. Console'a `[14]` menü seçeneği + handler/runner ekle.
5. Belgeleri güncelle, commit, senaryo 8'e geç.

---

# Senaryo 8 (Çoklu Sembol, Çoklu Strateji-Bileşke, Çoklu TF) — SIRADA (henüz uygulanmadı)

**Durum**: Tasarım taslağı, uygulama başlamadı. Senaryo 6 ve 7'den sonra sırada — ikisinin de
bitmiş olması gerekiyor çünkü bu senaryo ikisinin tekniklerinin bileşimi.

## Amaç

Matrisin en genel hâli: N sembol × M zaman dilimi, her hücrede `MultipleTrader` consensus'u —
hepsi birbirinden **tamamen bağımsız** (N×M ayrı backtest, hiçbir eksende TF/sembol konsensüsü
yok, sadece her hücrenin kendi içinde strateji-ekseni consensus'u var).

## Tasarım Taslağı — 6 ve 7'nin Bileşimi

Yeni bir teknik gerekmiyor — sadece iki mevcut desenin birleşimi:

- **Senaryo 6'dan**: dış/iç içe döngü iskeleti (sembol × TF, `Path.Combine(BaseFolder, tf,
  symbol + ".csv")` yol çözümlemesi, sembol keşfi — `AutoDiscover`/`SymbolList`).
- **Senaryo 7'den** (= senaryo 4'ün tekniği): her hücre için taze bir `AlgoTrader` kurup
  `ConfigureAlgoTrader` delegate'i ile `ApplyMultipleTrader(...)` çağırma, `GetMainTrader()`'dan
  sonuç toplama.

**Yeni sınıf**: `MultiStrategySymbolTimeframeScanner` (isim tartışmaya açık) —
`SymbolTimeframeScanner`'ın (senaryo 6) nested-loop iskeletini alıp, her hücrede
`RunSingleSymbolTimeframe()`'i (ham `SingleTrader` kuran) `MultiStrategySymbolScanner`'ın (senaryo
7) "taze `AlgoTrader` + `ConfigureAlgoTrader` delegate + `RunMultipleTraderWithProgressAsync`"
mantığıyla değiştirmek yeterli. Sonuç satırı: `Symbol;Timeframe;<stats>;...` (senaryo 6 ile aynı
iki-kimlik-kolonlu format, kaynak `mainTrader` olması dışında).

**Config**: `AppConfig.cs`'e `MultiStrategySymbolTimeframeScanConfig` (`BaseFolder`,
`AutoDiscover`/`SymbolList`, `Timeframes`, `MultipleTrader: MultipleTraderConfig`, `Sort`/`Save`).
Console'a `[15]` menü seçeneği.

**Dikkat — N×M büyüklüğü artık iki kat daha kritik**: Her hücre bir `SingleTrader` değil, N
child'lı bir `MultipleTrader` çalıştırıyor — 10 sembol × 9 TF × 2 child strateji gibi bir
senaryoda toplam 180 alt-backtest'e denk gelir. İlk testte küçük bir alt küme (2 sembol × 2 TF)
ile doğrulanmalı, gerçek kullanımda kullanıcıya çalışma süresi tahmini gösterilmesi düşünülebilir
(fast-follow, v1'in parçası değil).

**Child sinyallerini de raporla (senaryo 4'te DÜZELTİLEN desen, bkz. yukarıdaki "✅ DÜZELTİLDİ"
bölümü)**: Sadece `GetMainTrader()` (bileşke) değil, her hücrede `algoTrader.MultipleTrader.
Traders` (child'ların bağımsız sinyalleri) de harvest edilip `ChildSignals`'a eklenmeli — bu
senaryo en genel/kapsamlı olan olduğu için bu desenin eksik uygulanması en çok burada fark
edilir.

## Yapılacaklar (senaryo 6 ve 7 bitince)

1. `MultiStrategySymbolTimeframeScanner.cs` yaz — 6'nın nested-loop iskeleti + 7'nin
   throwaway-AlgoTrader tekniği.
2. `AppConfig.cs` + `AppConfig.json`'a config ekle.
3. En küçük alt kümeyle (2 sembol × 2 TF) scratch test ile uçtan uca doğrula.
4. Console'a `[15]` menü seçeneği + handler/runner ekle.
5. Belgeleri güncelle, commit — matris tamamlanmış olur (8/8).

---

# Sorgu Tarama Matrisi (Strateji Değil, Sorgu Ekseni) — AYRI BİR İŞ, HENÜZ BAŞLANMADI

**Kaynak**: Kullanıcı ile 2026-08-18'de netleşti — 6/7/8 bitince "Sembol × Strateji × Zaman Dilimi"
matrisi (8/8) tamamlanmış olacak, ama bu **sadece strateji ekseninde**. Projede `IStrategy`/
`BaseStrategy`/`StrategyRegistry` ile **birebir aynı desende** ayrı bir **Sorgu** alt sistemi var
(`IQuery`/`BaseQuery`/`QueryRegistry`, `Trading/Query/` ve `Trading/Queries/`) — ve
`SingleTrader.RunMode` zaten `TradeOnly | TradeAndQuery | QueryOnly` diye üç modu destekliyor.
Ama bugüne kadar yazılan **hiçbir tarama sınıfı Query modunu desteklemiyor** — `SymbolScanner`,
`TimeframeScanner`, `MultiStrategyTimeframeScanner` ve planlanan 6/7/8'in hepsi
`trader.RunMode = TraderRunMode.TradeOnly` hardcoded. Yani "tarama matrisi" şu ana kadar sadece
Strateji eksenini kapsıyor, Sorgu ekseni hiç ele alınmadı.

## Migration-Guide.md ile İlişkisi

- **Madde 6** (zengin sorgu tipleri) — alt yapı (`IQuery`/`BaseQuery`/`QueryRegistry`/
  `QueryConfigLoader`) TAMAM ve çalışıyor, ama somut sorgu örneği sadece 1 tane: `SimpleQuery1`
  (MA8/MA200 kesişimi + trader-state). Roadmap'te tarif edilen "fiyat-indikatör kesişimleri",
  "indikatör-indikatör kesişimleri", "kullanıcı stratejisinden A/S/F bayrakları sorgusu" gibi
  zengin sorgu tipleri henüz yazılmadı — **bu, sorgu taramasının ön koşulu**: tek bir sorgu
  türüyle tarama yapmanın pratik değeri sınırlı.
- **Madde 9** (Sorgu + Toplu Sembol Uygulama) — roadmap'te birebir şu şekilde tarif edilmiş:
  *"Madde 6'daki sorgu yeteneği AlgoTrader üzerinden tüm sembollere uygulanabilir. Örnek: 'Hangi
  sembollerde fiyat 20 MA'yi yukarı kırdı?' gibi sorgular tüm sembol havuzunda çalıştırılır ve
  eşleşen semboller listelenir."* — bu **tam olarak** "Sembol × Sorgu" tarama matrisinin tanımı.

## Tam Matris (8/8 — netleşti, 2026-08-18)

Strateji tarafındaki matrisle aynı mantık, "Strateji"nin yerini "Sorgu" alıyor:

| # | Sembol | Sorgu | Zaman Dilimi | Durum |
|---|--------|-------|---------------|-------|
| 1 | Tek | Tek | Tek | ✅ Mevcut — `SingleTrader.RunMode = QueryOnly` |
| 2 | Tek | Tek | Çoklu | ❌ Yok — `TimeframeScanner`'ın QueryOnly-varyantı gerekiyor |
| 3 | Tek | Çoklu | Tek | ❌ Yok — bkz. aşağıdaki karar |
| 4 | Tek | Çoklu | Çoklu | ❌ Yok — 2 ve 3'ün bileşimi |
| 5 | Çoklu | Tek | Tek | ❌ Yok — `SymbolScanner`'ın QueryOnly-varyantı, **madde 9'un birebir istediği şey** |
| 6 | Çoklu | Tek | Çoklu | ❌ Yok — 2 ve 5'in bileşimi |
| 7 | Çoklu | Çoklu | Tek | ❌ Yok — 3'ün çoklu sembol hali |
| 8 | Çoklu | Çoklu | Çoklu | ❌ Yok — hepsinin bileşimi |

## Karar: "Çoklu Sorgu" Ne Anlama Geliyor (kullanıcı ile netleşti, 2026-08-18)

Strateji tarafında "bileşke" gerekliydi çünkü tek bir pozisyon kararı üretmen lazım (Al/Sat/Flat,
tek yön) — `MultipleTrader` bu yüzden var. Sorgu ise salt okunur bir kontrol, pozisyon üretmiyor.
**Karar: hiçbir zaman birleştirilmiyor.** N sorgu çalıştırılır, N sonuç **ayrı ayrı** raporlanır
(ayrı kolonlar), kullanıcı kendisi yorumlar — AND/OR gibi bir birleştirme mantığı YOK.

**Mimari sonucu — önemli basitleştirme**: Bu karar sayesinde Sorgu tarafında Strateji
tarafındaki gibi yeni bir "MultipleQuery" consensus sınıfına (MultipleTrader'ın Sorgu karşılığı)
**hiç gerek yok**. "Çoklu Sorgu", tek bir sorgu yerine bir **sorgu listesi** çalıştırıp
sonuçları ek kolonlar olarak eklemekten ibaret — yani "Sembol" ve "Zaman Dilimi" eksenleri
Strateji tarafındakiyle birebir aynı mimariyi (mevcut `SymbolScanner`/`TimeframeScanner`
ailesi) kullanabilir, "Sorgu" ekseni sadece "tek mi çoklu mu" değil, "kaç tane sorgu kolonu
ekleniyor" detayına indirgeniyor. Yani pratikte 8 senaryo değil, **2 gerçek eksen** (Sembol,
Zaman Dilimi) + "kaç sorgu çalıştırılıyor" parametresi.

## Neden Şimdi Değil

1. **Madde 6 önce gerekiyor**: Zengin sorgu tipi olmadan (sadece `SimpleQuery1` varken) tarama
   motoru yazmanın getirisi düşük.
2. Strateji taraması (bu belgenin geri kalanı) zaten büyük bir iş, önce onu bitirmek (6→7→8)
   önceliklendirildi.
3. Mimari olarak muhtemelen **yeni sınıflar değil**, mevcut tarama sınıflarına (`SymbolScanner`
   vb.) bir "Query modu" eklenmesi yeterli olabilir (`RunMode` parametrik hale getirilip
   `QueryOnly` seçilebilir hale gelmesi, ve tek `QueryName` yerine bir `QueryNames: List<string>`
   alınması) — ama bu tasarım kararı henüz verilmedi, ayrı bir plan-mode oturumu gerektirecek.

## Yapılacaklar (6/7/8 bitince, ayrı bir oturumda)

1. Önce Madde 6'yı ele al — en az 2-3 yeni somut sorgu türü yaz (örn. "fiyat-indikatör kesişimi",
   "indikatör-indikatör kesişimi").
2. Sorgu taraması için mimari kararı ver: mevcut tarama sınıflarını Query-mode destekleyecek
   şekilde genişletmek mi (`RunMode` + `QueryNames: List<string>` parametrik hale getirmek), yoksa
   paralel yeni sınıflar mı (`SymbolQueryScanner` vb.) — plan-mode ile kullanıcıyla netleştir.
   "Çoklu sorgu" artık net (birleştirme yok, ayrı ayrı raporlama) — bu karar zaten verildi.
