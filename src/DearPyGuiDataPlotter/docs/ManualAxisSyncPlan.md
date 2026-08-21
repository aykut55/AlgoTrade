> **⚠️ ESKİMİŞ — güncel kod farklı (2026-08-21).** "Read" tarafı (`readPanelPlotParams`,
> `panelManager.py:1032`, `guiManager.py:479,526,544`) implement edilmiş, ama "Apply" tarafı
> önerilen isimlerle (`applyLastReadPlotParamsToOthers`, `_queuePanelAxisSync`,
> `_applyPendingAxisSync`, `_applyPendingYAdjust`, `_releasePendingAxisLocks`,
> `_queuePanelsForSyncOnScroll`) hiç yok. Daha önemlisi: bu belge "`InteractionManager` eksen
> uygulaması yapmayacak, `set_axis_limits`/`fit_axis_data` gibi işler `PanelManager`'da kalacak"
> diyor — gerçek kod tam tersini yapıyor: `guiManager.py:490`'da
> `interactionManager.scheduleSyncOthers()` çağrılıyor, yani sorumluluk `InteractionManager`'a
> verilmiş. Bu belgeyi tasarım niyeti için tarihsel referans olarak oku, davranış açıklaması
> olarak güvenme.

# Manual Axis Sync Plan

Bu not, top paneldeki `Read Params (src)` / `Apply Params (dst)` akisini
bozmadan ve DearPyGui/ImPlot'un native zoom-pan davranisini kalici olarak
degistirmeden eksen senkronu yapmak icin referanstir.

## Hedef Davranis

1. Active panel tek kaynak olacak.
   - Active panel state'inin sahibi `PanelManager` olacak.
   - `InteractionManager` active panel'i degistirmeyecek.
   - Top paneldeki active panel combo sadece `PanelManager.getActivePanelId()`
     sonucunu gosterecek.

2. `Read Params (src)` sadece okuma yapacak.
   - Aktif panel id alinacak.
   - O panelin `plot`, `x_axis`, `y_axis`, `xAxisLimits`, `yAxisLimits`,
     `ySyncId` gibi bilgileri okunacak.
   - Sonuc `PanelManager` icinde saklanacak.
   - Bu islem hicbir ekseni degistirmeyecek.

3. `Apply Params (dst)` diger panellere uygulayacak.
   - Hafizadaki source X limiti hedef panellere uygulanacak.
   - Source Y limiti tum panellere kopyalanmayacak.
   - Her hedef panel, sync edilen X penceresinde kendi visible datasina gore
     Y eksenini fit edecek.

4. Native zoom/pan davranisi kilitlenmeyecek.
   - `dpg.set_axis_limits(...)` ekseni kilitledigi icin kalici birakilmayacak.
   - Uygulama pending queue ile yapilacak:
     - Frame N: hedef panel queue'ya alinir.
     - Frame N+1: X limiti uygulanir.
     - Frame N+2: hedef panel kendi datasina gore Y adjust yapar.
     - Frame N+3: ilgili eksenler `set_axis_limits_auto` ile serbest birakilir.
   - Rastgele `split_frame()` kullanimi minimumda tutulacak; mumkunse merkezi
     render tick icindeki pending queue tercih edilecek.

5. Offscreen paneller sonradan gorununce sync olacak.
   - Apply sirasinda viewport disinda olan paneller "uygulandi" sayilmayacak.
   - Son source params ve sync version `PanelManager` icinde kalacak.
   - `centerCenterPanel` scroll pozisyonu `PanelManager.render()` icinde
     izlenecek.
   - Scroll degisince viewport'a giren ama son sync version'i almamis paneller
     pending queue'ya alinacak.
   - Bu paneller icin de ayni siralama uygulanacak: X sync -> Y adjust -> unlock.

## Sorumluluk Dagilimi

### PanelManager

`PanelManager` sync state'inin sahibi olacak.

Tutulacak state ornekleri:

```python
self._lastReadPlotParams = None
self._lastAppliedPlotParams = None
self._axisSyncVersion = 0
self._panelAppliedVersion = {}
self._pendingAxisSync = {}
self._pendingAxisUnlock = {}
self._lastContainerScrollY = None
```

Temel public methodlar:

```python
readPanelPlotParams(panelId=None, plotId=None)
applyLastReadPlotParamsToOthers()
```

Temel internal akış:

```python
_queuePanelAxisSync(panelId, version)
_applyPendingAxisSync()
_applyPendingYAdjust()
_releasePendingAxisLocks()
_queuePanelsForSyncOnScroll()
```

### GuiManager

Sadece UI callback baglantisini yapacak.

```python
_onReadSrcParams -> panelManager.readPanelPlotParams()
_onApplySrcParams -> panelManager.applyLastReadPlotParamsToOthers()
```

Status mesaji bottom paneldeki status text'e yazilabilir.

### InteractionManager

Manuel sync tasariminda `InteractionManager` eksen uygulamasi yapmayacak.

Kabul edilen sorumluluklar:

- Event yakalama.
- Event loglama.
- Gerekirse ileride opsiyonel "Auto Sync X" modu icin event kaynagi olmak.

Kacinilacak seyler:

- Active panel state'ini degistirmek.
- Pan/zoom event'i sonrasinda otomatik eksen sync yapmak.
- `set_axis_limits` / `fit_axis_data` gibi plot state degistiren isleri yapmak.

## Y Fit Kurali

X sync basarili olduktan sonra Y fit hedef panel bazinda yapilacak.

Her panel icin:

- Sadece visible data serileri taranacak.
- Sadece gorunur X penceresine denk gelen data araligi kullanilacak.
- Candle icin `low/high`.
- Line icin `ys`.
- Bar/volume icin `volume`.
- Min/max araligina %5-%10 civari padding eklenecek.

Bu davranis, OHLC/MACD/RSI/Stoch gibi farkli olcekli panellerde source Y limitini
kopyalamaktan daha dogrudur.

## Neden Auto Sync Degil?

Pan/zoom eventlerinden sonra otomatik sync yapmak native DearPyGui/ImPlot
davranisina mudahale edebilir. Bu yuzden ilk stabil hedef manuel akistir:

1. Kullanici source paneli aktif hale getirir.
2. `Read Params (src)` basar.
3. `Apply Params (dst)` basar.
4. Sistem diger panelleri ve sonradan viewport'a giren panelleri bu source'a
   gore sync eder.

Ileride gerekirse ayri bir `Auto Sync X` checkbox'i eklenebilir. Bu opsiyon kapali
iken pan/zoom native davranisi hic degistirilmemelidir.
