import json
import os
import shutil


class RuntimeCommandManager:
    """inputs/runtime_commands klasorunu izler, disaridan (orn. C# wrapper)
    yazilan .json komut dosyalarini sirayla okuyup uygular.

    C# tarafi once "*.json.tmp" yazip atomik rename ile ".json" yapar; bu
    yuzden burada sadece tam yazilmis ".json" dosyalari islenir.
    processPendingCommands() GuiManager.render() icinden her frame cagrilir.
    """

    def __init__(self, gm):
        self._gm = gm
        root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
        self._inputsDir = os.path.join(root, "inputs")
        self._commandsDir = os.path.join(self._inputsDir, "runtime_commands")
        self._processedDir = os.path.join(self._commandsDir, "processed")
        self._failedDir = os.path.join(self._commandsDir, "failed")
        self._inputConfigPath = os.path.join(self._inputsDir, "input.json")
        self._handlers = {
            "load_bundle": self._handleLoadBundle,
            "clear_panel": self._handleClearPanel,
            "clear_all_panels": self._handleClearAllPanels,
            "reload_current": self._handleReloadCurrent,
            "add_series_from_bundle": self._handleAddSeriesFromBundle,
            "shutdown": self._handleShutdown,
        }
        os.makedirs(self._commandsDir, exist_ok=True)
        os.makedirs(self._processedDir, exist_ok=True)
        os.makedirs(self._failedDir, exist_ok=True)

    def processPendingCommands(self):
        for fileName in self._listPendingCommandFiles():
            self._processCommandFile(fileName)

    def _listPendingCommandFiles(self):
        names = [n for n in os.listdir(self._commandsDir) if n.endswith(".json")]
        names.sort()
        return names

    def _processCommandFile(self, fileName):
        path = os.path.join(self._commandsDir, fileName)

        try:
            with open(path, "r", encoding="utf-8-sig") as f:
                command = json.load(f)
        except Exception as exc:
            print(f"[RuntimeCommand] Komut okunamadi: {fileName} ({exc})")
            self._moveTo(path, self._failedDir, fileName)
            return

        commandName = str(command.get("command") or "")
        handler = self._handlers.get(commandName)
        if handler is None:
            print(f"[RuntimeCommand] Bilinmeyen komut: '{commandName}' ({fileName})")
            self._moveTo(path, self._failedDir, fileName)
            return

        try:
            handler(command)
        except Exception as exc:
            print(f"[RuntimeCommand] Komut hatasi: '{commandName}' ({fileName}) -> {exc}")
            self._moveTo(path, self._failedDir, fileName)
            return

        print(f"[RuntimeCommand] Uygulandi: '{commandName}' ({fileName})")
        self._moveTo(path, self._processedDir, fileName)

    def _moveTo(self, path, targetDir, fileName):
        try:
            destination = os.path.join(targetDir, fileName)
            if os.path.exists(destination):
                os.remove(destination)
            shutil.move(path, destination)
        except Exception as exc:
            print(f"[RuntimeCommand] Dosya tasinamadi: {fileName} ({exc})")

    # ---- command handlers ---------------------------------------------

    def _handleLoadBundle(self, command):
        """Bundle + view path'lerini inputs/input.json'a yazip default.py'yi
        yeniden calistirir; boylece C#'in urettigi yeni data mevcut default.py
        akisi (stage2/stage3) uzerinden panellere basilir."""
        bundlePath = command.get("bundlePath")
        viewPath = command.get("viewPath")
        if not bundlePath:
            raise ValueError("load_bundle: 'bundlePath' zorunlu")

        inputConfig = {"bundle": bundlePath}
        if viewPath:
            inputConfig["view"] = viewPath

        with open(self._inputConfigPath, "w", encoding="utf-8") as f:
            json.dump(inputConfig, f, ensure_ascii=False, indent=2)

        self._gm.scriptPanel.runScriptFile("default.py")

    def _handleClearPanel(self, command):
        """Tek bir paneldeki tum data/level'lari siler ve plotu bos halde
        yeniden cizer (panel.dataList temizlenip drawPanelData tekrar cagrilir)."""
        panelId = command.get("panelId")
        if panelId is None:
            raise ValueError("clear_panel: 'panelId' zorunlu")

        pm = self._gm.panelManager
        panel = pm.getPanel(panelId)
        if panel is None:
            raise ValueError(f"clear_panel: panel bulunamadi (panelId={panelId})")

        panel.deleteAllData()
        panel.deleteAllLevels()
        pm.drawPanelData(panel.id)

    def _handleClearAllPanels(self, command):
        """Tum panellerdeki data/level'lari siler, panelleri kendisi silmez
        (bkz. pm.deleteAllPanels() - o panel kabuklarini da kaldirir)."""
        pm = self._gm.panelManager
        for panel in list(pm.iterateAllPanels()):
            panel.deleteAllData()
            panel.deleteAllLevels()
            pm.drawPanelData(panel.id)

    def _handleReloadCurrent(self, command):
        """inputs/input.json'u degistirmeden default.py'yi tekrar calistirir.
        C# ayni bundle/view path'lerini disk uzerinde guncelleyip (ayni dosya
        adiyla) tekrar cizdirmek istedigindeki senaryo icin."""
        self._gm.scriptPanel.runScriptFile("default.py")

    def _handleAddSeriesFromBundle(self, command):
        """Son yuklenen bundle'daki (gm.currentPreparedData) hazir bir seriyi
        (indikator/OHLCV/signalSteps) hedef panele ekler ve paneli yeniden cizer."""
        panelId = command.get("panelId")
        source = str(command.get("source") or "").lower()
        name = command.get("name")

        if panelId is None:
            raise ValueError("add_series_from_bundle: 'panelId' zorunlu")
        if not source:
            raise ValueError("add_series_from_bundle: 'source' zorunlu")

        data = getattr(self._gm, "currentPreparedData", None)
        if data is None:
            raise ValueError("add_series_from_bundle: once bir bundle yuklenmis olmali (gm.currentPreparedData yok)")

        pm = self._gm.panelManager
        panel = pm.getPanel(panelId)
        if panel is None:
            raise ValueError(f"add_series_from_bundle: panel bulunamadi (panelId={panelId})")

        intraday = bool(data.meta.get("intraday", True))

        if source == "indicator":
            if not name:
                raise ValueError("add_series_from_bundle: source='indicator' icin 'name' zorunlu")
            ys = None
            for indicatorName, values in zip(data.indicatorNames, data.indicatorValues):
                if indicatorName == name:
                    ys = values
                    break
            if ys is None:
                raise ValueError(f"add_series_from_bundle: indikator bulunamadi ({name})")
            label = str(name)
        elif source in ("signalsteps", "signal_steps"):
            ys = data.signalSteps
            label = str(name) if name else "Signal Step"
        elif source in ("open", "high", "low", "close", "volume", "size"):
            ys = getattr(data, source)
            label = str(name) if name else source.title()
        else:
            raise ValueError(f"add_series_from_bundle: bilinmeyen source '{source}'")

        dataId = command.get("dataId")
        if dataId is None:
            existingIds = [d.id for d in panel.iterateAllData()]
            dataId = (max(existingIds) + 1) if existingIds else 1

        panel.addData(int(dataId), label, "line", data.xs, ys,
                      timestamps=data.timestamps, intraday=intraday)
        pm.drawPanelData(panel.id)

    def _handleShutdown(self, command):
        """DearPyGuiDataPlotter uygulamasini nazikce kapatir. C#'in StopPlotter()
        cagrisi once bunu dener, cevap gelmezse process'i kill eder.
        gm.requestShutdown() dpg.stop_dearpygui()'yi cagirir VE render()'in
        kalan frame'lerde split_frame() gerektiren islemleri atlamasi icin
        GuiManager'in kendi flag'ini set eder (bkz. guiManager.py)."""
        self._gm.requestShutdown()
