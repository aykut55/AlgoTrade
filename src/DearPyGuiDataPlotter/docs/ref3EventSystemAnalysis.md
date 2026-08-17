# Ref3 Etkileşim/Event Sistemi Analizi (EventManager öncesi araştırma)

Kaynak: `D:\Aykut\Projects\Python ImGui Denemeleri\_eskiler\DearPyGuiDataPlotter3\src\plotting\`
İlgili dosyalar: `interaction_manager.py`, `interaction_state.py`, `plot_controller.py`,
`range_controller.py`, `lod_controller.py`, `panel_manager.py` (hover/click üretim kısmı),
`settings.py`, `gui_manager.py` (wiring).

Amaç: aktif projeye (`DearPyGuiDataPlotter`) eklenecek merkezi `EventManager` class'ı için
zemin hazırlamak. Bu dosya sadece **analiz** — henüz kod yazılmadı.

---

## 1. Önemli bulgu: Ref3'te İKİ farklı katman var, birbirine BAĞLANMAMIŞ

Ref3'ün bu kopyasını incelerken kritik bir şey ortaya çıktı:

- **Gerçekte çalışan wiring** (`gui_manager.py` docstring'i: *"LAYOUT SURUMU (etkilesim yok)"*):
  `GuiManager.__init__` içinde `PlotController` + `RangeController` + `LodController`
  doğrudan elle örnekleniyor, callback'ler setter'larla birbirine bağlanıyor
  (`gui_manager.py:44-85`). `PanelManager` bunlara `set_plot_controller` /
  `set_lod_controller` / `set_panel_visible_callback` / `set_panel_active_callback` ile
  referans veriliyor. Merkezi bir "EventManager" YOK — orkestrasyon `GuiManager`'ın
  içine dağılmış durumda.
- **Kullanılmayan bir prototip:** `interaction_manager.py` — tam olarak kullanıcının
  tarif ettiği şeyi yapan bir facade/coordinator class (`InteractionManager`). Ama
  `grep -rn "InteractionManager"` sonucu SADECE kendi dosyasında geçiyor — hiçbir yerden
  import edilmiyor, `gui_manager.py` bunu hiç kullanmıyor. Yani bu, "hepsini tek yere
  toplayalım" fikrinin denendiği ama **gui_manager'a hiç entegre edilmemiş** bir taslak.

**Sonuç:** Kullanıcının hatırladığı "tüm event/callback'leri merkezi yere bağladık" şeyi,
muhtemelen `InteractionManager`'ın tasarım niyeti — ama Ref3'ün bu spesifik
kopyasında fiilen devreye alınmamış. Yine de `InteractionManager` + onu besleyen
`PlotController`/`RangeController`/`LodController`/`InteractionState` bir bütün olarak
**tam olarak istenen merkezi EventManager mimarisinin taslağı**; aktif projeye
taşınacak model budur.

---

## 2. Bileşen bileşen analiz

### 2.1 `InteractionState` (interaction_state.py, 128 satır)
- Tüm etkileşim durumunun tutucusu: `active_panel_id/active_plot_id/active_event`,
  `last_hovered_*`, `last_clicked_*`, `last_plot_control_event`, `event_sequence`.
- **İKİ ayrı üretim yolu, TEK ortak yayın kanalı:**
  1. `set_hovered(panel_id, plot_id, sample)` / `set_clicked(...)` → hover/click
     (kaynağı: panel'e bağlı `item_hover_handler`/`item_clicked_handler`)
  2. `set_plot_control_event(event)` → zoom/pan/box/double-click
     (kaynağı: `PlotController`'ın GLOBAL mouse/klavye handler'ları)
- `add_listener(callback)` ile abone olunur; `_emit(event)` ikisini de aynı normalize
  şemaya (`{action, panel_id, plot_id, x_axis, y_axis, sender, app_data, frame,
  mouse_pos, selection, sample}`) sokup TÜM dinleyicilere yayınlar.
- Kod içi uyarı (README yorumu): hover/click üretim metodları "gereksiz" sanılıp
  silinmemeli — bunlar iki ayrı kaynağı BİRLEŞTİRMEZ, sadece ikisini de ортak akışa
  ek olarak yayınlar.

### 2.2 `PlotController` (plot_controller.py, 989 satır) — en büyük ve en kritik parça
- **Register API:** `register_plot(panel_id, plot_id, x_axis, y_axis)` her plota
  gizli (no_inputs) dikey+yatay crosshair `drag_line` ekler, plot'u bir listede
  (`_plot_infos`) tutar.
- **Global handler'lar** (`ensure_handlers()`, idempotent, tek `handler_registry`):
  - `mvKey_Spacebar` (adjust-Y — kodda fiilen NO-OP bırakılmış, `return` ile devre dışı)
  - `LShift`/`RShift` press/release → Y eksenlerini kilitle/aç (scroll sadece X zoomlasın)
  - `mouse_move` → crosshair güncelle
  - `mouse_wheel` → zoom (scope: x/y/xy, plotun kenar şeridine göre otomatik tespit,
    bkz. `_get_zoom_scope`); Ctrl+wheel → özel "ctrl wheel pan" callback'i
  - `mouse_drag` (left/middle/right, threshold=1.0) → pan / middle_pan / box_zoom_drag
  - `mouse_down/release` (right, middle) → box-selection ve middle-pan başlangıç/bitiş
  - `mouse_click` (middle, right) ve `mouse_double_click` (left) → ayrı action'lar
- **Event üretimi tek noktadan:** `_emit_plot_control_event(action, ...)` →
  `_on_plot_control_event(...)` → hem `interaction_state.set_plot_control_event(event)`
  hem opsiyonel `plot_control_event_callback(event)` çağrılır.
- **Crosshair modları:** `None | Source | All` — `Source`: sadece imlecin üstündeki
  plotta tam crosshair; `All`: imlecin üstündeki plotta tam, DİĞER TÜM plotlarda SADECE
  dikey çizgi (farklı Y ölçekleri anlamsız olacağı için). Harici (örn. range slider'dan
  gelen) dikey crosshair için `set_external_vertical_crosshair` + 1-frame "taze mi" kontrolü.
- **Link (eksen bağlama) modları:** `None | All | Custom`.
  - `All`: kaynak plotun X+Y'si diğer TÜM plotlara.
  - `Custom`: `add_axis_link_group(group_id, axis, panel_ids)` ile tanımlı gruplar
    (panel_ids yerine `"ALL"`/`-1` sentinel'i de olabilir, dinamik çözülür).
- **Eksen kilidi / gecikme yönetimi (ÇOK ÖNEMLİ — "gecikme/crash olmasın" burada çözülmüş):**
  - `set_axis_limits` bir ekseni SABİTLER (kilitler) — pan/zoom durur.
  - Limit uygulandıktan sonra **iki aşamalı** kilit açma: `_pending_unlock_axes` →
    (bir frame render edilsin diye bekle) → `_unlock_ready_axes` → `_release_axis_lock`
    (`set_axis_limits_auto`). Neden iki aşama: limit uygulanır uygulanmaz auto'ya
    geçilirse ImPlot yanlış/dar veriye fit eder; en az 1 frame render sonrası açılmalı.
  - `apply_saved_params_to_other_plots` gibi bazı yerlerde `dpg.split_frame()` (senkron,
    hemen) kullanılıyor; `apply_x_range_to_all_plots` (range/scroll kaynaklı) ise
    split_frame KULLANMIYOR — çünkü range marker'ları `drag_line`, ve split_frame'in
    açtığı iç-içe frame ImGui'nin sürükleme aktif-id'sini bozup marker'ı
    hareket ettirilemez hale getiriyordu. Bunun yerine kilit açma bir sonraki
    **merkezi frame-tick**'e devrediliyor.
  - **Re-entrancy guard:** `_auto_apply_in_progress` bayrağı — sync içindeki
    `split_frame` yeni kareler render edip aynı handler'ı tekrar tetikleyebiliyor;
    bayraksız bırakılsa sonsuz döngü/çökme riski var.
- **Adjust-Y:** `adjust_y_to_visible(plot_id)` / `adjust_all_plots_y()` — sadece
  GÖRÜNÜR X penceresindeki min/max'a göre Y'yi %10 pay ile ayarlar (candle serisi
  için low/high, line için ys). Veri yoksa `fit_axis_data` fallback'i.
- **Double-click / reset:** native DPG çift-tık fit'i sadece o plotun yüklü alt
  kümesine fit eder (LOD nedeniyle); bu yüzden merkezi katman X'i (aktif view-mode
  hook'u varsa onunla, yoksa full range) ve Y'yi (her plot `fit_axis_data`) yeniden uygular.
- **Mouse-hit-testing:** `_get_item_rect_and_mouse` ile plot dikdörtgeni + mouse
  pozisyonu (local/global iki uzayı da dener) — "hangi plotun üstündeyim" tespiti
  `is_item_hovered` yerine elle rect testiyle yapılıyor (global handler'lar için
  DPG'nin per-item hover'ı yeterli değil).

### 2.3 `RangeController` (range_controller.py, 618 satır)
- Range Slider (overview plot + Start/End `drag_line` + shade) + yatay Scroll Bar —
  aktif projedeki `rangeSliderBar.py`'nin atası, ama burada GERÇEKTEN plotlara bağlı.
- `set_visible_range_changed_callback(callback)` — görünür X penceresi değişince
  (marker sürükle / scroll / pan-step butonları / view-mode apply) çağrılır; bu callback
  `InteractionManager._on_range_visible_changed` → `plot_controller.apply_x_range_to_all_plots`.
- **Geri yön (plot → range):** `sync_visible_range_from_plot(x_min, x_max)` —
  `notify=False` ile çağrılır, yani range'in kendi widget'larını (marker/scroll/shade)
  günceller ama TEKRAR plotlara callback GÖNDERMEZ (feedback loop'u böyle kırıyor).
- Kendi mouse handler'ları var (overview plot üstünde sürükleyerek pan) — `PlotController`
  ile TAMAMEN ayrı bir `handler_registry`.
- Scroll bar min/max'ı görünür pencere genişliğine göre HER güncellemede yeniden
  hesaplanıyor (sabit değil) — "tüm veri görünüyor" durumunda thumb'ın sağda durması
  için özel `_is_full_range_visible` / `_scroll_min_value` / `_scroll_max_value` mantığı.
- `set_slider_visible` / `set_scrollbar_visible` — aktif projedeki
  `RangeSliderBar.setSliderVisible/setScrollbarVisible`'ın orijinali (isimler + fikir
  birebir buradan taşınmış).

### 2.4 `LodController` (lod_controller.py, 269 satır)
- **PASİF gözlemci** — hiçbir eksen limiti / event ÜRETMEZ, sadece merkezi
  per-frame tick'ten `apply()` çağrılır.
- Görünür X penceresine göre TÜM kayıtlı serileri decimate edip (`lod.decimate_min_max`
  / `decimate_ohlc`) `set_value` ile günceller — 5M nokta çizmek yerine ekran genişliği
  kadar (`_plot_target`, plotun piksel genişliği) nokta çizilir.
- Değişiklik algılama: son (x_min, x_max, target) ile karşılaştırıp AYNIYSA hiçbir
  şey yapmaz (gereksiz `set_value` yok → performans).
- LOD bilgi metnini (görünen/toplam oranı) plotun sağ-üst köşesine `viewport_drawlist`
  üzerine `draw_text` ile yazıyor (annotation değil — ekran-uzayı, pan/zoom'da kaymaz).

### 2.5 `InteractionManager` (interaction_manager.py, 369 satır) — kullanılmayan facade
- Yukarıdaki 4 bileşeni (`PlotController`, `LodController`, `RangeController`,
  `InteractionState`) COMPOSE eder; onların iç frame-timing mantığına DOKUNMAZ.
- **Kayıt API'si:** `register_plot`, `register_series`, `register_range`,
  `register_listener`, `bind_plot_interaction_handlers` (hover/click item handler'ı
  BURADAN bağlanıyor — `PlotController`'ın global handler'larından AYRI bir üretim
  yolu, `_on_plot_hovered`/`_on_plot_clicked`).
- **Merkezi per-frame tick (`start()` → `_start_frame_tick`):** TEK bir
  `set_frame_callback` zinciri — `_apply_pending_plot_sync()` →
  `lod_controller.apply()` → `plot_controller.release_pending_unlocks()`. Yorum:
  "Tek frame-callback zinciri → birden çok set_frame_callback çakışması yok" (aktif
  projede `guiManager._scheduleRenderLoop` AYNI fikri zaten kullanıyor).
- **`_on_plot_control_event`:** tüm sync mantığı (adjust-Y, link sync, plot→range
  yansıması, double-click reset) burada toplanmış; UI durum metni opsiyonel hook.
- **Tüm UI kancaları (callback setter'ları) OPSİYONEL** — hiçbiri bağlanmasa da
  çekirdek sync (link, range↔plot, double-click, LOD, hover/click emit) çalışır.
  `GuiManager` sadece kendi zengin UI'sini beslemek için bunlara bağlanır.

### 2.6 `Settings` (settings.py, 20 satır)
- Tek sorumluluk: `active_update_mode` (`Click | Hover | Click+Hover`) — hover/click
  event üretiminin GATE'i. Aktif projedeki `panelManager._activeUpdateMode`
  (`hover`/`click`, iki mod) bunun sadeleştirilmiş hali.

### 2.7 `PanelManager` (panel_manager.py) — hover/click üretim tarafı
- `_build_panel_ui` içinde her plot için `item_visible_handler` +
  `item_hover_handler` + `item_clicked_handler` bağlanıyor, `user_data=(panel_id,
  plot_id, "hover"|"click")` ile TEK bir `_on_panel_active` callback'ine akıyor →
  `GuiManager._set_active_plot` (veya `InteractionManager` kullanılsaydı
  `_on_plot_hovered`/`_on_plot_clicked`).
- Bu, `PlotController`'ın GLOBAL handler'larından bağımsız, panel-bazlı (item-level)
  ikinci bir üretim yolu — `InteractionState` bölümünde bahsedilen "iki ayrı yol" budur.

---

## 3. Event kataloğu (action string'leri + şema)

Tüm event'ler ortak şema kullanır:
`{action, panel_id, plot_id, x_axis, y_axis, sender, app_data, frame, mouse_pos, selection, sample}`
(hover/click event'leri `x_axis`/`y_axis`/`selection`'ı `None` bırakır.)

| Kategori | action değerleri | Kaynak |
|---|---|---|
| Hover/Click | `hover`, `click` | `PanelManager`/`InteractionManager` item handler'ları |
| Zoom (wheel) | `zoom_x_in/out`, `zoom_y_in/out`, `zoom_xy_in/out` | `PlotController._on_mouse_wheel` (scope: kenar şeridi tespiti) |
| Ctrl+wheel pan | `ctrl_wheel_pan_left/right` | `PlotController._on_mouse_wheel` (ctrl gate) |
| Pan (sol sürükle) | `pan_left/right/up/bottom` | `PlotController._on_left_mouse_drag` |
| Orta-tuş pan | `middle_pan_left/right/up/bottom` | `PlotController._on_middle_mouse_drag` |
| Box-zoom sürükle | `box_zoom_drag` | `PlotController._on_right_mouse_drag` |
| Box-seçim (sağ tık bas-bırak) | `right_press`, `right_release`, `box_selection` | `PlotController._on_right_mouse_down/release` |
| Orta tık | `middle_button_down`, `middle_button_release`, `middle_click` | `PlotController` orta tuş handler'ları |
| Sağ tık | `right_click` | `PlotController._on_right_mouse_click` |
| Çift tık | `double_click` | `PlotController._on_mouse_double_click` |
| Adjust-Y | `adjust_y_active`, `adjust_y_all` | SPACE/SHIFT+SPACE (kodda NO-OP, tetikleyici kaldırılmış) |

`_is_interactive_axis_action`: `pan_*`, `middle_pan_*`, `zoom_*`, `box_selection`,
`double_click` — bu action'lar geldiğinde ÖNCE o plotun x/y ekseninin bekleyen kilidi
(varsa) hemen serbest bırakılıyor (kullanıcı native etkileşime başladıysa merkezi
senkron kilidi engellememeli).

---

## 4. Merkezi senkron kuralları (özet)

1. **Link sync** (`_sync_from_source_params` / `sync_links_for_plot`): pan/zoom sonrası
   kaynak plotun X'i (ve link moduna göre Y) diğer plotlara uygulanır — ama HEMEN değil,
   bir sonraki frame'de (`_pending_plot_sync_event` + `_pending_plot_sync_wait=5` frame
   bekleme) çünkü native pan/zoom bu callback'ten SONRA ekseni güncelliyor.
2. **Plot → Range geri-yansıma** (`_update_range_from_plot`): pending sync uygulanınca
   kaynak plotun GÜNCEL X limiti range widget'larına `notify=False` ile yansıtılır
   (loop yok).
3. **Range → Plot** (`_on_range_visible_changed`): guard YOK (bilinçli — aksi halde
   double-click içindeki mode-apply bloklanır), çünkü `apply_x_range_to_all_plots`
   split_frame kullanmadığı için re-entrancy riski yok.
4. **Double-click reset**: X için hook (aktif view-mode) veya range `apply_full()`;
   Y için her plota `fit_axis_data`.
5. **LOD**: merkezi tick'te HER ZAMAN `apply()` çağrılır (kilit açmadan ÖNCE) —
   sıra önemli: önce güncel pencereye göre seriyi doldur, SONRA kilidi aç; aksi halde
   auto-unlock yanlış (eski) pencereye oturur.

---

## 5. Gecikme/çökme önleme teknikleri (kullanıcının özellikle istediği kısım)

- **Tek frame-callback zinciri**: birden fazla yerin kendi `set_frame_callback`'ini
  kaydetmesi DPG'de çakışıyor ("frame başına tek callback" kısıtlaması — aktif
  projede `guiManager.py`'deki `_alignTopViewNRow` yorumunda da AYNI sorun zaten
  yaşanmış ve aynı çözümle -her frame çalışan tek `render()` döngüsü- çözülmüş).
- **İki aşamalı eksen kilidi açma** (`_pending_unlock_axes` → `_unlock_ready_axes`):
  limit uygulanır uygulanmaz auto-fit'e geçmek yanlış/dar aralığa fit eder; en az
  1 frame render sonrası açılmalı.
- **split_frame'den bilinçli kaçınma**: range/scroll kaynaklı senkronda split_frame
  KULLANILMIYOR çünkü drag_line sürüklemesinin aktif-id'sini bozuyor (marker'ı
  "yapışkan" hale getiriyor, kullanıcı deneyimini kırıyor).
- **Re-entrancy guard bayrağı** (`_auto_apply_in_progress`): sync içindeki
  split_frame yeni kareler render edip AYNI handler'ı tekrar tetikleyebiliyor;
  bayrak olmazsa sonsuz döngü/çökme riski.
- **Dedup / frekans düşürme**: hover event'i her frame yerine SADECE
  `(panel_id, plot_id, nearest_index)` değiştiğinde yayınlanıyor (`_last_hover_report_key`).
- **LOD değişiklik algılama**: (x_min, x_max, target) aynıysa `set_value` ATLANIYOR —
  gereksiz seri güncellemesi = gereksiz CPU/GPU işi.
- **try/except (KeyError, SystemError, Exception) sarmalayıcılar**: DPG'nin
  silinmiş/henüz oluşmamış item'lara erişimde attığı hatalar (özellikle
  `get_axis_limits`/`get_item_rect_min` gibi) yutuluyor — tek bir kırık item
  tüm event zincirini ÇÖKERTMESİN diye.

---

## 6. Aktif projede (DearPyGuiDataPlotter) şu an ne var, ne yok

`src/plotting/panelManager.py` içinde ZATEN kısmen karşılığı olanlar:
- `_onPlotClicked` / `_onPlotDoubleClicked` (item-level, hover+click+dblclick — ama
  SADECE aktif panel seçimi ve padding-reset için; zoom/pan/box-select/adjust-Y YOK)
- `setActiveUpdateMode`/`getActiveUpdateMode`/`updateActivePanel` (Ref3'teki
  `Settings.active_update_mode`'un sadeleşmiş hali)
- `setCrossHairMode`/`updateCrossHairOverlays` (Ref3'teki `PlotController` crosshair
  mantığının bir kısmı — ama GLOBAL mouse_move handler yerine HER FRAME poll ediliyor)
- `setInfoPanelMode`/`updateInfoOverlays` (Ref3'te de aynı isimle var, hover_text_{id})
- `guiManager._scheduleRenderLoop` (Ref3'teki "tek frame-callback zinciri" fikriyle
  birebir aynı çözüm, zaten bağımsız keşfedilmiş)

**Aktif projede TAMAMEN EKSİK olanlar** (Ref3'te `PlotController`'da var):
- Global mouse wheel zoom (x/y/xy scope tespiti), sol/orta/sağ sürükleme ile
  pan/box-zoom/box-select, çift tık ile "tüm veriye + tüm Y'lere reset"
- Link modları (None/All/Custom) — birden fazla panelin ekseninin birbirine bağlanması
- Adjust-Y (görünür pencereye göre Y fit)
- Range Slider ↔ gerçek plot X ekseni bağlantısı (`rangeSliderBar.py` şu an SADECE
  görsel iskelet, hiçbir plot'a bağlı değil — bkz. kendi docstring'i)
- LOD (decimation) katmanı — `panelManager.py`'de `_render_cache_*` benzeri bir iz
  var mı ayrıca bakılmalı (bu analiz dosyasında doğrulanmadı)
- İki aşamalı eksen kilidi / pending-unlock mekanizması
- Merkezi bir `EventManager`/`InteractionState` — event'lerin tek bir yerden
  abone olunabilir şekilde yayınlanması yok; her callback kendi işini yapıp bitiyor

---

## 7. EventManager için önerilen kapsam (sonraki adım — henüz kod YOK)

Bu sadece bir kapsam/checklist taslağıdır, kullanıcı onayı olmadan uygulanmayacaktır:

- [ ] Merkezi event şeması (Ref3'teki `{action, panel_id, plot_id, x_axis, y_axis,
      sender, app_data, frame, mouse_pos, selection, sample}` + `register_listener`)
- [ ] Global plot-kontrol handler'ları (tek `handler_registry`, idempotent `ensure_handlers`)
- [ ] Hover/click üretimi mevcut `panelManager._onPlotClicked` ile birleştirilecek
      mi yoksa ayrı mı kalacak (Ref3 İKİ ayrı yol kullanıyor — karar gerekiyor)
- [ ] Link modları (None/All/Custom) + eksen bağlama grupları
- [ ] Adjust-Y (aktif/tümü)
- [ ] Çift-tık reset (X: view-mode hook, Y: fit)
- [ ] Range Slider ↔ plot X ekseni gerçek bağlantısı (`rangeSliderBar.py` şu an
      sadece iskelet — bu iş EventManager'ın ilk gerçek tüketicisi olabilir)
- [ ] İki aşamalı eksen kilidi açma (pending → ready → release)
- [ ] TEK merkezi frame-tick (`guiManager._scheduleRenderLoop` ile birleştirilecek,
      YENİ bir `set_frame_callback` zinciri AÇILMAYACAK — çakışma riski)
- [ ] Re-entrancy guard'lar (sync sırasında split_frame kaynaklı tekrar-tetikleme)
- [ ] LOD katmanı (varsa mevcut render-cache mekanizmasıyla ilişkisi netleştirilecek)

Sıradaki adım kullanıcının onayına bağlı — adım adım tarif edilecek.
