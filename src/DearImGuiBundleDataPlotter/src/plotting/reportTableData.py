"""Background indexing and bounded page reads for large pipe-delimited reports."""

from array import array
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from threading import Event


def splitRow(line):
    return [cell.strip() for cell in line.split("|")]


def readHeader(path):
    with open(path, "rb") as stream:
        for _ in range(30):
            raw = stream.readline()
            if not raw:
                break
            cells = splitRow(raw.decode("utf-8-sig", errors="replace"))
            if len(cells) > 1 and cells[0] in ("No", "BarNo"):
                return cells, stream.tell()
    raise ValueError("Rapor tablo basligi bulunamadi.")


def scanReport(path, headers, start, filters, sort, cancel):
    offsets = array("Q")
    sortable = []
    query, since, until, direction, result = filters
    dateIndex = next((i for i, h in enumerate(headers) if h in ("Açılış Tarihi", "Date")), None)
    directionIndex = headers.index("Yon") if "Yon" in headers else None
    pnlIndex = headers.index("Kar/Zarar") if "Kar/Zarar" in headers else None
    scanned = 0
    with open(path, "rb") as stream:
        stream.seek(start)
        while True:
            if cancel.is_set():
                return None
            offset = stream.tell()
            raw = stream.readline()
            if not raw:
                break
            cells = splitRow(raw.decode("utf-8-sig", errors="replace"))
            if len(cells) != len(headers) or not cells[0].isdigit():
                continue
            scanned += 1
            if query and query not in " | ".join(cells).casefold():
                continue
            date = cells[dateIndex].replace("-", ".") if dateIndex is not None else ""
            if since and date < since or until and date > until:
                continue
            if direction and (directionIndex is None or cells[directionIndex] != direction):
                continue
            if result and pnlIndex is not None:
                pnl = float(cells[pnlIndex])
                if (result == "Karli" and pnl <= 0 or result == "Zararli" and pnl >= 0
                        or result == "Notr" and pnl != 0):
                    continue
            if sort is None:
                offsets.append(offset)
            else:
                cell = cells[sort[0]]
                try:
                    key = (0, float(cell))
                except ValueError:
                    key = (1, cell)
                sortable.append((key, offset))
    if sort is not None:
        sortable.sort(key=lambda item: item[0], reverse=sort[1])
        offsets = array("Q", (offset for _, offset in sortable))
    return offsets, scanned


class PagedReportData:
    # Shared bounded worker pool; UI never waits for an entire report scan.
    _executor = ThreadPoolExecutor(max_workers=2, thread_name_prefix="report-index")

    def __init__(self):
        self.path = None
        self.headers = []
        self.offsets = array("Q")
        self.total = 0
        self.rows = []
        self.page = 0
        self.pageSize = 100
        self.error = ""
        self._future = None
        self._cancel = Event()
        self._start = 0

    @property
    def busy(self):
        return self._future is not None

    def open(self, path):
        self._cancel.set()
        self._future = None
        self.path = Path(path) if path else None
        self.headers, self.rows = [], []
        self.offsets = array("Q")
        self.page, self.total, self.error = 0, 0, ""
        if self.path is None:
            self.error = "Bu bundle icinde rapor bulunamadi."
            return
        try:
            self.headers, self._start = readHeader(self.path)
        except (OSError, ValueError) as exc:
            self.error = f"Rapor okunamadi: {exc}"

    def apply(self, filters=("", "", "", "", ""), sort=None):
        if not self.headers:
            return
        self._cancel.set()
        self._cancel = Event()
        self.rows, self.offsets = [], array("Q")
        self.page, self.total, self.error = 0, 0, ""
        self._future = self._executor.submit(
            scanReport, self.path, list(self.headers), self._start, filters, sort, self._cancel)

    def poll(self):
        if self._future is None or not self._future.done():
            return
        future, self._future = self._future, None
        try:
            result = future.result()
            if result is not None:
                self.offsets, self.total = result
                self.readPage(0)
        except (OSError, ValueError, IndexError) as exc:
            self.error = f"Rapor okunamadi: {exc}"

    def readPage(self, page):
        self.page = max(0, min(page, max(0, (len(self.offsets) - 1) // self.pageSize)))
        self.rows = []
        try:
            with open(self.path, "rb") as stream:
                start = self.page * self.pageSize
                for offset in self.offsets[start:start + self.pageSize]:
                    stream.seek(offset)
                    self.rows.append(splitRow(stream.readline().decode("utf-8-sig", errors="replace")))
        except OSError as exc:
            self.error = f"Sayfa okunamadi: {exc}"
