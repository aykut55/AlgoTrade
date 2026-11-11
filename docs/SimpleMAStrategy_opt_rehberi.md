# SimpleMAStrategy ile Optimizasyon Rehberi

Bu not `inputs/scripts/Config_03_SingleTraderOpt.csx` + `03_RunSingleTraderOptWithProgressAsync.csx`
üzerinden SimpleMAStrategy optimizasyonu için toplanmış pratik bilgidir.

---

## 1. Nereyi değiştireceğim?

Tek dosya: **`inputs/scripts/Config_03_SingleTraderOpt.csx`**

| Satır | Alan | Ne yapılır |
|---|---|---|
| ~169 | `int optChoice = 1;` | `1` = SimpleMAStrategy bloğu (zaten hazır) |
| ~197-210 | `optChoice == 1` bloğu | `optimizationRanges` + `fixedParams` burada düzenlenir |
| ~57 | `stockDataFullFileName` | Test edilecek veri dosyası |
| ~67 | `readDataChoice` | Veri penceresi (0=All, 4=AfterDateTime, ...) |

Ana script (`03_RunSingleTraderOptWithProgressAsync.csx`) bu config'i `#load` ile alır.
Başka dosyaya dokunmak gerekmez. Çalıştırma:

```
dotnet run --project AlgoTrade.Console
> 8            (Run Script)
> [boş ENTER]
> listeden 03_RunSingleTraderOptWithProgressAsync.csx numarası
```

İptal: `ESC`. Sonuçlar `outputs/` klasörüne:
- `singleTraderOptLog.csv` / `.txt` — tüm kombinasyonlar
- `singleTraderOptLog_sorted.csv` / `.txt` — `GetiriFiyatNet`'e göre en iyiden kötüye

---

## 2. SimpleMAStrategy constructor parametreleri

`src/AlgoTrade.Core/Trading/Strategies/SimpleMAStrategy.cs:82`

```
fastPeriod=10, slowPeriod=20,
fastMaMethod=SIMPLE, slowMaMethod=SIMPLE,   // MAMethod enum
priceSource=Close,
signalModeIndex=0, exitModeIndex=0, flatModeIndex=0, skipModeIndex=0, ruleModeIndex=0
```

Registry key'leri **isimle** eşler (case-insensitive), sıra önemsiz.
MA method string olarak verilebilir: `"EMA"` / `"SMA"` / `"WMA"` (Enum.Parse) veya
enum indeksi: `0=SIMPLE(SMA)`, `1=EMA`, `2=WMA`.

`signalModeIndex` (SimpleMAStrategy):
- `0` Fast/Slow MA kesişimi (klasik)
- `1` Fiyat–FastMA kesişimi
- `2` SlowMA slope flip
- `3` Fast/Slow MA state (koşul sürdükçe her bar)
- `4` band/mesafe · `5` breakout+retest · `6` confirmation bars · `7` slope+state combo

---

## 3. Örnek: MA tiplerini sabitleyip sadece periyot taramak

MA method'ları taramak istemiyorsan (örn. ikisi de EMA) → range'den çıkar, `fixedParams`'a koy:

```csharp
else if (optChoice == 1)
{
    optimizationStrategyName = "SimpleMAStrategy";
    optimizationRanges = new List<(string name, double min, double max, double step)>
    {
        ("fastPeriod",  3, 24, 3),
        ("slowPeriod", 12, 72, 6),
    };
    fixedParams = new Dictionary<string, object>
    {
        ["fastMaMethod"]    = "EMA",
        ["slowMaMethod"]    = "EMA",
        ["priceSource"]     = "Close",
        ["signalModeIndex"] = 0
    };
}
```

---

## 4. Range seçimi — genel kural

- `fastPeriod < slowPeriod` mantıklı olan. Strateji `fast >= slow`'da çökmez ama ters/gürültü
  sonuç üretir; `singleTraderOptLog_sorted.csv`'de dibe düşerler → üstten okursun, sorun olmaz.
- Örtüşmeyi tamamen istemiyorsan `slowPeriod` min'ini `fastPeriod` max'ından büyük seç:
  `fast 5→100`, `slow 105→600` → hiç saçma kombinasyon üretilmez (kod değişikliği gerekmez).
- Kod seviyesinde guard (`OnInit`'te `if (fastPeriod >= slowPeriod) return;`) **önerilmez**:
  manuel/tekli koşuda bilerek ters kurulum test etmeyi sessizce öldürür. Kısıt opt'a aittir,
  stratejiye değil → range tasarımıyla çöz.
- Ortak factory'ye (`SetOptimizationStrategyFactory` lambda) koşul yazmak **yanlış katman**:
  her strateji için çalışır, `merged["slowPeriod"]` olmayan stratejilerde `KeyNotFoundException`,
  ayrıca factory strateji nesnesi döndürmek zorunda ("atla" diyemez).

---

## 5. Veri: VIP-X030-T 5dk

`C:\data\csvFiles\VIP\05\VIP-X030-T.csv`
- BarCount: **541.144**, aralık 2007.06.11 – 2026.03.27, periyot 5
- 19 yıllık veri → tüm aralıkta optimize etmek rejim bulamacı verir. Son 4-5 yıl daha temiz:

```csharp
int readDataChoice = 4;                       // AfterDateTime
readDataDt1 = "2021.01.01 00:00:00";
```

### 600 periyot büyük mü?
- Warmup açısından önemsiz (541 binin %0,1'i).
- Stil açısından: 5dk seansı ~105-150 bar → **600 bar ≈ 4-6 işlem günü ≈ ~1 hafta**.
  Yavaş MA olarak meşru, hızlı MA olarak saçma. Çok yavaş MA → az kesişim → az işlem →
  istatistik gürültüye döner (Trades sütununa bak, <30-50 ise o satır anlamsız).

---

## 6. Gün-içi (intraday) senaryo: 10:00–18:00, gecelik tutma yok

**5dk'da 10:00–18:00 = 8 saat = 480 dk = 96 bar/gün.**

### MA aralığı
- `slowPeriod` **mutlak tavan ≈ 96** (bir tam seans). Üstü = MA bir günden uzun süreyi
  kapsar → gecelik taşımaya zorlar.
- Pratik: `slowPeriod` **20–60** (≈ 100 dk – 5 saat) → günde birkaç sinyal.
- `fastPeriod` **3–20** (15–100 dk), tipik 3–12.

Önerilen:
```csharp
("fastPeriod",  3, 24, 3),    // 15dk – 2 saat   → 8 değer
("slowPeriod", 12, 72, 6),    // 1 saat – 6 saat → 11 değer
```
→ 88 kombinasyon. Daha ince: `fast 2→20/2` × `slow 15→90/5` → 160 kombinasyon.

### Gün-içi için şart olan Config_03 ayarları
```csharp
bool gunSonuPozKapatEnabled = true;   // varsayılan false
```
Asıl "gecelik tutma kapatma" anahtarı — seans sonunda açık pozisyonu kapatır
(`ClosePositionEOD`). Bu olmadan gün sonunda gelen "AL" günlerce taşınır.

```csharp
bool timeFilteringEnabled = true;
string signalsStartDateTime = "2021.01.01 10:00:00";   // saat kısmı = günlük pencere başı
string signalsStopDateTime  = "2026.03.27 18:00:00";   // saat kısmı = günlük pencere sonu
```
`TimeFilteringEnabled` + datetime'ların **saat** kısmı günlük 10:00–18:00 penceresini kurar
(tarih kısmı genel test aralığı); pencere dışında flat'e zorlar
(`FilterMode==1`, `SingleTrader.cs:2144-2166`).

> Opt script'inin `timeFiltering`'i tam onurlandırdığını tek kombinasyonluk deneme koşusuyla
> doğrula (`optimizationTo = 1`): log'da işlemler sadece 10:00–18:00 arasında açılmalı.

---

## 7. İki aşamalı optimizasyon (önerilen yöntem)

1. **Kaba tarama:** `fast {10,20,40,60,80,100}`, `slow {50,100,150,200,300,400}` (ya da gün-içi
   için daha dar) — birkaç dakika. En iyi bölgeyi bul.
2. **İnce tarama:** kazanan bölge etrafında adım 5 (veya 2) ile daralt.

---

## 8. PartialOpt / durdurup devam ettirme

Otomatik "kaldığı yerden devam" **yok**. Manuel mekanizma: `optimizationFrom`/`optimizationTo`
(`Config_03` ~614-615) + `appendEnabled` (~145, zaten `true`).

- `from`/`to` **1-tabanlı ve her iki uç dahil**. `from=5, to=10` → 5,6,7,8,9,10 (6 kombinasyon).
- `-1` = o uçtan sınırsız.
- csv'deki `CombNo` (`#`) sütunu = `currentCombination`. Devam ederken:
  **`optimizationFrom = son_yazılan_CombNo + 1`**, `optimizationTo = -1`.
- `appendEnabled = true` şart: yeni satırlar mevcut csv/txt sonuna eklenir, dosya sıfırlanmaz.
  `false` → devam eden koşu ilk yazımda dosyayı siler, önceki kısım kaybolur.
- Devam ederken `optChoice`, range'ler (min/max/step), parametre listesi **değişmemeli** —
  kombinasyon listesi aynı sırayla yeniden üretiliyor, değişirse `#N` başka parametreye kayar.
- `WriteSortedFiles()` ilk çağrıda mevcut csv'yi okuyup cache'e alır, sonra yeni satırları ekler
  → devam koşusundan sonra `_sorted.csv` iki koşunun tümünü birlikte sıralı içerir
  (`appendEnabled=true` ve eski csv duruyorsa).
- `optimizationFrom`'u fazla küçük verme (örtüşme) → csv'de tekrar eden `CombNo`, sorted çift sayar.

---

## 9. Sonuç sütunları (opt log başlıkları)

`Statistics.GetOptimizationProgressHeader` / `...ProgressLine`

| Sütun | Kod alanı | Anlam |
|---|---|---|
| Net Score | `ScoreFiyatNet` | `Net Return % / MaxDD` — riske (drawdown'a) göre düzeltilmiş |
| Net Return % | `GetiriFiyatYuzdeNet` | Komisyon **düşülmüş** getiri % |
| Gross Return % | `GetiriFiyatYuzde` | Komisyon **düşülmemiş** ham getiri % |
| Score Return % | `GetiriPuanYuzde` | "Puan" bazlı getiri % (bakiye/fiyat değil, ham puan hareketi) |
| Trades | `IslemSayisi` | İşlem sayısı |
| Win% | `KarliIslemOrani` | Kârlı işlem oranı |
| PFNet | `ProfitFactorNet` | Net profit factor |
| MaxDD | `GetiriMaxDD` | Maksimum drawdown |
| Comm Count / Commission | `KomisyonIslemSayisi` / `KomisyonFiyat` | Komisyonlu işlem sayısı / toplam komisyon |

### Net vs Gross (`SingleTrader.cs:2037-2045`)
```csharp
double k = KomisyonCarpan != 0 ? 1.0 : 0.0;
GetiriFiyatNet      = GetiriFiyat − KomisyonFiyat * k;      // komisyon çıkar
GetiriFiyatYuzde    = 100 * GetiriFiyat    / İlkBakiye;     // Gross %
GetiriFiyatYuzdeNet = 100 * GetiriFiyatNet / İlkBakiye;     // Net %
```
- **Gross** = SonBakiye − İlkBakiye, komisyonsuz.
- **Net** = Gross − ödenen toplam komisyon.
- Fark = "Commission" sütunu. `KomisyonCarpan = 0` ise Net == Gross.
- Config_03 `sortField = "GetiriFiyatNet"` → sıralı dosya **net getiriye** göre.
  Komisyonu ciddiye alıyorsan doğru kolon **Net Return % / Net Score**.

---

## 10. MAMethod listesi (enum indeksi ile)

`src/AlgoTrade.Core/Trading/Indicators/Base/MAMethod.cs` — enum, açık `= N` yok, indeks
bildirim sırasına göre 0'dan başlar. `fastMaMethod` / `slowMaMethod` alanına ya bu sayı ya da
enum adı string olarak verilir (`Enum.Parse`, case-insensitive; `SIMPLE` = `"SMA"` değil,
string olarak `"SIMPLE"` beklenir — ama tarama genelde `0/1/2` indeksiyle yapılır).

> Pratikte güvenli / yaygın olanlar: **0 SIMPLE, 1 EMA, 2 WMA** (Config_03 örnekleri bunları
> `0,2,1` aralığıyla tarar). Diğerleri `MovingAverageCalculator.Calculate()` implementasyonuna
> bağlı — kullanmadan önce tek kombinasyonla deneyip 0-işlem/NaN çıkmadığını doğrula.
> `VIDYA` (12) gibi bazıları ayrı parametre/deneme ister.

### Basic MAs
| # | Ad | Açıklama |
|---|---|---|
| 0 | SIMPLE | Simple Moving Average (SMA) |
| 1 | EMA | Exponential Moving Average |
| 2 | WMA | Weighted Moving Average |
| 3 | HULL | Hull Moving Average |
| 4 | TRIANGULAR | Triangular Moving Average |
| 5 | VWMA | Volume Weighted Moving Average |
| 6 | WILDER | Wilder's Smoothing (RSI'de kullanılır) |
| 7 | SMMA | Smoothed Moving Average |

### Advanced MAs
| # | Ad | Açıklama |
|---|---|---|
| 8 | DEMA | Double Exponential MA |
| 9 | TEMA | Triple Exponential MA |
| 10 | LSMA | Least Squares MA (Linear Regression) |
| 11 | KAMA | Kaufman's Adaptive MA |
| 12 | VIDYA | Variable Index Dynamic Average |
| 13 | ZLEMA | Zero-Lag Exponential MA |
| 14 | T3 | Tillson T3 MA |
| 15 | ALMA | Arnaud Legoux MA |
| 16 | JMA | Jurik MA |

### Exotic MAs
| # | Ad | Açıklama |
|---|---|---|
| 17 | FRAMA | Fractal Adaptive MA |
| 18 | MAMA | MESA Adaptive MA |
| 19 | MCGINLEY | McGinley Dynamic |
| 20 | VAMA | Volatility Adjusted MA |

### Compound MAs
| # | Ad | Açıklama |
|---|---|---|
| 21 | DHULL | Double Hull MA |
| 22 | THULL | Triple Hull MA |
| 23 | DZLEMA | Double ZLEMA |
| 24 | TZLEMA | Triple ZLEMA |
| 25 | DSMMA | Double SMMA |
| 26 | TSMMA | Triple SMMA |
| 27 | DSMA | Double SMA |
| 28 | TSMA | Triple SMA |
| 29 | DWMA | Double WMA |
| 30 | TWMA | Triple WMA |
| 31 | DVWMA | Double VWMA |
| 32 | TVWMA | Triple VWMA |

### Statistical MAs
| # | Ad | Açıklama |
|---|---|---|
| 33 | MEDIAN | Median Moving Average |
| 34 | GMA | Geometric Moving Average |
| 35 | ZSMA | Zero-Lag Simple MA |

### Specialized MAs
| # | Ad | Açıklama |
|---|---|---|
| 36 | TIME_SERIES | Time Series Moving Average |
| 37 | COVWMA | Coefficient of Variation Weighted MA |
| 38 | COVWEMA | Coefficient of Variation Weighted EMA |
| 39 | FAMA | Following Adaptive MA |
| 40 | SRWMA | Square Root Weighted MA |
| 41 | SWMA | Symmetrically (Sine-) Weighted MA |
| 42 | EVWMA | Elastic Volume Weighted MA |
| 43 | REGMA | Regularized Exponential MA |
| 44 | REMA | Range EMA |
| 45 | REPMA | Repulsion MA |
| 46 | RSIMA | RSI-based MA |
| 47 | ETMA | Exponential Triangular MA |
| 48 | TREMA | Triangular Exponential MA |
| 49 | TRSMA | Triangular Simple MA |
| 50 | THMA | Triple Hull MA (alternatif) |

### Advanced Exotic MAs
| # | Ad | Açıklama |
|---|---|---|
| 51 | ADEMA | Alpha-Decreasing EMA |
| 52 | EDMA | Exponentially Deviating MA |
| 53 | EDSMA | Ehlers Dynamic Smoothed MA |
| 54 | AHMA | Ahrens MA |
| 55 | EHMA | Exponential Hull MA |
| 56 | ALSMA | Adaptive Least Squares MA |
| 57 | AARMA | Adaptive Autonomous Recursive MA |
| 58 | MCMA | McNicholl MA |
| 59 | LEOMA | Leo MA |
| 60 | CMA | Corrective MA |
| 61 | CORMA | Correlation MA |
| 62 | AUTOL | Auto-Line |
| 63 | XEMA | Optimized Exponential MA |
| 64 | DSSMA | Double SSMA |
| 65 | TSSMA | Triple SSMA |

### Opt'a dahil etmeye değer 4-5 method (gün-içi crossover, 5dk)

| # | Method | Neden |
|---|---|---|
| 1 | **EMA** | Standart, düşük gecikme, öngörülebilir. Intraday crossover'ın varsayılanı. |
| 2 | **WMA** | EMA'dan daha tepkisel (öne ağırlıklı), daha erken sinyal. |
| 3 | **HULL** | Çok düşük gecikme + pürüzsüz → whipsaw'ı ciddi azaltır. Trend takip için güçlü aday. |
| 13 | **ZLEMA** | Zero-lag ailesi, TEMA'ya göre daha az overshoot, sağlam. |
| 11 | **KAMA** | Adaptif: trendde hızlı, yatayda yavaş. Yatay seans whipsaw'ından korur. (Parametre hassasiyeti daha yüksek.) |

- **İlk tarama:** EMA + WMA + HULL. **İkinci tur:** ZLEMA, istersen KAMA.
- `AddOptimizationParameterRange` sadece **ardışık** aralık tarar, tek tek indeks seçilemez:
  - Ardışık band kolay: `("fastMaMethod", 0, 3, 1)` → SIMPLE, EMA, WMA, HULL.
  - ZLEMA (13) / KAMA (11) ardışık değil → ayrı opt koşularında `fixedParams`'ta sabitle,
    `optimizationRanges`'ten `fastMaMethod`/`slowMaMethod`'u çıkar:
    ```csharp
    fixedParams = new Dictionary<string, object> {
        ["fastMaMethod"] = "ZLEMA", ["slowMaMethod"] = "ZLEMA",
        ["priceSource"] = "Close", ["signalModeIndex"] = 0
    };
    ```
- `fastMaMethod` × `slowMaMethod` birlikte taranırsa kombinasyon **kare** artar (4×4 = 16× period
  gridi). Genelde `fastMaMethod == slowMaMethod` tutmak (iki ayrı koşu) daha anlamlı.
- Exotic/adaptif method'lardan önce (JMA, ALMA, VIDYA, MAMA, FRAMA...) tek kombinasyonla dene:
  `MovingAverageCalculator.Calculate()` implementasyonu eksikse 0-işlem / NaN çıkar.

---

## 11. Kurgu: sabit 2000 TL kâr-al + gün sonu poz kapat + "en çok sinyal" opt

Hedef sorusu: **"Bir günde belli bir TL kazancı sağlayan, en çok işlem üreten fast/slow
kombinasyonları hangileri?"**

### 11.1 Çıkış mantığı — SimpleMAStrategy `exitModeIndex`

`SimpleMAStrategy.cs:329-405`. Eşik değerleri **strateji içinde hardcoded**, ctor parametresi
değil — istediğin değeri kod'da değiştirirsin.

| exitModeIndex | Kâr-al metodu | Anlamı | Hardcoded eşik (satır) |
|---|---|---|---|
| 0 | `SonFiyataGoreKarAlSeviyeHesaplaSeviyeli` | Seviye, seviyeli | `5,50,1000` (334) |
| 1 | `SonFiyataGoreKarAlYuzdeHesaplaSeviyeli` | Yüzde, seviyeli | `2,10,0.01` (340) |
| 2 | `SonFiyataGoreKarAlSeviyeHesapla` | Giriş fiyatından +N puan | `2000.0` (346) |
| 3 | `SonFiyataGoreKarAlYuzdeHesapla` | Giriş fiyatından +%N | `2.0` (352) |
| **4** | **`KarZararFiyatSeviyesindenKarAlHesapla`** | **Anlık P&L (TL) ≥ eşik → kapat** | **`1000.0` (358)** |
| 5 | `KarZararYuzdesindenKarAlHesapla` | Anlık P&L %'si ≥ eşik | `3.0` (364) |

**"2000 TL kâr edince kapat" = exitModeIndex 4.** `KarAlZararKes.cs:432`:
`if (anlikKarZararFiyat >= karAlFiyatSeviyesi)` — `anlikKarZararFiyat = KarZararFiyatList[i]`
(TL cinsinden anlık kâr/zarar). `KarAlSeviyeHesaplaEnabled` ilk işlemden sonra otomatik `true`
olur (`SingleTrader.cs:1510-1517`), ekstra ayar gerekmez.

Eşiği 2000 yapmak için `SimpleMAStrategy.cs:358`:
```csharp
takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(currentIndex, 2000.0) != 0;
```

### 11.2 Gün sonu poz kapat

`Config_03`:
```csharp
bool gunSonuPozKapatEnabled = true;   // varsayılan false
```
`ClosePositionEOD` (`SingleTrader.cs:2384`) sabit saat değil, **gün değişimine** bakar
(`Data[i].Date != Data[i+1].Date`) → her işlem gününün son barında pozu zorla kapatır.
"Hard override" — MA sinyalini de TP'yi de ezer (`ResolveFilterDecisions`, Öncelik 2).
Sonuç: her işlem aynı gün kapanır (ya 2000 TL TP ya EOD). Net getiriyi bir miktar **düşürür**
(zorla çıkışlar küçük zarar + komisyon realize eder) ama intraday için gerçekçi.

### 11.3 signalModeIndex seçimi — frekansı bu belirler

- `signalModeIndex == 0` (sadece kesişim): TP frekansı **artırmaz**. TP kazancı erken kapatır,
  yeni giriş yine bir sonraki kesişimi bekler → `IslemSayisi` ≈ TP'siz hâliyle aynı.
- `signalModeIndex == 3` (state: `fastMA > slowMA` sürdükçe her bar AL): TP ile poz kapanır →
  bir sonraki bar koşul hâlâ geçerliyse **tekrar girer** → gerçek yüksek frekanslı
  "2000 TL scalp" davranışı. **Çok sinyal istiyorsan bu.**

### 11.4 Config_03 ayarları (özet)

```csharp
int optChoice = 1;                     // SimpleMAStrategy

// optChoice == 1 bloğu:
optimizationRanges = new List<(string name, double min, double max, double step)>
{
    ("fastPeriod",  5, 30, 5),         // dejenere bölgeden uzak (2/3 gibi çöp yok)
    ("slowPeriod", 15, 90, 5),
};
fixedParams = new Dictionary<string, object>
{
    ["priceSource"]     = "Close",
    ["signalModeIndex"] = 3,           // yüksek frekans için state
    ["exitModeIndex"]   = 4            // anlık P&L (TL) TP — eşik kod'da 2000.0
};

// Sinyaller:
bool karAlEnabled          = true;    // zaten true
bool zararKesEnabled       = false;   // sadece TP; stop yok — TEST KOŞUSUYLA DOĞRULA
bool gunSonuPozKapatEnabled = true;

// Sıralama: en çok işlem üste
string sortField = "IslemSayisi";

// Veri penceresi: son birkaç yıl
int readDataChoice = 4;
readDataDt1 = "2021.01.01 00:00:00";
```

### 11.5 "Günde belli bir TL kazanç" filtresini uygulamak

Opt **tek alanla** sıralar (`sortField`), çok kriterli değil. "Günlük TL kazanç" doğrudan
kolon da değil (`OptimizationResultsMap`'te sadece toplam `GetiriFiyatNet` var). Ama test
penceresindeki **işlem günü sayısı (N) tüm satırlar için sabit**, yani:

```
günlük net ≈ GetiriFiyatNet / N
günlük net ≥ hedef   ⟺   GetiriFiyatNet ≥ hedef × N
```

**Adımlar:**
1. Opt'u yukarıdaki ayarlarla çalıştır (`sortField = "IslemSayisi"`).
2. `N` = pencerede kaç işlem günü var → veriden say (pencere bar sayısı ÷ ~96 bar/gün,
   10:00–18:00 için) veya kabaca yıl×252.
3. `singleTraderOptLog_sorted.csv`'yi aç, tek eşik uygula:
   `GetiriFiyatNet >= hedef_TL_gün × N`
   (örn. 500 TL/gün, N≈1250 → `GetiriFiyatNet >= 625000`).
4. Bu eşiği geçen satırlar zaten `IslemSayisi`'ne göre sıralı → **en üstteki = cevap**
   (o günlük kazancı sağlayan, en çok işlem üreten fast/slow ikilisi).

### 11.6 Sağlık kontrolleri (sorted CSV'de)

- **Gross Return % > 0** olmayan satırları ele — negatif beklentili base'i filtre kurtarmaz.
- **Win% / PFNet** aşırı uçta mı? Çok küçük periyot → sürekli state true → her bar gir/çık →
  `IslemSayisi` şişer ama komisyonla net çöker. `PFNet >= 1` süz.
- **MaxDD** kabul edilebilir mi? Yüksek frekans + tek yönlü stop yok → tek kötü günde büyük DD.
- `IslemSayisi` çok yüksek satırlarda **komisyon (KomisyonFiyat)** toplamına bak — `GetiriFiyat`
  (gross) ile arasındaki fark net'i yiyor mu?
