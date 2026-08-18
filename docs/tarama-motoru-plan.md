# Sembol Tarama Motoru (Yapı Taşı C) — Mimari ve Durum

> Bu belge, `docs/todo.md`'deki "Tarama Matrisi Analizi" bölümünde tanımlanan 3 yapı taşından
> (A/B/C) üçüncüsünün (C — Sembol Tarama) tasarımını ve uygulanmış durumunu kaydeder. Kaynak:
> plan-mode oturumu (2026-08-18), kullanıcı ile soru-cevap şeklinde netleştirildi.

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

## Kapsam Dışı (fast-follow, `docs/todo.md`'ye eklendi)

- MultipleTrader-bazlı tarama (senaryo 7 — sembol başına consensus)
- Yapı taşı A (çoklu zaman dilimi) ile birleşim (senaryo 6/8) — veri zaten diskte var
  (`<tf>` klasörleri), sadece N dosyayı okuyup `MultipleTrader`'a benzer bir "zaman-dilimi
  bileşkesi" ile birleştirmek yeterli, resampling gerekmiyor
- Buffered flush / partial-resume (Optimizer'daki `FileFlushIntervalMs`/`PartialOpt` benzeri)
- Zengin JSON preview ekranı (SingleTrader/MultipleTrader'daki gibi) — v1'de sadece kutu-stili
  özet var, [T] Pause/Resume Timer satırı da v1'de yok (davranışı doğrulanamadığı için eklenmedi)
- Time filtering / TradeStartBarIndex desteği (`SymbolScanOptions`'a eklenmedi, v1'de tüm
  semboller `TimeFilteringEnabled=false` ile çalışıyor)
