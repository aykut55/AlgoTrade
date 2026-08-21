> **⚠️ ESKİMİŞ — güncel kod farklı (2026-08-21).** Bu belgenin "kalıcı" saydığı `00ff0d5` commit'i
> repoda bulunamadı ve önerilen metod isimleri (`onSync`, `_dispatchSync`,
> `applyClickEventToOthers`, `applyDoubleClickEventToOthers`, `applyZoomEventsToOthers`,
> `applyPanEventsToOthers`, `_applySrcToOthers`, `_applyInProgress`, `readSourceParams`,
> `applySavedParamsToOthers`) kodda yok. Gerçek kod farklı isimlerle bir sync mekanizması
> kullanıyor: `interactionManager.py`'de `scheduleSyncOthers`/`syncOthers`/
> `getPendingSyncCount`/`_applyPendingSyncToPanel`, `panelManager.py`'de
> `readPanelPlotParams`/`getLastReadPlotParams` (bağlandığı yer: `guiManager.py:486-491`
> `_onApplySrcParams`). Bu belgeyi tasarım niyeti için tarihsel referans olarak oku, davranış
> açıklaması olarak güvenme.

# InteractionManager — Tasarlanan Davranış (kod revert edilse bile kalıcı özet)

Bu dosya, `interactionManager.py`/`guiManager.py` üzerinde henüz commit edilmemiş
(working tree'de duran) değişikliklerle birlikte tasarlanan davranışı özetler.
Kod revert edilirse bu belge, aynı davranışı tekrar uygulamak için referans olsun
diye yazıldı.

## Commit edilmiş (güvende, revert etkilemez)

`00ff0d5` commit'inde şunlar zaten kalıcı:
- Panel/plot kayıt defteri (`registerPanel`/`unregisterPanel`, `panelManager.py`
  `drawPanel`/`deletePanel`/`deleteAllPanels` içinden çağrılır).
- Global mouse+klavye handler'ları (`ensureHandlers`): hover, wheel zoom
  (`zoom_x/y/xy_in/out`), sol/orta/sağ sürükleme (pan/middle_pan/box_zoom),
  sağ tık bas-bırak (box_selection), sol/orta/sağ tık, çift tık, key press/release.
- Event şeması (`eventId`, `action`, `panelId`/`plotId`/`region`, `x`/`y`/`xBar`/`yBar`,
  `xAxisLimits`/`yAxisLimits`, `screenRect`).
- Yakalama (`_emit`/`_emitKeyEvent`, sadece `_eventLog`'a ekler) ile basma
  (`onTick`→`flushEventLog`, toplu I/O) ayrımı — hızlı ardışık event'lerin print
  gecikmesinden dolayı kaybolmasını önlemek için.
- Event sahibi tespiti `panelManager.getActivePanelId()` üzerinden (kendi
  bağımsız hit-test YOK — topPanelGroupBox1'deki "Active Panel" göstergesiyle
  aynı kaynak).

## Henüz commit edilmemiş (revert edilirse KAYBOLACAK) — bu oturumda tasarlanan davranış

### 1. Yakalama ↔ Uygulama ayrımı

- `onTick()` her frame iki adım çalıştırır: önce `onSync()` (henüz flush
  edilmemiş `_eventLog`'u okuyup uygular), sonra `flushEventLog()` (basar ve
  kuyruğu boşaltır). Aynı event kümesini iki farklı sorumluluk (uygulama,
  yazdırma) ayrı ayrı işler.
- "Bir method tüm event'leri yakalıyor, başka bir method da uyguluyor" prensibi:
  `_emit`/`_emitKeyEvent` SADECE yakalar; `onSync`/`_dispatchSync`/`applyXxx...`
  SADECE uygular. Hiçbir zaman tek bir dev "genel apply" metodu yazılmadı,
  bilerek kategori bazlı ayrı metodlara bölündü:
  - `applyClickEventToOthers(event)` — şu an bilerek NO-OP (hangi durumun
    yansıyacağına karar verilmedi, sadece simetri için duruyor).
  - `applyDoubleClickEventToOthers(event)` — aşağıda ayrı madde.
  - `applyZoomEventsToOthers(event)` → `_applySrcToOthers(event)`.
  - `applyPanEventsToOthers(event)` → `_applySrcToOthers(event)`.
  - `_dispatchSync(event)`: `action` alanına göre yukarıdakilerden birine
    yönlendirir (`"click"`, `"double_click"`, `zoom_*` prefix, `pan_*`/`middle_pan_*` prefix).
  - Hangi action'ların gerçekten bu senkronu tetikleyeceğine (örn. box_selection
    dahil olsun mu) İLERİDE karar verilecek — şu an sadece click/double_click/zoom/pan bağlı.

### 2. Src → diğer panellere uygulama (`_applySrcToOthers`)

- `event['panelId']` = **src** (kaynak panel, hangi panelde pan/zoom oldu).
- `self._panels` içindeki TÜM kayıtlı panelleri gezer:
  - `panelId == srcPanelId` ise **skip**.
  - Panel **görünür değilse** (`panelManager.getPanel(panelId).getVisible() == False`)
    **skip** — sadece görünür panellere dokunulur.
  - **X ekseni limiti HER ZAMAN** (global, gruplama yok) diğer panellere uygulanır.
  - **Y ekseni limiti SADECE** src panel ile AYNI `ySyncId`'ye sahip panellere
    uygulanır (`panel.ySyncId`, bkz. `panel.py` `setYSyncId` — script tarafında
    örn. `ohlcPanel.setYSyncId(0)`, `movAvgPanel.setYSyncId(0)` gibi set edilir;
    `None` ise hiçbir gruba dahil değildir, Y hiç senkronlanmaz).

### 3. Re-entrancy guard (`_applyInProgress`)

- `_applySrcToOthers`/`applyDoubleClickEventToOthers` çalışırken
  `self._applyInProgress = True` yapılır (try/finally ile işlem bitince `False`).
- `_emit` ve `_emitKeyEvent`'in EN BAŞINDA `if self._applyInProgress: return`
  guard'ı var — diğer panellere `set_axis_limits`/`fit_axis_data` uygularken
  o panellerin de "src" gibi davranıp yeni bir senkron zinciri başlatmasını
  (sonsuz döngü/zincirleme hata riski) engeller. Ref3'teki
  `_auto_apply_in_progress` bayrağıyla aynı fikir.

### 4. Çift tık davranışı — "herkes resetlensin, full data göstersin"

- `applyDoubleClickEventToOthers`, `_applySrcToOthers`'tan FARKLI çalışır:
  src'nin GÖRÜNÜR penceresini diğerlerine KOPYALAMAZ.
- Bunun yerine: src HARİÇ tüm görünür panellerin kendi X VE Y eksenini
  `dpg.fit_axis_data` ile **kendi tam verisine** fit eder (src zaten native
  DPG double-click-to-fit + `panelManager._onPlotDoubleClicked`'ın kendi
  padding-reset'iyle kendi kendine resetleniyor, o yüzden döngüde atlanır).

### 5. Read Params (src) / Apply Params (dst) butonları

- `topPanelGroupBox3`'teki (önceden placeholder, event'siz) iki butona
  callback bağlandı:
  - **Read Params (src)** → `guiManager._onReadSrcParams` →
    `interactionManager.readSourceParams(srcId=None, eventTypes=None)`:
    `srcId` verilmezse `panelManager.getActivePanelId()` kullanılır; o panelin
    GÜNCEL X/Y eksen limitlerini `self._savedSourceParams`'a kaydeder.
    `eventTypes` parametresi şu an sadece saklanır, davranışı etkilemiyor
    (ileride "sadece zoom" / "sadece pan" ayrımı gerekirse kullanılacak).
  - **Apply Params (dst)** → `guiManager._onApplySrcParams` →
    `interactionManager.applySavedParamsToOthers()`: son `readSourceParams`
    çağrısında saklanan src'yi, AYNI `_applySrcToOthers` kuralıyla (X global,
    Y sadece `ySyncId` eşleşirse, sadece görünür panellere) diğerlerine uygular.
  - Ref3'teki `PlotController.read_source_params`/`apply_saved_params_to_other_plots`
    ikilisiyle aynı iki-adımlı model.

### 6. `centerCenterPanel` scroll fix'i

- `no_scroll_with_mouse=True` eklendi: bu container (panelleri barındıran dış
  scroll alanı) artık mouse tekerleğiyle HİÇ kaydırılamıyor (sadece scrollbar'ı
  sürükleyerek) — önceden scrollbar'a tıklandıktan sonra tekerlek, plot zoom'u
  yerine container'ı kaydırmaya "yapışıyordu".

### 7. Bilinen ÇÖZÜLMEMİŞ sorun — scrollbar sonrası native pan/zoom kaybı

- **Belirti**: `centerCenterPanel`'in dikey scrollbar'ına bir kez tıklandıktan
  SONRA, bir panel/plota tıklanınca InteractionManager event'i DOĞRU şekilde
  yakalıyor (`click` event, doğru `panelId` ile oluşuyor) ama plotun kendi
  NATIVE (DPG/ImPlot) pan/zoom davranışı bir daha çalışmıyor — grafik
  değişmiyor, ne pan ne zoom oluyor.
- **Teşhis**: Bu muhtemelen ImGui/ImPlot'un dahili "aktif item" durumunun
  scrollbar'da takılı kalmasıyla ilgili NATIVE bir davranış — bizim Python
  seviyesindeki `InteractionManager` kodumuzun kontrol edemediği bir şey
  (event capture'ımız çalışıyor, ama görsel pan/zoom'un kendisi DPG'nin C++
  tarafında oluyor).
- **Denenen fix'ler** (henüz kullanıcı tarafından doğrulanmadı):
  1. `centerCenterPanel`'e `no_scroll_with_mouse=True` (madde 6) — tek başına
     yetmedi, sorun devam etti.
  2. `_onLeftMouseClick` içinde, tıklanan plota `dpg.focus_item(plotId)` ile
     zorla focus vermeyi deneme — hipotez tabanlı, garanti değil, sonucu
     bilinmiyor.
- **Sonraki adım fikirleri** (henüz uygulanmadı): scrollbar yerine farklı bir
  panel-navigasyon yöntemi (örn. önceki/sonraki panel butonları), ya da DPG'nin
  bu tür nested-scroll + ImPlot etkileşimi için bilinen bir workaround'u olup
  olmadığının araştırılması.

## Özet: revert edilirse ne kaybedilir

Yukarıdaki 1-7 arası TÜM madde (senkron mekanizması, re-entrancy guard, Y-sync
grupları kullanımı, çift-tık full-reset davranışı, Read/Apply Params butonları,
scroll fix'i, focus_item denemesi) — sadece bu dosyada yazılı kalır, kod
tarafında YENİDEN YAZILMASI gerekir. Kayıt defteri, global handler'lar, event
şeması ve yakalama/basma ayrımı (commit `00ff0d5`) etkilenmez, olduğu gibi kalır.
