# Varyant Kataloğu — Kodda Var Ama Gerçek Veriyle Denenmemiş Olabilecek Konfigürasyonlar

> **Amaç**: Proje tasarımı zengin — birçok alanda (consensus modu, equity curve tetikleyicisi,
> konfirmasyon çakışma davranışı, market tipi, MA yöntemi vb.) birden fazla varyant kodlanmış.
> Ama bir varyantın **kodda yazılmış olması**, onun **gerçek veriyle çalıştırılıp doğrulandığı**
> anlamına gelmiyor. Bu dosya, statik kod/config taramasıyla çıkarılmış bir **envanter/checklist**:
> her varyant için "config'te/script'te gerçekten kullanılmış kanıtı var mı" sorusuna cevap
> arıyor. **Hiçbir varyant burada fiilen çalıştırılıp test edilmedi** — sadece "hazır ama
> denenmemiş" mi, "zaten kullanılıyor" mu diye statik kanıt toplandı.
>
> **Nasıl kullanılır**: Bir varyantı gerçek veriyle deneyip doğruladığında, durumunu ✅ yap ve
> kanıt sütununa hangi run/dosya ile doğrulandığını yaz. Zamanla bu tablo "hepsi doğrulandı"
> durumuna büyüsün diye tasarlandı.
>
> Analiz tarihi: 2026-08-21. Kaynak: `inputs/configs/AppConfig/*.json` (17 dosya),
> `inputs/configs/*.txt`, `inputs/scripts/*.csx` içinde grep taraması + ilgili `.cs` dosyaları.

## Durum lejantı
- ✅ **Kullanılmış** — gerçek bir config dosyasında veya script'te bu değer set edilmiş (ama "set edilmiş" ≠ "o run'ın gerçekten trade ürettiği doğrulandı" — sadece config'e girdiği doğrulandı).
- ⚠️ **Sadece kodda, hiç kullanılmamış** — enum/metod/parametre kodda tanımlı ama hiçbir config/script dosyasında referans edilmemiş.
- ⛔ **Erişilemez (hardcoded engel var)** — kodda birden fazla varyant olsa da, config'ten hiçbir zaman değiştirilemiyor; her zaman tek bir sabit değer çalışıyor.

---

## 1. MultipleTrader / ConfirmingMultipleTrader — ConsensusMode

Kaynak: `MultipleTrader.cs:49,209-286` (`BuildConsensusSignal()`), config şeması `AppConfig.json`
`MultipleTrader.Consensus` / `ConfirmingMultipleTrader.Consensus`.

| Varyant | Nasıl aktif edilir | Kod konumu | Durum |
|---|---|---|---|
| Net (varsayılan) | `"Consensus": {"Mode": "Net"}` | `MultipleTrader.cs:249-253,278-291` | ✅ Tüm 2 `Consensus` bloğunda (`AppConfig.json`) da `"Mode": "Net"` set edilmiş. |
| Majority | `"Consensus": {"Mode": "Majority"}` | `MultipleTrader.cs:237-245` | ⚠️ Hiçbir `AppConfig*.json` dosyasında `Mode` alanı `"Majority"` olarak set edilmemiş — sadece yorum satırlarında (`// Mode : Net \| Majority \| All \| Any`) ve `CustomConsensusExample.csx`'teki referans/comment-out edilmiş atama satırında geçiyor. |
| All | `"Consensus": {"Mode": "All"}` | `MultipleTrader.cs:247-254` | ⚠️ Aynı — sadece yorumlarda geçiyor, hiç set edilmemiş. |
| Any | `"Consensus": {"Mode": "Any"}` | `MultipleTrader.cs:256-265` | ⚠️ Aynı — sadece yorumlarda geçiyor, hiç set edilmemiş. |
| `CustomConsensusFunc` (script) | `[8] Run Script` üzerinden `multipleTrader.CustomConsensusFunc = ...` | `MultipleTrader.cs:62,211-217` | ✅ `CustomConsensusExample.csx` bu oturumda gerçek veriyle (1.9M bar) çalıştırıldı — `FirstChildWinsConsensus` aktif olarak doğrulandı; script içindeki `NetConsensusReference/MajorityConsensusReference/AllConsensusReference/AnyConsensusReference/WeightedConsensus/BothAgreeConsensus` fonksiyonları yazılı ama sadece derleme kontrolünden geçti, hiçbiri **aktif kural olarak** gerçek veriyle çalıştırılmadı (script'te comment-out). |

---

## 2. EquityCurveFilter — Threshold Tipi × Trigger Kombinasyonları

Kaynak: `SingleTrader.ApplyEquityCurveFilter()` (`SingleTrader.cs:2271`), `ConfirmationTrigger` enum
(`SingleTrader.cs:22-27`: `ProfitOnly=0, LossOnly=1, Both=2`), config dosyası
`inputs/configs/EquityCurveFilterConfig.txt`.

**Kritik bulgu**: `EquityCurveFilterConfig.txt` 7 versiyon tanımlıyor (v1-v7, percent/absolute ×
ProfitOnly/LossOnly/Both'un tam kombinasyonu) — ama **her 16 `AppConfig.json` referansı da
`"Version": "v1"`'e sabitlenmiş**, ve `v1 = enabled:false` (devre dışı). Yani proje genelinde
equity curve filtresi **her zaman kapalı** çalışıyor.

| Varyant | Config'teki karşılığı | Durum |
|---|---|---|
| Devre dışı (v1) | `EquityCurveFilterConfig.txt` satır: `v1\|Disabled\|enabled:bool:false\|...` | ✅ **Tek fiilen kullanılan versiyon** — tüm `AppConfig*.json` dosyalarındaki `EquityCurveFilter.Version` alanları `"v1"`. |
| Percent + Both (v2) | `v2\|PercentBoth\|enabled:bool:true\|thresholdType:string:percent\|...\|trigger:string:Both` | ⚠️ Config dosyasında tanımlı ama hiçbir `AppConfig*.json`'da `Version: "v2"` seçilmemiş. |
| Percent + ProfitOnly (v3) | `v3\|PercentProfitOnly\|...\|trigger:string:ProfitOnly` | ⚠️ Tanımlı, hiç seçilmemiş. |
| Percent + LossOnly (v4) | `v4\|PercentLossOnly\|...\|trigger:string:LossOnly` | ⚠️ Tanımlı, hiç seçilmemiş. |
| Absolute + Both (v5) | `v5\|AbsoluteBoth\|...\|thresholdType:string:absolute\|...\|trigger:string:Both` | ⚠️ Tanımlı, hiç seçilmemiş. |
| Absolute + ProfitOnly (v6) | `v6\|AbsoluteProfitOnly\|...` | ⚠️ Tanımlı, hiç seçilmemiş. |
| Absolute + LossOnly (v7) | `v7\|AbsoluteLossOnly\|...` | ⚠️ Tanımlı, hiç seçilmemiş. |

**Not**: `ConfirmationTrigger` enum'u ayrıca `ConfirmingSingleTrader`/`ConfirmingMultipleTrader`'ın
"sanal pozisyon konfirmasyonu" (`Confirmation` config bloğu) için de kullanılıyor — o kullanım
**ayrı bir konu**, bkz. §3. Orada `Trigger: "Both"` gerçekten set edilmiş durumda.

---

## 3. ConfirmingSingleTrader / ConfirmingMultipleTrader — Konfirmasyon Davranışı

Kaynak: `Trading/Core/VirtualPositionConfirmer.cs:8-20` (`SignalConflictMode`), config bloğu
`AppConfig.json` → `ConfirmingSingleTrader.Confirmation` / `ConfirmingMultipleTrader.Confirmation`
(toplam 2 blok, sadece ana `AppConfig.json`'da var — market-özel `AppConfig_*.json` dosyalarında
Confirming bölümleri yok).

| Varyant | Nasıl aktif edilir | Kod konumu | Durum |
|---|---|---|---|
| `SignalConflictMode.CancelAndRestart` | `"ConflictMode": "CancelAndRestart"` | `VirtualPositionConfirmer.cs:14` | ✅ Her 2 `Confirmation` bloğunda da bu set edilmiş — "eski projenin davranışı" olarak bilinçli seçilmiş varsayılan. |
| `SignalConflictMode.LockAndIgnore` | `"ConflictMode": "LockAndIgnore"` | `VirtualPositionConfirmer.cs:19` | ⚠️ Hiçbir config'te `LockAndIgnore` set edilmemiş — kod var, hiç denenmemiş. |
| `FlattenImmediatelyOnFlatSignal = true` | `"FlattenImmediatelyOnFlatSignal": true` | `AppConfig.json:424` (Confirmation bloğu) | ✅ Her 2 blokta da `true`. |
| `FlattenImmediatelyOnFlatSignal = false` | `"FlattenImmediatelyOnFlatSignal": false` | aynı alan | ⚠️ Hiç `false` olarak denenmemiş. |
| `ConfirmationTrigger.Both` (konfirmasyon tetikleyicisi) | `"Trigger": "Both"` | `AppConfig.json:423` | ✅ Her 2 blokta da `Both`. |
| `ConfirmationTrigger.ProfitOnly` / `.LossOnly` | `"Trigger": "ProfitOnly"` / `"LossOnly"` | aynı alan | ⚠️ Hiç denenmemiş (Confirmation bağlamında — §2'deki EquityCurveFilter'daki v3/v4/v6/v7'den bağımsız, ayrı bir kullanım noktası). |

---

## 4. InitialTradeParams — MarketTypes / SetKontratParams* Ailesi

Kaynak: `Trading/Core/InitialTradeParams.cs:5-24` (14 üyeli `MarketTypes` enum),
`SetKontratParams<Type>()` metodları (13 tane, satır 403-620).

| MarketType | SetKontratParams metodu | Config'te kullanımı | Durum |
|---|---|---|---|
| BistEndex | `SetKontratParamsBistEndex` | `"MarketType": "BistEndex"` (`AppConfig_BistEndex.json` ve `AppConfig.json` içinde) | ✅ |
| BistHisse | `SetKontratParamsBistHisse` | `AppConfig_BistHisse.json` içinde | ✅ |
| BistParite | `SetKontratParamsBistParite` | `AppConfig_BistParite.json` içinde | ✅ |
| BistMetal | `SetKontratParamsBistMetal` | `AppConfig_BistMetal.json` içinde | ✅ |
| ViopEndex | `SetKontratParamsViopEndex` | `AppConfig_ViopEndex.json`, ayrıca `inputs/scripts/*.csx` içinde de doğrudan çağrılıyor (bkz. `02_RunMultipleTraderWithProgressAsync.csx`, `CustomConsensusExample.csx`) | ✅ En çok script'te elle çağrılan varyant. |
| ViopHisse | `SetKontratParamsViopHisse` | `AppConfig_ViopHisse.json` | ✅ |
| ViopParite | `SetKontratParamsViopParite` | `AppConfig_ViopParite.json` | ✅ |
| ViopMetal | `SetKontratParamsViopMetal` | `AppConfig_ViopMetal.json` | ✅ |
| FxEndex | `SetKontratParamsFxEndex` | `AppConfig_FxEndex.json` | ✅ |
| FxHisse | `SetKontratParamsFxHisse` | `AppConfig_FxHisse.json` | ✅ |
| FxParite | `SetKontratParamsFxParite` | `AppConfig_FxParite.json` | ✅ |
| FxMetal | `SetKontratParamsFxMetal` | `AppConfig_FxMetal.json` | ✅ |
| FxCrypto | `SetKontratParamsFxCrypto` | `AppConfig_FxCrypto.json`, ayrıca ana `AppConfig.json`'ın varsayılanı | ✅ (bkz. `varlikAdedCarpani` 1.0→100.0 değişikliği, `docs/todo.md`) |
| Crypto | `SetKontratParamsCrypto` | `AppConfig_Crypto.json` | ✅ |

**Sonuç**: 14 market tipinin **hepsi** en az bir `AppConfig_*.json` dosyasında config-seviyesinde
seçilmiş durumda. Ama "config'te seçilmiş" ≠ "o config dosyasıyla gerçek bir run yapılıp
sonuçların gözlemlendiği doğrulandı" — bu envanterin kapsamı sadece config-seviyesi erişilebilirlik,
runtime doğrulaması değil.

---

## 5. Timing Filters (Order Time Eligibility) — ⛔ Büyük ölçüde erişilemez

Kaynak: `SingleTrader.CheckOrderTimeEligibility()` (`SingleTrader.cs:2079`, `FilterMode` parametresi
**enum değil, ham `int`**, 0-6 arası 7 mod destekliyor), çağrıldığı yer `ApplyTimingFilters()`
(`SingleTrader.cs:2252-2257`).

**Kritik bulgu**: `ApplyTimingFilters()` içinde `int filterMode = 1;` **hardcoded** — bu değeri
config'ten veya script'ten değiştirecek hiçbir alan/API yok. Yani 7 moddan (0-6) sadece **mod 1**
her zaman çalışıyor, geri kalan 6 mod (`FilterMode == 0,2,3,4,5,6`, `SingleTrader.cs:2116-2244`
arası) **koddan silinmeden çıkarılamaz, ama config'ten de asla tetiklenemez**.

Ayrıca: `TimeFilteringEnabled` (bu filtrenin ana açma/kapama anahtarı) taranan **her**
`AppConfig*.json` dosyasında `false` — yani pratikte timing filter mekanizmasının **tamamı**
(hardcoded mod 1 dahil) devre dışı.

| Varyant | Durum |
|---|---|
| `TimeFilteringEnabled = false` (kapalı) | ✅ **Tek fiilen kullanılan durum** — taranan tüm config dosyalarında. |
| `TimeFilteringEnabled = true` + FilterMode=1 (saat aralığı) | ⛔ Açık olarak hiç denenmemiş, ama açılsa bile sadece bu mod çalışabilir (hardcoded). |
| FilterMode = 0 (filtre yok/her zaman true) | ⛔ Koddan erişilemez — config/script'ten `filterMode` değiştirilemiyor. |
| FilterMode = 2,3,4,5,6 (tarih/datetime aralığı ve "sadece başlangıç" varyantları) | ⛔ Koddan erişilemez — aynı sebep. Bu 6 varyantı denemek için önce `ApplyTimingFilters()`'a bir config parametresi eklenmesi gerekir. |

---

## 6. MAMethod × OTT-ailesi Stratejiler — Kombinatoryal Alan

Kaynak: `Trading/Indicators/Base/MAMethod.cs` (70+ üye, 66'sı implement), OTT ailesi
(`OTT`/`HOTTLOTT`/`PMax`/`PTT`, `TrendIndicators.cs`) `maMethod: MAMethod` parametresi alıyor —
teorik olarak 66 MA yöntemiyle kombinlenebilir.

Bu alanı tek tek listelemek anlamsız (66 MA × birden fazla OTT-ailesi strateji = yüzlerce
kombinasyon). Onun yerine gerçek kullanım taraması:

- `inputs/configs/StrategyConfig.txt`'te tanımlı somut strateji sayısı: **sadece 3 farklı strateji**
  (`SimpleMAStrategy`, `SimpleMostStrategy`, `SimpleRSIStrategy`, toplam 5 parametre-seti/versiyon)
  — kod tabanında **24 strateji sınıfı** var (bkz. `docs/PROJECT_ANALYSIS.md` §3.3), yani
  **21 strateji (OTT/HOTTLOTT/PMax/PTT/SuperTrend/Ichimoku/Bollinger/MACD vb. dahil) hiç gerçek
  config'e girmemiş**, sadece kod olarak var.
  ⚠️ Bu bilgi 9-maddelik listenin dışında ama aynı temayı (yazılmış-ama-denenmemiş) güçlü şekilde
  destekliyor, bonus bulgu olarak not düşüldü.
- OTT-ailesi stratejilerin (`SimpleOTTStrategy`, `SimplePMaxStrategy`) hiçbiri `StrategyConfig.txt`'te
  yok → bunların constructor varsayılanı olan MA yöntemi (kod içi varsayılan, örn. OTT için VIDYA —
  bkz. `docs/PROJECT_ANALYSIS.md` §4.4) bile **hiç gerçek veriyle çalıştırılmamış** görünüyor.
- **Durum**: ⚠️ Tüm 66 MA yöntemi × OTT-ailesi kombinasyonu (varsayılanlar dahil) fiilen
  doğrulanmamış — bu, katalogdaki en büyük "açık alan".

---

## 7. RunMode (TraderRunMode) — ⚠️ Tek mod fiilen kullanılıyor

Kaynak: `TraderRunMode` enum (`TradeOnly`/`TradeAndQuery`/`QueryOnly`), her Trader tipinde
(`SingleTrader`/`MultipleTrader`/`ConfirmingSingleTrader`/`ConfirmingMultipleTrader`) `RunMode`
property'si olarak var.

| Varyant | Config'te kullanımı | Durum |
|---|---|---|
| `TradeOnly` | `"RunMode": "TradeOnly"` | ✅ Taranan **tüm 35** `RunMode` referansında (tüm `AppConfig*.json` dosyaları toplamında) bu değer — **başka hiçbir değer görülmedi.** |
| `TradeAndQuery` | `"RunMode": "TradeAndQuery"` | ⚠️ Hiç config'te set edilmemiş. Query altyapısı (`IQuery`/`BaseQuery`/`QueryRegistry`) çalışır durumda ama gerçek config'lerde hiç `TradeAndQuery`/`QueryOnly` seçilmemiş — sadece `inputs/scripts/Config_02_MultipleTrader.csx` gibi script-config dosyalarında `TraderRunMode.TradeAndQuery` örnek olarak yer alıyor (script içinde, AppConfig.json'da değil). |
| `QueryOnly` | `"RunMode": "QueryOnly"` | ⚠️ Hiç config'te set edilmemiş. |

---

## 8. Pyramiding / MaxPositionSize

Kaynak: `Trading/Core/InitialTradeParams.cs:145-160` (`PyramidingEnabled`, `MaxPositionSizeEnabled`,
`MaxPositionSize`, `MaxPositionSizeMicro`), `AppConfig.cs:157` (`PyramidingEnabled` AppConfig'e
bağlı), `AppConfigApplier.cs:1353`.

| Varyant | Config yolu var mı? | Durum |
|---|---|---|
| `PyramidingEnabled = false` (varsayılan) | `"PyramidingEnabled": false` | ✅ Taranan **tüm 263** referansta bu değer. |
| `PyramidingEnabled = true` | `"PyramidingEnabled": true` | ⚠️ `AppConfig`'e bağlı bir alan olmasına rağmen (`AppConfig.cs:157`) hiçbir gerçek config dosyasında `true` set edilmemiş — kod tam çalışır durumda (migration-guide.md'de "sonradan eklenmiş, tam implement" deniyor) ama hiç denenmemiş. |
| `MaxPositionSizeEnabled` / `MaxPositionSize` (pyramiding limiti) | — | ⛔ **`AppConfig.cs`/`AppConfigApplier.cs`'te bu alanlar hiç yok** — yani Pyramiding açılsa bile pozisyon büyüklüğü limiti config'ten set edilemiyor, sadece script/kod içinden doğrudan `InitialTradeParams` nesnesine erişilerek set edilebilir. Config katmanında tamamen kapalı bir alan. |

---

## 9. Slippage (`KaymayiDahilEt`) ve Micro Lot

Kaynak: `InitialTradeParams.cs:105,382` (`KaymayiDahilEt = kaymaMiktari != 0.0`, yani bu bir
config alanı değil, `KaymaMiktari` değerinden **otomatik türetiliyor**), `InitialTradeParams.cs:75,310,319`
(`MicroLotSizeEnabled`, `MarketType` Fx*/Crypto olduğunda otomatik `true`).

| Varyant | Durum |
|---|---|
| `KaymayiDahilEt = false` (KaymaMiktari=0.0) | ✅ 25 referansta `KaymaMiktari: 0.0`. |
| `KaymayiDahilEt = true` (KaymaMiktari≠0.0) | ✅ **238 referansta** `KaymaMiktari: 0.5` — bu, projede fiilen en yaygın kullanılan slippage değeri; bu varyant **iyi doğrulanmış** sayılabilir. |
| `MicroLotSizeEnabled = true` | Doğrudan config alanı yok, `MarketType` Fx*/Crypto seçildiğinde otomatik açılıyor | ✅ Fx/Crypto market tipleri config'lerde kullanıldığı için (bkz. §4) dolaylı olarak exercised. |
| `MicroLotSizeEnabled = false` | `MarketType` Viop/Bist seçildiğinde otomatik | ✅ Viop/Bist market tipleri de config'lerde var, dolaylı exercised. |

---

## Genel Özet

| Alan | Toplam varyant | ✅ Kullanılmış | ⚠️ Hiç kullanılmamış | ⛔ Config'ten erişilemez |
|---|---|---|---|---|
| 1. ConsensusMode | 5 (4 hazır + Custom) | 2 (Net, Custom via script) | 3 (Majority/All/Any) | 0 |
| 2. EquityCurveFilter (v1-v7) | 7 | 1 (v1=disabled) | 6 (v2-v7) | 0 |
| 3. Confirming davranışı | 6 alan | 2 (CancelAndRestart, Trigger=Both, Flatten=true — 3 alan ama hepsi tek yönde) | 3 (LockAndIgnore, Flatten=false, ProfitOnly/LossOnly trigger) | 0 |
| 4. MarketTypes | 14 | 14 | 0 | 0 |
| 5. Timing FilterMode | 7 (0-6) | 0 (mekanizma tamamen kapalı) | 0 | 6 (sadece mod 1 config'ten "açılabilir" ama o da hiç açılmamış) |
| 6. MAMethod × OTT ailesi | ~66 × birkaç strateji | 0 doğrulanmış | pratikte hepsi | — (kombinatoryal, ayrıntılı sayılmadı) |
| 7. RunMode | 3 | 1 (TradeOnly) | 2 (TradeAndQuery, QueryOnly) | 0 |
| 8. Pyramiding/MaxPositionSize | 2 ana + 2 alt | 1 (Enabled=false) | 1 (Enabled=true) | 2 (MaxPositionSize alanları config'te hiç yok) |
| 9. Slippage/MicroLot | 4 | 4 | 0 | 0 |

**En çok "yazılmış ama denenmemiş" biriken üç alan**: (a) EquityCurveFilter'ın 6 aktif varyantı
(v2-v7) — hepsi tanımlı ama sistemde her yerde kapalı (v1) kullanılıyor; (b) Timing Filters —
mekanizmanın tamamı kapalı VE açılsa bile 7 moddan 6'sı config'ten hiç erişilemiyor; (c) 24
stratejiden 21'i hiç gerçek `StrategyConfig.txt`'e girmemiş (bonus bulgu, §6).

## İlgili dosyalar
- [docs/PROJECT_ANALYSIS.md](../PROJECT_ANALYSIS.md) — bu sınıfların/enum'ların tam kod envanteri
- [docs/migration-guide.md](../migration-guide.md) — Pyramiding'in "sonradan eklenen özellik" notu
- [docs/todo.md](../todo.md) — `VarlikAdedCarpani` gibi doğrulama bekleyen diğer noktalar
- [docs/manual/01-class-reference.md](01-class-reference.md) — bu varyantların ait olduğu sınıfların API referansı
