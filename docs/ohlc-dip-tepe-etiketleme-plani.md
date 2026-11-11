# OHLC Dip/Tepe Etiketleme ve Pencere Analizi Planı

## 1. Amaç

Bu özellik, boyutu çalışma anında değişebilen OHLC veri kümelerinin (örneğin 50.000,
2.000.000 veya daha fazla bar) küçük ve yönetilebilir
pencereler halinde incelenmesini; kullanıcının grafik üzerinde dip ve tepe noktalarını elle
işaretlemesini; işaretlenen noktalar ve aralarındaki hareketler hakkında ölçümlerin otomatik
hesaplanıp kalıcı dosyalara yazılmasını sağlayacaktır.

Bu çalışma normal SingleTrader, MultipleTrader ve ConfirmingTrader plotlarından bağımsız bir
iş akışı olacaktır. İlk sürümde strateji, indikatör, işlem sinyali ve performans eğrileri
çizilmeyecek; yalnız OHLC mumları ve kullanıcı etiketleri gösterilecektir.

## 2. Önerilen Mimari

### 2.1. Menü

Ana menüye ayrı bir seçenek eklenecektir:

```text
[26] OHLC Dip/Tepe Etiketleme
```

Bu seçenek mevcut bellekteki veriyi kullanabilecek, veri okunmamışsa kullanıcıya önce veri
okuması gerektiğini bildirecektir. Daha sonra gerekirse `Read Data + OHLC Dip/Tepe Etiketleme`
için ikinci bir menü seçeneği eklenebilir.

### 2.2. C# tarafı

Yeni bir oturum yöneticisi oluşturulacaktır:

```text
SwingAnnotationSession
```

Sorumlulukları:

- OHLC, zaman, sembol ve periyot verilerini plotter bundle'ına aktarmak.
- Etiketleme oturumunun kimliğini ve dosya yollarını oluşturmak.
- Pencere boyutu, kaydırma miktarı ve örtüşme ayarlarını konfigürasyondan okuyup plotter'a göndermek.
- Dear ImGui plotter'ı `swing_annotation` modunda başlatmak.
- Kaydedilen sonuç dosyalarını doğrulamak.
- Daha önce başlanmış bir oturumu devam ettirmek.
- Oturum kapanırken bekleyen kayıtların diske yazılmasını sağlamak.

İlk aşamada mevcut `TradeDataBundleConverter` değiştirilmeden ayrı ve küçük bir dönüştürücü
tercih edilecektir:

```text
OhlcAnnotationBundleConverter
```

Bu dönüştürücü yalnız şu dizileri yazacaktır:

- `timestamps`
- `open`
- `high`
- `low`
- `close`
- İsteğe bağlı `volume`
- İsteğe bağlı `size/lot`
- Sembol, periyot ve veri aralığını içeren metadata

### 2.3. Python / Dear ImGui tarafı

Yeni bir kontrol sınıfı oluşturulacaktır:

```text
SwingAnnotationController
```

Sorumlulukları:

- Yalnız görünür pencerenin mumlarını çizmek.
- Fare ile seçilen barı global bar indeksine çevirmek.
- Dip, tepe ve isteğe bağlı diğer etiketleri oluşturmak.
- Etiketleri grafik üzerinde renkli işaret ve açıklamalarla göstermek.
- Ardışık etiketler arasındaki analizleri hesaplamak.
- Her kullanıcı işleminden sonra güvenli kayıt yapmak.
- Geri alma, silme, değiştirme ve oturuma devam etme işlemlerini yönetmek.

Mevcut Dear ImGui uygulaması kullanılacak, ancak normal trade plotundan ayrılmış bir
`swing_annotation` çalışma modu eklenecektir.

## 3. Dinamik Veri Boyutu ve Pencereleme

Toplam bar sayısı sabit kabul edilmeyecek ve kaynak veri/bundle metadata'sından çalışma anında
okunacaktır. Pencere sayısı, son pencerenin sınırları ve gezinme adımları bu gerçek toplam bar
sayısından hesaplanacaktır. Boş, küçük veya pencere boyutundan daha kısa veri kümeleri de aynı
akışta desteklenecektir.

Çok büyük veri kümelerindeki mumların tamamı aynı anda çizilmeyecektir. Veri bir kez yüklenecek, grafik ve kullanıcı
etkileşimi yalnız görünür aralık üzerinde çalışacaktır.

Örnek başlangıç ayarları:

```text
Pencere boyutu   : 2.000 bar
Kaydırma miktarı : 500 bar
Örtüşme          : 1.500 bar
```

Bu değerler kod içine sabitlenmeyecek, aşağıda tanımlanan konfigürasyon dosyasından okunacaktır.

Örtüşme kullanılmasının nedeni, pencere sınırına yakın dip ve tepelerin komşu barlar görülmeden
yanlış değerlendirilmesini önlemektir.

Her pencere için tutulacak bilgiler:

- Pencere sıra numarası
- Global başlangıç ve bitiş bar indeksleri
- Başlangıç ve bitiş zamanı
- Penceredeki bar sayısı
- Önceki ve sonraki pencereyle örtüşen bar sayısı
- Pencerenin tamamlanıp tamamlanmadığı
- Pencerede oluşturulan etiket sayısı

### 3.1. Pencere konfigürasyonu

Pencere davranışı şu dosyadan yönetilecektir:

```text
inputs/configs/SwingAnnotationConfig.json
```

Önerilen ilk şema:

```json
{
  "Window": {
    "InitialViewMode": "FitToScreen (Ultra)",
    "InitialWidthBars": 2000,
    "NavigationStepBars": 500,
    "MinimumWidthBars": 100,
    "MaximumWidthBars": 20000,
    "ClampLastWindowToData": true,
    "AutoSaveOnWindowChange": true,
    "Presets": [250, 500, 1000, 2000, 5000, 10000]
  },
  "Rendering": {
    "MaxCandlesWithoutAggregation": 10000,
    "LoadVolume": true,
    "LoadSize": false
  }
}
```

Alanların anlamı:

- `InitialViewMode`: İlk açılış görünümü; varsayılan `FitToScreen (Ultra)` son verileri gösterir.
- `InitialWidthBars`: `Range` görünümüne geçildiğinde kullanılacak bar sayısı.
- `NavigationStepBars`: Önceki/sonraki komutunda başlangıç indeksinin kaç bar kaydırılacağı.
- `MinimumWidthBars`: Kullanıcının daraltabileceği en küçük pencere.
- `MaximumWidthBars`: Kullanıcının genişletebileceği en büyük pencere.
- `ClampLastWindowToData`: Son pencerede boş alan yerine pencerenin veri sonuna yaslanması.
- `AutoSaveOnWindowChange`: Pencere değiştirilmeden önce etiketlerin otomatik kaydedilmesi.
- `Presets`: Arayüzden tek tıklamayla seçilebilecek pencere genişlikleri.
- `MaxCandlesWithoutAggregation`: Bu sınırın üzerinde ayrıntılı mum yerine özet çizim kullanılması.

Örtüşme ayrıca sabit tutulmayacak ve şu formülle dinamik hesaplanacaktır:

```text
Örtüşme = Pencere genişliği - Kaydırma adımı
```

`NavigationStepBars`, pencere genişliğinden büyükse örtüşme sıfır kabul edilecek ve arada veri
atlanmaması için konfigürasyon doğrulama uyarısı üretilecektir.

Kullanıcı çalışma sırasında preset seçerek pencere genişliğini değiştirebilecek; seçilen değer
oturum durumuna yazılacak, ancak ana konfigürasyon dosyası kullanıcı açıkça istemeden
değiştirilmeyecektir.

### 3.2. Gezinme

Önerilen kontroller:

- `Sol ok` / `A`: Önceki pencere
- `Sağ ok` / `D`: Sonraki pencere
- `Page Up` / `Page Down`: Daha büyük adımla ilerleme
- `Home`: İlk pencere
- `End`: Son işlenmiş veya son pencere
- Fare tekerleği: Yakınlaştırma/uzaklaştırma
- Fare sürükleme: Görünür alanı kaydırma
- Global indeks veya tarih girerek doğrudan konuma gitme

Pencere değiştirildiğinde kaydedilmemiş işlem bırakılmayacaktır.

## 4. Kullanıcı Etiketleri

### 4.1. İlk sürümde zorunlu etiketler

- `Dip`
- `Tepe`

Varsayılan fiyat seçimi:

- Dip için seçilen mumun `Low` değeri
- Tepe için seçilen mumun `High` değeri

Kullanıcı isterse seçilen fiyatı daha sonra değiştirebilecektir.

### 4.2. İleride eklenebilecek etiketler

- Ara dip
- Ara tepe
- Ana dip
- Ana tepe
- Kırılım başlangıcı
- Kırılım onayı
- Trend başlangıcı
- Trend bitişi
- Gap
- Anomali / veri hatası
- Belirsiz nokta
- Kullanıcı notu

### 4.3. Etkileşim

Önerilen kullanım:

- Sol tıklama: Aktif etiket türüyle barı işaretleme
- Sağ tıklama: Bar ve mevcut etikete ait ayrıntı menüsü
- `1`: Dip moduna geçme
- `2`: Tepe moduna geçme
- `Delete`: Seçili etiketi silme
- `Ctrl+Z`: Son işlemi geri alma
- `Ctrl+Y`: Geri alınan işlemi yeniden uygulama
- `Enter`: Pencereyi tamamlandı olarak işaretleme ve sonraki pencereye geçme
- `N`: Seçili etikete not ekleme

Aynı global bara ikinci bir etiket eklenmesi varsayılan olarak engellenecek, kullanıcıdan onay
alınmadan mevcut etiketin üzerine yazılmayacaktır.

## 5. Etiket Başına Kaydedilecek Veriler

Her dip veya tepe etiketi için aşağıdaki alanlar kaydedilebilir:

### 5.1. Kimlik ve konum

- Oturum kimliği
- Etiket kimliği
- Sembol
- Periyot
- Global bar indeksi
- Pencere içi göreli indeks
- Tarih-saat
- Etiket türü (`Dip` / `Tepe`)
- Kullanıcı notu
- Oluşturulma ve son değiştirilme zamanı

### 5.2. Mum verileri

- Open
- High
- Low
- Close
- Volume
- Lot/size
- Mum gövde büyüklüğü
- Üst fitil büyüklüğü
- Alt fitil büyüklüğü
- Toplam mum aralığı (`High - Low`)
- Mum yönü (`Bullish`, `Bearish`, `Doji`)
- Gövde/aralık oranı
- Üst fitil/aralık oranı
- Alt fitil/aralık oranı

### 5.3. Yerel çevre bilgileri

- Önceki ve sonraki N barın en yüksek/en düşük değerleri
- Seçilen noktanın yerel penceredeki sırası
- Soldaki ve sağdaki en yakın daha yüksek/düşük bar mesafesi
- Noktanın N barlık yerel dip/tepe olup olmadığı
- Yerel fiyat aralığı içindeki yüzdesel konumu
- Etiket çevresindeki ortalama hacim
- Etiket barının hacminin yerel ortalamaya oranı
- Etiket çevresindeki volatilite

## 6. İki Ardışık Etiket Arasında Hesaplanabilecek Veriler

Bir önceki dip/tepe ile yeni etiket arasındaki segment için aşağıdaki ölçümler üretilebilir.

### 6.1. Temel fiyat değişimleri

- Başlangıç fiyatı
- Bitiş fiyatı
- Mutlak fiyat farkı
- İşaretli yüzdesel değişim
- Mutlak yüzdesel değişim
- Logaritmik getiri
- Hareket yönü (`Yükseliş` / `Düşüş`)

Formüller:

```text
Yüzdesel değişim = (Bitiş - Başlangıç) / Başlangıç * 100
Log getiri        = ln(Bitiş / Başlangıç)
```

### 6.2. Süre ve mesafe

- Geçen bar sayısı
- Geçen takvim süresi
- İşlem seansı sayısı
- Gün sayısı
- Saat/dakika sayısı
- Bar başına ortalama fiyat değişimi
- Bar başına ortalama yüzdesel değişim
- Hareketin hızı (`% / bar`, `% / saat`, `% / gün`)

### 6.3. Segment içi fiyat davranışı

- Segment içindeki en yüksek fiyat
- Segment içindeki en düşük fiyat
- Segment içi toplam fiyat aralığı
- Başlangıçtan görülen maksimum olumlu sapma (MFE)
- Başlangıçtan görülen maksimum olumsuz sapma (MAE)
- Bitişten önce oluşan en büyük geri çekilme
- Bitişten önce oluşan en büyük ters hareket
- Hareket boyunca yeni yüksek/yeni düşük sayısı
- Yükselen bar sayısı
- Düşen bar sayısı
- Doji bar sayısı
- Yükselen/düşen bar oranı
- Arka arkaya en uzun yükseliş ve düşüş serisi

### 6.4. Yol ve verimlilik ölçümleri

- Bar kapanışları arasındaki mutlak değişimlerin toplamı
- Net hareket / toplam yol oranı
- Fiyat hareketi verimlilik oranı
- Segmentin doğrusal regresyon eğimi
- Regresyon uyumu (`R²`)
- Başlangıç-bitiş doğrusundan maksimum sapma
- Hareketin düzgünlük/zikzak ölçüsü

Örnek verimlilik formülü:

```text
Efficiency Ratio = abs(Bitiş - Başlangıç) /
                   sum(abs(Close[i] - Close[i-1]))
```

Değer 1'e yaklaştıkça hareket daha doğrudan, 0'a yaklaştıkça daha zikzaklıdır.

### 6.5. Volatilite ölçümleri

- Segmentteki ortalama True Range
- ATR
- Getiri standart sapması
- En yüksek tek bar hareketi
- Ortalama mum aralığı
- Hareket yüzdesi / ATR
- Hareket büyüklüğü / volatilite oranı
- Başlangıç ve bitiş çevresindeki volatilite karşılaştırması

### 6.6. Hacim ve aktivite ölçümleri

Veride hacim mevcutsa:

- Toplam hacim
- Ortalama hacim
- Medyan hacim
- Maksimum hacim
- Başlangıç ve bitiş barı hacmi
- İlk yarı ve ikinci yarı hacim karşılaştırması
- Yükselen bar hacmi / düşen bar hacmi
- Fiyat değişimi / hacim oranı
- Hacim trendi
- Etiket barındaki hacim anomalisi

### 6.7. Dip-tepe yapısı

- Segment türü (`Dip→Tepe`, `Tepe→Dip`, `Dip→Dip`, `Tepe→Tepe`)
- Beklenen alternasyonun korunup korunmadığı
- Önceki dipten daha yüksek/düşük dip
- Önceki tepeden daha yüksek/düşük tepe
- `Higher High`, `Lower High`, `Higher Low`, `Lower Low` sınıfı
- Önceki yükseliş/düşüş segmentine göre büyüklük oranı
- Önceki segmente göre süre oranı
- Fiyat geri alma oranı
- Bir önceki hareketin ne kadarının geri alındığı
- Yeni zirve/dip yapılıp yapılmadığı

### 6.8. Fibonacci ve oran analizleri

- Önceki hareketin geri çekilme yüzdesi
- 23.6 / 38.2 / 50 / 61.8 / 78.6 seviyelerine yakınlık
- Önceki segmente göre uzatma oranı
- 1.0 / 1.272 / 1.618 uzatma seviyelerine yakınlık

Bu ölçümler ilk sürüm için zorunlu değildir; temel kayıt sistemi doğrulandıktan sonra
eklenecektir.

## 7. Üç veya Daha Fazla Etiketle Hesaplanabilecek Yapılar

- Dip–tepe–dip geri çekilme oranı
- Tepe–dip–tepe toparlanma oranı
- Ardışık diplerin eğimi
- Ardışık tepelerin eğimi
- Trend kanalı genişliği
- Swing büyüklüklerinin ortalaması ve standart sapması
- Swing sürelerinin ortalaması ve standart sapması
- Yükseliş ve düşüş swing asimetrisi
- Fiyat-zaman simetrisi
- Daralan/genişleyen swing yapısı
- Olası üçgen, kanal ve sıkışma yapıları
- Son N swing içinde en büyük/en küçük hareket
- Rejim sınıflaması (`trend`, `yatay`, `yüksek volatilite`, `düşük volatilite`)

Bu ölçümler, kullanıcı etiketleri yeterli sayıya ulaştıktan sonra ayrı bir analiz katmanında
hesaplanacaktır.

## 8. Kayıt Formatı

### 8.1. Olay günlüğü: JSONL

Her kullanıcı işlemi anında, eklemeli bir dosyaya yazılacaktır:

```text
outputs/annotations/{symbol}_{period}/{sessionId}/annotation_events.jsonl
```

Örnek:

```json
{"action":"add","annotationId":"A-000123","globalBarIndex":1254780,"type":"Peak","price":10842.5,"createdAt":"2026-11-09T21:00:00+03:00"}
```

Olay günlüğü sayesinde uygulama veya bilgisayar beklenmedik şekilde kapanırsa son işlem dahil
olmak üzere oturum yeniden kurulabilir.

### 8.2. Güncel etiketler: CSV

```text
outputs/annotations/{symbol}_{period}/{sessionId}/annotations.csv
```

Temel sütunlar:

```text
SessionId;AnnotationId;Symbol;Period;GlobalBarIndex;DateTime;Type;Price;
PreviousAnnotationId;PreviousType;PreviousPrice;BarDistance;ElapsedTime;
PriceChange;ChangePercent;WindowStart;WindowEnd;Note
```

### 8.3. Segment sonuçları: CSV

```text
outputs/annotations/{symbol}_{period}/{sessionId}/segments.csv
```

Her iki ardışık etiket arasında bir satır üretilecektir. Gelişmiş analiz sütunları bu dosyaya
eklenecektir.

### 8.4. Oturum durumu

```text
outputs/annotations/{symbol}_{period}/{sessionId}/session.json
```

İçerik:

- Veri dosyasının yolu ve parmak izi
- Sembol ve periyot
- Toplam bar sayısı
- Pencere ayarları
- Son açık pencere
- Son seçili etiket
- Tamamlanan pencere aralıkları
- Oluşturulma ve son kayıt zamanı
- Dosya şema sürümü

## 9. Doğrulama Kuralları

İlk sürümde aşağıdaki kontroller uygulanacaktır:

- Global bar indeksi veri sınırları içinde olmalıdır.
- Dip fiyatı varsayılan olarak barın Low değerine eşit olmalıdır.
- Tepe fiyatı varsayılan olarak barın High değerine eşit olmalıdır.
- Ardışık iki etiket aynı türdeyse kullanıcı uyarılmalıdır.
- Yeni etiket zaman olarak önceki etiketten gerideyse yeniden sıralama onayı istenmelidir.
- Aynı bara mükerrer etiket eklenmesi engellenmelidir.
- Veri dosyası değişmişse eski oturum doğrudan açılmamalı, kullanıcı uyarılmalıdır.
- Örtüşen pencerelerde aynı etiket iki kez oluşturulmamalıdır.

Uyarılar ilk sürümde engelleyici olmayabilir; kullanıcı gerekçeli olarak kuralı aşabilmelidir.

## 10. Performans İlkeleri

- Veri kümesinin tamamı her frame dönüştürülmeyecektir; algoritma toplam bar sayısından bağımsız çalışacaktır.
- Görünür pencere dilimi mümkün olduğunca kopyasız kullanılacaktır.
- Etiketler global indeksle saklanacaktır.
- Pencere değişiminde yalnız görünür mum ve etiket geometrisi güncellenecektir.
- Dosyaya her işlemde küçük JSONL satırı eklenecektir.
- Büyük CSV dosyaları her tıklamada baştan yazılmayacaktır.
- CSV snapshot periyodik, pencere tamamlandığında veya oturum kapanırken üretilecektir.
- Gerekirse sıkıştırılmış NPZ yerine bellek eşlemeli NPY/binary veri formatı değerlendirilecektir.
- Çok uzak görünüm seviyelerinde mum yerine min/max tabanlı özet çizim kullanılabilecektir.

## 11. Uygulama Aşamaları

### Aşama 1 — Veri modeli ve dosya sözleşmesi

- `SwingAnnotation`, `SwingSegment`, `SwingAnnotationSessionState` modelleri oluşturulacak.
- `SwingAnnotationConfig.json` şeması ve varsayılan pencere profilleri oluşturulacak.
- Konfigürasyon sınırları ve veri boyutuna göre pencere doğrulaması yazılacak.
- JSONL, annotations CSV, segments CSV ve session JSON şemaları kesinleştirilecek.
- Global indeks ve fiyat doğrulama kuralları yazılacak.
- Dosya yazma/yeniden yükleme testleri hazırlanacak.

Başarı ölçütü: Grafik olmadan örnek etiketler kaydedilip yeniden yüklenebilmeli ve aynı sonuç
üretilmelidir.

### Aşama 2 — OHLC-only bundle

- `OhlcAnnotationBundleConverter` oluşturulacak.
- Küçük, orta ve çok büyük örnek veri kümeleriyle boyut ve yükleme süresi ölçülecek.
- Sembol, periyot, tarih aralığı ve veri parmak izi metadata'ya eklenecek.

Başarı ölçütü: Bundle yalnız gerekli OHLC verilerini içermeli ve mevcut trade bundle'larından
bağımsız üretilmelidir.

### Aşama 3 — Ayrı menü ve oturum başlatma

- Menü `26` eklenecek.
- `SwingAnnotationSession` oluşturulacak.
- Yeni oturum ve mevcut oturuma devam etme akışları eklenecek.
- Plotter `swing_annotation` komutuyla başlatılacak.

Başarı ölçütü: Menüden yalnız OHLC içeren boş etiketleme ekranı açılmalıdır.

### Aşama 4 — Pencereleme ve gezinme

- Görünür pencere state'i oluşturulacak.
- Önceki/sonraki pencere, doğrudan indekse/tarihe gitme ve örtüşme uygulanacak.
- Pencere ilerleme durumu session JSON'a yazılacak.

Başarı ölçütü: İki milyon bar üzerinde arayüz akıcı kalmalı ve global indeks kaymamalıdır.

### Aşama 5 — Dip/tepe işaretleme

- Bar hit-test uygulanacak.
- Dip ve tepe modları eklenecek.
- İşaret ekleme, silme, değiştirme, undo/redo eklenecek.
- Grafik üzerindeki marker ve etiket metinleri çizilecek.
- Her işlem JSONL dosyasına anında yazılacak.

Başarı ölçütü: Uygulama kapatılıp açıldığında tüm işaretler aynı barlarda görünmelidir.

### Aşama 6 — Temel segment analizi

İlk uygulanacak ölçümler:

- Segment türü
- Başlangıç ve bitiş fiyatı
- Fiyat farkı
- Yüzdesel değişim
- Geçen bar sayısı
- Geçen süre
- Segment içi en yüksek ve en düşük
- MFE ve MAE
- Ortalama değişim/bar
- Efficiency Ratio

Başarı ölçütü: Ardışık her iki etiket için `segments.csv` içinde doğrulanabilir bir satır
üretilmelidir.

### Aşama 7 — Gelişmiş analiz

- Volatilite, ATR, regresyon ve hacim ölçümleri eklenecek.
- Üç veya daha fazla swing kullanan yapı analizleri eklenecek.
- Pencere ve oturum özet raporları oluşturulacak.

### Aşama 8 — Kullanılabilirlik ve güvenlik

- Klavye kısayolları ve bağlam menüsü tamamlanacak.
- Çökme sonrası kurtarma test edilecek.
- Büyük veri performans profili çıkarılacak.
- Otomatik yedekleme ve şema sürüm yükseltmesi eklenecek.

## 12. İlk Uygulama Kapsamı (MVP)

İlk çalışan sürüm bilinçli olarak sınırlı tutulacaktır:

- Ayrı menü
- OHLC-only plot
- 2.000 barlık pencere ve 500 barlık kaydırma
- Dip/tepe ekleme ve silme
- Global indeks, tarih, tür ve fiyat kaydı
- Yüzdesel değişim
- Geçen bar sayısı ve süre
- JSONL otomatik kayıt
- annotations ve segments CSV çıktıları
- Oturuma kaldığı yerden devam etme

ATR, regresyon, Fibonacci, hacim anomalisi ve çoklu swing formasyonları MVP doğrulandıktan sonra
eklenecektir.

## 13. Uygulamaya Başlamadan Önce Verilecek Kararlar

1. Varsayılan pencere boyutu ve kaydırma miktarı ne olmalı?
2. Dip/tepe fiyatı kesin olarak Low/High mı alınmalı, kullanıcı fiyatı değiştirebilmeli mi?
3. Aynı türden iki etiket art arda eklenebilmeli mi?
4. Pencere `Enter` ile tamamlandığında otomatik ilerlenmeli mi?
5. Etiketleme tek kullanıcı/tek oturum mu, aynı veri için birden fazla etiket seti mi desteklemeli?
6. İlk sürümde volume ve lot/size bundle'a dahil edilmeli mi?
7. Çıktılar sembol/periyot bazında mı, kaynak dosyanın adına göre mi klasörlenmeli?
8. Etiketlenen barın ekran görüntüsü veya pencere görüntüsü saklanmalı mı?

Bu kararlar Aşama 1 sırasında kesinleştirilecektir.

## 14. Uygulama Durumu ve Devam Notu (2026-11-09)

### Tamamlananlar

- Ana menüye `[26] OHLC Dip/Tepe Etiketleme` ve `[27] Read Data + OHLC Dip/Tepe Etiketleme` akışları eklendi.
- Yalnız OHLC verisi üreten `OhlcAnnotationBundleConverter` eklendi.
- Dinamik veri boyutu, `SwingAnnotationConfig.json` ve C# konfigürasyon modeli eklendi.
- Başlangıç görünümü `FitToScreen (Ultra)` olarak ayarlandı ve son veriler gösterildi.
- Ayrı `SwingAnnotationController` oluşturuldu.
- Swing Controls, en sağdaki kendisine ait dock alanına yerleştirildi.
- Swing modunda Script Panel, Left Menu, Data Manager, Pool, Panel Manager 1/2, Reports ve Console gizlendi.
- Swing Controls kapatılırsa `Open Swing Controls` düğmesiyle yeniden açılabiliyor.
- Pencere genişliği, kaydırma adımı ve preset değerleri konfigürasyondan okunuyor.
- Elle özel pencere genişliği girilip çalışma anındaki preset listesine eklenebiliyor.
- Başlangıç, önceki, sonraki ve son pencere gezinmesi ile global indekse gitme eklendi.
- Başlangıç/bitiş indeksleri, genişlik, örtüşme ve ilerleme yüzdesi gösteriliyor.
- Pan Controls etiketi gerçek OHLC X aralığıyla senkronize edildi.
- Dip ve Tepe etiketleri bellekte oluşturulup grafikte gösteriliyor.
- Dip fiyatı `Low`, Tepe fiyatı `High` değerinden alınıyor.
- Aynı global bara ikinci etiket eklenmesi engelleniyor.
- Etiketleme normal fare tuşlarından ayrıldı; sol ve sağ tuşlar grafik gezinmesine bırakıldı.
- Etiket silme ve undo/redo geçmişi eklendi.

### Mevcut kısayollar

- `A` veya `Sol Ok`: Önceki pencere.
- `D` veya `Sağ Ok`: Sonraki pencere.
- `Page Up` / `Page Down`: Pencere genişliği kadar geri/ileri gitme.
- `Home` / `End`: İlk/son pencere.
- `Space`: Serbest pan/zoom sonrasında kayıtlı swing penceresine dönme.
- İmleç mumun üzerindeyken `1`: Dip ekleme.
- İmleç mumun üzerindeyken `2`: Tepe ekleme.
- İmleç etiketli mumun üzerindeyken `Delete`: Etiketi silme.
- `Ctrl+Z` / `Ctrl+Y`: Geri alma/yeniden uygulama.

Bir giriş alanı aktifken klavye kısayolları çalıştırılmıyor.

### Mevcut sınırlama

Etiketler şimdilik yalnız bellekte tutuluyor ve plot kapatılınca kayboluyor. Kalıcı kayıt,
yeniden yükleme ve segment hesaplama henüz uygulanmadı.

### Sıradaki adım: kalıcı oturum ve etiket kaydı

1. Ekleme, silme, undo ve redo işlemleri `annotation_events.jsonl` dosyasına anında yazılacak.
2. Güncel etiketler `annotations.csv` dosyasına aktarılacak.
3. Sembol, periyot, veri kimliği, pencere ayarları ve son konum `session.json` içinde tutulacak.
4. Aynı veri yeniden açıldığında oturum ve etiketler aynı global barlarda gösterilecek.
5. Çıktılar `outputs/annotations/{symbol}_{period}/{sessionId}/` altında saklanacak.
6. Segment türü, fiyat farkı, yüzdesel değişim, geçen bar sayısı ve süre hesaplanarak
   `segments.csv` üretilecek.

### Planlanan: görünür aralıkta otomatik dip/tepe bulma

Kullanıcı grafiği pan/zoom ile istediği bölgeye getirdikten sonra `Auto Detect Visible Range`
düğmesine basabilecek. Algoritma toplam veri yerine yalnız grafikte o anda görünen global X
aralığını inceleyecek.

- Yerel dip için barın `Low` değeri, sol ve sağ komşuluk içindeki düşüklerle karşılaştırılacak.
- Yerel tepe için barın `High` değeri, sol ve sağ komşuluk içindeki yükseklerle karşılaştırılacak.
- Sol/sağ doğrulama bar sayısı konfigürasyondan veya Swing Controls üzerinden ayarlanabilecek.
- Gürültüyü azaltmak için minimum yüzdesel hareket, minimum bar mesafesi ve isteğe bağlı prominence
  filtresi kullanılabilecek.
- İlk aşamada bulunan noktalar aday/önizleme olarak gösterilecek; kullanıcı onayladığında kalıcı
  etikete dönüştürülecek.
- Mevcut manuel etiketlerin üzerine yazılmayacak ve aynı bardaki mükerrer etiketler engellenecek.
- Tek otomatik tarama işlemi tek bir undo grubu olacak; `Ctrl+Z` ile topluca geri alınabilecek.
- Algoritma görünür alanın sınırlarında doğru karar verebilmek için iki taraftan doğrulama barı kadar
  ek bağlam okuyacak, ancak yalnız görünür aralık içindeki noktaları etiketleyecek.
