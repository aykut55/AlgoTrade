from imgui_bundle import imgui


class PoolPanel:
    """Bagimsiz (standalone) Pool penceresi - DearPyGuiDataPlotter'daki
    plotting/poolPanel.py'nin imgui_bundle PORTU. DataManager/ScriptPanel gibi
    kullanici acip kapatana kadar ekranda kalir, layout'taki hicbir slotu
    isgal etmez (centerPanel bundan etkilenmez).

    PoolDataManager'dan besleniyor: Symbols > Sembol > Grup ("Data" /
    "Indicators" / "Levels") > item.

    DPG versiyonu retained bir agac kuruyordu (dpg.tree_node/add_text/
    drag_payload item'lari BIR KERE olusturup refresh() cagrilana kadar
    sakliyordu) - burada immediate-mode oldugu icin agac HER FRAME render()
    icinde, refresh()'in onbelleklerdigi (TEKRAR TARAMAYAN, hafif) {symbol:
    {group: [PoolItem,...]}} yapisindan yeniden cizilir. Drag-drop: DPG'nin
    `dpg.drag_payload(payload_type="pool_item", drag_data=item.id)` (STRING
    id tasiyabiliyordu) yerine imgui_bundle'in Python-dostu
    `set_drag_drop_payload_py_id`/`accept_drag_drop_payload_py_id` kullanildi
    - bunlar SADECE bir int ("PayloadId") tasiyabiliyor, STRING degil - bu
    yuzden refresh() sirasinda her PoolItem.id (string, "pool_N") icin kucuk
    bir int ("dragId") uretilip iki yonlu bir sozluk (_dragIdByItemId/
    _itemByDragId) tutuluyor; drop-hedefi (PanelManager) payload'tan gelen
    int'i resolveDragData(dragId) ile gercek PoolItem'e cevirir."""

    PAYLOAD_TYPE = "pool_item"

    def __init__(self):
        self._visible = False
        self._onCloseCallback = None
        self._poolDataManager = None  # bkz. setPoolDataManager (guiManager tarafindan baglanir)
        self._pendingGeometry = None  # (x,y,w,h) - bkz. build/setGeometry, bir sonraki render()'da UYGULANIP dusurulur
        self._tree = {}  # refresh() tarafindan kurulan {symbol: {group: [PoolItem,...]}}
        self._dragIdByItemId = {}
        self._itemByDragId = {}
        self._dockWindow = None

    def setPoolDataManager(self, poolDataManager):
        self._poolDataManager = poolDataManager

    def build(self, x=0, y=0, width=420, height=600, onClose=None):
        """DPG versiyonu pencere/child_window'u BIR KERE (retained) kuruyordu -
        immediate-mode'da karsiligi: baslangic geometrisini sakla (render() ilk
        frame'de Cond_.always ile uygulayip dusurur - sonra kullanici pencereyi
        serbestce tasiyabilir/boyutlandirabilir), onClose callback'ini kaydet,
        agaci kur (refresh())."""
        self._onCloseCallback = onClose
        self._pendingGeometry = (x, y, width, height)
        self.refresh()

    def refresh(self):
        """Pool agacini (Symbols > Sembol > Grup > item) ONBELLEGE alir - gercek
        cizim HER FRAME render()'da bu onbellekten yapilir. Otomatik
        CAGRILMIYOR - script pool.addItem/clear yaptiktan sonra elle cagirmasi
        beklenir (DPG versiyonuyla AYNI sozlesme - bkz. scripts/default.py
        stage3FillPool + gm.poolPanel.refresh())."""
        tree = {}
        dragIdByItemId = {}
        itemByDragId = {}
        if self._poolDataManager is not None:
            nextDragId = 1
            for item in self._poolDataManager.iterateAllItems():
                tree.setdefault(item.symbol or "-", {}).setdefault(item.group, []).append(item)
                dragIdByItemId[item.id] = nextDragId
                itemByDragId[nextDragId] = item
                nextDragId += 1
        self._tree = tree
        self._dragIdByItemId = dragIdByItemId
        self._itemByDragId = itemByDragId

    # ------------------------------------------------------------------ render
    def renderDockContent(self):
        imgui.begin_child("pool_panel_tree_container", size=(-1, -1),
                          child_flags=imgui.ChildFlags_.borders.value)
        self._renderTree()
        imgui.end_child()

    def render(self):
        """GuiManager'in HER FRAME cagirmasi beklenir (gorunur degilse no-op)."""
        if self._dockWindow is not None:
            return
        if not self._visible:
            return
        if self._pendingGeometry is not None:
            x, y, w, h = self._pendingGeometry
            imgui.set_next_window_pos((float(x), float(y)), imgui.Cond_.always.value)
            imgui.set_next_window_size((float(w), float(h)), imgui.Cond_.always.value)
            self._pendingGeometry = None

        opened, stillVisible = imgui.begin("Pool", self._visible)
        if opened:
            self.renderDockContent()
        imgui.end()

        if stillVisible is not None and not stillVisible:
            self._visible = False
            if self._onCloseCallback:
                self._onCloseCallback()

    def _renderTree(self):
        if not imgui.tree_node("Symbols"):
            return
        for symbol, groups in self._tree.items():
            if imgui.tree_node(symbol):
                for group, items in groups.items():
                    if imgui.tree_node(group):
                        for item in items:
                            self._renderDragItem(item)
                        imgui.tree_pop()
                imgui.tree_pop()
        imgui.tree_pop()

    def _renderDragItem(self, item):
        imgui.selectable(item.label, False)
        if imgui.begin_drag_drop_source():
            dragId = self._dragIdByItemId.get(item.id)
            if dragId is not None:
                imgui.set_drag_drop_payload_py_id(self.PAYLOAD_TYPE, dragId)
            imgui.text(f"{item.symbol} / {item.group} / {item.label}")
            imgui.end_drag_drop_source()

    def resolveDragData(self, dragId):
        """PanelManager'in (veya baska bir drop-hedefinin)
        `imgui.accept_drag_drop_payload_py_id(PoolPanel.PAYLOAD_TYPE)` ile
        aldigi Payload_PyId.data_id'sini gercek PoolItem'e cevirir - drop
        hedefi `gm.poolPanel.resolveDragData(payload.data_id)` seklinde
        cagirmali. DIKKAT: id'ler HER refresh()'te YENIDEN uretilir (kalici
        DEGIL) - cozumleme ayni frame/drop anında yapilmali (tipik kullanim,
        drag suresince pool degismez)."""
        return self._itemByDragId.get(dragId)

    # ----------------------------------------------------------- görünürlük
    def setDockWindow(self, dockWindow):
        self._dockWindow = dockWindow

    def isVisible(self):
        return self._dockWindow.is_visible if self._dockWindow else self._visible

    def getVisible(self):
        return self.isVisible()

    def setVisible(self, visible):
        if self._dockWindow: self._dockWindow.is_visible = bool(visible)
        else: self._visible = bool(visible)

    def toggle(self):
        self.setVisible(not self.isVisible())

    def show(self):
        self.setVisible(True)

    def hide(self):
        self.setVisible(False)

    def setGeometry(self, x, y, width, height):
        self._pendingGeometry = (x, y, width, height)
