import os
from datetime import datetime

from imgui_bundle import imgui

from ..trading.stockDataReader import StockDataReader, FilterMode


class DataManager:
    """Bagimsiz (standalone) Data Manager penceresi - DearPyGuiDataPlotter'daki
    plotting/dataManager.py'nin imgui_bundle PORTU (backend swap, ayni
    davranis/amac: bir kok dizini tarar, market/period/symbol agacini doldurur,
    secilen sembolun CSV'sini okur). Bu surum de DPG orijinali gibi KENDI
    BASINA calisir - okunan veriyi panel/pool/sol-agac gibi baska GUI
    kontrollerine BASMAZ, sadece getReader()/readData() ile disariya sunar.

    Mekanik farklar (tasarim degil, ImGui/DPG API farki):
    - DPG'nin agac + item_handler_registry (tiklama) kombinasyonu yerine
      imgui.tree_node()/imgui.selectable() (tiklama zaten bool donus degeri).
    - DPG'nin string-degerli combo'su yerine imgui.combo (int index) - secili
      deger STRING olarak degil, index + kendi listesi uzerinden tutulur.
    - DPG'nin pixel-exact pos=(x,y) Read-Mode mikro-duzeni yerine normal
      imgui same_line() akisi (gorsel sonuc ayni gruplama, duzen daha basit/
      saglam).
    - DPG'nin "..." Browse butonu (dpg.file_dialog) PORT EDILMEDI (native bir
      imgui dosya dialogu yok; kapsam disi birakildi) - dizin yolu metin
      kutusuna elle yazilir, Scan butonu ayni sekilde calisir.
    - DPG'nin Read-Mode combo'sunda acik popup'ta yukari/asagi ok tuslarinin
      native calismamasi icin eklenmis ozel workaround PORT EDILMEDI - imgui
      combo popup'lari ok tuslarini zaten NATIVE destekler (bu, DPG'ye ozgu
      bir hata duzeltmesiydi, imgui'de gereksiz).
    """

    READ_MODE_ITEMS = [
        "Full Data", "First N Data", "Last N Data", "Index Range",
        "Start Date", "Stop Date", "Date Range",
    ]
    MODE_FIELDS = {
        "First N Data": ("n1",), "Last N Data": ("n1",), "Index Range": ("n1", "n2"),
        "Start Date": ("dt1",), "Stop Date": ("dt2",), "Date Range": ("dt1", "dt2"),
    }

    def __init__(self):
        self._visible = False
        self._initialStateApplied = False
        self._initialPos = None
        self._initialSize = None

        self._dirPath = "C:\\data\\csvFiles\\"
        self._markets = {}  # {market: {symbol: {period, ...}}}
        self._marketItems = ["All (0)"]
        self._marketIdx = 0
        self._periodItems = ["All"]
        self._periodIdx = 0
        self._symbolFilter = ""
        self._symbolItems = []
        self._symbolIdx = 0
        self._currentMarket = "All"
        self._currentPeriod = "All"

        self._reader = None
        self._readSource = None
        self._fullPath = ""
        self._symbol = ""
        self._period = ""
        self._selBaseDir = ""
        self._selMarket = ""
        self._selSymbol = ""
        self._selFilename = ""
        self._selPeriod = ""

        self._metaBarCount = 0
        self._metaStartDate = ""
        self._metaStopDate = ""
        self._metadataText = ""

        self._readModeIdx = 0
        self._n1 = 0
        self._n2 = 0
        self._dt1 = ""
        self._dt2 = ""
        self._lastReadMode = "Full Data"
        self._modeValues = {}  # {modeName: {"n1":...,"n2":...,"dt1":...,"dt2":...}}
        self._dockWindow = None

    # ---- visibility / initial geometry (coordinator wires this from config) --
    def isVisible(self):
        return self._dockWindow.is_visible if self._dockWindow else self._visible

    def setDockWindow(self, dockWindow):
        self._dockWindow = dockWindow

    def setVisible(self, v: bool):
        if self._dockWindow: self._dockWindow.is_visible = bool(v)
        else: self._visible = bool(v)

    def getVisible(self):
        return self._dockWindow.is_visible if self._dockWindow else self._visible

    def toggle(self):
        self.setVisible(not self.getVisible())

    def show(self): self.setVisible(True)
    def hide(self): self.setVisible(False)

    def build(self, x=100, y=100, width=520, height=800, onClose=None):
        self.applyInitialState({"x": x, "y": y, "width": width, "height": height}, self._visible)
        self._onCloseCallback = onClose

    def setGeometry(self, x, y, width, height):
        self._initialPos = (float(x), float(y))
        self._initialSize = (float(width), float(height))
        self._initialStateApplied = False

    def applyInitialState(self, coords: dict | None, visible: bool):
        """coords: {'x','y','width','height'} (DearPyGuiDataPlotter config.json
        'panels.dataManager.initialCoordinates' ile ayni sema) veya None."""
        self._visible = bool(visible)
        if coords:
            self._initialPos = (float(coords.get("x", 0)), float(coords.get("y", 0)))
            self._initialSize = (float(coords.get("width", 520)), float(coords.get("height", 820)))
        self._initialStateApplied = False

    # ---- render -----------------------------------------------------------
    def renderDockContent(self):
        self._renderContents()

    def _renderContents(self):
        self._renderDirectoryRow()
        imgui.separator()
        self._renderFilterRow()
        imgui.separator()
        self._renderSymbolRow()
        imgui.separator()
        imgui.begin_child("dm_tree", size=(-1, 260), child_flags=imgui.ChildFlags_.borders.value)
        self._renderTree()
        imgui.end_child()
        imgui.separator()
        self._renderPathRow()
        imgui.separator()
        self._renderReadModeAndMetadata()

    def render(self):
        if self._dockWindow is not None:
            return
        if not self._visible:
            return
        if not self._initialStateApplied:
            if self._initialPos:
                imgui.set_next_window_pos(self._initialPos, imgui.Cond_.once.value)
            if self._initialSize:
                imgui.set_next_window_size(self._initialSize, imgui.Cond_.once.value)
            self._initialStateApplied = True

        expanded, stillOpen = imgui.begin("Data Manager", True)
        if not stillOpen:
            self._visible = False
        if expanded:
            self._renderContents()
        imgui.end()

    def _renderDirectoryRow(self):
        imgui.text("Directory:")
        imgui.set_next_item_width(350)
        changed, self._dirPath = imgui.input_text("##dm_dir_input", self._dirPath)
        imgui.same_line()
        if imgui.button("Scan", (50, 0)):
            self._onScan()

    def _renderFilterRow(self):
        imgui.text("Filter:")
        imgui.same_line()
        imgui.text("Market")
        imgui.same_line()
        imgui.set_next_item_width(150)
        changed, newIdx = imgui.combo("##dm_market_combo", self._marketIdx, self._marketItems)
        if changed and newIdx != self._marketIdx:
            self._marketIdx = newIdx
            self._onMarketChanged()
        imgui.same_line()
        imgui.text("Period")
        imgui.same_line()
        imgui.set_next_item_width(60)
        changed, newIdx = imgui.combo("##dm_period_combo", self._periodIdx, self._periodItems)
        if changed and newIdx != self._periodIdx:
            self._periodIdx = newIdx
            self._onPeriodChanged()

    def _renderSymbolRow(self):
        imgui.set_next_item_width(160)
        changed, text = imgui.input_text_with_hint("##dm_symbol_input", "Symbol filter...", self._symbolFilter)
        if changed and text != self._symbolFilter:
            self._symbolFilter = text
            self._onSymbolInput()
        imgui.same_line()
        imgui.text("Symbol")
        imgui.same_line()
        imgui.set_next_item_width(110)
        changed, newIdx = imgui.combo("##dm_symbol_combo", self._symbolIdx, self._symbolItems or [""])
        if changed and self._symbolItems and newIdx != self._symbolIdx:
            self._symbolIdx = newIdx
            self._onSymbolChanged(self._symbolItems[newIdx])
        imgui.same_line()
        imgui.text(f"({len(self._symbolItems)})")

    def _renderTree(self):
        market = self._currentMarket
        filterText = self._symbolFilter
        period = self._currentPeriod
        if market == "All" and not filterText:
            for m in sorted(self._markets.keys()):
                if imgui.tree_node(m):
                    for sym in sorted(self._markets[m].keys()):
                        if imgui.selectable(sym, False)[0]:
                            self._onSymbolLeafClicked(sym)
                    imgui.tree_pop()
            return
        symbolMap = self._getSymbols(market)
        symbols = sorted(symbolMap.keys())
        if filterText:
            symbols = [s for s in symbols if s.upper().startswith(filterText.upper())]
        for sym in symbols:
            periods = symbolMap.get(sym, set())
            if period != "All":
                periods = {p for p in periods if p == period}
            sortedPeriods = sorted([p for p in periods if p.isdigit()], key=int) + \
                sorted([p for p in periods if not p.isdigit()])
            if sortedPeriods:
                if imgui.tree_node(sym):
                    for p in sortedPeriods:
                        if imgui.selectable(p, False)[0]:
                            self._onPeriodLeafClicked(sym, p)
                    imgui.tree_pop()
            else:
                if imgui.selectable(sym, False)[0]:
                    self._onSymbolLeafClicked(sym)

    def _renderPathRow(self):
        imgui.text("Path:")
        imgui.same_line()
        imgui.text_wrapped(self._fullPath or "Not Selected...")
        if imgui.button("Select VIP-X030-T 1Dk"):
            self._quickSelectSymbol("VIP", "VIP-X030-T", "01")
        imgui.same_line()
        if imgui.button("Select THYAO 5Dk"):
            self._quickSelectSymbol("IMKBH", "THYAO", "05")
        imgui.same_line()
        if imgui.button("Read MetaData"):
            self._onReadMetadata()

    def _renderReadModeAndMetadata(self):
        imgui.begin_child("dm_readmode", size=(300, 140), child_flags=imgui.ChildFlags_.borders.value)
        imgui.set_next_item_width(130)
        changed, newIdx = imgui.combo("##dm_read_mode_combo", self._readModeIdx, self.READ_MODE_ITEMS)
        if changed and newIdx != self._readModeIdx:
            self._onReadModeChanged(self.READ_MODE_ITEMS[newIdx])

        mode = self.READ_MODE_ITEMS[self._readModeIdx]
        if mode in ("First N Data", "Last N Data", "Index Range"):
            imgui.set_next_item_width(120)
            _, self._n1 = imgui.input_int("N1", self._n1)
        if mode == "Index Range":
            imgui.set_next_item_width(120)
            _, self._n2 = imgui.input_int("N2", self._n2)
        if mode in ("Start Date", "Date Range"):
            imgui.set_next_item_width(130)
            _, self._dt1 = imgui.input_text("Dt1 (yyyy.mm.dd)", self._dt1)
        if mode in ("Stop Date", "Date Range"):
            imgui.set_next_item_width(130)
            _, self._dt2 = imgui.input_text("Dt2 (yyyy.mm.dd)", self._dt2)

        if imgui.button("Read", (80, 0)):
            self._onRead()
        imgui.same_line()
        if imgui.button("Clear", (80, 0)):
            self._onClear()
        imgui.end_child()
        imgui.same_line()
        imgui.begin_child("dm_metadata", size=(-1, 140), child_flags=imgui.ChildFlags_.borders.value)
        imgui.text_wrapped(self._metadataText)
        imgui.end_child()

    # ---- scan / tree --------------------------------------------------------
    def _onScan(self):
        self._markets.clear()
        if not os.path.isdir(self._dirPath):
            return
        totalSymbols = set()
        for entry in sorted(os.listdir(self._dirPath)):
            marketPath = os.path.join(self._dirPath, entry)
            if not os.path.isdir(marketPath):
                continue
            marketSymbols = {}
            for period in os.listdir(marketPath):
                periodPath = os.path.join(marketPath, period)
                if not os.path.isdir(periodPath):
                    continue
                for f in os.listdir(periodPath):
                    name, ext = os.path.splitext(f)
                    if ext.lower() in (".csv", ".txt", ".dat"):
                        marketSymbols.setdefault(name, set()).add(period)
                        totalSymbols.add(name)
            self._markets[entry] = marketSymbols
        self._marketItems = [f"All ({len(totalSymbols)})"] + \
            [f"{m} ({len(self._markets[m])})" for m in sorted(self._markets.keys())]
        self._marketIdx = 0
        self._currentMarket = "All"
        self._symbolFilter = ""
        self._currentPeriod = "All"
        self._periodItems = ["All"]
        self._periodIdx = 0
        self._updateSymbolCombo()

    def _getSymbols(self, market: str):
        if market == "All":
            result = {}
            for m, syms in self._markets.items():
                for s, periods in syms.items():
                    result.setdefault(s, set()).update(periods)
            return result
        return self._markets.get(market, {})

    def _updateSymbolCombo(self):
        if self._currentMarket == "All":
            symbols, seen = [], set()
            for m in sorted(self._markets.keys()):
                for sym in sorted(self._markets[m].keys()):
                    if sym not in seen:
                        symbols.append(sym)
                        seen.add(sym)
        else:
            symbols = sorted(self._getSymbols(self._currentMarket).keys())
        if self._symbolFilter:
            symbols = [s for s in symbols if s.upper().startswith(self._symbolFilter.upper())]
        self._symbolItems = symbols
        self._symbolIdx = 0
        if symbols:
            self._setPeriods(symbols[0])

    def _onMarketChanged(self):
        value = self._marketItems[self._marketIdx]
        self._currentMarket = value.split(" (")[0] if value else "All"
        self._symbolFilter = ""
        self._currentPeriod = "All"
        self._periodItems = ["All"]
        self._periodIdx = 0
        self._updateSymbolCombo()

    def _onSymbolChanged(self, symbol):
        if symbol and symbol != self._symbolFilter:
            self._symbolFilter = symbol
            self._setPeriods(symbol)
            self._updatePathLabel()

    def _onSymbolInput(self):
        self._updateSymbolCombo()
        if not self._symbolFilter:
            self._periodItems = ["All"]
            self._periodIdx = 0
            self._currentPeriod = "All"

    def _onPeriodChanged(self):
        value = self._periodItems[self._periodIdx]
        if value == self._currentPeriod:
            return
        self._currentPeriod = value
        self._updatePathLabel()

    def _setPeriods(self, symbol: str):
        symbolMap = self._getSymbols(self._currentMarket)
        periods = symbolMap.get(symbol, set())
        numPeriods = sorted([p for p in periods if p.isdigit()], key=int)
        strPeriods = sorted([p for p in periods if not p.isdigit()])
        self._periodItems = ["All"] + numPeriods + strPeriods
        self._periodIdx = 0
        self._currentPeriod = "All"
        self._updatePathLabel()

    def _onSymbolLeafClicked(self, symbol):
        self._symbolFilter = symbol
        self._setPeriods(symbol)

    def _onPeriodLeafClicked(self, symbol, period):
        self._symbolFilter = symbol
        self._setPeriods(symbol)
        if period in self._periodItems:
            self._periodIdx = self._periodItems.index(period)
            self._currentPeriod = period
        self._updatePathLabel()

    def _updatePathLabel(self):
        path = self._selectedFilePath()
        self._fullPath = path or ""

    def _selectedFilePath(self):
        market = self._currentMarket
        symbol = (self._symbolItems[self._symbolIdx] if self._symbolItems else "") or self._symbolFilter
        period = self._currentPeriod or "All"
        if not symbol:
            return ""
        if market == "All":
            for m, syms in self._markets.items():
                if any(s.upper() == symbol.upper() for s in syms):
                    market = m
                    break
        return os.path.join(self._dirPath, market, period, f"{symbol}.csv")

    def _quickSelectSymbol(self, market, symbol, period):
        self._onScan()
        marketValue = next((it for it in self._marketItems if it.split(" (")[0] == market), None)
        if marketValue:
            self._marketIdx = self._marketItems.index(marketValue)
            self._onMarketChanged()
        self._symbolFilter = symbol
        self._onSymbolInput()
        if symbol in self._symbolItems:
            self._symbolIdx = self._symbolItems.index(symbol)
        self._setPeriods(symbol)
        if period in self._periodItems:
            self._periodIdx = self._periodItems.index(period)
            self._currentPeriod = period
        self._updatePathLabel()

    # ---- read mode ----------------------------------------------------------
    def _onReadModeChanged(self, newMode):
        self._saveModeValues(self._lastReadMode)
        self._readModeIdx = self.READ_MODE_ITEMS.index(newMode)
        self._restoreModeValues(newMode)
        self._lastReadMode = newMode

    def _saveModeValues(self, mode):
        fields = self.MODE_FIELDS.get(mode, ())
        if not fields:
            return
        values = self._modeValues.setdefault(mode, {})
        for field in fields:
            values[field] = getattr(self, f"_{field}")

    def _restoreModeValues(self, mode):
        values = self._modeValues.get(mode)
        if not values:
            return
        for field, value in values.items():
            setattr(self, f"_{field}", value)

    def _selectedReadFilter(self):
        mode = self.READ_MODE_ITEMS[self._readModeIdx]
        dt1 = self._parseDatetimeValue(self._dt1)
        dt2 = self._parseDatetimeValue(self._dt2)
        if mode == "First N Data":
            return FilterMode.FirstN, self._n1, 0, None, None
        if mode == "Last N Data":
            return FilterMode.LastN, self._n1, 0, None, None
        if mode == "Index Range":
            return FilterMode.IndexRange, self._n1, self._n2, None, None
        if mode == "Start Date":
            return FilterMode.AfterDateTime, 0, 0, dt1, None
        if mode == "Stop Date":
            return FilterMode.BeforeDateTime, 0, 0, dt2, None
        if mode == "Date Range":
            return FilterMode.DateTimeRange, 0, 0, dt1, dt2
        return FilterMode.All, 0, 0, None, None

    def _parseDatetimeValue(self, value):
        value = str(value or "").strip()
        if not value:
            return None
        for fmt in ("%Y.%m.%d %H:%M:%S", "%Y-%m-%d %H:%M:%S", "%d.%m.%Y %H:%M:%S",
                    "%Y.%m.%d", "%Y-%m-%d", "%d.%m.%Y"):
            try:
                return datetime.strptime(value, fmt)
            except ValueError:
                pass
        print(f"Invalid date: {value}")
        return None

    # ---- read / metadata ------------------------------------------------------
    def _onRead(self):
        filePath = self._selectedFilePath()
        if not filePath:
            print("No symbol selected")
            return None
        self._decomposeFullPath(filePath)
        print(f"Symbol : {self._selSymbol}\nMarket : {self._selMarket}\nPeriod : {self._selPeriod}")
        self._readSource = "manual"
        return self._readFile(filePath)

    def _onReadMetadata(self):
        filePath = self._selectedFilePath()
        if not filePath:
            print("No symbol selected")
            self._metadataText = "No symbol selected"
            return None
        return self._readMetadataFile(filePath)

    def _readMetadataFile(self, filePath):
        self._decomposeFullPath(filePath)
        if not os.path.isfile(filePath):
            self._metadataText = f"File not found:\n{filePath}"
            print(self._metadataText)
            return None
        reader = StockDataReader()
        meta = reader.readMetaData(filePath)
        self._applyMetadataDefaults(meta)
        self._seedReadModeInputs()
        self._metadataText = "\n".join([
            f"Path   : {filePath}", f"Market : {self._selMarket}", f"Symbol : {self._selSymbol}",
            f"Period : {self._selPeriod}", f"Bars   : {self._metaBarCount or '...'}",
            f"Start  : {self._metaStartDate or '...'}", f"Stop   : {self._metaStopDate or '...'}",
        ])
        print(self._metadataText)
        return meta

    def _applyMetadataDefaults(self, meta):
        self._metaBarCount = self._metadataInt(meta, "BarCount")
        self._metaStartDate = self._metadataDate(meta, start=True)
        self._metaStopDate = self._metadataDate(meta, start=False)

    def _seedReadModeInputs(self):
        barCount = int(self._metaBarCount or 0)
        self._modeValues = {
            "First N Data": {"n1": 1000}, "Last N Data": {"n1": 1000},
            "Index Range": {"n1": 0, "n2": max(0, barCount - 1)},
            "Start Date": {"dt1": self._metaStartDate}, "Stop Date": {"dt2": self._metaStopDate},
            "Date Range": {"dt1": self._metaStartDate, "dt2": self._metaStopDate},
        }
        self._restoreModeValues(self.READ_MODE_ITEMS[self._readModeIdx])

    def _metadataInt(self, meta, wantedKey):
        if not meta:
            return 0
        for key, value in meta.items():
            if key == wantedKey:
                try:
                    return int(str(value).strip())
                except ValueError:
                    return 0
        return 0

    def _metadataDate(self, meta, start=True):
        if not meta:
            return ""
        needles = ("baş", "bas", "lang", "start") if start else ("biti", "stop", "end")
        for key, value in meta.items():
            normalized = key.lower()
            if any(n in normalized for n in needles) and "tarih" in normalized:
                return str(value).strip()
        for key, value in meta.items():
            if any(n in key.lower() for n in needles):
                return str(value).strip()
        return ""

    def _onClear(self):
        self._reader = None
        self._readSource = None
        self._fullPath = ""
        self._symbol = ""
        self._period = ""
        self._selBaseDir = self._selMarket = self._selSymbol = self._selFilename = self._selPeriod = ""
        self._metadataText = ""
        print("Data temizlendi (reader silindi).")

    def _readFile(self, filePath):
        self._decomposeFullPath(filePath)
        print(f"Path   : {filePath}\n")
        if not os.path.isfile(filePath):
            print(f"File not found: {filePath}")
            return None
        reader = StockDataReader()
        try:
            reader.readMetaData(filePath)
            self._applyMetadataDefaults(reader.metaData)
            for key in ["GrafikPeriyot", "BarCount", "Başlangiç Tarihi", "Bitiş Tarihi"]:
                if key in reader.metaData:
                    print(f"{key:<17} : {reader.metaData[key]}")
            print("\nVeri Okunuyor...")
            mode, n1, n2, dt1, dt2 = self._selectedReadFilter()
            reader.readDataWithPandas(filePath, mode=mode, n1=n1, n2=n2, dt1=dt1, dt2=dt2)
            print(f"\nVeri Okundu\n{'BarCount':<17} : {reader.data.length}\n{'ElapsdTime':<17} : {reader.elapsedMs} ms\n")
            self._reader = reader
            return reader
        except Exception as e:
            print(f"Read error: {e}")
            return None

    def _decomposeFullPath(self, filePath):
        periodDir = os.path.dirname(filePath)
        marketDir = os.path.dirname(periodDir)
        baseDir = os.path.dirname(marketDir)
        filename = os.path.basename(filePath)
        symbol = os.path.splitext(filename)[0]
        period = os.path.basename(periodDir)
        self._selBaseDir = baseDir
        self._selMarket = os.path.basename(marketDir)
        self._selPeriod = period
        self._selFilename = filename
        self._selSymbol = symbol
        self._symbol = symbol
        self._period = period
        self._fullPath = filePath

    # ---- public API (script / entegrasyon icin) --------------------------
    def hasReader(self):
        return self._reader is not None

    def getReader(self):
        return self._reader

    def getSembolFullPath(self):
        return self._fullPath

    def getSembolBaseDir(self):
        return self._selBaseDir

    def getSembolMarket(self):
        return self._selMarket

    def getSembolName(self):
        return self._selSymbol

    def getSembolFileName(self):
        return self._selFilename

    def getSembolPeriyod(self):
        return self._selPeriod

    def getSembolDataCount(self):
        return self._reader.data.length if self._reader else 0

    def getPeriod(self):
        return self._period

    def getSymbol(self):
        return self._symbol

    def isIntraday(self):
        return self._period.isdigit()

    def setReader(self, reader):
        self._reader = reader

    def setSembolFullPath(self, path):
        self._fullPath = path

    def setSembolBaseDir(self, baseDir):
        self._selBaseDir = baseDir
        self._applySelectionPath()

    def setSembolMarket(self, market):
        self._selMarket = market
        self._applySelectionPath()

    def setSembolName(self, name):
        self._selSymbol = name
        self._symbol = name
        self._applySelectionPath()

    def setSembolFileName(self, filename):
        self._selFilename = filename
        self._symbol = os.path.splitext(filename)[0]
        self._applySelectionPath()

    def setSembolPeriyod(self, period):
        self._period = period
        self._selPeriod = period
        self._applySelectionPath()

    def _applySelectionPath(self):
        filename = self._selFilename or (f"{self._selSymbol}.csv" if self._selSymbol else "")
        if self._selMarket and filename and self._selPeriod:
            dirPath = self._selBaseDir or self._dirPath
            self.setSembolFullPath(os.path.join(dirPath, self._selMarket, self._selPeriod, filename))

    def readData(self, filePath=None):
        if filePath is None:
            filePath = self._fullPath
        self._readSource = "script"
        return self._readFile(filePath)

    def getReadSource(self):
        return self._readSource
