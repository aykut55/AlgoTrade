import os
import traceback

from imgui_bundle import imgui, portable_file_dialogs as pfd


class NullPool:
    """Fallback pool used only when ScriptRunner is instantiated standalone."""

    def clear(self):
        pass

    def addItem(self, *args, **kwargs):
        pass


class ScriptRunner:
    """DearPyGuiDataPlotter'daki ScriptPanel'in (src/DearPyGuiDataPlotter/src/
    plotting/scriptPanel.py, 420 satir) imgui_bundle PORTU - hem DPG orijinalinin
    REPL editor penceresini (Run/Open/Reopen/Save/Save As/Clear/Copy/New Script
    Window) hem de onceden (bu oturumun ilk turunda) eklenmis UI'siz cekirdek
    exec kapasitesini (runScriptFile/_scripts_dir) AYNI SINIFTA birlestirir -
    RuntimeCommandManager.py (verbatim port) `gm.scriptPanel.runScriptFile(...)`
    cagirdigi ve scripts/default.py `gm.scriptPanel._scripts_dir`'i okudugu
    icin BU IKI isim/davranis DEGISTIRILMEDI (bkz. __init__/setGlobals/
    runScriptFile/_scripts_dir asagida, DPG orijinaliyle AYNI).

    DPG -> imgui_bundle ceviri notlari:
    - DPG'nin kalici `dpg.add_input_text(multiline=True)` widget'i (dpg.get_value/
      set_value ile okunup yazilan) -> `imgui.input_text_multiline(label, str,
      size, flags)` HER FRAME cagrilir, (changed, newStr) dondurur - metin artik
      bir Python str attribute'unda (self._editorText) tutuluyor, widget'in
      kendisi DEGIL (immediate-mode, retained item yok).
    - DPG'nin `dpg.file_dialog` (senkron callback) -> `portable_file_dialogs.
      open_file`/`save_file` (native, ASENKRON - bir nesne dondurur, her frame
      `.ready(0)` ile non-blocking poll edilir, hazir olunca `.result()` okunur).
    - "External Script Window" coklu-pencere destegi (DPG orijinalinde N tag'li
      dpg.window) -> dict-of-state (self._externalWindows), her biri kendi
      imgui.begin(f"External Script Window {id}", ...) cagrisiyla cizilir.
    - DPG'nin `dpg.set_clipboard_text` -> `imgui.set_clipboard_text` (ayni isim).
    """

    FALLBACK_CODE = "# Hazir: gm, pm, pool, tsr, Panel, PanelData\n"

    def __init__(self, scriptsDir):
        self._scripts_dir = scriptsDir  # scripts/default.py bu AD ile okuyor (getattr(gm.scriptPanel, "_scripts_dir", ...)) - DEGISTIRME
        self._namespace = {}

        # --- REPL UI durumu ---
        self._visible = True
        self._currentPath = os.path.join(scriptsDir, "default.py")
        self._editorText = self._readFile(self._currentPath)
        self._statusText = ""
        self._onRunComplete = None
        self._pendingOpenDialog = None
        self._pendingSaveAsDialog = None
        self._externalWindows = {}  # {windowId: {"path","text","visible","status","openDialog","saveAsDialog"}}
        self._nextExternalWindowId = 1
        self._pendingGeometry = None
        self._dockWindow = None

    # ------------------------------------------------- mevcut (DEGISTIRILMEDI)
    def setGlobals(self, **kwargs):
        self._namespace.update(kwargs)

    # DearPyGui ScriptPanel API compatibility. Existing scripts can keep the
    # same calls while this backend renders them with immediate-mode ImGui.
    def set_globals(self, **kwargs): self.setGlobals(**kwargs)
    @property
    def namespace(self): return self._namespace
    def setDockWindow(self, dockWindow): self._dockWindow = dockWindow
    def isVisible(self): return self._dockWindow.is_visible if self._dockWindow else self._visible
    def is_visible(self): return self._visible
    def show(self):
        if self._dockWindow: self._dockWindow.is_visible = True
        else: self._visible = True
    def hide(self):
        if self._dockWindow: self._dockWindow.is_visible = False
        else: self._visible = False
    def toggle(self):
        if self._dockWindow: self._dockWindow.is_visible = not self._dockWindow.is_visible
        else: self._visible = not self._visible
    def set_on_run_complete(self, callback): self.setOnRunComplete(callback)
    def set_on_open_console(self, callback): self._onOpenConsole = callback
    def set_geometry(self, x, y, width, height): self.setGeometry(x, y, width, height)
    def setGeometry(self, x, y, width, height): self._pendingGeometry = (x, y, width, height)
    def build(self, x=100, y=100, width=520, height=550, onClose=None):
        self.setGeometry(x, y, width, height); self._onClose = onClose

    def runScriptFile(self, fileName):
        path = os.path.join(self._scripts_dir, fileName)
        with open(path, "r", encoding="utf-8") as f:
            source = f.read()
        exec(compile(source, fileName, "exec"), self._namespace)
        if self._onRunComplete:
            try:
                self._onRunComplete()
            except Exception:
                print(traceback.format_exc())

    # --------------------------------------------------------------- görünürlük
    def setVisible(self, visible: bool):
        if self._dockWindow: self._dockWindow.is_visible = bool(visible)
        else: self._visible = bool(visible)

    def getVisible(self) -> bool:
        return self._dockWindow.is_visible if self._dockWindow else self._visible

    def renderDockContent(self):
        self._renderMainWindowContents()
        self._pollMainDialogs()
        self._renderExternalWindows()

    def setOnRunComplete(self, callback):
        """GuiManager, Run tusuna basildiktan/runScriptFile'dan SONRA cagrilacak
        bir geri-bildirim baglayabilir (orn. 'Active Panel' combo'sunu tazelemek) -
        DPG orijinalindeki set_on_run_complete ile ayni fikir."""
        self._onRunComplete = callback

    # ------------------------------------------------------------- dosya yardimcilari
    def _readFile(self, path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                return f.read()
        except FileNotFoundError:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "w", encoding="utf-8") as f:
                f.write(self.FALLBACK_CODE)
            return self.FALLBACK_CODE
        except UnicodeDecodeError:
            # Harici bir editor Windows Turkce (cp1254) ANSI ile kaydetmis olabilir.
            with open(path, "r", encoding="cp1254", errors="replace") as f:
                return f.read()

    def _saveTo(self, path):
        try:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "w", encoding="utf-8") as f:
                f.write(self._editorText)
            self._currentPath = path
            self._statusText = f"Saved: {os.path.basename(path)}"
            print(f"Kaydedildi: {path}")
        except Exception:
            print(traceback.format_exc())

    # ------------------------------------------------------------------ calistirma
    def runEditorText(self):
        """'Run' tusu: editordeki GUNCEL metni (dosyaya KAYDETMEDEN) KALICI
        namespace'e (runScriptFile ile AYNI self._namespace) karsi exec eder -
        boylece halihazirda calisan uygulamaya (gm/pm/pool/tsr) karsi hizlica
        ad-hoc debug kodu yazip calistirmak mumkun (DPG orijinalindeki _run ile
        ayni davranis/hata yonetimi: hata uygulamayi CRASH ETMEZ, traceback
        stdout'a (ConsolePanel varsa oradan da gorulur) yazilir)."""
        try:
            exec(compile(self._editorText, self._currentPath, "exec"), self._namespace)
            self._statusText = "Run completed"
        except Exception:
            print(traceback.format_exc())
            self._statusText = "Run error, see console"
        if self._onRunComplete:
            try:
                self._onRunComplete()
            except Exception:
                print(traceback.format_exc())

    # ------------------------------------------------------------------- render
    def render(self):
        """GuiManager'in HER FRAME cagirmasi beklenir. Gizliyse hicbir sey
        cizmez (immediate-mode - DPG'nin show=False'unun karsiligi, pencere
        HIC acilmaz)."""
        if self._dockWindow is not None:
            return
        if self._visible:
            if self._pendingGeometry is not None:
                x, y, w, h = self._pendingGeometry
                imgui.set_next_window_pos((float(x), float(y)), imgui.Cond_.once.value)
                imgui.set_next_window_size((float(w), float(h)), imgui.Cond_.once.value)
                self._pendingGeometry = None
            expanded, self._visible = imgui.begin("Script Panel", self._visible)
            if expanded:
                self._renderMainWindowContents()
            imgui.end()
        self._pollMainDialogs()
        self._renderExternalWindows()

    def _renderMainWindowContents(self):
        imgui.text(f"File: {os.path.basename(self._currentPath)}")
        if imgui.button("Run"):
            self.runEditorText()
        imgui.same_line()
        if imgui.button("Open"):
            self._pendingOpenDialog = pfd.open_file("Open Script", self._scripts_dir, ["*.py", "*.*"])
        imgui.same_line()
        if imgui.button("Reopen"):
            self._editorText = self._readFile(self._currentPath)
            self._statusText = f"Yeniden yuklendi: {self._currentPath}"
        imgui.same_line()
        if imgui.button("Save"):
            self._saveTo(self._currentPath)
        imgui.same_line()
        if imgui.button("Save As"):
            self._pendingSaveAsDialog = pfd.save_file("Save Script As", self._currentPath, ["*.py", "*.*"])
        imgui.same_line()
        if imgui.button("Clear"):
            # Sadece EDITOR metnini temizler (Save/Save As hala AYNI dosyayi
            # hedef alir) - DPG orijinalindeki _new ile ayni fikir.
            self._editorText = ""
        imgui.same_line()
        if imgui.button("Copy"):
            imgui.set_clipboard_text(self._editorText)
            self._statusText = "Copied"
        imgui.same_line()
        if imgui.button("New Script Window"):
            self._openExternalScriptWindow()
        if self._statusText:
            imgui.text(self._statusText)
        _, self._editorText = imgui.input_text_multiline(
            "##scriptCode", self._editorText, (-1, -1),
            imgui.InputTextFlags_.allow_tab_input.value)

    def _pollMainDialogs(self):
        if self._pendingOpenDialog is not None and self._pendingOpenDialog.ready(0):
            result = self._pendingOpenDialog.result()
            self._pendingOpenDialog = None
            if result:
                self._currentPath = result[0]
                self._editorText = self._readFile(self._currentPath)
        if self._pendingSaveAsDialog is not None and self._pendingSaveAsDialog.ready(0):
            path = self._pendingSaveAsDialog.result()
            self._pendingSaveAsDialog = None
            if path:
                if not os.path.splitext(path)[1]:
                    path += ".py"
                self._saveTo(path)

    # --------------------------------------------- External Script Window'lar
    def _openExternalScriptWindow(self):
        """DPG orijinalindeki _open_external_script_window ile ayni fikir:
        scripts/external_window.py'i yukleyen, BAGIMSIZ (ama AYNI kalici
        namespace'i paylasan) yeni bir floating editor penceresi acar - N
        tane es zamanli acilabilir (DPG orijinaliyle AYNI, 'tek ek slot'a
        indirilmedi)."""
        windowId = self._nextExternalWindowId
        self._nextExternalWindowId += 1
        path = os.path.join(self._scripts_dir, "external_window.py")
        self._externalWindows[windowId] = {
            "path": path,
            "text": self._readFile(path),
            "visible": True,
            "status": "Waiting...",
            "openDialog": None,
            "saveAsDialog": None,
        }

    def _renderExternalWindows(self):
        closedIds = []
        for windowId, state in list(self._externalWindows.items()):
            if not state["visible"]:
                closedIds.append(windowId)
                continue
            expanded, state["visible"] = imgui.begin(
                f"External Script Window {windowId}", state["visible"])
            if expanded:
                self._renderExternalWindowContents(windowId, state)
            imgui.end()
            self._pollExternalDialogs(windowId, state)
            if not state["visible"]:
                closedIds.append(windowId)
        for windowId in closedIds:
            self._externalWindows.pop(windowId, None)

    def _renderExternalWindowContents(self, windowId, state):
        imgui.text(f"File: {os.path.basename(state['path'])}")
        if imgui.button("Run"):
            self._runExternalWindow(windowId)
        imgui.same_line()
        if imgui.button("Open"):
            state["openDialog"] = pfd.open_file("Open Script", self._scripts_dir, ["*.py", "*.*"])
        imgui.same_line()
        if imgui.button("Reopen"):
            state["text"] = self._readFile(state["path"])
            state["status"] = f"Yeniden yuklendi: {os.path.basename(state['path'])}"
        imgui.same_line()
        if imgui.button("Save"):
            self._saveExternalWindow(windowId)
        imgui.same_line()
        if imgui.button("Save As"):
            state["saveAsDialog"] = pfd.save_file("Save Script As", state["path"], ["*.py", "*.*"])
        imgui.same_line()
        if imgui.button("Clear"):
            state["text"] = ""
        imgui.same_line()
        if imgui.button("Copy"):
            imgui.set_clipboard_text(state["text"])
            state["status"] = "Copied"
        imgui.text(state["status"])
        _, state["text"] = imgui.input_text_multiline(
            f"##externalScriptCode{windowId}", state["text"], (-1, -1),
            imgui.InputTextFlags_.allow_tab_input.value)

    def _pollExternalDialogs(self, windowId, state):
        if state["openDialog"] is not None and state["openDialog"].ready(0):
            result = state["openDialog"].result()
            state["openDialog"] = None
            if result:
                state["path"] = result[0]
                state["text"] = self._readFile(state["path"])
        if state["saveAsDialog"] is not None and state["saveAsDialog"].ready(0):
            path = state["saveAsDialog"].result()
            state["saveAsDialog"] = None
            if path:
                if not os.path.splitext(path)[1]:
                    path += ".py"
                state["path"] = path
                self._saveExternalWindow(windowId, path=path)

    def _runExternalWindow(self, windowId):
        state = self._externalWindows.get(windowId)
        if not state:
            return
        state["status"] = "Running..."
        print(f"External Script Window Run: {state['path']}")
        try:
            exec(compile(state["text"], state["path"], "exec"), self._namespace)
            result = self._namespace.get("EXTERNAL_WINDOW_RESULT")
            state["status"] = str(result or "Run completed")
        except Exception:
            print(traceback.format_exc())
            state["status"] = "Run error, see console"
        if self._onRunComplete:
            try:
                self._onRunComplete()
            except Exception:
                print(traceback.format_exc())

    def _saveExternalWindow(self, windowId, path=None):
        state = self._externalWindows.get(windowId)
        if not state:
            return
        target = path or state["path"]
        try:
            os.makedirs(os.path.dirname(target), exist_ok=True)
            with open(target, "w", encoding="utf-8") as f:
                f.write(state["text"])
            state["status"] = f"Saved: {os.path.basename(target)}"
            print(f"Kaydedildi: {target}")
        except Exception:
            print(traceback.format_exc())
