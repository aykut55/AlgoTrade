"""Grouped view of the complete statistics report, without formatted-report omissions."""

import os
import re

from imgui_bundle import imgui

from .reportsWindow import ReportsWindow
from .reportConfig import ReportConfig, MODES
from .reportTableView import ReportTableView


def statisticsGroup(key):
    if key.startswith(("EnUzunDD", "IslemIciMaxZarar", "EnKotuGun")):
        return "Risk, drawdown ve performans"
    if key.startswith(("EnUzunIslemsiz", "IslemYapilanGun", "IslemGunuBasina")):
        return "Zaman ve islem sikligi"
    if key.startswith(("Trader", "Symbol", "System", "Strategy")):
        return "Trader, sistem ve strateji"
    if key.startswith(("LastExecution", "LastReset", "LastStatistics")):
        return "Calistirma bilgileri"
    if key.startswith(("ToplamBar", "SecilenBar", "IlkBar", "SonBar")) and key != "SonBarNo":
        return "Bar bilgileri"
    if key.startswith(("ToplamGecenSure", "Ort")):
        return "Zaman ve islem sikligi"
    if re.match(r"Getiri(?:FiyatNet|Fiyat|Puan)(?:Bu|Ay\d|Hafta\d|Gun\d|Saat\d)", key):
        if key.startswith("GetiriFiyatNet"):
            return "Donemsel getiriler (net fiyat)"
        return "Donemsel getiriler (puan)" if key.startswith("GetiriPuan") else "Donemsel getiriler (fiyat)"
    if key.startswith(("GetiriMax", "ProfitFactor", "Score")):
        return "Risk, drawdown ve performans"
    if key.startswith(("MinBakiye", "MaxBakiye")):
        return "Bakiye minimum / maksimum"
    if key.startswith(("IlkBakiye", "Bakiye", "Getiri")):
        return "Bakiye ve getiriler"
    if key.startswith("Komisyon"):
        return "Komisyon"
    if "KomutSayisi" in key:
        return "Komut sayilari"
    if key.startswith(("KarZarar", "ToplamKar", "ToplamZarar", "NetKar", "MaxKar", "MaxZarar", "Karda", "Zararda", "Karli")):
        return "Kar ve zarar"
    if key.startswith(("Sinyal", "SonYon", "Prev", "SonFiyat", "SonAFiyat", "SonSFiyat", "SonFFiyat", "SonPFiyat", "SonBarNo", "SonABarNo", "SonSBarNo", "Emir")):
        return "Sinyal ve emir durumu"
    if key.startswith(("Hisse", "Kontrat", "Varlik", "SonVarlik", "Kayma", "Micro", "Pyramiding", "MaxPosition")):
        return "Varlik ve pozisyon"
    if key.endswith("Sayisi"):
        return "Islem sayilari ve sonuclari"
    return "Diger istatistikler"


def parseStatistics(content):
    groups = {}
    for line in content.splitlines():
        # Split only the field separator: date/time values contain colons too.
        match = re.match(r"^(\w+)\s+:\s?(.*)$", line)
        if match:
            key, value = match.groups()
            groups.setdefault(statisticsGroup(key), []).append((key, value.strip()))
    return groups


class Report2Window(ReportsWindow):
    WINDOW_TITLE = "Report2"

    def __init__(self, guiManager):
        super().__init__(guiManager)
        self._groups = {}
        self._search = ""
        self._openAll = None
        self._config = ReportConfig()
        self._editing = False
        self._folderError = ""
        self._performanceTable = ReportTableView(self._config, performance=True)
        self._listsTable = ReportTableView(self._config)

    def _loadIndex(self, bundlePath):
        super()._loadIndex(bundlePath)
        for table, filename in ((self._performanceTable, "singletraderperformans.txt"),
                                (self._listsTable, "singletraderlists.txt")):
            entry = next((entry for entry in self._files
                          if os.path.basename(self._entryValue(entry, "Path")).lower() == filename), None)
            path = os.path.join(os.path.dirname(self._indexPath), self._entryValue(entry, "Path")) if entry else None
            table.setPath(path)
        self._files = [entry for entry in self._files
                       if os.path.basename(self._entryValue(entry, "Path")).lower()
                       == "singletraderstatistics.txt"]
        self._selected = 0
        self._contentPath = None
        self._loadSelected()
        if self._index and not self._files:
            self._status = "Bu bundle icinde SingleTraderStatistics.txt bulunamadi."

    def _loadSelected(self):
        super()._loadSelected()
        self._groups = parseStatistics(self._content)
        legacyOrder = (self._gm.configManager.get("report2") or {}).get("groupOrder", [])
        self._config.sync(self._groups, legacyOrder)

    def _moveGroup(self, name, destination):
        groups = self._config.data["groups"]
        group = next(group for group in groups if group["id"] == name)
        groups.remove(group)
        groups.insert(destination, group)
        self._config.save()

    def _openConfigFolder(self):
        try:
            os.startfile(str(self._config.path.parent))
            self._folderError = ""
        except OSError as exc:
            self._folderError = f"Klasor acilamadi: {exc}"

    def _renderCustomSettings(self):
        imgui.text_wrapped("Custom secimlerini burada duzenleyin. Gizli gruplar da bu listede bulunur.")
        changed = False
        for group in self._config.data["groups"]:
            imgui.push_id("edit_" + group["id"])
            toggled, group["visible"] = imgui.checkbox("##visible", group["visible"])
            changed |= toggled
            imgui.same_line()
            if imgui.tree_node(group["title"]):
                for item in group["items"]:
                    toggled, item["visible"] = imgui.checkbox(item["key"], item["visible"])
                    changed |= toggled
                imgui.tree_pop()
            imgui.pop_id()
        if changed:
            self._config.save()

    def _renderContents(self):
        if imgui.begin_tab_bar("report2_tabs"):
            for title, render in (("Istatistikler", self._renderStatistics),
                                  ("Performans", self._performanceTable.render),
                                  ("Lists", self._listsTable.render)):
                opened, _ = imgui.begin_tab_item(title)
                if opened:
                    imgui.push_id(title)
                    render()
                    imgui.pop_id()
                    imgui.end_tab_item()
            imgui.end_tab_bar()

    def _renderStatistics(self):
        if imgui.button("Refresh"):
            self._config.load()
            self._reloadIndex(self._lastBundlePath)
        imgui.same_line()
        if imgui.button("Copy"):
            imgui.set_clipboard_text(self._content)
        imgui.same_line()
        if imgui.button("Config Klasorunu Ac"):
            self._openConfigFolder()
        imgui.set_next_item_width(150)
        changed, selected = imgui.combo("Gorunum", MODES.index(self._config.data["mode"]), list(MODES))
        if changed:
            self._config.data["mode"] = MODES[selected]
            self._config.save()
        imgui.same_line()
        _, self._editing = imgui.checkbox("Custom Ayarlari", self._editing)
        if self._index:
            trader = self._index.get("TraderType", self._index.get("traderType", ""))
            generated = self._index.get("GeneratedAt", self._index.get("generatedAt", ""))
            imgui.text_wrapped(f"Trader: {trader}   Generated: {generated}")
        imgui.text_wrapped(self._contentPath or self._status)
        if self._config.error or self._folderError:
            imgui.text_wrapped(self._config.error or self._folderError)
        if self._editing:
            imgui.begin_child("report2_custom", size=(-1, 250), child_flags=imgui.ChildFlags_.borders.value)
            self._renderCustomSettings()
            imgui.end_child()
        if not self._groups:
            imgui.text_wrapped(self._content or self._status)
            return
        _, self._search = imgui.input_text("Ara", self._search)
        if imgui.button("Tumunu Ac"):
            self._openAll = True
        imgui.same_line()
        if imgui.button("Tumunu Kapat"):
            self._openAll = False
        query = self._search.strip().casefold()
        count = sum(len(rows) for rows in self._groups.values())
        visibleGroups = self._config.rows(self._groups, query)
        shown = sum(len(rows) for _, rows in visibleGroups)
        imgui.same_line()
        imgui.text(f"{shown} / {count} alan, {len(visibleGroups)} grup")
        imgui.separator()
        imgui.begin_child("report2_groups", size=(-1, -1), child_flags=imgui.ChildFlags_.borders.value)
        visibleCount = 0
        configured = self._config.data["groups"]
        for group, rows in visibleGroups:
            name = group["id"]
            index = configured.index(group)
            visibleCount += len(rows)
            imgui.push_id(name)
            imgui.begin_disabled(index == 0)
            if imgui.small_button("En uste"):
                self._moveGroup(name, 0)
            imgui.same_line()
            if imgui.arrow_button("up", imgui.Dir_.up):
                self._moveGroup(name, index - 1)
            imgui.end_disabled()
            imgui.same_line()
            imgui.begin_disabled(index == len(configured) - 1)
            if imgui.arrow_button("down", imgui.Dir_.down):
                self._moveGroup(name, index + 1)
            imgui.end_disabled()
            imgui.same_line()
            if self._openAll is not None or query:
                imgui.set_next_item_open(True if query else self._openAll, imgui.Cond_.always)
            expanded = imgui.collapsing_header(f"{group['title']} ({len(rows)})", imgui.TreeNodeFlags_.default_open)
            if expanded:
                flags = imgui.TableFlags_.borders | imgui.TableFlags_.row_bg | imgui.TableFlags_.resizable
                if imgui.begin_table("values", 2, flags):
                    imgui.table_setup_column("Alan")
                    imgui.table_setup_column("Deger")
                    imgui.table_headers_row()
                    for key, value in rows:
                        imgui.table_next_row()
                        imgui.table_set_column_index(0)
                        imgui.text_unformatted(key)
                        imgui.table_set_column_index(1)
                        imgui.text_wrapped(value)
                    imgui.end_table()
            imgui.pop_id()
        if not visibleCount:
            imgui.text("Gosterilecek alan yok. Gorunum, Custom ayarlari veya aramayi kontrol edin.")
        imgui.end_child()
        self._openAll = None
