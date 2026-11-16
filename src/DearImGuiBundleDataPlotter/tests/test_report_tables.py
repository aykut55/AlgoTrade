import json
from pathlib import Path
import sys
import tempfile
import unittest
from threading import Event

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from src.plotting.reportConfig import ReportConfig
from src.plotting.reportTableData import PagedReportData, readHeader, scanReport
from src.plotting.reportTableView import ReportTableView


class ReportTableTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.path = self.root / "performance.txt"
        self.path.write_bytes((
            "\ufeffSINGLE TRADER PERFORMANS\r\nGenerated: today\r\n"
            "No | Yon | Açılış Tarihi | Açılış Saati | Kar/Zarar\r\n"
            "------------------------\r\n"
            "1 | A | 2026.01.01 | 10:30:00 | 2.50\r\n"
            "2 | S | 2026.01.02 | 11:30:00 | -10.00\r\n"
            "3 | A | 2026.01.03 | 12:30:00 | 100.00\r\n"
            "4 | S | 2026.01.04 | 13:30:00 | 0.00\r\n"
            "END OF REPORT\r\n").encode("utf-8"))

    def scan(self, filters=("", "", "", "", ""), sort=None):
        headers, start = readHeader(self.path)
        return scanReport(self.path, headers, start, filters, sort, Event())

    def rows(self, offsets):
        with self.path.open("rb") as stream:
            result = []
            for offset in offsets:
                stream.seek(offset)
                result.append(stream.readline().decode("utf-8").strip())
            return result

    def test_binary_offsets_and_numeric_sort(self):
        offsets, count = self.scan(sort=(4, False))
        self.assertEqual(count, 4)
        self.assertEqual([row[0] for row in self.rows(offsets)], ["2", "4", "1", "3"])

    def test_filters_apply_to_entire_file(self):
        offsets, count = self.scan(("12:30", "2026.01.02", "2026.01.03", "A", "Karli"))
        self.assertEqual(count, 4)
        self.assertEqual(len(offsets), 1)
        self.assertTrue(self.rows(offsets)[0].startswith("3 |"))
        offsets, _ = self.scan(("", "", "", "S", "Zararli"))
        self.assertTrue(self.rows(offsets)[0].startswith("2 |"))
        offsets, _ = self.scan(("missing", "", "", "", ""))
        self.assertFalse(offsets)

    def test_bounded_pages_and_reset(self):
        data = PagedReportData()
        data.pageSize = 2
        data.open(self.path)
        data.apply()
        data._future.result(timeout=5)
        data.poll()
        self.assertEqual([row[0] for row in data.rows], ["1", "2"])
        data.readPage(999)
        self.assertEqual(data.page, 1)
        self.assertEqual([row[0] for row in data.rows], ["3", "4"])
        data.open(None)
        self.assertFalse(data.rows)
        self.assertFalse(data.headers)
        self.assertFalse(data.offsets)
        self.assertFalse(data.busy)

    def test_cancel_and_bad_header(self):
        headers, start = readHeader(self.path)
        cancel = Event(); cancel.set()
        self.assertIsNone(scanReport(self.path, headers, start, ("", "", "", "", ""), None, cancel))
        self.path.write_text("not a report")
        with self.assertRaises(ValueError):
            readHeader(self.path)

    def test_custom_columns_preserve_statistics_config(self):
        path = self.root / "config.json"
        original = {"mode": "Custom", "groups": [
            {"id": "a", "title": "Stats", "visible": False,
             "items": [{"key": "Bakiye", "visible": True}]}]}
        path.write_text(json.dumps(original))
        config = ReportConfig(path)
        view = ReportTableView(config)
        view.data.headers = ["BarNo", "Date", "Time", "Open", "EmirStatus"]
        view._syncColumns()
        self.assertEqual(view._visibleColumns(), [0, 1, 2, 3])
        settings = view._settings()
        settings["columnMode"] = "Custom"
        settings["columns"]["Open"] = False
        config.save()
        self.assertEqual(view._visibleColumns(), [0, 1, 2, 4])
        reloaded = ReportConfig(path)
        self.assertEqual(reloaded.data["groups"], original["groups"])
        self.assertEqual(reloaded.data["mode"], "Custom")
        self.assertFalse(reloaded.data["tables"]["lists"]["columns"]["Open"])

    def test_invalid_table_settings_do_not_overwrite_file(self):
        path = self.root / "config.json"
        raw = '{"mode":"ShowAll","groups":[],"tables":{"lists":{"columnMode":"bad","columns":{}}}}'
        path.write_text(raw)
        config = ReportConfig(path)
        self.assertTrue(config.error)
        config.save()
        self.assertEqual(path.read_text(), raw)


if __name__ == "__main__":
    unittest.main()
