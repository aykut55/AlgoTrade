from imgui_bundle import imgui


class LeftMenuPanel:
    """Panel/data tree with context actions and pool insertion."""

    def __init__(self):
        self._visible = True
        self._dockWindow = None
        self._panelManager = None
        self._poolDataManager = None
        self._pendingGeometry = None
        self._showAddPanel = False
        self._showAddPool = False
        self._addPoolTarget = None
        self._newName = "Panel"
        self._newCaption = "Panel"
        self._newHeight = 200
        self._poolSelection = 0
        self._initializedTreeNodes = set()
        self._pendingTreeOpenStates = {}

    def setPanelManager(self, manager): self._panelManager = manager
    def setPoolDataManager(self, manager): self._poolDataManager = manager
    def setAddPoolItemCallback(self, callback): self._addPoolItem = callback
    def build(self, x=10, y=160, width=300, height=700, onClose=None):
        self._pendingGeometry = (x, y, width, height)
        self._onClose = onClose
    def refresh(self): pass
    def buildTree(self): pass
    def sync(self): pass
    def setDockWindow(self, dockWindow): self._dockWindow = dockWindow
    def isVisible(self): return self._dockWindow.is_visible if self._dockWindow else self._visible
    def toggle(self):
        if self._dockWindow: self._dockWindow.is_visible = not self._dockWindow.is_visible
        else: self._visible = not self._visible
    def show(self):
        if self._dockWindow: self._dockWindow.is_visible = True
        else: self._visible = True
    def hide(self):
        if self._dockWindow: self._dockWindow.is_visible = False
        else: self._visible = False
    def setGeometry(self, x, y, width, height): self._pendingGeometry = (x, y, width, height)

    def renderDockContent(self):
        self._renderContents()
        self._renderAddPanelDialog()
        self._renderAddPoolDialog()

    def _renderContents(self):
        if imgui.button("Add Panel..."):
            count = len(list(self._panelManager.iterateAllPanels())) if self._panelManager else 0
            self._newName = f"Panel {count + 1}"
            self._newCaption = self._newName
            self._showAddPanel = True
        imgui.same_line()
        if imgui.button("Delete All") and self._panelManager:
            self._panelManager.deleteAllPanels()
        imgui.same_line()
        if imgui.button("Expand All"):
            self._setAllTreeNodesOpen(True)
        imgui.same_line()
        if imgui.button("Collapse All"):
            self._setAllTreeNodesOpen(False)
        imgui.separator()
        self._applyTreeNodeInitialState("panels_root", True)
        if self._panelManager and imgui.tree_node("Panels"):
            for panel in list(self._panelManager.iterateAllPanels()):
                self._renderPanelNode(panel)
            imgui.tree_pop()

    def render(self):
        if self._dockWindow is not None:
            return
        if not self._visible:
            return
        if self._pendingGeometry:
            x, y, w, h = self._pendingGeometry
            imgui.set_next_window_pos((float(x), float(y)), imgui.Cond_.once.value)
            imgui.set_next_window_size((float(w), float(h)), imgui.Cond_.once.value)
            self._pendingGeometry = None
        opened, visible = imgui.begin("Left Menu", self._visible)
        if opened:
            self._renderContents()
        imgui.end()
        if visible is not None and not visible:
            self._visible = False
            if getattr(self, "_onClose", None): self._onClose()
        self._renderAddPanelDialog()
        self._renderAddPoolDialog()

    def _renderPanelNode(self, panel):
        label = f"{panel.name}##left_panel_{panel.id}"
        nodeKey = f"panel_{panel.id}"
        self._applyTreeNodeInitialState(nodeKey, True)
        expanded = imgui.tree_node(label)
        if imgui.begin_popup_context_item(f"panel_ctx_{panel.id}"):
            if imgui.menu_item_simple("Add Data From Pool..."):
                self._addPoolTarget = panel.id
                self._poolSelection = 0
                self._showAddPool = True
            if imgui.menu_item_simple("Delete Panel"):
                self._panelManager.deletePanel(panel.id)
            imgui.end_popup()
        if expanded:
            for data in list(panel.iterateAllData()):
                changed, visible = imgui.checkbox(
                    f"{data.name} ({data.id}) [{data.dataType}]##left_data_{panel.id}_{data.id}",
                    bool(data.isVisible))
                if changed:
                    data.setVisible(visible)
                if imgui.begin_popup_context_item(f"data_ctx_{panel.id}_{data.id}"):
                    if imgui.menu_item_simple("Delete"):
                        panel.deleteData(data.id)
                    if imgui.menu_item_simple("Hide" if data.isVisible else "Show"):
                        data.setVisible(not data.isVisible)
                    imgui.end_popup()
            imgui.tree_pop()

    def _setAllTreeNodesOpen(self, opened):
        self._pendingTreeOpenStates["panels_root"] = bool(opened)
        if self._panelManager:
            for panel in self._panelManager.iterateAllPanels():
                self._pendingTreeOpenStates[f"panel_{panel.id}"] = bool(opened)

    def _applyTreeNodeInitialState(self, nodeKey, defaultOpened):
        if nodeKey in self._pendingTreeOpenStates:
            imgui.set_next_item_open(
                self._pendingTreeOpenStates.pop(nodeKey), imgui.Cond_.always.value)
            self._initializedTreeNodes.add(nodeKey)
        elif nodeKey not in self._initializedTreeNodes:
            imgui.set_next_item_open(bool(defaultOpened), imgui.Cond_.always.value)
            self._initializedTreeNodes.add(nodeKey)

    def _renderAddPanelDialog(self):
        if not self._showAddPanel: return
        imgui.open_popup("Add Panel")
        opened, _ = imgui.begin_popup_modal("Add Panel", True)
        if opened:
            _, self._newName = imgui.input_text("Name", self._newName)
            _, self._newCaption = imgui.input_text("Caption", self._newCaption)
            _, self._newHeight = imgui.input_int("Height", self._newHeight)
            if imgui.button("Add") and self._panelManager:
                panel = self._panelManager.createPanel(self._newName.strip() or "Panel", self._newCaption.strip())
                panel.setHeight(max(60, self._newHeight))
                self._panelManager.addPanel(panel)
                self._showAddPanel = False
                imgui.close_current_popup()
            imgui.same_line()
            if imgui.button("Cancel"):
                self._showAddPanel = False
                imgui.close_current_popup()
            imgui.end_popup()

    def _renderAddPoolDialog(self):
        if not self._showAddPool: return
        items = list(self._poolDataManager.iterateAllItems()) if self._poolDataManager else []
        labels = [f"{i.symbol or '-'} / {i.group} / {i.label}" for i in items]
        imgui.open_popup("Add From Pool")
        opened, _ = imgui.begin_popup_modal("Add From Pool", True)
        if opened:
            if labels:
                _, self._poolSelection = imgui.list_box("##poolItems", self._poolSelection, labels, 12)
            else:
                imgui.text_disabled("Pool is empty")
            if imgui.button("Add") and items and self._panelManager:
                self._panelManager.addPoolItemToPanel(self._addPoolTarget, items[self._poolSelection])
                self._showAddPool = False
                imgui.close_current_popup()
            imgui.same_line()
            if imgui.button("Cancel"):
                self._showAddPool = False
                imgui.close_current_popup()
            imgui.end_popup()
