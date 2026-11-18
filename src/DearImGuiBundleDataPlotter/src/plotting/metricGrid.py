import json
from math import isfinite
from pathlib import Path

from imgui_bundle import imgui, implot


CONFIG_PATH = Path(__file__).resolve().parents[4] / "inputs" / "configs" / "OhlcMetricCells.json"


def attachConfiguredMetricGrid(pm, panel, getMetric, bundleConfig=None):
    if panel is None:
        return

    config = bundleConfig
    try:
        config = json.loads(CONFIG_PATH.read_text(encoding="utf-8-sig"))
    except FileNotFoundError:
        pass
    except (OSError, ValueError) as exc:
        print(f"OHLC metric grid: {CONFIG_PATH.name}: {exc}")
    if config is None:
        return

    try:
        cells = []
        for entry in config["cells"]:
            value = entry["value"] if "value" in entry else getMetric(entry["metric"])
            if value is None:
                continue

            cell = {**entry, "value": format(value, entry.get("format", ""))}
            if isinstance(value, (int, float)) and value < 0 and "negativeValueColor" in entry:
                cell["valueColor"] = entry["negativeValueColor"]
            for key in ("labelColor", "valueColor", "backgroundColor"):
                if key in cell and (not isinstance(cell[key], list) or len(cell[key]) != 4 or
                                    not all(isinstance(channel, int) and 0 <= channel <= 255 for channel in cell[key])):
                    raise ValueError(f"{key} must contain four integers between 0 and 255.")
            cells.append(cell)

        attachMetricGrid(pm, panel, cells, inline=config.get("inline", True),
                         cellWidth=config.get("cellWidth"))
    except (OSError, TypeError, KeyError, ValueError, AttributeError) as exc:
        print(f"OHLC metric grid: {CONFIG_PATH.name}: {exc}")


def attachMetricGrid(pm, panel, cells, *, inline=False, cellWidth=None):
    """Place cells at one-based row and column coordinates inside the plot."""
    if panel is None:
        return
    if cellWidth is not None and (
        not isinstance(cellWidth, (int, float)) or not isfinite(cellWidth) or cellWidth <= 0
    ):
        raise ValueError("cellWidth must be a positive number.")
    placed = {}
    for cell in cells:
        row, column = cell["row"], cell["column"]
        if not isinstance(row, int) or not isinstance(column, int) or row < 1 or column < 1:
            raise ValueError("Metric cell row and column must be positive integers.")
        width = cell.get("width", cellWidth)
        if width is not None and (not isinstance(width, (int, float)) or not isfinite(width) or width <= 0):
            raise ValueError("Cell width must be a positive number.")
        if cell["value"] is not None:
            placed[row, column] = (
                str(cell["label"]), str(cell["value"]),
                cell.get("labelColor", (194, 207, 222, 255)),
                cell.get("valueColor", (247, 250, 255, 255)),
                cell.get("backgroundColor", (0, 0, 0, 0)),
                width,
            )
    if not placed:
        return

    columnCount = max(column for _, column in placed)

    def drawMetricGrid(_pm, _panelId, _panel):
        pos, size = implot.get_plot_pos(), implot.get_plot_size()
        margin, gap, padding = 8, 4, 6
        availableWidth = size.x - 2 * margin - (columnCount - 1) * gap
        if availableWidth <= 0:
            return

        columnWidths = {}
        measured = {}
        for (row, column), (label, value, labelColor, valueColor, backgroundColor, width) in placed.items():
            labelText = f"{label} : " if inline else label
            labelWidth = imgui.calc_text_size(labelText).x
            valueWidth = imgui.calc_text_size(value).x
            textWidth = labelWidth + valueWidth if inline else max(labelWidth, valueWidth)
            width = width if width is not None else textWidth + 2 * padding
            columnWidths[column] = max(columnWidths.get(column, 0), width)
            measured[row, column] = labelText, labelWidth, value, labelColor, valueColor, backgroundColor, width

        emptyColumnWidth = cellWidth if cellWidth is not None else min(columnWidths.values())
        widths = [columnWidths.get(column, emptyColumnWidth) for column in range(1, columnCount + 1)]
        scale = min(1, availableWidth / sum(widths))
        x = pos.x + size.x - margin - sum(widths) * scale - (columnCount - 1) * gap
        columnStarts = []
        for width in widths:
            columnStarts.append(x)
            x += width * scale + gap

        textHeight = imgui.get_text_line_height()
        height = textHeight * (1 if inline else 2) + 10
        top = pos.y + margin
        draw = implot.get_plot_draw_list()
        implot.push_plot_clip_rect()
        try:
            for (row, column), (labelText, labelWidth, value, labelColor, valueColor, backgroundColor, width) in measured.items():
                x = columnStarts[column - 1]
                y = top + (row - 1) * (height + gap)
                lo, hi = (x, y), (x + width * scale, y + height)
                if backgroundColor[3]:
                    background = imgui.color_convert_float4_to_u32(tuple(channel / 255 for channel in backgroundColor))
                    draw.add_rect_filled(lo, hi, background, 4)
                draw.push_clip_rect(lo, hi, True)
                try:
                    labelX, labelY = x + padding, y + 5
                    labelTint = imgui.color_convert_float4_to_u32(tuple(channel / 255 for channel in labelColor))
                    valueTint = imgui.color_convert_float4_to_u32(tuple(channel / 255 for channel in valueColor))
                    draw.add_text((labelX, labelY), labelTint, labelText)
                    if inline:
                        valuePos = (labelX + labelWidth, labelY)
                    else:
                        valuePos = (labelX, labelY + textHeight)
                    draw.add_text(valuePos, valueTint, value)
                finally:
                    draw.pop_clip_rect()
        finally:
            implot.pop_plot_clip_rect()

    pm.addPanelDrawHook(panel.id, drawMetricGrid)
