import json
import os

from imgui_bundle import imgui

from src.jsonc import strip_jsonc

_PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
ENUM_FIELDS_PATH = os.path.join(_PROJECT_ROOT, "inputs", "configs", "enumFields.json")


def _loadEnumFields():
    try:
        with open(ENUM_FIELDS_PATH, "r", encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


class ConfigPanel:
    def __init__(self):
        self._dockWindow = None
        self._selectedPath = None
        self._text = ""
        self._data = None
        self._hasComments = False
        self._pipeLines = None
        self._lastMtime = None
        self._statusText = ""
        self._confirmOverwriteComments = False
        self._enumFields = _loadEnumFields()

    def setDockWindow(self, dockWindow):
        self._dockWindow = dockWindow

    def setSelectedFile(self, path):
        self._selectedPath = path
        self._load()

    def _load(self):
        path = self._selectedPath
        self._text = self._readFile(path)
        self._data = None
        self._hasComments = False
        self._pipeLines = None
        self._statusText = ""
        self._confirmOverwriteComments = False
        if path.lower().endswith(".json"):
            try:
                self._data = json.loads(self._text)
            except ValueError:
                try:
                    self._data = json.loads(strip_jsonc(self._text))
                    self._hasComments = True
                except ValueError:
                    self._data = None
        elif path.lower().endswith(".txt"):
            self._pipeLines = self._parsePipeLines(self._text)
        try:
            self._lastMtime = os.path.getmtime(path)
        except OSError:
            self._lastMtime = None

    def _checkForExternalChanges(self):
        if not self._selectedPath:
            return
        try:
            mtime = os.path.getmtime(self._selectedPath)
        except OSError:
            return
        if mtime != self._lastMtime:
            self._load()

    def _parsePipeLines(self, text):
        lines = text.splitlines()
        if not any("|" in ln and not ln.strip().startswith("#") for ln in lines):
            return None
        parsed = []
        for ln in lines:
            if ln.strip() and "|" in ln and not ln.strip().startswith("#"):
                parsed.append({"type": "data", "fields": ln.split("|")})
            else:
                parsed.append({"type": "text", "raw": ln})
        return parsed

    def _readFile(self, path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                return f.read()
        except UnicodeDecodeError:
            with open(path, "r", encoding="latin-1") as f:
                return f.read()
        except OSError as e:
            return f"[okunamadi: {e}]"

    def renderDockContent(self):
        if not self._selectedPath:
            imgui.text("Bir config dosyasi sec")
            return
        self._checkForExternalChanges()
        imgui.text(self._selectedPath)
        isJsonGrid = isinstance(self._data, dict)
        isPipeGrid = self._pipeLines is not None
        self._renderToolbar(isJsonGrid, isPipeGrid)
        imgui.separator()
        if isJsonGrid:
            self._renderGrid()
        elif isPipeGrid:
            self._renderPipeTree()
        else:
            _, self._text = imgui.input_text_multiline(
                "##configContent", self._text, (-1, -1),
                imgui.InputTextFlags_.allow_tab_input.value)

    def _renderToolbar(self, isJsonGrid, isPipeGrid):
        if not isJsonGrid and not isPipeGrid:
            if imgui.button("Dosyaya Kaydet"):
                self._saveTextFile()
        elif isPipeGrid:
            if imgui.button("Satir Ekle"):
                self._addPipeRow()
            imgui.same_line()
            if imgui.button("Dosyaya Kaydet"):
                self._savePipeFile()
        elif isJsonGrid:
            if self._hasComments:
                _, self._confirmOverwriteComments = imgui.checkbox(
                    "Yorumlarin silinecegini biliyorum", self._confirmOverwriteComments)
                imgui.same_line()
                imgui.begin_disabled(not self._confirmOverwriteComments)
                if imgui.button("Dosyaya Kaydet"):
                    self._saveJsonFile()
                imgui.end_disabled()
            else:
                if imgui.button("Dosyaya Kaydet"):
                    self._saveJsonFile()
        imgui.same_line()
        if imgui.button("Reload from File"):
            self._load()
            self._statusText = "Dosyadan yeniden yuklendi"
        if self._statusText:
            imgui.same_line()
            imgui.text_disabled(self._statusText)

    def _saveTextFile(self):
        try:
            with open(self._selectedPath, "w", encoding="utf-8", newline="\n") as f:
                f.write(self._text)
            self._lastMtime = os.path.getmtime(self._selectedPath)
            self._statusText = "Kaydedildi"
        except OSError as e:
            self._statusText = f"Kaydetme hatasi: {e}"

    def _addPipeRow(self):
        dataRows = [row for row in self._pipeLines if row["type"] == "data"]
        if dataRows:
            newFields = list(dataRows[-1]["fields"])
            if newFields and ":" not in newFields[0]:
                newFields[0] = "NewStrategy"
            self._statusText = "Son satir kopyalandi (Alan 1 = NewStrategy) - duzenleyip kaydet"
        else:
            newFields = ["NewStrategy", "", ""]
            self._statusText = "Satir eklendi (henuz kaydedilmedi)"
        self._pipeLines.append({"type": "data", "fields": newFields})

    def _savePipeFile(self):
        lines = []
        for row in self._pipeLines:
            if row["type"] == "text":
                lines.append(row["raw"])
            else:
                lines.append("|".join(row["fields"]))
        try:
            with open(self._selectedPath, "w", encoding="utf-8", newline="\n") as f:
                f.write("\n".join(lines) + "\n")
            self._lastMtime = os.path.getmtime(self._selectedPath)
            self._statusText = "Kaydedildi"
        except OSError as e:
            self._statusText = f"Kaydetme hatasi: {e}"

    def _saveJsonFile(self):
        try:
            text = json.dumps(self._data, indent=2, ensure_ascii=False)
            with open(self._selectedPath, "w", encoding="utf-8", newline="\n") as f:
                f.write(text + "\n")
            self._lastMtime = os.path.getmtime(self._selectedPath)
            self._confirmOverwriteComments = False
            self._hasComments = False
            self._statusText = "Kaydedildi"
        except OSError as e:
            self._statusText = f"Kaydetme hatasi: {e}"

    def _renderGrid(self):
        flags = (imgui.TableFlags_.borders.value |
                  imgui.TableFlags_.resizable.value |
                  imgui.TableFlags_.row_bg.value)
        if not imgui.begin_table("configGrid", 2, flags):
            return
        imgui.table_setup_column("Key", imgui.TableColumnFlags_.width_stretch.value, 0.4)
        imgui.table_setup_column("Value", imgui.TableColumnFlags_.width_stretch.value, 0.6)
        imgui.table_headers_row()
        for key in list(self._data.keys()):
            self._renderNode(self._data, key, self._enumFields)
        imgui.end_table()

    def _renderNode(self, container, key, enumNode):
        value = container[key]
        label = str(key)
        childEnumNode = enumNode.get(key) if isinstance(enumNode, dict) else None
        if isinstance(value, dict):
            imgui.table_next_row()
            imgui.table_next_column()
            opened = imgui.tree_node(label)
            imgui.table_next_column()
            if opened:
                for k in list(value.keys()):
                    self._renderNode(value, k, childEnumNode)
                imgui.tree_pop()
        elif isinstance(value, list):
            imgui.table_next_row()
            imgui.table_next_column()
            opened = imgui.tree_node(f"{label} [{len(value)}]")
            imgui.table_next_column()
            if opened:
                for i in range(len(value)):
                    self._renderNode(value, i, None)
                imgui.tree_pop()
        elif isinstance(childEnumNode, list) and isinstance(value, str):
            imgui.table_next_row()
            imgui.table_next_column()
            imgui.text(label)
            imgui.table_next_column()
            imgui.set_next_item_width(-1)
            options = childEnumNode
            currentIdx = options.index(value) if value in options else 0
            widgetId = f"##{id(container)}_{key}"
            changed, newIdx = imgui.combo(widgetId, currentIdx, options)
            if changed:
                container[key] = options[newIdx]
        else:
            imgui.table_next_row()
            imgui.table_next_column()
            imgui.text(label)
            imgui.table_next_column()
            imgui.set_next_item_width(-1)
            widgetId = f"##{id(container)}_{key}"
            if isinstance(value, bool):
                changed, newVal = imgui.checkbox(widgetId, value)
            elif isinstance(value, int):
                changed, newVal = imgui.input_int(widgetId, value)
            elif isinstance(value, float):
                changed, newVal = imgui.input_float(widgetId, value)
            else:
                changed, newVal = imgui.input_text(widgetId, str(value))
            if changed:
                container[key] = newVal

    def _renderPipeTree(self):
        flags = (imgui.TableFlags_.borders.value |
                 imgui.TableFlags_.resizable.value |
                 imgui.TableFlags_.row_bg.value |
                 imgui.TableFlags_.scroll_x.value |
                 imgui.TableFlags_.scroll_y.value)
        if not imgui.begin_table("pipeTree", 2, flags, (-1, -1)):
            return
        imgui.table_setup_column("Key", imgui.TableColumnFlags_.width_stretch.value, 0.4)
        imgui.table_setup_column("Value", imgui.TableColumnFlags_.width_stretch.value, 0.6)
        imgui.table_headers_row()
        for rowIndex, row in enumerate(self._pipeLines):
            imgui.table_next_row()
            imgui.table_next_column()
            if row["type"] == "text":
                imgui.text_disabled(row["raw"])
                imgui.table_next_column()
                continue
            headerFields = [f for f in row["fields"] if ":" not in f]
            label = " | ".join(headerFields) if headerFields else f"Satir {rowIndex + 1}"
            opened = imgui.tree_node(f"{label}###row{rowIndex}")
            imgui.table_next_column()
            if opened:
                headerIdx = 0
                for fieldIndex, original in enumerate(row["fields"]):
                    if ":" in original:
                        parts = original.split(":", 1)
                        leafLabel = parts[0]
                        leafValue = parts[1]
                    else:
                        headerIdx += 1
                        leafLabel = f"Alan {headerIdx}"
                        leafValue = original
                    imgui.table_next_row()
                    imgui.table_next_column()
                    imgui.text(leafLabel)
                    imgui.table_next_column()
                    imgui.set_next_item_width(-1)
                    widgetId = f"##pipe_{rowIndex}_{fieldIndex}"
                    changed, newVal = imgui.input_text(widgetId, leafValue)
                    if changed:
                        if ":" in original:
                            row["fields"][fieldIndex] = f"{leafLabel}:{newVal}"
                        else:
                            row["fields"][fieldIndex] = newVal
                imgui.tree_pop()
        imgui.end_table()
