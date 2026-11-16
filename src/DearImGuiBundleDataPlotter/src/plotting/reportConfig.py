"""Persistent Report2 layout and Custom visibility settings."""

import json
from pathlib import Path


CONFIG_PATH = Path(__file__).resolve().parents[4] / "inputs" / "configs" / "reportConfig.json"
MODES = ("ShowAll", "HideAll", "Custom")


class ReportConfig:
    def __init__(self, path=CONFIG_PATH):
        self.path = Path(path)
        self.data = {"mode": "ShowAll", "groups": []}
        self.error = ""
        self._valid = True
        self.load()

    def load(self):
        try:
            if self.path.exists():
                data = json.loads(self.path.read_text(encoding="utf-8-sig"))
                self._validate(data)
                self.data = data
            self._valid = True
            self.error = ""
        except (OSError, ValueError, TypeError) as exc:
            self._valid = False
            self.error = f"Rapor ayarlari okunamadi: {exc}"

    @staticmethod
    def _validate(data):
        if not isinstance(data, dict) or data.get("mode") not in MODES or not isinstance(data.get("groups"), list):
            raise ValueError("mode: ShowAll/HideAll/Custom; groups: liste olmali.")
        ids, keys = set(), set()
        for group in data["groups"]:
            if (not isinstance(group, dict) or not isinstance(group.get("id"), str)
                    or not isinstance(group.get("title"), str)
                    or not isinstance(group.get("visible"), bool)
                    or not isinstance(group.get("items"), list)):
                raise ValueError("Her grup id, title, visible ve items icermeli.")
            if group["id"] in ids:
                raise ValueError("Grup id degerleri benzersiz olmali.")
            ids.add(group["id"])
            for item in group["items"]:
                if (not isinstance(item, dict) or not isinstance(item.get("key"), str)
                        or not isinstance(item.get("visible"), bool)):
                    raise ValueError("Her alan key ve visible icermeli.")
                if item["key"] in keys:
                    raise ValueError(f"Alan birden fazla grupta: {item['key']}")
                keys.add(item["key"])
        tables = data.get("tables", {})
        if not isinstance(tables, dict):
            raise ValueError("tables bir nesne olmali.")
        modes = ("All", "Custom", "Fiyatlar", "Sinyaller", "Bakiye / Getiri", "Pozisyon", "Komisyon", "Emirler")
        for name in ("lists", "performance"):
            if name not in tables:
                continue
            settings = tables[name]
            if (not isinstance(settings, dict) or settings.get("columnMode") not in modes
                    or not isinstance(settings.get("columns"), dict)
                    or not all(isinstance(value, bool) for value in settings["columns"].values())):
                raise ValueError(f"tables.{name}: columnMode ve columns gecersiz.")

    def save(self):
        # Never overwrite a malformed file while the user is editing it.
        if not self._valid:
            return
        temp = self.path.with_suffix(".json.tmp")
        try:
            self.path.parent.mkdir(parents=True, exist_ok=True)
            temp.write_text(json.dumps(self.data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            temp.replace(self.path)
            self.error = ""
        except OSError as exc:
            self.error = f"Rapor ayarlari kaydedilemedi: {exc}"

    def sync(self, groups, legacyOrder=()):
        """Append new fields without replacing custom groups, order or visibility."""
        configured = self.data["groups"]
        known = {item["key"] for group in configured for item in group["items"]}
        byId = {group["id"]: group for group in configured}
        priority = ["Bakiye ve getiriler", "Risk, drawdown ve performans",
                    "Kar ve zarar", "Islem sayilari ve sonuclari"]
        order = list(dict.fromkeys(list(legacyOrder) + priority + list(groups)))
        changed = False
        for name in order:
            for key, _ in groups.get(name, []):
                if key in known:
                    continue
                if name not in byId:
                    group = {"id": name, "title": name, "visible": True, "items": []}
                    configured.append(group)
                    byId[name] = group
                byId[name]["items"].append({"key": key, "visible": True})
                known.add(key)
                changed = True
        if changed:
            self.save()

    def rows(self, groups, query=""):
        values = dict(row for rows in groups.values() for row in rows)
        mode = self.data["mode"]
        result = []
        if mode == "HideAll":
            return result
        query = query.strip().casefold()
        for group in self.data["groups"]:
            if mode == "Custom" and not group["visible"]:
                continue
            rows = []
            for item in group["items"]:
                key = item["key"]
                if key not in values or (mode == "Custom" and not item["visible"]):
                    continue
                value = values[key]
                if not query or any(query in cell.casefold() for cell in (group["title"], key, value)):
                    rows.append((key, value))
            if rows:
                result.append((group, rows))
        return result
