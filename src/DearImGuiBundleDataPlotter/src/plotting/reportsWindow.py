import json
import os
import time

from imgui_bundle import imgui


class ReportsWindow:
    """Son yuklenen bundle'a ait trader raporlarini gosteren dock penceresi."""

    MAX_TEXT_BYTES = 4 * 1024 * 1024
    WINDOW_TITLE = "Reports"

    def __init__(self, guiManager):
        self._gm = guiManager
        self._visible = False
        self._dockWindow = None
        self._lastBundlePath = None
        self._indexStamp = None
        self._lastIndexCheck = 0.0
        self._indexPath = None
        self._index = None
        self._files = []
        self._selected = 0
        self._contentPath = None
        self._content = ""
        self._status = "Bir bundle yuklendiginde raporlar burada gorunecek."

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

    # Ayni bundle yolu her kosuda yeniden yaziliyor (latest_bundle) ve rapor paketi plottan SONRA
    # olusturuluyor; bu yuzden yol degismese de indeks dosyasinin degisimi kontrol edilir.
    INDEX_CHECK_INTERVAL_SEC = 0.5

    @staticmethod
    def _indexPathFor(bundlePath):
        if not bundlePath:
            return None
        base, _ = os.path.splitext(os.path.abspath(bundlePath))
        return base + ".reports.json"

    @staticmethod
    def _fileStamp(path):
        if not path:   # henuz bundle yuklenmedi
            return None
        try:
            st = os.stat(path)
            return (st.st_mtime_ns, st.st_size)
        except OSError:
            return None

    def _syncBundle(self):
        bundlePath = getattr(self._gm, "currentBundlePath", None)
        if bundlePath != self._lastBundlePath:
            self._lastBundlePath = bundlePath
            self._reloadIndex(bundlePath)
            return

        now = time.monotonic()
        if now - self._lastIndexCheck < self.INDEX_CHECK_INTERVAL_SEC:
            return
        self._lastIndexCheck = now
        if self._fileStamp(self._indexPathFor(bundlePath)) != self._indexStamp:
            self._reloadIndex(bundlePath)

    def _reloadIndex(self, bundlePath):
        self._lastIndexCheck = time.monotonic()
        stamp = self._fileStamp(self._indexPathFor(bundlePath))
        self._loadIndex(bundlePath)
        # Indeks yazilirken okunduysa (yarim JSON) damga saklanmaz, bir sonraki kontrolde yeniden denenir.
        self._indexStamp = stamp if (self._index is not None or stamp is None) else None

    def _loadIndex(self, bundlePath):
        self._index = None
        self._files = []
        self._selected = 0
        self._contentPath = None
        self._content = ""
        self._indexPath = None
        if not bundlePath:
            self._status = "Henuz bundle yuklenmedi."
            return

        base, _ = os.path.splitext(os.path.abspath(bundlePath))
        self._indexPath = base + ".reports.json"
        if not os.path.isfile(self._indexPath):
            self._status = "Bu bundle icin rapor paketi bulunamadi."
            return
        try:
            with open(self._indexPath, "r", encoding="utf-8-sig") as stream:
                self._index = json.load(stream)
            self._files = self._index.get("Files", self._index.get("files", [])) or []
            self._status = f"{len(self._files)} rapor bulundu."
            self._loadSelected()
        except Exception as exc:
            self._status = f"Rapor indeksi okunamadi: {exc}"

    def _entryValue(self, entry, name, default=""):
        return entry.get(name, entry.get(name[:1].lower() + name[1:], default))

    def _loadSelected(self):
        if not self._files:
            self._contentPath = None
            self._content = ""
            return
        self._selected = max(0, min(self._selected, len(self._files) - 1))
        entry = self._files[self._selected]
        relative = self._entryValue(entry, "Path")
        path = os.path.normpath(os.path.join(os.path.dirname(self._indexPath), relative))
        if path == self._contentPath:
            return
        self._contentPath = path
        try:
            size = os.path.getsize(path)
            with open(path, "r", encoding="utf-8-sig", errors="replace") as stream:
                self._content = stream.read(self.MAX_TEXT_BYTES)
            if size > self.MAX_TEXT_BYTES:
                self._content += "\n\n[Dosya buyuk oldugu icin ilk 4 MB gosteriliyor.]"
        except Exception as exc:
            self._content = f"Dosya okunamadi:\n{path}\n\n{exc}"

    def renderDockContent(self):
        self._syncBundle()
        self._renderContents()

    def _renderContents(self):
        if imgui.button("Refresh"):
            self._reloadIndex(self._lastBundlePath)
        imgui.same_line()
        imgui.text(self._status)

        if self._index:
            traderType = self._index.get("TraderType", self._index.get("traderType", ""))
            generated = self._index.get("GeneratedAt", self._index.get("generatedAt", ""))
            imgui.text(f"Trader: {traderType}   Generated: {generated}")
        imgui.separator()

        if not self._files:
            imgui.text_wrapped(self._status)
            if self._indexPath:
                imgui.text_wrapped(self._indexPath)
            return

        labels = []
        for entry in self._files:
            name = self._entryValue(entry, "Name", "Report")
            kind = self._entryValue(entry, "Type")
            fmt = self._entryValue(entry, "Format")
            labels.append(f"{name} [{kind}/{fmt}]")
        changed, selected = imgui.list_box("##report_files", self._selected, labels, 10)
        if changed:
            self._selected = selected
            self._loadSelected()
        if imgui.button("Copy"):
            imgui.set_clipboard_text(self._content)
        imgui.same_line()
        imgui.text_wrapped(self._contentPath or "")
        imgui.separator()
        imgui.begin_child("reports_content", size=(-1, -1),
                          child_flags=imgui.ChildFlags_.borders.value)
        imgui.text_unformatted(self._content)
        imgui.end_child()

    def render(self):
        self._syncBundle()
        if self._dockWindow is not None or not self._visible:
            return
        expanded, self._visible = imgui.begin(self.WINDOW_TITLE, self._visible)
        if expanded:
            self._renderContents()
        imgui.end()
