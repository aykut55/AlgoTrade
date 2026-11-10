import csv
import json
import os
import tempfile
from datetime import datetime, timezone


class SwingAnnotationStore:
    """Append-only event log plus recoverable annotation/session snapshots."""

    def __init__(self, sessionConfig, data):
        cfg = sessionConfig if isinstance(sessionConfig, dict) else {}
        self.directory = cfg.get("directory")
        self.symbol = str(cfg.get("symbol") or "OHLC")
        self.period = str(cfg.get("period") or "N/A")
        self.schemaVersion = int(cfg.get("schemaVersion") or 1)
        self.data = data
        if self.directory:
            os.makedirs(self.directory, exist_ok=True)

    def load(self):
        annotations = {}
        if not self.directory:
            return annotations, {}
        state = self._readJson("session.json") or {}
        path = os.path.join(self.directory, "annotation_events.jsonl")
        if os.path.isfile(path):
            with open(path, "r", encoding="utf-8") as stream:
                for line in stream:
                    try:
                        event = json.loads(line)
                        index = int(event["globalBarIndex"])
                        if event.get("action") == "delete":
                            annotations.pop(index, None)
                        elif event.get("action") == "add":
                            annotations[index] = {
                                "id": event.get("annotationId"),
                                "type": event["type"],
                                "price": float(event["price"]),
                                "createdAt": event.get("createdAt"),
                            }
                    except (ValueError, KeyError, TypeError, json.JSONDecodeError):
                        continue
        return annotations, state

    def record(self, action, index, annotation, windowStart, windowWidth):
        if not self.directory:
            return
        now = datetime.now(timezone.utc).astimezone().isoformat()
        event = {
            "schemaVersion": self.schemaVersion, "action": action,
            "annotationId": annotation.get("id"), "globalBarIndex": int(index),
            "type": annotation.get("type"), "price": annotation.get("price"),
            "createdAt": annotation.get("createdAt") or now, "recordedAt": now,
        }
        with open(os.path.join(self.directory, "annotation_events.jsonl"), "a",
                  encoding="utf-8", newline="\n") as stream:
            stream.write(json.dumps(event, ensure_ascii=False) + "\n")
            stream.flush()
            os.fsync(stream.fileno())
        self.saveState(windowStart, windowWidth, index)

    def saveSnapshots(self, annotations, windowStart, windowWidth):
        if not self.directory:
            return
        rows = []
        for index, annotation in sorted(annotations.items()):
            timestamp = str(self.data.timestamps[index]) if self.data is not None else ""
            rows.append([annotation.get("id"), self.symbol, self.period, index, timestamp,
                         annotation["type"], annotation["price"], annotation.get("createdAt", "")])
        self._atomicCsv(
            "annotations.csv",
            ["AnnotationId", "Symbol", "Period", "GlobalBarIndex",
             "DateTime", "Type", "Price", "CreatedAt"],
            rows)
        self._saveSegments(annotations)
        self.saveState(windowStart, windowWidth, None, len(rows))

    def _saveSegments(self, annotations):
        header = [
            "SegmentId", "StartAnnotationId", "EndAnnotationId", "SegmentType",
            "StartBarIndex", "EndBarIndex", "StartDateTime", "EndDateTime",
            "StartPrice", "EndPrice", "PriceChange", "ChangePercent",
            "BarDistance", "ElapsedSeconds", "SegmentHigh", "SegmentLow",
            "Mfe", "Mae", "AverageChangePerBar", "EfficiencyRatio",
        ]
        rows = []
        ordered = sorted(annotations.items())
        for number, ((startIndex, start), (endIndex, end)) in enumerate(
                zip(ordered, ordered[1:]), start=1):
            segment = self.calculateSegment(startIndex, start, endIndex, end)
            if segment is None: continue
            rows.append([
                f"S-{number:09d}", start.get("id"), end.get("id"),
                segment["type"], startIndex, endIndex,
                segment["startTime"], segment["endTime"], segment["startPrice"],
                segment["endPrice"], segment["priceChange"], segment["changePercent"],
                segment["barDistance"], segment["elapsedSeconds"], segment["high"],
                segment["low"], segment["mfe"], segment["mae"],
                segment["averageChangePerBar"], segment["efficiencyRatio"],
            ])
        self._atomicCsv("segments.csv", header, rows)

    def calculateSegment(self, startIndex, start, endIndex, end):
        if self.data is None or endIndex <= startIndex:
            return None
        startPrice = float(start["price"])
        endPrice = float(end["price"])
        priceChange = endPrice - startPrice
        barDistance = endIndex - startIndex
        highs = self.data.high[startIndex:endIndex + 1]
        lows = self.data.low[startIndex:endIndex + 1]
        closes = self.data.close[startIndex:endIndex + 1]
        segmentHigh = float(max(highs))
        segmentLow = float(min(lows))
        if priceChange >= 0:
            mfe = segmentHigh - startPrice
            mae = segmentLow - startPrice
        else:
            mfe = startPrice - segmentLow
            mae = startPrice - segmentHigh
        totalPath = sum(abs(float(closes[i]) - float(closes[i - 1]))
                        for i in range(1, len(closes)))
        startTime = str(self.data.timestamps[startIndex])
        endTime = str(self.data.timestamps[endIndex])
        return {
            "type": f"{start['type']}->{end['type']}",
            "startIndex": startIndex, "endIndex": endIndex,
            "startTime": startTime, "endTime": endTime,
            "startPrice": startPrice, "endPrice": endPrice,
            "priceChange": priceChange,
            "changePercent": priceChange / startPrice * 100.0 if startPrice else None,
            "barDistance": barDistance,
            "elapsedSeconds": self._elapsedSeconds(startTime, endTime),
            "high": segmentHigh, "low": segmentLow, "mfe": mfe, "mae": mae,
            "averageChangePerBar": priceChange / barDistance,
            "efficiencyRatio": abs(priceChange) / totalPath if totalPath else None,
        }

    def saveState(self, windowStart, windowWidth, selectedIndex=None, annotationCount=None):
        if not self.directory:
            return
        previous = self._readJson("session.json") or {}
        state = {
            "schemaVersion": self.schemaVersion, "symbol": self.symbol, "period": self.period,
            "totalBars": len(self.data.xs) if self.data is not None else 0,
            "windowStart": int(windowStart), "windowWidth": int(windowWidth),
            "selectedBarIndex": selectedIndex,
            "annotationCount": annotationCount if annotationCount is not None else previous.get("annotationCount", 0),
            "createdAt": previous.get("createdAt") or datetime.now(timezone.utc).astimezone().isoformat(),
            "updatedAt": datetime.now(timezone.utc).astimezone().isoformat(),
        }
        self._atomicJson("session.json", state)

    def _readJson(self, name):
        path = os.path.join(self.directory, name)
        if not os.path.isfile(path): return None
        try:
            with open(path, "r", encoding="utf-8") as stream: return json.load(stream)
        except (OSError, json.JSONDecodeError): return None

    def _atomicJson(self, name, value):
        target = os.path.join(self.directory, name)
        fd, temp = tempfile.mkstemp(dir=self.directory, suffix=".tmp")
        try:
            with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as stream:
                json.dump(value, stream, ensure_ascii=False, indent=2)
            os.replace(temp, target)
        finally:
            if os.path.exists(temp): os.remove(temp)

    def _atomicCsv(self, name, header, rows):
        target = os.path.join(self.directory, name)
        fd, temp = tempfile.mkstemp(dir=self.directory, suffix=".tmp")
        try:
            with os.fdopen(fd, "w", encoding="utf-8-sig", newline="") as stream:
                writer = csv.writer(stream, delimiter=";")
                writer.writerow(header)
                writer.writerows(rows)
            os.replace(temp, target)
        finally:
            if os.path.exists(temp): os.remove(temp)

    @staticmethod
    def _elapsedSeconds(startValue, endValue):
        try:
            start = datetime.fromisoformat(startValue.replace("Z", "+00:00"))
            end = datetime.fromisoformat(endValue.replace("Z", "+00:00"))
            return (end - start).total_seconds()
        except (TypeError, ValueError):
            return None
