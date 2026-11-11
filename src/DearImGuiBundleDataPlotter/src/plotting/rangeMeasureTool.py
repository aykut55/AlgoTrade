import csv
import os
from datetime import datetime, date, time as dtime, timezone

import numpy as np
from imgui_bundle import imgui, implot


class RangeMeasureTool:
    """Plot uzerinde iki bar (A/B) secip aralarindaki farki olcen ortak arac.

    Swing Controls'taki olcumden kopyalanarak genellestirildi. PanelManager
    her panelin begin_plot/end_plot'u ACIKKEN drawInPlot() ve handlePlotClick()
    cagirir; tum paneller ayni bar-index X eksenini paylastigi icin A/B
    cizgileri her panelde gorunur. Sonuclari MeasurePanel gosterir.

    Swing modunda Swing Controls kendi olcumunu kullanir; o sirada bu arac
    setSuspended(True) ile tamamen pasiftir (cizim/tiklama/klavye yok)."""

    COLOR_A = (0.25, 0.85, 1.0, 0.95)
    COLOR_B = (1.0, 0.7, 0.2, 0.95)
    # Swing Controls'un kendi cizgileri 710001/710002 kullanir; cakismasin.
    LINE_ID_A = 720001
    LINE_ID_B = 720002

    # Save Measure: analiz icin gecici kayit dosyasi (repo koku/outputs altinda;
    # outputs yeni kosumda temizlenebilir, kalicilik beklenmez).
    REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                             "..", "..", "..", ".."))
    MEASUREMENTS_FILE = os.path.join(REPO_ROOT, "outputs", "measurements",
                                     "range_measurements.txt")
    # Ayni kayitlarin analiz icin tek-satir CSV hali (';' ayirici, UTF-8 BOM -
    # swing segments.csv ile ayni kural; Excel/pandas dogrudan acar).
    MEASUREMENTS_CSV = os.path.splitext(MEASUREMENTS_FILE)[0] + ".csv"
    CSV_HEADER = ["SavedAt", "Source", "Bundle", "Series",
                  "StartBar", "StartTime", "StartClose",
                  "EndBar", "EndTime", "EndClose",
                  "Bars", "ElapsedSeconds", "Elapsed", "Difference", "ChangePercent"]

    def __init__(self, panelManager):
        self._pm = panelManager
        # GuiManager baglar: o an yuklu bundle'in yolunu dondurur (kayit basligi icin).
        self.bundlePathProvider = None
        self.suspended = False
        self.enabled = False
        self.startIndex = None
        self.endIndex = None
        self.sourcePanelId = None
        self.status = "Measure modunu acip plot uzerinde A ve B barlarini secin."
        self._enabledListeners = []

    # ------------------------------------------------------------ durum
    def addEnabledListener(self, callback):
        """Mod acildiginda cagrilir (orn. Measure panelini one getirmek icin)."""
        self._enabledListeners.append(callback)

    def setEnabled(self, enabled):
        enabled = bool(enabled)
        if enabled == self.enabled:
            return
        self.enabled = enabled
        if enabled:
            self.startIndex = None
            self.endIndex = None
            self.sourcePanelId = None
            self.status = "Range measurement: click the first bar (A)."
            for callback in self._enabledListeners:
                callback()
        else:
            self.status = "Range measurement mode closed; lines were preserved."

    def toggle(self):
        self.setEnabled(not self.enabled)

    def setSuspended(self, suspended):
        """Swing modunda True: olcum temizlenir ve arac pasif kalir."""
        self.suspended = bool(suspended)
        if self.suspended:
            self.clear()

    def clear(self):
        self.enabled = False
        self.startIndex = None
        self.endIndex = None
        self.sourcePanelId = None
        self.status = "Range measurement cleared."

    def reset(self):
        """Veri yeniden yuklendiginde (PanelManager.deleteAllPanels) cagrilir."""
        self.clear()
        self.status = "Measure modunu acip plot uzerinde A ve B barlarini secin."

    def selectBar(self, index, panelId=None):
        if self.startIndex is None or self.endIndex is not None:
            self.startIndex = int(index)
            self.endIndex = None
            self.sourcePanelId = panelId
            self.status = f"Range A selected: bar={index}. Click the second bar (B)."
            return
        self.endIndex = int(index)
        self.status = (f"Range selected: bar {self.startIndex} -> {index}. "
                       "Click again to start a new measurement.")

    def handleKeyboard(self):
        """M: modu ac/kapa, Esc: modu kapat (cizgiler korunur)."""
        io = imgui.get_io()
        if self.suspended or io.want_text_input:
            return
        if imgui.is_key_pressed(imgui.Key.m, repeat=False) and not io.key_ctrl:
            self.toggle()
        elif imgui.is_key_pressed(imgui.Key.escape, repeat=False) and self.enabled:
            self.setEnabled(False)

    # ----------------------------------------------------- plot icinde
    def drawInPlot(self, panelId):
        if self.suspended:
            return
        if self.startIndex is not None:
            implot.drag_line_x(self.LINE_ID_A, float(self.startIndex), self.COLOR_A, 2,
                               implot.DragToolFlags_.no_inputs.value)
        if self.endIndex is not None:
            implot.drag_line_x(self.LINE_ID_B, float(self.endIndex), self.COLOR_B, 2,
                               implot.DragToolFlags_.no_inputs.value)

    def handlePlotClick(self, panelId, index, clicked):
        if self.suspended or not self.enabled or not clicked or index < 0:
            return
        totalBars = self._totalBars()
        if totalBars and index >= totalBars:
            return
        self.selectBar(index, panelId)

    # ----------------------------------------------------------- veri
    @staticmethod
    def _array(data, name):
        full = getattr(data, "_full" + name[:1].upper() + name[1:], None)
        if full is not None and len(full):
            return full
        return getattr(data, name, None)

    def _positionOf(self, data, index):
        """Bar index'inin serideki konumu (xs bar index degilse searchsorted)."""
        xs = self._array(data, "xs")
        if xs is None or len(xs) == 0:
            return None
        if 0 <= index < len(xs) and abs(float(xs[index]) - index) < 0.5:
            return index
        arr = np.asarray(xs, dtype=np.float64)
        pos = int(np.searchsorted(arr, index))
        for cand in (pos, pos - 1):
            if 0 <= cand < len(arr) and abs(arr[cand] - index) < 0.5:
                return cand
        return None

    def _valueAt(self, data, index, field=None):
        pos = self._positionOf(data, index)
        if pos is None:
            return None
        if field is None:
            field = "close" if data.dataType == "candle" else "ys"
        values = self._array(data, field)
        if values is None or pos >= len(values):
            return None
        try:
            return float(values[pos])
        except (TypeError, ValueError):
            return None

    def _timestampAt(self, data, index):
        pos = self._positionOf(data, index)
        stamps = self._array(data, "timestamps")
        if pos is None or stamps is None or pos >= len(stamps):
            return None
        return stamps[pos]

    def _iterPanels(self):
        for panelId in self._pm.getPanelOrder():
            panel = self._pm.getPanel(panelId)
            if panel is not None:
                yield panelId, panel

    def _totalBars(self):
        total = 0
        for _, panel in self._iterPanels():
            for data in panel.dataList:
                xs = self._array(data, "xs")
                if xs is not None and len(xs):
                    total = max(total, int(float(xs[-1])) + 1)
        return total

    def pickPriceData(self):
        """Close metrikleri icin kaynak seri: once olcumun basladigi paneldeki
        mum serisi, sonra herhangi bir paneldeki ilk mum serisi, en son
        olcumun basladigi paneldeki ilk gorunur seri."""
        panels = list(self._iterPanels())
        if self.sourcePanelId is not None:
            panels.sort(key=lambda item: item[0] != self.sourcePanelId)
        for _, panel in panels:
            for data in panel.dataList:
                if data.dataType == "candle" and data.isVisible:
                    return data
        for panelId, panel in panels:
            if self.sourcePanelId is not None and panelId != self.sourcePanelId:
                continue
            for data in panel.dataList:
                if data.isVisible:
                    return data
        return None

    # -------------------------------------------------------- metrikler
    def computeMetrics(self, data=None):
        """A/B secimine gore metrikleri dict olarak dondurur. data verilmezse
        pickPriceData() kullanilir. Secim yoksa None."""
        if self.startIndex is None:
            return None
        data = data if data is not None else self.pickPriceData()
        startValue = self._valueAt(data, self.startIndex) if data is not None else None
        startTime = self._timestampAt(data, self.startIndex) if data is not None else None
        result = {
            "seriesName": getattr(data, "name", "") if data is not None else "",
            "startIndex": self.startIndex, "endIndex": self.endIndex,
            "startValue": startValue, "startTime": startTime,
            "endValue": None, "endTime": None, "bars": None,
            "elapsedSeconds": None, "difference": None, "percent": None,
        }
        if self.endIndex is None or data is None:
            return result
        endValue = self._valueAt(data, self.endIndex)
        endTime = self._timestampAt(data, self.endIndex)
        result["endValue"] = endValue
        result["endTime"] = endTime
        result["bars"] = abs(self.endIndex - self.startIndex)
        elapsed = self.elapsedSeconds(startTime, endTime)
        result["elapsedSeconds"] = abs(elapsed) if elapsed is not None else None
        if startValue is not None and endValue is not None:
            difference = endValue - startValue
            result["difference"] = difference
            result["percent"] = difference / startValue * 100.0 if startValue else None
        return result

    # ------------------------------------------------------ yardimcilar
    @staticmethod
    def toDatetime(value):
        if value is None:
            return None
        if isinstance(value, datetime):
            return value
        if isinstance(value, np.datetime64):
            return value.astype("datetime64[us]").astype(datetime)
        if isinstance(value, date):
            return datetime.combine(value, dtime())
        if isinstance(value, (tuple, list)) and len(value) == 2:
            day, clock = value
            dayValue = RangeMeasureTool.toDatetime(day)
            if dayValue is None:
                return None
            if isinstance(clock, dtime):
                return datetime.combine(dayValue.date(), clock)
            try:
                return datetime.fromisoformat(f"{dayValue.date().isoformat()}T{clock}")
            except (TypeError, ValueError):
                return dayValue
        if isinstance(value, (int, float, np.integer, np.floating)):
            try:
                return datetime.fromtimestamp(float(value), tz=timezone.utc)
            except (OverflowError, OSError, ValueError):
                return None
        try:
            return datetime.fromisoformat(str(value).replace("Z", "+00:00"))
        except (TypeError, ValueError):
            return None

    @staticmethod
    def elapsedSeconds(startValue, endValue):
        start = RangeMeasureTool.toDatetime(startValue)
        end = RangeMeasureTool.toDatetime(endValue)
        if start is None or end is None:
            return None
        try:
            return (end - start).total_seconds()
        except TypeError:
            # naive/aware karisimi
            return (end.replace(tzinfo=None) - start.replace(tzinfo=None)).total_seconds()

    @staticmethod
    def formatDuration(seconds):
        if seconds is None:
            return "-"
        seconds = int(seconds)
        days, seconds = divmod(seconds, 86400)
        hours, seconds = divmod(seconds, 3600)
        minutes, seconds = divmod(seconds, 60)
        prefix = f"{days}d " if days else ""
        return f"{prefix}{hours:02d}:{minutes:02d}:{seconds:02d}"

    @staticmethod
    def formatTime(value):
        """Zamani her kaynaktan (datetime, ISO 'T'li string, tuple...) ayni
        bicimde yazar: 2026-10-21 20:15:00."""
        if value is None:
            return "-"
        parsed = RangeMeasureTool.toDatetime(value)
        if parsed is None:
            return str(value)
        return str(parsed.replace(tzinfo=None))

    @staticmethod
    def metricsRows(metrics):
        """Olcumun satirlari: [(etiket, deger, renk|None), ...]. Hem panelde
        gosterim (renderMetrics) hem Copy Measure metni (formatMetricsText)
        bunu kullanir - ikisi her zaman ayni gorunur."""
        if metrics is None:
            return []

        def num(value, fmt=".6g"):
            return "-" if value is None else format(value, fmt)

        rows = []
        if metrics["seriesName"]:
            rows.append(("Series", str(metrics["seriesName"]), None))
        rows.append(("A", f"Bar {metrics['startIndex']} | "
                          f"{RangeMeasureTool.formatTime(metrics['startTime'])} | "
                          f"Close {num(metrics['startValue'])}", RangeMeasureTool.COLOR_A))
        if metrics["endIndex"] is None:
            rows.append(("B", "-", RangeMeasureTool.COLOR_B))
            return rows
        rows.append(("B", f"Bar {metrics['endIndex']} | "
                          f"{RangeMeasureTool.formatTime(metrics['endTime'])} | "
                          f"Close {num(metrics['endValue'])}", RangeMeasureTool.COLOR_B))
        difference = metrics["difference"]
        signColor = None
        if difference is not None:
            signColor = ((0.25, 0.9, 0.35, 1.0) if difference >= 0
                         else (1.0, 0.35, 0.3, 1.0))
        percent = metrics["percent"]
        rows.append(("Bars", str(metrics["bars"]), None))
        rows.append(("Time", RangeMeasureTool.formatDuration(metrics["elapsedSeconds"]), None))
        rows.append(("Difference", num(difference, "+.6g"), signColor))
        rows.append(("Change", "-" if percent is None else f"{percent:+.3f}%", signColor))
        return rows

    @staticmethod
    def formatMetricsText(metrics):
        """Copy Measure: olcumu txt'ye yapistirilabilir duz metin olarak dondurur."""
        rows = RangeMeasureTool.metricsRows(metrics)
        if not rows:
            return ""
        lines = ["Range Measurement"]
        lines += [f"{label:<11}: {value}" for label, value, _ in rows]
        return "\n".join(lines) + "\n"

    @staticmethod
    def _csvRow(metrics, stamp, source, bundleName):
        def num(value, fmt=".10g"):
            return "" if value is None else format(value, fmt)

        elapsed = metrics["elapsedSeconds"]
        return [
            stamp, source, bundleName, metrics["seriesName"] or "",
            metrics["startIndex"], RangeMeasureTool.formatTime(metrics["startTime"]),
            num(metrics["startValue"]),
            metrics["endIndex"], RangeMeasureTool.formatTime(metrics["endTime"]),
            num(metrics["endValue"]),
            metrics["bars"], "" if elapsed is None else int(elapsed),
            RangeMeasureTool.formatDuration(elapsed),
            num(metrics["difference"]), num(metrics["percent"], ".6f"),
        ]

    def saveMeasurement(self, metrics, source):
        """Save Measure: olcumu iki dosyaya append eder -
        MEASUREMENTS_FILE (.txt, okunur blok: Copy Measure metni + baslik) ve
        MEASUREMENTS_CSV (.csv, her olcum tek satir, analiz icin). Basliga kayit
        zamani, kaynak ekran ve bundle adi yazilir. (ok, mesaj) dondurur;
        hata olursa plotter'i dusurmez, mesajla bildirir."""
        if metrics is None or metrics.get("endIndex") is None:
            return False, "Save Measure: select both A and B first."
        bundleName = ""
        if self.bundlePathProvider is not None:
            try:
                bundlePath = self.bundlePathProvider()
                bundleName = os.path.basename(bundlePath) if bundlePath else ""
            except Exception:
                bundleName = ""
        stamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        header = " | ".join(part for part in (stamp, source, bundleName) if part)
        text = (f"=== {header} ===\n"
                f"{self.formatMetricsText(metrics)}"
                f"{'-' * 47}\n")
        path = self.MEASUREMENTS_FILE
        csvPath = self.MEASUREMENTS_CSV
        try:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "a", encoding="utf-8", newline="\n") as stream:
                stream.write(text)
            # Yeni/bos dosyada BOM + baslik satiri; sonrakilerde sadece satir.
            isNew = not os.path.isfile(csvPath) or os.path.getsize(csvPath) == 0
            with open(csvPath, "a", encoding="utf-8-sig" if isNew else "utf-8",
                      newline="") as stream:
                writer = csv.writer(stream, delimiter=";")
                if isNew:
                    writer.writerow(self.CSV_HEADER)
                writer.writerow(self._csvRow(metrics, stamp, source, bundleName))
        except OSError as exc:
            return False, f"Save Measure failed: {exc}"
        folder = os.path.relpath(os.path.dirname(path), self.REPO_ROOT).replace("\\", "/")
        return True, (f"Saved -> {folder}/{os.path.basename(path)} + "
                      f"{os.path.basename(csvPath)}")

    @staticmethod
    def renderMetrics(metrics, tableId="##rangeMeasureRows"):
        """Olcumu Copy Measure metniyle ayni duzende (etiket : deger) gosterir.
        Etiketler hizali dursun diye iki kolonlu tablo kullanilir."""
        rows = RangeMeasureTool.metricsRows(metrics)
        if not rows:
            return
        imgui.text("Range Measurement")
        flags = imgui.TableFlags_.sizing_fixed_fit.value
        if not imgui.begin_table(tableId, 2, flags):
            return
        for label, value, color in rows:
            imgui.table_next_row()
            imgui.table_next_column()
            imgui.text(label)
            imgui.table_next_column()
            if color is not None:
                imgui.text_colored(color, f": {value}")
            else:
                imgui.text(f": {value}")
        imgui.end_table()

    # ---------------------------------------------------------- cizim UI
    def renderMeasurementSection(self, compact=False):
        """MeasurePanel'in gosterdigi olcum blogu."""
        if self.suspended:
            imgui.text_disabled("Swing modunda olcum Swing Controls'ta yapilir.")
            return None
        if self.enabled:
            if imgui.button("Measuring: click plot", (210, 0)):
                self.setEnabled(False)
        else:
            if imgui.button("Measure Range (M)", (210, 0)):
                self.setEnabled(True)
        imgui.same_line()
        if imgui.button("Clear Measure", (140, 0)):
            self.clear()

        metrics = self.computeMetrics()
        imgui.same_line()
        imgui.begin_disabled(metrics is None)
        if imgui.button("Copy Measure", (140, 0)):
            imgui.set_clipboard_text(self.formatMetricsText(metrics))
            self.status = "Measurement copied to clipboard."
        imgui.end_disabled()
        imgui.same_line()
        imgui.begin_disabled(metrics is None or metrics["endIndex"] is None)
        if imgui.button("Save Measure", (140, 0)):
            _, self.status = self.saveMeasurement(metrics, "Measure Panel")
        imgui.end_disabled()
        if not compact:
            imgui.text_wrapped(self.status)

        if metrics is None:
            imgui.text_disabled("Range Measurement: select A and B on the plot")
            return metrics

        imgui.separator()
        self.renderMetrics(metrics)
        if metrics["endIndex"] is None:
            imgui.text_disabled("Click the second bar (B).")
        elif compact:
            imgui.text_disabled("The next plot click starts a new measurement.")
        return metrics
