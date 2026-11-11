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
                  "Bars", "ElapsedSeconds", "Elapsed", "Difference", "ChangePercent",
                  "High", "HighBar", "HighTime", "Low", "LowBar", "LowTime",
                  "Range", "RangePercent", "MaxRunup", "MaxRunupPercent",
                  "MaxDrawdown", "MaxDrawdownPercent", "Mfe", "Mae",
                  "Efficiency", "AvgPerBar"]

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
            startPos = self._positionOf(data, self.startIndex)
            endPos = self._positionOf(data, self.endIndex)
            if startPos is not None and endPos is not None:
                isCandle = data.dataType == "candle"
                closes = self._array(data, "close" if isCandle else "ys")
                result.update(self.rangeStats(
                    self._array(data, "xs"),
                    self._array(data, "high") if isCandle else closes,
                    self._array(data, "low") if isCandle else closes,
                    closes, self._array(data, "timestamps"),
                    startPos, endPos, startValue, difference))
        return result

    @staticmethod
    def rangeStats(xs, highs, lows, closes, timestamps, startPos, endPos,
                   startValue, difference):
        """A ile B arasindaki (iki uc dahil) tum barlara bakan aralik istatistikleri.
        Pozisyonlar seri dizilerindeki konumlardir; A, B'den sonra secilmis olsa
        da aralik kronolojik alinir. Referans fiyat A'nin Close'udur.
        Veri eksikse bos dict dondurur (panel o satirlari gostermez)."""
        try:
            lo, hi = sorted((int(startPos), int(endPos)))
            highArr = np.asarray(highs[lo:hi + 1], dtype=np.float64)
            lowArr = np.asarray(lows[lo:hi + 1], dtype=np.float64)
            closeArr = np.asarray(closes[lo:hi + 1], dtype=np.float64)
        except (TypeError, ValueError, IndexError):
            return {}
        if len(highArr) == 0 or len(lowArr) != len(highArr) or len(closeArr) != len(highArr):
            return {}
        if np.isnan(highArr).all() or np.isnan(lowArr).all():
            return {}

        highOffset = int(np.nanargmax(highArr))
        lowOffset = int(np.nanargmin(lowArr))
        high = float(highArr[highOffset])
        low = float(lowArr[lowOffset])

        def barAt(offset):
            pos = lo + offset
            try:
                return int(round(float(xs[pos]))) if xs is not None else pos
            except (TypeError, ValueError, IndexError):
                return pos

        def timeAt(offset):
            pos = lo + offset
            if timestamps is None or pos >= len(timestamps):
                return None
            return timestamps[pos]

        def pct(value):
            return value / startValue * 100.0 if startValue else None

        maxRunup = high - startValue
        maxDrawdown = low - startValue
        # Swing segmentindeki tanimla ayni: harekete gore en iyi/en kotu nokta.
        if difference >= 0:
            mfe, mae = high - startValue, low - startValue
        else:
            mfe, mae = startValue - low, startValue - high
        path = float(np.nansum(np.abs(np.diff(closeArr)))) if len(closeArr) > 1 else 0.0
        bars = hi - lo
        return {
            "high": high, "highIndex": barAt(highOffset), "highTime": timeAt(highOffset),
            "low": low, "lowIndex": barAt(lowOffset), "lowTime": timeAt(lowOffset),
            "range": high - low, "rangePercent": pct(high - low),
            "maxRunup": maxRunup, "maxRunupPercent": pct(maxRunup),
            "maxDrawdown": maxDrawdown, "maxDrawdownPercent": pct(maxDrawdown),
            "mfe": mfe, "mae": mae,
            "efficiency": abs(difference) / path if path else None,
            "avgPerBar": difference / bars if bars else None,
        }

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

        # Aralik istatistikleri (A..B arasindaki tum barlar). Swing Controls
        # bunlari hesaplamadigi icin anahtarlar yoksa satirlar eklenmez.
        if metrics.get("high") is None:
            return rows

        def withPct(value, percentValue, signed=False):
            text = num(value, "+.6g" if signed else ".6g")
            if percentValue is None:
                return text
            return f"{text} ({percentValue:+.3f}%)" if signed else f"{text} ({percentValue:.3f}%)"

        green, red = (0.25, 0.9, 0.35, 1.0), (1.0, 0.35, 0.3, 1.0)
        rows.append(("High", f"{num(metrics['high'])} | Bar {metrics['highIndex']} | "
                             f"{RangeMeasureTool.formatTime(metrics['highTime'])}", None))
        rows.append(("Low", f"{num(metrics['low'])} | Bar {metrics['lowIndex']} | "
                            f"{RangeMeasureTool.formatTime(metrics['lowTime'])}", None))
        rows.append(("Range", withPct(metrics["range"], metrics["rangePercent"]), None))
        rows.append(("Max Runup", withPct(metrics["maxRunup"], metrics["maxRunupPercent"], True),
                     green))
        rows.append(("Max DD", withPct(metrics["maxDrawdown"], metrics["maxDrawdownPercent"], True),
                     red))
        rows.append(("MFE / MAE", f"{num(metrics['mfe'], '+.6g')} / {num(metrics['mae'], '+.6g')}",
                     None))
        efficiency = metrics.get("efficiency")
        rows.append(("Efficiency", "-" if efficiency is None else f"{efficiency:.4f}", None))
        rows.append(("Avg / Bar", num(metrics.get("avgPerBar"), "+.6g"), None))
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
            # Aralik istatistikleri - Swing Controls'tan gelen kayitlarda bos kalir.
            num(metrics.get("high")), metrics.get("highIndex", ""),
            RangeMeasureTool.formatTime(metrics["highTime"]) if metrics.get("highTime") is not None else "",
            num(metrics.get("low")), metrics.get("lowIndex", ""),
            RangeMeasureTool.formatTime(metrics["lowTime"]) if metrics.get("lowTime") is not None else "",
            num(metrics.get("range")), num(metrics.get("rangePercent"), ".6f"),
            num(metrics.get("maxRunup")), num(metrics.get("maxRunupPercent"), ".6f"),
            num(metrics.get("maxDrawdown")), num(metrics.get("maxDrawdownPercent"), ".6f"),
            num(metrics.get("mfe")), num(metrics.get("mae")),
            num(metrics.get("efficiency"), ".6f"), num(metrics.get("avgPerBar")),
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
            # Eski (daha az sutunlu) basliga sahip bir dosya varsa ustune satir
            # eklemek sutunlari kaydirir; onu kenara alip yeni dosya baslatilir.
            if os.path.isfile(csvPath) and os.path.getsize(csvPath) > 0:
                with open(csvPath, "r", encoding="utf-8-sig") as stream:
                    existingHeader = stream.readline().rstrip("\r\n").split(";")
                if existingHeader != self.CSV_HEADER:
                    oldStamp = datetime.now().strftime("%Y%m%d_%H%M%S")
                    os.replace(csvPath, os.path.splitext(csvPath)[0] + f"_old_{oldStamp}.csv")
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
            RangeMeasureTool._helpTooltip(label)
            imgui.table_next_column()
            if color is not None:
                imgui.text_colored(color, f": {value}")
            else:
                imgui.text(f": {value}")
            RangeMeasureTool._helpTooltip(label)
        imgui.end_table()
        RangeMeasureTool._renderHelpSection(tableId, [label for label, _, _ in rows])

    # Etiket aciklamalari: satirin uzerine gelince ipucu olarak, "Aciklamalar"
    # bolumunde tablo olarak gosterilir. Varsayilan ImGui fontunda Turkce
    # karakter (ı, ş, ğ...) olmadigi icin metinler ASCII yazildi.
    METRIC_HELP = {
        "Series": "Olcumun yapildigi seri (olcumun basladigi paneldeki mum serisi).",
        "A": "Ilk secilen bar: bar numarasi, zamani ve Close degeri. "
             "Tum yuzdelerin referansi A'nin Close'udur.",
        "B": "Ikinci secilen bar: bar numarasi, zamani ve Close degeri.",
        "Bars": "A ile B arasindaki bar sayisi.",
        "Time": "A ile B arasinda gecen sure. Seans aralari ve hafta sonu "
                "yuzunden bar sayisindan farkli olabilir.",
        "Difference": "B'nin Close'u eksi A'nin Close'u (puan).",
        "Change": "Difference'in A'nin Close'una gore yuzdesi.",
        "High": "Araliktaki en yuksek High; hangi barda ve hangi zamanda oldugu.",
        "Low": "Araliktaki en dusuk Low; hangi barda ve hangi zamanda oldugu.",
        "Range": "High - Low, yani araliktaki toplam salinim (puan ve A'ya gore %).",
        "Max Runup": "A'dan sonra fiyatin A'nin Close'unun en fazla ne kadar "
                     "ustune ciktigi.",
        "Max DD": "Max Drawdown: A'dan sonra fiyatin A'nin Close'unun en fazla "
                  "ne kadar altina indigi.",
        "MFE / MAE": "Hareket yonune gore en iyi (MFE) ve en kotu (MAE) nokta. "
                     "Yukseliste MFE en yuksek nokta, duseste en dusuk nokta.",
        "Efficiency": "|B - A| / bar bar Close degisimlerinin toplami. 1'e yakinsa "
                      "duz bir hareket, 0'a yakinsa zikzakli bir hareket.",
        "Avg / Bar": "Difference / bar sayisi, bar basina ortalama degisim.",
    }

    @staticmethod
    def _helpTooltip(label):
        """Son cizilen ogenin (etiket ya da deger) uzerine gelince aciklama."""
        text = RangeMeasureTool.METRIC_HELP.get(label)
        if not text or not imgui.is_item_hovered():
            return
        imgui.begin_tooltip()
        imgui.push_text_wrap_pos(imgui.get_font_size() * 28.0)
        imgui.text_colored((1.0, 0.85, 0.4, 1.0), label)
        imgui.text_unformatted(text)
        imgui.pop_text_wrap_pos()
        imgui.end_tooltip()

    @staticmethod
    def _renderHelpSection(tableId, labels):
        """Olcumun altinda varsayilan kapali 'Aciklamalar' bolumu: o an
        gosterilen satirlarin aciklamalari iki kolonlu tablo olarak."""
        if not imgui.collapsing_header(f"Aciklamalar{tableId}_help"):
            return
        flags = (imgui.TableFlags_.borders_inner_h.value |
                 imgui.TableFlags_.sizing_stretch_prop.value)
        if not imgui.begin_table(f"{tableId}_helpTable", 2, flags):
            return
        imgui.table_setup_column("Deger", imgui.TableColumnFlags_.width_fixed.value)
        imgui.table_setup_column("Anlami", imgui.TableColumnFlags_.width_stretch.value)
        for label in labels:
            text = RangeMeasureTool.METRIC_HELP.get(label)
            if not text:
                continue
            imgui.table_next_row()
            imgui.table_next_column()
            imgui.text(label)
            imgui.table_next_column()
            imgui.text_wrapped(text)
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
