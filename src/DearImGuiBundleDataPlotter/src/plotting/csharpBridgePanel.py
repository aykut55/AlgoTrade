import os
import threading
import time
import traceback
import types

from imgui_bundle import imgui

from ..trading import csharpBridge
from .panel import Panel
from .panelData import PanelData

# inputs/scripts/csharpBridge/ - AlgoTrade repo KOKUNDEKI inputs/scripts/
# altinda AYRI bir alt klasor (diger/ileride eklenecek scriptlerle karismasin
# diye) - kullanici bu dosyalari Notepad++ ile DOGRUDAN duzenler ("Open"
# butonu Windows'un .csx/.py icin varsayilan editorunu acar).
# _PLOTTER_DIR = <repo kok>/src/DearImGuiBundleDataPlotter (bu dosyadan 3 ust
# dizin); inputs/scripts/csharpBridge repo KOKUNDE oldugu icin 2 ust dizin daha.
_PLOTTER_DIR = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPTS_DIR = os.path.abspath(os.path.join(_PLOTTER_DIR, "..", "..", "inputs", "scripts", "csharpBridge"))

READ_DATA_SCRIPT_PATH = os.path.join(SCRIPTS_DIR, "readData.csx")
SIGNAL_SCRIPT_PATH = os.path.join(SCRIPTS_DIR, "signal.csx")
FILTER_SCRIPT_PATH = os.path.join(SCRIPTS_DIR, "filter.csx")
PLOT_SCRIPT_PATH = os.path.join(SCRIPTS_DIR, "plot.py")
CALCULATE_SCRIPT_PATH = os.path.join(SCRIPTS_DIR, "calculate.csx")
WRITE_TO_DISK_SCRIPT_PATH = os.path.join(SCRIPTS_DIR, "writeToDisk.csx")

DEFAULT_CSV_PATH = r"C:\data\csvFiles\VIP\05\VIP-X030-T.csv"


class CSharpBridgePanel:
    """6 asama (Read Data / Signal / Filter / Plot / Calculate / Write To Disk),
    her biri inputs/scripts/csharpBridge/ altinda AYRI bir dosya - "Open" butonu
    dosyayi Windows'un varsayilan editoruyle (Notepad++ dahil) acar, kullanici
    duzenler+kaydeder, ilgili "calistir" butonu GUNCEL dosya icerigini diskten
    okuyup calistirir.

    pythonnet uzerinden AlgoTrade (orijinal repo) C# kodunu dogrudan cagirir
    (Read Data/Signal/Filter/Calculate/Write To Disk icin Roslyn) - dosya/bundle
    round-trip'i yok. Plot asamasi Python'dur (bu plotter'in GERCEK PanelManager/
    Panel/TradeSignalRenderer altyapisini kullanir, default.py'nin de kullandigi
    ayni siniflar)."""

    def __init__(self, panelManager, tradeSignalRenderer):
        self._pm = panelManager
        self._tsr = tradeSignalRenderer
        self._visible = True
        self._dockWindow = None

        self._filePath = DEFAULT_CSV_PATH
        self._lastN = 0  # 0 = Tumu (FilterMode.All)
        self._statusText = ""

        self._netData = None
        self._rawSignals = None    # Signal asamasindan (ham)
        self._signals = None       # Filter asamasindan (Plot/Calculate bunu kullanir)
        self._signalIndicators = []  # Signal asamasindan - OHLC uzerine cizilecek MA/indikator serileri

        self._algoTrader = None
        self._replaySummary = None

        self._ohlcPanel = None
        self._signalPanel = None

        self._writeThread = None  # Write To Disk arka planda - GUI render dongusunu bloklamasin

        _ensureDefaultScriptsExist()

    # --------------------------------------------------------- visibility
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

    # -------------------------------------------------------------- render
    def renderDockContent(self):
        imgui.text("1) Read Data")
        imgui.text_disabled(READ_DATA_SCRIPT_PATH)
        imgui.set_next_item_width(-80)
        _, self._filePath = imgui.input_text("##csvPath", self._filePath)
        imgui.same_line()
        imgui.set_next_item_width(70)
        _, self._lastN = imgui.input_int("Bar (0=Tumu)##lastN", self._lastN, 0)
        if imgui.button("Open##openReadData"):
            _openInEditor(READ_DATA_SCRIPT_PATH)
        imgui.same_line()
        if imgui.button("Read Data"):
            self._onReadData()
        imgui.same_line()
        imgui.text_disabled(self._dataStatusLabel())

        imgui.separator()
        imgui.text("2) Signal")
        imgui.text_disabled(SIGNAL_SCRIPT_PATH)
        if imgui.button("Open##openSignal"):
            _openInEditor(SIGNAL_SCRIPT_PATH)
        imgui.same_line()
        if imgui.button("Run Signal"):
            self._onRunSignal()
        imgui.same_line()
        imgui.text_disabled(self._rawSignalStatusLabel())

        imgui.separator()
        imgui.text("3) Filter")
        imgui.text_disabled(FILTER_SCRIPT_PATH)
        if imgui.button("Open##openFilter"):
            _openInEditor(FILTER_SCRIPT_PATH)
        imgui.same_line()
        if imgui.button("Run Filter"):
            self._onRunFilter()
        imgui.same_line()
        imgui.text_disabled(self._signalStatusLabel())

        imgui.separator()
        imgui.text("4) Calculate (ReplaySignalStrategy -> SingleTrader)")
        imgui.text_disabled(CALCULATE_SCRIPT_PATH)
        if imgui.button("Open##openCalculate"):
            _openInEditor(CALCULATE_SCRIPT_PATH)
        imgui.same_line()
        if imgui.button("Calculate"):
            self._onCalculateReplay()
        imgui.same_line()
        imgui.text_disabled(self._replaySummaryLabel())

        imgui.separator()
        imgui.text("5) Plot")
        imgui.text_disabled(PLOT_SCRIPT_PATH)
        if imgui.button("Open##openPlot"):
            _openInEditor(PLOT_SCRIPT_PATH)
        imgui.same_line()
        if imgui.button("Run Plot"):
            self._onRunPlot()
        imgui.same_line()
        if imgui.button("Clear Plot"):
            self._onClearPlot()

        imgui.separator()
        imgui.text("6) Write To Disk (tum loglar)")
        imgui.text_disabled(WRITE_TO_DISK_SCRIPT_PATH)
        if imgui.button("Open##openWriteToDisk"):
            _openInEditor(WRITE_TO_DISK_SCRIPT_PATH)
        imgui.same_line()
        writing = self._writeThread is not None and self._writeThread.is_alive()
        if writing:
            imgui.text_disabled("Write To Disk (yaziliyor...)")
        elif imgui.button("Write To Disk"):
            self._onWriteReplayToDisk()

        if self._statusText:
            imgui.separator()
            imgui.text_wrapped(self._statusText)

    def _dataStatusLabel(self):
        if self._netData is None:
            return "(veri yok)"
        return f"{self._netData.Count} bar"

    def _rawSignalStatusLabel(self):
        if self._rawSignals is None:
            return "(sinyal yok)"
        al = sum(1 for s in self._rawSignals if s == 1)
        sat = sum(1 for s in self._rawSignals if s == -1)
        pasgec = sum(1 for s in self._rawSignals if s == 6)
        return f"AL={al} SAT={sat}" + (f" PasGec={pasgec}" if pasgec else "")

    def _signalStatusLabel(self):
        if self._signals is None:
            return "(filtrelenmedi)"
        al = sum(1 for s in self._signals if s == 1)
        sat = sum(1 for s in self._signals if s == -1)
        karAl = sum(1 for s in self._signals if s == 3)
        zararKes = sum(1 for s in self._signals if s == 4)
        flatOl = sum(1 for s in self._signals if s == 5)
        pasgec = sum(1 for s in self._signals if s == 6)
        extra = "".join([
            f" KarAl={karAl}" if karAl else "",
            f" ZararKes={zararKes}" if zararKes else "",
            f" FlatOl={flatOl}" if flatOl else "",
            f" PasGec={pasgec}" if pasgec else "",
        ])
        return f"AL={al} SAT={sat}{extra}"

    def _replaySummaryLabel(self):
        if self._replaySummary is None:
            return "(hesaplanmadi)"
        s = self._replaySummary
        return (f"Islem={s['tradeCount']} (K={s['winCount']}/Z={s['lossCount']}) "
                f"PnL={s['pnl']:.2f} Bakiye={s['startBalance']:.0f}->{s['endBalance']:.0f}")

    # ------------------------------------------------------------- actions
    def _setStatus(self, text):
        """Panel icindeki status satirina YAZAR ve stdout'a da print() eder -
        ConsolePanel acilista sys.stdout/sys.stderr'i tee'ledigi icin (bkz.
        guiManager.__init__ -> consolePanel.attachStdout()) butun C# Bridge
        ciktisi otomatik olarak Console Panel'de de gorunur."""
        self._statusText = text
        print(f"[{time.strftime('%H:%M:%S')}] [C# Bridge] {text}")

    def _onReadData(self):
        self._setStatus("Okunuyor...")
        t0 = time.time()
        try:
            ok, netData, metaSummary, error = csharpBridge.read_data_from_file(
                READ_DATA_SCRIPT_PATH, self._filePath, self._lastN)
            elapsed = time.time() - t0
            if not ok:
                self._netData = None
                self._setStatus(f"Read Data HATA ({elapsed:.2f}s):\n{error}")
                return
            self._netData = netData
            self._rawSignals = None
            self._signals = None
            self._signalIndicators = []
            summarySuffix = f"\n{metaSummary}" if metaSummary else ""
            self._setStatus(f"Read Data OK ({elapsed:.2f}s): {self._dataStatusLabel()}{summarySuffix}")
        except Exception as exc:
            self._netData = None
            self._setStatus(f"Read Data HATA ({time.time() - t0:.2f}s): {exc}")
            print(traceback.format_exc())

    def _onRunSignal(self):
        if self._netData is None:
            self._setStatus("Once Read Data calistirin.")
            return
        self._setStatus("Signal calistiriliyor...")
        t0 = time.time()
        try:
            ok, signals, indicators, error = csharpBridge.run_signal_script_from_file(
                SIGNAL_SCRIPT_PATH, self._netData)
            elapsed = time.time() - t0
            if not ok:
                self._rawSignals = None
                self._signals = None
                self._signalIndicators = []
                self._setStatus(f"Signal HATA ({elapsed:.2f}s):\n{error}")
                return
            self._rawSignals = signals
            self._signals = None
            self._signalIndicators = indicators
            self._setStatus(f"Run Signal OK ({elapsed:.2f}s): {self._rawSignalStatusLabel()}")
        except Exception as exc:
            self._rawSignals = None
            self._signals = None
            self._signalIndicators = []
            self._setStatus(f"Signal HATA ({time.time() - t0:.2f}s): {exc}")
            print(traceback.format_exc())

    def _onRunFilter(self):
        if self._netData is None:
            self._setStatus("Once Read Data calistirin.")
            return
        if self._rawSignals is None:
            self._setStatus("Once Run Signal calistirin.")
            return
        self._setStatus("Filter calistiriliyor...")
        t0 = time.time()
        try:
            ok, signals, error = csharpBridge.run_filter_script_from_file(
                FILTER_SCRIPT_PATH, self._netData, self._rawSignals)
            elapsed = time.time() - t0
            if not ok:
                self._signals = None
                self._setStatus(f"Filter HATA ({elapsed:.2f}s):\n{error}")
                return
            self._signals = signals
            self._setStatus(f"Run Filter OK ({elapsed:.2f}s): {self._signalStatusLabel()}")
        except Exception as exc:
            self._signals = None
            self._setStatus(f"Filter HATA ({time.time() - t0:.2f}s): {exc}")
            print(traceback.format_exc())

    def _effectiveSignals(self):
        """Filter asamasi OPSIYONEL - calistirilmissa (self._signals) onu,
        calistirilmamissa Signal asamasinin ham ciktisini (self._rawSignals)
        kullan. Hicbiri yoksa None."""
        if self._signals is not None:
            return self._signals
        return self._rawSignals

    def _onRunPlot(self):
        if self._netData is None:
            self._setStatus("Once Read Data calistirin.")
            return
        signals = self._effectiveSignals()
        if signals is None:
            self._setStatus("Once Run Signal calistirin.")
            return
        t0 = time.time()
        try:
            opens, highs, lows, closes, volumes, sizes, timestamps = csharpBridge.to_ohlc_lists(self._netData)
            symbol = os.path.splitext(os.path.basename(self._filePath))[0]
            # Calculate asamasi calistirilmissa (self._algoTrader dolu) PnL/Return
            # serilerini de cikar - Plot script'i bunlari gorup zengin panel seti
            # (PnL/PnL%/Return/Return%) kurabilir; yoksa None (Plot script primitif
            # OHLC+Signals'a duser).
            traderSeries = csharpBridge.extract_trader_series(self._algoTrader) if self._algoTrader else None
            bridge = types.SimpleNamespace(
                opens=opens, highs=highs, lows=lows, closes=closes,
                volumes=volumes, sizes=sizes, timestamps=timestamps,
                signals=signals[:len(closes)], symbol=symbol, period="",
                trader=self._algoTrader, traderSeries=traderSeries,
                signalIndicators=self._signalIndicators)

            with open(PLOT_SCRIPT_PATH, "r", encoding="utf-8") as f:
                code = f.read()
            namespace = {
                "pm": self._pm, "tsr": self._tsr,
                "Panel": Panel, "PanelData": PanelData,
                "bridge": bridge,
            }
            exec(compile(code, PLOT_SCRIPT_PATH, "exec"), namespace)
            self._setStatus(f"Plot OK ({time.time() - t0:.2f}s).")
        except Exception as exc:
            self._setStatus(f"Plot HATA ({time.time() - t0:.2f}s): {exc}")
            print(traceback.format_exc())

    def _onClearPlot(self):
        self._pm.deleteAllPanels()
        self._ohlcPanel = None
        self._signalPanel = None
        self._setStatus("Plot temizlendi.")

    def _onCalculateReplay(self):
        if self._netData is None:
            self._setStatus("Once Read Data calistirin.")
            return
        signals = self._effectiveSignals()
        if signals is None:
            self._setStatus("Once Run Signal calistirin.")
            return
        self._setStatus("Gercek simulasyon calistiriliyor (ReplaySignalStrategy)...")
        t0 = time.time()
        try:
            symbol = os.path.splitext(os.path.basename(self._filePath))[0]
            ok, algoTrader, error = csharpBridge.run_replay_from_file(
                CALCULATE_SCRIPT_PATH, self._netData, signals, symbol, "")
            elapsed = time.time() - t0
            if not ok:
                self._algoTrader = None
                self._replaySummary = None
                self._setStatus(f"Calculate HATA ({elapsed:.2f}s):\n{error}")
                return
            self._algoTrader = algoTrader
            self._replaySummary = csharpBridge.get_replay_summary(algoTrader)
            self._setStatus(f"Calculate OK ({elapsed:.2f}s): {self._replaySummaryLabel()}")
        except Exception as exc:
            self._algoTrader = None
            self._replaySummary = None
            self._setStatus(f"Calculate HATA ({time.time() - t0:.2f}s): {exc}")
            print(traceback.format_exc())

    def _onWriteReplayToDisk(self):
        if self._algoTrader is None:
            self._setStatus("Once Calculate calistirin.")
            return
        if self._writeThread is not None and self._writeThread.is_alive():
            self._setStatus("Zaten yaziliyor, bekleyin.")
            return

        self._setStatus("Diske yaziliyor (arka planda)...")
        t0 = time.time()
        algoTrader = self._algoTrader  # thread'e sabit referans (calisirken degismesin)

        def onProgress(event, payload, total):
            if event == "start":
                self._setStatus(f"Diske yaziliyor... (beklenen {total} dosya)")
            elif event == "file":
                _, path, size = payload
                self._setStatus(f"  yazildi: {os.path.basename(path)} ({size:,} bayt)")

        def worker():
            try:
                ok, written, error = csharpBridge.write_to_disk_from_file(
                    WRITE_TO_DISK_SCRIPT_PATH, algoTrader, progress_callback=onProgress)
                elapsed = time.time() - t0
                if not ok:
                    self._setStatus(f"Write To Disk HATA ({elapsed:.2f}s):\n{error}")
                    return
                self._setStatus(
                    f"Write To Disk OK ({elapsed:.2f}s): {len(written)} dosya -> {csharpBridge.get_logs_dir()}")
            except Exception as exc:
                self._setStatus(f"Write To Disk HATA ({time.time() - t0:.2f}s): {exc}")
                print(traceback.format_exc())

        # GUI render dongusu (immediate-mode, tek thread) bu cagri boyunca
        # bloklanmasin diye arka plan thread'inde calistiriyoruz - butonun
        # kendisi thread calisirken devre disi (bkz. renderDockContent "yaziliyor" kontrolu).
        self._writeThread = threading.Thread(target=worker, daemon=True)
        self._writeThread.start()


def _openInEditor(path):
    """Windows'un path icin KAYITLI "edit" verb'uyle acar (.csx/.py icin
    Notepad++ kayitliysa dogrudan o acilir) - sabit bir editor yolu ARANMAZ.
    "edit" verb'u ozellikle kullaniliyor: .py icin varsayilan "open" verb'u
    genelde CALISTIRMAK (python.exe ile) - "edit" ise metin editorune gider."""
    try:
        os.startfile(path, "edit")
    except OSError:
        try:
            os.startfile(path)
        except Exception as exc:
            print(f"[C# Bridge] Dosya acilamadi: {path} ({exc})")


_DEFAULT_SCRIPTS = {
    "readData.csx": (
        '// Read Data asamasi - globals: FilePath (string), LastN (int, 0 = Tumu)\n'
        '// return: List<StockData>\n'
        'var reader = new StockDataReader();\n'
        'reader.ReadMetaData(FilePath);\n'
        'if (!reader.IsMetaDataRead)\n'
        '    throw new Exception("MetaData okunamadi: " + FilePath);\n\n'
        'if (LastN > 0)\n'
        '    reader.ReadDataFast(FilePath, StockDataReader.FilterMode.LastN, LastN, 0, null, null);\n'
        'else\n'
        '    reader.ReadDataFast(FilePath, StockDataReader.FilterMode.All, 0, 0, null, null);\n\n'
        'return reader.GetData();\n'
    ),
}


def _ensureDefaultScriptsExist():
    """inputs/scripts/csharpBridge/ klasoru veya dosyalar bir sebeple yoksa (orn.
    ilk checkout, silindi) en azindan readData.csx icin minimal bir varsayilan
    olusturur - diger dosyalar (signal.csx/filter.csx/plot.py/calculate.csx/
    writeToDisk.csx) repo ile birlikte gelir, burada yeniden uretilmez."""
    os.makedirs(SCRIPTS_DIR, exist_ok=True)
    for name, content in _DEFAULT_SCRIPTS.items():
        path = os.path.join(SCRIPTS_DIR, name)
        if not os.path.isfile(path):
            with open(path, "w", encoding="utf-8") as f:
                f.write(content)
