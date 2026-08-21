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

### Kalan Farkliliklar (TAMAMLANDI)

#### 2. Strategy Configuration (StrategyFactory)
- **Eski**: `StrategyFactoryMethod` null kontrolu, default `SimpleMAStrategy` olusturma,
  `SetStrategyFactory`, `strategy.OnInit()`, `singleTrader.SetStrategy(strategy)` akisi var (satir 1049-1110)
- **Yeni**: `src/AlgoTrade.Core/Trading/Strategy/IStrategy.cs`, `BaseStrategy.cs` ve
  `Strategies/StrategyRegistry.cs` ile tasindi. Alti somut strateji mevcut: `SimpleMAStrategy`,
  `SimpleRSIStrategy`, `SimpleSuperTrendStrategy`, `SimpleMFIStrategy`, `SimpleMostStrategy`,
  `SimpleDIStrategy`.
- **Durum**: TAMAM

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

---

## ApplyEquityCurveFilter() Implementasyonu (TAMAMLANDI)

### Kaynak
- **Eski proje**: `ConfirmingSingleTrader.buildConsensusSignal()` (coklu trader consensus mantigi)
- **Yeni proje**: `SingleTrader.ApplyEquityCurveFilter(int barIndex)` (tek trader, kendi equity curve'une bakar)

### Yapilan Degisiklikler

#### 1. ConfirmationTrigger Enum
- `SingleTrader.cs` icinde `TraderRunMode` altina eklendi
- Degerler: `ProfitOnly = 0`, `LossOnly = 1`, `Both = 2`

#### 2. EquityCurveFilterProperties (SingleTrader field'lari)
| Field | Varsayilan | Aciklama |
|-------|-----------|----------|
| `thresholdTypeIsPercent` | false | false = Deger, true = Yuzde |
| `profitConfirmationThreshold` | 10.0 | Kar esigi |
| `lossConfirmationThreshold` | 5.0 | Zarar esigi |
| `confirmationTrigger` | Both | ProfitOnly / LossOnly / Both |
| `_equityCurveConfirmed` | false | Onay durumu (yon degisince sifirlanir) |

#### 3. Reset() icine eklendi
- Tum equity curve filter field'lari varsayilan degerlere sifirlaniyor

#### 4. is_prev_yon_a/s/f() Methodlari
- `is_son_yon_a/s/f()` pattern'inde `signals.PrevYon` kontrolu icin eklendi

#### 5. ApplyEquityCurveFilter Akisi
```
Adim 1: Mevcut yon belirleme (SonYon -> A/S/F)
Adim 2: Onceki yon ile karsilastir (PrevYon -> yon degisti mi?)
Adim 3: Yon degiştiyse confirmed = false
Adim 4: FLAT ise -> direkt gec, confirmed = false, return
Adim 5: LONG/SHORT ve confirmed degilse -> esik kontrolu:
         - thresholdTypeIsPercent'e gore deger/yuzde bazli kontrol
         - confirmationTrigger moduna gore (ProfitOnly/LossOnly/Both)
         - Esik gecildi -> confirmed = true, sinyal gecerli
         - Esik gecilmedi -> sadece giris sinyalleri iptal (Al=false, Sat=false, None=true)
         - Zaten confirmed ise -> esik kontrolu yapilmaz, sinyal devam eder
```

#### 6. Cagri Sirasi (Run methodu icinde)
```
ExecuteStrategy(i)
  -> MapStrategyCommandsToTradeCommands()
    -> ApplyTimingFilters(i)          // Zaman filtresi + GunSonuPozKapat
      -> ApplyEquityCurveFilter(i)    // Equity curve filtresi
        -> ExecutePostOrderMethods(i) // Emirler calistirilir
```

#### 7. Onemli Tasarim Kararlari
- **Sadece giris sinyalleri (Al/Sat) filtrelenir.** Cikis sinyalleri (FlatOl, KarAl, ZararKes, PasGec) dokunulmadan gecilir.
- **Sebep**: GunSonuPozKapat gibi koruyucu mekanizmalar baskılanmamali. Ornegin pozisyon acikken (SonYon=A) GunSonuPozKapat FlatOl=true set ederse, equity curve filtresi bunu iptal etmemeli.
- `EquityCurveFilteringEnabled == false` ise method hicbir sey yapmadan gecer.
- Ileride `IsEquityCurveTradeEnabled` / `IsTimingFiltersTradeEnabled` flag'leri aktif edilebilir (su an comment'li).

---

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

---

## TODO — Yol Haritasi Maddelerinin Gercek Durumu (2026-08-18 kod analizine gore)

> Kaynak: [docs/PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md) — tum proje (152 dosya) satir satir
> okunarak cikarilan detayli analiz. Asagidaki liste, yukaridaki "Yol Haritasi" (madde 1-10)
> maddelerinin kod tabaninda GERCEKTE hangi durumda oldugunu yansitir; bu belgenin eski hali
> (madde 2 ve 4) "ileride yapilacak" diyordu ama kod bunlari cogunlukla tamamlamis. Sirasiyla
> once TAMAMLANMASI GEREKENLER (yapilmamis/eksik), sonra sadece dokuman guncellemesi gereken
> maddeler listelenmistir.

### Yapilmamis / Eksik Maddeler (oncelik sirasiyla)

- [ ] **Madde 5.1b — Scripting: Sandbox Mod**
      `ScriptExecutor` (src/AlgoTrade.Core/Scripting/ScriptExecutor.cs) su an SADECE tam
      erisimli modda calisiyor; script tum proje assembly'sine erisebiliyor. Kullaniciyi
      DataReader/SingleTrader gibi ic kodlardan izole edip sadece fiyat+indikator veren bir
      sandbox mod yok.

- [ ] **Madde 5.1c (kismi) — Dinamik Strateji Yukleme: GUI tarafi**
      Script dosyasindan (.csx, `#load` inliner ile) yukleme TAMAM. WinForms uzerinden GUI ile
      strateji hazirlama YOK — `AlgoTrade.WinForms/MainForm.cs` 52 satirlik bir iskelet, Console
      uygulamasinin kullandigi gercek `AlgoTrader` akisiyla (RegisterLogger/SetData/
      ConfigureStrategyFromConfig/Initialize/...) bile ortusmuyor gibi duruyor (dogrulanmali).

- [ ] **Madde 5.3 — MultiTrader icin Scripting**
      Birden fazla trader'in nasil birlestirilecegi (consensus kurallari) hala script uzerinden
      tanimlanamiyor — ama artik 4 hazir mod (Net/Majority/All/Any, AppConfig.json'dan secilebilir)
      var (bkz. Madde 2 notu, TAMAMLANDI 2026-08-18). Kalan tek eksik: script'ten TAMAMEN OZEL
      (bu 4 modun disinda) bir consensus kurali tanimlama imkani — dar bir eksik, genis kapsamli
      degil.

- [ ] **Madde 6 (genisletme) — Sorgu Yapabilme: zengin sorgu tipleri**
      Alt yapi (IQuery/BaseQuery/QueryRegistry/QueryConfigLoader) TAMAM ve calisiyor, ama somut
      sorgu ornegi sadece 1 tane: `SimpleQuery1` (MA8/MA200 kesisimi + trader-state). Roadmap'te
      tarif edilen "fiyat-indikator kesisimleri", "indikator-indikator kesisimleri" (genel
      amacli), "kullanici stratejisinden A/S/F bayraklari sorgusu" gibi zengin sorgu tipleri
      henuz yazilmadi.

- [ ] **Madde 7 (kismi) — MultiTrader icin Performans Hesaplamasi**
      SingleTrader tarafinda trade-bazli detayli performans raporu TAMAM (`Statistics.
      PerformansRow` — Yon/Lot/Acilis-Kapanis Tarihi-Fiyati/KarZarar/Bakiye/MaxDD). MultipleTrader
      tarafinda `WriteMultipleTraderListsToFiles()` sadece BAR-BAR rapor uretiyor (her bar icin
      tum trader'larin Yon/Seviye/Sinyal'i), consensus trader'in (mainTrader) trade-bazli
      performans raporu urup uretmedigi dogrulanmali.

- [x] **Madde 8 — AlgoTrader ile Toplu Sembol Taramasi (Screening)** — TAMAMLANDI (2026-08-18)
      `SymbolScanner` (bkz. `src/AlgoTrade.Core/Trading/Traders/SymbolScanner.cs`) + Console
      `[10] Tarama` menu secenegi. AlgoTrader'dan bilincli olarak bagimsiz, tek strateji + tek
      sembol klasoru (AutoDiscover ya da acik liste) uzerinde calisir; SingleTrader-bazli, sonuc
      CSV/TXT'ye ozet satir olarak yaziliyor + SortField'e gore siralanan ayri bir dosya. Detay:
      [docs/todo.md](todo.md) "Tarama Motorları" bolumu.
      **2026-08-18 devami**: Strateji tarafinda tum matris (8/8, Console `[10]`-`[15]`) ve Sorgu
      tarafinda da tum matris (8/8, Console `[16]`-`[21]`) tamamlandi — bkz.
      [docs/todo.md](todo.md) "Tarama Motorları — TAMAMLANDI (16/16, 2026-08-18)" bolumu.

- [x] **Madde 9 — Sorgu + Toplu Sembol Uygulama** — TAMAMLANDI (2026-08-18)
      `QuerySymbolScanner` (Console `[16]`) madde 9'un birebir istedigi seyi karsiliyor: "Hangi
      sembollerde fiyat 20 MA'yi yukari kirdi?" gibi bir sorgu tum sembol havuzunda calistirilip
      sonuclar listeleniyor. Detay: [docs/todo.md](todo.md) "Sorgu Tarama Matrisi" bolumu.

- [ ] **Madde 10 — Farkli Stratejilerin Ayni Sembol Icin Karsilastirmasi**
      Hic implement edilmemis. `SingleTraderOptimizer` AYNI stratejinin farkli parametreleriyle
      tarama yapiyor (grid search) ama FARKLI stratejileri (orn. SimpleRSIStrategy vs
      SimpleMACDStrategy) ayni sembolde calistirip karsilastiran bir rapor/tablo mekanizmasi yok.

### Tam Tamamlanmis Madde

- [x] **Madde 3 — SingleTrader + Getiri Egrisi / KarZarar Egrisi (sanal islem konfirmasyonu)** —
  TAMAMLANDI (2026-08-19). Onceki analizde "Hic implement edilmemis / KAPSAM DISI" denmisti, bu
  artik guncel degil: `ConfirmingSingleTrader.cs` (469 satir) + `ConfirmingMultipleTrader.cs`
  (483 satir) + `Trading/Core/VirtualPositionConfirmer.cs` (175 satir) ile tam bir
  signal-trader → sanal pozisyon → konfirmasyon → mainTrader gercek emir state machine'i
  implement edilmis. AppConfig'te `ConfirmingSingleTraderConfig`/`ConfirmingMultipleTraderConfig`
  ile yapilandiriliyor, export tarafinda `SetConfirmingSignalTraderExportConfig` ile ayrica
  versiyonlanmis export destegi var, Console `[22]`-`[25]` menuleri bu akisi calistiriyor. Detay:
  [docs/PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md) §2.9.

- [x] **Madde 2 — MultiTrader**: `MultipleTrader.cs` (618 satir) tam ve calisir durumda
  implement edilmis (`Trading/Traders/MultipleTrader.cs`). **2026-08-18 guncellemesi**: Consensus
  modu artik hardcoded "Net" degil — `BuildConsensusSignal()` `AppConfig.MultipleTraderConfig.
  ConsensusConfig`'ten okunan Net/Majority/All/Any modlarinin hepsini destekliyor (`MinNetCount`
  dahil). Bu belgenin "Yol Haritasi" bolumundeki madde 2 ifadesi ("ileride tasarlanabilir") artik
  tamamen guncel degil — MultiTrader hem temel hem consensus-genisletmesi olarak tam.
  **Ayrica**: Tum tarama matrisi (Strateji ve Sorgu ekseninde 8/8 + 8/8, Console `[10]`-`[21]`)
  tamamlandi — bkz. [docs/todo.md](todo.md) "Tarama Motorları" bolumu.

### Sadece Belge Guncellemesi Gereken Madde (kod calisiyor, kucuk bir tutarsizlik var)

- [x→belge guncellensin] **Madde 4 — SingleTraderOptimization**: `SingleTraderOptimizer.cs`
  (934 satir) tam bir grid-search optimizasyon motoru olarak implement edilmis
  (`GenerateParameterCombinations`, `PartialOpt` destegi, sirali CSV/TXT ciktilari).
  `OptimizationConfigLoader` parametre araligi config'ini okuyor. Tek kucuk tutarsizlik:
  `GetBestResult()` sadece NetProfit'e gore siraliyor, dosya ciktisi ise config'teki
  `SortField`'e gore — bu ikisi farkli sonuc verebilir (bkz. PROJECT_ANALYSIS.md §8).

### Ayrica Not (roadmap disi ama onemli)
Pyramiding destegi (`InitialTradeParams.cs`) bu belgede hic gecmiyor ama tam implement edilmis —
belgeye sonradan eklenmis bir ozellik olarak not dusulmeli.
