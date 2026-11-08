from imgui_bundle import imgui


class PanelManagerWindow2:
    """DearPyGui tarzinda listeler kullanan alternatif Panel Manager arayuzu.

    Bu sinif grafik cizmez ve ikinci bir PanelManager modeli tutmaz. Butun
    islemler ``setPanelManager`` ile verilen mevcut DearImGui PanelManager
    backend'ine aktarilir.
    """

    VIEW_MODES = [
        "FitToScreen (Normal)", "FitToScreen (Wide)",
        "FitToScreen (Ultra)", "Full Data", "Last N Data",
        "First N Data", "Range",
    ]
    PAN_MODES = [
        "VisibleScreenWidth", "1 Bar", "10 Bar", "100 Bar",
        "1000 Bar", "UserDefined",
    ]

    def __init__(self):
        # Script Panel ile ayni RightDockSpace'te baslangictan itibaren bir
        # dock sekmesi olarak yer alsin. Script Panel focus istegi tasidigi
        # icin uygulama acilisinda aktif sekme yine Script Panel olur.
        self._visible = True
        self._panelManager = None
        self._dockWindow = None
        self._pendingGeometry = None
        self._onClose = None
        self._panelIndex = 0
        self._dataIndex = 0
        self._newName = "Panel"
        self._newCaption = "Panel"
        self._newHeight = 300
        self._viewMode = 2
        self._viewN = 1000
        self._viewN2 = 2000
        self._panMode = 0
        self._panStep = 100

    def setPanelManager(self, manager):
        self._panelManager = manager

    def build(self, x=550, y=100, width=760, height=900, onClose=None):
        self._pendingGeometry = (x, y, width, height)
        self._onClose = onClose

    def setDockWindow(self, dockWindow):
        self._dockWindow = dockWindow

    def isVisible(self):
        return self._dockWindow.is_visible if self._dockWindow else self._visible

    def getVisible(self):
        return self.isVisible()

    def setVisible(self, visible):
        if self._dockWindow:
            self._dockWindow.is_visible = bool(visible)
        else:
            self._visible = bool(visible)

    def toggle(self):
        self.setVisible(not self.isVisible())

    def show(self):
        self.setVisible(True)

    def hide(self):
        self.setVisible(False)

    def refresh(self):
        pass

    def sync(self):
        pass

    def render(self):
        if self._dockWindow is not None or not self._visible or not self._panelManager:
            return
        if self._pendingGeometry:
            x, y, width, height = self._pendingGeometry
            imgui.set_next_window_pos((float(x), float(y)), imgui.Cond_.once.value)
            imgui.set_next_window_size((float(width), float(height)), imgui.Cond_.once.value)
            self._pendingGeometry = None
        opened, visible = imgui.begin("Panel Manager 2", self._visible)
        if opened:
            self._renderContents()
        imgui.end()
        if visible is not None and not visible:
            self._visible = False
            if self._onClose:
                self._onClose()

    def renderDockContent(self):
        if self._panelManager:
            self._renderContents()

    def _selectedPanel(self, panels):
        if not panels:
            self._panelIndex = 0
            return None
        self._panelIndex = max(0, min(self._panelIndex, len(panels) - 1))
        return panels[self._panelIndex]

    def _renderContents(self):
        panels = list(self._panelManager.iterateAllPanels())
        panel = self._selectedPanel(panels)

        imgui.text("Panel Manager 2 - DearPyGui Layout")
        imgui.same_line()
        if imgui.button("Read Panels"):
            self._panelIndex = min(self._panelIndex, max(0, len(panels) - 1))
        imgui.separator()

        available = imgui.get_content_region_avail().x
        colWidth = max(180.0, (available - 16.0) / 3.0)
        self._renderPanelList(panels, panel, colWidth)
        imgui.same_line()
        self._renderPanelVisibility(panels, colWidth)
        imgui.same_line()
        self._renderPanelOrder(panels, panel, colWidth)

        panel = self._selectedPanel(list(self._panelManager.iterateAllPanels()))
        imgui.separator()
        self._renderData(panel)
        imgui.separator()
        self._renderGlobalActions()
        imgui.separator()
        self._renderViewAndPan(panel)

    def _renderPanelList(self, panels, panel, width):
        imgui.begin_child("pm2_panel_list", size=(width, 315),
                          child_flags=imgui.ChildFlags_.borders.value)
        imgui.text("Panels")
        labels = [f"Panel {p.id}: {p.name}" for p in panels]
        if labels:
            changed, index = imgui.list_box("##pm2_panels", self._panelIndex, labels, 8)
            if changed:
                self._panelIndex = index
                self._dataIndex = 0
        else:
            imgui.text_disabled("No panels")

        imgui.separator()
        imgui.text("Add New Panel")
        _, self._newName = imgui.input_text("Name##pm2", self._newName)
        _, self._newCaption = imgui.input_text("Caption##pm2", self._newCaption)
        _, self._newHeight = imgui.input_int("Height##pm2", self._newHeight)
        if imgui.button("Add Panel##pm2"):
            newPanel = self._panelManager.createPanel(
                self._newName.strip() or "Panel", self._newCaption.strip())
            newPanel.setHeight(max(60, self._newHeight))
            self._panelManager.addPanel(newPanel)
            self._panelIndex = len(list(self._panelManager.iterateAllPanels())) - 1
        if panel is not None:
            imgui.same_line()
            if imgui.button("Delete##pm2"):
                self._panelManager.deletePanel(panel.id)
                self._panelIndex = max(0, self._panelIndex - 1)
        imgui.end_child()

    def _renderPanelVisibility(self, panels, width):
        imgui.begin_child("pm2_panel_visibility", size=(width, 315),
                          child_flags=imgui.ChildFlags_.borders.value)
        imgui.text("Hide / Show Panel")
        imgui.separator()
        for panel in panels:
            changed, visible = imgui.checkbox(
                f"{panel.id}: {panel.name}##pm2_visible_{panel.id}", panel.getVisible())
            if changed:
                panel.setVisible(visible)
        imgui.separator()
        if imgui.button("Show All##pm2"):
            self._panelManager.showAllPanels()
        imgui.same_line()
        if imgui.button("Hide All##pm2"):
            self._panelManager.hideAllPanels()
        if imgui.button("Expand All Plots##pm2"):
            self._panelManager.expandAllPanels()
        if imgui.button("Collapse All Plots##pm2"):
            self._panelManager.collapseAllPanels()
        if imgui.button("Adjust All Y##pm2"):
            self._panelManager.adjustAllYAxes()
        imgui.end_child()

    def _renderPanelOrder(self, panels, panel, width):
        imgui.begin_child("pm2_panel_order", size=(width, 315),
                          child_flags=imgui.ChildFlags_.borders.value)
        imgui.text("Panel Order")
        labels = [f"Panel {p.id}: {p.name}" for p in panels]
        if labels:
            changed, index = imgui.list_box("##pm2_panel_order_list", self._panelIndex, labels, 8)
            if changed:
                self._panelIndex = index
                panel = panels[index]
        if panel is not None:
            if imgui.button("Up##pm2"):
                self._panelManager.swapPanelUp(panel.id)
                self._panelIndex = max(0, self._panelIndex - 1)
            imgui.same_line()
            if imgui.button("Down##pm2"):
                self._panelManager.swapPanelDown(panel.id)
                self._panelIndex = min(len(panels) - 1, self._panelIndex + 1)
        if imgui.button("Apply Order##pm2"):
            self._panelManager.applyPanelOrder()
        imgui.same_line()
        if imgui.button("Reset##pm2"):
            self._panelManager.resetPanelOrder()
            self._panelIndex = 0
        imgui.end_child()

    def _renderData(self, panel):
        imgui.text("Data Operations / Hide-Show / Order")
        if panel is None:
            imgui.text_disabled("Select a panel")
            return
        data = list(panel.iterateAllData())
        labels = [f"{item.id}: {item.name} [{item.dataType}]" for item in data]
        if not labels:
            self._dataIndex = 0
            imgui.text_disabled("No data")
            return
        self._dataIndex = min(self._dataIndex, len(data) - 1)
        changed, index = imgui.list_box("##pm2_data_list", self._dataIndex, labels, 7)
        if changed:
            self._dataIndex = index
        item = data[self._dataIndex]
        changed, visible = imgui.checkbox(
            f"Visible: {item.name}##pm2_data_visible", item.isVisible)
        if changed:
            item.setVisible(visible)
        if imgui.button("Data Up##pm2"):
            panel.swapDataUp(item.id)
            self._dataIndex = max(0, self._dataIndex - 1)
        imgui.same_line()
        if imgui.button("Data Down##pm2"):
            panel.swapDataDown(item.id)
            self._dataIndex = min(len(data) - 1, self._dataIndex + 1)
        imgui.same_line()
        if imgui.button("Delete Data##pm2"):
            panel.deleteData(item.id)
            self._dataIndex = max(0, self._dataIndex - 1)
        imgui.same_line()
        if imgui.button("Show All Data##pm2"):
            panel.showAllData()
        imgui.same_line()
        if imgui.button("Hide All Data##pm2"):
            panel.hideAllData()

    def _renderGlobalActions(self):
        imgui.text("Global Data Actions")
        if imgui.button("Show All Data (All Panels)##pm2"):
            for panel in self._panelManager.iterateAllPanels():
                panel.showAllData()
        imgui.same_line()
        if imgui.button("Hide All Data (All Panels)##pm2"):
            for panel in self._panelManager.iterateAllPanels():
                panel.hideAllData()

    def _renderViewAndPan(self, panel):
        imgui.text("View / Range Controls")
        _, self._viewMode = imgui.combo("Mode##pm2", self._viewMode, self.VIEW_MODES)
        mode = self.VIEW_MODES[self._viewMode]
        if mode in ("Last N Data", "First N Data", "Range"):
            _, self._viewN = imgui.input_int("N##pm2", self._viewN)
        if mode == "Range":
            _, self._viewN2 = imgui.input_int("N2##pm2", self._viewN2)
        if panel is not None and imgui.button("Apply View##pm2"):
            self._panelManager.applyViewMode(
                panel.id, mode, n=self._viewN, n2=self._viewN2)
        imgui.same_line()
        if panel is not None and imgui.button("Fit Y##pm2"):
            self._panelManager.adjustYAxis(panel.id)

        imgui.text("Pan Controls")
        _, self._panMode = imgui.combo("Pan Step##pm2", self._panMode, self.PAN_MODES)
        panMode = self.PAN_MODES[self._panMode]
        if panMode == "UserDefined":
            _, self._panStep = imgui.input_int("Step##pm2", self._panStep)
        if panel is not None and imgui.button("<< Left##pm2"):
            self._panelManager.panPanel(panel.id, "left", panMode, self._panStep)
        imgui.same_line()
        if panel is not None and imgui.button("Right >>##pm2"):
            self._panelManager.panPanel(panel.id, "right", panMode, self._panStep)

