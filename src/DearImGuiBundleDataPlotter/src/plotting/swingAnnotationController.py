from imgui_bundle import imgui, implot
from datetime import datetime, timezone

from .swingAnnotationStore import SwingAnnotationStore
from .rangeMeasureTool import RangeMeasureTool


class SwingAnnotationController:
    """Swing etiketleme gorunumu icin bar-penceresi gezinme denetleyicisi."""

    def __init__(self, panelManager):
        self._pm = panelManager
        self._active = False
        self._open = True
        self._panelId = None
        self._totalBars = 0
        self._start = 0
        self._width = 2000
        self._step = 500
        self._minWidth = 100
        self._maxWidth = 20000
        self._presets = []
        self._presetIndex = 0
        self._customPreset = 2000
        self._gotoIndex = 0
        self._status = ""
        self._initialGeometryPending = True
        self._data = None
        self._annotations = {}
        self._hoveredBarIndex = None
        self._hoveredAnnotationIndex = None
        self._undoStack = []
        self._redoStack = []
        self._dockWindow = None
        self._store = None
        self._deleteAllPending = False
        self._selectedSegmentEndIndex = None
        self._showManualMarkers = True
        self._showZigZagMarkers = True
        self._measureEnabled = False
        self._measureStartIndex = None
        self._measureEndIndex = None

    def setDockWindow(self, dockWindow):
        self._dockWindow = dockWindow
        if self._dockWindow is not None:
            self._dockWindow.is_visible = self._active

    def getVisible(self):
        return bool(self._active and (
            self._dockWindow.is_visible if self._dockWindow else self._open))

    def deactivate(self):
        self._active = False
        self._panelId = None
        if self._dockWindow is not None:
            self._dockWindow.is_visible = False

    def configure(self, view, panelId, totalBars, preparedData=None):
        if not isinstance(view, dict) or view.get("mode") != "swing_annotation":
            self.deactivate()
            return

        cfg = view.get("window") or {}
        rendering = view.get("rendering") or {}
        self._showManualMarkers = bool(rendering.get("showManualMarkers", True))
        self._showZigZagMarkers = bool(rendering.get("showZigZagMarkers", True))
        self._panelId = panelId
        self._data = preparedData
        self._store = SwingAnnotationStore(view.get("session"), preparedData)
        self._annotations, savedState = self._store.load()
        self._undoStack = []
        self._redoStack = []
        self._deleteAllPending = False
        self._selectedSegmentEndIndex = None
        self._hoveredAnnotationIndex = None
        self._measureEnabled = False
        self._measureStartIndex = None
        self._measureEndIndex = None
        self._totalBars = max(0, int(totalBars or 0))
        self._minWidth = max(1, int(cfg.get("minimumWidthBars", 100)))
        self._maxWidth = max(self._minWidth, int(cfg.get("maximumWidthBars", 20000)))
        self._maxWidth = min(self._maxWidth, max(1, self._totalBars))
        self._minWidth = min(self._minWidth, self._maxWidth)
        self._width = self._clampWidth(cfg.get("initialWidthBars", 2000))
        self._step = max(1, min(int(cfg.get("navigationStepBars", 500)),
                                max(1, self._width)))
        self._presets = sorted({self._clampWidth(v) for v in cfg.get("presets", [])})
        if self._width not in self._presets:
            self._presets.append(self._width)
            self._presets.sort()
        self._presetIndex = self._presets.index(self._width)
        self._customPreset = self._width
        self._gotoIndex = self._start

        mode = str(cfg.get("initialViewMode", "FitToScreen (Ultra)"))
        savedWidth = savedState.get("windowWidth")
        if savedWidth is not None:
            self._width = self._clampWidth(savedWidth)
        offset = max(0, int(savedState.get("windowStart", cfg.get("initialOffsetBars", 0))))
        self._start = self._lastStart() if mode.startswith("FitToScreen") else self._clampStart(offset)
        self._active = True
        self._open = True
        if self._dockWindow is not None:
            self._dockWindow.is_visible = True
            self._dockWindow.focus_window_at_next_frame = True
        self._initialGeometryPending = True
        self._updateStatus()
        if self._panelId is not None:
            self._pm.addPanelDrawHook(self._panelId, self._drawAnnotationsAndHandleClick)

    def render(self):
        if not self._active:
            return
        if self._dockWindow is not None:
            if not self._dockWindow.is_visible:
                imgui.set_next_window_pos((20.0, 290.0), imgui.Cond_.once.value)
                imgui.set_next_window_size((210.0, 70.0), imgui.Cond_.once.value)
                imgui.begin("Swing Controls Launcher")
                if imgui.button("Open Swing Controls", (-1, 0)):
                    self._dockWindow.is_visible = True
                imgui.end()
            return
        if not self._open:
            imgui.set_next_window_pos((20.0, 290.0), imgui.Cond_.once.value)
            imgui.set_next_window_size((210.0, 70.0), imgui.Cond_.once.value)
            imgui.begin("Swing Controls Launcher")
            if imgui.button("Open Swing Controls", (-1, 0)):
                self._open = True
                self._initialGeometryPending = True
            imgui.end()
            return
        if self._initialGeometryPending:
            imgui.set_next_window_pos((20.0, 290.0), imgui.Cond_.once.value)
            imgui.set_next_window_size((430.0, 350.0), imgui.Cond_.once.value)
            self._initialGeometryPending = False

        expanded, self._open = imgui.begin("Swing Window Controls", self._open)
        if expanded:
            self._renderControls()
        imgui.end()
        self._handleKeyboard()

    def renderDockContent(self):
        if not self._active:
            return
        self._renderControls()
        self._handleKeyboard()

    def _renderControls(self):
        imgui.text(f"Bars: {self._totalBars}")
        imgui.text_wrapped(self._status)
        hoveredText = "-" if self._hoveredBarIndex is None else str(self._hoveredBarIndex)
        imgui.text(f"Target Bar: {hoveredText}  Annotations: {len(self._annotations)}")
        self._renderRangeMeasurement()
        self._renderSegmentSummary()
        if imgui.button("1 - Add Dip", (120, 0)): self._addAnnotationAtHoveredBar("Dip")
        imgui.same_line()
        if imgui.button("2 - Add Tepe", (120, 0)): self._addAnnotationAtHoveredBar("Tepe")
        imgui.same_line()
        if imgui.button("Delete", (100, 0)): self._deleteAnnotationAtHoveredBar()
        if not self._deleteAllPending:
            if imgui.button("Delete All...", (160, 0)):
                self._deleteAllPending = True
        else:
            imgui.text_colored((1.0, 0.45, 0.25, 1.0),
                               f"Delete all {len(self._annotations)} annotations?")
            if imgui.button("Confirm Delete All", (180, 0)):
                self._deleteAllAnnotations()
            imgui.same_line()
            if imgui.button("Cancel", (100, 0)):
                self._deleteAllPending = False
        if imgui.button("Undo (Ctrl+Z)", (160, 0)): self._undo()
        imgui.same_line()
        if imgui.button("Redo (Ctrl+Y)", (160, 0)): self._redo()

        imgui.set_next_item_width(150)
        changed, width = imgui.input_int("Window Width", self._width, 0)
        if changed:
            self._width = self._clampWidth(width)
            self._start = self._clampStart(self._start)
            self._syncPresetIndex()
        imgui.set_next_item_width(150)
        changed, step = imgui.input_int("Navigation Step", self._step, 0)
        if changed: self._step = max(1, min(int(step), max(1, self._width)))
        if self._presets:
            imgui.set_next_item_width(150)
            changed, index = imgui.combo("Width Preset", self._presetIndex,
                                         [str(value) for value in self._presets])
            if changed:
                self._presetIndex = index
                self._width = self._presets[index]
                self._start = self._clampStart(self._start)
                self._applyWindow()
        imgui.set_next_item_width(150)
        changed, value = imgui.input_int("Custom Preset", self._customPreset, 0)
        if changed: self._customPreset = self._clampWidth(value)
        imgui.same_line()
        if imgui.button("Add + Apply", (110, 0)):
            self._width = self._clampWidth(self._customPreset)
            if self._width not in self._presets:
                self._presets.append(self._width)
                self._presets.sort()
            self._presetIndex = self._presets.index(self._width)
            self._start = self._clampStart(self._start)
            self._applyWindow()
        if imgui.button("|< Start", (90, 0)):
            self._start = 0; self._applyWindow()
        imgui.same_line()
        if imgui.button("< Previous", (90, 0)):
            self._start = self._clampStart(self._start - self._step); self._applyWindow()
        imgui.same_line()
        if imgui.button("Next >", (90, 0)):
            self._start = self._clampStart(self._start + self._step); self._applyWindow()
        imgui.same_line()
        if imgui.button("End >|", (90, 0)):
            self._start = self._lastStart(); self._applyWindow()
        if imgui.button("Apply Width / Position", (-1, 0)): self._applyWindow()
        imgui.set_next_item_width(150)
        changed, value = imgui.input_int("Global Bar Index", self._gotoIndex, 0)
        if changed:
            self._gotoIndex = max(0, min(int(value), max(0, self._totalBars - 1)))
        imgui.same_line()
        if imgui.button("Go", (110, 0)):
            self._start = self._clampStart(self._gotoIndex); self._applyWindow()

    def _renderRangeMeasurement(self):
        imgui.separator()
        if self._measureEnabled:
            if imgui.button("Measuring: click plot", (210, 0)):
                self._measureEnabled = False
        else:
            if imgui.button("Measure Range", (210, 0)):
                self._measureEnabled = True
                self._measureStartIndex = None
                self._measureEndIndex = None
                self._status = "Range measurement: click the first bar (A)."
        imgui.same_line()
        if imgui.button("Clear Measure", (140, 0)):
            self._clearMeasurement()
        imgui.same_line()
        imgui.begin_disabled(self._measureStartIndex is None)
        if imgui.button("Copy Measure", (140, 0)):
            self._copyMeasurement()
        imgui.end_disabled()
        imgui.same_line()
        imgui.begin_disabled(self._measureStartIndex is None or self._measureEndIndex is None)
        if imgui.button("Save Measure", (140, 0)):
            self._saveMeasurement()
        imgui.end_disabled()

        metrics = self._measureMetrics()
        if metrics is None:
            imgui.text_disabled("Range Measurement: select A and B on the plot")
            imgui.separator()
            return

        # Measure paneli ve Copy Measure metniyle ayni duzen
        # (bkz. RangeMeasureTool.metricsRows/renderMetrics).
        RangeMeasureTool.renderMetrics(metrics, "##swingMeasureRows")
        if metrics["endIndex"] is None:
            imgui.text_disabled("Click the second bar (B).")
        else:
            imgui.text_disabled("The next plot click starts a new measurement.")
        imgui.separator()

    def _measureMetrics(self):
        """Swing'in kendi A/B secimini RangeMeasureTool.computeMetrics ile ayni
        alanlara sahip dict'e cevirir; secim yoksa None."""
        startIndex = self._measureStartIndex
        if startIndex is None or self._data is None:
            return None
        endIndex = self._measureEndIndex
        startTime = str(self._data.timestamps[startIndex])
        startClose = float(self._data.close[startIndex])
        metrics = {
            "seriesName": self._store.symbol if self._store is not None else "",
            "startIndex": startIndex, "endIndex": endIndex,
            "startValue": startClose, "startTime": startTime,
            "endValue": None, "endTime": None, "bars": None,
            "elapsedSeconds": None, "difference": None, "percent": None,
        }
        if endIndex is not None:
            endTime = str(self._data.timestamps[endIndex])
            endClose = float(self._data.close[endIndex])
            difference = endClose - startClose
            elapsed = self._store._elapsedSeconds(startTime, endTime)
            metrics.update({
                "endValue": endClose, "endTime": endTime,
                "bars": abs(endIndex - startIndex),
                "elapsedSeconds": abs(elapsed) if elapsed is not None else None,
                "difference": difference,
                "percent": difference / startClose * 100.0 if startClose else None,
            })
        return metrics

    def _copyMeasurement(self):
        """Copy Measure: A/B olcumunu Measure panelindekiyle ayni duz metin
        formatinda panoya kopyalar (bkz. RangeMeasureTool.formatMetricsText)."""
        metrics = self._measureMetrics()
        if metrics is None:
            return
        imgui.set_clipboard_text(RangeMeasureTool.formatMetricsText(metrics))
        self._status = "Measurement copied to clipboard."

    def _saveMeasurement(self):
        """Save Measure: Measure panelindekiyle ayni dosyaya/formatta append
        eder (bkz. RangeMeasureTool.saveMeasurement)."""
        tool = self._pm.getRangeMeasureTool() if self._pm is not None else None
        if tool is None:
            self._status = "Save Measure unavailable."
            return
        _, self._status = tool.saveMeasurement(self._measureMetrics(), "Swing Controls")

    def _clearMeasurement(self):
        self._measureEnabled = False
        self._measureStartIndex = None
        self._measureEndIndex = None
        self._status = "Range measurement cleared."

    def _selectMeasurementBar(self, index):
        if self._measureStartIndex is None or self._measureEndIndex is not None:
            self._measureStartIndex = index
            self._measureEndIndex = None
            self._status = f"Range A selected: bar={index}. Click the second bar (B)."
            return
        self._measureEndIndex = index
        self._status = (f"Range selected: bar {self._measureStartIndex} -> {index}. "
                        "Click again to start a new measurement.")

    def _renderSegmentSummary(self):
        ordered = sorted(self._annotations.items())
        if len(ordered) < 2:
            imgui.text_disabled("Segment: add at least two annotations")
            return
        segmentEnds = [index for index, _ in ordered[1:]]
        if self._selectedSegmentEndIndex not in segmentEnds:
            self._selectedSegmentEndIndex = segmentEnds[-1]
        pairIndex = segmentEnds.index(self._selectedSegmentEndIndex)
        hoveredPosition = None
        if self._hoveredAnnotationIndex is not None:
            for position, (index, _) in enumerate(ordered):
                if index == self._hoveredAnnotationIndex:
                    hoveredPosition = position
                    pairIndex = 0 if position == 0 else position - 1
                    break
        labels = [
            f"{ordered[i - 1][1]['type']} {ordered[i - 1][0]} -> "
            f"{ordered[i][1]['type']} {ordered[i][0]}"
            for i in range(1, len(ordered))
        ]
        imgui.set_next_item_width(-1)
        changed, selected = imgui.combo("Segment", pairIndex, labels)
        if changed:
            pairIndex = selected
            self._selectedSegmentEndIndex = segmentEnds[pairIndex]
        if imgui.button("< Previous Segment", (160, 0)) and pairIndex > 0:
            pairIndex -= 1
            self._selectedSegmentEndIndex = segmentEnds[pairIndex]
        imgui.same_line()
        if imgui.button("Next Segment >", (160, 0)) and pairIndex < len(segmentEnds) - 1:
            pairIndex += 1
            self._selectedSegmentEndIndex = segmentEnds[pairIndex]
        (startIndex, start), (endIndex, end) = ordered[pairIndex:pairIndex + 2]
        segment = self._store.calculateSegment(startIndex, start, endIndex, end)
        if segment is None: return
        percent = segment["changePercent"]
        elapsed = segment["elapsedSeconds"]
        efficiency = segment["efficiencyRatio"]
        imgui.separator()
        imgui.text("Hovered Segment" if hoveredPosition is not None
                   else "Selected / Latest Segment")
        if hoveredPosition is not None:
            hoveredIndex, hovered = ordered[hoveredPosition]
            timestamp = str(self._data.timestamps[hoveredIndex])
            imgui.text_colored((0.3, 0.9, 1.0, 1.0),
                               f"Hover: {hovered['type']}  Bar {hoveredIndex}  "
                               f"Price {hovered['price']:.6g}")
            imgui.text_wrapped(timestamp)
        imgui.text(f"{segment['type']}  Bars {startIndex} -> {endIndex}")
        imgui.text(f"Distance: {segment['barDistance']} bars  "
                   f"Elapsed: {self._formatDuration(elapsed)}")
        imgui.text(f"Change: {segment['priceChange']:+.6g}  "
                   f"({percent:+.3f}%)" if percent is not None else
                   f"Change: {segment['priceChange']:+.6g}")
        imgui.text(f"High / Low: {segment['high']:.6g} / {segment['low']:.6g}")
        imgui.text(f"MFE / MAE: {segment['mfe']:+.6g} / {segment['mae']:+.6g}")
        imgui.text("Efficiency: -" if efficiency is None else
                   f"Efficiency: {efficiency:.4f}")
        imgui.separator()

    @staticmethod
    def _formatDuration(seconds):
        if seconds is None: return "-"
        seconds = int(seconds)
        days, seconds = divmod(seconds, 86400)
        hours, seconds = divmod(seconds, 3600)
        minutes, seconds = divmod(seconds, 60)
        prefix = f"{days}d " if days else ""
        return f"{prefix}{hours:02d}:{minutes:02d}:{seconds:02d}"

    def _handleKeyboard(self):
        io = imgui.get_io()
        if io.want_text_input:
            return

        if io.key_ctrl and imgui.is_key_pressed(imgui.Key.z, repeat=False):
            self._undo()
            return
        if io.key_ctrl and imgui.is_key_pressed(imgui.Key.y, repeat=False):
            self._redo()
            return

        move = None
        if (imgui.is_key_pressed(imgui.Key.left_arrow, repeat=True) or
                imgui.is_key_pressed(imgui.Key.a, repeat=True)):
            move = -self._step
        elif (imgui.is_key_pressed(imgui.Key.right_arrow, repeat=True) or
              imgui.is_key_pressed(imgui.Key.d, repeat=True)):
            move = self._step
        elif imgui.is_key_pressed(imgui.Key.page_up, repeat=True):
            move = -self._width
        elif imgui.is_key_pressed(imgui.Key.page_down, repeat=True):
            move = self._width

        if move is not None:
            self._start = self._clampStart(self._start + move)
            self._gotoIndex = self._start
            self._applyWindow()
        elif imgui.is_key_pressed(imgui.Key.home, repeat=False):
            self._start = 0
            self._gotoIndex = self._start
            self._applyWindow()
        elif imgui.is_key_pressed(imgui.Key.end, repeat=False):
            self._start = self._lastStart()
            self._gotoIndex = self._start
            self._applyWindow()
        elif imgui.is_key_pressed(imgui.Key.space, repeat=False):
            # Fareyle yapilan serbest pan/zoom sonrasinda mevcut swing
            # penceresinin kayitli baslangic ve genisligine geri don.
            self._applyWindow()
        elif imgui.is_key_pressed(imgui.Key._1, repeat=False):
            self._addAnnotationAtHoveredBar("Dip")
        elif imgui.is_key_pressed(imgui.Key._2, repeat=False):
            self._addAnnotationAtHoveredBar("Tepe")
        elif imgui.is_key_pressed(imgui.Key.delete, repeat=False):
            self._deleteAnnotationAtHoveredBar()
        elif imgui.is_key_pressed(imgui.Key.escape, repeat=False) and self._measureEnabled:
            self._measureEnabled = False
            self._status = "Range measurement mode closed; lines were preserved."

    def _drawAnnotationsAndHandleClick(self, pm, panelId, panel):
        data = self._data
        if not self._active or data is None or panelId != self._panelId:
            return

        limits = pm.getXAxisLimits(panelId)
        if limits is None:
            return
        lo = max(0, int(limits[0]) - 1)
        hi = min(self._totalBars - 1, int(limits[1]) + 1)

        profileNames = getattr(data, "zigZagProfileNames", [])
        pivotRows = getattr(data, "zigZagPivots", [])
        priceRows = getattr(data, "zigZagPivotPrices", [])
        showMarkers = getattr(data, "zigZagShowMarkers", [])
        visibleProfiles = getattr(data, "zigZagVisible", [])
        for profileIndex, (pivots, prices) in enumerate(zip(pivotRows, priceRows)):
            if not self._showZigZagMarkers:
                break
            if profileIndex < len(visibleProfiles) and not visibleProfiles[profileIndex]:
                continue
            if profileIndex < len(showMarkers) and not showMarkers[profileIndex]:
                continue
            for index in range(lo, hi + 1):
                pivot = int(pivots[index])
                if pivot == 0:
                    continue
                isTrough = pivot < 0
                color = imgui.ImVec4(*(0.15, 0.85, 1.0, 0.9) if isTrough
                                     else (1.0, 0.65, 0.1, 0.9))
                offset = (0, 28 + profileIndex * 12) if isTrough else (0, -28 - profileIndex * 12)
                marker = "D" if isTrough else "T"
                if len(profileNames) > 1:
                    marker += str(profileIndex + 1)
                implot.annotation(float(index), float(prices[index]), color,
                                  offset, False, marker)

        for index, annotation in self._annotations.items():
            if not self._showManualMarkers:
                break
            if index < lo or index > hi:
                continue
            isDip = annotation["type"] == "Dip"
            colorValues = (0.15, 0.85, 1.0, 1.0) if isDip else (1.0, 0.65, 0.1, 1.0)
            color = imgui.ImVec4(*colorValues)
            offset = (0, 18) if isDip else (0, -18)
            text = "D" if isDip else "T"
            implot.annotation(float(index), float(annotation["price"]),
                              color, offset, False, text)

        if self._measureStartIndex is not None:
            implot.drag_line_x(710001, float(self._measureStartIndex),
                               (0.25, 0.85, 1.0, 0.95), 2,
                               implot.DragToolFlags_.no_inputs.value)
        if self._measureEndIndex is not None:
            implot.drag_line_x(710002, float(self._measureEndIndex),
                               (1.0, 0.7, 0.2, 0.95), 2,
                               implot.DragToolFlags_.no_inputs.value)

        if not implot.is_plot_hovered():
            # Imlec Swing Controls penceresine tasindiginda son gecerli hedef
            # korunur; aksi halde penceredeki Add/Delete dugmeleri hedefini
            # daha kullanici dugmeye ulasamadan kaybeder.
            return
        mouseX, _ = implot.get_plot_mouse_pos()
        index = int(round(mouseX))
        if index < 0 or index >= self._totalBars:
            self._hoveredAnnotationIndex = None
            return
        self._hoveredBarIndex = index
        self._hoveredAnnotationIndex = index if index in self._annotations else None
        if self._measureEnabled and imgui.is_mouse_clicked(0):
            self._selectMeasurementBar(index)

    def _addAnnotationAtHoveredBar(self, annotationType):
        index = self._hoveredBarIndex
        data = self._data
        if index is None or data is None:
            self._status = "Move the mouse over a candle first."
            return
        if index in self._annotations:
            self._status = f"Bar {index} already has an annotation."
            return

        if annotationType == "Dip":
            price = float(data.low[index])
        else:
            price = float(data.high[index])
        self._annotations[index] = {
            "id": f"A-{index:09d}",
            "type": annotationType,
            "price": price,
            "createdAt": datetime.now(timezone.utc).astimezone().isoformat(),
        }
        self._undoStack.append(("add", index, dict(self._annotations[index])))
        self._redoStack.clear()
        self._store.record("add", index, self._annotations[index], self._start, self._width)
        self._store.saveSnapshots(self._annotations, self._start, self._width)
        self._status = f"{annotationType} added: bar={index}, price={price:g}"

    def _deleteAnnotationAtHoveredBar(self):
        index = self._hoveredBarIndex
        if index is None:
            self._status = "Move the mouse over an annotated candle first."
            return
        removed = self._annotations.pop(index, None)
        if removed is None:
            self._status = f"Bar {index} has no annotation."
            return
        self._undoStack.append(("delete", index, dict(removed)))
        self._redoStack.clear()
        self._store.record("delete", index, removed, self._start, self._width)
        self._store.saveSnapshots(self._annotations, self._start, self._width)
        self._status = f"Annotation deleted: bar={index}, type={removed['type']}"

    def _deleteAllAnnotations(self):
        self._deleteAllPending = False
        if not self._annotations:
            self._status = "There are no annotations to delete."
            return
        removed = [(index, dict(annotation))
                   for index, annotation in sorted(self._annotations.items())]
        for index, annotation in removed:
            self._store.record("delete", index, annotation, self._start, self._width)
        self._annotations.clear()
        self._undoStack.append(("delete_all", removed, None))
        self._redoStack.clear()
        self._store.saveSnapshots(self._annotations, self._start, self._width)
        self._status = f"Deleted all annotations: {len(removed)}"

    def _undo(self):
        if not self._undoStack:
            self._status = "Nothing to undo."
            return
        action, index, annotation = self._undoStack.pop()
        if action == "delete_all":
            for restoredIndex, restored in index:
                self._annotations[restoredIndex] = dict(restored)
                self._store.record("add", restoredIndex, restored, self._start, self._width)
            self._redoStack.append((action, index, None))
            self._store.saveSnapshots(self._annotations, self._start, self._width)
            self._status = f"Undo: restored {len(index)} annotations"
            return
        if action == "add":
            self._annotations.pop(index, None)
        else:
            self._annotations[index] = dict(annotation)
        self._redoStack.append((action, index, dict(annotation)))
        persistedAction = "delete" if action == "add" else "add"
        self._store.record(persistedAction, index, annotation, self._start, self._width)
        self._store.saveSnapshots(self._annotations, self._start, self._width)
        self._status = f"Undo: {action} bar={index}"

    def _redo(self):
        if not self._redoStack:
            self._status = "Nothing to redo."
            return
        action, index, annotation = self._redoStack.pop()
        if action == "delete_all":
            for removedIndex, removed in index:
                self._annotations.pop(removedIndex, None)
                self._store.record("delete", removedIndex, removed, self._start, self._width)
            self._undoStack.append((action, index, None))
            self._store.saveSnapshots(self._annotations, self._start, self._width)
            self._status = f"Redo: deleted {len(index)} annotations"
            return
        if action == "add":
            self._annotations[index] = dict(annotation)
        else:
            self._annotations.pop(index, None)
        self._undoStack.append((action, index, dict(annotation)))
        self._store.record(action, index, annotation, self._start, self._width)
        self._store.saveSnapshots(self._annotations, self._start, self._width)
        self._status = f"Redo: {action} bar={index}"

    def _applyWindow(self):
        if self._panelId is None or self._totalBars <= 0:
            return
        self._width = self._clampWidth(self._width)
        self._start = self._clampStart(self._start)
        self._gotoIndex = self._start
        result = self._pm.applyViewMode(
            self._panelId, "Range", n=self._start, n2=self._width)
        if result is None:
            self._status = "Window could not be applied."
            return
        self._updateStatus()
        self._store.saveSnapshots(self._annotations, self._start, self._width)

    def _clampWidth(self, value):
        try:
            value = int(value)
        except (TypeError, ValueError):
            value = self._minWidth
        return max(self._minWidth, min(value, self._maxWidth))

    def _lastStart(self):
        return max(0, self._totalBars - self._width)

    def _clampStart(self, value):
        return max(0, min(int(value), self._lastStart()))

    def _syncPresetIndex(self):
        if self._width in self._presets:
            self._presetIndex = self._presets.index(self._width)

    def _updateStatus(self):
        if self._totalBars <= 0:
            self._status = "Window: no data"
            return
        end = min(self._totalBars - 1, self._start + self._width - 1)
        overlap = max(0, self._width - self._step)
        windowNo = self._start // self._step + 1
        lastStart = self._lastStart()
        progress = self._start / lastStart * 100.0 if lastStart > 0 else 100.0
        self._status = (f"Window {windowNo}: [{self._start} - {end}]  "
                        f"Progress={progress:.2f}%  "
                        f"Width={end - self._start + 1}  Overlap={overlap}")
