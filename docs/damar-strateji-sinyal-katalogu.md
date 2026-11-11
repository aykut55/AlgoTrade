# Damar Strateji — Sinyal Varyantları Kataloğu

Amaç: Faz 1 için "çok sinyal üreten" (damar) bir ham sinyal ailesi seçmek. Her aile,
indikatör tabanlı 2-4 yüksek-frekans varyantla listelenmiştir. Filtre/kalite katmanı
(TP/SL, zaman filtresi, rejim filtresi vb.) bu aşamada yok — bilerek gürültülü.

Her satırda `[Mevcut]` = repo'da zaten strateji/indikatör olarak var, referans alınabilir.
`[Yeni]` = henüz yok, sıfırdan yazılır.

**Kısıt (tüm fazları bağlar): gün içinde en az 1 işlem şart.** 3-5 gün sinyalsiz kalmak kabul
edilmiyor. Bu yüzden: (a) işlem TF kapsamı **5dk / 15dk / 1sa** ile sınırlı (1dk ve 4sa+ dışarıda —
karar verildi), (b) Faz 2 filtreleri (özellikle dip-teyidi
zarar eşiği) bu frekansı 3-5 güne yayacak kadar sıkı kalibre edilmemeli — eşik değeri Faz 3'te
"günlük işlem sayısı" metriğine karşı kontrol edilecek.

**Kişisel kısıtlar (2026-11-11):**
- **Komisyon az olmalı:** Aşırı sık işlem yapan aday elenir. Ölçüt: `OrtIslemGunuBasinaGiris` (üst sınır) ve
  `KomisyonBrutKarPayiYuzde` (komisyonun brüt kârdan aldığı pay).
- **İşlem içi aşırı zarar olmamalı:** Makul bir zarara dayanılabilir ama aşırı zarar psikolojik olarak yıpratır.
  Ölçüt: işlem bazında MAE, yani `IslemIciMaxZararYuzdeP95` / `...EnKotu` ve `IslemIciZararYuzde1AsanSayisi` /
  `...2AsanSayisi`. Sayısal eşik henüz belirlenmedi.

**Ölçüm (Statistics'e eklendi):** `ToplamIslemGunuSayisi`, `IslemYapilanGunSayisi`, `IslemsizGunSayisi`,
`EnUzunIslemsizGunSerisi` (+ başlangıç tarihi), `IslemYapilanGunOrani`, `GirisSayisi`, `OrtIslemGunuBasinaGiris`.
"Giriş" = pozisyon açılışı ya da yön dönüşü (A/S). Gün sonu kapanışı (F) giriş sayılmaz.

## Deneme protokolü (2026-11-11, her aile için aynı)

> Amaç: Ailelerin sonuçları birbiriyle **doğrudan karşılaştırılabilsin.** Bir aile denenirken aşağıdaki sabitler
> değiştirilmez. Bir sabit değişecekse önce protokol güncellenir, sonra bütün aileler yeniden koşulur.
> Ana hedef: 1 kontratta tolere edilebilir zararla para kazanmak.

**Sabitler**

| | |
|---|---|
| Veri | VIP-X030-T 5 dk. Tasarım **tüm veride (2007+)**, sonuçlar ayrıca **2021+** için de raporlanır |
| İşlem | **1 kontrat**, gerçek komisyon (bacak 20 TL), kayma 0 (kayma duyarlılığı D aşamasında) |
| Saatler | **10:00-18:00** penceresi, 18:00 barında kapat, **gün sonu kapat**. İndikatörler tüm barlardan hesaplanır |
| Çerçeve | Ortak damar iskeleti: yön kaynağı parametre, giriş modu (state / tetik), pencere, gün sonu, net TL kâr al |
| Ölçütler | Net ve brüt: getiri, işlem günü başına ortalama, en kötü gün, net DD (TL), en uzun DD süresi, kârlı gün oranı, işlem içi zarar (%95, en kötü), günlük giriş, en uzun işlemsiz seri, komisyon payı |

**Aşamalar (her aile)**

| Aşama | Ne yapılır | Geçme koşulu |
|---|---|---|
| **A. Ham tarama** | Yön modları × periyot ızgarası × giriş modu (state, tetik). Kâr al yok | Brütü pozitif bölge var mı? |
| **B. Bölge** | Isı haritası, komşu ortalaması. En iyi köşedeyse ızgara genişletilir, A tekrarlanır | Tek tepe değil, geniş bir bölge |
| **C. Kâr al** | En iyi bölgede kâr al 0 / 500 / 1000 / 2000 / 4000 TL | Neti ve/veya DD'yi iyileştiriyor mu? |
| **D. Doğrulama** | Yıl yıl sonuç, 2007-2020 ile 2021+ karşılaştırması, kayma 1 ve 2 puan | Dönemler arasında tutarlı |
| **E. Seçenek tablosu** | Aday başına: net DD / en kötü gün / günlük ortalama net (net ve brüt) | Her gün işlem + komisyon kısıtı sağlanıyor |

**Kayıt kuralı:** Her koşu sürüm ekli çıktıya yazılır (`{aile}_05_{sürüm}_opt*.csv`). Çalışma kaydına tek satırlık
özet eklenir: aile, sürüm, aşama, ayarlar, en iyi sonuç, bölge sağlam mı, kısıtlar. Finale kalan aday
`StrategyConfig.txt`'ye hazır ayar olarak girer.

**Sıra:** 1) Trailing bantlar (SuperTrend, MOST, AlphaTrend, OTT, PMax) → 2) MACD / Momentum → 3) DI+/DI- →
4) HHV/LLV → 5) Osilatörler (RSI, Stochastic) → 6) MA'nın protokolle yeniden koşulması (v1-v6 keşifti, protokole uymuyor).
En sonda bütün ailelerin E tabloları yan yana konup seçim yapılır.

## Geçmiş deneyim (bu yol haritasının motivasyonu)
Daha önce MA'larla çok denenmiş ama sonuç alınamamış. Sebep tam olarak şu: **sinyal/frekans
tarafı zaten çözülmüştü** ("çok sinyal geliyor") — eksik olan hiç kurulamayan **filtre katmanıydı**
("onu filtreleyemedim"). Sonuç: gürültü arasında yakalanmak istenen gerçek trend hareketleri
kaçırıldı ("istediğim trendleri kaçırdım"). Bu gözlem doğrudan bu dosyanın Faz 1/Faz 2 ayrımının
nedeni — MA (ve benzerleri) damar adayı olarak **dışlanmıyor**, çünkü geçmişte zaten yeterli sinyal
ürettiği görülmüş; şimdi eksik olan parça (filtre — özellikle rejim ayrımı ve dip-teyidi) üzerine
inşa ediliyor.

---

## 1. MA (Hareketli Ortalama)
- **Fast/Slow kesişim, state mode**: `fastMA > slowMA` olduğu HER barda AL, tersi SAT — kesişim
  anında değil, koşul sürdükçe sürekli sinyal. `[Mevcut]` `SimpleMAStrategy` signalModeIndex=3.
- **Fiyat–MA state**: `Close > MA` sürdükçe AL. Tek MA, en basit/en yüksek frekans. `[Mevcut]`
  signalModeIndex=1 varyasyonu.
- **Slope flip**: MA'nın yönü (artan/azalan) her değiştiğinde sinyal — kesişim yokken de üretir.
  `[Mevcut]` signalModeIndex=2.
- **Üç MA stack**: fast>mid>slow sürdükçe AL (stack sırası bozulunca SAT) — MA'ya ek bir filtre
  katmanı, biraz daha seçici. `[Yeni]`

## 2. RSI
- **Orta hat (50) kesişim, state**: RSI>50 sürdükçe AL. Klasik eşik (70/30) değil, orta hat →
  çok daha sık sinyal. `[Mevcut]` `SimpleRSIStrategy` benzer mod.
- **Eşik geçişi anlık**: RSI 30 altından yukarı kesince AL, 70 üstünden aşağı kesince SAT — tek
  seferlik, state'ten daha seyrek ama hâlâ sık (5-15dk'da RSI sık salınır).
- **RSI slope flip**: RSI'nin kendi yönü değiştiğinde sinyal (seviyeden bağımsız) — en yüksek
  frekanslı RSI varyantı. `[Yeni]`

## 3. Mum Grafikleri (Candlestick / Price Action)
- **Renk flip**: kapanış > açılış ise yeşil, tersi kırmızı; renk değiştiği her barda sinyal.
  N=1 ile en yüksek frekans. `[Yeni]`
- **N ardışık aynı yön**: son N kapanış aynı yönde ise momentum sinyali (N=2-3 küçük tut, N
  büyürse frekans düşer). `[Yeni]`
- **Engulfing / Pin bar**: klasik 2-bar yutan mum veya uzun fitilli tek bar — repo'da hazır
  pattern tanıyıcı yok, sıfırdan yazılır. Diğerlerine göre daha seyrek. `[Yeni]`
- **Body/Range oranı eşiği**: gövde/toplam-range oranı eşiği aşan her bar yön sinyali üretir
  (güçlü mum = güçlü yönlü bar). `[Yeni]`

## 4. MACD
- **Sinyal çizgisi kesişimi, state**: MACD line > Signal line sürdükçe AL. `[Mevcut]`
  `SimpleMACDStrategy`.
- **Histogram sıfır kesişimi**: histogram pozitif/negatif olduğu sürece sinyal — MACD/Signal
  kesişiminden daha erken, biraz daha sık. `[Mevcut]`/`[Yeni]` varyant olarak eklenebilir.
- **Histogram slope flip**: histogramın kendi yönü değiştiğinde sinyal (ivme değişimi) — en
  erken/en sık MACD varyantı. `[Yeni]`

## 5. Stochastic
- **%K/%D kesişimi, state**: `%K > %D` sürdükçe AL. `[Mevcut]` `SimpleStochasticStrategy`.
- **Orta hat (50) geçişi**: klasik 80/20 aşırı bölge değil, 50 orta hat — çok daha sık tetikler.
- **%K slope flip**: %K yönü değiştiğinde sinyal — seviyeden bağımsız en yüksek frekans. `[Yeni]`

## 6. SuperTrend
- **Yön flip**: SuperTrend çizgisi AL/SAT yönü değiştirdiği anda sinyal — doğası gereği zaten
  state/trailing, orta frekans (whipsaw'da sık, trendde seyrek). `[Mevcut]` `SimpleSuperTrendStrategy`.
- **Fiyat-bant mesafesi eşiği**: fiyat SuperTrend çizgisine belirli bir mesafeden yaklaşınca
  erken sinyal (flip'ten önce) — daha sık ama daha gürültülü. `[Yeni]`

## 7. DI+ / DI- (ADX ailesi)
- **DI+/DI- kesişimi, state**: `DI+ > DI-` sürdükçe AL. `[Mevcut]` `SimpleDIStrategy`.
- **ADX eşiksiz DI flip**: ADX seviyesini (trend gücü) hiç filtre olarak kullanmadan sadece DI
  kesişimi — ADX filtresi eklenince frekans düşer, bu yüzden Faz 1'de ADX'i dışarıda tut.
- **DI farkı (DI+ − DI-) slope flip**: farkın kendi yönü değiştiğinde sinyal — kesişimden önce
  tetikler, daha sık. `[Yeni]`

## 8. HHV / LLV
- **N-bar breakout**: `Close > HHV(N)[1 bar önceki]` → AL, `Close < LLV(N)[1 bar önceki]` → SAT.
  N küçük tutulursa (5-10) sık tetikler. `[Mevcut]` `SimpleHHVLLVStrategy`.
- **HHV/LLV orta nokta kesişimi**: fiyatın `(HHV+LLV)/2` orta bandını kesmesi — breakout'tan
  daha sık, daha az "temiz". `[Yeni]`
  - **Denenmiş somut varyant (2026-11-11)**: HHV/LLV ile kanal oluşturup kanalın ortasını (orta
    bandı) referans alma — tam bu kesişim mantığının kendisi, daha önce denenmiş. `[Yeni]`

## 9. Kairi (Fiyatın MA'dan % uzaklığı)
- **Sıfır kesişimi**: Kairi pozitif/negatif olduğu sürece state sinyali — MA'nın fiyat-state
  varyantına çok yakın ama normalize. `[Mevcut]` `SimpleKairiStrategy`.
- **Küçük eşik bandı (±%0.5 gibi)**: bandın içinden dışına her çıkışta sinyal — sıfır
  kesişiminden biraz daha seyrek ama daha az gürültü.
- **Kairi slope flip**: Kairi'nin kendi yönü değiştiğinde sinyal. `[Yeni]`

## 10. Göstergesiz / Ham Fiyat Yapısı (indikatör yok)
- **Bar değişim yönü (close-close)**: `Close[i] > Close[i-1]` → AL, tersi SAT, her bar — tüm
  katalogdaki en yüksek frekanslı ve en basit sinyal, saf gürültüye en yakın. `[Yeni]`
- **Bar değişim yönü (close-open, aynı bar)**: barın kendi içindeki yön (yeşil/kırmızı mum ile
  aynı şey, ama MA/indikatöre hiç bağlı değil). `[Yeni]`
- **N-bar getiri eşiği (burst)**: son N barlık toplam değişim ±%X'i geçerse yön sinyali —
  "ani hareket" yakalar, sabit eşiğe göre sık/seyrek ayarlanır. `[Yeni]`
- **Günün ilk bar açılışı/yönü**: seans başındaki ilk bar yeşil/kırmızı ise günün geri kalanı
  için tek seferlik yön sinyali (günde 1 sinyal — damar değil ama diğerleriyle birleşmek için
  "gün filtresi" olarak değerli). `[Yeni]`
- **Açılış fiyatına göre state (opening range)**: `Close > günün açılış fiyatı` sürdükçe AL —
  intraday'de MA'sız trend state'i, 5-15dk için doğal bir aday. `[Yeni]`
- **Gap yönü**: `Open[bugün] - Close[dün]` farkının yönü — günde 1 sinyal, gün-içi damar için
  tek başına yetersiz ama günlük/1sa TF'de anlamlı. `[Yeni]`
- **Önceki gün H/L kırılımı**: `Close > dünün High'ı` → AL, `Close < dünün Low'u` → SAT —
  indikatörsüz breakout, HHV/LLV'nin "gün" versiyonu. `[Yeni]`
  - **Denenmiş somut varyant (2026-11-11)**: günün açılış mumundan hemen değil, **N'inci bardan**
    (örn. 5. veya 10. bar) itibaren kontrole başla — ilk N bar gürültüsünü/false-breakout'u
    bilerek atlar. Yön aynı: dünün Low'unu yukarıdan aşağı keserse **short**, dünün High'ını
    aşağıdan yukarı keserse **long** (yukarıdaki kuralla birebir aynı yön mantığı, sadece giriş
    zamanlaması "gün açılışından N bar sonra"ya kaydırılmış). 5-15dk'da N=5-10 bar ≈ ilk 25-150
    dakika. `[Yeni]`
- **Bar içi range konumu**: kapanışın bar range'i içindeki yeri (üst/alt yarı) ardışık N barda
  aynı tarafta kalırsa sinyal — mum rengi flip'ten biraz daha "kaliteli" ama hâlâ indikatörsüz.
  `[Yeni]`

> Not: Bu aile en "ham" olanı — MA/RSI gibi hesaplama gecikmesi (lag) sıfır, tamamen anlık fiyat
> yapısına bakıyor. Frekans en yüksek ama muhtemelen en gürültülü de bu grup olacak.

## 11. Diğer mevcut aileler (ekstra, istersen)
- **Bollinger**: orta bant (SMA) state kesişimi en sık; üst/alt bant dokunuşu daha seyrek.
  `[Mevcut]` `SimpleBollingerStrategy`.
- **Momentum (ROC)**: sıfır kesişimi state. `[Mevcut]` `SimpleMomentumStrategy`.
- **CMF / MFI**: sıfır (CMF) veya orta hat (MFI 50) kesişimi state. `[Mevcut]`
- **ParabolicSAR**: flip anı, doğası gereği orta frekans. `[Mevcut]`
- **MOST / OTT / PMax / AlphaTrend**: SuperTrend ailesi gibi trailing-band flip, orta frekans.
  `[Mevcut]`
- **Ichimoku**: Tenkan/Kijun kesişimi (state) veya fiyat-bulut kesişimi. `[Mevcut]`
- **WaveTrend / Mavilim W / Tillson T3**: kendi çizgi/sinyal kesişimi state. `[Mevcut]`

---

## Frekans sıralaması (kabaca, yüksekten alçağa)
1. Renk flip / slope flip varyantları (MA, RSI, MACD histogram, Stochastic %K, DI farkı, Kairi) —
   seviyeden bağımsız, her yön değişiminde tetikler → en sık.
2. State mode kesişimler (fast/slow MA, RSI 50, Stoch 50, MACD/Signal, DI+/-, Kairi sıfır) —
   koşul sürdükçe her bar, ama koşul değişimi daha seyrek.
3. Eşik/orta hat tek-seferlik geçişler (RSI 30/70, N-bar breakout) — en seyrek bu grupta.
4. Trailing-band flip'ler (SuperTrend, MOST, OTT, PMax, ParabolicSAR) — doğası gereği en seyrek,
   ama trend rejiminde "doğru" sinyal kalitesi daha yüksek olabilir.

## Faz 1: Damar seçimi (şimdiki adım)
Yukarıdakilerden 1-2 aday seçip (örn. MA state + RSI slope flip) 5dk/15dk/1sa üzerinde
TimeframeScanner (menü 11) ile IslemSayisi'ne göre sıralayıp ham frekansı gözlemlemek.
Kalite filtresi (PFNet, Win%) bu turda sadece "çöp mü değil mi" seviyesinde bakılır, asıl filtre
ayıklaması Faz 2'de.

## Faz 2: Filtre Katmanı (sıradaki aşama — henüz detaylandırılmadı)
Damar sinyal üretmeye devam eder; filtre katmanı sinyali **iptal/onayla** kararı verir (sinyal
üretimiyle aynı yerde değil, ayrı bir karar katmanı). Olası filtre kategorileri — bunları da
tek tek, iteratif olarak detaylandıracağız:
- **Zaman filtresi**: gün içi saat penceresi, haftanın günü, açılış/kapanış yakınlığı ("ilk N bar
  işlem yok" gibi). Repo'da `timeFilteringEnabled` zaten var.
- **Rejim filtresi (TREND vs OSİLASYON) — ikinci öncelik**: piyasa her an bu iki halden birinde —
  rejimi önce tanımak, sonra damar+dip-teyit mantığını rejime göre ayarlamak istiyoruz ("akıllı
  mantık"). Taslak yaklaşım:
  - **Rejim tespiti**: ADX seviyesi (yüksek=trend, düşük=osilasyon) veya ATR/Bollinger bant
    genişliği (dar=osilasyon, genişleyen=trend) ile her bar bir rejim etiketi üretilir.
  - **Trend rejiminde**: dip-teyit mantığı olduğu gibi işler — geri çekilme (pullback) = asıl
    trend yönünde giriş fırsatı, mevcut Faz 2 planıyla tutarlı.
  - **Osilasyon rejiminde**: "zarar sonrası aynı yönde gir" mantığı ters tepebilir — range'de
    fiyat genelde dönüyor, pullback'in dibi sayılan yer trend değil range'in diğer ucu olabilir.
    Burada muhtemelen ZIT yönde (mean-reversion) teyit daha doğru — ama bu ayrı, sonradan
    netleştirilecek bir dal.
  - Pratik ilk adım: rejimi sadece **ölçüp loglamak** (henüz davranışı değiştirmemek), Faz 3'teki
    getiri eğrisini rejime göre ayırıp ("trend günleri" vs "range günleri") dip-teyit mantığının
    hangisinde işe yaradığını görmek — rejim ayrımı olmadan tek bir ortalama sonuca bakmak
    yanıltıcı olur.
  - **Önemli kısıt**: 5-15dk'nın kendi verisiyle "trend mi osilasyon mu" güvenilir anlaşılamıyor
    (gürültü çok, rejim o çözünürlükte net ayrışmıyor) — muhtemelen üst TF'den (1sa/4sa/günlük)
    beslenmesi gerekecek. Ama güvenilir olmasa da **kayıtlara düşmesi isteniyor** — yani rejim
    etiketi (ADX/ATR tabanlı, hangi kaynaktan geliyorsa) her satırda loglanacak, ilk etapta
    kararları etkilemese de Faz 3 analizinde geriye dönük kesinleştirilecek.
- **Trend filtresi (üst TF teyidi)**: örn. 1sa trend yönüyle ters 5dk sinyalini iptal et — gerçek
  multi-TF değil, ayrı okunan üst TF sonucunu statik bir koşul olarak kullanmak.
- **Hacim teyidi**: OBV/CMF/MFI yönü sinyal yönüyle uyuşmuyorsa iptal.
- **Sanal pozisyon teyidi — dip teyidi (ÖNCELİKLİ, karar verildi)**: damar+filtre sinyali gelince
  pozisyon hemen gerçek açılmaz; sanal (paper) takip edilir. Sanal pozisyon belirli bir zarar
  eşiğine ulaşınca (piyasa yapıcı shakeout'u tamamlandığı varsayımıyla) **aynı yönde** gerçek
  pozisyon açılır. Repo'da birebir hazır: `VirtualPositionConfirmer`
  (`src/AlgoTrade.Core/Trading/Core/VirtualPositionConfirmer.cs:153-174`) `Trigger =
  ConfirmationTrigger.LossOnly` + `LossThreshold = -X` (negatif), `ConfirmingSingleTrader`/
  `ConfirmingMultipleTrader` (menü 22-25) üzerinden çalıştırılır. Rasyonel: piyasa yapıcı önce
  retail'i ters yöne sürükleyip sonra asıl hareketi yapıyor varsayımı — zararı görüp sonra girmek
  bu "sürüklemeyi" bekleyip atlatmak.
- **Çoklu sinyal konsensüsü**: birden fazla damar varyantı aynı yönde anlaşmazsa iptal.
  Repo'da `MultipleTrader` consensus modları (`All`/`Majority`/`Any`/`Net`, menü 3/6) hazır.
- **Equity-curve filtresi**: son dönem performansı kötüyse stratejiyi geçici devre dışı bırak.
  Repo'da `EquityCurveFilterConfig.txt` hazır altyapı.

Faz 2'ye geçmeden önce Faz 1'de seçilen damar(lar)ın ham sinyal sayısı/dağılımı netleşmeli.
Amaç filtrelerle **gürültüyü** azaltmak — damarın ürettiği sinyal sayısını değil, kötü/anlamsız
sinyalleri elemek.

## Faz 3: Getiri Eğrisi İnceleme (damar+filtre kombinasyonunu değerlendirme)
Seçilen damar + filtre kombinasyonu çalıştırılınca ortaya çıkan **getiri eğrisini** (equity
curve) ve işlem bazlı anlık kâr/zararı incelemek. Bakılacaklar:
- **Anlık kâr/zarar dağılımı**: tek işlem büyüklükleri — aşırı uç (çok büyük tek kazanç/kayıp)
  eğriyi şişiriyor mu? (`KarZararFiyatList`, SingleTrader çıktısı)
- **Toplam getiri (Net/Gross)**: `GetiriFiyatNet` / `GetiriFiyatYuzdeNet` — komisyon sonrası
  gerçek sonuç.
- **Getiri eğrisinin şekli**: düzenli mi (sürekli yukarı) yoksa birkaç büyük işlemin taşıdığı mı
  — grafik olarak `DearPyGuiDataPlotter`/`DearImGuiBundleDataPlotter` ile görsel kontrol (menü
  5/2, `PlotEnabled=true`).
- **MaxDD ve DD süresi**: `GetiriMaxDD` — eğri ne kadar derin/ne kadar uzun süre dibe gidiyor.
- **PFNet / Win%**: filtre öncesi/sonrası karşılaştırma — filtre gerçekten gürültüyü mü azalttı
  yoksa sadece işlem sayısını mı düşürdü (getiri/DD oranı iyileşmediyse filtre işe yaramamış).

Bu faz tek seferlik değil: filtre eşiklerini ayarlayıp getiri eğrisine tekrar bakıp iteratif
ince ayar yapılacak döngü. Sayısal karşılaştırma için Config_03 opt akışı / `singleTraderOptLog`
kullanılabilir; görsel karar için plotter.
