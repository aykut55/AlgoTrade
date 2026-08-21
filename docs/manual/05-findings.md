# Findings — Notable Discoveries from the Variant Audit

> [04-variant-catalog.md](04-variant-catalog.md) taradığı 9 alandan çıkan **en çarpıcı, aksiyon
> gerektirebilecek** bulguları burada ayrı topladık — katalog geniş bir checklist, bu dosya onun
> "öncelik sırasına dizilmiş özeti". Her bulgu için: ne bulundu, neden önemli, önerilen sonraki
> adım. Kaynak: kod taraması (2026-08-21), hiçbir dosya çalıştırılıp test edilmedi — "gerçek
> veriyle hiç denenmemiş" tespiti sadece config/script dosyalarında o değerin hiç geçmemesine
> dayanıyor.

## 1. 🔴 Timing Filter mekanizması fiilen erişilemez — muhtemelen bug

**Bulgu**: `SingleTrader.ApplyTimingFilters()` (`SingleTrader.cs:2252-2257`) içinde
`int filterMode = 1;` **hardcoded**. `CheckOrderTimeEligibility()` (`SingleTrader.cs:2079`) 7 farklı
mod destekliyor (0-6: filtre yok / saat aralığı / tarih aralığı / datetime aralığı × "sadece
başlangıç" varyantları) ama bu değeri config'ten ya da script'ten değiştirecek **hiçbir alan
yok**. Üstüne, taranan tüm `AppConfig*.json` dosyalarında `TimeFilteringEnabled = false` — yani
mekanizmanın tamamı (hardcoded mod 1 dahil) zaten kapalı.

**Neden önemli**: Bu, "tasarlandı ama denenmedi" değil, "tasarlandı ama **kullanılamaz**" — 6
moddan hiçbiri şu an config'ten hiçbir şekilde tetiklenemiyor. Bilinçli bir kısıtlama mı yoksa
unutulmuş bir TODO mu belirsiz.

**Önerilen adım**: `AppConfig`'e (`SingleTraderConfig.Signals` altına, `TimeFilteringEnabled`'ın
yanına) bir `FilterMode: int` (veya daha okunabilir bir `enum`) alanı ekleyip
`ApplyTimingFilters()`'taki hardcoded `1`'i oradan okumak — küçük, izole bir değişiklik. Karar
senin: bilinçli bir kısıtlama ise bunu dokümante etmek yeterli, unutulmuşsa açmak gerekir.

## 2. 🟡 24 stratejiden sadece 3'ü gerçek config'te kullanılmış

**Bulgu**: `inputs/configs/StrategyConfig.txt`'te tanımlı somut strateji sayısı **3**
(`SimpleMAStrategy`, `SimpleMostStrategy`, `SimpleRSIStrategy` — toplam 5 parametre-seti). Kod
tabanında **24 strateji sınıfı** var (bkz. [PROJECT_ANALYSIS.md](../PROJECT_ANALYSIS.md) §3.3).
Yani 21 strateji (OTT/HOTTLOTT/PMax/PTT/SuperTrend/Ichimoku/Bollinger/MACD/AlphaTrend/Kairi/HYLY
vb.) **hiç gerçek veriyle çalıştırılmamış** — sadece kod olarak var, derleniyor ama hiç bar-bar
koşulmamış.

**Neden önemli**: 66 MA yöntemi × OTT-ailesi kombinasyonu da bu yüzden tamamen doğrulanmamış alan
(bkz. katalog §6) — OTT-ailesi stratejilerin (`SimpleOTTStrategy`, `SimplePMaxStrategy`)
kendisi hiç config'e girmediği için, varsayılan MA yöntemleri (örn. OTT için VIDYA) bile hiç
gerçek veriyle test edilmemiş.

**Önerilen adım**: Kısa vadede acil değil (kod derleniyor, strateji altyapısı test edilmiş —
sadece bu 21 strateji "kullanılmamış" konumda). Ama bir strateji karşılaştırma çalışması
yapılacaksa (bkz. [todo.md](../todo.md) "Strateji Karşılaştırma" bölümü), önce bu 21 stratejinin
en azından birer kez çalıştırılıp hatasız bar-bar koştuğunun doğrulanması iyi bir ilk adım olur.

## 3. 🟡 RunMode'un TradeAndQuery/QueryOnly hâlleri hiç config'te seçilmemiş

**Bulgu**: Taranan **tüm 35** `RunMode` referansında (`AppConfig*.json` dosyaları toplamında)
değer hep `TradeOnly`. `TradeAndQuery`/`QueryOnly` sadece `inputs/scripts/Config_02_MultipleTrader.csx`
gibi script-config dosyalarında **örnek** olarak geçiyor, gerçek `AppConfig.json`'da hiç.

**Neden önemli**: Query altyapısı (`IQuery`/`BaseQuery`/`QueryRegistry`) kod olarak çalışır
durumda ama Console menüsünden gerçek bir "TradeAndQuery" veya "QueryOnly" koşumu muhtemelen hiç
yapılmamış — sorgu sisteminin gerçek Console akışında sorunsuz çalıştığı doğrulanmamış.

**Önerilen adım**: Bir `AppConfig.json`'da `RunMode: "TradeAndQuery"` deneyip `[3]`/`[6]` üzerinden
gerçek bir koşum yapmak, sorgu sütunlarının beklendiği gibi üretildiğini görmek.

## 4. 🟡 EquityCurveFilter'ın 7 versiyonundan 6'sı hiç seçilmemiş

**Bulgu**: `EquityCurveFilterConfig.txt`'te v1-v7 arası 7 versiyon tanımlı, ama gerçek
çalıştırmalarda hep **v1 = disabled** kullanılmış. v2-v7 (aktif profit/loss eşiği + farklı
`ConfirmationTrigger` kombinasyonları) tanımlı ama hiç seçilmemiş.

**Neden önemli**: Equity curve konfirmasyon mekanizması (`ApplyEquityCurveFilter`,
`SingleTrader.ResolveFilterDecisions`'daki öncelik sırasının bir parçası — bkz.
[PROJECT_ANALYSIS.md](../PROJECT_ANALYSIS.md) §2.2) gerçek anlamda hiç "aktif" halde
test edilmemiş; sadece "kapalı" hali doğrulanmış.

**Önerilen adım**: v2-v7'den birini seçip gerçek veriyle bir koşum — özellikle
`ConfirmationTrigger` = Profit/Loss/Both farklarının beklenen şekilde çalıştığını görmek için.

## 5. 🟡 ConsensusMode: Majority/All/Any hiç denenmemiş

**Bulgu**: Bugün eklenen `CustomConsensusFunc` + hep var olan `Net` dışında, **Majority/All/Any**
modları hiçbir `AppConfig.json`'da seçilmemiş.

**Neden önemli**: 3 hazır modun (`MultipleTrader.BuildConsensusSignal()`, bkz.
[01-class-reference.md](01-class-reference.md) §3) matematiksel olarak doğru implement
edildiğini biliyoruz (kod okundu, mantık basit) ama gerçek veride farklı davranış üretip
üretmediği hiç gözlemlenmedi.

**Önerilen adım**: `CustomConsensusExample.csx`'teki `MajorityConsensusReference`/
`AllConsensusReference`/`AnyConsensusReference` referans metodlarını (zaten script'te hazır
duruyor) sırayla aktif edip gerçek veride birer koşum — düşük efor, script zaten yazılmış.

## 6. 🟢 Slippage ve Market Type'lar iyi doğrulanmış — kontrol örneği

Karşıt örnek olarak: `KaymaMiktari: 0.5` **238 referansta** geçiyor, tüm 14 `MarketType`
config'lerde kullanılmış. Bu ikisi katalogdaki "✅ iyi doğrulanmış" örnekleri — yani proje
genelinde her varyant "hiç denenmemiş" değil, bazı alanlar gerçekten olgun. Bu bulgu listesi
sadece **boşluklara** odaklanıyor, genel resmi çarpıtmasın diye buraya not düşüldü.

## 7. ⛔ Pyramiding'in pozisyon limiti config'ten hiç set edilemiyor

**Bulgu**: `PyramidingEnabled` `AppConfig`'e bağlı (`AppConfig.cs:157`) ama hep `false`. Daha
önemlisi: `MaxPositionSizeEnabled`/`MaxPositionSize`/`MaxPositionSizeMicro`
(`InitialTradeParams.cs:145-160`) **`AppConfig.cs`/`AppConfigApplier.cs`'te hiç yok** — yani
Pyramiding açılsa bile pozisyon büyüklüğü limiti config'ten asla set edilemez, sadece
script/kod içinden `InitialTradeParams` nesnesine doğrudan erişilerek set edilebilir.

**Neden önemli**: Pyramiding "tam implement" deniyordu (migration-guide.md) ama config
katmanından erişilemeyen bir güvenlik limiti (`MaxPositionSize`) olması, pyramiding'i Console/
AppConfig üzerinden güvenle açılabilir bir özellik olmaktan çıkarıyor — sadece script'ten elle
kurulursa güvenli.

**Önerilen adım**: Pyramiding gerçekten kullanılacaksa, `MaxPositionSize*` alanlarının
`AppConfig`'e eklenmesi öncelikli olmalı (limitsiz pyramiding riskli).

---

## Özet Öncelik Sırası (aksiyon alınacaksa)

1. Timing Filter hardcode'u — bilinçli mi unutulmuş mu netleştir (§1)
2. Pyramiding güvenlik limiti config'e taşınmalı, açılmadan önce (§7)
3. Kalanlar (§2-5) — düşük risk, sadece "denenmemiş", zaman buldukça teker teker doğrulanabilir

## İlgili Dosyalar

- [04-variant-catalog.md](04-variant-catalog.md) — bu bulguların çıktığı tam katalog (9 alan, tüm detay tablolar)
- [01-class-reference.md](01-class-reference.md) — bahsi geçen sınıfların API referansı
- [03-scripting-guide.md](03-scripting-guide.md) — §5, `CustomConsensusExample.csx`'teki hazır referans metodlar
