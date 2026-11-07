import sys
from collections import deque

from imgui_bundle import imgui


class ConsolePanel:
    """Floating window that mirrors sys.stdout/sys.stderr - imgui_bundle port of
    DearPyGuiDataPlotter's plotting/consolePanel.py. Independent of the layout
    system: opened/closed and moved/resized freely by the user, not repositioned
    automatically like the layout panels or ScriptPanel.

    DPG'nin readonly multiline input_text'i kendi basina word-wrap YAPMIYORDU, bu
    yuzden orijinal dpg.get_text_size ile elle olcup satirlari kirpiyordu. ImGui
    bunu NATIF destekliyor (push_text_wrap_pos/text_wrapped, ya da burada kullanilan
    begin_child + text_wrapped(full_buffer) deseni) - bu yuzden o elle-olcme
    workaround'u (orijinalin NIYETINI, okunakli otomatik-kaydirmali konsol metni,
    degistirmeden) tasinmadi; bu ImGui'nin DPG'de olmayan bir ozelligiyle mumkun
    olan mekanik bir basitlestirme, tasarim sapmasi degil.

    Bellek sinirsiz buyumesin diye buffer en fazla MAX_LINES satir tutar (deque)."""

    MAX_LINES = 2000

    def __init__(self):
        self._visible = False
        self._buffer = deque(maxlen=self.MAX_LINES)
        self._pending = ""  # write() ile gelen, henuz satira bolunmemis parca
        self._clearOnDblClick = False
        self._addInputValue = ""
        self._dirty = False  # yeni icerik geldi -> bir sonraki render()'da scroll-to-bottom
        self._pendingGeometry = None
        self._dockWindow = None

    def attachStdout(self):
        """sys.stdout/sys.stderr'i bu panele TEE'ler - orijinal stream'e de
        YAZMAYI SURDURUR (terminal ciktisi kaybolmaz), SADECE ayrica burada
        biriktirir. Uygulama baslangicinda BIR KERE cagrilmali."""
        sys.stdout = _ConsoleTee(sys.__stdout__, self)
        sys.stderr = _ConsoleTee(sys.__stderr__, self)

    def write(self, text):
        self._pending += text
        while "\n" in self._pending:
            line, self._pending = self._pending.split("\n", 1)
            self._buffer.append(line)
        self._dirty = True

    def clear(self):
        self._buffer.clear()
        self._pending = ""
        self._dirty = True

    def copy(self):
        imgui.set_clipboard_text(self._fullText())

    def isVisible(self):
        return self._dockWindow.is_visible if self._dockWindow else self._visible

    def getVisible(self):
        return self._dockWindow.is_visible if self._dockWindow else self._visible

    def setDockWindow(self, dockWindow):
        self._dockWindow = dockWindow

    def setVisible(self, visible):
        if self._dockWindow: self._dockWindow.is_visible = bool(visible)
        else: self._visible = bool(visible)

    def toggle(self):
        self.setVisible(not self.getVisible())

    def show(self):
        self.setVisible(True)

    def hide(self):
        self.setVisible(False)

    def build(self, x=100, y=100, width=520, height=260, onClose=None):
        self.setGeometry(x, y, width, height)
        self._onCloseCallback = onClose

    def setGeometry(self, x, y, width, height):
        self._pendingGeometry = (x, y, width, height)

    def _fullText(self):
        lines = list(self._buffer)
        if self._pending:
            lines.append(self._pending)
        return "\n".join(lines)

    # ------------------------------------------------------------------ render
    def renderDockContent(self):
        self._renderContents()

    def _renderContents(self):
        if imgui.button("Clear"):
            self.clear()
        imgui.same_line()
        if imgui.button("Copy"):
            self.copy()
        imgui.same_line()
        changed, self._clearOnDblClick = imgui.checkbox("Clear On DblClick", self._clearOnDblClick)
        imgui.same_line()
        if imgui.button("Add"):
            self.write(self._addInputValue + "\n")
            self._addInputValue = ""
        imgui.same_line()
        imgui.set_next_item_width(200)
        _, self._addInputValue = imgui.input_text("##console_add_input", self._addInputValue,
                                                   flags=imgui.InputTextFlags_.enter_returns_true.value)

        imgui.begin_child("console_output", size=(-1, -1),
                          child_flags=imgui.ChildFlags_.borders.value)
        imgui.push_text_wrap_pos(0.0)
        imgui.text_wrapped(self._fullText())
        imgui.pop_text_wrap_pos()
        if imgui.is_window_hovered() and imgui.is_mouse_double_clicked(0) and self._clearOnDblClick:
            self.clear()
        if self._dirty:
            imgui.set_scroll_here_y(1.0)
            self._dirty = False
        imgui.end_child()

    def render(self):
        if self._dockWindow is not None:
            return
        if not self._visible:
            return
        if self._pendingGeometry is not None:
            x, y, w, h = self._pendingGeometry
            imgui.set_next_window_pos((float(x), float(y)), imgui.Cond_.once.value)
            imgui.set_next_window_size((float(w), float(h)), imgui.Cond_.once.value)
            self._pendingGeometry = None
        expanded, self._visible = imgui.begin("Console Panel", self._visible)
        if expanded:
            self._renderContents()
        imgui.end()


class _ConsoleTee:
    def __init__(self, original, panel):
        self._original = original
        self._panel = panel

    def write(self, text):
        self._original.write(text)
        self._panel.write(text)

    def flush(self):
        self._original.flush()
