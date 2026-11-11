import json
import os

from imgui_bundle import imgui


class ActiveSettingsPanel:
    """AlgoTrade.Console/Program.cs showSingleTraderRunPreview() tarafindan yazilan
    SingleTraderRunPreview.json'u gosterir - parametre cozumleme mantigi sadece
    C# tarafinda, burada tekrarlanmiyor."""

    def __init__(self, previewPath):
        self._previewPath = previewPath
        self._dockWindow = None
        self._lastMtime = None
        self._data = None
        self._error = None

    def setDockWindow(self, dockWindow):
        self._dockWindow = dockWindow

    def _reload(self):
        try:
            mtime = os.path.getmtime(self._previewPath)
        except OSError:
            self._data = None
            self._error = None
            self._lastMtime = None
            return
        if mtime == self._lastMtime:
            return
        self._lastMtime = mtime
        try:
            with open(self._previewPath, "r", encoding="utf-8") as f:
                self._data = json.load(f)
            self._error = None
        except (OSError, ValueError) as e:
            self._data = None
            self._error = str(e)

    def renderDockContent(self):
        self._reload()
        imgui.text_disabled(self._previewPath)
        imgui.separator()
        if self._error:
            imgui.text_colored((1.0, 0.4, 0.4, 1.0), f"Okunamadi: {self._error}")
            return
        if self._data is None:
            imgui.text_disabled(
                "Henuz olusturulmadi. Konsolda ilgili Run Preview ekranina gir.")
            return
        for key, value in self._data.items():
            self._renderValue(key, value)

    def _renderValue(self, key, value):
        label = str(key)
        if isinstance(value, dict):
            if imgui.tree_node(label):
                for k, v in value.items():
                    self._renderValue(k, v)
                imgui.tree_pop()
        elif isinstance(value, list):
            if imgui.tree_node(f"{label} [{len(value)}]"):
                for i, v in enumerate(value):
                    self._renderValue(i, v)
                imgui.tree_pop()
        else:
            imgui.text(f"{label}: {value}")
