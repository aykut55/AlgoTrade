"""
SingleTraderPerformans.csv veya .txt dosyasini satir satir okuyup, verilen filtrelere
(AND ile birlesen) uyan satirlari yeni bir dosyaya yazar. Girdi/cikti formati dosya
uzantisindan (.csv / .txt) otomatik anlasilir. Hic --filter verilmezse DEFAULT_FILTERS
uygulanir (runMe.bat'in argumansiz calistirilma durumu da budur).

ALAN olarak hem gercek Turkce header ("Yön Bazlı Değişim %") hem de ASCII alias
(FIELD_ALIASES, orn. "YonBazliDegisimYuzde") kullanilabilir - .bat dosyalarindan Turkce
karakterli argument gecirmek guvenilir olmadigi icin alias'lar eklendi.

Kullanim ornekleri:
  # Yon Bazli Degisim % >= 2 olanlar (CSV -> CSV)
  python filter_performans.py --filter YonBazliDegisimYuzde ">=" "2"
  python filter_performans.py --filter "Yön Bazlı Değişim %" ">=" "2"

  # Yon Bazli Degisim % <= -1.5 olanlar
  python filter_performans.py --filter YonBazliDegisimYuzde "<=" "-1.5"

  # Ilk yonu A olup Getiri % > 1 olanlar, .txt girdi -> .txt cikti
  python filter_performans.py -i outputs/logs/SingleTraderPerformans.txt -o outputs/logs/filtered.txt --filter Yon "==" "A" --filter GetiriYuzde ">" "1"

  # Farkli girdi/cikti dosyasi ile
  python filter_performans.py -i outputs/logs/SingleTraderPerformans.csv -o outputs/logs/filtered.csv --filter KarZarar "<" "0"

  # Sadece yonu A olanlar
  python filter_performans.py --filter Yon "==" "A"

  # Sadece yonu A olup Yon Bazli Degisim % 3'ten buyuk olanlar
  python filter_performans.py --filter Yon "==" "A" --filter YonBazliDegisimYuzde ">" "3"

  # Sadece yonu S olup Yon Bazli Degisim % 3'ten buyuk olanlar
  # (Yon Bazli Degisim % yone gore duzeltilmistir: short'ta fiyat dusmesi de pozitif gorunur)
  python filter_performans.py --filter Yon "==" "S" --filter YonBazliDegisimYuzde ">" "3"
"""

import argparse
import csv
import operator
import sys
from pathlib import Path

DEFAULT_INPUT = "outputs/logs/SingleTraderPerformans.csv"
DEFAULT_OUTPUT = "outputs/logs/SingleTraderPerformans_filtered.csv"

# Hic --filter verilmezse (orn. runMe.bat argumansiz cagrildiginda) uygulanan varsayilan filtre.
DEFAULT_FILTERS = [("YonBazliDegisimYuzde", ">=", "2")]

# StatisticsExporter.IsLeftAlignedPerformansField ile ayni alanlar (txt ciktisinda hizalama icin).
LEFT_ALIGNED_HEADERS = {"Yon", "Açılış Tarihi", "Açılış Saati", "Kapanış Tarihi", "Kapanış Saati"}

# Turkce ozel karakterli header'lar icin ASCII takma adlar - .bat dosyalari Turkce karakterli
# argumanlari guvenilir parse edemiyor (cmd.exe batch parser UTF-8 ile sorunlu), bu yuzden
# --filter alaninda bu ASCII isimler de kabul edilir.
FIELD_ALIASES = {
    "AcilisBari": "Açılış Barı",
    "AcilisTarihi": "Açılış Tarihi",
    "AcilisSaati": "Açılış Saati",
    "AcilisFiyati": "Açılış Fiyatı",
    "KapanisBari": "Kapanış Barı",
    "KapanisTarihi": "Kapanış Tarihi",
    "KapanisSaati": "Kapanış Saati",
    "KapanisFiyati": "Kapanış Fiyatı",
    "HamDegisimYuzde": "Ham Değişim %",
    "YonBazliDegisimYuzde": "Yön Bazlı Değişim %",
    "KarZarar": "Kar/Zarar",
    "BakiyePuan": "Bakiye Puan",
    "GetiriPuan": "Getiri Puan",
    "GetiriYuzde": "Getiri %",
    "Acik": "Açık",
}

OPERATORS = {
    ">=": operator.ge,
    "<=": operator.le,
    "==": operator.eq,
    "!=": operator.ne,
    ">": operator.gt,
    "<": operator.lt,
}


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("-i", "--input", default=DEFAULT_INPUT, help=f"Kaynak dosya, .csv veya .txt (varsayilan: {DEFAULT_INPUT})")
    parser.add_argument("-o", "--output", default=DEFAULT_OUTPUT, help=f"Hedef dosya, .csv veya .txt (varsayilan: {DEFAULT_OUTPUT})")
    parser.add_argument(
        "--filter",
        action="append",
        nargs=3,
        metavar=("ALAN", "OP", "DEGER"),
        default=[],
        help=f"Tekrarlanabilir filtre: ALAN OP DEGER. OP: {', '.join(OPERATORS)}. Hepsi AND ile birlesir.",
    )
    return parser.parse_args()


def coerce(value: str):
    try:
        return float(value.replace(",", "."))
    except ValueError:
        return value


def row_matches(row: dict, field: str, op: str, raw_value: str) -> bool:
    field = FIELD_ALIASES.get(field, field)
    if field not in row:
        raise SystemExit(f"Bilinmeyen alan: '{field}'. Mevcut alanlar: {', '.join(row.keys())}")

    compare = OPERATORS[op]
    left = coerce(row[field])
    right = coerce(raw_value)

    # Tip uyusmazligi (orn. sayisal alana metin verilmesi) halinde string olarak karsilastir.
    if type(left) is not type(right):
        left, right = str(left), str(right)

    return compare(left, right)


def split_pipe_line(line: str) -> list[str]:
    # Satirlar " | " ile ayrilip sonunda da " |" ile kapatiliyor (StatisticsExporter formati);
    # "|" uzerinden bolup her hucreyi trim etmek, sondaki bos parcayi da eler.
    cells = [cell.strip() for cell in line.split("|")]
    if cells and cells[-1] == "":
        cells.pop()
    return cells


def read_txt(path: Path) -> tuple[list[str], list[dict]]:
    lines = path.read_text(encoding="utf-8-sig").splitlines()

    banner_indices = [i for i, ln in enumerate(lines) if ln.strip() and set(ln.strip()) == {"="}]
    if not banner_indices:
        raise SystemExit(f"'{path}' icinde header banner'i ('===...') bulunamadi.")

    header_idx = banner_indices[0] + 1
    fieldnames = split_pipe_line(lines[header_idx])

    data_start = header_idx + 2  # banner + header + dash-separator
    data_end = banner_indices[1] if len(banner_indices) > 1 else len(lines)

    rows = []
    for ln in lines[data_start:data_end]:
        if not ln.strip() or set(ln.strip()) == {"-"}:
            continue
        values = split_pipe_line(ln)
        rows.append(dict(zip(fieldnames, values)))

    return fieldnames, rows


def read_csv(path: Path) -> tuple[list[str], list[dict]]:
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f, delimiter=";")
        rows = list(reader)
        fieldnames = list(reader.fieldnames or [])
    return fieldnames, rows


def read_rows(path: Path) -> tuple[list[str], list[dict]]:
    if path.suffix.lower() == ".txt":
        return read_txt(path)
    return read_csv(path)


def write_csv(path: Path, fieldnames: list[str], rows: list[dict]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, delimiter=";")
        writer.writeheader()
        writer.writerows(rows)


def write_txt(path: Path, fieldnames: list[str], rows: list[dict]) -> None:
    widths = {
        h: max(len(h), max((len(row.get(h, "")) for row in rows), default=0))
        for h in fieldnames
    }

    def format_row(values: dict) -> str:
        cells = []
        for h in fieldnames:
            text = values.get(h, "")
            cells.append(text.ljust(widths[h]) if h in LEFT_ALIGNED_HEADERS else text.rjust(widths[h]))
        return " | ".join(cells) + " |"

    header_line = format_row({h: h for h in fieldnames})
    line_width = len(header_line)

    with path.open("w", encoding="utf-8-sig", newline="") as f:
        f.write("=" * line_width + "\n")
        f.write(header_line + "\n")
        f.write("-" * line_width + "\n")
        for row in rows:
            f.write(format_row(row) + "\n")
        f.write("=" * line_width + "\n")


def write_rows(path: Path, fieldnames: list[str], rows: list[dict]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.suffix.lower() == ".txt":
        write_txt(path, fieldnames, rows)
    else:
        write_csv(path, fieldnames, rows)


def main():
    args = parse_args()

    if not args.filter:
        args.filter = DEFAULT_FILTERS

    for field, op, _ in args.filter:
        if op not in OPERATORS:
            raise SystemExit(f"Gecersiz operator: '{op}'. Secenekler: {', '.join(OPERATORS)}")

    input_path = Path(args.input)
    if not input_path.exists():
        raise SystemExit(f"Girdi dosyasi bulunamadi: {input_path}")

    fieldnames, rows = read_rows(input_path)

    matched = [
        row for row in rows
        if all(row_matches(row, field, op, value) for field, op, value in args.filter)
    ]

    output_path = Path(args.output)
    write_rows(output_path, fieldnames, matched)

    print(f"{len(matched)}/{len(rows)} satir filtreye uydu -> {output_path}")


if __name__ == "__main__":
    sys.exit(main())
