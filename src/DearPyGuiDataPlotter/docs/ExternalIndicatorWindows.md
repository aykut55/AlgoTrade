# External Indicator Windows Tasarimi

## Amac

Kullanici ana OHLC grafikte al/sat sinyallerini gordukten sonra, bu sinyalleri
ureten indikatorleri centerPanel disinda bagimsiz floating pencerelerde
inceleyebilmelidir.

Ilk hedef lightweight ve tek yonlu sync'tir:

- Ana OHLC panel source panel kabul edilir.
- Bagimsiz indikator pencereleri follower olur.
- Ana OHLC panelde X pan/zoom degistikce follower pencereler ayni X araligina gelir.
- Follower pencereler Y eksenini kendi gorunur verilerine gore fit eder.

Two-way sync sonraki asamaya birakilir.

## Neden PanelManager'a Normal Panel Olarak Eklenmiyor?

External indikator pencereleri debug/analiz penceresi gibi davranmali:

- centerPanel layout'unu bozmaz.
- Panel siralamasina, left menu panel listesine ve global Reset All davranisina karismaz.
- PanelManager'in syncGroup ve active panel akisina dahil olmaz.
- Birden fazla bagimsiz pencere kolayca acilabilir.

Bu yuzden ayri bir lightweight manager tercih edilir.

## Ilk Surum Kapsami

Ilk uygulama:

1. Top panel veya ScriptPanel uzerinden `scripts/open_indicator_window.py` calistirilir.
2. Script aktif/source OHLC paneli bulur.
3. Script kaynak paneldeki indikator serileri arasindan varsayilan olarak EMA serileri secer:
   - EMA50
   - EMA100
   - EMA200
4. Yeni floating DearPyGui window acilir.
5. Window icinde plot ve line serileri cizilir.
6. Her render tick'te source panelin X limitleri takip edilir.
7. X limitleri degistiyse external plot X eksenine uygulanir.
8. External plot Y ekseni sadece gorunur X araligindaki kendi serilerine gore fit edilir.
9. Buyuk datada external plot full seri cizmez; gorunur X araligi LOD ile
   seyreltilerek cizilir.
10. Crosshair icin source paneldeki son X pozisyonu external pencerede dikey
    cizgi olarak gosterilir.

## State Modeli

Her external pencere icin tutulacak state:

```python
{
    "windowTag": "external_indicator_window_1",
    "plotTag": "external_indicator_plot_1",
    "xAxisTag": "external_indicator_x_axis_1",
    "yAxisTag": "external_indicator_y_axis_1",
    "sourcePanelId": 1,
    "series": [
        {"name": "EMA50", "xs": [...], "ys": [...]},
        {"name": "EMA100", "xs": [...], "ys": [...]},
        {"name": "EMA200", "xs": [...], "ys": [...]}
    ],
    "lastXLimits": None,
}
```

## One-Way Sync

Render tick'te uygulanacak davranis:

```text
source panel X limits oku
her external pencere icin:
  pencere/axis hala var mi kontrol et
  X limit degismediyse gec
  external x axis limitlerini source X ile ayni yap
  external y axis limitlerini gorunur X araligina gore fit et
```

Y fit hesabi:

```text
visibleYs = xMin <= x <= xMax araligindaki tum secili serilerin y degerleri
yMin/yMax = visibleYs min/max
margin = (yMax - yMin) * 0.08
set y axis limits = yMin-margin, yMax+margin
```

## External LOD

2M bar gibi buyuk datalarda external pencerede full EMA serilerini cizmek
performans sorununa yol acar. Bu yuzden external manager:

- Window acildiginda line series'leri bos olusturur.
- Source X araligi degistikce sadece gorunur X araligini secer.
- Gorunur nokta sayisi buyukse stride ile `LOD_MAX_POINTS` civarina indirir.
- `dpg.set_value(seriesTag, [xs, ys])` ile var olan series'i gunceller.

Bu yontem external plot'u lightweight tutar ve z-order/legend/handler state'ini
silip yeniden olusturmaz.

## Crosshair

External window PanelManager panel listesine dahil degildir. Bu nedenle ana
PanelManager crosshair overlay'leri otomatik olarak external pencerede
calismaz.

Ilk surumda external manager, PanelManager'in son crosshair pozisyonundaki X
degerini okur ve external plot uzerinde dikey drag line olarak gosterir. Bu,
external pencerelerin source OHLC ile ayni bar'i isaret etmesini saglar.

## Two-Way Sync Icin Sonraki Adim

Two-way sync'e gecilirse event loop guard gerekir:

```python
isSyncing = True
lastAppliedFrame = dpg.get_frame_count()
syncSource = "main" | "external"
```

Dis pencere pan/zoom'u ana OHLC'ye uygularken tekrar follower update tetiklenip
sonsuz dongu olusmamasi icin bu guard zorunludur.

## Uygulama Notlari

- Ilk surumda external window plot etkileşimi pasif olabilir; source OHLC panelden
  yonetim yeterlidir.
- Data kaynagi olarak source panelin mevcut PanelData listesi kullanilir.
- C# bundle/view akisinda indikatorler zaten panel modeline ve pool'a girdigi icin
  ek veri formati gerektirmez.
- Daha sonra indicator secim UI'si eklenebilir.
- Indikator secimi manager icinde hard-coded tutulmaz. `open_indicator_window.py`
  gibi scriptler kaynak panel ve seri secimini yapar; manager sadece generic
  `openIndicatorWindow(sourcePanelId, series, ...)` altyapisini saglar.
