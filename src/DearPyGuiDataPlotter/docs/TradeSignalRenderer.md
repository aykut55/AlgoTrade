# TradeSignalRenderer — durum notu (debug için)

Bu dosya başka bir ajana context vermek için yazıldı: ne yapılmak isteniyor,
ne yapıldı, hangi düşünceyle, ne çalışıyor, ne çalışmıyor ve şu an açık kalan
sorun ne.

## Amaç

OHLC (candlestick) panelinde, hesaplanmış AL/SAT/FLAT trade sinyallerine göre:

1. **Bar boyama**: AL sinyalinden sonraki barlar (bir sonraki sinyale kadar)
   yeşil, SAT sonrası kırmızı, FLAT sonrası beyaz.
2. **Sinyal harfleri**: her sinyalin ateşlendiği barda küçük bir harf
   (AL→"A", SAT→"S", FLAT→"F").
3. **Seviye çizgileri**: AL sinyali geldiği barın **low**'undan, SAT
   sinyali geldiği barın **high**'ından başlayıp bir sonraki sinyale kadar
   uzanan yatay çizgi. FLAT'ta çizgi yok.

Bu 3 katman birbirinden bağımsız açılıp kapatılabilir flag'lerle kontrol
ediliyor (`showSignals`, `showLevelLines`, `colorBars`).

## Neden ayrı bir class (`TradeSignalRenderer`)

`PanelManager` zaten 1900+ satır ve candle/line/bar çizimi + LOD (Level of
Detail, 2M+ bar'lık veri için decimation) genel-amaçlı bir altyapı sunuyor.
Trade-sinyali görselleştirmesi tamamen ayrı bir domain olduğu için
`RangeSliderBar`/`PanelManagerWindow` gibi `PanelManager`'a bağımlı ama ondan
bağımsız yeni bir class (`src/plotting/tradeSignalRenderer.py`) olarak
yazıldı. `PanelManager`'ın candle/line/bar/LOD çizim mantığına hiç dokunmuz,
sadece onun oluşturduğu tag'leri (`candle_{panelId}_{dataId}`,
`y_axis_{panelId}`, `plot_{panelId}`) okuyup üstüne ekleme yapıyor.

## Veri modeli: sinyal listesi

`App.generateTradeSignals()` (scripts/default.py), RSI(14)'ün level70/level30
kesişmelerinden bir sinyal listesi üretir — **ana seriyle (OHLC) aynı
uzunlukta bir Python listesi**, sinyal olmayan barlarda `None`, sinyal
ateşlenen barlarda `"AL"`/`"SAT"`/`"FLAT"` string'i:

```python
def generateTradeSignals(self, rsiYs=None, level30=30.0, level70=70.0, warmup=15):
    # level70'i asagidan yukari kesme -> "AL"
    # level70'i yukaridan asagiya kesme -> "FLAT"
    # level30'u yukaridan asagiya kesme -> "SAT"
    # level30'u asagidan yukari kesme -> "FLAT"
    ...
    return signals  # len(signals) == len(rsiYs) == bar sayisi
```

`App.computeIndicators()` içinde `self.rsiYs` hesaplandıktan hemen sonra
`self.signals = self.generateTradeSignals()` çağrılıyor.

AL↔SAT arasında her zaman bir FLAT olması gerekliliği (kullanıcının kuralı:
"sinyal varsa flat olunur") ayrı bir state makinesi gerektirmeden otomatik
sağlanıyor — RSI sürekli bir değer olduğu için 70'in üstünden 30'un altına
önce 70'i aşağı kesmeden inemez. Tek istisna: bir barda her iki eşiği birden
aşan aşırı bir sıçrama (gerçek RSI'de pratikte görülmez) — o durumda öncelik
sırası (AL > 70-FLAT > SAT > 30-FLAT) tek bir sinyal seçer.

## `TradeSignalRenderer` API

```python
class TradeSignalRenderer:
    def __init__(self, panelManager): ...

    def draw(self, panelId, dataId, signals,
             showSignals=True, showLevelLines=True, colorBars=True):
        """panelId'deki panelin dataId'li (OHLC/candle) serisine overlay çizer.
        Tekrar çağrılabilir - önceki çizimler önce temizlenir (_clear)."""

    def setOhlcVisible(self, panelId, dataId, visible): ...
    def toggleOhlcVisible(self, panelId, dataId): ...
    def setActiveOhlcVisible(self, visible):
        """En son draw() çağrısının hedefine (panelId,dataId) uygular."""

    def clearAll(self): ...
```

Kullanım (scripts/default.py, `App.drawTradeSignals`):

```python
tsr.draw(self.ohlcPanel.id, 0, self.signals,
        showSignals=SHOW_TRADE_SIGNALS,
        showLevelLines=SHOW_TRADE_SIGNAL_LINES,
        colorBars=COLOR_BARS_BY_SIGNAL)
```

- `panelId`: `self.ohlcPanel.id` (int, PanelManager panel id'si).
- `dataId`: `0` — `Panel.setCandleData()`'nın varsayılan dataId'si (OHLC
  candle serisinin `PanelData.id`'si).
- `signals`: `self.signals` (generateTradeSignals'ın ürettiği liste).

`GuiManager.__init__`'te enjekte ediliyor:
```python
self.tradeSignalRenderer = TradeSignalRenderer(self.panelManager)
self.scriptPanel.set_globals(..., TradeSignalRenderer=TradeSignalRenderer,
                             tsr=self.tradeSignalRenderer)
```
Yani script namespace'inde hem `TradeSignalRenderer` (class) hem `tsr`
(GuiManager'ın instance'ı, `gm.tradeSignalRenderer` ile aynı referans) hazır.

`scripts/default.py`'nin en üstünde 3 flag var (test için kolay değiştirilsin
diye):
```python
SHOW_TRADE_SIGNALS = False      # harfler (göz yorduğu için kapatıldı)
SHOW_TRADE_SIGNAL_LINES = True  # seviye çizgileri
COLOR_BARS_BY_SIGNAL = True     # bar boyama
```

## Bar boyama nasıl çalışıyor (ve neden karmaşık)

DPG'nin `add_candle_series`'i **per-bar özel renk desteklemiyor** — sadece
tüm seri için TEK bir `bull_color`/`bear_color` (açılış/kapanış yönüne göre
seçilen 2 renk). Yani "AL barları yeşil, SAT barları kırmızı" gibi bar-bar
değişen bir renklendirme native olarak YOK.

**Çözüm**: veri, aynı sinyal durumunda kalan ardışık bar aralıklarına
("run") bölünüp **her run ayrı bir `add_candle_series`** olarak çiziliyor,
`bull_color=bear_color=<durum rengi>` (yani yön farkı gözetmeden düz bir
renk). `_computeRuns(signals, n)` bu bölünmeyi yapıyor:

```python
def _computeRuns(self, signals, n):
    # signals[0..n)'i [state, start, end) run'larina boler.
    # Bir run kendi ILK bar'inda (sinyalin ateslendigi bar) baslar,
    # bir SONRAKI sinyal barina kadar (haric) surer.
    # Ilk sinyalden ONCEKI barlar 'run' sayilmaz, state=None ile
    # ayri donduruluyor (bar boyama o kismi varsayilan renkte cizer).
```

`_drawColoredRuns` her run için:
- `RUN_LOD_MAX_POINTS` (2000) barı aşan run'ları `panelData._decimateOhlc`
  ile (ana LOD sistemindeki AYNI OHLC-bucket algoritması: her kovada
  open=ilk, high=max, low=min, close=son) küçültür.
- Tag şeması: `tradesignal_candle_{panelId}_{dataId}_{idx}` (idx =
  `enumerate(runs)`'daki sıra).
- Orijinal (PanelManager'ın çizdiği, `candle_{panelId}_{dataId}` tag'li)
  tekil candle serisi `colorBars=True` iken **gizlenir**
  (`dpg.configure_item(originalTag, show=False)`), silinmez.

## ÇÖZÜLEMEYEN sorun 1: Legend'e tıklayınca sadece 1 segment etkileniyor

Her run ayrı bir seri olduğu için legend'de hepsine aynı ismi (`data.name`,
örn. "THYAO") versek TEKRARLI satırlar görünür. Bu yüzden **sadece SON run**
(`idx == lastIdx`) etiketleniyor, diğerleri `label="" , use_internal_label=False`
ile legend'e hiç girmiyor.

Ama bu da demek ki kullanıcı legend'deki o TEK satıra tıklayınca ImPlot
SADECE o segmenti gizler/gösterir, diğer run'lar ETKİLENMEZ — kullanıcı
eskiden (segmentli yapıdan önce, tek native candle serisiyken) legend'e
tıklayınca TÜM OHLC'nin gizlendiğini hatırlıyor, şimdi bu davranış bozuldu.

### Bunu Python'dan senkronize etmeyi 3 farklı yöntemle denedim, ÜÇÜ DE BAŞARISIZ:

1. **`dpg.is_item_shown(tag)`** — her frame poll edip önceki değerle
   karşılaştırdım (`syncLegendVisibility`, artık kodda YOK, silindi).
   Sonuç: legend tıklamasını YANSITMADI (muhtemelen sadece bizim
   `configure_item` ile SET ettiğimiz statik değeri okuyor, ImPlot'un
   iç legend-toggle state'ini DEĞİL).

2. **`dpg.is_item_visible(tag)`** — aynı polling mantığı, farklı API.
   Sonuç: **CRASH** — `KeyError: 'visible'`. Sebebi doğrulandı:
   ```python
   dpg.get_item_state('c1')  # c1 = bir candle_series tag'i
   # -> {'ok': True, 'pos': [0, 0]}   <-- 'visible' anahtarı YOK
   ```
   `mvCandleSeries` türü için `get_item_state`'in döndürdüğü sözlükte
   `visible` alanı YOK — yani bu API bu item tipi için desteklenmiyor.

3. **`dpg.item_handler_registry` + `add_item_clicked_handler`** —
   candle_series'e bir click handler bağlamayı denedim (belki legend
   tıklaması bir "clicked" event'i olarak yakalanır diye). Sonuç:
   **DPG açıkça reddetti**:
   ```
   Error: [1000] Command: bind_item_handler_registry
   Item Type: mvAppItemType::mvCandleSeries
   Message: Item Handler Registry includes inapplicable handler: mvClickedHandler
   ```
   Yani `mvCandleSeries` item'larına HİÇBİR click handler bağlanamıyor.

**Sonuç**: Bu üç deneme, DPG'nin plot legend tıklamasını (ImPlot'un iç
state'i) Python tarafına HİÇBİR ŞEKİLDE yansıtmadığını kanıtlıyor — bu bir
DPG API sınırı, kod hatası değil. Çok-segmentli mimaride native legend
tıklamasının TÜM grubu etkilemesini sağlamanın bilinen bir yolu YOK.

### Geçici/kalıcı çözüm: elle kontrol

Native legend yerine, kendi ACIKÇA çağrılan metodlarımız var:

```python
tsr.setOhlcVisible(panelId, dataId, True/False)
tsr.toggleOhlcVisible(panelId, dataId)
tsr.setActiveOhlcVisible(True/False)  # en son draw() hedefine uygular
```

Bunlara bağlı, GUI'de gerçek bir checkbox var:
`guiManager.py`'de `topPanelGroupBox4` içinde, "Auto Sync Y" satırının
altında, tag=`top_show_ohlc_checkbox`, callback=`_onShowOhlcChanged`:

```python
def _onShowOhlcChanged(self, sender=None, appData=None):
    self.tradeSignalRenderer.setActiveOhlcVisible(bool(appData))
```

`setOhlcVisible` **candle segmentleri + harfler + seviye çizgilerinin
HEPSİNİ** birlikte gizler/gösterir (kullanıcı "candle'lar gizlenirken
harf/çizgilerin ekranda yalnız kalması mantıksız" dedi, ilk versiyonda
sadece candle'ları kapsıyordu, düzeltildi):

```python
def setOhlcVisible(self, panelId, dataId, visible):
    originalTag = f"candle_{panelId}_{dataId}"
    if dpg.does_item_exist(originalTag):
        dpg.configure_item(originalTag, show=visible)
    for tag in self._createdTags.get((panelId, dataId), []):
        if tag.endswith("_theme"):
            continue
        if dpg.does_item_exist(tag):
            dpg.configure_item(tag, show=visible)
```

Kullanıcı bu checkbox'ın çalıştığını (bar boyama + harf + çizgi hep birlikte
gizlenip gösterildiğini) DOĞRULADI — bu kısım ÇÖZÜLDÜ.

## ÇÖZÜLEMEYEN sorun 2 (AKTİF, ÜZERİNDE ÇALIŞILIYOR): orijinal VE segmentli candle'lar AYNI ANDA görünüyor

Kullanıcının son bildirdiği sorun: `colorBars=True` iken hem **orijinal**
(PanelManager'ın çizdiği, `show=False` yapılması gereken) candle serisi HEM
DE yeni segmentli/renkli candle'lar aynı anda ekranda görünüyor — yani
`draw()` içindeki bu satırın:

```python
if colorBars:
    if dpg.does_item_exist(originalTag):
        dpg.configure_item(originalTag, show=False)
    self._drawColoredRuns(panelId, dataId, data, runs, yTag)
```

hide çağrısı ya çalışmıyor, ya da bir yerlerde SONRADAN tekrar `show=True`
yapılıyor.

### Şu ana kadar elenen teoriler (statik kod okumasıyla):

- `PanelManager.updateLod()` (her frame, sadece `fullCount > 6000` bar'lık
  seriler için çalışır) — `_drawOrUpdateSeries` içinde `exists=True` olan
  bir tag için sadece `dpg.set_value(...)` çağırıyor, `show`'a hiç
  dokunmuyor. Ayrıca küçük test veri setlerinde (THYAO 5dk gibi) muhtemelen
  `fullCount` eşiği aşmıyor, bu path hiç çalışmıyor olabilir.
- `drawPanelData`'nın tekrar çağrılıp `yTag`'in TÜM çocuklarını silip
  (`dpg.delete_item(yTag, children_only=True)`) yeniden çizdiği bir ikinci
  çağrı YOK gibi görünüyor (`run()` içinde `self.draw()` sadece BİR kez,
  `drawTradeSignals()`'tan ÖNCE çağrılıyor) — ama tam doğrulanmadı.
  `PanelManagerWindow`/`LeftMenuPanel`'de "Data Ops/Hide-Show" gibi bir UI
  elemanının panel verisini periyodik olarak yeniden çizip çizmediği TAM
  incelenmedi.
- `d.isVisible` (PanelData model flag'i) tabanlı bir "her frame show'u
  isVisible'a göre senkronla" mekanizması aratıldı, BULUNAMADI (`grep`
  ile `isVisible.*show=` ve varyasyonları projede yok).
- Auto Sync X/Y (`_onAdjustXAxisAll`/`_onAdjustYAxisAll`, default açık,
  ~150ms'de bir periyodik çalışıyor) sadece eksen limitlerini
  (`dpg.set_axis_limits`/`adjustYAxis`) değiştiriyor, seri
  oluşturma/`show` ile ilgisi yok gibi görünüyor ama TAM doğrulanmadı.

### Şu an eklenmiş teşhis kodu

`tradeSignalRenderer.py` `draw()` içinde (satır ~89-94), her `draw()`
çağrısında konsola şunu basıyor:

```python
if colorBars:
    existsBefore = dpg.does_item_exist(originalTag)
    if existsBefore:
        dpg.configure_item(originalTag, show=False)
    print(f"[TradeSignalRenderer] originalTag={originalTag} existsBefore={existsBefore} "
          f"show_after={dpg.get_item_configuration(originalTag).get('show') if existsBefore else 'N/A'}")
    self._drawColoredRuns(panelId, dataId, data, runs, yTag)
```

**Kullanıcıdan bu print'in çıktısını bekliyoruz** — `existsBefore` ve
`show_after` değerleri, hide çağrısının o AN doğru çalışıp çalışmadığını
gösterecek. Eğer `show_after=False` çıkıyorsa, sorun draw() SONRASINDA bir
yerde `show=True`'ya geri döndürülmesi demek (başka bir kod yolu bulunmalı -
belki `PanelManagerWindow`'daki "Hide-Show" UI, belki `updateLod`'un
BEKLENMEDIK bir dalı, belki `dpg.split_frame()` sonrası bir DPG davranışı).
Eğer `show_after=True` çıkıyorsa (yani `configure_item` çağrısı HİÇ işe
yaramıyor), o zaman DPG'nin `show=False`'un candle_series için nasıl
davrandığına dair farklı bir soruşturma gerekir (örn. `show` DEĞİL de
`enabled` mi kullanmak gerekiyor, ya da `before=` parametresiyle ilgili bir
DPG tuhaflığı mı var).

### Araştırma önerileri (bir sonraki ajan için)

1. Önce kullanıcıdan konsol çıktısını (`[TradeSignalRenderer] originalTag=...`
   satırı) alıp `show_after` değerini kontrol et.
2. `PanelManagerWindow.py` ve `LeftMenuPanel.py`'de panel/data görünürlüğünü
   periyodik senkronize eden bir `render()`/`sync()` metodu olup olmadığını
   ara (`grep -rn "show=" src/plotting/panelManagerWindow.py
   src/plotting/leftMenuPanel.py`).
3. `dpg.configure_item(tag, show=False)`'in `add_candle_series` için
   GERÇEKTEN native legend rengini/görünürlüğünü etkileyip etkilemediğini
   headless bir DPG render döngüsüyle test et (bu repoda daha önce
   `dpg.create_viewport()+show_viewport()+render_dearpygui_frame()` ile
   headless render'ın ÇALIŞTIĞI doğrulandı - bkz. bu conversation'daki
   custom_series/candle_series denemeleri).
4. `before=originalTag` parametresinin (segment'leri orijinal candle'ın
   POZİSYONUNA yerleştirmek için kullanılıyor, bkz. `_drawColoredRuns`)
   DPG'de `show=False` olan bir item'ın ÖNÜNE yeni item eklemenin herhangi
   bir yan etkisi olup olmadığını (örn. o item'i YENİDEN GÖSTERİR gibi bir
   DPG davranışı) araştır - bu ŞÜPHELİ bir nokta, doğrulanmadı.
5. `_clear()`'ın, bir ÖNCEKİ `draw()` çağrısından kalma tag'leri silerken
   YANLIŞLIKLA orijinal candle tag'ini de silip silmediğini kontrol et
   (silmemeli - `_createdTags`'e sadece `tradesignal_*` prefix'li tag'ler
   ekleniyor, `candle_{panelId}_{dataId}` hiç `_track()` edilmiyor - ama
   yine de çapraz kontrol edilmeli).

## İlgili dosyalar

- `src/plotting/tradeSignalRenderer.py` — ana class.
- `src/plotting/panelData.py` — `_decimateOhlc`, `_decimateStride` (LOD
  decimation helper'ları, `TradeSignalRenderer` bunlardan `_decimateOhlc`'yi
  import edip kullanıyor).
- `src/plotting/panelManager.py` — `drawPanelData`, `_drawOrUpdateSeries`,
  `updateLod` (candle/line/bar'ın ASIL native çizim/LOD mantığı,
  `TradeSignalRenderer`'in DOKUNMADIĞI ama tag isimlerini PAYLAŞTIĞI yer).
- `src/plotting/guiManager.py` — `TradeSignalRenderer` enjeksiyonu
  (`__init__`, `set_globals`), "Show OHLC" checkbox'ı (`_buildLayoutDefault`
  içinde `topPanelGroupBox4`), `_onShowOhlcChanged`.
- `scripts/default.py` — `generateTradeSignals`, `drawTradeSignals`, 3
  flag (`SHOW_TRADE_SIGNALS`, `SHOW_TRADE_SIGNAL_LINES`,
  `COLOR_BARS_BY_SIGNAL`), `computeIndicators` içinde `self.signals`
  ataması.
