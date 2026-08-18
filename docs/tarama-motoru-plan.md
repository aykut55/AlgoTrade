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
- Senaryo 4 (Tek sembol, çoklu strateji-bileşke, çoklu TF) — `MultipleTrader`'ı
  `TimeframeScanner`-tarzı bağımsız bir döngüye sokmak gerekir
- Senaryo 6 (Çoklu sembol, tek strateji, çoklu TF) — `SymbolScanner` içinde her sembol için
  `TimeframeScanner`'ı da çalıştırmak (iç içe iki bağımsız tarama)
- Senaryo 8 — 4 ve 6'nın bileşimi
