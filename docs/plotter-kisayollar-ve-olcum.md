# DearImGuiBundleDataPlotter: Kısayollar ve Ölçüm

> Bu dosya ileride plotter'daki **Help** menüsünün içeriği olacak.
> Yeni bir kısayol ya da özellik eklendiğinde burası da güncellenmeli.

## 1. Klavye kısayolları (normal plotlar)

Sadece **fare bir plotun üzerindeyken** çalışırlar. Bir yazı kutusuna yazı yazarken ve
**swing modunda** çalışmazlar. Üst bardaki **Plot Keys** onay kutusuyla açılıp kapatılırlar
(config: `topPanel.plotKeys`). Onay kutusunun üzerine gelince bu liste ipucu olarak görünür.

| Tuş | Ne yapar | Düğme karşılığı |
|---|---|---|
| `←` / `A` | Sola kaydırır (Pan Controls modunda) | `<< Sol` |
| `→` / `D` | Sağa kaydırır | `Sag >>` |
| `PageUp` / `PageDown` | Görünen genişlik kadar sola / sağa kaydırır | — |
| `Home` / `End` | Başa / sona gider | `\|< Basa` / `Son >\|` |
| `↑` / `↓` | X ekseninde yakınlaştırır / uzaklaştırır (Zoom oranıyla) | Zoom Controls |
| `Shift` + `↑` / `↓` | Y ekseninde yakınlaştırır / uzaklaştırır | Zoom Controls |
| `Ctrl` + `←` / `→` | Görünümü %10 sola / sağa kaydırır (en az 1 bar) | — |
| `Ctrl` + `↑` / `↓` | Görünümü %10 yukarı / aşağı kaydırır (aktif panel) | — |
| `Space` | Mevcut pencerede plotu ortalar: X korunur, tüm panellerin Y'si oturtulur | — |
| `Enter` | Adjust All | `Adjust All` |
| `Backspace` | Reset All (View Range ayarına göre) | `Reset All` |
| `M` | Ölçüm modunu açar / kapatır | `Measure Range (M)` |
| `Esc` | Ölçüm modunu kapatır (çizgiler kalır) | — |

Notlar:
- Kaydırma tuşları fiyat (OHLC) panelini kullanır, diğer paneller Auto Sync X ile izler.
- Zoom ve Ctrl+↑/↓ **aktif panele** uygulanır (Active Update Mode: Click / Hover).
- **Auto Sync Y** açıkken Y zoom'u ve Y kaydırması hemen geri alınabilir.
- `Ctrl` + ok tuşlarının adım oranı: `guiManager.py` → `KEY_NUDGE_RATIO = 0.10`.

## 2. Swing Annotation ekranının tuşları

Swing modunda sadece Swing Controls'un kendi tuşları çalışır:

| Tuş | Ne yapar |
|---|---|
| `←` / `A`, `→` / `D` | Navigation Step kadar kaydırır |
| `PageUp` / `PageDown` | Bir pencere genişliği kadar kaydırır |
| `Home` / `End` | Başa / sona gider |
| `Space` | Kayıtlı swing penceresine döner (Apply Width / Position) |
| `1` / `2` | Fare altındaki bara Dip / Tepe ekler |
| `Delete` | Fare altındaki işareti siler |
| `Ctrl+Z` / `Ctrl+Y` | Geri alır / yineler |
| `Esc` | Ölçüm modunu kapatır |

## 3. Measure paneli (aralık ölçümü)

Sağdaki panel grubunda **Measure** sekmesi. Açmak için üst bardaki **Measure** düğmesi,
**Panels → Measure** menüsü ya da komut kutusuna `measure` yazmak kullanılır.
Varsayılan olarak görünür (config: `panels.measurePanel.visible`).

Kullanım:
1. `M` ya da **Measure Range** ile ölçüm modu açılır.
2. Plotta ilk tık **A** barını, ikinci tık **B** barını seçer, üçüncü tık yeni ölçüme başlar.
3. A/B çizgileri bütün panellerde görünür. A cyan, B turuncu.
4. Yeni veri yüklenince ölçüm sıfırlanır.

Düğmeler:
- **Clear Measure**: ölçümü temizler.
- **Copy Measure**: ölçümü panoya düz metin olarak kopyalar.
- **Save Measure**: ölçümü iki dosyanın sonuna ekler (A ve B seçilmeden pasif):
  - `outputs/measurements/range_measurements.txt`: okumak için, her ölçüm bir blok
  - `outputs/measurements/range_measurements.csv`: analiz için, her ölçüm tek satır
    (`;` ayırıcı, UTF-8 BOM). pandas: `pd.read_csv(path, sep=";", encoding="utf-8-sig")`.
    Başlığı farklı eski bir dosya varsa `range_measurements_old_<zaman>.csv` adıyla kenara alınır.

Swing Controls'ta da aynı Copy / Save düğmeleri var. Kaynak, CSV'deki `Source` sütununda yazar.

### Değerlerin anlamı

Referans fiyat her zaman **A'nın Close** değeridir. Aralık, A ve B dahil aradaki tüm barlardır.

Panelde bir satırın üzerine gelince o değerin açıklaması ipucu olarak çıkar. Ölçümün altındaki
kapalı **Aciklamalar** bölümü açılınca bütün açıklamalar tablo halinde görünür. Metinler
`RangeMeasureTool.METRIC_HELP` içinde tanımlı. Measure paneli ve Swing Controls aynı metinleri kullanır.

| Değer | Anlamı |
|---|---|
| Series | Ölçümün yapıldığı seri (ölçümün başladığı paneldeki mum serisi) |
| A / B | Bar numarası, zaman, Close |
| Bars | A ile B arasındaki bar sayısı |
| Time | Geçen süre (bar sayısından farklı olabilir: seans araları, hafta sonu) |
| Difference / Change | B − A, puan ve yüzde |
| High / Low | Aralıktaki en yüksek High ve en düşük Low, bar numarası ve zamanı |
| Range | High − Low (puan ve A'ya göre %) |
| Max Runup | A'dan sonra fiyatın A'nın en fazla ne kadar üstüne çıktığı |
| Max DD | A'dan sonra fiyatın A'nın en fazla ne kadar altına indiği |
| MFE / MAE | Hareketin yönüne göre en iyi / en kötü nokta (swing segmentindeki tanım) |
| Efficiency | \|B − A\| ÷ bar bar Close değişimlerinin toplamı. 1'e yakınsa düz hareket, 0'a yakınsa zikzak |
| Avg / Bar | Fark ÷ bar sayısı |

Swing Controls'taki ölçüm de aynı değerleri gösterir ve kaydeder (hesap: `RangeMeasureTool.rangeStats`).

## 4. Gün geçiş çizgileri

Her yeni günün ilk barına bütün panellerde dikey çizgi çizer. Ayarlar `scripts/default.py`'nin başında:

| Ayar | Anlamı |
|---|---|
| `SHOW_DAY_SEPARATORS` | Normal plot ekranlarında aç / kapa |
| `SHOW_DAY_SEPARATORS_IN_SWING` | Swing Annotation ekranında aç / kapa |
| `DAY_SEPARATOR_COLOR` | Renk (RGBA 0..1). Kırmızı / sarı / yeşil / purple / cyan seçenekleri yorum satırında |
| `DAY_SEPARATOR_WEIGHT` | Çizgi kalınlığı |
| `DAY_SEPARATOR_MAX_VISIBLE` | Ekranda bundan fazla gün başı varsa çizilmez (çok uzaktan bakarken) |

Sadece intraday veride çizilir. Değişiklik, `default.py` tekrar çalışınca görünür.
