# AlgoTrade - Migration Guide (Eski Proje -> Yeni Proje)

## Oturum Bilgisi
Bu dosya, eski projedeki kodlarin yeni projeye tasinmasi icin referans belgesidir.
Yeni oturumda Claude'a "D:\Aykut\Projects\AlgoTrade\MIGRATION_GUIDE.md dosyasini oku ve kaldigimiz yerden devam et" denilmesi yeterlidir.

## Proje Yollari

| Proje | Yol |
|-------|-----|
| **Eski (Kaynak)** | `D:\Aykut\Projects\AlgoTradeWithOptimizationSupport\AlgoTradeWithOptimizationSupportWinFormsApp\AlgoTradeWithOptimizationSupportWinFormsApp\src\Trading\` |
| **Yeni (Hedef)** | `D:\Aykut\Projects\AlgoTrade\src\AlgoTrade.Core\Trading\` |

## Agent Yapisi

- **"Demir"**: Eski projeyi okuyan agent. Eski dosyalari acar, okur ve farkliliklari raporlar.
- **Claude (ana)**: Yeni projeyi yonetir. Demir'den gelen farkliliklari yeni projeye uygular.
- Kullanici "Demir'e sor", "Demir'den iste" dediginde eski projeyi okuyan agent calistirilir.

## Calisma Yontemi

1. Demir eski projedeki ilgili method/class'i okur
2. Claude yeni projedeki karsiligini okur
3. Farkliliklar listelenir
4. Kullanici onayiyla farkliliklar adim adim yeni projeye aktarilir

---

## RunSingleTraderWithProgressAsync() Karsilastirmasi

### Dosya Konumlari
- **Eski**: `...\AlgoTradeWithOptimizationSupportWinFormsApp\src\Trading\AlgoTrader.cs` (satir 1020-1278)
- **Yeni**: `D:\Aykut\Projects\AlgoTrade\src\AlgoTrade.Core\Trading\AlgoTrader.cs` (satir 141-310)

### Kalan Farkliliklar (BEKLIYOR)

#### 2. Strategy Configuration (StrategyFactory)
- **Eski**: `StrategyFactoryMethod` null kontrolu, default `SimpleMAStrategy` olusturma,
  `SetStrategyFactory`, `strategy.OnInit()`, `singleTrader.SetStrategy(strategy)` akisi var (satir 1049-1110)
- **Yeni**: Bu kisim tamamen YOK. Strateji siniflari ve factory pattern henuz yeni projeye tasinmadi.
- **Durum**: BEKLIYOR
- **Yapilacak**: Yeni projeye IStrategy interface'i, StrategyFactory mekanizmasi ve en az bir
  ornek strateji (SimpleMAStrategy vb.) tasinacak. AlgoTrader'a `SetStrategyFactory()` ve
  `StrategyFactoryMethod` eklenecek. SingleTrader'a `SetStrategy()` cagrisi eklenecek.
  Console menusune strateji secimi eklenebilir.

#### 5. Symbol/System/Strategy attributes
- **Eski**: `SymbolName`, `SymbolPeriod`, `SystemId`, `SystemName`, `StrategyId`, `StrategyName`
  AlgoTrader'dan SingleTrader'a atanir (satir 1126-1131)
- **Yeni**: AlgoTrader'a `#region Properties` icinde eklendi, comment acildi
- **Durum**: TAMAM

---

## CreateModules() Karsilastirmasi (TAMAMLANDI)

### Dosya Konumlari
- **Eski**: `...\Traders\SingleTrader.cs` (satir 453-475)
- **Yeni**: `...\Trading\Traders\SingleTrader.cs` (satir 334-375)

### Sonuc
| Modul | Durum | Aciklama |
|-------|-------|----------|
| `initialTradeParams` | YENI | Eski `pozisyonBuyuklugu` yerine eklendi |
| `signals` | AYNI | |
| `status` | AYNI | |
| `flags` | AYNI | |
| `lists` | AYNI | |
| `timeUtils` | AYNI | |
| `karZarar` | AYNI | |
| `karAlZararKes` | AYNI | |
| `komisyon` | KALDIRILDI | Artik class degil, method ile hesaplaniyor |
| `Bakiye` / `bakiye` | KALDIRILDI | Artik class degil, method ile hesaplaniyor |
| `pozisyonBuyuklugu` | KALDIRILDI | `initialTradeParams` ile degistirildi |
| `Position` | KALDIRILDI | Eski projede de kullanilmiyordu |
| `statistics` | AYNI | Namespace guncellendi |

---

## ResetModules() / InitModules() / DeleteModules() Karsilastirmasi (TAMAMLANDI)

### Sonuc
Uc method da tutarli. Kaldirilan moduller (komisyon, Bakiye, pozisyonBuyuklugu, Position) beklenen sekilde eksik.
- **ResetModules()**: AYNI (kaldirilan moduller haric)
- **InitModules()**: AYNI (kaldirilan moduller haric)
- **DeleteModules()**: Yeni projede sadeleştirilmis (null atama). `ClearCallbacks()` eklendi (memory leak onlemi).

### Kaldirilan Moduller (uc method icin ortak)
| Modul | Sebep |
|-------|-------|
| `komisyon` | Artik class degil, method ile hesaplaniyor |
| `Bakiye` / `bakiye` | Artik class degil, method ile hesaplaniyor |
| `pozisyonBuyuklugu` | `initialTradeParams` ile degistirildi |
| `Position` | Kullanilmiyordu |

---

## Reset() / Init() Karsilastirmasi (TAMAMLANDI)

### Reset()
- Data null check: eklendi, TAMAM
- `OnReset(0)` sirasi, `CurrentIndex = 0` comment out, `OnApplyUserFlags` comment out: BILINCLI farklar, dokunulmayacak
- `ExecutionStepNumber/BakiyeInitialized` reset: buraya tasinmis, TAMAM
- `ResetConfirmationMode()`: KAPSAM DISI — Getiri Egrisi kullanimi icin tasarlanmisti.
  Amac: SingleTrader strateji emirlerini once SANAL olarak calistirir. Ornegin strateji
  "AL" dediyse, gercek emir uretmek yerine sanal al yapilir ve izlenir. Eger sanal islem
  belirli bir zarar esigine duserse gercek giris yapilir (zarar konfirmasyonu). Ya da sanal
  islem kar ediyorsa (emir dogru cikmis) gercek emri uretir (kar konfirmasyonu). Ayni mantik
  SELL icin de gecerli. Bu ozellik ileride yeni projede de implement edilebilir.

### Init()
- Data null check: eklendi, TAMAM
- Geri kalan: AYNI

---

## Tamamlanan Degisiklikler

### Namespace Degisiklikleri
| Eski | Yeni |
|------|------|
| `AlgoTradeWithOptimizationSupportWinFormsApp.Trading.Traders` | `AlgoTrade.Core.Trading` |
| `AlgoTradeWithOptimizationSupportWinFormsApp.Trading.Statistics` | `AlgoTrade.Core.Trading.Statistics` |

### Isim Degisiklikleri
| Eski | Yeni |
|------|------|
| `pozisyonBuyuklugu` (PozisyonBuyuklugu sinifi) | `initialTradeParams` (InitialTradeParams sinifi) |
| `istatistikleri_hesapla()` | `CalculateStatistics()` |
| `istatistikleri_dosyaya_yaz()` | `WriteStatisticsToFile(outputDir)` |

### Diger Tamamlanan Isler
- `WriteStatisticsToFile` artik `outputDir` parametresi aliyor (hardcoded path kaldirildi)
- `Finalize(bool saveStatisticsToFile, string? outputDir)` signature guncellendi
- Statistics.cs dosya yazma: `FileShare.ReadWrite` ile yaziliyor (Notepad acikken hata vermez)
- LogManager'a `LogRawInstance()` non-static method eklendi (sinifa ozel logger desteği)
- Console menü sistemi eklendi ([1] Read Data, [2] Run, [3] Read+Run, [0] Cikis)
- `DeleteModules()` icine `ClearCallbacks()` eklendi (event memory leak onlemi)
- AlgoTrader'a SymbolName/SymbolPeriod vb. property'ler eklendi, metadata'dan set ediliyor

================================================================================

# AlgoTrade - Yol Haritasi ve Tasarim Notlari

## 1. SingleTrader
Mevcut cekirdek yapidir. Tek bir stratejiyi verilen data uzerinde calistirir.
Bar bar ilerler, strateji sinyallerine gore A/S/F islemleri uretir.
Su an calisan temel akis budur.

## 2. MultiTrader
Birden fazla SingleTrader'in sinyallerinin bileskesini alarak tek bir sinyal uretir.
Ornek: Trader1 "AL" + Trader2 "AL" + Trader3 "SAT" => Bileske sinyal belirlenir.
Her SingleTrader farkli strateji/parametre ile calisabilir.

## 3. SingleTrader + Getiri Egrisi / KarZarar Egrisi
SingleTrader'i sanal (virtual) modda calistirip, getiri egrisine veya anlik KarZarar
egrisine bakarak gercek islem acma karari vermek.
- Strateji "AL" dediginde hemen gercek emir uretilmez
- Sanal al yapilir ve izlenir
- Belirli bir zarar esigine duserse => gercek giris yapilir (zarar konfirmasyonu)
- Sanal islem kar ediyorsa (emir dogru cikmis) => gercek emri uretir (kar konfirmasyonu)
- Ayni mantik SELL icin de gecerli
(Eski projedeki ConfirmationMode bu amacla tasarlanmisti)

## 4. SingleTraderOptimization
SingleTrader'i farkli parametre setleriyle cok sayida calistirip en iyi parametreleri bulmak.
Ornek: MA periyotlarini 5-50 arasinda tarayarak en iyi performansi veren kombinasyonu bulmak.
Sonuclar CSV/TXT olarak kaydedilir, karsilastirma tablolari uretilir.

## 5. AlgoTrader'a Script Yetenegi Kazandirmak

### 5.1 SingleTrader icin Scripting
  a) **Tam erisimli mod**: Kullanici tum kodlari butunuyle calistirabilir.
     DataReader, IndicatorManager, SingleTrader vb. her seye erisebilir.
  b) **Sandbox mod**: Kullaniciya DataReader ve diger ic kodlara eristirmeden
     sadece for dongusu ile stratejisini yazdirmak ve calisitrabilmek.
     Kullanici sadece fiyat verisi ve indikator degerleri gorur,
     A/S/F sinyali uretir, gerisini sistem halleder.
  c) **Dinamik strateji yukleme**: Stratejileri dosyadan okuyarak veya
     WinForm projesinde GUI uzerinden hazirlayarak kullanmak.
     Script dosyasi (.cs/.csx) yuklenip derlenir ve calistirilir.

### 5.2 SingleTraderOptimization icin Scripting
  Optimizasyon parametreleri ve strateji tanimi script uzerinden tanimlanabilir.

### 5.3 MultiTrader icin Scripting
  Birden fazla trader'in nasil birlestirilecegistrateji bileske kurallari
  script ile tanimlanabilir.

## 6. SingleTrader icin Sorgu Yapabilme
Calistirma sonrasi veya calistirma sirasinda sorgulanabilir veriler:
  a) **Indikator degerleri**: Herhangi bir bar icin indikator degerlerini sorgulama
  b) **Kullanici stratejisinden A/S/F bayraklari**: Kullanici tarafindan yazilmis
     strateji benzeri kodlardan uretilen al/sat/flat sinyalleri
  c) **Fiyat-indikator kesisimleri**: Fiyat hareketlerinin indikatorlerle
     kesisim noktalari (ornegin fiyat MA'yi yukari kirdi)
  d) **Indikator-indikator kesisimleri**: Indikatorlerin birbirleriyle
     kesisim noktalari (ornegin hizli MA yavas MA'yi yukari kesti)

## 7. Performans Hesaplamasi (SingleTrader ve MultiTrader icin)
Islem bazli detayli performans raporu uretilecek:
  - Yon | Lot | Acilis Tarihi | Acilis Fiyati | Kapanis Tarihi | Kapanis Fiyati | KarZarar | Bakiye | MaxDD

  **Inputlar:**
  - Strateji
  - Periyot
  - Ilk Bakiye
  - Ilk Lot
  - Baslangic Tarihi
  - Bitis Tarihi

## 8. AlgoTrader ile Toplu Sembol Taramasi
AlgoTrader'a verilen tum sembolleri belirtilen strateji ile tarar.
Her sembol icin strateji calistirilir, sonuclar toplu olarak listelenir.
(Screening / Tarama ozelligi)

## 9. Sorgu + Toplu Sembol Uygulama
Madde 6'daki sorgu yetenegi AlgoTrader uzerinden tum sembollere uygulanabilir.
Ornek: "Hangi sembollerde fiyat 20 MA'yi yukari kirdi?" gibi sorgular
tum sembol havuzunda calistirilir ve eslesen semboller listelenir.

## 10. Farkli Stratejilerin Ayni Sembol Icin Karsilastirmasi
Ayni sembol uzerinde birden fazla strateji calistirilir ve sonuclar topluca listelenir:
  - Her stratejinin getiri egrisi
  - Performans degerleri (KarZarar, MaxDD, ProfitFactor, Islem Sayisi vb.)
  - Karsilastirma tablosu / raporu
  Amac: Hangi stratejinin bu sembol icin en iyi calistigi gorulmek.
