from imgui_bundle import imgui


class PanelManagerWindow:
    """Functional panel and series manager for the ImGui backend."""

    def __init__(self):
        self._visible = False
        self._panelManager = None
        self._pendingGeometry = None
        self._panelIndex = 0
        self._dataIndex = 0
        self._viewMode = 2
        self._viewN = 1000
        self._viewN2 = 2000
        self._panMode = 0
        self._panStep = 100
        self._newName = "Panel"
        self._newCaption = "Panel"
        self._newHeight = 300
        self._dockWindow = None

    def setPanelManager(self, manager): self._panelManager = manager
    def build(self, x=550, y=100, width=700, height=900, onClose=None):
        self._pendingGeometry = (x, y, width, height); self._onClose = onClose
    def setDockWindow(self, dockWindow): self._dockWindow = dockWindow
    def isVisible(self): return self._dockWindow.is_visible if self._dockWindow else self._visible
    def getVisible(self): return self.isVisible()
    def setVisible(self, visible):
        if self._dockWindow: self._dockWindow.is_visible = bool(visible)
        else: self._visible = bool(visible)
    def toggle(self): self.setVisible(not self.isVisible())
    def show(self): self.setVisible(True)
    def hide(self): self.setVisible(False)
    def refresh(self): pass
    def sync(self): pass
    def setGeometry(self, x, y, width, height):
        self._pendingGeometry = (x, y, width, height)

    def render(self):
        if self._dockWindow is not None: return
        if not self._visible or not self._panelManager: return
        if self._pendingGeometry:
            x, y, w, h = self._pendingGeometry
            imgui.set_next_window_pos((float(x), float(y)), imgui.Cond_.once.value)
            imgui.set_next_window_size((float(w), float(h)), imgui.Cond_.once.value)
            self._pendingGeometry = None
        opened, visible = imgui.begin("Panel Manager", self._visible)
        if opened: self._renderContents()
        imgui.end()
        if visible is not None and not visible:
            self._visible = False
            if getattr(self, "_onClose", None): self._onClose()

    def renderDockContent(self):
        if self._panelManager:
            self._renderContents()

    def _renderContents(self):
        panels = list(self._panelManager.iterateAllPanels())
        labels = [f"{p.id}: {p.name}" for p in panels]
        if panels:
            self._panelIndex = min(self._panelIndex, len(panels) - 1)
            _, self._panelIndex = imgui.combo("Panel", self._panelIndex, labels)
            panel = panels[self._panelIndex]
            if imgui.button("Hide" if panel.visible else "Show"):
                panel.setVisible(not panel.visible)
            imgui.same_line()
            if imgui.button("Up"): self._panelManager.swapPanelUp(panel.id)
            imgui.same_line()
            if imgui.button("Down"): self._panelManager.swapPanelDown(panel.id)
            imgui.same_line()
            if imgui.button("Delete"): self._panelManager.deletePanel(panel.id)
            self._renderData(panel)
            self._renderView(panel.id)
        else:
            imgui.text_disabled("No panels")
        imgui.separator()
        imgui.text("Add Panel")
        _, self._newName = imgui.input_text("Name", self._newName)
        _, self._newCaption = imgui.input_text("Caption", self._newCaption)
        _, self._newHeight = imgui.input_int("Height", self._newHeight)
        if imgui.button("Add Panel"):
            panel = self._panelManager.createPanel(self._newName.strip() or "Panel", self._newCaption.strip())
            panel.setHeight(max(60, self._newHeight)); self._panelManager.addPanel(panel)
        imgui.same_line()
        if imgui.button("Delete All Panels"): self._panelManager.deleteAllPanels()

    def _renderData(self, panel):
        imgui.separator(); imgui.text("Series")
        data = list(panel.iterateAllData())
        if not data: imgui.text_disabled("No series"); return
        self._dataIndex = min(self._dataIndex, len(data) - 1)
        _, self._dataIndex = imgui.combo("Data", self._dataIndex, [f"{d.id}: {d.name}" for d in data])
        item = data[self._dataIndex]
        if imgui.button("Hide Data" if item.isVisible else "Show Data"):
            item.setVisible(not item.isVisible)
        imgui.same_line()
        if imgui.button("Data Up"): panel.swapDataUp(item.id)
        imgui.same_line()
        if imgui.button("Data Down"): panel.swapDataDown(item.id)
        imgui.same_line()
        if imgui.button("Delete Data"): panel.deleteData(item.id)

    def _renderView(self, panelId):
        modes = ["FitToScreen (Normal)", "FitToScreen (Wide)", "FitToScreen (Ultra)",
                 "Full Data", "Last N Data", "First N Data", "Range"]
        pans = ["VisibleScreenWidth", "1 Bar", "10 Bar", "100 Bar", "1000 Bar", "UserDefined"]
        imgui.separator(); imgui.text("View / Range")
        _, self._viewMode = imgui.combo("Mode", self._viewMode, modes)
        if modes[self._viewMode] in ("Last N Data", "First N Data", "Range"):
            _, self._viewN = imgui.input_int("N", self._viewN)
        if modes[self._viewMode] == "Range": _, self._viewN2 = imgui.input_int("N2", self._viewN2)
        if imgui.button("Apply View"): self._panelManager.applyViewMode(panelId, modes[self._viewMode], self._viewN, self._viewN2)
        imgui.same_line()
        if imgui.button("Fit Y"): self._panelManager.adjustYAxis(panelId)
        _, self._panMode = imgui.combo("Pan", self._panMode, pans)
        if pans[self._panMode] == "UserDefined": _, self._panStep = imgui.input_int("Step", self._panStep)
        if imgui.button("<< Left"): self._panelManager.panPanel(panelId, "left", pans[self._panMode], self._panStep)
        imgui.same_line()
        if imgui.button("Right >>"): self._panelManager.panPanel(panelId, "right", pans[self._panMode], self._panStep)
