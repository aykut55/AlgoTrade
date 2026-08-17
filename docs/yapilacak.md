# AlgoTrade → DearPyGuiDataPlotter Entegrasyonu — Durum ve Yapılacaklar

C#'ın ürettiği gerçek trade/sinyal datasını, mevcut pythonnet+imgui_bundle akışının YANINA
(onu bozmadan) DearPyGuiDataPlotter ile de çizdirmek. Bu dosya güncel durumu özetler; yeni bir
konuşmada buradan devam edilecek.

## TAMAMLANDI

### 1) Dosya tabanlı runtime command mekanizması (kök `docs/todo.md` planı)
- `src/AlgoTrade.Core/Python/DearPyGuiDataPlotter/DearPyGuiDataPlotter.cs` — ayrı process
  (pythonnet DEĞİL, gerçek `Process.Start`). `StartPlotter()`, `StopPlotter(gracefulTimeoutMs=3000)`
  (önce "shutdown" komutunu dener, sonra kill), `LoadBundle`, `ClearPanel`, `ClearAllPanels`,
  `ReloadCurrent`, `AddSeriesFromBundle`, `Shutdown` — hepsi `WriteCommand()` ile
  `inputs/runtime_commands/`'a sıra numaralı json yazıyor (önce `.tmp`, atomik rename).
- `src/DearPyGuiDataPlotter/src/plotting/runtimeCommandManager.py` — `RuntimeCommandManager`,
  `GuiManager.render()`'ın EN SONUNDA çağrılıyor (shutdown gibi komutlar frame'in geri kalanını
  bozmasın diye). Tüm handler'lar (`load_bundle`, `clear_panel`, `clear_all_panels`,
  `reload_current`, `add_series_from_bundle`, `shutdown`) yazılı. `load_bundle`/`reload_current`
  `default.py`'nin mevcut stage2/stage3 mantığını (`gm.scriptPanel.runScriptFile("default.py")`)
  tekrar kullanıyor, kopyalamıyor.

### 2) Gerçek veri converter'ı
- `src/AlgoTrade.Core/Python/DearPyGuiDataPlotter/NpzWriter.cs` — elle `.npy`/`.npz` yazıcı,
  gerçek numpy'ye karşı (throwaway test projesiyle) doğrulandı.
- `src/AlgoTrade.Core/Python/DearPyGuiDataPlotter/TradeDataBundleConverter.cs` —
  `ConvertSingleTrader(trader, outputDir, fileBaseName="latest_bundle")` → `(bundlePath, viewPath)`.
  - `Lists.SinyalList` (her barda AL=1.0/SAT=-1.0/FLAT=0.0 state'i tekrarlar) → `signal_codes`
    (SEYREK/event, sadece değişim barlarında 1=AL/-1=SAT/2=FLAT — TradeSignalRenderer için) VE
    `signal_steps` (YOĞUN/state, Signals panelindeki step-line için) olarak İKİ farklı alana yazılıyor.
  - `Lists.KarZararFiyatList/…YuzdeList/GetiriFiyatList/…NetList/…YuzdeList/…YuzdeNetList` → generic
    "indicator" serisi olarak bundle'a yazılıyor (Python bunun PnL mi teknik indikatör mü olduğunu
    bilmiyor, sadece isimle eşleşiyor — Python tarafında hiç değişiklik gerekmedi).
  - `trader.Strategy.GetPlotIndicators()` (örn. MOST+EXMOV) da aynı şekilde generic seri.
  - `BuildAndWriteView()` **7 panelli** view.json üretiyor (bkz. `src/panels.jpg` referansı):
    OHLC, Signals, PnL, PnL %, Return+Net Return, Return %+Net Return %, Strategy Indicators.
    OHLC serisi `dataId: 0` ile ZORUNLU (TradeSignalRenderer bunu hardcoded arıyor).
- Test hook (**GEÇİCİ, TODO ile işaretli**): `Program.cs`'in `runSingleTraderAlgoTrade()`'inde,
  mevcut pythonnet `PlotSingleTraderData` çağrısının hemen altında — aynı SingleTrader'dan bundle
  üretip DearPyGuiDataPlotter'ı da açıyor, try/catch'li, pythonnet akışını hiç bozmuyor.
  `[9]` numaralı ayrı bir demo menüsü de var (StartPlotter/LoadBundle/ClearPanel testi).
- **Kullanıcı gerçek bir SingleTrader run'ı ile uçtan uca test etti ve doğruladı.**

### 3) Görsel düzeltmeler (kullanıcı ile birlikte, hepsi doğrulandı)
- OHLC'de AL bölgelerinin "bulut" gibi görünmesi düzeltildi (`_drawColoredRuns` artık her run'u
  kendi ayrı `candle_series`'i olarak çiziyor, aynı state'in uzak run'larını birleştirmiyor).
- AL/SAT boyunca yatay seviye çizgileri kayboluyordu: **veri sorunu değildi**,
  `TradeSignalRenderer.MAX_SIGNAL_EVENTS` (20000) 928K bar'lık gerçek veri setindeki 26013 sinyal
  olayını aşıyordu → güvenlik supabı harf/çizgileri atlıyordu. `MAX_SIGNAL_EVENTS` 50000'e çıkarıldı.
- Panel yükseklikleri artırıldı (Signals 200, PnL/PnL%/Return/Return% 220).
- Y ekseni etiketi artık `panel.caption`'dan gelir (eskiden sabit "y"); `Panel.yLabel` ile
  caption'dan BAĞIMSIZ ayarlanabiliyor (view.json'da `"yLabel"`).
- Seri rengi genel/dışarıdan set edilebiliyor: `PanelData.color` artık gerçekten kullanılıyor
  (`Panel.addData(color=...)` → `PanelManager._applySeriesColorTheme`), view.json'da `"color"`.
  Signals=cyan, PnL/PnL%=yellow.
- `Panel.yFixedRange`/`ySyncMode="fixedRange"` (önceden dead code) gerçekten devreye alındı
  (`_getFixedYRange`, `_applyAxisPadding`+`adjustYAxis`'te kullanılıyor) — Signals paneli her zaman
  -2..2 Y aralığında sabit.
- "Show Plot Captions" checkbox'ı eklendi (Show OHLC'nin altına) — `PanelManager.setShowCaptions()`
  ile `no_title` + `caption_spacer_{id}` (15px) toggle ediliyor, kalıcı tercih (yeni panellere de uygulanıyor).
- Info panel (`hover_text_{id}`) ve mouse-pos overlay (`mouse_pos_text_{id}`) konumları
  caption açık/kapalıya göre iki ayrı Y kullanıyor (`_infoPanelY*`/`_mousePosY*`).
- `updateMousePosOverlays()` yeniden yazıldı: artık info panel'in kullandığı AYNI paylaşım
  mekanizmasını (`_currentHoverInfoIndex`/`_infoSharedIndex`/`_resolveInfoIndex`) kullanarak TÜM
  panellerde senkron `X: <bar>  Y: <değer>` gösteriyor (eskiden sadece hover edilen panelde).
- Info panelin X konumu artık panelin Y-eksen değerlerinin basamak sayısına göre dinamik
  (`_estimateYAxisLabelWidth`, span'e göre ondalık basamak da hesaba katıyor) + `max(80, …)` alt
  sınır — OHLC gibi geniş sayılarda sola çok yakın düşme sorunu çözüldü.
- X ekseni tarih/saat formatı tartışıldı (`"%d.%m.%Y\n%H:%M:%S"` iki satır kaplıyor, tek satıra
  `"%d.%m.%Y %H:%M:%S"` yapılması öneriliyor) — **henüz uygulanmadı, sıradaki küçük iş.**

## AÇIK / SIRADAKİ İŞLER

1. **Gerçek `PlotBackend` switch'i**: Şu an converter+process başlatma test hook üzerinden
   çalışıyor. Gerçek entegrasyon: `Program.cs`/`AlgoTrader.SetupPython()` seviyesinde
   `PlotBackend { ImguiBundle, DearPyGui }` seçimi eklenip mevcut pythonnet çağrısının YANINA
   (switch ile) düzgünce oturtulacak.
2. Switch bitince **`[9]` demo menüsü + geçici test hook'u silinecek** (ikisi de kod içinde TODO
   ile işaretli).
3. X ekseni datetime formatını tek satıra indirmek (`"%d.%m.%Y %H:%M:%S"`) — `panelManager.py:38`
   (`_dayChangeFormat`) hâlâ iki satırlı (`"%d.%m.%Y\n%H:%M:%S"`). Not: `setXAxisMode()` artık
   `dateTimeFormat` parametresi kabul ediyor (satır 1252-1276), mekanizma hazır — sadece varsayılan
   değer değiştirilecek.
4. ~~`src/DearPyGuiDataPlotter` kopyasının kendi `.git`'i var (nested repo)~~ — **ÇÖZÜLDÜ**: kontrol
   edildi, nested `.git` yok. `.venv` artık `.gitignore` (`**/.venv/`) ile hariç tutuluyor,
   `setupPythonEnvs.bat` ile kuruluyor — nested repo endişesi kalmadı.
5. Kozmetik/küçük ince ayarlar (renk/yükseklik/etiket) — kullanıcı isterse tek tek istenebilir,
   mekanizmaların hepsi (color, yLabel, yFixedRange, height) artık view.json üzerinden dışarıdan
   set edilebilir durumda.

---

Kalan açık işler (1-3) [docs/todo.md](todo.md)'ye de bağlandı.
