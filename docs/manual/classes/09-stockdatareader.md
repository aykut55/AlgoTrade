# StockDataReader — Veri Okuma (Read Data, Menü [1])

> [Class Reference](../01-class-reference.md) setinin bir parçası — bu sınıf ayrı dosyada,
> çünkü diğer 8 sınıftan (§1-§8, hâlâ [01-class-reference.md](../01-class-reference.md)'de)
> çok daha derin işlendi (tam kaynak kodu, bağımsız kullanım örnekleri, kullanım haritası).

### Dosyalar

- `src/AlgoTrade.Core/StockDataReader/StockDataReader.cs` (325 satır)
- `src/AlgoTrade.Core/DataProvider/MarketDataProvider.cs` (204 satır, taban sınıf)
- `src/AlgoTrade.Core/StockData/StockData.cs` (94 satır, veri birimi `struct`)

### Rolü

- Disk üzerindeki `;`-ayraçlı CSV/TXT bar verisini (meta veri header + bar satırları) okuyup
  `List<StockData>`'a çevirir.
- `MarketDataProvider`'dan türer (`GetData()`, `GetDataCount()`, `IsDataReady` vb. oradan gelir),
  `IDisposable`.
- Console `[1] Read Data` menüsü bu sınıfı iki aşamalı (`ReadMetaData` → `ReadDataFast`) çağırır.
- Sonucu tüm `AlgoTrader` tabanlı run'lar (`SingleTrader`/`MultipleTrader`/Confirming*/Optimizer,
  bkz. [Class Reference Bölüm 1](../01-class-reference.md#1-algotrader--orkestratörfacade)–[Bölüm 5](../01-class-reference.md#5-singletraderoptimizer--grid-search-optimizasyon)) veri kaynağı olarak kullanır.

### Ne zaman kullanılır

- Her `AlgoTrader` akışından ÖNCE.
- Console `[1]` (sadece oku) veya `[5]`-`[7]`/`[23]`/`[25]` ("Read Data + X" —
  `handleReadData()` önce çağrılıp `true` dönerse ilgili trader handler'ı ardından çalışır,
  bkz. [02-console-menu-guide.md § Menü Haritası](../02-console-menu-guide.md#1-menü-haritası).

### Sınıf İskeleti (ilk bakış)

Aşağıdaki bloktaki metod gövdeleri kaldırılmış — sadece alan/property/event/metod imzaları
(public + private, hepsi), gerçek kaynağın (`StockDataReader.cs`) sırasıyla birebir aynı.
Detaylı açıklamalar aşağıdaki [Public API](#public-api) bölümünde (private yardımcı metodların
gövdeleri — `ApplyFilter` hariç, o ayrıca gösterildi — bu dokümanda yer almıyor, gerekirse
doğrudan kaynağa bakılmalı).

```csharp linenums="1"
public class StockDataReader : MarketDataProvider, IDisposable
{
    public enum FilterMode
    {
        All, LastN, FirstN, IndexRange, AfterDateTime, BeforeDateTime, DateTimeRange
    }

    private readonly ConcurrentDictionary<string, string> _metaData = new();
    private readonly List<string> _metaDataLines = new();
    private readonly Stopwatch _stopwatch = new();
    private bool _isDisposed;

    public bool IsMetaDataRead { get; private set; }

    // GetDataCount(), GetData(), GetData(int,int), Data → MarketDataProvider taban sınıfından gelir

    public int GetMetaDataCount() => _metaData.Count;
    public ConcurrentDictionary<string, string> GetMetaData() => _metaData;
    public List<string> GetMetaDataLines() => _metaDataLines;
    public int GetMetaDataLinesCount() => _metaDataLines.Count;

    public event Action<StockDataReader, ConcurrentDictionary<string, string>>? OnReadMetaData;
    public event Action<StockDataReader, List<StockData>, long>? OnReadData;
    public event Action<StockDataReader, int, bool>? OnProgress;

    public void StartTimer() => _stopwatch.Start();
    public void ReStartTimer() => _stopwatch.Restart();
    public void StopTimer() => _stopwatch.Stop();
    public long GetElapsedTimeMsec() => _stopwatch.ElapsedMilliseconds;
    public TimeSpan GetElapsedTime() => _stopwatch.Elapsed;

    public void Clear();

    public ConcurrentDictionary<string, string> ReadMetaData(string filePath);

    public List<StockData> ReadDataFast(
        string filePath,
        FilterMode mode = FilterMode.All,
        int n1 = 0,
        int n2 = 0,
        DateTime? dt1 = null,
        DateTime? dt2 = null);

    private static List<StockData> ApplyFilter(
        List<StockData> data, FilterMode mode, int n1, int n2, DateTime? dt1, DateTime? dt2);

    public string Head(int n = 5);
    public string Tail(int n = 5);
    public string ToTable();
    public string ToTable(int start, int end);

    public void WriteToCsvFile(string filePath, List<StockData> data);
    public void WriteToTxtFile(string filePath, List<StockData> data);

    private string BuildCsvHeader(List<StockData> data);
    private string BuildTxtHeader(List<StockData> data);

    private static string FormatTable(List<StockData> data);
    private static string FormatSigned(double value);

    public void Dispose();

    private static StockData CreateStockData(string[] parts, CultureInfo culture);
}
```

### Üye İndeksi — Hangisi Nerede Anlatılıyor

Yukarıdaki iskeletteki her üye, kaynak sırasıyla, `StockDataReader::Üye` notasyonuyla — aşağıdaki
Public API bölümlerinden hangisinde detaylandırıldığına link veriyor (private alanlar/yardımcı
metodlar için ayrı bir alt başlık yoksa, o üyenin fiilen kullanıldığı en yakın bölüme yönlendirir).
**#** kolonu, yukarıdaki kod bloğunun (`linenums="1"`) gerçek satır numarasıyla birebir eşleşiyor.

| # | Üye | Tür | Detay |
|---|---|---|---|
| 3 | `StockDataReader::FilterMode` | enum | [Bar verisi okuma](#bar-verisi-okuma) |
| 8 | `StockDataReader::_metaData` | private field | [Meta veri okuma](#meta-veri-okuma) |
| 9 | `StockDataReader::_metaDataLines` | private field | [Meta veri okuma](#meta-veri-okuma) |
| 10 | `StockDataReader::_stopwatch` | private field | [Zamanlama](#zamanlama) |
| 11 | `StockDataReader::_isDisposed` | private field | [Kurulum/Temizlik](#kurulumtemizlik) |
| 13 | `StockDataReader::IsMetaDataRead` | public property | [Meta veri okuma](#meta-veri-okuma) |
| 17 | `StockDataReader::GetMetaDataCount()` | public method | [Meta veri okuma](#meta-veri-okuma) |
| 18 | `StockDataReader::GetMetaData()` | public method | [Meta veri okuma](#meta-veri-okuma) |
| 19 | `StockDataReader::GetMetaDataLines()` | public method | [Meta veri okuma](#meta-veri-okuma) |
| 20 | `StockDataReader::GetMetaDataLinesCount()` | public method | [Meta veri okuma](#meta-veri-okuma) |
| 22 | `StockDataReader::OnReadMetaData` | public event | [Event'ler](#eventler) |
| 23 | `StockDataReader::OnReadData` | public event | [Event'ler](#eventler) |
| 24 | `StockDataReader::OnProgress` | public event | [Event'ler](#eventler) |
| 26 | `StockDataReader::StartTimer()` | public method | [Zamanlama](#zamanlama) |
| 27 | `StockDataReader::ReStartTimer()` | public method | [Zamanlama](#zamanlama) |
| 28 | `StockDataReader::StopTimer()` | public method | [Zamanlama](#zamanlama) |
| 29 | `StockDataReader::GetElapsedTimeMsec()` | public method | [Zamanlama](#zamanlama) |
| 30 | `StockDataReader::GetElapsedTime()` | public method | [Zamanlama](#zamanlama) |
| 32 | `StockDataReader::Clear()` | public method | [Kurulum/Temizlik](#kurulumtemizlik) |
| 34 | `StockDataReader::ReadMetaData(filePath)` | public method | [Meta veri okuma](#meta-veri-okuma) |
| 36 | `StockDataReader::ReadDataFast(...)` | public method | [Bar verisi okuma](#bar-verisi-okuma) |
| 44 | `StockDataReader::ApplyFilter(...)` | private static method | [Bar verisi okuma](#bar-verisi-okuma) — gerçek kaynağı orada gösteriliyor |
| 47 | `StockDataReader::Head(n)` | public method | [İnceleme/çıktı](#incelemeçıktı) |
| 48 | `StockDataReader::Tail(n)` | public method | [İnceleme/çıktı](#incelemeçıktı) |
| 49 | `StockDataReader::ToTable()` | public method | [İnceleme/çıktı](#incelemeçıktı) |
| 50 | `StockDataReader::ToTable(start, end)` | public method | [İnceleme/çıktı](#incelemeçıktı) |
| 52 | `StockDataReader::WriteToCsvFile(...)` | public method | [İnceleme/çıktı](#incelemeçıktı) |
| 53 | `StockDataReader::WriteToTxtFile(...)` | public method | [İnceleme/çıktı](#incelemeçıktı) |
| 55 | `StockDataReader::BuildCsvHeader(...)` | private method | [İnceleme/çıktı](#incelemeçıktı) — `WriteToCsvFile`'ın iç yardımcısı, ayrıca anlatılmıyor |
| 56 | `StockDataReader::BuildTxtHeader(...)` | private method | [İnceleme/çıktı](#incelemeçıktı) — `WriteToTxtFile`'ın iç yardımcısı, ayrıca anlatılmıyor |
| 58 | `StockDataReader::FormatTable(...)` | private static method | [İnceleme/çıktı](#incelemeçıktı) — `Head`/`Tail`/`ToTable`'ın iç yardımcısı, ayrıca anlatılmıyor |
| 59 | `StockDataReader::FormatSigned(...)` | private static method | Sadece "Sınıf İskeleti"nde — kaynakta tanımlı ama hiçbir yerden çağrılmıyor (bkz. `StockDataReader.cs:273-276`, çağıran yok) |
| 61 | `StockDataReader::Dispose()` | public method | [Kurulum/Temizlik](#kurulumtemizlik) |
| 63 | `StockDataReader::CreateStockData(...)` | private static method | [Bar verisi okuma](#bar-verisi-okuma) — `ReadDataFast`'ın satır-parse yardımcısı |

## Public API

### Kurulum/Temizlik

- `StockDataReader()` — parametresiz constructor.
- `Clear()` — timer/metaData/metaDataLines/data'yı sıfırlar (yeniden okumadan önce çağrılmalı).
- `Dispose()` — aynı temizliği yapar (`IDisposable`).

### Meta veri okuma

- `ReadMetaData(string filePath)` → `ConcurrentDictionary<string,string>` — dosyanın başındaki
  `#` ile başlayan satırları (`key: value` formatında) parse eder, ilk `#`-olmayan satırda durur.
  Dosya yoksa `FileNotFoundException`. Başarılı olursa `IsMetaDataRead=true` olur,
  `OnReadMetaData` event'i tetiklenir.
- `GetMetaData()` → `ConcurrentDictionary<string,string>`, `GetMetaDataCount()` → `int`,
  `GetMetaDataLines()` → `List<string>` (ham satırlar), `GetMetaDataLinesCount()` → `int`.
- Tipik meta anahtarlar (Console'un `OnReadMetaData` callback'inin bastığı 6 alan):
  `Kayit_Zamani`, `GrafikSembol`, `GrafikPeriyot`, `BarCount`, `Baslangic_Tarihi`,
  `Bitis_Tarihi`, `Format`.

### Bar verisi okuma

```csharp linenums="1"
public List<StockData> ReadDataFast(
    string filePath,
    FilterMode mode = FilterMode.All,
    int n1 = 0,
    int n2 = 0,
    DateTime? dt1 = null,
    DateTime? dt2 = null)
```

| Parametre | Tip | Açıklama |
|---|---|---|
| `filePath` | `string` | Okunacak dosya yolu; yoksa `FileNotFoundException` |
| `mode` | `FilterMode` enum | Aşağıdaki 7 modtan biri, varsayılan `All` |
| `n1`, `n2` | `int` | `LastN`/`FirstN`'de `n1`=alınacak bar sayısı; `IndexRange`'de `n1`=başlangıç, `n2`=bitiş index (inclusive) |
| `dt1`, `dt2` | `DateTime?` | `AfterDateTime`/`BeforeDateTime`'da `dt1`; `DateTimeRange`'de ikisi de |

**İşleyiş:**

- `File.ReadLines(...).AsParallel()` ile satır satır okur, boş/`#`/`Id`-başlangıçlı satırları
  eler, `;` ile böler (≥8 alan şart).
- Her satırı `CreateStockData(...)` ile `StockData` struct'ına çevirir (format hatalı satırlar
  `FormatException` yutularak sessizce atlanır).
- `Id`'ye göre sıralar, `ApplyFilter(...)` uygular, sonucu `_data`'ya atar — `IsDataReady`
  (taban sınıftan, `_data.Count > 0`) bundan sonra otomatik `true` olur.
- Bitince `OnProgress(count, isCompleted=true)` ve `OnReadData(this, filtered, elapsedMs)`
  tetiklenir; sırasında (her 1000 satırda bir) `OnProgress(count, isCompleted=false)`.

`FilterMode` enum ve `ApplyFilter(...)`'daki karşılıkları:

| Mode | Davranış |
|---|---|
| `All` | Filtre yok, tüm veri |
| `LastN` | `data.TakeLast(n1)` |
| `FirstN` | `data.Take(n1)` |
| `IndexRange` | `n1..n2` index aralığı (inclusive); `n1`/`n2` geçersizse boş liste |
| `AfterDateTime` | `DateTime >= dt1` (dt1 boşsa filtre uygulanmaz) |
| `BeforeDateTime` | `DateTime <= dt1` (dt1 boşsa filtre uygulanmaz) |
| `DateTimeRange` | `dt1 <= DateTime <= dt2` (ikisi de doluysa; değilse filtre uygulanmaz) |

Yukarıdaki tablonun asıl kaynağı — `private static` bir metod, "Sınıf İskeleti" bloğunda
(sadece public API'yi listelediği için) yer almıyor, gerçek gövdesi (`StockDataReader.cs:142-159`):

```csharp linenums="1"
private static List<StockData> ApplyFilter(List<StockData> data, FilterMode mode, int n1, int n2, DateTime? dt1, DateTime? dt2)
{
    return mode switch
    {
        FilterMode.All => data,
        FilterMode.LastN => data.TakeLast(n1).ToList(),
        FilterMode.FirstN => data.Take(n1).ToList(),
        FilterMode.IndexRange => n1 >= 0 && n2 >= n1 && n1 < data.Count
            ? data.Skip(n1).Take(Math.Min(n2, data.Count - 1) - n1 + 1).ToList()
            : new List<StockData>(),
        FilterMode.AfterDateTime => dt1.HasValue ? data.Where(x => x.DateTime >= dt1.Value).ToList() : data,
        FilterMode.BeforeDateTime => dt1.HasValue ? data.Where(x => x.DateTime <= dt1.Value).ToList() : data,
        FilterMode.DateTimeRange => dt1.HasValue && dt2.HasValue
            ? data.Where(x => x.DateTime >= dt1.Value && x.DateTime <= dt2.Value).ToList()
            : data,
        _ => data
    };
}
```

- `IndexRange` — `n1 >= 0 && n2 >= n1 && n1 < data.Count` şartlarından biri bile sağlanmazsa
  **sessizce boş liste** döner (istisna fırlatmaz) — örneğin `n1=500` ama veri sadece 300 bar
  içeriyorsa, sonuç `0` bar olur, hata değil.
- `AfterDateTime`/`BeforeDateTime` — `dt1` `null` ise (yani `AppConfig.json`'da `Dt1` boş
  bırakılmışsa) filtre tamamen atlanır, `data` olduğu gibi döner — `FilterMode.All` ile aynı
  sonucu verir.
- `DateTimeRange` — `dt1`/`dt2`'den biri bile `null` ise aynı şekilde filtre atlanır.
- Varsayılan `_ => data` dalı aslında hiç tetiklenmez (enum'daki 7 değerin hepsi yukarıda
  ayrı ayrı ele alınmış), C#'ın `switch` ifadesinin exhaustive olmasını garanti etmek için var.

### Sonuç erişimi

(`MarketDataProvider` taban sınıfından — `StockDataReader`'a özel değil)

- `GetData()` → `List<StockData>`, `GetData(start, end)` → aralık (inclusive), `GetDataCount()`
  → `int`, `Data` (property, aynı liste).
- `IsDataReady` / `IsDataRead` / `IsInitialized` → `_data.Count > 0`.
- `GetDataRange()` → `(DateTime Start, DateTime End)`; `GetDataInfo()` → `StringBuilder`
  ("Total Bars / Start Date / End Date").
- `GetClosePrices()/GetOpenPrices()/GetHighPrices()/GetLowPrices()` → `double[]`;
  `GetVolume()/GetLotSizes()` → `long[]`.
- `GetDateTimes()/GetDates()/GetTimes()/GetEpochTimes()` → zaman dizileri.

### İnceleme/çıktı

- `Head(n=5)` / `Tail(n=5)` / `ToTable()` / `ToTable(start, end)` → `string` (tablo formatında;
  `Id/DateTime/Date/Time/Open/High/Low/Close/Volume/Size/Diff/Chg%/EpochTime` kolonları).
  Console'da `AppSettings`'teki `addHeadTailInfo` bayrağı açıksa `readStockData()` sonunda
  `Head()`/`Tail()` otomatik loglanır.
- `WriteToCsvFile(filePath, data)` / `WriteToTxtFile(filePath, data)` — meta veri header +
  bar satırlarını dosyaya yazar (`;`-ayraçlı CSV veya sabit-genişlik TXT).

### Zamanlama

- `StartTimer()` / `ReStartTimer()` / `StopTimer()` / `GetElapsedTimeMsec()` → `long` /
  `GetElapsedTime()` → `TimeSpan` — `readStockData()` bunu `ReadMetaData` ve `ReadDataFast` için
  ayrı ayrı ölçüp Console'a "... ms" olarak basar.

### Event'ler

- `OnReadMetaData(StockDataReader sender, ConcurrentDictionary<string,string> metaData)` —
  `ReadMetaData()` sonunda.
- `OnReadData(StockDataReader sender, List<StockData> data, long elapsedMs)` —
  `ReadDataFast()` sonunda.
- `OnProgress(StockDataReader sender, int count, bool isCompleted)` — `ReadDataFast()`
  sırasında (her 1000 satırda `isCompleted=false`) ve bitince (`isCompleted=true`).

## StockData — Veri Birimi (`struct`)

- Ham alanlar: `Id`(int), `DateTime`, `Date`, `Time`(TimeSpan), `Open/High/Low/Close`(double),
  `Volume`(long), `Size`(long, lot).
- Hesaplanan salt-okunur property'ler: `EpochTime`, `Diff`(Close-Open), `ChangePct`,
  `IsBullish`/`IsBearish`/`IsNeutral`, `Range`, `BodySize`, `UpperShadow`/`LowerShadow`,
  `MidPrice`, `TypicalPrice`, `WeightedClose`.

## Çağrı Zinciri — Menüden Çağrılma (Console `[1]` → `handleReadData()`)

1. `handleReadData()` (`Program.cs:730`) — döngü: `showReadDataPreview()` ile
   `appConfig.ReadData` (`FilterMode`/`N1`/`N2`/`Dt1`/`Dt2`) + `stockDataFullFileName`'i JSON
   önizleme olarak basar, `[ENTER]`/`[E]`/`[R]`/`[B]` seçimini bekler ([02-console-menu-guide.md § Preview/Confirm Ekranı Kısayolları](../02-console-menu-guide.md#5-previewconfirm-ekranı-kısayolları),
   ortak Preview/Confirm desenin aynısı).
2. `[ENTER]` → `readStockData(appConfig.ReadData)` (`Program.cs:610`) — asıl işi yapan fonksiyon.
   `appConfig`, `AppConfig.json`'ın tamamının deserialize edilmiş hali (`AppConfigLoader.Load(...)`
   ile yüklenmiş); `.ReadData` onun `ReadDataConfig` tipindeki property'si, yani JSON'daki
   `"ReadData"` bölümü (`FilterMode`/`N1`/`N2`/`Dt1`/`Dt2`) — fonksiyona `cfg` parametresi
   olarak geçiyor.
3. `readStockData` içinde: **önce `stockDataFullFileName` var mı kontrolü** (`Program.cs:614`,
   bkz. aşağıdaki not) → `new StockDataReader()` +
   3 event bağlama (`OnReadMetaData`/`OnReadData`/`OnProgress`, Console tarafı callback'leri
   `Program.cs:64-94`) → `Clear()` → `ReStartTimer()` → `ReadMetaData(filePath)` → `StopTimer()`
   → **`if (!stockDataReader.IsMetaDataRead) return;`** (`Program.cs:646`, bkz. aşağıdaki not)
   → `cfg`'den (`ReadDataConfig`) `FilterMode`/`N1`/`N2`/`Dt1`/`Dt2` çözümü (`Enum.TryParse`,
   `DateTime.Parse`) → `ReStartTimer()` → `ReadDataFast(filePath, filterMode, n1, n2, dt1, dt2)`
   → `StopTimer()` → `stockDataList = stockDataReader.GetData()` (global state'e atanır).

> **Not — `IsMetaDataRead` koruması fiilen hiç tetiklenmiyor:** `Program.cs:646`'daki
> `if (!stockDataReader.IsMetaDataRead) return;` satırı, meta veri okuma başarısızsa
> `ReadDataFast(...)`'ın (dolayısıyla asıl bar verisinin) hiç çalışmamasını sağlamak için var.
> Ama `StockDataReader.ReadMetaData()`'nın kaynağına bakınca (`StockDataReader.cs:66-98`)
> `IsMetaDataRead = true` satırı, metodun normal dönüşünden hemen önce **koşulsuz** çalışıyor —
> dosyada hiç `#` header satırı olmasa bile (`_metaData` boş kalır ama `IsMetaDataRead` yine
> `true` olur). Metodun `IsMetaDataRead`'i `false` bırakarak dönebildiği tek yol yok: ya
> `FileNotFoundException` fırlatıp hiç dönmüyor (bu durumda zaten `readStockData`'nın dış
> `try/catch`'i devreye girer, kod hiçbir zaman satır 646'ya ulaşmaz), ya da normal dönüyor ve
> flag her zaman `true`. Yani bu satır mevcut haliyle **erişilemez/ölü bir savunma kontrolü** —
> gelecekte `ReadMetaData()` refactor edilip "meta header hiç yoksa `IsMetaDataRead=false`
> bırak" gibi bir davranış eklenirse anlam kazanır, ama bugün hiçbir girdiyle tetiklenmiyor.

> **Not — `stockDataFullFileName` dosyası bulunamazsa ne olur:** `readStockData()`'nın en
> başındaki `if (!File.Exists(stockDataFullFileName))` kontrolü (`Program.cs:614-618`)
> başarısız olursa:
>
> - Ekrana `"File does not exist : {stockDataFullFileName}"` mesajı basılır
>   (`LogManager.LogRaw(...)`).
> - Fonksiyon anında `return` eder — **hiçbir `StockDataReader` yaratılmaz**, `ReadMetaData()`/
>   `ReadDataFast()` hiç çağrılmaz.
> - Bu, `stockDataReader`/`stockDataList`/`stockMetaData` global değişkenlerine hiç
>   dokunulmadan çıkılması demek — yani eğer bundan ÖNCE başarılı bir okuma yapılmışsa, o eski
>   veriler bellekte **olduğu gibi kalır** (`stockDataReader.IsDataReady` hâlâ `true` görünür).
>   Örneğin `AppConfig.json`'da `StockDataFile` yolunu geçersiz bir dosyaya çevirip `[1] Read
>   Data`'yı tekrar çalıştırırsan, ekranda hata mesajını görürsün ama Console bir önceki
>   (geçerli dosyadan okunmuş) veriyle çalışmaya devam eder — sanki hiçbir şey olmamış gibi.
> - Bu kontrol, meta veri okumadan (`ReadMetaData()`, kendi içinde `FileNotFoundException`
>   fırlatabilir) ÖNCE yapılıyor — yani dosya yokluğu için ayrı bir `try/catch` gerekmiyor,
>   `File.Exists` ile daha en baştan erken çıkılıyor.

## AppConfig Kaynağı — `ReadDataConfig`

`AppConfig.json`'daki `"ReadData"` bölümünü karşılayan C# sınıfı (`AppConfig.cs:62-69`) —
`readStockData(ReadDataConfig? cfg)`'nin `cfg` parametresinin gerçek tipi bu:

```csharp linenums="1"
public class ReadDataConfig
{
    public string FilterMode { get; set; } = "All";
    public int    N1         { get; set; } = 0;
    public int    N2         { get; set; } = 0;
    public string Dt1        { get; set; } = "";
    public string Dt2        { get; set; } = "";
}
```

Bu sınıfın `AppConfig.json`'daki JSON karşılığı:

```json linenums="1"
{ "FilterMode": "All", "N1": 0, "N2": 0, "Dt1": "", "Dt2": "" }
```

- `Dt1`/`Dt2` boş string ise `readStockData()` içinde `DateTime.Parse` hiç çağrılmaz (`null`
  kalır → ilgili filtre modunda filtre uygulanmaz).
- Okunacak dosyanın kendisi `ReadData` altında DEĞİL — `AppSettings.StockDataFile` alanında
  (`AppConfigApplier.ApplyAppSettings(...)` bunu `stockDataFullFileName`'e çözer, bkz.
  `Program.cs:369`).

## `readStockData()` — Tam Kaynak (`Program.cs:610-692`)

```csharp linenums="1" hl_lines="11 25 37 39-42 56 68 74 76"
void readStockData(ReadDataConfig? cfg = null)
{
    try
    {
        if (!File.Exists(stockDataFullFileName))
        {
            LogManager.LogRaw($"File does not exist : {stockDataFullFileName}");
            return;
        }

        stockDataReader = new StockDataReader();
        stockDataReader.OnReadMetaData += OnReadMetaData;
        stockDataReader.OnReadData     += OnReadData;
        stockDataReader.OnProgress     += OnProgress;

        string fileName = Path.GetFileName(stockDataFullFileName);
        string fileDir  = Path.GetDirectoryName(stockDataFullFileName)!;
        string filePath = Path.Combine(fileDir, fileName);

        LogManager.LogRaw("");
        LogManager.LogRaw($"Reading Meta Data from   : {filePath}");

        stockDataReader.Clear();
        stockDataReader.ReStartTimer();
        stockMetaData = stockDataReader.ReadMetaData(filePath);
        stockDataReader.StopTimer();

        long t1 = stockDataReader.GetElapsedTimeMsec();
        LogManager.DisableConsoleSink();
        {
            consoleLogger!.Write("is completed in ");
            consoleLogger.Write($"{t1}", ConsoleColor.Green);
            consoleLogger.WriteLine(" ms.");
            LogManager.EnableConsoleSink();
        }

        if (!stockDataReader.IsMetaDataRead) return;

        // ReadDataFast parametrelerini config'den çöz
        var    filterMode = StockDataReader.FilterMode.All;
        int    n1 = 0, n2 = 0;
        DateTime? dt1 = null, dt2 = null;

        if (cfg != null)
        {
            Enum.TryParse<StockDataReader.FilterMode>(cfg.FilterMode, ignoreCase: true, out filterMode);
            n1 = cfg.N1;
            n2 = cfg.N2;
            if (!string.IsNullOrWhiteSpace(cfg.Dt1)) dt1 = DateTime.Parse(cfg.Dt1);
            if (!string.IsNullOrWhiteSpace(cfg.Dt2)) dt2 = DateTime.Parse(cfg.Dt2);
        }

        LogManager.LogRaw($"Loading data from        : {filePath}");

        stockDataReader.ReStartTimer();
        stockDataReader.ReadDataFast(filePath, filterMode, n1, n2, dt1, dt2);
        stockDataReader.StopTimer();

        long t2 = stockDataReader.GetElapsedTimeMsec();
        LogManager.DisableConsoleSink();
        {
            consoleLogger!.Write("is completed in ");
            consoleLogger.Write($"{t2}", ConsoleColor.Green);
            consoleLogger.WriteLine(" ms.");
            LogManager.EnableConsoleSink();
        }

        stockDataList = stockDataReader.GetData();
        LogManager.LogRaw($"{"\n\tData count".PadRight(18)} : {stockDataReader.GetDataCount()}");

        if (addHeadTailInfo)
        {
            LogManager.LogRaw("");
            LogManager.LogRaw(stockDataReader.Head());
            LogManager.LogRaw("");
            LogManager.LogRaw(stockDataReader.Tail());
        }
    }
    catch (Exception ex)
    {
        LogManager.LogError($"An error occurred while reading data: {ex.Message}", ex);
    }
}
```

## Console Tarafındaki Callback Metodları (`Program.cs:64-96`)

`readStockData()` içindeki `stockDataReader.OnReadMetaData += OnReadMetaData;` /
`OnReadData += OnReadData;` / `OnProgress += OnProgress;` satırlarının bağladığı 3 metod —
`StockDataReader`'ın event'leri (bkz. yukarıdaki [Event'ler](#eventler)) tetiklendiğinde
gerçekte çalışan kod bu:

**`OnReadMetaData`** — `sender.GetMetaData()`'dan meta bilgiyi (Record Time/Chart Symbol/Chart
Period/Bar Count/Start Date/End Date/Format) hizalı bir metin olarak `LogManager.LogRaw(...)`
ile ekrana basar.

```csharp linenums="1"
void OnReadMetaData(StockDataReader sender, ConcurrentDictionary<string, string> metaData)
{
    if (!sender.IsMetaDataRead) return;

    var meta         = sender.GetMetaData();
    int padding      = 18;
    sb.Clear();
    sb.AppendLine($"{"\tRecord Time".PadRight(padding)}: {meta.GetValueOrDefault("Kayit_Zamani",    "N/A")}");
    sb.AppendLine($"{"\tChart Symbol".PadRight(padding)}: {meta.GetValueOrDefault("GrafikSembol",    "N/A")}");
    sb.AppendLine($"{"\tChart Period".PadRight(padding)}: {meta.GetValueOrDefault("GrafikPeriyot",  "N/A")}");
    sb.AppendLine($"{"\tBar Count".PadRight(padding)}: {meta.GetValueOrDefault("BarCount",            "N/A")}");
    sb.AppendLine($"{"\tStart Date".PadRight(padding)}: {meta.GetValueOrDefault("Baslangic_Tarihi", "N/A")}");
    sb.AppendLine($"{"\tEnd Date".PadRight(padding)}: {meta.GetValueOrDefault("Bitis_Tarihi",    "N/A")}");
    sb.Append(    $"{"\tFormat".PadRight(padding)}: {meta.GetValueOrDefault("Format",                "N/A")}");
    LogManager.LogRaw(sb.ToString());
}
```

- `if (!sender.IsMetaDataRead) return;` koruması burada da var — ama `ReadMetaData()` bu
  callback'i zaten `IsMetaDataRead = true` yapıldıktan **sonra** tetiklediği için (bkz.
  `StockDataReader.cs:95-96`) bu satır da pratikte hep `true` görür, yukarıdaki
  `readStockData()`'daki `Program.cs:646` guard'ıyla aynı "erişilemez koruma" durumu.

**`OnProgress`** — `isCompleted=false` iken `\r` ile aynı satırı üzerine yazarak ilerleyen kayıt
sayısını (`Record no`), `isCompleted=true` iken final sayıyı (`Record count`) basar;
`ReadDataFast()` sırasında (her 1000 satırda bir) ve bitişinde tetiklenir.

```csharp linenums="1"
void OnProgress(StockDataReader sender, int count, bool isCompleted)
{
    if (isCompleted)
    {
        consoleLogger!.Write($"\r\tRecord count     : {count}");
        consoleLogger.WriteLine("");
    }
    else
    {
        consoleLogger!.Write($"\r\tRecord no        : {count}");
    }
}
```

**`OnReadData`** — boş gövde. Event bağlanmış ama Console tarafında hiçbir şey yapmıyor;
veri zaten `stockDataList = stockDataReader.GetData()` ile senkron olarak okunduğu için bu
event'e ihtiyaç duyulmamış.

```csharp linenums="1"
void OnReadData(StockDataReader sender, List<StockData> data, long elapsedMs) { }
```

## Dönüş / Sonuç — Global State

`readStockData()` bittiğinde Console tarafında (`Program.cs` üst seviye değişkenler) 3 alan
güncellenir:

| Değişken | Tip | Kaynak |
|---|---|---|
| `stockDataReader` | `StockDataReader` | `new StockDataReader()` — sonraki menülerde `IsDataReady` kontrolü için tutulur |
| `stockDataList` | `List<StockData>` | `stockDataReader.GetData()` |
| `stockMetaData` | `ConcurrentDictionary<string,string>` | `stockDataReader.GetMetaData()` — `SymbolName`/`SymbolPeriod` (`GrafikSembol`/`GrafikPeriyot`) buradan okunur |

- Bu üçü, sonraki tüm `runXxxAlgoTrade()` fonksiyonlarının başındaki
  `if (stockDataReader is null || !stockDataReader.IsDataReady) return;` kontrolüyle tüketilir.
- Veri `algoTrader.SetData(stockDataReader.GetData())` ile `AlgoTrader`'a aktarılır (bkz.
  [Class Reference Bölüm 1](../01-class-reference.md#1-algotrader--orkestratörfacade) "Tipik Kullanım Akışı" adım 2).
- Okuma sırasında hata olursa (`readStockData`'daki `try/catch`) `LogManager.LogError(...)` ile
  loglanır, `stockDataReader.IsDataReady` `false` kalır — sonraki adım (varsa) veri yokluğu
  nedeniyle sessizce `return` eder.

## Tipik Kullanım — Script'ten Çağrılma (Farklı `FilterMode` Kombinasyonları)

- Konum: `Program.cs`/Console akışının dışında (örn. bir `.csx` script'inde) `StockDataReader`'ı
  doğrudan çağırırken.
- Kapsam: 7 `FilterMode`'un tipik kullanımı — her biri bağımsız, kendi başına çalıştırılabilir
  bir örnek.
- Ortak kurulum: `new StockDataReader()` + `Clear()` + `ReadMetaData(filePath)` (tekrarı
  azaltmak için sadece ilk örnekte gösterildi).

**1) Create StockDataReader object**

```csharp linenums="1"
var reader = new StockDataReader();

reader.Clear();
```
**2) ReadMetaData**

```csharp linenums="1"
reader.ReadMetaData(filePath);
```

**3) All (varsayılan)** — filtre yok, dosyadaki tüm barlar

```csharp linenums="1"
var all = reader.ReadDataFast(filePath);
```

**4) LastN** — son 500 bar

```csharp linenums="1"
var last500 = reader.ReadDataFast(filePath,
    StockDataReader.FilterMode.LastN,
    n1: 500);
```

**5) FirstN** — ilk 1000 bar

```csharp linenums="1"
var first1000 = reader.ReadDataFast(filePath,
    StockDataReader.FilterMode.FirstN,
    n1: 1000);
```

**6) IndexRange** — 200. bar ile 800. bar arası (inclusive)

```csharp linenums="1"
var range = reader.ReadDataFast(
    filePath,
    StockDataReader.FilterMode.IndexRange,
    n1: 200,
    n2: 800);
```

**7) AfterDateTime** — belirli tarihten sonrası

```csharp linenums="1"
var after = reader.ReadDataFast(
    filePath,
    StockDataReader.FilterMode.AfterDateTime,
    dt1: new DateTime(2026, 1, 1));
```

**8) BeforeDateTime** — belirli tarihten öncesi

```csharp linenums="1"
var before = reader.ReadDataFast(
    filePath,
    StockDataReader.FilterMode.BeforeDateTime,
    dt1: new DateTime(2026, 6, 30));
```

**9) DateTimeRange** — iki tarih arası (ikisi de zorunlu)

```csharp linenums="1"
var betweenDates = reader.ReadDataFast(
    filePath,
    StockDataReader.FilterMode.DateTimeRange,
    dt1: new DateTime(2026, 1, 1),
    dt2: new DateTime(2026, 6, 30));
```

## Console Tarafında Aynı 7 Kombinasyon — Kod Yazmadan, `AppConfig.json` ile

Yukarıdaki 7 örnekte `ReadDataFast(...)`'ı **kod yazarak** çağırıyorsun. Console uygulamasını
kullanırken kod yazmıyorsun — bunun yerine gerçek bir dosyayı düzenliyorsun, uygulama arkadan
senin yerine aynı çağrıyı yapıyor:

1. `inputs/configs/AppConfig/AppConfig.json` dosyasını aç.
2. İçindeki `"ReadData"` bölümünü istediğin senaryoyla değiştir (aşağıdaki 7 örnekten biri).
3. Kaydet, Console'u çalıştır (`AlgoTrade.Console`), menüden `[1] Read Data` (veya `[5]`-`[7]`/
   `[23]`/`[25]`) seç.

Örneğin "son 500 bar" için `AppConfig.json`'a şunu yazarsın:

```json linenums="1"
"ReadData": {
    "FilterMode": "LastN",
    "N1": 500,
    "N2": 0,
    "Dt1": "",
    "Dt2": ""
}
```

Arkada `readStockData()` (`Program.cs:610`) bu JSON'u `appConfig.ReadData` nesnesine okuyup
senin yerine tam olarak yukarıdaki 4. örnekle aynı çağrıyı yapar:

```csharp linenums="1"
var cfg = appConfig.ReadData;   // JSON'dan gelen: FilterMode="LastN", N1=500

stockDataReader.ReadDataFast(
    filePath,
    StockDataReader.FilterMode.LastN,   // cfg.FilterMode string'inden parse edildi
    n1: 500,                            // cfg.N1
    n2: cfg.N2,
    dt1: null,                          // cfg.Dt1 boş string olduğu için null
    dt2: null);
```

Yani C# örneği ile JSON örneği **aynı sonucu üreten iki farklı giriş yolu** — biri script'ten
kod yazarak, diğeri Console menüsünden dosya düzenleyerek. Alan adları değişir
(`n1` ↔ `"N1"`, `mode` ↔ `"FilterMode"` vb.) ama mantık (hangi barların seçileceği) birebir
aynı. Aşağıdaki 7 blok, yukarıdaki C# örnekleriyle aynı sırada, her senaryonun
`AppConfig.json` → `"ReadData"` karşılığı:

**Tümü**

```json linenums="1"
{ "FilterMode": "All" }
```

**Son 500 bar**

```json linenums="1"
{ "FilterMode": "LastN", "N1": 500 }
```

**İlk 1000 bar**

```json linenums="1"
{ "FilterMode": "FirstN", "N1": 1000 }
```

**200-800 index aralığı**

```json linenums="1"
{ "FilterMode": "IndexRange", "N1": 200, "N2": 800 }
```

**2026-01-01 sonrası**

```json linenums="1"
{ "FilterMode": "AfterDateTime", "Dt1": "2026-01-01" }
```

**2026-06-30 öncesi**

```json linenums="1"
{ "FilterMode": "BeforeDateTime", "Dt1": "2026-06-30" }
```

**2026-01-01 — 2026-06-30 arası**

```json linenums="1"
{ "FilterMode": "DateTimeRange", "Dt1": "2026-01-01", "Dt2": "2026-06-30" }
```

## Kimler Kullanıyor — Instantiation Noktaları

`new StockDataReader()` için tüm kod tabanında ("`AlgoTrade.Console`", `inputs/scripts/*.csx`,
`AlgoTrade.WinForms`) grep taraması — 15 çağırım noktası, 1'i Console'da, 14'ü script'lerde.
Her satır kendi bağımsız instance'ını yaratıyor (aralarında paylaşılan state yok).

**Console (1 nokta)**

| Dosya | Fonksiyon | Satır |
|---|---|---|
| `AlgoTrade.Console/Program.cs` | `readStockData(ReadDataConfig? cfg = null)` | 620 (fonksiyon 610'da başlıyor) |

**`.csx` Scriptler — kendi `readStockData()` local fonksiyonu olanlar (4 nokta)**

| Dosya | Fonksiyon | Satır |
|---|---|---|
| `inputs/scripts/mainScript.csx` | `readStockData()` | 173 (fonksiyon 163'te başlıyor) |
| `inputs/scripts/mainScriptSimplified.csx` | `readStockData()` | 169 (fonksiyon 159'da başlıyor) |
| `inputs/scripts/mainScriptMultipleTrader.csx` | `readStockData()` | 162 (fonksiyon 152'de başlıyor) |
| `inputs/scripts/mainScriptMultipleTraderSimplified.csx` | `readStockData()` | 174 (fonksiyon 164'te başlıyor) |

**`.csx` Scriptler — üst-seviye akışta (fonksiyon yok, script'in "1. Veri Oku" adımı, 10 nokta)**

| Dosya | Değişken adı | Satır |
|---|---|---|
| `inputs/scripts/01_RunSingleTraderWithProgressAsync.csx` | `stockDataReader` | 110 |
| `inputs/scripts/02_RunMultipleTraderWithProgressAsync.csx` | `stockDataReader` | 40 |
| `inputs/scripts/03_RunSingleTraderOptWithProgressAsync.csx` | `stockDataReader` | 41 |
| `inputs/scripts/04_GenerateDearPyGuiDataPlotterBundle.csx` | `stockDataReader` | 40 |
| `inputs/scripts/06_RunConfirmingSingleTraderWithProgressAsync.csx` | `stockDataReader` | 39 |
| `inputs/scripts/07_RunConfirmingMultipleTraderWithProgressAsync.csx` | `stockDataReader` | 39 |
| `inputs/scripts/CustomConsensusExample.csx` | `reader` | 34 |
| `inputs/scripts/paramSweep.csx` | `reader` | 35 |
| `inputs/scripts/runSingleTraderWithStrategy.csx` | `reader` | 31 |
| `inputs/scripts/runMultiTraderWithStrategies.csx` | `reader` | 26 |

- İki farklı desen var: `mainScript*.csx` dörtlüsü Console'un `readStockData()` deseninin
  aynısını (`OnReadMetaData`/`OnReadData`/`OnProgress` event bağlama dahil) kopyalıyor; diğer
  10 script daha sade — event bağlamadan, doğrudan `ReadMetaData()` → `IsMetaDataRead` kontrolü
  → `ReadDataFast()` sırasıyla top-level akışta çalışıyor.
- Değişken adı **`stockDataReader`** (11 dosya, Program.cs'inkiyle aynı isim ama **ayrı, kendi
  yerel instance'ı** — global değil) veya **`reader`** (4 dosya) olarak ikiye ayrılıyor; bu
  ayrım [Kullanım Haritası](#kullanım-haritası)'nın ilk taramasında gözden kaçmıştı, bu tablo
  onu düzeltiyor.
- `06_`/`07_` numaralı scriptlerin içindeki `Log("=== ... ===")` mesajları dosya adıyla
  uyuşmuyor (`06_` dosyası "05_..." yazıyor, `07_` dosyası "06_..." yazıyor) — muhtemelen
  scriptler numaralandırılırken kopyala-yapıştır kalıntısı, işlevi etkilemiyor ama kafa
  karıştırabilir.

## Kullanım Haritası

`StockDataReader`'ın (kendi + `MarketDataProvider`'dan miras) tüm public üyelerinin
`Program.cs`, `inputs/scripts/*.csx` ve `AlgoTrade.WinForms` genelinde taranmasıyla çıkan
sonuç — hangi metoda güvenle dokunulabileceğini (✅), hangisinin sadece örnek/yorum satırında
kaldığını (⚠️), hangisinin hiç çağrılmadığını (❌) gösterir:

| Üye | Durum | Nerede |
|---|---|---|
| `Clear`, `ReStartTimer`, `ReadMetaData`, `StopTimer`, `GetElapsedTimeMsec`, `IsMetaDataRead`, `ReadDataFast`, `GetData()`, `GetDataCount`, `Head`, `Tail` | ✅ | `readStockData()` (`Program.cs:610-692`, yukarıda tam kaynağıyla var) |
| `OnReadMetaData`/`OnReadData`/`OnProgress` (event'ler) | ✅ | `readStockData()`'da bağlanıyor, gövdeleri `Program.cs:64-96` (yukarıda) |
| `GetMetaData()` | ✅ | `OnReadMetaData` callback'i içinde `sender.GetMetaData()` |
| `IsDataReady` | ✅ | Her `runXxxAlgoTrade()`'in başında guard (`Program.cs:765,840,898,959,1020`) |
| `GetData()` (ikinci kullanım) | ✅ | `algoTrader.SetData(stockDataReader.GetData())` — her `runXxxAlgoTrade()`'de |
| `WriteToCsvFile`/`WriteToTxtFile`/`GetData(start, end)` | ⚠️ | Sadece `mainScript.csx:377-412`'de **10 satır yorum halinde örnek** — hiç çalıştırılmıyor (`GetData(200, 299)` overload'ı da orada, sadece o yorum bloğunda geçiyor) |
| `GetMetaDataCount()` | ❌ | Hiçbir yerde çağrılmıyor |
| `GetMetaDataLines()` / `GetMetaDataLinesCount()` | ❌ | Hiçbir yerde çağrılmıyor (ham `#` satırları dışarı açılmış ama tüketilmiyor) |
| `StartTimer()` | ❌ | Hiç kullanılmıyor — her yerde `ReStartTimer()` tercih edilmiş |
| `GetElapsedTime()` (TimeSpan) | ❌ | Hiç kullanılmıyor — her yerde `GetElapsedTimeMsec()` tercih edilmiş |
| `ToTable()` / `ToTable(start, end)` | ❌ | Hiçbir yerde çağrılmıyor |
| `Dispose()` | ✅ (sadece script'lerde) | `Program.cs`'in kendi `stockDataReader`'ı (uygulama ömrü boyunca yaşayan) hiç `Dispose()` edilmiyor — ama neredeyse tüm `.csx` scriptler (`mainScript.csx`, `mainScriptSimplified.csx`, `mainScriptMultipleTrader(Simplified).csx`, `01`-`04`/`06`-`07` numaralı scriptler, `CustomConsensusExample.csx`, `paramSweep.csx`, `runSingleTraderWithStrategy.csx`, `runMultiTraderWithStrategies.csx` — 13 dosya) script sonunda kendi `StockDataReader` instance'ını (`stockDataReader` veya `reader` adıyla) `Dispose()` ediyor |
| `Data` (property, `MarketDataProvider`) | ❌ | `StockDataReader` üzerinden hiç okunmuyor — her yerde `GetData()` metodu tercih edilmiş (ikisi de aynı listeyi döner) |
| `IsInitialized` / `IsDataRead` (`IsDataReady` ile aynı şeyi döner, `MarketDataProvider`) | ❌ | Sadece `IsDataReady` alias'ı kullanılıyor, bu ikisi hiç çağrılmıyor |
| `GetLastBarIndex()` / `LastBarIndex` (`MarketDataProvider`) | ❌ | `StockDataReader` üzerinden hiç çağrılmıyor |
| `GetDataRange()` (MarketDataProvider) | ❌ | `StockDataReader` üzerinden hiç çağrılmıyor (`GetDataInfo()` benzeri çağrılar `algoTrader` üzerinden, farklı instance) |
| `GetClosePrices/GetOpenPrices/GetHighPrices/GetLowPrices/GetVolume/GetLotSizes/GetDateTimes/GetDates/GetTimes/GetEpochTimes` (MarketDataProvider) | ❌ | `StockDataReader` üzerinden hiç çağrılmıyor |

Not: `AlgoTrade.WinForms` projesi `StockDataReader`'a hiç dokunmuyor — yukarıdaki tarama
sadece `AlgoTrade.Console` + `.csx` script'leri kapsıyor.

---

## İlgili Dosyalar

- [01-class-reference.md](../01-class-reference.md) — diğer 8 sınıf (§1-§8) ve bu sayfanın
  ait olduğu index.
- [02-console-menu-guide.md](../02-console-menu-guide.md) — Console menü rehberi, `[1] Read
  Data` ve "Read Data + X" kombo menülerinin tam haritası.
