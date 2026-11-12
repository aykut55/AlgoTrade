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
        self._pctChangePanel = None  # bkz. setPctChangePanel - default.py'nin "% Change" paneli

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

    def setPctChangePanel(self, panel):
        """default.py, 'pctChange' panelini kurduktan sonra bunu cagirir -
        panelin KENDISINE (construction/properties) dokunmaz, sadece acik/kapali
        yapabilmek icin bir referans tutar."""
        self._pctChangePanel = panel

    def handleKeyboard(self):
        """C: % Change panelini ac/kapa (M'nin kendi tusuyla ayni bagimsizlikte -
        fare bir plot uzerinde olsun olmasin calisir)."""
        if self._pctChangePanel is None:
            return
        io = imgui.get_io()
        if io.want_text_input or io.key_ctrl:
            return
        if imgui.is_key_pressed(imgui.Key.c, repeat=False):
            self._pctChangePanel.setVisible(not self._pctChangePanel.getVisible())

    def renderDockContent(self):
        self._renderContents()

    def _renderContents(self):
        imgui.text_disabled("M: measure on/off   Esc: stop   click: A, then B")
        imgui.separator()
        self._tool.renderMeasurementSection()
        if self._pctChangePanel is not None:
            imgui.separator()
            label = ("% Change panel : ON (C)" if self._pctChangePanel.getVisible()
                     else "% Change panel : OFF (C)")
            if imgui.button(label, (200, 0)):
                self._pctChangePanel.setVisible(not self._pctChangePanel.getVisible())

    def render(self):
        if self._dockWindow is not None or not self._visible:
            return
        expanded, self._visible = imgui.begin("Measure", self._visible)
        if expanded:
            self._renderContents()
        imgui.end()
