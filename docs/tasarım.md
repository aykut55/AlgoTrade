# AlgoTrade Tasarımı ve Kullanım Kılavuzu

> Bu belge, projeyi uzun süre sonra yeniden açan biri için hazırlanmıştır. Önce uygulamanın kullanıcı olarak nasıl çalıştırılacağını ve konsol menülerinin ne yaptığını, ardından mimariyi, önemli sınıfları ve metotları anlatır.
>
> Kaynak esas alınan sürüm: `AlgoTrade.Console/Program.cs` ve `src/AlgoTrade.Core` (8 Kasım 2026).

## 1. Proje ne yapıyor?

AlgoTrade, geçmiş OHLCV piyasa verisi üzerinde strateji çalıştırmak, geriye dönük test yapmak, birden fazla stratejinin ortak kararını incelemek, parametre optimizasyonu ve sembol/zaman dilimi taraması yapmak için geliştirilmiş bir .NET 8 uygulamasıdır.

Temel akış şöyledir:

```text
AppConfig.json ve diğer ayarlar
             │
             ▼
     Piyasa verisini oku
             │
             ▼
 Strateji / sorgu / tarayıcı seç
             │
             ▼
 Barlar üzerinde hesaplama ve işlem simülasyonu
             │
             ├──► CSV/TXT raporları
             ├──► log dosyaları
             └──► isteğe bağlı Python grafik ekranı
```

Projenin ana kullanıcı arayüzü `AlgoTrade.Console` uygulamasıdır. `AlgoTrade.WinForms` henüz temel bir iskelet durumundadır; günlük kullanım için konsol uygulaması esas alınmalıdır.

## 2. Hızlı başlangıç

### Gereksinimler

- .NET 8 SDK
- Grafik özellikleri kullanılacaksa Python ortamı ve proje kökündeki `.venv`
- Geçerli bir piyasa veri dosyası
- `inputs/configs/AppConfig/AppConfig.json` içinde doğru veri yolu ve çalışma ayarları

Python ortamını ilk kez hazırlamak gerekiyorsa proje kökündeki `setupPythonEnvs.bat` kullanılabilir.

### Uygulamayı çalıştırma

Proje kökünde:

```powershell
dotnet run --project .\AlgoTrade.Console\AlgoTrade.Console.csproj
```

Derleme kontrolü için:

```powershell
dotnet build .\AlgoTrade.sln
```

### İlk kullanım için önerilen yol

1. `inputs/configs/AppConfig/AppConfig.json` dosyasını açın.
2. `AppSettings` ve `ReadData` bölümlerinde veri dosyasını ve filtreyi kontrol edin.
3. Programı çalıştırın.
4. Tek stratejilik standart test için ana menüde **5** yazın.
5. Önizleme ekranında ayarları kontrol edip **Enter**'a basın.
6. Çalışma bitince sonuçları `outputs/logs` altında inceleyin.

`5`, veri okumayı ve SingleTrader çalıştırmayı arka arkaya yaptığı için en güvenli başlangıç seçeneğidir.

## 3. Konsolun ortak tuş davranışları

Menülerde genel olarak şu tuşlar kullanılır:

| Tuş | Davranış |
|---|---|
| `Enter` | Ekrandaki varsayılan işlemi başlatır. |
| `E` | `AppConfig.json` dosyasını varsayılan editörde açar; kaydedip konsola dönünce Enter ile yeniden yükler. |
| `R` | Dosyayı editörde açmadan `AppConfig.json` ayarlarını yeniden yükler. |
| `B` | Bir önceki/ana menüye döner. Boş giriş alanında Backspace de `B` gibi davranır. |
| `Esc` | Bulunulan akıştan çıkar; ana menüde programı kapatır. |
| `T` | Geri sayım başlamış fakat henüz yazı yazılmamışsa sayacı duraklatır/devam ettirir. |

`AppSettings.MenuTimeoutSeconds` sıfırdan büyükse menüler geri sayımlıdır. Bir karakter yazmaya başlayınca normal giriş moduna geçilir. Süre dolunca ilgili ekranın varsayılan seçimi uygulanır.

> **Dikkat:** Ana menünün kodda tanımlı mevcut varsayılanı `9b`'dir. Ana menüde doğrudan Enter'a basılır veya sayaç dolarsa DearImGuiBundleDataPlotter test akışı açılır. Gerçek bir backtest başlatmak için `5`, `6`, `7`, `23` veya `25` gibi seçeneği açıkça yazın.

## 4. Ana menü seçenekleri

### 4.1 Veri ve temel trader işlemleri

| Seçim | Menü | Ne olur? |
|---:|---|---|
| `1` | Read Data | `AppConfig.ReadData` ayarlarına göre veri dosyasının meta bilgisini ve barlarını belleğe yükler. Tek başına strateji çalıştırmaz. |
| `2` | SingleTrader | Bellekte daha önce yüklenmiş veri üzerinde tek strateji çalıştırır. Veri yoksa çalıştırma metodu sessizce döner. |
| `3` | MultipleTrader | Bellekteki veri üzerinde bir ana trader ve birden fazla child trader çalıştırır; sinyalleri consensus kuralıyla birleştirir. |
| `4` | SingleTraderOptimizer | Bellekteki veri üzerinde parametre kombinasyonlarını dener ve sonuçları sıralar. |
| `5` | Read Data + SingleTrader | Önce veriyi okur, başarılıysa SingleTrader çalıştırır. Standart tek-strateji backtest'i için önerilen seçimdir. |
| `6` | Read Data + MultipleTrader | Önce veriyi okur, sonra çoklu strateji/consensus testini çalıştırır. |
| `7` | Read Data + SingleTraderOptimizer | Önce veriyi okur, sonra optimizasyonu çalıştırır. |

#### `1 - Read Data` seçilince

Bir JSON önizlemesi gösterilir:

- `StockDataFile`: okunacak dosya,
- `FilterMode`: verinin hangi bölümünün okunacağı,
- `N1`, `N2`: adet veya indeks filtre parametreleri,
- `Dt1`, `Dt2`: tarih filtresi sınırları.

Enter'a basılınca `StockDataReader` önce `#` ile başlayan meta verileri, sonra fiyat barlarını okur. Konsolda sembol, periyot, tarih aralığı, bar sayısı ve okuma süresi görünür. Okunan veri bellekte tutulur ve `2`, `3`, `4`, `22`, `24` gibi “yalnız çalıştır” seçeneklerinde yeniden kullanılır.

Desteklenen `FilterMode` değerleri:

- `All`: tüm kayıtlar,
- `LastN`: son `N1` kayıt,
- `FirstN`: ilk `N1` kayıt,
- `IndexRange`: `N1`–`N2` indeks aralığı,
- `AfterDateTime`: `Dt1` sonrasındaki kayıtlar,
- `BeforeDateTime`: `Dt1` öncesindeki kayıtlar,
- `DateTimeRange`: `Dt1`–`Dt2` tarih aralığı.

#### `2/5 - SingleTrader` seçilince

Ön ekranda seçili strateji, query, equity-curve filtresi ve işlem parametreleri gösterilir. Ardından çalışma modu seçilebilir:

| Tuş | RunMode | Anlamı |
|---:|---|---|
| `1` | `TradeOnly` | Stratejinin al/sat sinyallerini ve sanal işlemlerini çalıştırır. |
| `2` | `TradeAndQuery` | Strateji işlemleriyle birlikte query değerlendirmesini de çalıştırır. |
| `3` | `QueryOnly` | İşlem simülasyonu yerine yalnız query üretir; grafik açılmaz. |
| `Enter` | AppConfig değeri | `SingleTrader.RunMode` içinde yazılı modu kullanır. |

Çalıştırma sırasında:

1. `AlgoTrader` oluşturulur ve veri atanır.
2. `AppConfigApplier.ApplySingleTrader()` strateji, query, filtre, işlem ve export ayarlarını uygular.
3. `Initialize()` ile trader hazırlanır.
4. `RunSingleTraderWithProgressAsync()` bütün barları işler.
5. Sonuç dosyaları asenkron yazılır.
6. `PlotEnabled=true` ve mod `QueryOnly` değilse grafik üretimi başlar.

Grafik tarafında `AppSettings.PlotBackend` değeri `0` ise DearPyGui, `1` ise DearImGui Bundle backend'i kullanılır. NPZ bundle ve view JSON dosyaları da üretilir.

#### `3/6 - MultipleTrader` seçilince

`MultipleTrader`, aynı veri üzerinde birden fazla child stratejiyi çalıştırır. Child sinyalleri `Consensus.Mode` ayarına göre birleştirilir; çıkan ortak sinyal main trader tarafından işleme dönüştürülür.

Başlıca consensus türleri:

- `Net`: alış ve satış oylarının net sonucuna bakar,
- `Majority`: çoğunluk kararını kullanır,
- `All`: tüm child'ların aynı yönde olmasını ister,
- `Any`: en az bir uygun sinyali yeterli kabul eder.

Önizlemede çalışma modu, consensus biçimi, child stratejiler ve main trader işlem parametreleri gösterilir. Sonuçta main trader daima, `WriteChildTradersDataToFiles` açıksa child trader verileri de dosyaya yazılır.

#### `4/7 - SingleTraderOptimizer` seçilince

Seçili stratejinin `OptimizationConfig.txt` içindeki parametre uzayı taranır. Her kombinasyon için bir SingleTrader testi yapılır, istatistikler toplanır ve `SortField` ölçütüne göre sonuç raporu üretilir. Çıktıların ana konumu `outputs/opt` klasörüdür.

Kombinasyon sayısı hızla büyüyebilir. Önizlemede görülen parametre aralıklarını çalıştırmadan önce kontrol etmek önemlidir.

### 4.2 Script

| Seçim | Menü | Ne olur? |
|---:|---|---|
| `8` | Run Script | `inputs/scripts` altındaki `.csx` dosyalarını listeler, seçilen C# script'ini Roslyn ile çalıştırır. |

Script seçiminde numara girilir; seçim yapılmazsa ilk script varsayılan olabilir. Script'e `ScriptGlobals` üzerinden `algoTrader`, `stockData`, trader/indicator yardımcıları, log ve sonuç callback'leri sunulur.

> Script motoru bir güvenlik sandbox'ı değildir. Script, uygulamanın ve makinenin erişebildiği kaynaklara erişebilir. Yalnız güvendiğiniz `.csx` dosyalarını çalıştırın.

Örnekler `inputs/scripts` klasöründedir. Özellikle numaralı `01_...`–`19_...` dosyaları menü işlemlerinin script karşılıklarını gösterir.

### 4.3 Grafik backend testleri

| Seçim | Menü | Ne olur? |
|---:|---|---|
| `9` | DearPyGuiDataPlotter Test | DearPyGui process'ini başlatır, varsa örnek `latest_bundle.npz` dosyasını yükler, panel temizleme komutunu test eder ve process'i kapatır. |
| `9b` | DearImGuiBundleDataPlotter Test | Aynı start/load/clear/stop testini DearImGui Bundle backend'iyle yapar. |

Bunlar gerçek strateji testi değil, plotter iletişimini doğrulayan demo/test seçenekleridir. Kodda ileride kaldırılacakları belirtilmiştir.

### 4.4 Strateji taramaları

Tarama menüleri kendi AppConfig bölümlerini kullanır; önizleme ekranından sonra Enter ile başlar. Sonuçlar genel olarak `outputs/scan` altında CSV ve TXT olarak yazılır.

| Seçim | Tarama | Ne karşılaştırılır? |
|---:|---|---|
| `10` | Symbol Scan | Tek strateji × çok sayıda sembol. |
| `11` | Timeframe Scan | Tek strateji × çok sayıda zaman dilimi. |
| `12` | Multi-Strategy Timeframe Scan | Birden fazla strateji × çok sayıda zaman dilimi. |
| `13` | Symbol-Timeframe Scan | Tek strateji × sembol × zaman dilimi matrisi. |
| `14` | Multi-Strategy Symbol Scan | Birden fazla strateji × sembol. |
| `15` | Multi-Strategy Symbol-Timeframe Scan | Birden fazla strateji × sembol × zaman dilimi; en geniş strateji taramasıdır. |

Tarayıcılar her hücre için uygun veri dosyasını bulur/okur, trader'ı kurar, testi çalıştırır, başarılı ve hatalı sonuçları ayrı ayrı raporlar. `AutoDiscover` açıksa semboller belirtilen veri klasöründen keşfedilebilir; değilse config içindeki liste kullanılır.

### 4.5 Query taramaları

Query, doğrudan pozisyon açmaktan çok veriyi belirli bir koşula göre sınıflandırmak veya mevcut durumu sorgulamak için kullanılır.

| Seçim | Tarama | Ne karşılaştırılır? |
|---:|---|---|
| `16` | Query Symbol Scan | Tek query × sembol. |
| `17` | Query Timeframe Scan | Tek query × zaman dilimi. |
| `18` | Multi-Query Timeframe Scan | Birden fazla query × zaman dilimi. |
| `19` | Query Symbol-Timeframe Scan | Tek query × sembol × zaman dilimi. |
| `20` | Multi-Query Symbol Scan | Birden fazla query × sembol. |
| `21` | Multi-Query Symbol-Timeframe Scan | Birden fazla query × sembol × zaman dilimi. |

Tek-query sonuçlarında her satırda sorgu özeti; multi-query sonuçlarında her hücre altında query kimliği, adı ve özeti gösterilir. CSV/TXT dosya adları ilgili AppConfig bölümünün `Save` alanından gelir.

### 4.6 Confirming trader'lar

| Seçim | Menü | Ne olur? |
|---:|---|---|
| `22` | ConfirmingSingleTrader | Bellekte yüklü veriyle tek sinyal stratejisi + sanal pozisyon konfirmasyonu çalıştırır. |
| `23` | Read Data + ConfirmingSingleTrader | Önce veriyi yükler, sonra confirming single akışını çalıştırır. Önerilen kullanım budur. |
| `24` | ConfirmingMultipleTrader | Bellekte yüklü veriyle child consensus + sanal pozisyon konfirmasyonu çalıştırır. |
| `25` | Read Data + ConfirmingMultipleTrader | Önce veriyi yükler, sonra confirming multiple akışını çalıştırır. Önerilen kullanım budur. |

Confirming yaklaşımında sinyal doğrudan gerçek simüle pozisyona uygulanmaz. Önce bir sanal pozisyon açılır; kâr veya zarar eşiğine ulaşınca `Confirmation.Trigger` ve `ConflictMode` kurallarına göre main trader'a onay/red sinyali üretilir.

Önizlemede şunlar görülür:

- sinyal stratejisi veya child stratejiler,
- consensus modu (multiple varyantında),
- `ProfitThreshold` ve `LossThreshold`,
- eşiklerin yüzde mi mutlak değer mi olduğu,
- trigger ve conflict modu,
- main trader işlem parametreleri.

### 4.7 Çıkış

`0`, uygulamayı düzgün biçimde kapatır. Logger, trader, veri okuyucu ve Python plotter kaynakları dispose/shutdown edilir. Ana menüde `Esc` de çıkış sinyalidir.

## 5. Önizleme ve çalışma sonu ekranları

Trader ve tarama seçenekleri hemen başlamaz. Önce etkin config'in özeti gösterilir. Bu ekran, yanlış sembol/strateji/parametreyle uzun bir çalışma başlatmayı engelleyen son kontroldür.

Genel desen:

```text
Ana menü seçimi
      │
      ▼
Config özeti / Preview
      ├── E → dosyayı düzenle ve yeniden yükle
      ├── R → yeniden yükle
      ├── B veya Esc → geri dön
      └── Enter → çalıştır
                    │
                    ▼
              Sonuçları yaz
                    │
                    ├── Enter → ana menü
                    ├── R → aynı işi yeniden çalıştır
                    └── Esc → programdan çık
```

SingleTrader'ın ilk özet ekranında `1/2/3` ile RunMode doğrudan seçilebilir. Sonraki ayrıntılı preview ekranında Enter gerçek çalışmayı başlatır.

## 6. AutoRun modu

`AppConfig.AppSettings.AutoRunMode`, açılışta menüyü beklemeden belirli bir işi başlatabilir. Geçerli değerler:

- `None`
- `SingleTrader`
- `MultipleTrader`
- `SingleTraderOptimizer`
- `ConfirmingSingleTrader`
- `ConfirmingMultipleTrader`

AutoRun her zaman önce `ReadData` ayarlarıyla veriyi okur. İş bitince:

- `R`: aynı modu yeniden çalıştırır,
- `Enter`: ana menüye döner,
- `Esc`: uygulamadan çıkar.

Normal etkileşimli kullanım isteniyorsa `AutoRunMode` değerini `None` yapın.

## 7. Ayar dosyaları ve çıktı klasörleri

### Başlıca girdiler

| Yol | Amaç |
|---|---|
| `inputs/configs/AppConfig/AppConfig.json` | Uygulamanın ana ayarı; veri, trader, tarama, export, menü ve grafik seçenekleri. |
| `inputs/configs/StrategyConfig.txt` | Strateji adı, sürümü ve parametre setleri. |
| `inputs/configs/QueryConfig.txt` | Query adı, sürümü ve parametre setleri. |
| `inputs/configs/OptimizationConfig.txt` | Optimizasyonda gezilecek parametre aralıkları. |
| `inputs/configs/EquityCurveFilterConfig.txt` | Getiri eğrisi filtresi ve eşikleri. |
| `inputs/configs/StatisticsExporterConfig.json` | İstatistik export biçimi. |
| `inputs/configs/GridReportConfig.json` | Grid/optimizasyon rapor ayarları. |
| `inputs/scripts/*.csx` | Menü dışında veya menüye paralel özel çalışma script'leri. |

`AppConfig.json` içinde birçok bağımsız bölüm vardır: `ReadData`, `SingleTrader`, `MultipleTrader`, `SingleTraderOptimizer`, confirming trader bölümleri ve her tarama tipi kendi bölümünden okunur.

### Çıktılar

| Yol | İçerik |
|---|---|
| `outputs/logs` | Uygulama logu, trader listeleri, istatistikler ve bundle kopyaları. Uygulama açılışında bu klasör temizlenir. |
| `outputs/opt` | Optimizasyon sonuçları. |
| `outputs/scan` | Sembol/zaman dilimi/strateji/query tarama CSV ve TXT raporları. |
| `src/DearPyGuiDataPlotter/inputs` | DearPyGui'nin açacağı son NPZ/view bundle'ı. |
| `src/DearImGuiBundleDataPlotter/inputs` | DearImGui backend'inin açacağı son NPZ/view bundle'ı. |
| `inputs/python/...` | Plotter ve offline replay için ek runtime bundle kopyaları. |

> `outputs/logs` program başlangıcında temizlendiği için saklanması gereken sonuçları yeni bir çalıştırmadan önce başka yere kopyalayın.

## 8. Mimari

### Çözüm yapısı

| Proje/klasör | Sorumluluk |
|---|---|
| `AlgoTrade.Console` | Ana uygulama, menüler, önizlemeler ve çalışma orkestrasyonu. |
| `src/AlgoTrade.Core` | Veri okuma, trader'lar, stratejiler, query'ler, indikatörler, config, raporlama ve Python köprüleri. |
| `AlgoTrade.WinForms` | Henüz tamamlanmamış masaüstü arayüz iskeleti. |
| `src/PythonPlotter` | Eski/in-process Python grafik mekanizmasının Python tarafı. |
| `src/DearPyGuiDataPlotter` | Ayrı process çalışan DearPyGui grafik uygulaması. |
| `src/DearImGuiBundleDataPlotter` | DearImGui/implot tabanlı alternatif bundle görüntüleyici. |

### Katmanların ilişkisi

```text
Program.cs (menü ve orkestrasyon)
        │
        ├── AppConfigLoader / AppConfigApplier
        ├── StockDataReader
        └── AlgoTrader (facade)
               │
               ├── SingleTrader
               ├── MultipleTrader
               ├── Confirming*Trader
               ├── SingleTraderOptimizer
               ├── Scanner sınıfları
               ├── Strategy / Query registry
               └── IndicatorManager
                        │
                        └── MA, trend, momentum, volatility,
                            volume, price action, support/resistance
```

## 9. Önemli sınıflar ve metotlar

### `Program.cs`

Top-level console uygulamasıdır. Başlıca görevleri:

- `main()`: klasörleri ve log sink'lerini hazırlar, config'i yükler, AutoRun veya ana menü döngüsünü başlatır.
- `showMainMenu()`: ana menüyü çizer.
- `MenuInput()`, `ReadMenuInputWithTimeout()`: geri sayımlı, Esc/Backspace/T destekli giriş yönetimi.
- `handleReadData()`: okuma önizlemesi ve kullanıcı kararını yönetir.
- `handleSingleTrader()` vb.: config özeti, preview, çalıştırma ve yeniden çalıştırma döngülerini yönetir.
- `runSingleTraderAlgoTrade()` vb.: gerçek Core sınıflarını kurup işi çalıştırır ve çıktıları üretir.
- `runFullScript()`: dosyadan `.csx` okuyup çalıştırır.

### `AppSettings`

Repo kökünü uygulama binary konumundan hesaplar ve bütün standart yolları merkezi olarak sunar:

- `InputsDir`, `ConfigsDir`, `ScriptsDir`
- `OutputsDir`, `LogsDir`, `OptLogsDir`, `ScanLogsDir`
- Python/plotter klasörleri
- `EnsureDirectories()`
- `ResolvePythonDll()`

### `AppConfig`, `AppConfigLoader`, `AppConfigApplier`

- `AppConfig`: JSON'daki bütün ayar bölümlerinin strongly typed modelidir.
- `AppConfigLoader.Load()`: JSON'u modele yükler.
- `AppConfigLoader.CreateSampleIfNotExists()`: ana config yoksa örnek üretir.
- `AppConfigApplier.ApplyAppSettings()`: genel ayarları ve veri yolunu uygular.
- `ApplySingleTrader()`, `ApplyMultipleTrader()`, `ApplyConfirmingSingleTrader()`, `ApplyConfirmingMultipleTrader()`: ilgili çalışma nesnesini config'ten kurar.
- `Build...ScanOptions()`: tarayıcı sınıfları için options nesneleri üretir.

### `StockData` ve `StockDataReader`

`StockData`, tek OHLCV barını temsil eder. Fiyat alanlarının yanında mum gövdesi, gölge, değişim yüzdesi ve tipik fiyat gibi hesaplanan özellikler sağlar.

`StockDataReader : MarketDataProvider`:

- `ReadMetaData()`: dosyanın `#` meta satırlarını okur.
- `ReadDataFast()`: veriyi paralel ve filtreli okur.
- `GetData()`, `GetDataCount()`: bellekteki barları döndürür.
- `Head()`, `Tail()`: örnek ilk/son kayıtları verir.
- `OnReadMetaData`, `OnReadData`, `OnProgress`: konsol ilerlemesini besleyen event'lerdir.

### `AlgoTrader`

Core katmanının ana facade/orchestrator sınıfıdır. Console uygulaması ayrıntılı trader sınıflarını çoğunlukla bunun üzerinden yönetir.

Önemli metotlar:

- `SetData()`: fiyat verisini bağlar.
- `RegisterLogger()`, `RegisterTimer()`: altyapı servislerini bağlar.
- `ConfigureStrategyFromConfig()`, `ConfigureQueryFromConfig()`: config tabanlı bileşen seçer.
- `Initialize()`, `Reset()`: çalışma durumunu hazırlar/temizler.
- `RunSingleTraderWithProgressAsync()`
- `RunMultipleTraderWithProgressAsync()`
- `RunConfirmingSingleTraderWithProgressAsync()`
- `RunConfirmingMultipleTraderWithProgressAsync()`
- `WriteTraderDataToFilesAsync()`: liste ve istatistik çıktılarını üretir.
- `SetupPython()`, `PlotSingleTraderData()`, `PlotMultipleTraderData()`: grafik entegrasyonunu yürütür.

### Trader sınıfları

- `SingleTrader`: tek strateji/query üzerinde bar-bar sinyal, pozisyon, kâr/zarar ve istatistik hesaplar.
- `MultipleTrader`: child trader'ları çalıştırır ve `BuildConsensusSignal()` ile ortak karar üretir.
- `ConfirmingSingleTrader`: sinyal trader'ının sanal pozisyon sonucunu main trader için teyit olarak kullanır.
- `ConfirmingMultipleTrader`: consensus sinyalini sanal pozisyon konfirmasyonundan geçirir.
- `VirtualPositionConfirmer`: sanal pozisyonun kâr/zarar eşiklerini, trigger ve conflict kurallarını izler.
- `SingleTraderOptimizer`: parametre kombinasyonlarını üretir, testleri yürütür, sonuçları sıralar ve raporlar.

### Strateji ve query sistemi

- `IStrategy` / `BaseStrategy`: strateji sözleşmesi ve ortak davranış.
- `StrategyRegistry`: reflection ile mevcut stratejileri keşfeder ve ada göre örnek oluşturur.
- `StrategyConfigLoader`: `StrategyConfig.txt` içindeki sürümlü parametreleri okur.
- `IQuery` / `BaseQuery`: query sözleşmesi ve tabanı.
- `QueryRegistry`, `QueryConfigLoader`: query keşfi ve config yükleme.

Yeni stratejiler çoğunlukla `src/AlgoTrade.Core/Trading/Strategies` altına eklenir. Basit stratejiler arasında MA, MACD, RSI, MOST, OTT, PMax, SuperTrend, Bollinger, Stochastic, ADX ve benzerleri bulunur.

### İndikatör sistemi

`IndicatorManager`, trader verisi üzerinde indikatörlere merkezi erişim ve cache sağlar. Hesaplayıcılar kategori bazında ayrılmıştır:

- `MovingAverageCalculator`: çok sayıda hareketli ortalama,
- `MomentumIndicators`: RSI, MACD, Stochastic, CCI, Williams %R vb.,
- `TrendIndicators`: SuperTrend, MOST, ADX, OTT, PMax, Ichimoku vb.,
- `VolatilityIndicators`: ATR, Bollinger, Keltner, Donchian,
- `VolumeIndicators`: OBV, VWAP, MFI, CMF,
- `PriceActionIndicators`: HH/HL, swing, ZigZag, fractal, HHV/LLV,
- `SupportResistanceIndicators`: Fibonacci ve pivot çeşitleri.

### Tarayıcı sınıfları

Her menü taramasına karşılık gelen bir sınıf bulunur:

- `SymbolScanner`, `TimeframeScanner`, `SymbolTimeframeScanner`
- `MultiStrategySymbolScanner`, `MultiStrategyTimeframeScanner`, `MultiStrategySymbolTimeframeScanner`
- `QuerySymbolScanner`, `QueryTimeframeScanner`, `QuerySymbolTimeframeScanner`
- `MultiQuerySymbolScanner`, `MultiQueryTimeframeScanner`, `MultiQuerySymbolTimeframeScanner`

Genel kullanım desenleri `Run(options, csvPath, txtPath)`, `Results` koleksiyonu ve `OnProgress` callback'idir.

### Raporlama ve loglama

- `Statistics`: işlem performans ölçülerini hesaplar.
- `StatisticsExporter`: istatistikleri dışarı verir.
- `LogManager`: singleton log yöneticisidir.
- `ConsoleSink`, `FileSink`, `DebugSink`, `NetworkSink`: farklı log hedefleridir.
- `TimeManager`: isimlendirilmiş stopwatch'larla süre ölçer.

### Python ve bundle köprüleri

- `PythonPlotter`: pythonnet ile eski in-process grafik akışı.
- `TradeDataBundleConverter`: trader listelerini NPZ + view JSON paketine dönüştürür.
- `NpzWriter`: NumPy olmadan NPZ/NPY yazar.
- `DearPyGuiDataPlotter`: ayrı DearPyGui process'ini başlatır ve runtime komutları gönderir.
- `DearImGuiBundleDataPlotter`: aynı bundle protokolünü DearImGui backend'iyle kullanır.

## 10. Hangi menüyü ne zaman kullanmalıyım?

| İhtiyaç | Seçim |
|---|---:|
| Tek stratejiyi normal biçimde test etmek | `5` |
| Birden fazla stratejinin ortak kararını test etmek | `6` |
| Strateji parametrelerini optimize etmek | `7` |
| Aynı bellekteki veriyle ayar değiştirip yeniden koşmak | Önce `1`, sonra `2/3/4/22/24` |
| Çok sayıda sembolde tek strateji | `10` |
| Çok sayıda zaman diliminde tek strateji | `11` |
| Sembol × zaman dilimi matrisi | `13` |
| Birden çok stratejiyi geniş matriste karşılaştırmak | `15` |
| Query ile sembol taramak | `16` veya birden çok query için `20` |
| Query ile sembol × periyot matrisi | `19` veya birden çok query için `21` |
| Sanal pozisyon teyitli tek strateji | `23` |
| Consensus + sanal pozisyon teyidi | `25` |
| Hazır özel bir `.csx` akışı | `8` |
| Yalnız grafik process iletişimini test etmek | `9` / `9b` |

## 11. Sorun giderme

### Seçim yaptım ama hiçbir şey olmadı

Muhtemelen `2`, `3`, `4`, `22` veya `24` ile yalnız çalıştırma seçilmiş, fakat veri belleğe yüklenmemiştir. Önce `1` kullanın veya doğrudan birleşik seçenek olan `5`, `6`, `7`, `23`, `25`'i seçin.

### Veri dosyası bulunamadı

Konsoldaki `File does not exist` mesajında gösterilen yolu kontrol edin. `AppConfig.json` içindeki veri klasörü/dosya ayarını düzeltip preview ekranında `R` ile yeniden yükleyin.

### Enter'a bastım, grafik testi açıldı

Bu mevcut ana menü varsayılanının `9b` olmasından kaynaklanır. Ana menüde istediğiniz numarayı açıkça yazın. Kalıcı değişiklik istenirse `Program.cs` içindeki `MenuInput("9b")` varsayılanı daha güvenli bir değere dönüştürülmelidir.

### Grafik açılmıyor

- `PlotEnabled` değerini kontrol edin.
- `QueryOnly` modunda grafik açılmadığını unutmayın.
- `.venv` ve Python bağımlılıklarını `setupPythonEnvs.bat` ile hazırlayın.
- `PlotBackend` değerinin `0` veya `1` olduğunu kontrol edin.
- Ayrı ayrı `9` ve `9b` testleriyle backend process'lerini sınayın.

### Eski sonuçlar kayboldu

Uygulama başlangıçta `outputs/logs` içeriğini temizler. Kalıcı tutulacak sonuçları başka klasöre kopyalayın. `outputs/opt` ve `outputs/scan` farklı çıktı alanlarıdır.

### Ayar değişikliği uygulanmadı

Dosyayı kaydettikten sonra preview ekranında `R` kullanın. `E` akışında editörü kaydedip kapattıktan sonra konsola dönerek Enter'a basılması gerekir.

## 12. İlgili mevcut belgeler

Daha derin ayrıntı gerektiğinde:

- `docs/manual/02-console-menu-guide.md`: önceki ayrıntılı konsol menü notları,
- `docs/manual/01-class-reference.md`: geniş sınıf referansı,
- `docs/manual/03-scripting-guide.md`: script sistemi,
- `docs/manual/09-new-strategy-guide.md`: yeni strateji ekleme,
- `docs/PROJECT_ANALYSIS.md`: kapsamlı teknik proje analizi,
- `docs/Indicators-README.md`: indikatör kütüphanesi,
- `docs/SimpleMAStrategy_opt_rehberi.md`: MA optimizasyon örneği.

Bu dosya ise günlük kullanım için başlangıç noktası ve menülerin güncel davranışının özetidir.
