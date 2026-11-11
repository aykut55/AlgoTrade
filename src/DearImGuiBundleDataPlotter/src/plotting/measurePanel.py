from imgui_bundle import imgui


class MeasurePanel:
    """Sag dock grubundaki (Script/Data Manager/Pool...) 'Measure' sekmesi.

    Olcumun durumu ve hesabi RangeMeasureTool'dadir; bu panel sadece
    kontrolleri ve sonuclari gosterir. Measure modu acildiginda panel
    kendiliginden gorunur olur ve one gelir."""

    def __init__(self, measureTool):
        self._tool = measureTool
        self._visible = True  # varsayilan gorunur (config'te measurePanel.visible ile kapatilabilir)
        self._dockWindow = None
        self._tool.addEnabledListener(self.bringToFront)

    def setDockWindow(self, dockWindow):
        self._dockWindow = dockWindow

    def getVisible(self):
        return self._dockWindow.is_visible if self._dockWindow else self._visible

    def setVisible(self, visible):
        if self._dockWindow:
            self._dockWindow.is_visible = bool(visible)
        else:
            self._visible = bool(visible)

    def toggle(self):
        self.setVisible(not self.getVisible())

    def show(self):
        self.setVisible(True)

    def hide(self):
        self.setVisible(False)

    def bringToFront(self):
        self.setVisible(True)
        if self._dockWindow is not None:
            self._dockWindow.focus_window_at_next_frame = True

    def renderDockContent(self):
        self._renderContents()

    def _renderContents(self):
        imgui.text_disabled("M: measure on/off   Esc: stop   click: A, then B")
        imgui.separator()
        self._tool.renderMeasurementSection()

    def render(self):
        if self._dockWindow is not None or not self._visible:
            return
        expanded, self._visible = imgui.begin("Measure", self._visible)
        if expanded:
            self._renderContents()
        imgui.end()
