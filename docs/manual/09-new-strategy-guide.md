# Yeni Bir Simple*Strategy Yazma Rehberi

> Bu dosya, `src/AlgoTrade.Core/Trading/Strategies/` altında **henüz yazılmamış/yeniden
> yazılmamış bir strateji** için (yeni bir indikatör sarmalarken, ya da eski `choice`-bazlı bir
> strateji "ortak mimariye" taşınırken) baştan sona ne yapılması gerektiğini anlatır. 2026-08-31'de
> `SimpleMostStrategy`'den başlayıp 21 stratejiye uygulanan rewrite işinden çıkarılan, kalıcı
> referans olsun diye buraya taşınan format. `../todo.md`'deki o işin kendi görev-günlüğü (tarihli,
> "iş bitti" notlarıyla) — bu sayfa onun yerine YAŞAYAN, tarihsiz kural seti.

## Hazır prompt (kopyala, yeni bir konuşmada `[İndikatör Adı]` yerine gerçek ismi yazıp yapıştır)

```
docs/manual/09-new-strategy-guide.md'yi oku. Oradaki "Ortak mimari" ve "Turnkey rollout deseni"ni
takip ederek Simple[İndikatör Adı]Strategy'yi (src/AlgoTrade.Core/Trading/Strategies/) MOST/OTT/
SuperTrend/RSI referans mimarisine göre anahtar teslim yaz/yeniden yaz: signalModeIndex (0-7) +
exitModeIndex (0-5) + flatModeIndex/skipModeIndex placeholder + (mantıklıysa) priceSource +
OHLCV/length-guard + iki değil TEK constructor (data/indicators'lı — parametresiz ctor artık
YAZILMIYOR, bkz. rehberdeki not). signalModeIndex menüsünü tasarlarken indikatörün şeklini (dual-
line mi, tek-line mı, 0-100 osilatör mü, direction-array mı) MOST/RSI/SuperTrend'den hangisine
benziyorsa ona göre uyarla, farklıysa doc comment'te neden farklı olduğunu belirt. Build kontrolü
(dotnet build AlgoTrade.sln) yap. Sonra rollout: Config_01/Config_03'e ekleyip aktif yap (kullanıcı
Menu[8]→1 ve Menu[8]→3 ile test edecek) + tüm diğer config/script dosyalarına yay (rehberdeki
dosya listesi) — GenerateReplaySampleBundles.csx/playlist.json'da önce zaten var mı kontrol et,
CustomConsensusExample.csx'e eklerken kaç child olacağını ve pedagojik amaç kaybı riskini göz
önünde bulundur. Tam rollout mu yoksa sadece çekirdek mi istendiğini emin değilsen sor.
```

## Referans dosyalar (kopyala-uyarla kaynağı)

Şu dosyalar zaten bu mimariye göre yazılmış, en yakın olanı şablon olarak kullan:

- **`SimpleMostStrategy.cs`** — iki-satırlı gösterge (`most`/`exmov`), `priceSource` var. En genel referans.
- **`SimpleOTTStrategy.cs`** / **`SimpleSuperTrendStrategy.cs`** / **`SimpleParabolicSARStrategy.cs`** —
  trend-flip ailesi (trailing-stop + Direction/Trend array). ATR/SAR gibi High/Low'a bağımlı
  göstergelerde `priceSource` İNDİKATÖRE eklenmez, sadece OnStep'in `source` serisine eklenir
  (bkz. SuperTrend'in doc comment'i).
- **`SimpleRSIStrategy.cs`** / **`SimpleMFIStrategy.cs`** / **`SimpleCMFStrategy.cs`** /
  **`SimpleKairiStrategy.cs`** / **`SimpleMomentumStrategy.cs`** — 0-100 ya da 0-merkezli tek-seri
  osilatör ailesi (threshold/midline crossover + state + band + retest + confirmation + slope
  combo).
- **`SimpleMACDStrategy.cs`** / **`SimpleStochasticStrategy.cs`** / **`SimpleIchimokuStrategy.cs`** —
  iki-seri (dual-line) göstergeler, MOST'un `most`/`exmov` çiftinin analogu.
- **`SimpleBollingerStrategy.cs`** / **`SimpleATRStrategy.cs`** / **`SimpleHHVLLVStrategy.cs`** —
  üst/orta/alt kanal (channel-breakout) ailesi.
- **`SimpleADXStrategy.cs`** / **`SimpleDIStrategy.cs`** — aynı indikatörü (`ADXWithDI`) paylaşan
  ama filtre eklenmiş/eklenmemiş iki varyant örneği.
- **`SimpleTillsonT3Strategy.cs`** / **`SimpleAlphaTrendStrategy.cs`** — ikinci bir referans serisi
  OLMAYAN tek-hat göstergeler; mod 1 için özgün bir varyant (eğim-teyitli kırılım, ya da eski
  stratejinin orijinal mantığının korunması) gerekir.

## Ortak mimari

- **`signalModeIndex` (0-7)**: 0=ana kırılım/kesişim (genelde eski `choice=0` ile aynı), 1=ikinci
  seri kesişimi (yoksa uyarla — bkz. SuperTrend/SAR/PMax'ın Direction-flip çözümü, ya da T3/
  AlphaTrend'in özgün varyantı), 2=indikatörün kendi slope flip'i, 3=state (kesişim değil, her
  bar), 4=band/uzaklık filtresi, 5=breakout+retest, 6=confirmation bars, 7=slope+state combo.
- **`exitModeIndex` (0-5)**: `Trader.karAlZararKes` üzerinden — kod MOST'takiyle BİREBİR AYNI,
  değişmez: 0=Seviye/seviyeli, 1=Yüzde/seviyeli, 2=Seviye/tek, 3=Yüzde/tek, 4=Anlık fiyat
  seviyesi, 5=Anlık yüzde. Her mod `Trader.flags?.XEnabled == true` korumalı. Mod 4/5'in sabit
  (hardcoded) hedef değerleri ctor parametresi olmalı: `karAlFiyatSeviyesi`/`zararKesFiyatSeviyesi`/
  `karAlYuzdesi`/`zararKesYuzdesi` (2026-11'de SuperTrend/MA'ya eklendi, bkz. aşağıdaki
  "Değişiklik Taşıma" bölümü).
- **`takeProfitExitModeIndex`/`stopLossExitModeIndex`/`flatModeIndex`/`skipModeIndex`/
  `ruleModeIndex` default'u `-1`, `0` DEĞİL** (2026-11'de düzeltildi) — `0` gerçek/aktif bir mod
  olduğu için default bırakılırsa, `Signals.KarAlEnabled`/`ZararKesEnabled` yanlışlıkla `true` iken
  istenmeyen bir mod sessizce devreye girer. `-1` hiçbir dispatch dalıyla eşleşmediği için güvenli
  "kapalı" sentinel'i. (`buySignalModeIndex`/`sellSignalModeIndex` bu kuralın DIŞINDA — `0` onlar
  için gerçek/istenen varsayılan davranış, landmine değil, `0` kalmalı.)
- **`takeProfitExitModeEnabled`/`stopLossExitModeEnabled`/`flatModeEnabled`/`skipModeEnabled`**:
  ctor içinde AÇIKÇA `true` set edilmeli (BaseStrategy default'u zaten `true`, ama örtük bırakmak
  yerine açıkça yazmak tutarlılık için şart — bkz. SuperTrend/MA).
- **`priceSource`**: indikatör mantıksal olarak tek bir fiyat serisine MA/hesap uyguluyorsa hem
  indikatöre hem OnStep'in `source`'una bağlanır. İndikatör yapısal olarak OHLC'ye bağımlıysa
  (ATR/True Range, SAR, ADX/DI gibi) İNDİKATÖRÜ DEĞİŞTİRME — sadece OnStep `source`'unu besler
  (ya da indikatörün kendi hesaplaması için hiç kullanılmaz, `SimpleADXStrategy`/`SimpleDIStrategy`
  gibi).
- **OnInit**: `Indicators.GetDataCount()` + tüm OHLCV/date alanları + `allSeriesLengthsMatch`
  uzunluk kontrolü (throw `InvalidOperationException` uyuşmazsa).
- **TEK constructor**: `(List<StockData> data, IndicatorManager indicators, ...params)`.
  **Parametresiz/data'sız constructor artık YAZILMIYOR** — 2026-08-31'de tüm eski stratejilerden
  kaldırıldı çünkü `StrategyRegistry.CreateFromBestMatchingConstructor` zaten ilk iki parametrenin
  `List<StockData>`/`IndicatorManager` olmasını zorunlu tutuyor, o ctor hiç çağrılamıyordu (bkz.
  `StrategyRegistry.cs`).
- `using static AlgoTrade.Core.Trading.Utils.Utils;` → `YukarıKesti`/`AsagiKesti`/`Buyuk`/`Kucuk`
  (hem dizi-dizi hem dizi-skaler overload'ları var — skaler overload RSI'nin oversold/overbought
  gibi seviye kesişimleri için).
- Sinyal önceliği hep aynı: Skip > Flat > TakeProfit > StopLoss > Buy > Sell > None.

## Turnkey rollout deseni

1. İndikatörü oku (`src/AlgoTrade.Core/Trading/Indicators/...`), yukarıdaki referans dosyalardan
   hangisine en çok benzediğini belirle, signalModeIndex 0-7 menüsünü tasarla.
2. Strategy `.cs` dosyasını şablona göre yaz/yeniden yaz.
3. `dotnet build AlgoTrade.sln -c Debug --nologo` — 0 error şart.
4. `Config_01_SingleTrader.csx`'e yeni `strategyChoice` branch'i ekle VE aktif değeri buna ayarla
   (kullanıcının hızlı test yolu: Menu[8]→1, `01_RunSingleTraderWithProgressAsync.csx`).
5. `Config_03_SingleTraderOpt.csx`'e yeni `optChoice` branch'i ekle VE aktif değeri buna ayarla
   (Menu[8]→3, `03_RunSingleTraderOptWithProgressAsync.csx`).
6. Tam rollout isteniyorsa (kullanıcıya sor, otomatik varsayma) — bir önceki stratejinin geçtiği
   HER dosyayı grep'le bul, aynı satırı yeni strateji için ekle: `Config_02/06/07`,
   `mainScript(+Simplified)`, `mainScriptMultipleTrader(+Simplified)`,
   `runSingleTraderWithStrategy.csx`, `runMultiTraderWithStrategies.csx`, `paramSweep.csx`,
   `StrategyConfig.txt` (v1 Default, v2 opsiyonel), `OptimizationConfig.txt`,
   `GenerateReplaySampleBundles.csx`, `inputs/python/offlineReplay/playlist.json` (SON İKİSİNDE
   ÖNCE zaten var mı kontrol et — bazı stratejiler o listelerde önceden duruyor olabilir, mükerrer
   ekleme).
7. `CustomConsensusExample.csx`'e eklenecekse: yeni child (id = önceki en yüksek + 1), `AddChild`
   çağrısı. Child sayısı arttıkça özel (index'e bağlı) örnek consensus kurallarının generic
   modlarla (All/Majority/Net/Any) aynılaşıp pedagojik farkını kaybedip kaybetmediğini kontrol et
   — gerekirse o örneği bilinçli olarak bir ALT KÜMEYE (örn. "ilk N child") sabitle.
8. `AlgoTrade.Console/Program.cs`'e DOKUNMA (oradaki fallback'ler Most+MA'yı hiç eşleştirmiyor,
   kasıtlı atlanıyor).
9. Grep ile parite doğrulaması: önceki strateji adı vs yeni strateji adı, dokunulan her dosyada
   eşit sayıda geçmeli (sample-bundle listeleri hariç — orada asimetri normal olabilir, önceden
   var olup olmadığını kontrol ederek yorumla).
10. Kısa özet ver. Memory'e (varsa) bir node ekle.

## Değişiklik Taşıma / Parite Kontrol Listesi

> Bu bölüm **yeni strateji yazmak** için değil — **bir stratejide (örn. SuperTrend) yapılan bir
> değişikliği/düzeltmeyi başka bir stratejiye (örn. MA) veya hepsine taşırken** kullanılır.
> Kullanıcı "bunu diğerlerine de uygula" dediğinde bu listeyi sırayla kontrol et, mekanik kopyala-
> yapıştır yapma — her madde için referans dosyanın (şu an referans: `SimpleMAStrategy.cs`) o
> maddedeki hâlini örnek al, hedef dosyayı ona göre düzelt, farkı kullanıcıya özetle.

1. **Using'ler**: hedef dosyada referans dosyada olmayan, görünüşte ilgisiz/kullanılmayan using
   var mı (örn. görsel/3. parti kütüphane using'i, strateji mantığıyla ilgisiz)? Varsa işaretle,
   silmeden önce sor.
2. **Field deklarasyon sırası**: field'ların sırası ctor imza sırasıyla AYNI olmalı (örn.
   `[indikatöre-özgü parametreler] → priceSource → karAl/zararKes grubu → [diğerleri]`). Sadece
   ctor body'sini düzeltip field deklarasyonunu unutmak kolay — ikisini birlikte kontrol et.
3. **Ctor imza sırası**: `[indikatöre-özgü parametreler], priceSource, karAlFiyatSeviyesi,
   zararKesFiyatSeviyesi, karAlYuzdesi, zararKesYuzdesi, buySignalModeIndex, sellSignalModeIndex,
   takeProfitExitModeIndex, stopLossExitModeIndex, flatModeIndex, skipModeIndex, ruleModeIndex`.
4. **Ctor body atama sırası**: `this.X = X;` satırları imza sırasıyla birebir aynı sırada olmalı.
5. **`Parameters[...]` dict sırası**: aynı şekilde imza sırasıyla birebir aynı.
6. **Enabled flag'leri** (`takeProfitExitModeEnabled`/`stopLossExitModeEnabled`/`flatModeEnabled`/
   `skipModeEnabled`): ctor'da açıkça `true` mu set ediliyor? (Bir strateji bilerek farklıysa —
   örn. sadece buy/sell — bu kasıtlı olmalı ve yorumla belirtilmeli, sessizce unutulmuş olmamalı.)
7. **ModeIndex default'ları**: exit/flat/skip/rule default'u `-1` mi (yoksa unutulup `0`'da mı
   kalmış)? `buySignalModeIndex`/`sellSignalModeIndex` `0`'da mı kaldı (onlar `-1` OLMAMALI)?
8. **karAl/zararKes parametreleri**: mode 4/5 dallarında hardcoded sayı (`1000.0`, `-500.0`,
   `3.0`, `-2.0` gibi) kaldı mı, yoksa hepsi ctor parametresine mi bağlandı?
9. **`OnInit()` try/catch**: `NotImplementedException` için try/catch + 2 satır `LogWarning`
   (ikinci satır doğru indikatör dosya yolunu göstermeli) + catch içinde tüm ilgili diziler
   (field tipine uygun, örn. `int[]` olan `direction` gibi alanlar `double[]` yapılmamalı) boş
   array ile dolduruluyor mu?
10. **`OnStep()` iskeleti** — şu sıra korunmalı: 6 bool deklarasyonu → `ResolveRunContext` →
    zaman/tarih pencere değişkenleri → erken-return guard'ları → "Geçerli ve önceki değerler"
    bloğu → `isOneMinute`/.../`isSonYonA/S/F` placeholder bloğu (AÇIKLAYICI YORUMLARIYLA birlikte,
    yorumsuz bırakılmasın) → buy/sell dispatch → takeProfit/stopLoss dispatch (mode 0-5) →
    flat/skip placeholder → **SİNYAL GATE'İ yorumu + `Trader?.signals?.X==false` satırları** →
    zaman/tarih gate'i → **"Sinyal önceliklendirmesi" banner'ı + "Not: 6 sinyal de..." açıklaması
    (bu banner GATE'DEN SONRA, asıl `if(skip){...}` zincirinden HEMEN ÖNCE olmalı — gate'ten önce
    DEĞİL, bu kolay kayan bir hata)** → öncelik zinciri (Skip>Flat>TP>SL>Buy>Sell>None).
11. **Trailing accessor'lar**: `IsValidParameterCombination()` (strateji-özgü mantık varsa XML
    doc comment'i olmalı), `GetX()`/property getter'lar, `GetPlotIndicators()` — doc-comment
    stili ve varlık/yokluk tutarlı mı.
12. **`StrategyConfig.txt`/`OptimizationConfig.txt` v1-v7(+preset) şeması**: koddaki karAl/
    zararKes parametreleştirmesi yapıldıktan sonra, iki config dosyasında da strateji için AYNI
    versiyon numaralandırması/DisplayName'leri kurulmalı: `v1=Default` (sadece al/sat),
    `v2=KarAl`, `v3=ZararKes`, `v4=KarAlZararKes`, `v5=KarAlYuzde`, `v6=ZararKesYuzde`,
    `v7=KarAlZararKesYuzde` (+ varsa `v100` gibi strateji-özgü preset/varyantlar, karAl/zararKes
    numaralandırmasının DIŞINDA, ayrı bir blok olarak). `StrategyConfig.txt`'te tek sabit değer
    (`karAlFiyatSeviyesi:double:5500` gibi), `OptimizationConfig.txt`'te range (`karAlFiyatSeviyesi:
    500:15000:500` gibi) — iki dosyada da `fixed:takeProfitExitModeIndex:int:4/5` ve
    `fixed:stopLossExitModeIndex:int:4/5` eşlik etmeli. Kalıntı `signalModeIndex` alanı (hedef
    stratejide gerçek karşılığı yoksa, bkz. madde 7) bu satırlardan temizlenmeli.

Bu kontrolü yaptıktan sonra bulduğun farkları (hangi madde, hangi dosyada, ne farklı) kullanıcıya
listele, sonra hangisini düzelteceğini sor — otomatik "hepsini düzelt" yapma, kullanıcı bazılarının
kasıtlı olabileceğini (örn. MA'nın exit'leri default kapalı tasarımı gibi) bilir, sen bilemezsin.

## Kısıtlar

- **ASLA** `git commit` çalıştırma — kullanıcı kendi commit atıyor.
- Build her zaman doğrulanmalı (`dotnet build AlgoTrade.sln`), 0 error şart.
- Tasarım kararları (signalModeIndex menüsü gibi) için kısa onay iste; mekanik rollout için
  (dosyadan dosyaya aynı satırı ekleme) durmadan devam et.
- Yeni bir isim seçerken `SimpleMAStrategy`/`SimpleMACrossStrategy` gibi kazara duplicate'e
  düşmemeye dikkat (bkz. `../PROJECT_ANALYSIS.md` "İsimlendirme notu").
