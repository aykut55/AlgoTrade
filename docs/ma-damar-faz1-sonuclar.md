# MA Damar: Faz 1 Ölçüm Kaydı

> Amaç: `docs/damar-strateji-sinyal-katalogu.md`'deki Faz 1'i (damar seçimi) MA ailesi için yürütmek.
> Bu dosya, çalışmanın yöntemini, her koşunun ayarlarını, sonuçlarını ve sıradaki adımları tutar.
> Yeni bir oturumda kaldığın yerden devam etmek için önce burayı oku.
> Son güncelleme: 2026-11-11 (v4 sonuçları: ilk net pozitifler; net DD hatası bulundu).

## 1. Kısıtlar (kullanıcının, değişmez)

| Kısıt | Ölçüt (Statistics alanı) |
|---|---|
| Her gün en az 1 işlem (3-5 gün işlemsiz kabul edilmez) | `EnUzunIslemsizGunSerisi`, `IslemYapilanGunOrani` |
| Komisyon az olmalı | `OrtIslemGunuBasinaGiris`, `KomisyonBrutKarPayiYuzde` |
| İşlem içi aşırı zarar olmamalı (makul zarara dayanılır) | `IslemIciMaxZararYuzdeP95`, `...EnKotu`, `IslemIciZararYuzde1AsanSayisi` |
| Zarar kes YOK (kullanıcı zararda oturur, gün sonu kapanışı sınırlar) | - |

Kontrat ve komisyon ayarları **gerçek piyasa değerleri** (`Config_03`): bir bacak 20 TL, gidiş-dönüş
40 TL, yani yaklaşık **4 endeks puanı** (1 puan = 10 TL). Net sonuçlar gerçekçi kabul edilir.

**Başabaş eşiği:** İşlem başına brüt kâr (`GetiriPuan / GirisSayisi`) **≥ 4 puan** olmalı.
Bu, sonuçları okumanın en hızlı yolu.

## 2. Araçlar

- **Strateji:** `src/AlgoTrade.Core/Trading/Strategies/CustomStrategies/Strategy002_MaDamar.cs`
  - `signalModeIndex` (yön): 0 Fiyat/FastMA, 1 Fiyat/SlowMA, 2 **Fast/Slow**, 3 FastMA eğim,
    4 SlowMA eğim, 5 **Stack** (fast > mid > slow). Mid = round(√(fast·slow)) (`midPeriod = 0`).
    Bütün modlar state biçiminde: her bar yön üretir, işlem yalnızca yön dönünce olur.
  - `firstEntryModeIndex` (pozisyon BOŞKEN giriş): 0 **state** (koşul geçerliyse hemen, sabah
    gap'li açılıştan), 1 **kesişim** (yön aynı günün iki barı arasında döndüğünde), 2 **tetik**
    (yön seçilen moddan, giriş fiyatın `triggerPeriod`'luk EMA'yı aynı gün içinde yön doğrultusunda
    kestiği barda). Pozisyon AÇIKKEN hepsinde state geçerli (yön dönerse pozisyon çevrilir).
  - `takeProfitTL`: > 0 ise komisyon sonrası net TL kâr al (eşik = hedef + gidiş-dönüş komisyon,
    mevcut `KarZararFiyatSeviyesindenKarAlHesapla` ile). 0 = kapalı.
  - `triggerPeriod`: tetik EMA periyodu.
  - Not: EMA'da mod 0 ≡ mod 3 ve mod 1 ≡ mod 4 (`slopeLookback = 1` iken). Tek barlık eğimin
    işareti, fiyatın EMA'ya göre konumuyla aynı. Bu bir hata değil, matematiksel bir eşitlik.
- **Koşu:** `inputs/scripts/Config_03_SingleTraderOpt.csx`, `optChoice = 27`. Menü: `[8]` → `[3]`
  (`03_RunSingleTraderOptWithProgressAsync.csx`, `SingleTraderOptimizer`). Blok içinde
  `maDamarTf` (zaman dilimi) ve `maDamarRun` (sürüm eki) ayarlanır.
- **Çıktı:** `outputs/opt/maDamar_{tf}_{run}_opt*.csv` (`;` ayırıcı, UTF-8 BOM). Uygulama açılışta
  yalnızca `outputs/logs`'u temizler, `outputs/opt` korunur. `appendEnabled = true` olduğu için
  parametre listesi değişince **sürüm eki mutlaka artırılmalı**, yoksa sütunlar kayar.
- **Ortak ayarlar:** VIP-X030-T, gün sonu pozisyon kapat açık, EMA, zarar kes yok.
  Veri: **2021-01-01 sonrası** (1453 işlem günü). v4 de 2021 sonrası (kullanıcı kararı: önceki koşularla
  karşılaştırılabilsin). Tüm veriye (2007-06 → 2026-10, 562.894 bar) geçmek için `Config_03`'te
  `maDamarAllData = true`. Bu, seçilen damarın doğrulama adımı olarak ayrıca yapılacak.
- **Seans uzunluğu (5 dk):** 2021 sonrası günde **153 bar** (09:25-22:55). 2020 öncesi daha kısa
  (işlem listesinde yıllık işlem sayısı 2020'de ~1.400'den ~2.100'e sıçrıyor). Yani aynı bar sayısı
  eski yıllarda daha uzun bir süreyi kapsıyor. Fiyat 2007'de ~548, bugün ~14.000. Sabit 4 puanlık
  komisyon eski yıllarda çok daha ağır. Tüm veride toplam sonuç dönemlerin karışımıdır, en iyi adaylar
  yıl yıl incelenmeli.
- **Analiz:** pandas ile CSV okunur. Kullanılmayan parametreler yüzünden tekrarlayan satırlar
  (örn. mod 0'da slowPeriod) `drop_duplicates` ile atılır.

## 3. Koşular ve bulgular

### v1: 5 dk ve 15 dk, state giriş, kâr al yok
- 5 dk: fast 5-30, slow 20-80. 15 dk: fast 3-15, slow 10-40. 6 mod.
- **Hiçbir kombinasyonda net getiri pozitif değil.** Brüt pozitif: 5 dk'da 135/178, 15 dk'da 151/151.
- İşlem başına brüt kâr: 5 dk en iyi 2.53 puan, 15 dk en iyi 3.16 puan (eşik 4).
- State + gün sonu kapatma her sabah yeniden giriş yaptığı için kısıt 1 kendiliğinden sağlanıyor
  (%100 gün). Bedeli günde en az bir gidiş-dönüş komisyonu.
- Fiyat/Fast ve Fiyat/Slow: günde 9-43 giriş, komisyonda eriyor. **Elendi.**
- Aday: Fast/Slow ve Stack. 15 dk'da en iyi: Stack 12/35, net -%23.

### v2: 5 dk, state ve kesişim girişi
- Kesişim, Fast/Slow'da netini 38 kombinasyonun 37'sinde iyileştirdi (30/80: -%77 → -%44).
  Günlük giriş tam 1 azaldı, en kötü işlem içi zarar küçüldü (%-7.8 → %-5.7).
- Bedeli: brüt düştü (sabah gap girişleri ortalamada kâr bırakıyordu) ve **kısıt 1 bozuldu**:
  günlerin %15-20'si işlemsiz, en uzun seri 3 gün.
- Stack'te kesişim karışık sonuç verdi (24/38 iyileşti, en iyisi -%47 → -%58).
- Kısa periyot kısıtı düzeltiyor ama kaliteyi düşürüyor. ≤ 1 günlük işlemsiz seri sağlayan en iyi
  kombinasyon -%104 net.

### v3: 5 dk, state / kesişim / tetik × kâr al 0/500/1000/1500 TL (2016 kombinasyon)
- **Tetik modu açık ara en iyi.** Kısıt 1'i sağlıyor (en uzun seri 0-1 gün), kalite de en yüksek.
- En iyi: **Stack + tetik, 30/80, kâr al yok** → net **-%4.5**, brüt +%114, işlem başına brüt
  **3.85 puan**, günde 2.0 giriş, işlem içi zarar %95 dilimi %-1.55, en kötü %-8.3.
- Fast/Slow + tetik 30/80 → net -%21, ama işlem içi en kötü zarar **%-5.7** (psikolojik olarak daha rahat).
- **Kâr al zarar veriyor:** Her mod ve girişte en kötüsü 500 TL, en iyisi 0. 1500 TL, 0'a yaklaşıyor.
  Kazanç az sayıda büyük işlemden geliyor, kâr al onları kesiyor. Kâr al en kötü işlem içi zararı da
  artırıyor (%-6 → %-11/-12).
- **Isı haritası:** Net, fast ve slow büyüdükçe düzenli iyileşiyor. En iyi değer ızgaranın KÖŞESİNDE
  (fast = 30, slow = 80 üst sınırlar). Gerçek en iyi büyük ihtimalle daha uzun periyotlarda.

### Zaman dilimleri arası tutarlılık
Saat cinsinden en iyiler benzer: hızlı MA yaklaşık **2.5-3.75 saat**, yavaş MA yaklaşık **7-10 saat**
(5 dk 30/80, 15 dk 15/40 ve 12/35). Bu, sonucun tek bir zaman dilimine özgü olmadığına işaret.

## 4. Değerlendirme ilkeleri (aşırı uyuma karşı)

- Opt'u "en iyi parametreyi seç" diye değil, **"hangi yapı genel olarak işe yarıyor"** diye oku.
- Tek bir satıra güvenme: **komşuların ortalamasına** ve ısı haritasının biçimine bak. Tepeler sivri
  (komşu ortalaması en iyiden 20-30 puan kötü), ama eğilim düzenli.
- Izgara kenarındaki bir en iyi, ızgaranın yetersiz olduğunu gösterir, genişlet.
- Seçilen damar **görülmemiş veride** doğrulanmadan (2021 öncesi yüzde bazında ya da son aylar
  ayrılarak) Faz 2/3'e geçilmez.
- 50/100/200 gibi klasik değerler günlük grafiğe özgü. 5 dk'da 200 bar bir seanstan uzun, gün sonu
  kapatma ve gap eleme kurgusuyla çelişir. İstenirse "periyot seti" indeksiyle ayrıca karşılaştırılır.

### v4: 5 dk, tetik girişi, fast 20-60 / slow 60-140, tetik 3/6/9, kâr al 0/2000/4000 (900 kombinasyon)
- **İlk kez net pozitif:** Fast/Slow + Stack'in 432 farklı sonucunun **186'sında net getiri > 0**.
- En iyi: **Fast/Slow 30/140, tetik 3, kâr al yok** → net **+%64.7**, brüt +%187, işlem başına brüt
  **6.12 puan** (komisyon 4), günde 2.1 giriş, **her gün işlem var**, kârlı işlem oranı %28, PFNet 1.02,
  işlem içi zarar %95 dilimi %-1.54, en kötü %-8.3. Komisyon brüt kârın %65'i.
- Stack'in en iyileri benzer (50/120: +%49.9) ama işlem içi en kötü zarar **%-14**. Psikolojik kısıt açısından Fast/Slow daha iyi.
- **Pozitif bölge geniş:** Fast/Slow tetik 3'te slow 100-140 / fast 30-60 bölgesinin büyük kısmı pozitif
  (+%20 ile +%65). Stack tetik 3'te fast ≥ 30, slow ≥ 80 bölgesi neredeyse tamamen pozitif. Tek sivri bir tepe değil.
- **Tetik 3 en iyi** (6 ve 9 belirgin kötü). 3 ızgaranın alt sınırı, yani 1-2 de denenmeli.
  slow için 140 üst sınır (seans sınırı 153 bar).
- **Kâr al yine zararlı:** En iyisi 0. 4000 TL, 0'a yakın. 2000 TL en kötüsü.
- **Hata bulundu ve düzeltildi: `GetiriMaxDDNet` her yerde %100 çıkıyordu.** Net DD tarihi 2021.01.04 09:30,
  yani ilk bar. Neden: `SingleTrader.Run()` ilk barda (`i < 1`) erken döndüğü için `CalculateBalance(0)`
  çalışmıyor. Brüt ve puan listeleri `UpdateVariablesOnNewIteration`'daki başlatma bloğunda ilk bakiyeye
  ayarlanıyor, ama net listeler (`BakiyeFiyatNetList` vb.) ayarlanmıyordu, 0 kalıyordu. Düzeltme: aynı bloğa
  net listeler eklendi. v1-v4 CSV'lerindeki `GetiriMaxDDNet` değerleri geçersiz. Brüt DD (`GetiriMaxDD`)
  doğru: en iyide %17.
  Yan bulgu (dokunulmadı): `BakiyePuanNetList` hiçbir barda doldurulmuyor, `BakiyePuanNet` hep 0.
- **Net bakiye derin bir çukurdan geçiyor:** En iyide `MinBakiyeFiyatNet` = 67.012 TL (başlangıç 100.000), yani
  net bakiye bir noktada başlangıcın %33 altına inmiş, sonra 169.410'a çıkmış. Gerçek net DD en az ~%33. Bu,
  psikolojik kısıt için önemli.
- **Kenar payı ince:** PFNet 1.02, komisyon brüt kârın %65'i. `kaymaMiktari = 0`. Gerçek kayma
  (slippage) bacak başına 1 puan olsa gidiş-dönüş 2 puan daha gider ve kalan kenarın yaklaşık 1/3'ü erir.

### Menü 5 doğrulaması (v4 en iyisi, `StrategyConfig.txt` → `Strategy002_MaDamar|v1`)
- Opt ile birebir aynı: net +%64.69, 3053 giriş (günde 2.10), işlem içi zarar %95 %-1.544 / en kötü %-8.34.
- **Gerçek net DD: %33.03** (2022.10.26), DD hatası düzeltildikten sonra. Brüt DD %17.03 (2023.12.06).
  Net bakiye dibi 67.012 TL. Net DD brütün yaklaşık 2 katı, çünkü komisyon kayıplı dönemde de birikiyor.
- İşlem içi zararı %1'i aşan 415 işlem, %2'yi aşan 62 işlem var (toplam 3053).
- Menü 5 için ayarlar: `AppConfig.json` SingleTrader → `Strategy002_MaDamar` v1 (v2 = Stack 50/120),
  `GunSonuPozKapatEnabled = true`, `ReadData` = AfterDateTime 2021.01.01. **ReadData genel bir ayar,
  diğer menüleri de etkiler.** Eski değerleri dosyada yorum olarak duruyor.

### Veri uyarısı: vade geçişi (roll) sıçramaları
- `VIP-X030-T` sürekli seri. **Çift ayların son iş günü saat 19:00 barında** fiyat bir sonraki vadeye
  atlıyor: 2021-2026'da 28 kez, ortalama **+%4.56** (en büyüğü 2023.04.28 +%10.4). Ocak başı 09:25
  açılışlarında da benzer sıçramalar var (Aralık vadesi). Bunlar piyasa hareketi değil, vade primi.
- Referansın **en kötü 5 işleminin hepsi** bu barlarda short (örn. 2024.06.28 short, -8.592,5 TL).
- Bu 28 barda stratejinin brüt K/Z toplamı **-22.642 TL** (16 kayıp, 12 kazanç). Yani sıçramalar sonucu
  şişirmemiş, aşağı çekmiş. Ama işlem içi en kötü zarar ve en kötü gün istatistikleri büyük ölçüde bu yapay
  sıçramalardan geliyor.
- Yapılacak: vade geçişi barlarını ayarlanmış (back-adjusted) seriyle ya da vade günü 19:00 öncesi pozisyon
  kapatarak yeniden test etmek. Gerçek işlemde vade günü pozisyon da zaten bu şekilde (roll) yönetilmeli.

### Referansın zararları (2021+)
- 3.052 işlemin 1.765'i zararlı (ortalama -541 TL), 1.272'si kârlı (ortalama +898 TL). Art arda en fazla 10 zararlı işlem.
- En kötü gün -10.255 TL (2024.06.28, vade günü), en kötü hafta -11.930 TL, en kötü ay -15.950 TL (2026.08).
  Günlerin 760/1453'ü, ayların 38/70'i zararlı.
- Net DD: tepe 2021.01.11 (100.062 TL) → dip 2022.10.26 (67.012 TL, %-33) → toparlanma 2023.08.09.
  Yani **yaklaşık 2.5 yıl** başlangıç sermayesinin altında.
- Yıllık bileşik net getiri **%8.98**. Aynı dönemde endeks 1.663 → 13.904 (×8.36, yıllık %44).
  TL bazında endeksin ve muhtemelen enflasyonun çok altında. Not: 1 kontratın nominali ~139.000 TL, bu da
  100.000 TL sermayeyle ~1.4 kaldıraç demek. Getiri yüzdesi sermaye varsayımına bağlı.

### v5: v4 ile aynı ızgara + işlem penceresi 10:00-18:00 (trader zaman filtresi, Config_03 `maDamarSeansPenceresi`)
- **Pencere stratejiyi bozdu:** Net pozitif 186/432 (v4) → **1/432** (v5). Aynı kombinasyonların 431'inde v5 daha kötü.
- Referans (Fast/Slow 30/140, tetik 3, TP 0): net +%64.7 → **+%2.6**, brüt/işlem 6.12 → 4.10 puan,
  **net DD %59** (v4 %33), net bakiye dibi 40.882 TL.
- İşlem içi en kötü zarar değişmedi (%-8.34): En kötü işlem içi nokta vade sıçramasından değil, pencere içindeki
  gerçek bir hareketten geliyor.
- Yorum: Kazancın önemli bir kısmı 18:00-23:00 akşam seansından ve/veya 09:25-10:00'den geliyor. 18:00'de
  kapatmak kârlı trendlerin kuyruğunu kesiyor.
- Not: v1-v4 CSV'lerindeki `GetiriMaxDDNet` (%100) DD düzeltmesi öncesine ait, geçersiz. v5'tekiler gerçek.
- Sonraki: Referansın K/Z'sini saat saat ayırmak (menü 5 + bar listesi). Sonra pencere yerine nokta atışı:
  (a) yalnızca vade günü (çift ayın son iş günü) 18:55'te kapat, (b) 09:25-10:00 giriş yok, akşam seansı kalsın.

### Yön değişikliği: kullanıcının penceresi (2026-11-11)
Kullanıcı akşam seansında ve gece pozisyon tutmak istemiyor (sosyal hayat). İşlem penceresi gündüz (10:00-18:00).
Bu yüzden **v4 referansı (+%64.7) bu kullanıcı için geçerli değil**, kazancı akşam seansından geliyor.
Parametreler doğrudan pencere içinde seçilecek. Karar: EMA'lar tüm barlardan hesaplanır (akşam seansı bilgi
olarak kullanılır), pozisyon yalnızca pencerede tutulur.

### Kurallar stratejiye taşındı (2026-11-11)
- `Strategy002_MaDamar`'a `gunSonuKapat`, `islemPenceresi`, `pencereBaslangic`, `pencereBitis` ("HH:mm" / "HH:mm:ss") eklendi.
  `StrategyConfigLoader` değerdeki `:`'yi kabul edecek şekilde değişti (`Split(':', 3)`).
  Pencere kuralı trader filtresiyle aynı: başlangıç ≤ saat < bitiş, bitiş barında kapat.
- Config'ler aslına döndü: `AppConfig.json` oturum öncesi hali, `Config_03` optChoice varsayılanı 1 (MA damar için 27 seç).
  optChoice 27 bloğu artık trader şalterlerini değiştirmiyor, gün sonu ve pencere `fixedParams` ile veriliyor.
- `StrategyConfig.txt`: v1 / v2 artık `gunSonuKapat:bool:true` içeriyor (v4 referansını üretir). Yeni **v3** = v1 + pencere 10:00-18:00
  (v5'teki aynı kombinasyonu üretmeli).
- Menü 5'te MA damar çalıştırmak için `AppConfig.json`'da strateji adı ve sürümünü elle seçmek gerekiyor (config aslına döndüğü için).

### v6 (hazırlandı): parametreler 10:00-18:00 için
Tetik girişi sabit, mod 2-5, fast 5/15/25/35, slow 20/50/80/110/140, tetik 2/3/4, kâr al 0/500/1000/1500/2000 TL.
Yaklaşık 1.200 kombinasyon. Çıktı `maDamar_05_v6_opt*.csv`.
**Tasarım ilkesi (kullanıcı):** Tasarım tüm veri ve tüm seanslar üzerinde yapılır, kısıtlar (saat penceresi vb.)
ilgili yerlerde filtre olarak uygulanır. Bu yüzden v6 **tüm veride (2007+)** koşuluyor (`maDamarAllData = true`).
v1-v5 (2021+) ile doğrudan karşılaştırılamaz. Karşılaştırma için v6'nın en iyileri ayrıca 2021+ yıl yıl ayrılacak.

## 5. Sıradaki adımlar (v4 sonrası)

1. ~~`GetiriMaxDDNet` hatasını düzelt~~ (yapıldı).
2. **En iyi adayı tek başına çalıştır** (menü 5, `AppConfig.json`'daki SingleTrader ayarlarını kullanır):
   getiri eğrisi, yıl yıl net, gerçek net DD (67.012 TL'lik dip ne zaman?), plot.
3. **Görülmemiş veri:** Aynı adayları 2007-2020'de çalıştır (`maDamarAllData` ya da DateTimeRange).
4. **Kayma duyarlılığı:** `kaymaMiktari` 1-2 puan ile tekrar.
5. Tetik 1-2 ve 15 dk'da tetik modu.

## v4 (tamamlandı): plan

5 dk, **tetik girişine sabit**, **2021 sonrası** (v1-v3 ile karşılaştırılabilir), ızgara uzun tarafa genişletildi:
- `signalModeIndex` 2-5 (3/4 aralık ardışık olduğu için koşuyor, elenmiş modlar)
- `fastPeriod` 20-60 (adım 10), `slowPeriod` 60-140 (adım 20; 140 bar ≈ 11.7 saat, tek seans içinde)
- `triggerPeriod` 3 / 6 / 9
- `takeProfitTL` 0 / 2000 / 4000 (küçük kâr al zararlıydı, büyük değerler deneniyor)
- Yaklaşık 900 kombinasyon, ~8 dk.
- Çıktı: `maDamar_05_v4_opt*.csv`

v4'te bakılacaklar: (1) İşlem başına brüt kâr komisyonu geçiyor mu, net pozitif çıkıyor mu?
(2) Isı haritasında ızgaranın içinde bir tepe oluşuyor mu? (3) triggerPeriod'un etkisi.
(4) Uzun periyotlarda işlem içi en kötü zarar ne kadar? (5) v3 (2021+) en iyileriyle aynı bölge mi
öne çıkıyor? (6) En iyi adayların yıl yıl davranışı (tek koşu + işlem listesi ile).

Sonra (doğrulama): seçilen adaylar `maDamarAllData = true` ile tüm veride koşulup yıl yıl incelenecek.
O sonuçlar v1-v4 ile doğrudan karşılaştırılamaz. "Komisyon = 4 puan" eşiği yalnızca bugünkü fiyat
seviyesinde geçerli, TL net getiriye ve yıl yıl ayrıma bakılmalı.

## 6. Açık sorular

- İşlem içi zarar için sayısal eşik (kullanıcı henüz vermedi). Örnek öneri: giriş fiyatının %1'i ≈ 140 puan.
- 3 günlük işlemsiz seri kabul edilebilir mi? (Tetik modunda sorun kalmadı, ama kesişim modu için açık.)
- 15 dk ve 60 dk için tetik modu henüz denenmedi.
- Seçilen damarın 2021 öncesi verideki davranışı (yıl yıl, yüzde bazında).
