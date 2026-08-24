# AlgoTrade Manual — İçindekiler

> Proje büyüdükçe (147 `.cs` dosyası, ~33.000 satır, `[1]`-`[25]` Console menüsü, güçlü bir
> scripting katmanı) "kim ne yapar, hangi metodlar var, yeni bir şey eklerken nereye
> dokunulur" sorularını tek tek dosya okuyarak cevaplamak zorlaştı. Bu klasör, o soruların
> kalıcı cevabı olsun diye hazırlandı — [`../PROJECT_ANALYSIS.md`](../PROJECT_ANALYSIS.md)'in
> "proje ne durumda" sorusuna cevap vermesi gibi, bu set "projeyi nasıl kullanır/genişletirim"
> sorusuna cevap veriyor. İkisi birbirini tamamlıyor, tekrar etmemeye çalışıyor.
>
> Başlangıç tarihi: 2026-08-21. Bu README'yi güncel tutmanın kuralı basit: yeni bir bölüm
> eklendiğinde/silindiğinde buradaki listeye de bir satır eklenir/silinir.

## Bölümler

1. **[01-class-reference.md](01-class-reference.md)** — Class/API Referansı. `AlgoTrader`,
   `SingleTrader`, `MultipleTrader`, `ConfirmingSingleTrader`/`ConfirmingMultipleTrader`,
   `SingleTraderOptimizer`, `IndicatorManager`, `StrategyRegistry`/`QueryRegistry`, 12 Scanner
   sınıfı, `StockDataReader` (Menü `[1]` Read Data) — property grupları, public metodlar,
   tipik kullanım akışları.
2. **[02-console-menu-guide.md](02-console-menu-guide.md)** — Console Menü Rehberi. `[1]`-`[25]`
   menü haritası, `handleXxx()`/`runXxxAlgoTrade()` ortak deseni, **yeni bir menü öğesi adım adım
   nasıl eklenir**.
3. **[03-scripting-guide.md](03-scripting-guide.md)** — Scripting Rehberi. `ScriptExecutor`/
   `ScriptGlobals` mekanizması, üç kullanım seviyesi (hazır orkestrasyon → manuel kurulum → tam
   serbest), `CustomConsensusExample.csx` üzerinden worked example.
4. **[04-variant-catalog.md](04-variant-catalog.md)** — Varyant Kataloğu. Kodda **var olan ama
   gerçek veriyle hiç denenmemiş/doğrulanmamış** konfigürasyon varyantlarının checklist'i
   (consensus modları, EquityCurveFilter tetikleyicileri, Confirming davranış flag'leri, market
   type'lar, timing filter modları, MA×strateji kombinasyonları vb.). **Zamanla büyüyen bir
   liste** — bir varyantı gerçek veriyle denedikçe buraya "✅ doğrulandı" olarak işlenir.
5. **[05-findings.md](05-findings.md)** — Findings. Varyant kataloğundan çıkan **en çarpıcı,
   aksiyon gerektirebilecek** bulguların öncelik sırasına dizilmiş özeti (örn. Timing Filter
   mekanizmasının hardcode nedeniyle fiilen erişilemez olması). Katalog geniştir, bu dosya onun
   "önce buna bak" özeti.
6. **[06-class-doc-method.md](06-class-doc-method.md)** — Sınıf Dokümantasyon Yöntemi.
   `StockDataReader` (§9, `classes/09-stockdatareader.md`) belgelenirken canlı iterasyonla
   çıkan, onaylanmış bölüm sırası + format kuralları — §1-§8'den biri kendi sayfasına
   taşınırken **bu dosya okunup harfiyen takip edilecek**.
7. **[07-menu-vs-script-parity.md](07-menu-vs-script-parity.md)** — Menü ↔ Script Paritesi.
   Console'un interaktif `[N]` menü çiftleri (SingleTrader `[5]`, MultipleTrader `[6]`,
   SingleTraderOptimizer `[7]`, ...) ile bunların tek seferlik script hali
   (`inputs/scripts/0N_RunXxxWithProgressAsync.csx`) arasındaki davranış farklarının
   (plot, dosya yazımı, veri filtreleme vb.) takip listesi — hangisi düzeltildi, hangisi
   hâlâ açık.

## Bu Setin Kapsamadığı Şeyler (bilerek — başka dokümanlarda zaten var)

- **Projenin genel envanteri / hangi alt sistem ne durumda** → [`../PROJECT_ANALYSIS.md`](../PROJECT_ANALYSIS.md)
- **Eski projeden taşıma durumu, roadmap madde 1-10** → [`../migration-guide.md`](../migration-guide.md)
- **İndikatör kütüphanesi detayları** → [`../Indicators-README.md`](../Indicators-README.md), [`../Indicators-TODO.md`](../Indicators-TODO.md)
- **Aktif/güncel TODO listesi, tarih damgalı geliştirme notları** → [`../todo.md`](../todo.md)
- **Python plotter alt-projesi kendi mimarisi** → `src/DearPyGuiDataPlotter/docs/*.md`

## Güncelleme Kuralı

Bu doküman seti de diğerleri gibi eskiyebilir — bu oturumda `PROJECT_ANALYSIS.md`,
`migration-guide.md` ve iki Python plotter dokümanının (`InteractionManagerDavranisi.md`,
`ManualAxisSyncPlan.md`) kod ile ne kadar hızlı ayrıştığını gördük. Kural aynı: her iddiayı
mümkün olduğunca dosya:satır referansıyla bağla, böylece "bu hâlâ doğru mu" kontrolü tek tek
kod okumak yerine hızlı bir grep/diff işine dönüşsün.
