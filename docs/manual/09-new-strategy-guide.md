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
  seviyesi, 5=Anlık yüzde. Her mod `Trader.flags?.XEnabled == true` korumalı.
- **`flatModeIndex`/`skipModeIndex`/`ruleModeIndex`**: placeholder, hep 0, okunmuyor.
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

## Kısıtlar

- **ASLA** `git commit` çalıştırma — kullanıcı kendi commit atıyor.
- Build her zaman doğrulanmalı (`dotnet build AlgoTrade.sln`), 0 error şart.
- Tasarım kararları (signalModeIndex menüsü gibi) için kısa onay iste; mekanik rollout için
  (dosyadan dosyaya aynı satırı ekleme) durmadan devam et.
- Yeni bir isim seçerken `SimpleMAStrategy`/`SimpleMACrossStrategy` gibi kazara duplicate'e
  düşmemeye dikkat (bkz. `../PROJECT_ANALYSIS.md` "İsimlendirme notu").
