from imgui_bundle import imgui


class InteractionManager:
    """Merkezi etkilesim/event yoneticisi (DearPyGuiDataPlotter'daki
    InteractionManager'in imgui_bundle/implot PORTU - mimari/sorumluluklar
    AYNI, sadece event URETIM mekanizmasi DPG'nin kalici handler_registry'sinden
    her-frame polling'e donustu, bkz. ensureHandlers/onTick).

    Ayni uc parca:
      1) Panel kayit defteri (register/unregister, bkz. panelManager.py). DPG
         surumu plot/xAxis/yAxis TAG'lerini saklayip sonradan dpg.* ile
         sorguluyordu - immediate-mode implot'ta boyle kalici bir tag sorgusu
         YOK, bu yuzden burada sadece panelId UYELIGI tutulur; gercek hover/
         rect/eksen bilgisi PanelManager.getPanelHoverInfo(panelId) uzerinden
         HER FRAME (panel kendi begin_plot/end_plot'u icindeyken PanelManager
         tarafindan onbellege yazilmis halde) okunur.
      2) DPG'deki ensure_handlers() ile AYNI event seti (hover, wheel zoom,
         sol/orta/sag surukleme, sag/orta/sol tik, cift tik, key press/
         release) - SADECE event URETIYOR (_emit/_emitKeyEvent -> _lastEvent +
         _eventLog kuyrugu). "event'i yakalayip bagli diger kontrollere
         uygulama" kurgusu DPG surumunde de HENUZ yazilmamisti - burada da
         bilerek NO-OP (orijinaldeki ile ayni kapsam).
      3) Event YAKALAMA (_emit, list.append) ile BASMA (onTick/flushEventLog,
         konsola I/O) BILINCLI ayrildi - ayni sebep: ardisik wheel/click
         event'lerinde her event'te hemen print yapmak frame'i yavaslatir.
         hover event'i AYRICA hic _eventLog'a eklenmez (log=False).

    Event'in SAHIBI (hangi panel) kendi hit-test'imizle DEGIL, PanelManager'in
    zaten var olan 'aktif panel' takibinden (bkz. setPanelManager,
    panelManager.getActivePanelId) okunur - DPG surumuyle AYNI kaynak.

    PanelManager KONTRATI (yeni, implot portu icin): her panel kendi
    begin_plot(...)/end_plot() blogu icindeyken, panel.id -> {
        "hovered": bool, "mouseX": float|None, "mouseY": float|None,
        "rectMin": (x,y), "rectMax": (x,y),
        "xAxisLimits": (min,max)|None, "yAxisLimits": (min,max)|None,
    } seklinde bir onbellege yazmali (getPanelHoverInfo(panelId) ile okunur) -
    implot.get_plot_mouse_pos()/get_item_rect_min()/get_item_rect_max() gibi
    cagrilar SADECE o plot'un begin/end'i arasinda gecerli oldugu icin,
    onTick() TUM panellerin cizimi bittikten SONRA calisan bu sinif
    dogrudan implot'a soramaz - PanelManager'in o anki (ayni frame'de
    onceden alinmis) onbellegine bakar."""

    X_AXIS_BAND = 40.0  # DPG surumuyle ayni: alt serit (X ekseni etiketleri)
    Y_AXIS_BAND = 55.0  # sol serit (Y ekseni etiketleri)

    # onTick()'te taranan klavye tuslari - DPG surumu "key verilmeden" HER
    # tusu dinliyordu (appData=tus kodu); imgui_bundle'da boyle bir "joker"
    # dinleyici yok, is_key_pressed/is_key_released tek tek tus sorgular.
    # Bu alt-ozellik orijinalde de sadece event URETIYORDU (Shift/Space'e
    # ozel bir "uygulama" hic baglanmamisti, bkz. sinif docstring'i) - o
    # yuzden pratikte onemli tuslarin kucuk/sabit bir listesiyle tarama,
    # orijinalin kapsamini (loglama) kaybetmeden yeterli.
    _POLLED_KEYS = (
        imgui.Key.left_shift, imgui.Key.right_shift,
        imgui.Key.left_ctrl, imgui.Key.right_ctrl,
        imgui.Key.space, imgui.Key.escape, imgui.Key.enter,
        imgui.Key.left_arrow, imgui.Key.right_arrow,
        imgui.Key.up_arrow, imgui.Key.down_arrow,
        imgui.Key.home, imgui.Key.end,
    )

    def __init__(self):
        self._panels = set()  # panelId uyelik kumesi (bkz. sinif docstring'i)
        self._panelManager = None  # bkz. setPanelManager - event sahibi PanelManager'in aktif panelinden okunur
        self._lastEvent = None
        self._rightSelectionPanelId = None
        self._middlePanPanelId = None
        self._eventSequence = 0  # her _emit/_emitKeyEvent'te 1 artan, event'leri siraya koymak icin
        self._eventLog = []  # emit() ANINDA basilmayan, kuyruklanan event'ler (bkz. onTick/flushEventLog)
        self._syncParams = None  # Read Params (src) ile yakalanan son kaynak plot parametreleri
        self._syncMode = "params"
        self._syncVersion = 0
        self._pendingSyncTargets = {}  # panelId -> syncVersion; gorunur olunca uygulanir
        self.PRINT_FORMATTED = False  # True -> satir-sutun tablo, False -> tek satir (bkz. _printEvent)
        self._eventLoggingEnabled = False

    def setPrintFormatted(self, formatted):
        self.PRINT_FORMATTED = bool(formatted)

    def setEventLoggingEnabled(self, enabled):
        self._eventLoggingEnabled = bool(enabled)
        if not self._eventLoggingEnabled:
            self._eventLog.clear()

    def setPanelManager(self, panelManager):
        """Event'in sahibini (hangi panel) PanelManager'in ZATEN var olan
        'aktif panel' takibinden (getActivePanelId) okumak icin gerekli.
        Boylece topPanelGroupBox1'deki 'Active Panel' gostergesiyle AYNI
        kaynak kullanilir - burada AYRI bir hit-test kurulmadi."""
        self._panelManager = panelManager

    # ------------------------------------------------------------- kayit
    def registerPanel(self, panelId, plotId=None, xAxis=None, yAxis=None):
        """Bir panel/plot cifti olusturulup PanelManager'a eklendiginde cagrilir.
        plotId/xAxis/yAxis parametreleri DPG surumuyle imza uyumlulugu icin
        kabul edilir ama implot portunda SAKLANMAZ (kalici tag yok) - sadece
        panelId uyelige eklenir, bkz. sinif docstring'indeki PanelManager
        kontrati."""
        self._panels.add(panelId)

    def unregisterPanel(self, panelId):
        """Bir panel silindiginde cagrilir."""
        self._panels.discard(panelId)
        self._pendingSyncTargets.pop(panelId, None)

    def getLastEvent(self):
        return self._lastEvent

    # ------------------------------------------------------------- yasam dongusu
    def ensureHandlers(self):
        """DPG surumunde burada bir kere global `dpg.handler_registry`
        kurulup mouse/klavye callback'leri KAYIT ediliyordu. immediate-mode
        ImGui'da onceden kaydedilecek bir 'handler' kavrami yok - butun
        mouse/klavye durumu HER FRAME onTick() -> _pollMouseAndKeyboardEvents()
        icinde dogrudan sorgulanir (polling). Bu metod GuiManager'in cagri
        sirasini (DPG surumuyle ayni yerde cagrilsin diye) degistirmemek
        icin duruyor - idempotent no-op."""
        pass

    def onTick(self):
        """Her frame (merkezi render dongusunden) cagrilir."""
        self._pollMouseAndKeyboardEvents()
        self.syncOthers()
        self.flushEventLog()

    def scheduleSyncOthers(self, params=None, mode="params"):
        """Kaynak plot parametrelerini diger panellere uygulamak icin kuyruga alir.

        O anda GUI'de gorunur olmayan hedefler pending kalir; syncOthers()
        her frame tekrar deneyip gorunur olduklari anda uygular.
        """
        if self._panelManager is None:
            return 0
        params = params or self._panelManager.getLastReadPlotParams()
        if not params:
            return 0

        sourcePanelId = params.get("panelId")
        self._syncVersion += 1
        self._syncParams = dict(params)
        self._syncMode = mode
        self._pendingSyncTargets.clear()
        for panel in self._panelManager.iterateAllPanels():
            if panel.id == sourcePanelId or not panel.getVisible():
                continue
            self._pendingSyncTargets[panel.id] = self._syncVersion
        self.syncOthers()
        return len(self._pendingSyncTargets)

    def syncOthers(self):
        """Pending hedeflere son kaynak parametrelerini uygular."""
        if self._panelManager is None or not self._syncParams or not self._pendingSyncTargets:
            return
        for panelId, version in list(self._pendingSyncTargets.items()):
            panel = self._panelManager.getPanel(panelId)
            if panel is None or version != self._syncVersion:
                self._pendingSyncTargets.pop(panelId, None)
                continue
            if not self._panelManager.isPanelReadyForSourceParams(panelId):
                continue
            if self._applyPendingSyncToPanel(panelId):
                self._pendingSyncTargets.pop(panelId, None)

    def getPendingSyncCount(self):
        return len(self._pendingSyncTargets)

    def _applyPendingSyncToPanel(self, panelId):
        if self._syncMode == "x":
            return self._panelManager.applySourceXAxisToPanel(panelId, self._syncParams)
        return self._panelManager.applySourceParamsToPanel(panelId, self._syncParams)

    def flushEventLog(self):
        """_eventLog'da BIRIKEN (emit aninda basilmayan) event'leri simdi
        basar ve kuyrugu bosaltir. Event YAKALAMA (_emit/_emitKeyEvent,
        sadece list.append) ile BASMA (burada, konsola I/O) BILINCLI olarak
        ayrildi - ayni sebep DPG surumuyle: hizli ardisik wheel/click
        event'lerinde her event'te hemen print yapmak frame'i yavaslatir."""
        if not self._eventLog:
            return
        if not self._eventLoggingEnabled:
            self._eventLog.clear()
            return
        for event in self._eventLog:
            fields = self.KEY_EVENT_PRINT_FIELDS if "keyCode" in event else self.EVENT_PRINT_FIELDS
            self._printEvent(event, fields, formatted=self.PRINT_FORMATTED)
        self._eventLog.clear()

    # ------------------------------------------------------------- polling (DPG handler'larinin karsiligi)
    def _pollMouseAndKeyboardEvents(self):
        """DPG'nin ensureHandlers() ile KAYIT ettigi tum mouse/klavye
        handler'larinin ayni event'leri URETEN polling karsiligi - her frame
        onTick() tarafindan cagrilir. Buton indeksleri Dear ImGui'da da
        DPG'yle AYNI kurala uyar: 0=left, 1=right, 2=middle."""
        panelInfo = self._getPanelUnderMouse()

        # hover (log=False - DPG'deki _onMouseMove karsiligi)
        self._emit("hover", panelInfo, None, None, log=False)

        # wheel zoom
        wheel = imgui.get_io().mouse_wheel
        if wheel != 0.0 and panelInfo is not None:
            scope = self._getZoomScope(panelInfo)
            direction = "in" if wheel > 0 else "out"
            self._emit(f"zoom_{scope}_{direction}", panelInfo, None, wheel)

        # sol surukleme -> pan
        if imgui.is_mouse_dragging(0):
            self._emit(self._panActionFromDelta("pan"), panelInfo, None, None)

        # orta surukleme -> middle_pan (surukleme BASLARKEN hangi panelin
        # ustunde oldugumuz sabitlenir, DPG'deki _middlePanPanelId ile ayni)
        if imgui.is_mouse_dragging(2):
            middlePanelInfo = self._getRegisteredPanelInfo(self._middlePanPanelId) or panelInfo
            self._emit(self._panActionFromDelta("middle_pan"), middlePanelInfo, None, None)

        # sag surukleme -> box_zoom_drag
        if imgui.is_mouse_dragging(1):
            self._emit("box_zoom_drag", panelInfo, None, None)

        # sag tus bas/birak -> right_press/right_release + box_selection
        if imgui.is_mouse_clicked(1):
            self._rightSelectionPanelId = panelInfo["panelId"] if panelInfo else None
            self._emit("right_press", panelInfo, None, None)
        if imgui.is_mouse_released(1):
            releasePanelInfo = self._getRegisteredPanelInfo(self._rightSelectionPanelId) or panelInfo
            self._emit("right_release", releasePanelInfo, None, None)
            if self._rightSelectionPanelId is not None:
                self._emit("box_selection", releasePanelInfo, None, None)
            self._rightSelectionPanelId = None

        # orta tus bas/birak
        if imgui.is_mouse_clicked(2):
            self._middlePanPanelId = panelInfo["panelId"] if panelInfo else None
            self._emit("middle_button_down", panelInfo, None, None)
        if imgui.is_mouse_released(2):
            middlePanelInfo = self._getRegisteredPanelInfo(self._middlePanPanelId)
            self._emit("middle_button_release", middlePanelInfo, None, None)
            self._middlePanPanelId = None

        # tiklamalar
        if imgui.is_mouse_clicked(2):
            self._emit("middle_click", panelInfo, None, None)
        if imgui.is_mouse_clicked(1):
            self._emit("right_click", panelInfo, None, None)
        if imgui.is_mouse_clicked(0):
            self._emit("click", panelInfo, None, None)
        if imgui.is_mouse_double_clicked(0):
            self._emit("double_click", panelInfo, None, None)

        # klavye - bkz. _POLLED_KEYS ve sinif docstring'indeki not
        for key in self._POLLED_KEYS:
            if imgui.is_key_pressed(key, repeat=False):
                self._emitKeyEvent("key_press", int(key))
            if imgui.is_key_released(key):
                self._emitKeyEvent("key_release", int(key))

    # ------------------------------------------------------------- event uretimi
    def _emit(self, action, panelInfo, sender, appData, log=True):
        if panelInfo is None:
            return
        x, y = panelInfo.get("mouseX"), panelInfo.get("mouseY")
        event = {
            "eventId": self._nextEventId(),
            "action": action,
            "panelId": panelInfo["panelId"],
            "plotId": panelInfo.get("plotId"),
            "xAxis": panelInfo.get("xAxis"),
            "yAxis": panelInfo.get("yAxis"),
            "region": self._getHoveredRegion(panelInfo),
            "sender": sender,
            "appData": appData,
            "frame": imgui.get_frame_count(),
            "x": x,
            "y": y,
            "xBar": None if x is None else int(round(x)),
            "yBar": None if y is None else int(round(y)),
            "xAxisLimits": panelInfo.get("xAxisLimits"),
            "yAxisLimits": panelInfo.get("yAxisLimits"),
            "screenRect": (panelInfo.get("rectMin"), panelInfo.get("rectMax")),
        }
        self._lastEvent = event
        if log and self._eventLoggingEnabled:
            self._eventLog.append(event)

    def _emitKeyEvent(self, action, keyCode):
        """Mouse event'lerinden farkli: bir panele/plota BAGLI DEGIL (klavye
        etkilesimi mouse'un hangi panelde oldugundan bagimsizdir), bu yuzden
        panelInfo/x/y/eksen alanlari yok - sadece tus kodu + frame."""
        event = {
            "eventId": self._nextEventId(),
            "action": action,
            "keyCode": keyCode,
            "frame": imgui.get_frame_count(),
        }
        self._lastEvent = event
        if self._eventLoggingEnabled:
            self._eventLog.append(event)

    # Basilirken hangi alan hangi sirada gorunecek (satir-sutun hizali tablo).
    EVENT_PRINT_FIELDS = (
        "eventId", "action", "panelId", "plotId", "region", "frame",
        "x", "y", "xBar", "yBar",
        "xAxisLimits", "yAxisLimits", "screenRect",
    )
    KEY_EVENT_PRINT_FIELDS = ("eventId", "action", "keyCode", "frame")

    def _nextEventId(self):
        self._eventSequence += 1
        return self._eventSequence

    def _printEvent(self, event, fields, formatted=True):
        """formatted=True -> her alan kendi satirinda, hizali satir-sutun
        tablo. formatted=False -> tek satirda, kisa ozet."""
        if formatted:
            label = "InteractionManager event"
            print(f"[{label}] " + "-" * (40 - len(label)))
            for field in fields:
                print(f"  {field:<12}: {event.get(field)}")
        else:
            summary = ", ".join(f"{field}={event.get(field)}" for field in fields)
            print(f"[InteractionManager] {summary}")

    def _getRegisteredPanelInfo(self, panelId):
        if panelId is None or panelId not in self._panels:
            return None
        return self._fetchPanelHoverInfo(panelId)

    def _fetchPanelHoverInfo(self, panelId):
        """PanelManager.getPanelHoverInfo(panelId) sonucuna panelId/plotId/
        xAxis/yAxis alanlarini ekleyip DPG surumundeki panelInfo sozlugu
        (panelId/plotId/xAxis/yAxis) ile ayni sekle getirir - bkz. sinif
        docstring'indeki PanelManager kontrati."""
        if self._panelManager is None:
            return None
        info = self._panelManager.getPanelHoverInfo(panelId)
        if info is None:
            return None
        merged = dict(info)
        merged.setdefault("panelId", panelId)
        merged.setdefault("plotId", f"plot_{panelId}")
        merged.setdefault("xAxis", f"x_axis_{panelId}")
        merged.setdefault("yAxis", f"y_axis_{panelId}")
        return merged

    def _panActionFromDelta(self, prefix):
        deltaX, deltaY = self._getMouseDragDelta()
        if abs(deltaX) >= abs(deltaY):
            direction = "right" if deltaX > 0 else "left"
        else:
            direction = "bottom" if deltaY > 0 else "up"
        return f"{prefix}_{direction}"

    def _getMouseDragDelta(self):
        """DPG'nin dpg.get_mouse_drag_delta() (SURUKLEME BASLANGICINA gore
        TOPLAM delta) karsiligi - Dear ImGui'da per-frame delta
        (io.mouse_delta) kullanmak yon tespiti icin (sadece isaret onemli,
        DPG surumunde de sadece yon kullaniliyordu) esdeger ve daha basit."""
        delta = imgui.get_io().mouse_delta
        return float(delta.x), float(delta.y)

    def _getZoomScope(self, panelInfo):
        return {"xAxis": "x", "yAxis": "y", "plot": "xy"}[self._getRectRegion(panelInfo)]

    def _getRectRegion(self, panelInfo):
        """Mouse'un panelin plot rect'inde HANGI SERITTE oldugunu (alt=xAxis,
        sol=yAxis, ortadaki govde=plot) panelInfo'nun onbelleklenmis
        rectMin/rectMax'ina ve GUNCEL mouse pozisyonuna gore bulur - DPG
        surumundeki rect-tabanli yontemle AYNI mantik, sadece rect kaynagi
        (kalici dpg tag sorgusu degil) PanelManager'in onbellegi."""
        rectMin = panelInfo.get("rectMin")
        rectMax = panelInfo.get("rectMax")
        if not rectMin or not rectMax:
            return "plot"
        minX, minY = rectMin
        maxX, maxY = rectMax
        mousePos = imgui.get_mouse_pos()
        mouseX, mouseY = mousePos.x, mousePos.y
        if mouseY >= maxY - self.X_AXIS_BAND:
            return "xAxis"
        if mouseX <= minX + self.Y_AXIS_BAND:
            return "yAxis"
        return "plot"

    def _getHoveredRegion(self, panelInfo):
        return self._getRectRegion(panelInfo)

    def _getPanelUnderMouse(self):
        """Event'in sahibi panel KIMLIGINI PanelManager'in ZATEN var olan
        'aktif panel' takibinden okur (bkz. setPanelManager/
        panelManager.getActivePanelId) - DPG surumuyle AYNI kaynak. AMA
        panelManager.getActivePanelId() artik (hicbir hover/click olmasa
        bile) ILK paneli varsayilan dondurebiliyor - bu yuzden SADECE
        panelId almak yetmez: PanelManager'in O FRAME icin onbelleklemis
        'hovered' bayragiyla dogrulanir (DPG surumundeki
        dpg.is_item_hovered(plotId) kontrolunun karsiligi)."""
        if self._panelManager is None:
            return None
        panelId = self._panelManager.getActivePanelId()
        if panelId is None:
            return None
        info = self._fetchPanelHoverInfo(panelId)
        if info is None or not info.get("hovered"):
            return None
        return info
