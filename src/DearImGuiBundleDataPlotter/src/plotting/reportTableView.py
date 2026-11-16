"""Independent table tabs used by Report2; statistics rendering stays separate."""

from datetime import datetime

from imgui_bundle import imgui

from .reportTableData import PagedReportData


def columnGroup(name):
    if name in ("BarNo", "Date", "Time"):
        return "Kimlik"
    if name in ("Open", "High", "Low", "Close", "Volume", "Size", "Change", "Change%"):
        return "Fiyatlar"
    if name.startswith("Komisyon"):
        return "Komisyon"
    if name.startswith(("Bakiye", "Getiri", "KarZarar")):
        return "Bakiye / Getiri"
    if name.startswith(("Yon", "Seviye", "Sinyal")):
        return "Sinyaller"
    if name.startswith(("Emir", "IsTrade", "IsPoz", "KarAl", "ZararKes", "Izleyen")):
        return "Emirler"
    return "Pozisyon"


class ReportTableView:
    def __init__(self, config, performance=False):
        self.config = config
        self.performance = performance
        self.key = "performance" if performance else "lists"
        self.data = PagedReportData()
        self._loaded = False
        self._query = self._since = self._until = ""
        self._direction = self._result = 0
        self._sort = 0
        self._descending = False
        self._selected = None
        self._error = ""

    def setPath(self, path):
        # Lazy open: the half-gigabyte Lists report is not scanned until its tab is used.
        self.data.open(None)
        self._path = path
        self._loaded = False
        self._selected = None

    def _settings(self):
        return self.config.data.setdefault("tables", {}).setdefault(
            self.key, {"columnMode": "All" if self.performance else "Fiyatlar", "columns": {}})

    def _syncColumns(self):
        settings = self._settings()
        changed = False
        for name in self.data.headers:
            if name not in settings["columns"]:
                settings["columns"][name] = True
                changed = True
        if changed:
            self.config.save()

    def _apply(self):
        try:
            dates = []
            for text in (self._since, self._until):
                dates.append(datetime.strptime(text.strip().replace("-", "."), "%Y.%m.%d").strftime("%Y.%m.%d") if text.strip() else "")
            if dates[0] and dates[1] and dates[0] > dates[1]:
                raise ValueError("Baslangic tarihi bitis tarihinden buyuk.")
        except ValueError:
            self._error = "Tarih araligi gecersiz. YYYY.AA.GG kullanin."
            return
        self._error = ""
        self._selected = None
        filters = (self._query.strip().casefold(), *dates,
                   ("", "A", "S")[self._direction],
                   ("", "Karli", "Zararli", "Notr")[self._result])
        sort = (self._sort - 1, self._descending) if self.performance and self._sort else None
        self.data.apply(filters, sort)

    def _visibleColumns(self):
        settings = self._settings()
        mode = settings["columnMode"]
        return [i for i, name in enumerate(self.data.headers)
                if self.performance or name in ("BarNo", "Date", "Time") or mode == "All"
                or (mode == "Custom" and settings["columns"].get(name, True))
                or columnGroup(name) == mode]

    def render(self):
        if not self._loaded:
            self.data.open(getattr(self, "_path", None))
            self._loaded = True
            self._syncColumns()
            self._apply()
        self.data.poll()
        if imgui.button("Refresh"):
            self.config.load()
            self.data.open(getattr(self, "_path", None))
            self._syncColumns()
            self._apply()
        imgui.same_line()
        if imgui.button("Gorunen Sayfayi Kopyala"):
            columns = self._visibleColumns()
            lines = [[self.data.headers[i] for i in columns]]
            lines.extend([row[i] for i in columns] for row in self.data.rows)
            imgui.set_clipboard_text("\n".join("\t".join(row) for row in lines))
        imgui.text_wrapped(str(self.data.path or "Rapor bulunamadi."))
        _, self._query = imgui.input_text("Ara", self._query)
        _, self._since = imgui.input_text("Baslangic (YYYY.AA.GG)", self._since)
        _, self._until = imgui.input_text("Bitis (YYYY.AA.GG)", self._until)
        _, self._direction = imgui.combo("Yon", self._direction, ["Tumu", "Alis (A)", "Satis (S)"])
        if self.performance:
            _, self._result = imgui.combo("Sonuc", self._result, ["Tumu", "Karli", "Zararli", "Notr"])
            _, self._sort = imgui.combo("Sirala", self._sort, ["Dosya sirasi"] + self.data.headers)
            _, self._descending = imgui.checkbox("Azalan", self._descending)
        if imgui.button("Uygula"):
            self._apply()
        imgui.same_line()
        if imgui.button("Filtreleri Temizle"):
            self._query = self._since = self._until = ""
            self._direction = self._result = self._sort = 0
            self._descending = False
            self._apply()
        if not self.performance and self.data.headers:
            self._renderColumnSettings()
        if self._error or self.data.error or self.config.error:
            imgui.text_wrapped(self._error or self.data.error or self.config.error)
        if self.data.busy:
            imgui.text("Rapor arka planda taraniyor...")
            return
        if not self.data.headers:
            return
        pages = max(1, (len(self.data.offsets) + self.data.pageSize - 1) // self.data.pageSize)
        imgui.begin_disabled(self.data.page == 0)
        if imgui.button("Onceki"):
            self.data.readPage(self.data.page - 1)
        imgui.end_disabled()
        imgui.same_line()
        imgui.begin_disabled(self.data.page + 1 >= pages)
        if imgui.button("Sonraki"):
            self.data.readPage(self.data.page + 1)
        imgui.end_disabled()
        imgui.same_line()
        changed, page = imgui.input_int("Sayfa", self.data.page + 1)
        if changed:
            self.data.readPage(page - 1)
        imgui.text(f"Sayfa {self.data.page + 1}/{pages} | {len(self.data.offsets)} eslesen / {self.data.total} satir")
        self._renderTable()
        if self._selected is not None:
            self._renderDetails()

    def _renderColumnSettings(self):
        settings = self._settings()
        modes = ["Fiyatlar", "Sinyaller", "Bakiye / Getiri", "Pozisyon", "Komisyon", "Emirler", "All", "Custom"]
        mode = settings["columnMode"]
        changed, index = imgui.combo("Sutun grubu", modes.index(mode) if mode in modes else 0, modes)
        if changed:
            settings["columnMode"] = modes[index]
            self.config.save()
        if settings["columnMode"] == "Custom" and imgui.collapsing_header("Sutun Secimi"):
            imgui.begin_child("columns", size=(-1, 180), child_flags=imgui.ChildFlags_.borders.value)
            changed = False
            for name in self.data.headers:
                if name in ("BarNo", "Date", "Time"):
                    continue
                toggled, settings["columns"][name] = imgui.checkbox(name, settings["columns"].get(name, True))
                changed |= toggled
            if changed:
                self.config.save()
            imgui.end_child()

    def _renderTable(self):
        columns = self._visibleColumns()
        flags = (imgui.TableFlags_.borders | imgui.TableFlags_.row_bg | imgui.TableFlags_.resizable
                 | imgui.TableFlags_.scroll_x | imgui.TableFlags_.scroll_y)
        if not imgui.begin_table("report_rows", len(columns), flags, outer_size=(-1, 300 if self._selected else 400)):
            return
        for index in columns:
            imgui.table_setup_column(self.data.headers[index], imgui.TableColumnFlags_.width_fixed, 140)
        imgui.table_setup_scroll_freeze(1 if self.performance else 3, 1)
        imgui.table_headers_row()
        coloredColumns = {i for i, name in enumerate(self.data.headers)
                          if self.performance and name in ("Kar/Zarar", "Yön Bazlı Değişim %")}
        for rowIndex, row in enumerate(self.data.rows):
            imgui.table_next_row()
            imgui.push_id(rowIndex)
            for position, index in enumerate(columns):
                imgui.table_set_column_index(position)
                if position == 0:
                    clicked, _ = imgui.selectable(row[index], self._selected == row, imgui.SelectableFlags_.span_all_columns)
                    if clicked:
                        self._selected = row
                elif index in coloredColumns:
                    value = float(row[index])
                    color = (0.3, 0.85, 0.4, 1) if value > 0 else (1, 0.4, 0.4, 1) if value < 0 else (0.8, 0.8, 0.8, 1)
                    imgui.text_colored(color, row[index])
                else:
                    imgui.text_unformatted(row[index])
            imgui.pop_id()
        imgui.end_table()

    def _renderDetails(self):
        if imgui.button("Ayrintiyi Kapat"):
            self._selected = None
            return
        imgui.begin_child("row_details", size=(-1, 220), child_flags=imgui.ChildFlags_.borders.value)
        groups = {}
        for name, value in zip(self.data.headers, self._selected):
            groups.setdefault("Islem" if self.performance else columnGroup(name), []).append((name, value))
        for title, rows in groups.items():
            if imgui.collapsing_header(title, imgui.TreeNodeFlags_.default_open):
                if imgui.begin_table(title, 2, imgui.TableFlags_.borders | imgui.TableFlags_.row_bg):
                    for name, value in rows:
                        imgui.table_next_row(); imgui.table_set_column_index(0)
                        imgui.text_unformatted(name); imgui.table_set_column_index(1)
                        imgui.text_unformatted(value)
                    imgui.end_table()
        imgui.end_child()
