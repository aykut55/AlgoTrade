# Sınıf Dokümantasyon Yöntemi

> `StockDataReader`'ı (§9) belgelerken canlı iterasyonla ortaya çıkan, kullanıcı tarafından
> onaylanmış bir yöntem — bir sonraki sınıfa (§1-§8'den biri) geçildiğinde **buradan
> sapılmaması** için yazıldı. Kullanıcı bu dosyayı "oku" dediğinde, aşağıdaki sırayı ve
> kuralları harfiyen takip et. Başlangıç tarihi: 2026-08-21, `StockDataReader` (§9) örneği
> üzerinden çıkarıldı — bkz. [classes/09-stockdatareader.md](classes/09-stockdatareader.md).

## 1. Dosya Yeri ve İsimlendirme

- Her sınıf kendi dosyasında: `docs/manual/classes/NN-sinifadi-kucukharf.md` (örn.
  `09-stockdatareader.md`). Tek dosyada tüm sınıfları anlatmak (eski `01-class-reference.md`
  deseni) sayfayı hem çok büyütüyor hem sol menüyü (mkdocs `toc.integrate`) kalabalıklaştırıyor.
- `01-class-reference.md`'deki ilgili `## N. SınıfAdı` bölümü **kısa bir özete** indirilir:
  Dosyalar/Rolü/Ne zaman kullanılır tek paragrafta + `**[→ SınıfAdı (ayrı sayfa)](classes/NN-sinifadi.md)**`
  linki. İçindekiler'deki madde de güncellenir: `... — özet burada, tam referans [ayrı sayfada](classes/NN-sinifadi.md)`.
- Yeni sayfa `mkdocs.yml`'in `nav:` ağacına eklenir — `Class Reference` grubunun altına, mevcut
  `Genel Bakış (§1-§8)` satırının yanına yeni bir satır (bkz. `mkdocs.yml`'deki `StockDataReader (§9)` örneği).
- Yeni sayfadan `01-class-reference.md`'ye veya `02-console-menu-guide.md`'ye giden linkler
  **göreli yol farkını** hesaba katmalı (`docs/manual/classes/`'ten `docs/manual/`'e çıkmak için
  `../` prefix'i şart).

## 2. Bölüm Sırası (bu sırayla, atlamadan)

1. **H1 başlık** — `# SınıfAdı — Kısa Açıklama (varsa Menü [N] referansı)`.
2. Üstte küçük bir not: `01-class-reference.md`'ye (ve varsa kardeş sayfalara) geri link.
3. **Dosyalar** (`### Dosyalar`) — madde listesi, her satır `` `tam/yol/Dosya.cs` (satır sayısı) ``.
4. **Rolü** (`### Rolü`) — madde listesi (bold-label + paragraf DEĞİL, her cümle ayrı madde).
5. **Ne zaman kullanılır** (`### Ne zaman kullanılır`) — madde listesi.
6. **Sınıf İskeleti (ilk bakış)** (`### Sınıf İskeleti...`) — TEK bir ```csharp bloğu: gövdeler
   kaldırılmış, alan/property/event/metod imzaları **public VE private hepsi**, kaynaktaki
   sırayla birebir aynı. Private yardımcı metodlar sadece imza (gövde yok — kalabalık olur,
   kullanıcı bunu açıkça belirtti: "gövdeleri eklersek kalabalık olur").
   - Hemen altına (Public API'den önce) opsiyonel bir **"Üye İndeksi"** tablosu eklenebilir:
     her üye `SınıfAdı::ÜyeAdı` notasyonuyla, türü (public/private field/property/event/method),
     ve Public API'nin hangi alt bölümünde anlatıldığına gerçek anchor link. Private yardımcı
     metodlar için ayrı bir alt başlık yoksa "en yakın ilgili bölüme" yönlendirilir; hiçbir
     yerden çağrılmayan ölü bir private metod bulunursa (bkz. `StockDataReader::FormatSigned`
     örneği) bu tabloda dürüstçe belirtilir — link yerine "hiçbir yerden çağrılmıyor" notu.
7. **Public API** (`## Public API`, alt gruplar `###`) — her metod grubu (Kurulum/Temizlik,
   ana işlev grupları, event'ler vb.) kendi alt başlığında, madde listesi. Karmaşık/tartışmalı
   bir metodun (örn. filtre/switch mantığı) gerçek private kaynağı ayrıca gösterilir (bkz. adım 9).
8. **Yardımcı veri tipi** varsa (`StockData` struct gibi) — `##` seviyesinde ayrı bölüm.
9. **Çağrı Zinciri** (`## Çağrı Zinciri (... → ...)`) — sınıfın gerçek çağrıldığı yerden
   (genelde Console `Program.cs`) başlayarak numaralı liste, dosya:satır referanslarıyla.
   Bulunan **davranışsal bulgular** (ölü kod, tetiklenemeyen guard, sessiz veri kaybı, vb.)
   `> **Not — ...:**` blockquote'u olarak buraya eklenir — iddia "muhtemelen" mi "kesin" mi
   net yazılır, kaynağa (`Dosya.cs:NN-MM`) referans verilir.
10. **AppConfig/Config kaynağı** varsa (`## AppConfig Kaynağı — \`XxxConfig\``) — sırasıyla:
    gerçek C# DTO sınıfı (```csharp), sonra JSON karşılığı (```json), sonra kısa notlar.
11. **Ana orkestrasyon fonksiyonunun tam kaynağı** (`## \`fonksiyonAdı()\` — Tam Kaynak (Dosya:NN-MM)`)
    — gerçek kaynak, paraphrase değil, tek ```csharp bloğu. Kullanıcı hangi satırların önemli
    olduğunu belirttikçe `hl_lines="N M ..."` (fence bilgi satırında) ile kırmızı+kalın
    vurgulanır (stil `docs/stylesheets/extra.css`'te `.highlight .hll` — zaten kurulu, sadece
    fence'e `hl_lines` eklemek yeterli). Satır numaraları fence'in kendi 1. satırından sayılır
    (dosyadaki gerçek satır numarası değil).
12. **Callback/event handler'ların gerçek gövdeleri** varsa — HER BİRİ kendi bağımsız
    ```csharp bloğunda, hemen üstünde 1-2 cümlelik açıklama. Asla birden fazla callback'i
    tek blokta birleştirme.
13. **Dönüş/Sonuç — Global State** (`## Dönüş / Sonuç — Global State`) — fonksiyon bittiğinde
    hangi değişkenlerin güncellendiği, tablo + madde listesi.
14. **Tipik Kullanım örnekleri** (`## Tipik Kullanım — ...`) — HER senaryo kendi bağımsız
    ```csharp bloğunda (asla tek blokta birleştirilmiş liste değil — kullanıcı bunu açıkça
    düzeltti: "hepsi bağımsız olsun"). Uzun metod çağrıları parametre-başına-satır olacak
    şekilde sarılır (tek satırlık uzun çağrı okunmaz bulundu).
15. **Console/JSON eşleşmesi** varsa (aynı örneklerin config-dosyası karşılığı) — önce somut
    adım adım "ne yapman gerekiyor" anlatımı (dosya yolu, hangi alanı değiştir, nasıl çalıştır),
    SONRA kod örneği. Soyut "alan adları değişir, mantık aynı" gibi cümleler tek başına
    YETERSİZ bulunuyor — kullanıcı bunu anlamadığını belirtti, somut adım+kod istiyor.
16. **Kimler Kullanıyor — Instantiation Noktaları** (`## Kimler Kullanıyor — Instantiation Noktaları`)
    — sınıfın `new XxxClass()` ile nerede yaratıldığının tam envanteri: dosya, fonksiyon/bağlam
    (veya "top-level akış" + değişken adı), satır numarası. Tüm kod tabanı (Console + `.csx`
    scriptler + WinForms) gerçek grep taramasıyla çıkarılır, varsayımla yazılmaz.
17. **Kullanım Haritası** (`## Kullanım Haritası`) — sınıfın (kendisi + taban sınıftan miras)
    TÜM public üyelerinin gerçek kod tabanında ✅ kullanılıyor / ⚠️ sadece yorum/örnekte /
    ❌ hiç kullanılmıyor sınıflandırması, tablo halinde, her satırda dosya:satır kanıtı.
18. **İlgili Dosyalar** (`## İlgili Dosyalar`, footer) — bu sayfanın ait olduğu index + ilişkili
    diğer rehber sayfalarına linkler.

## 3. Format Kuralları (her adımda geçerli)

- **Bold-label + inline paragraf YOK.** `**Rolü**: uzun paragraf...` gibi bir şey yazma —
  `### Rolü` başlığı + altında madde listesi (`- ...`) yaz. Aynı kural `Dosyalar`/
  `Ne zaman kullanılır` ve benzeri tüm giriş alanları için geçerli.
- **Kod örnekleri her zaman bağımsız fenced block.** Birden fazla senaryo/örnek tek
  ```csharp bloğunda ASLA birleştirilmez.
- **Uzun satırlar sarılır** — bir metod çağrısının parametreleri 3'ten fazlaysa veya satır
  ~80 karakteri geçiyorsa, parametre başına bir satır.
- **§N / Bölüm referansları her zaman gerçek link.** Çıplak `§N` sembolü asla kullanılmaz.
  Aynı sayfa içi referans → `[Bölüm N](#gerçek-anchor)`; başka sayfaya referans →
  `[Sayfa § Başlık](goreli/yol.md#gerçek-anchor)`. Anchor'ı ASLA tahmin etme — Türkçe karakterler
  (ı, ş, ğ, İ) pymdownx'in slugify'ında beklenmedik sonuç verebiliyor. Doğrulama adımı:
  `mkdocs build` çalıştır, sonra `grep -oE 'id="[0-9][^"]*"' site/.../index.html` ile gerçek
  id'yi al, linki ona göre yaz. Build sonrası "contains a link '#...', but there is no such
  anchor" (WARNING veya INFO) çıkmadığından emin ol.
- **Her kod bloğunda satır numarası göster.** Her ```csharp/```json fence'ine `linenums="1"`
  ekle (kapanış fence'i `` ``` `` hariç, sadece açılış satırına) — sınıf iskeleti, gerçek
  kaynak listeleri, bağımsız örnekler, JSON blokları, hepsi. Kullanıcı bunu tek tek her
  bloğa sormak yerine baştan genel kural yaptı: "code bloklarında her zaman numara göster".
  Yeni bir sayfa/bölüm yazarken bunu unutma, sonradan eklemek yerine baştan koy.
- **Kod önemli satırları vurgulama** — kullanıcı bir satırı işaret edip "bunu highlight/bold
  yap" dediğinde, gerçek markdown bold (kod bloğu içinde çalışmaz) yerine `hl_lines="N"`
  kullan (satır numarası artık `linenums="1"` sayesinde fence'in GERÇEK satır numarasıyla
  aynı — `linenums` ve `hl_lines` aynı fence'te birlikte kullanılabilir, örn.
  `` ```csharp linenums="1" hl_lines="11 25" ``). Yeni bir satır eklenince `hl_lines`
  listesine ekle, var olanları bozma.
- **Bulgular temkinli ve kanıtlı yazılır.** "muhtemelen X" / "kesin X" ayrımı net olmalı;
  her iddia dosya:satır referansı taşımalı. Emin olmadan "hiç kullanılmıyor" gibi kesin
  ifadeler yazma — önce gerçekten grep ile TÜM kod tabanını (Console + `.csx` + WinForms,
  farklı değişken adlarını da hesaba katarak) tara. Bir kez `Dispose()` için bu atlandı ve
  yanlış "❌ hiç kullanılmıyor" yazıldı, sonra 13 script'te kullanıldığı bulunup düzeltildi —
  bu hatayı tekrarlama: **her ❌ iddiasından önce en az iki farklı değişken adı deseniyle
  (örn. `stockDataReader.` VE `reader.`) ayrı ayrı grep at.**
- **Her önemli ekleme sonrası doğrula.** `.venv\Scripts\python.exe -m mkdocs build` çalıştır,
  çıktıda yeni bir WARNING/ERROR olmadığından emin ol (önceden var olan, bu dosyayla ilgisiz
  uyarılar — `roadmap.md`, `export-adimlar.md`, `inputs/scripts/*` dangling linkleri —
  göz ardı edilebilir, onlar bu yöntemin kapsamı dışında).

## 4. mkdocs Altyapısı (bir kere kuruldu, hatırlatma amaçlı)

- `mkdocs.yml`: `theme.features` içinde `toc.integrate` + `navigation.expand` (sol menüde
  sayfa başlıkları görünsün diye) + `markdown_extensions.toc.toc_depth: 2` (sadece `##`
  seviyesi sol menüye çıksın, `###`/`####` alt başlıklar sayfa içinde kalsın, menü kalabalık
  olmasın).
- `markdown_extensions.pymdownx.highlight`: `anchor_linenums: true` + `line_spans: __span` —
  gerektiğinde tek bir satırı CSS ile hedeflemeyi mümkün kılar (nadiren gerekir, `hl_lines`
  çoğu durumda yeterli).
- `docs/stylesheets/extra.css`: `.highlight .hll` için kalın + kırmızı (açık tema `#b71c1c`,
  koyu tema `#ff8a65`) — `hl_lines` kullanan her kod bloğu otomatik bu stili alır, ekstra
  bir şey yapmaya gerek yok.
- Canlı önizleme: `startDocsServer.bat` (kökte) veya `.venv\Scripts\python.exe -m mkdocs serve`
  — dosya kaydedince tarayıcı otomatik yenilenir.

## İlgili Dosyalar

- [01-class-reference.md](01-class-reference.md) — §1-§8'in hâlâ yaşadığı ana dosya, bu
  yöntem bir sonraki sefer buradan bir sınıf çıkarılırken uygulanacak.
- [classes/09-stockdatareader.md](classes/09-stockdatareader.md) — bu yöntemin çıkarıldığı,
  referans alınacak canlı örnek.
