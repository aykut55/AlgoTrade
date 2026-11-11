import os
import subprocess

from imgui_bundle import imgui


TOP_LEVEL_HIDDEN = {"python"}  # runtime/IPC bundles, strateji/config degil


class LeftMenuPanel:
    def __init__(self, rootDir):
        self._rootDir = rootDir
        self._dockWindow = None
        self._selectedPath = None
        self._onFileSelected = None

    def setOnFileSelected(self, callback):
        self._onFileSelected = callback

    def setDockWindow(self, dockWindow):
        self._dockWindow = dockWindow

    def renderDockContent(self):
        imgui.text("inputs/")
        imgui.separator()
        if os.path.isdir(self._rootDir):
            self._renderDir(self._rootDir, isRoot=True)
        else:
            imgui.text_disabled(f"Bulunamadi: {self._rootDir}")

    def _renderDir(self, dirPath, isRoot=False):
        try:
            entries = sorted(os.listdir(dirPath))
        except OSError:
            return
        hidden = TOP_LEVEL_HIDDEN if isRoot else set()
        dirs = [e for e in entries
                if os.path.isdir(os.path.join(dirPath, e)) and not e.startswith(".")
                and e not in hidden]
        files = [e for e in entries if not os.path.isdir(os.path.join(dirPath, e))]
        for d in dirs:
            fullDirPath = os.path.join(dirPath, d)
            opened = imgui.tree_node(d)
            if imgui.begin_popup_context_item(f"dir_ctx_{fullDirPath}"):
                if imgui.menu_item_simple("Klasoru ac"):
                    subprocess.run(["explorer", os.path.normpath(fullDirPath)])
                imgui.end_popup()
            if opened:
                self._renderDir(fullDirPath)
                imgui.tree_pop()
        for f in files:
            fullPath = os.path.join(dirPath, f)
            if imgui.selectable(f, fullPath == self._selectedPath)[0]:
                self._selectedPath = fullPath
                if self._onFileSelected:
                    self._onFileSelected(fullPath)
            if imgui.begin_popup_context_item(f"file_ctx_{fullPath}"):
                if imgui.menu_item_simple("Iceren klasoru ac"):
                    subprocess.run(["explorer", f"/select,{os.path.normpath(fullPath)}"])
                imgui.end_popup()
