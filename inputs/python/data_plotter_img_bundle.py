"""
Multi-panel data plotter with Level of Detail (LOD) support for all data types.
Provides synchronized pan/zoom/crosshair across all panels.
"""

from enum import Enum, auto
from typing import Any, Dict, List, Tuple, Optional, Callable
import numpy as np
from imgui_bundle import imgui, implot, immapp, ImVec2
from dataclasses import dataclass


class DataType(Enum):
    """Enumeration of supported data types for plotting."""
    OHLC = auto()           # Candlestick data (Open, High, Low, Close)
    Volume = auto()         # Volume bars
    Line = auto()           # Single line (MA, RSI, etc.)
    Bands = auto()          # Upper/Lower bands (Bollinger Bands)
    Levels = auto()         # Horizontal reference lines
    Histogram = auto()      # Bar chart (MACD histogram, etc.)
    TradeSignals = auto()   # Buy/sell markers (scatter points)
    Stairs = auto()         # Step/stairs line (trade signal states: -1, 0, 1)
    PnL = auto()            # Profit/Loss line
    Balance = auto()        # Balance line


@dataclass
class DataItem:
    """Represents a single data series in a panel."""
    index: int              # Ordering index
    data_type: DataType     # Type of data
    data: Any               # Actual data (format depends on data_type)
    label: str              # Label for legend
    color: Optional[Tuple[float, float, float, float]] = None  # RGBA color


class _ArrayOHLCReader:
    """Lightweight adapter exposing an .ohlc property for array sources."""
    def __init__(self, ohlc_array: np.ndarray):
        self._ohlc = np.array(ohlc_array, dtype=np.float64).reshape(-1, 4)

    @property
    def ohlc(self) -> np.ndarray:
        return self._ohlc

    @property
    def bars(self):
        # Optional: empty bars to satisfy access; date labels should come from shared labels
        return []


class Panel:
    """
    Represents a single panel (subplot) that can contain multiple data series.
    Supports LOD (Level of Detail) rendering for all data types.
    """

    def __init__(self, index: int):
        self.index = index
        self.data_items: List[DataItem] = []
        self.ohlc_data = None  # Special storage for OHLC data from CSVReader
        self.ohlc_array: Optional[np.ndarray] = None  # Direct OHLC array storage (N,4)
        self.title = f"Panel {index}"
        self.y_axis_label = ""
        self.height_ratio = 1.0  # Relative height (1.0 = standard)
        # Absolute sizing (used in scroll mode when requested)
        self.size_mode: str = "ratio"  # 'ratio' or 'absolute'
        self.fixed_height_px: Optional[float] = None
        self.fixed_width_px: Optional[float] = None
        # Optional external datetime labels (e.g., ["YYYY.MM.DD HH:MM:SS", ...])
        self.datetime_labels: Optional[List[str]] = None
        # Info panel positioning (relative to plot origin)
        self.info_panel_dx: float = 100.0
        self.info_panel_dy: float = 2.0
        self.info_label_dx: float = 5.0
        self.info_value_dx: float = 70.0
        # LOD info positioning (relative to plot origin)
        self.lod_info_dx: float = 8.0
        self.lod_info_dy: float = 0.0  # If 0, we auto-place below plot; else use relative
        # Y-axis sync group (panels in same group share Y limits)
        self.y_sync_group: Optional[int] = None
        # X-axis sync group (panels in same group share X limits)
        self.x_sync_group: Optional[int] = None
        # Internal event tracking for pan/drag/select
        self._pan_active_left: bool = False
        self._drag_active_right: bool = False
        self._select_active_middle: bool = False

    def setTitle(self, title: str):
        """Set panel title."""
        self.title = title

    def setYAxisLabel(self, label: str):
        """Set Y-axis label."""
        self.y_axis_label = label

    def setHeightRatio(self, ratio: float):
        """Set relative height (default is 1.0)."""
        self.height_ratio = float(ratio)
        self.size_mode = "ratio"

    # Backwards/typo compatibility: allow setHeghtRato(...) calls
    def setHeghtRato(self, ratio: float):  # noqa: N802 - preserve user's call style
        """Alias for setHeightRatio for typo compatibility."""
        self.setHeightRatio(ratio)

    # Absolute sizing APIs for scroll mode or explicit control
    def setHeight(self, height_px: float):
        """Set fixed height in pixels (activates absolute sizing mode)."""
        self.fixed_height_px = float(height_px)
        self.size_mode = "absolute"

    def setWidth(self, width_px: float):
        """Set fixed width in pixels (used in scroll mode)."""
        self.fixed_width_px = float(width_px)
        self.size_mode = "absolute"

    # Typos compatibility requested by user
    def setHeght(self, height_px: float):  # noqa: N802
        self.setHeight(height_px)

    def setWdth(self, width_px: float):  # noqa: N802
        self.setWidth(width_px)

    def setOHLCData(self, csv_reader):
        """Set OHLC data from CSVReader instance."""
        self.ohlc_data = csv_reader
        self.ohlc_array = None

    def setOHLC(self, ohlc: np.ndarray):
        """Set OHLC data directly as array with shape (N,4) [Open, High, Low, Close]."""
        self.ohlc_array = np.array(ohlc, dtype=np.float64).reshape(-1, 4)
        self.ohlc_data = None

    # Info panel positioning setters
    def setInfoPanelPosition(self, dx: float, dy: float):
        """Set info panel position relative to plot origin (pixels)."""
        self.info_panel_dx = float(dx)
        self.info_panel_dy = float(dy)

    def setInfoPanelOffsets(self, label_dx: Optional[float] = None, value_dx: Optional[float] = None):
        """Set label/value x offsets within the info panel (pixels)."""
        if label_dx is not None:
            self.info_label_dx = float(label_dx)
        if value_dx is not None:
            self.info_value_dx = float(value_dx)

    def setYSyncGroup(self, group_id: int):
        """Set Y-axis sync group. Panels in same group share Y-axis limits."""
        self.y_sync_group = group_id

    def setXSyncGroup(self, group_id: int):
        """Set X-axis sync group. Panels in same group share X-axis limits."""
        self.x_sync_group = group_id

    # LOD info positioning setter
    def setLODPanelInfoPosition(self, dx: float, dy: float):
        """Set LOD info position relative to plot origin (pixels)."""
        self.lod_info_dx = float(dx)
        self.lod_info_dy = float(dy)

    # ========================
    # Info Panel Builders
    # ========================
    def BuildPanelInfo(self, idx: int, ohlc_src: Optional[np.ndarray], shared_labels: Optional[List[str]], shared_crosshair: Optional[Dict[str, Any]]):
        """
        Draw a small info panel for this panel, tailored to its content.

        - Always shows Date and BarIndex
        - If OHLC data is present, shows O/H/L/C + Volume/Lot + Diff
        - Else, shows values from the panel's own data series (Line, Histogram, Volume, etc.)
        """
        try:
            # idx is already passed from caller, use it directly
            date_str = self._fmt_time_label(idx, shared_labels)

            dl = implot.get_plot_draw_list()
            plot_pos = implot.get_plot_pos()
            panel_x = plot_pos.x + float(getattr(self, 'info_panel_dx', 100.0))
            panel_y = plot_pos.y + float(getattr(self, 'info_panel_dy', 2.0))
            label_x = panel_x + float(getattr(self, 'info_label_dx', 5.0))
            value_x = panel_x + float(getattr(self, 'info_value_dx', 70.0))
            row_y = panel_y + 5

            # Date
            dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, "Date")
            dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, f" : {date_str}")
            row_y += 16

            drew_any = False

            # Prefer OHLC layout if available
            try:
                if ohlc_src is not None and 0 <= idx < len(ohlc_src):
                    row_y = self.BuildPanelInfoOHLC(idx, ohlc_src, shared_crosshair, dl, label_x, value_x, row_y)
                    drew_any = True
            except Exception as e:
                try:
                    print(f"[DEBUG] BuildPanelInfoOHLC failed at idx={idx}: {e}")
                except Exception:
                    pass

            # If not OHLC, render based on panel data types
            if not drew_any:
                # Show values per data series present in this panel
                for item in self.data_items:
                    try:
                        t = item.data_type
                        if t == DataType.Volume or t == DataType.Histogram:
                            row_y = self.BuildPanelInfoVolume(idx, item, dl, label_x, value_x, row_y)
                        elif t == DataType.Line:
                            row_y = self.BuildPanelInfoLine(idx, item, dl, label_x, value_x, row_y)
                        elif t == DataType.Histogram:
                            # handled in Volume/Histogram branch above
                            pass
                        elif t == DataType.TradeSignals:
                            row_y = self.BuildPanelInfoSignals(idx, item, dl, label_x, value_x, row_y)
                        elif t == DataType.Stairs:
                            row_y = self.BuildPanelInfoStairs(idx, item, dl, label_x, value_x, row_y)
                        elif t == DataType.Levels:
                            # Optional: show constant level(s)
                            row_y = self.BuildPanelInfoLevels(item, dl, label_x, value_x, row_y)
                    except Exception as e:
                        try:
                            print(f"[DEBUG] Build info for {item.label} failed at idx={idx}: {e}")
                        except Exception:
                            pass

            # BarIndex
            dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, "BarIndex")
            try:
                dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, f" : {idx}")
            except Exception as e:
                try:
                    print(f"[DEBUG] Failed to draw BarIndex at idx={idx}: {e}")
                except Exception:
                    pass
        except Exception as e:
            try:
                print(f"[DEBUG] BuildPanelInfo error: {e}")
            except Exception:
                pass

    def BuildPanelInfoOHLC(self, idx: int, ohlc_src: np.ndarray, shared_crosshair: Dict[str, Any], dl, label_x: float, value_x: float, row_y: float) -> float:
        """Draw OHLC specific info and return updated row_y."""
        try:
            o, h, l, c = [float(v) for v in ohlc_src[idx]]
        except Exception:
            return row_y

        dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, "Open")
        dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, f" : {o:.2f}")
        row_y += 16
        dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, "High")
        dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, f" : {h:.2f}")
        row_y += 16
        dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, "Low")
        dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, f" : {l:.2f}")
        row_y += 16
        dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, "Close")
        dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, f" : {c:.2f}")
        row_y += 16

        # Volume
        vol = None
        try:
            vsrc = shared_crosshair.get("volume") if shared_crosshair else None
            if vsrc is not None and 0 <= idx < len(vsrc):
                vol = float(vsrc[idx])
        except Exception:
            vol = None
        dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, "Volume")
        if vol is not None:
            dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, f" : {int(vol)}")
        else:
            dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, " : N/A")
        row_y += 16

        # Lot
        lot = None
        try:
            lsrc = shared_crosshair.get("lot") if shared_crosshair else None
            if lsrc is not None and 0 <= idx < len(lsrc):
                lot = float(lsrc[idx])
        except Exception:
            lot = None
        dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, "Lot")
        if lot is not None:
            dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, f" : {int(lot)}")
        else:
            dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, " : N/A")
        row_y += 16

        # Diff
        delta = None
        delta_pct = None
        try:
            dsrc = shared_crosshair.get("delta") if shared_crosshair else None
            psrc = shared_crosshair.get("delta_pct") if shared_crosshair else None
            if dsrc is not None and 0 <= idx < len(dsrc):
                delta = float(dsrc[idx])
            if psrc is not None and 0 <= idx < len(psrc):
                delta_pct = float(psrc[idx])
        except Exception:
            pass
        # Fallback compute
        if delta is None and (o is not None and c is not None):
            try:
                delta = float(c - o)
                delta_pct = float((delta / o) * 100.0) if o != 0 else 0.0
            except Exception:
                delta = None
        dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, "Diff")
        if delta is not None:
            sign = "+" if delta >= 0 else ""
            pct_txt = f" ({sign}{delta_pct:.2f}%)" if delta_pct is not None else ""
            diff_text = f" : {sign}{delta:.2f}{pct_txt}"
            dl.add_text(ImVec2(value_x, row_y), 0xFFFFFFFF, diff_text)
        else:
            dl.add_text(ImVec2(value_x, row_y), 0xFFFFFFFF, " : N/A")
        row_y += 16
        return row_y

    def BuildPanelInfoVolume(self, idx: int, item: 'DataItem', dl, label_x: float, value_x: float, row_y: float) -> float:
        """Show the value of a Volume or Histogram item at idx (single item)."""
        try:
            arr = np.array(item.data).reshape(-1)
            val = None
            if 0 <= idx < len(arr):
                val = float(arr[idx])
            label = item.label or ("Histogram" if item.data_type == DataType.Histogram else "Volume")
            if item.data_type == DataType.Volume:
                txt = f" : {int(val)}" if val is not None else " : N/A"
            else:
                txt = f" : {val:.2f}" if val is not None else " : N/A"
            dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, label)
            dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, txt)
            row_y += 16
        except Exception:
            pass
        return row_y

    def BuildPanelInfoLine(self, idx: int, item: 'DataItem', dl, label_x: float, value_x: float, row_y: float) -> float:
        try:
            arr = np.array(item.data).reshape(-1)
            val = None
            if 0 <= idx < len(arr):
                val = float(arr[idx])
            dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, item.label)
            dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, f" : {val:.2f}" if val is not None else " : N/A")
            row_y += 16
        except Exception:
            pass
        return row_y

    def BuildPanelInfoHistogram(self, idx: int, item: 'DataItem', dl, label_x: float, value_x: float, row_y: float) -> float:
        try:
            arr = np.array(item.data).reshape(-1)
            val = None
            if 0 <= idx < len(arr):
                val = float(arr[idx])
            dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, item.label)
            dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, f" : {val:.2f}" if val is not None else " : N/A")
            row_y += 16
        except Exception:
            pass
        return row_y

    def BuildPanelInfoSignals(self, idx: int, item: 'DataItem', dl, label_x: float, value_x: float, row_y: float) -> float:
        try:
            x_indices, y_values, signal_types = item.data
            x_indices = np.array(x_indices).astype(int)
            y_values = np.array(y_values).reshape(-1)
            signal_types = np.array(signal_types).astype(int)
            match = np.where(x_indices == idx)[0]
            if len(match) > 0:
                i = int(match[0])
                sig = int(signal_types[i])
                sig_text = "BUY" if sig == 1 else ("SELL" if sig == -1 else str(sig))
                price = float(y_values[i]) if 0 <= i < len(y_values) else None
                dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, f"Signal")
                dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, f" : {sig_text} @ {price:.2f}" if price is not None else f" : {sig_text}")
                row_y += 16
        except Exception:
            pass
        return row_y

    def BuildPanelInfoLevels(self, item: 'DataItem', dl, label_x: float, value_x: float, row_y: float) -> float:
        # Skip Levels in RSI panel as requested
        try:
            title = (self.title or "").upper()
            ylab = (self.y_axis_label or "").upper()
            if "RSI" in title or ylab == "RSI":
                return row_y
            levels = list(item.data)
            dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, item.label or "Levels")
            dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, " : " + ", ".join(f"{float(v):.2f}" for v in levels))
            row_y += 16
        except Exception:
            pass
        return row_y

    def BuildPanelInfoStairs(self, idx: int, item: 'DataItem', dl, label_x: float, value_x: float, row_y: float) -> float:
        """Show the original (unscaled) value of a Stairs item at idx."""
        try:
            arr = np.array(item.data).reshape(-1)
            val = None
            if 0 <= idx < len(arr):
                val = int(arr[idx])  # Stairs values are typically integers (-1, 0, 1)
            
            # Map to human-readable labels if it's a trade signal
            if val == 1:
                val_str = "LONG (1)"
            elif val == -1:
                val_str = "SHORT (-1)"
            elif val == 0:
                val_str = "FLAT (0)"
            else:
                val_str = str(val) if val is not None else "N/A"
            
            dl.add_text(ImVec2(label_x, row_y), 0xFFFFFFFF, item.label)
            dl.add_text(imgui.ImVec2(value_x, row_y), 0xFFFFFFFF, f" : {val_str}")
            row_y += 16
        except Exception:
            pass
        return row_y

    def BuildLODPanelInfo(self, visible_bars: int, plotted_bars: int) -> None:
        """Draw a small LOD info box at configured position for this panel."""
        try:
            dl = implot.get_plot_draw_list()
            plot_pos = implot.get_plot_pos()
            plot_size = implot.get_plot_size()

            # Text and color
            lod_active = plotted_bars > 0 and plotted_bars < max(1, visible_bars)
            if lod_active:
                lod_text = f"LOD Active  Visible: {visible_bars}  Plotted: {plotted_bars}"
                lod_color = 0xFF00FF00  # Green
            else:
                lod_text = f"Full Detail  Bars: {visible_bars}"
                lod_color = 0xFF00FFFF  # Yellow

            # Position: relative to plot origin; if dy==0, auto-place below plot
            base_x = plot_pos.x + float(getattr(self, 'lod_info_dx', 8.0))
            if float(getattr(self, 'lod_info_dy', 0.0)) == 0.0:
                base_y = plot_pos.y + plot_size.y + 24.0
            else:
                base_y = plot_pos.y + float(getattr(self, 'lod_info_dy', 0.0))

            lod_pos = ImVec2(base_x, base_y)
            text_size = imgui.calc_text_size(lod_text)
            bg_min = ImVec2(base_x - 5, base_y - 2)
            bg_max = ImVec2(base_x + text_size.x + 5, base_y + text_size.y + 2)

            # Background rounded rect and text
            dl.add_rect_filled(bg_min, bg_max, 0xAA000000, 4)
            dl.add_text(lod_pos, lod_color, lod_text)
        except Exception:
            pass

    def BuildLODPanelInfoBottomRight(self, visible_bars: int, plotted_bars: int) -> None:
        """Draw LOD info box at bottom-right inside the plot area for this panel."""
        try:
            dl = implot.get_plot_draw_list()
            plot_pos = implot.get_plot_pos()
            plot_size = implot.get_plot_size()

            lod_active = plotted_bars > 0 and plotted_bars < max(1, visible_bars)
            if lod_active:
                lod_text = f"LOD Active  Visible: {visible_bars}  Plotted: {plotted_bars}"
                lod_color = 0xFF00FF00
            else:
                lod_text = f"Full Detail  Bars: {visible_bars}"
                lod_color = 0xFF00FFFF

            text_size = imgui.calc_text_size(lod_text)
            margin = 8.0
            base_x = plot_pos.x + plot_size.x - text_size.x - margin
            base_y = plot_pos.y + plot_size.y - text_size.y - margin
            lod_pos = ImVec2(base_x, base_y)
            bg_min = ImVec2(base_x - 5, base_y - 2)
            bg_max = ImVec2(base_x + text_size.x + 5, base_y + text_size.y + 2)
            dl.add_rect_filled(bg_min, bg_max, 0xAA000000, 4)
            dl.add_text(lod_pos, lod_color, lod_text)
        except Exception:
            pass

    def _fmt_time_label(self, idx: int, shared_labels: Optional[List[str]] = None) -> str:
        """Format x-axis label from CSVReader date/time if available."""
        try:
            # Prefer explicit datetime labels if provided
            if self.datetime_labels is not None and 0 <= idx < len(self.datetime_labels):
                return str(self.datetime_labels[idx])
            if shared_labels is not None and 0 <= idx < len(shared_labels):
                return str(shared_labels[idx])
            # C# verileriyle çalışıyoruz - bars yok
                b = self.ohlc_data.bars[idx]
                # Expecting b.date like YYYY.MM.DD and b.time like HH:MM:SS
                return f"{b.date} {b.time}"
        except Exception:
            pass
        return str(idx)

    def setDateTime(self, labels: Any):
        """Set pre-formatted datetime labels for X-axis.

        labels can be a list/array of strings like "YYYY.MM.DD HH:MM:SS" matching data length.
        """
        try:
            if labels is None:
                self.datetime_labels = None
                return
            # Convert to list of strings
            if isinstance(labels, np.ndarray):
                self.datetime_labels = [str(x) for x in labels.tolist()]
            else:
                self.datetime_labels = [str(x) for x in list(labels)]
        except Exception:
            # Fallback: store nothing
            self.datetime_labels = None

    def setData(self, index: int, data_type: DataType, data: Any, label: str = "", color: Optional[Tuple] = None):
        """
        Add data to panel.

        Args:
            index: Ordering index for this data series
            data_type: Type of data (from DataType enum)
            data: Actual data - format depends on data_type:
                - OHLC: Not used (use setOHLCData instead)
                - Volume: np.ndarray of volume values
                - Line/PnL/Balance: np.ndarray of Y values
                - Bands: tuple of (upper_array, lower_array)
                - Levels: list of Y values for horizontal lines
                - Histogram: np.ndarray of values (can be positive/negative)
                - TradeSignals: tuple of (x_indices, y_values, signal_types)
            label: Label for legend
            color: Optional RGBA tuple (0.0-1.0)
        """
        item = DataItem(index, data_type, data, label, color)
        self.data_items.append(item)
        # Sort by index to maintain order
        self.data_items.sort(key=lambda x: x.index)

    def _calculate_lod_ohlc(self, time_data: np.ndarray, ohlc: np.ndarray,
                            visible_range: Tuple[float, float], plot_width_pixels: float) -> Tuple[np.ndarray, np.ndarray]:
        """
        Calculate LOD for OHLC data (from existing plotOhlcDataWithLOD logic).

        Returns:
            Tuple of (lod_time, lod_ohlc)
        """
        x_min, x_max = visible_range

        # Filter visible range
        visible_mask = (time_data >= x_min) & (time_data <= x_max)
        visible_indices = np.where(visible_mask)[0]

        if len(visible_indices) == 0:
            return np.array([]), np.array([]).reshape(0, 4)

        visible_time = time_data[visible_indices]
        visible_ohlc = ohlc[visible_indices]

        # Calculate bars per pixel
        num_visible_bars = len(visible_time)
        bars_per_pixel = num_visible_bars / plot_width_pixels if plot_width_pixels > 0 else 0

        # LOD threshold
        if bars_per_pixel <= 1.0:
            return visible_time, visible_ohlc

        # Downsampling
        target_bars = int(plot_width_pixels * 1.5)
        step = max(1, num_visible_bars // target_bars)

        lod_time = []
        lod_ohlc = []

        for i in range(0, num_visible_bars, step):
            chunk_end = min(i + step, num_visible_bars)
            chunk = visible_ohlc[i:chunk_end]

            # Aggregate: first open, max high, min low, last close
            o = chunk[0, 0]
            h = np.max(chunk[:, 1])
            l = np.min(chunk[:, 2])
            c = chunk[-1, 3]

            lod_time.append(visible_time[i])
            lod_ohlc.append([o, h, l, c])

        return np.array(lod_time), np.array(lod_ohlc)

    def _calculate_lod_line(self, x_data: np.ndarray, y_data: np.ndarray,
                            visible_range: Tuple[float, float], plot_width_pixels: float) -> Tuple[np.ndarray, np.ndarray]:
        """
        Calculate LOD for line data using min-max preserving downsampling.

        Returns:
            Tuple of (lod_x, lod_y)
        """
        # # Ensure numpy arrays (handle callers passing Python lists)
        # try:
        #     x_data = np.asarray(x_data, dtype=np.float64)
        # except Exception:
        #     x_data = np.array([], dtype=np.float64)
        # try:
        #     y_data = np.asarray(y_data, dtype=np.float64)
        # except Exception:
        #     y_data = np.array([], dtype=np.float64)

        x_min, x_max = visible_range

        # Filter visible range and NaN values
        visible_mask = (x_data >= x_min) & (x_data <= x_max) & (~np.isnan(y_data))
        visible_indices = np.where(visible_mask)[0]

        if len(visible_indices) == 0:
            return np.array([]), np.array([])

        visible_x = x_data[visible_indices]
        visible_y = y_data[visible_indices]

        # Calculate points per pixel
        num_visible_points = len(visible_x)
        points_per_pixel = num_visible_points / plot_width_pixels if plot_width_pixels > 0 else 0

        # LOD threshold
        if points_per_pixel <= 2.0:
            return visible_x, visible_y

        # Downsampling with min-max preservation
        target_points = int(plot_width_pixels * 2.0)
        step = max(1, num_visible_points // target_points)

        lod_x = []
        lod_y = []

        for i in range(0, num_visible_points, step):
            chunk_end = min(i + step, num_visible_points)
            chunk_y = visible_y[i:chunk_end]

            # Add min and max points to preserve shape
            min_idx = i + np.argmin(chunk_y)
            max_idx = i + np.argmax(chunk_y)

            # Add in order
            if min_idx < max_idx:
                lod_x.extend([visible_x[min_idx], visible_x[max_idx]])
                lod_y.extend([visible_y[min_idx], visible_y[max_idx]])
            else:
                lod_x.extend([visible_x[max_idx], visible_x[min_idx]])
                lod_y.extend([visible_y[max_idx], visible_y[min_idx]])

        return np.array(lod_x), np.array(lod_y)

    def _calculate_lod_bars(self, x_data: np.ndarray, y_data: np.ndarray,
                           visible_range: Tuple[float, float], plot_width_pixels: float) -> Tuple[np.ndarray, np.ndarray]:
        """
        Calculate LOD for bar data (volume, histogram) using max preserving downsampling.

        Returns:
            Tuple of (lod_x, lod_y)
        """
        # # Ensure numpy arrays (handle callers passing Python lists)
        # try:
        #     x_data = np.asarray(x_data, dtype=np.float64)
        # except Exception:
        #     x_data = np.array([], dtype=np.float64)
        # try:
        #     y_data = np.asarray(y_data, dtype=np.float64)
        # except Exception:
        #     y_data = np.array([], dtype=np.float64)

        x_min, x_max = visible_range

        # Filter visible range
        visible_mask = (x_data >= x_min) & (x_data <= x_max)
        visible_indices = np.where(visible_mask)[0]

        if len(visible_indices) == 0:
            return np.array([]), np.array([])

        visible_x = x_data[visible_indices]
        visible_y = y_data[visible_indices]

        # Calculate bars per pixel
        num_visible_bars = len(visible_x)
        bars_per_pixel = num_visible_bars / plot_width_pixels if plot_width_pixels > 0 else 0

        # LOD threshold
        if bars_per_pixel <= 1.0:
            return visible_x, visible_y

        # Downsampling - keep max absolute value in each chunk
        target_bars = int(plot_width_pixels * 1.5)
        step = max(1, num_visible_bars // target_bars)

        lod_x = []
        lod_y = []

        for i in range(0, num_visible_bars, step):
            chunk_end = min(i + step, num_visible_bars)
            chunk_y = visible_y[i:chunk_end]

            # Find max absolute value
            max_abs_idx = i + np.argmax(np.abs(chunk_y))

            lod_x.append(visible_x[max_abs_idx])
            lod_y.append(visible_y[max_abs_idx])

        return np.array(lod_x), np.array(lod_y)

    def _calculate_lod_stairs(self, x_data: np.ndarray, y_data: np.ndarray,
                             visible_range: Tuple[float, float], plot_width_pixels: float) -> Tuple[np.ndarray, np.ndarray]:
        """
        Calculate LOD for stairs/step data (trade signals) preserving value transitions.
        Critical for step plots where horizontal-to-vertical transitions must be preserved.

        Returns:
            Tuple of (lod_x, lod_y)
        """
        # # Ensure numpy arrays (handle callers passing Python lists)
        # try:
        #     x_data = np.asarray(x_data, dtype=np.float64)
        # except Exception:
        #     x_data = np.array([], dtype=np.float64)
        # try:
        #     y_data = np.asarray(y_data, dtype=np.float64)
        # except Exception:
        #     y_data = np.array([], dtype=np.float64)

        x_min, x_max = visible_range

        # Filter visible range
        visible_mask = (x_data >= x_min) & (x_data <= x_max)
        visible_indices = np.where(visible_mask)[0]

        if len(visible_indices) == 0:
            return np.array([]), np.array([])

        visible_x = x_data[visible_indices]
        visible_y = y_data[visible_indices]

        # Calculate points per pixel
        num_visible_points = len(visible_x)
        points_per_pixel = num_visible_points / plot_width_pixels if plot_width_pixels > 0 else 0

        # LOD threshold
        if points_per_pixel <= 2.0:
            return visible_x, visible_y

        # Downsampling with value transition preservation
        target_points = int(plot_width_pixels * 2.0)
        step = max(1, num_visible_points // target_points)

        lod_x = []
        lod_y = []

        for i in range(0, num_visible_points, step):
            chunk_end = min(i + step, num_visible_points)
            chunk_y = visible_y[i:chunk_end]

            # For stairs, preserve value changes within chunk
            if len(chunk_y) > 1:
                transitions = np.where(chunk_y[:-1] != chunk_y[1:])[0]
                
                # If too many transitions (noisy data), fallback to Min/Max preservation
                # This prevents generating millions of points for 2M datasets
                if len(transitions) > 4:
                    # Find indices of min and max values to capture the range
                    min_idx = np.argmin(chunk_y)
                    max_idx = np.argmax(chunk_y)
                    
                    indices_to_keep = sorted(list(set([0, min_idx, max_idx, len(chunk_y) - 1])))
                    
                    for idx in indices_to_keep:
                        lod_x.append(visible_x[i + idx])
                        lod_y.append(visible_y[i + idx])
                
                elif len(transitions) > 0:
                    # Include first point, all transition points, and last point
                    indices_to_keep = [0]
                    indices_to_keep.extend(transitions.tolist())
                    indices_to_keep.extend((transitions + 1).tolist())
                    if (len(chunk_y) - 1) not in indices_to_keep:
                        indices_to_keep.append(len(chunk_y) - 1)

                    # Remove duplicates and sort
                    indices_to_keep = sorted(set(indices_to_keep))

                    for idx in indices_to_keep:
                        lod_x.append(visible_x[i + idx])
                        lod_y.append(visible_y[i + idx])
                else:
                    # No transitions in this chunk, just keep first point
                    lod_x.append(visible_x[i])
                    lod_y.append(visible_y[i])
            else:
                # Single point in chunk
                lod_x.append(visible_x[i])
                lod_y.append(visible_y[i])

        return np.array(lod_x), np.array(lod_y)

    def render(
        self,
        time_data: np.ndarray,
        plot_width_pixels: float,
        offset: int,
        visible_count: int,
        needs_update: bool,
        desired_size: Optional[Tuple[float, float]] = None,
        shared_labels: Optional[List[str]] = None,
        shared_crosshair: Optional[Dict[str, Any]] = None,
        shared_lod: Optional[Dict[str, Any]] = None,
        event_handler: Optional[Callable[[Dict[str, Any]], None]] = None,
        panel_x_overrides: Optional[Dict[int, Tuple[int, int]]] = None,
        panel_y_overrides: Optional[Dict[int, Tuple[float, float]]] = None,
        shared_xaxis_enabled: bool = False,
    ):
        """
        Render this panel using ImPlot.

        Args:
            time_data: X-axis data (time/index array from CSVReader)
            plot_width_pixels: Current plot width in pixels for LOD calculation
            offset: Starting index for visible data
            visible_count: Number of visible data points
            needs_update: Flag indicating axes need update
            panel_x_overrides: Per-panel X override dict {panel_idx: (offset, visible_count)}
            panel_y_overrides: Per-panel Y override dict {panel_idx: (y_min, y_max)}
        """
        # Check if this panel has X override (from UpdateOtherPlotsX or hover sync)
        has_x_override = False
        if panel_x_overrides is not None and self.index in panel_x_overrides:
            offset, visible_count = panel_x_overrides[self.index]
            has_x_override = True

        # Begin plot
        plot_flags = (
            implot.Flags_.no_title.value |
            implot.Flags_.no_mouse_text.value
        )

        # Determine plot size: absolute sizing has priority; otherwise use desired_size; else auto (-1)
        width_px = -1.0
        height_px = -1.0
        if self.size_mode == "absolute":
            if self.fixed_width_px is not None:
                width_px = float(self.fixed_width_px)
            if self.fixed_height_px is not None:
                height_px = float(self.fixed_height_px)
        if desired_size is not None:
            dw, dh = desired_size
            if dw is not None:
                width_px = float(dw)
            if dh is not None:
                height_px = float(dh)

        plot_size = imgui.ImVec2(width_px, height_px)
        if not implot.begin_plot(self.title, plot_size, plot_flags):
            return

        # Setup axes
        implot.setup_axis(implot.ImAxis_.x1, "")
        implot.setup_axis(implot.ImAxis_.y1, self.y_axis_label)
        # Set empty numeric format for X (custom labels will be drawn)
        implot.setup_axis_format(implot.ImAxis_.x1, "")

        # Set X-axis limits based on offset and visible_count (indices)
        x_min = float(offset)
        x_max = float(offset + visible_count)

        # Calculate Y-axis limits from ALL visible data in this panel (vectorized, no Python loops over elements)
        y_min = float('inf')
        y_max = float('-inf')

        # Collect Y min/max from OHLC data (from array or reader)
        ohlc_src = None
        if self.ohlc_array is not None:
            ohlc_src = self.ohlc_array
        elif self.ohlc_data is not None:
            ohlc_src = self.ohlc_data.ohlc
        if ohlc_src is not None:
            start_idx = max(0, offset)
            end_idx = min(len(ohlc_src), offset + visible_count)
            if end_idx > start_idx:
                visible_ohlc = ohlc_src[start_idx:end_idx]
                try:
                    y_max = max(y_max, float(np.max(visible_ohlc[:, 1])))  # High
                    y_min = min(y_min, float(np.min(visible_ohlc[:, 2])))  # Low
                except Exception:
                    pass

        # Collect Y min/max from data items
        for item in self.data_items:
            try:
                if item.data_type in [DataType.Line, DataType.PnL, DataType.Balance]:
                    # Single line data
                    start_idx = max(0, offset)
                    end_idx = min(len(item.data), offset + visible_count)
                    if end_idx > start_idx:
                        visible_data = np.asarray(item.data[start_idx:end_idx], dtype=np.float64)
                        if visible_data.size > 0:
                            # Ignore NaNs in min/max
                            if np.any(~np.isnan(visible_data)):
                                y_min = min(y_min, float(np.nanmin(visible_data)))
                                y_max = max(y_max, float(np.nanmax(visible_data)))

                elif item.data_type in [DataType.Volume, DataType.Histogram]:
                    # Bar data
                    start_idx = max(0, offset)
                    end_idx = min(len(item.data), offset + visible_count)
                    if end_idx > start_idx:
                        visible_data = np.asarray(item.data[start_idx:end_idx], dtype=np.float64)
                        if visible_data.size > 0:
                            y_min = min(y_min, float(np.min(visible_data)))
                            y_max = max(y_max, float(np.max(visible_data)))

                elif item.data_type == DataType.Bands:
                    # Bands (upper, lower)
                    upper_data, lower_data = item.data
                    start_idx = max(0, offset)
                    end_idx = min(len(upper_data), offset + visible_count)
                    if end_idx > start_idx:
                        visible_upper = np.asarray(upper_data[start_idx:end_idx], dtype=np.float64)
                        visible_lower = np.asarray(lower_data[start_idx:end_idx], dtype=np.float64)
                        if visible_upper.size > 0:
                            y_max = max(y_max, float(np.nanmax(visible_upper)))
                        if visible_lower.size > 0:
                            y_min = min(y_min, float(np.nanmin(visible_lower)))

                elif item.data_type == DataType.Levels:
                    # Horizontal levels
                    levels = np.asarray(item.data, dtype=np.float64)
                    if levels.size > 0:
                        y_min = min(y_min, float(np.min(levels)))
                        y_max = max(y_max, float(np.max(levels)))
            except Exception:
                pass

        if not np.isfinite(y_min) or not np.isfinite(y_max):
            y_min, y_max = 0.0, 100.0
        y_range = y_max - y_min
        y_padding = y_range * 0.1 if y_range > 0 else 1.0

        # If this panel contains Volume, ensure baseline at 0 (Yaplacak 4)
        try:
            if any(item.data_type == DataType.Volume for item in self.data_items):
                y_min = min(y_min, 0.0)
        except Exception:
            pass

        # Check if this panel has Y override (from UpdateOtherPlotsY)
        if panel_y_overrides is not None and self.index in panel_y_overrides:
            y_min, y_max = panel_y_overrides[self.index]
            y_padding = 0.0  # No padding when using override

        # Apply axis limits
        axis_cond = imgui.Cond_.always if needs_update else imgui.Cond_.once
        # Apply X limits as default window. If override present, force apply once.
        # Using Cond.once for normal updates allows user pan/zoom to persist.
        if has_x_override:
            implot.setup_axis_limits(implot.ImAxis_.x1, x_min, x_max, imgui.Cond_.always)
        else:
            implot.setup_axis_limits(implot.ImAxis_.x1, x_min, x_max, axis_cond)
        implot.setup_axis_limits(implot.ImAxis_.y1, y_min - y_padding, y_max + y_padding, axis_cond)

        # Get visible range for LOD calculation
        x_lim = implot.get_plot_limits()
        visible_range = (x_lim.x.min, x_lim.x.max)
        # Publish limits and hover info
        try:
            if shared_lod is not None:
                # Always save current limits for sync operations
                shared_lod[f"limits_{self.index}"] = (float(x_lim.x.min), float(x_lim.x.max),
                                                      float(x_lim.y.min), float(x_lim.y.max))
                if imgui.is_plot_hovered():
                    shared_lod["hover_limits"] = (float(x_lim.x.min), float(x_lim.x.max))
                    shared_lod["panel_index"] = self.index
                    shared_lod["title"] = self.title
        except Exception:
            pass
        # LOD tracking per panel
        try:
            visible_bars = int(max(0, int(np.ceil(visible_range[1]) - np.floor(visible_range[0]))))
        except Exception:
            visible_bars = int(visible_count)
        plotted_bars = 0

        # Setup X-axis ticks with DateTime labels from CSVReader (Yaplacak 2)
        try:
            if self.ohlc_data is not None:
                start_i = max(0, int(np.floor(visible_range[0])))
                end_i = min(int(np.ceil(visible_range[1])), len(self.ohlc_data.bars) - 1)
                if end_i >= start_i:
                    # No direct axis ticks to avoid setup order conflicts; custom labels drawn below
                    pass
        except Exception:
            pass

        # Render OHLC data if present
        if ohlc_src is not None:
            lod_time, lod_ohlc = self._calculate_lod_ohlc(
                time_data,
                ohlc_src,
                visible_range,
                plot_width_pixels
            )

            if len(lod_time) > 0:
                try:
                    plotted_bars = max(plotted_bars, int(len(lod_time)))
                except Exception:
                    pass
                # Plot all candlesticks with same label "OHLC"
                # ImPlot will group them under one legend entry
                # Get trade signal settings from shared_lod
                show_signals = False
                signals_array = None
                if shared_lod is not None:
                    show_signals = shared_lod.get("show_trade_signals", False)
                    signals_array = shared_lod.get("trade_signals", None)

                for i in range(len(lod_time)):
                    o, h, l, c = lod_ohlc[i]

                    # Determine color based on trade signals if enabled
                    bar_index = int(lod_time[i])  # Original bar index
                    signal_color = None

                    if show_signals and signals_array is not None and 0 <= bar_index < len(signals_array):
                        signal = signals_array[bar_index]
                        if signal == 1:  # LONG
                            signal_color = imgui.ImVec4(0.0, 1.0, 0.0, 1.0)  # GREEN
                        elif signal == -1:  # SHORT
                            signal_color = imgui.ImVec4(1.0, 0.0, 0.0, 1.0)  # RED
                        else:
                            signal_color = imgui.ImVec4(1.0, 1.0, 1.0, 1.0)  # WHITE
                        # signal == 0 (FLAT): use default color

                    # Use signal color if available, otherwise default color
                    if signal_color is not None:
                        color = signal_color
                    else:
                        # Default color: green if bullish, red if bearish
                        if c >= o:
                            color = imgui.ImVec4(0.0, 1.0, 0.0, 1.0)  # Green (bullish)
                        else:
                            color = imgui.ImVec4(1.0, 0.0, 0.0, 1.0)  # Red (bearish)

                    # Draw candlestick
                    x = lod_time[i]
                    body_width = 0.6
                    x_left = x - body_width/2
                    x_right = x + body_width/2

                    # All candlesticks use "OHLC" label
                    # ImPlot will create only one legend entry and control all together
                    label = "OHLC"

                    # High-Low wick (thin line)
                    implot.set_next_line_style(color, 1.0)
                    implot.plot_line(label, np.array([x, x]), np.array([l, h]))

                    # Open-Close body (filled rectangle)
                    if c >= o:
                        # Bullish - hollow (just outline)
                        implot.set_next_line_style(color, 2.0)
                        xs = np.array([x_left, x_right, x_right, x_left, x_left])
                        ys = np.array([o, o, c, c, o])
                        implot.plot_line(label, xs, ys)
                    else:
                        # Bearish - filled rectangle
                        implot.set_next_fill_style(color, 0.8)
                        xs_top = np.array([x_left, x_right])
                        xs_bottom = np.array([x_left, x_right])
                        ys_top = np.array([c, c])
                        ys_bottom = np.array([o, o])
                        implot.plot_shaded(label, xs_top, ys_top, ys_bottom)

                # Draw horizontal lines for Buy/Sell signals (TODO 4)
                if show_signals and signals_array is not None:
                    try:
                        # Find signal segments in visible range
                        start_idx = max(0, int(np.floor(visible_range[0])))
                        end_idx = min(len(signals_array), int(np.ceil(visible_range[1])))

                        if end_idx > start_idx:
                            # Scan for signal segments (Buy=1, Sell=-1, Flat=0)
                            i = start_idx
                            while i < end_idx:
                                signal = signals_array[i]

                                # Skip Flat signals
                                if signal == 0:
                                    i += 1
                                    continue

                                # Found Buy or Sell signal, find the segment end
                                segment_start = i
                                current_signal = signal

                                # Find where this signal ends (changes to different signal)
                                while i < end_idx and signals_array[i] == current_signal:
                                    i += 1
                                segment_end = i - 1

                                # Get OHLC data for this segment
                                segment_ohlc = ohlc_src[segment_start:segment_end + 1]

                                y_offset = 0.001 # 5% offset applied for visual separation from candlesticks

                                if len(segment_ohlc) > 0:
                                    # Calculate line position based on signal type
                                    if current_signal == 1:  # BUY
                                        # Green line above the first bar's high
                                        y_pos = float(segment_ohlc[0, 1])  # First bar's High
                                        offset = (segment_ohlc[0, 1] - segment_ohlc[0, 2]) * y_offset  # First bar height
                                        y_pos += offset  # Above the high
                                        line_color = imgui.ImVec4(0.0, 1.0, 0.0, 1.0)  # GREEN
                                    else:  # SELL (-1)
                                        # Red line below the first bar's low
                                        y_pos = float(segment_ohlc[0, 2])  # First bar's Low
                                        offset = (segment_ohlc[0, 1] - segment_ohlc[0, 2]) * y_offset  # First bar height
                                        y_pos -= offset  # Below the low
                                        line_color = imgui.ImVec4(1.0, 0.0, 0.0, 1.0)  # RED

                                    # Draw horizontal line for this segment
                                    x_start = float(segment_start)
                                    x_end = float(segment_end + 1)
                                    implot.set_next_line_style(line_color, 2.0)
                                    implot.plot_line(
                                        f"##Signal_{current_signal}_{segment_start}",  # ## prefix hides from legend
                                        np.array([x_start, x_end]),
                                        np.array([y_pos, y_pos])
                                    )
                    except Exception as e:
                        try:
                            print(f"[DEBUG] Signal line drawing error: {e}")
                        except Exception:
                            pass

        # Render other data items
        for item in self.data_items:
            if item.data_type == DataType.Line or item.data_type == DataType.PnL or item.data_type == DataType.Balance:
                # Line plot with LOD
                lod_x, lod_y = self._calculate_lod_line(
                    time_data,
                    item.data,
                    visible_range,
                    plot_width_pixels
                )

                if len(lod_x) > 0:
                    try:
                        plotted_bars = max(plotted_bars, int(len(lod_x)))
                    except Exception:
                        pass
                    if item.color:
                        implot.set_next_line_style(imgui.ImVec4(*item.color))
                    implot.plot_line(item.label, lod_x, lod_y)

            elif item.data_type == DataType.Volume or item.data_type == DataType.Histogram:
                # Bar plot with LOD
                lod_x, lod_y = self._calculate_lod_bars(
                    time_data,
                    item.data,
                    visible_range,
                    plot_width_pixels
                )

                # Fallback if LOD produced nothing but we have visible data
                if len(lod_x) == 0:
                    try:
                        start_i = max(0, int(np.floor(visible_range[0])))
                        end_i = min(int(np.ceil(visible_range[1])), len(item.data) - 1)
                        if end_i >= start_i:
                            xs = np.arange(start_i, end_i + 1, dtype=np.float64)
                            ys = item.data[start_i:end_i + 1]
                            lod_x, lod_y = xs, ys
                    except Exception:
                        pass

                if len(lod_x) > 0:
                    try:
                        plotted_bars = max(plotted_bars, int(len(lod_x)))
                    except Exception:
                        pass
                    # Ensure float64 arrays for ImPlot bindings
                    lod_x = np.asarray(lod_x, dtype=np.float64)
                    lod_y = np.asarray(lod_y, dtype=np.float64)
                    # Ensure visible fill color
                    if item.color:
                        implot.set_next_fill_style(imgui.ImVec4(*item.color))
                    else:
                        implot.set_next_fill_style(imgui.ImVec4(0.2, 0.6, 1.0, 0.6))

                    # For histogram, use different colors for positive/negative
                    if item.data_type == DataType.Histogram:
                        pos_mask = lod_y >= 0
                        neg_mask = lod_y < 0

                        if np.any(pos_mask):
                            implot.set_next_fill_style(imgui.ImVec4(0.0, 1.0, 0.0, 0.5))
                            implot.plot_bars(f"{item.label}_pos", lod_x[pos_mask], lod_y[pos_mask], 0.8)

                        if np.any(neg_mask):
                            implot.set_next_fill_style(imgui.ImVec4(1.0, 0.0, 0.0, 0.5))
                            implot.plot_bars(f"{item.label}_neg", lod_x[neg_mask], lod_y[neg_mask], 0.8)
                    else:
                        implot.plot_bars(item.label, lod_x, lod_y, 0.9)

            elif item.data_type == DataType.Bands:
                # Bands (upper, lower)
                upper_data, lower_data = item.data

                lod_x_upper, lod_y_upper = self._calculate_lod_line(
                    time_data, upper_data, visible_range, plot_width_pixels
                )
                lod_x_lower, lod_y_lower = self._calculate_lod_line(
                    time_data, lower_data, visible_range, plot_width_pixels
                )

                if len(lod_x_upper) > 0 and len(lod_x_lower) > 0:
                    color = item.color if item.color else (0.5, 0.5, 1.0, 1.0)

                    # Plot upper band
                    implot.set_next_line_style(imgui.ImVec4(*color))
                    implot.plot_line(f"{item.label}_upper", lod_x_upper, lod_y_upper)

                    # Plot lower band
                    implot.set_next_line_style(imgui.ImVec4(*color))
                    implot.plot_line(f"{item.label}_lower", lod_x_lower, lod_y_lower)

                    # Shaded area between bands
                    implot.set_next_fill_style(imgui.ImVec4(color[0], color[1], color[2], 0.2))
                    # Note: ImPlot's plot_shaded requires same X arrays, so we need to interpolate
                    # For simplicity, plot without shading for now
                    # TODO: Implement proper band shading

            elif item.data_type == DataType.Levels:
                # Horizontal reference lines
                for level in item.data:
                    if item.color:
                        implot.set_next_line_style(imgui.ImVec4(*item.color))

                    # Plot horizontal line across visible range
                    x_range = np.array([visible_range[0], visible_range[1]])
                    y_range = np.array([level, level])
                    implot.plot_line(f"{item.label}_{level}", x_range, y_range)

            elif item.data_type == DataType.TradeSignals:
                # Scatter plot for trade signals
                x_indices, y_values, signal_types = item.data

                # Filter visible range
                visible_mask = (time_data[x_indices] >= visible_range[0]) & (time_data[x_indices] <= visible_range[1])
                visible_x_indices = x_indices[visible_mask]
                visible_y = y_values[visible_mask]
                visible_types = signal_types[visible_mask]

                if len(visible_x_indices) > 0:
                    visible_x = time_data[visible_x_indices]

                    # Separate buy/sell signals
                    buy_mask = visible_types == 1
                    sell_mask = visible_types == -1

                    if np.any(buy_mask):
                        implot.set_next_marker_style(implot.Marker_.up, 8, imgui.ImVec4(0.0, 1.0, 0.0, 1.0))
                        implot.plot_scatter(f"{item.label}_buy", visible_x[buy_mask], visible_y[buy_mask])

                    if np.any(sell_mask):
                        implot.set_next_marker_style(implot.Marker_.down, 8, imgui.ImVec4(1.0, 0.0, 0.0, 1.0))
                        implot.plot_scatter(f"{item.label}_sell", visible_x[sell_mask], visible_y[sell_mask])

            elif item.data_type == DataType.Stairs:
                # Stairs/step plot for trade signal states (e.g., -1, 0, 1)
                # Auto-scale small value ranges for better visibility
                data_array = np.array(item.data, dtype=np.float64)
                
                # Check if data is in small range (e.g., -1 to 1) and scale it
                data_min = np.min(data_array)
                data_max = np.max(data_array)
                data_range = data_max - data_min
                
                # If range is very small (< 10), scale by 100 for better visibility
                scale_factor = 1.0
                if data_range > 0 and data_range < 10:
                    scale_factor = 1.0
                    data_scaled = data_array * scale_factor
                else:
                    data_scaled = data_array
                
                lod_x, lod_y = self._calculate_lod_stairs(
                    time_data,
                    data_scaled,
                    visible_range,
                    plot_width_pixels
                )

                if len(lod_x) > 0:
                    try:
                        plotted_bars = max(plotted_bars, int(len(lod_x)))
                    except Exception:
                        pass

                    # Use ImPlot's stairs mode if available, otherwise use line with step-like rendering
                    if item.color:
                        implot.set_next_line_style(imgui.ImVec4(*item.color), 2.0)
                    else:
                        implot.set_next_line_style(imgui.ImVec4(1.0, 0.5, 0.0, 1.0), 2.0)  # Orange default

                    # Check if ImPlot has plot_stairs function
                    if hasattr(implot, 'plot_stairs'):
                        implot.plot_stairs(item.label, lod_x, lod_y)
                    else:
                        # Fallback: manually create step/stairs effect by duplicating points
                        # For stairs: horizontal then vertical transitions
                        stairs_x = []
                        stairs_y = []
                        for i in range(len(lod_x)):
                            if i == 0:
                                stairs_x.append(lod_x[i])
                                stairs_y.append(lod_y[i])
                            else:
                                # Add horizontal segment (same y as previous point)
                                stairs_x.append(lod_x[i])
                                stairs_y.append(lod_y[i-1])
                                # Add vertical segment (new y at current x)
                                stairs_x.append(lod_x[i])
                                stairs_y.append(lod_y[i])

                        implot.plot_line(item.label, np.array(stairs_x), np.array(stairs_y))

        # Shared crosshair & interactions (vertical synced, horizontal local + info)
        try:
            if shared_crosshair and shared_crosshair.get("enabled"):
                # Update from hovered plot
                if implot.is_plot_hovered():
                    mouse = implot.get_plot_mouse_pos()
                    # Round to nearest bar index
                    cx = float(max(0, min(int(round(mouse.x)), len(time_data) - 1)))
                    shared_crosshair["x"] = cx
                    shared_crosshair["y"] = float(mouse.y)
                    # Draw horizontal line at local mouse y
                    plot_pos = implot.get_plot_pos()
                    plot_size = implot.get_plot_size()
                    py = implot.plot_to_pixels(0.0, mouse.y).y
                    dl = implot.get_plot_draw_list()
                    dl.add_line(imgui.ImVec2(plot_pos.x, py), imgui.ImVec2(plot_pos.x + plot_size.x, py), 0x88FFFFFF, 1)

                # # Interactions for X/Y sync
                # if shared_xaxis and shared_xaxis.get("enabled") and implot.is_plot_hovered():
                #     io = imgui.get_io()
                #     # LMB pan: shift visible window by delta x
                #     if imgui.is_mouse_dragging(0):
                #         mouse = implot.get_plot_mouse_pos()
                #         last_x = shared_xaxis.get("lmb_last_x")
                #         if last_x is None:
                #             shared_xaxis["lmb_last_x"] = mouse.x
                #         else:
                #             dx = mouse.x - float(last_x)
                #             # Negative dx pans right (increase offset), positive pans left
                #             total = len(time_data)
                #             start = max(0, min(int(x_lim.x.min - dx), total - 1))
                #             end = max(start + 1, min(int(x_lim.x.max - dx), total))
                #             count = max(1, end - start)
                #             shared_xaxis["request"] = (start, count)
                #             shared_xaxis["lmb_last_x"] = mouse.x
                #     else:
                #         # Reset last when not dragging
                #         if "lmb_last_x" in shared_xaxis:
                #             shared_xaxis.pop("lmb_last_x", None)
                #
                #     # RMB drag: continuous zoom on X (horizontal) and Y (vertical)
                #     if imgui.is_mouse_dragging(1):
                #         # Horizontal drag -> X zoom around center
                #         mdx = getattr(io, 'mouse_delta').x if hasattr(io, 'mouse_delta') else 0.0
                #         if mdx != 0.0:
                #             total = len(time_data)
                #             span = max(2.0, (x_lim.x.max - x_lim.x.min))
                #             cx = 0.5 * (x_lim.x.max + x_lim.x.min)
                #             # Sensitivity factor
                #             new_span = max(2.0, span * (1.0 + (-0.01 * mdx)))
                #             start = int(max(0, min(cx - new_span / 2.0, total - 1)))
                #             end = int(max(start + 1, min(cx + new_span / 2.0, total)))
                #             count = max(1, end - start)
                #             shared_xaxis["request"] = (start, count)
                #         # Vertical drag -> Y zoom factor accumulate
                #         mdy = getattr(io, 'mouse_delta').y if hasattr(io, 'mouse_delta') else 0.0
                #         if mdy != 0.0:
                #             y_mul = shared_xaxis.get("y_mul", 1.0)
                #             y_mul *= (1.0 + (-0.01 * mdy))
                #             y_mul = max(0.1, min(10.0, y_mul))
                #             shared_xaxis["y_mul"] = y_mul
                #
                #     # MMB box zoom: on drag draw rect, on release apply
                #     if imgui.is_mouse_clicked(2):
                #         mouse = implot.get_plot_mouse_pos()
                #         shared_xaxis["mmb_start_x"] = mouse.x
                #         shared_xaxis["mmb_drag"] = True
                #     if shared_xaxis.get("mmb_drag") and imgui.is_mouse_down(2):
                #         mouse = implot.get_plot_mouse_pos()
                #         x0 = float(shared_xaxis.get("mmb_start_x", mouse.x))
                #         x1 = float(mouse.x)
                #         # Draw selection rectangle
                #         dl = implot.get_plot_draw_list()
                #         pos = implot.get_plot_pos()
                #         size = implot.get_plot_size()
                #         p0 = implot.plot_to_pixels(min(x0, x1), x_lim.y.min)
                #         p1 = implot.plot_to_pixels(max(x0, x1), x_lim.y.max)
                #         dl.add_rect(imgui.ImVec2(p0.x, pos.y), imgui.ImVec2(p1.x, pos.y + size.y), 0x66FFFFFF, 0, 0, 1)
                #     if shared_xaxis.get("mmb_drag") and imgui.is_mouse_released(2):
                #         mouse = implot.get_plot_mouse_pos()
                #         x0 = float(shared_xaxis.get("mmb_start_x", mouse.x))
                #         x1 = float(mouse.x)
                #         if abs(x1 - x0) > 1e-6:
                #             total = len(time_data)
                #             start = max(0, min(int(min(x0, x1)), total - 1))
                #             end = min(int(max(x0, x1)), total - 1)
                #             count = max(1, end - start)
                #             shared_xaxis["request"] = (start, count)
                #         shared_xaxis.pop("mmb_drag", None)
                #         shared_xaxis.pop("mmb_start_x", None)

                # Draw if we have a target
                if shared_crosshair.get("x") is not None:
                    cx = float(shared_crosshair["x"])
                    plot_pos = implot.get_plot_pos()
                    plot_size = implot.get_plot_size()
                    px = implot.plot_to_pixels(cx, 0.0).x
                    dl = implot.get_plot_draw_list()
                    # Draw vertical crosshair line
                    dl.add_line(imgui.ImVec2(px, plot_pos.y), imgui.ImVec2(px, plot_pos.y + plot_size.y), 0x88FFFFFF, 1)

                    # Get Y value for this panel at crosshair X position
                    cy = None
                    idx = int(cx)
                    if implot.is_plot_hovered():
                        # Use mouse Y for hovered plot
                        mouse = implot.get_plot_mouse_pos()
                        cy = float(mouse.y)
                    else:
                        # Try to get Y from panel data at crosshair X index
                        if 0 <= idx < len(time_data):
                            if ohlc_src is not None and idx < len(ohlc_src):
                                # Use Close price for OHLC panels
                                cy = float(ohlc_src[idx, 3])
                            elif len(self.data_items) > 0:
                                # Use first line/data item for other panels
                                for item in self.data_items:
                                    if item.data_type == DataType.Line and idx < len(item.data):
                                        cy = float(item.data[idx])
                                        break
                                    elif item.data_type == DataType.Histogram and idx < len(item.data):
                                        cy = float(item.data[idx])
                                        break
                                    elif item.data_type == DataType.Stairs and idx < len(item.data):
                                        cy = float(item.data[idx])
                                        break

                    # Draw horizontal crosshair line at Y position
                    if cy is not None:
                        py = implot.plot_to_pixels(0.0, cy).y
                        dl.add_line(imgui.ImVec2(plot_pos.x, py), imgui.ImVec2(plot_pos.x + plot_size.x, py), 0x88FFFFFF, 1)

                        # Draw cross-hair position in top-right corner
                        pos_text = f"X: {cx:.0f}  Y: {cy:.2f}"
                        text_size = imgui.calc_text_size(pos_text)
                        margin = 8.0
                        text_x = plot_pos.x + plot_size.x - text_size.x - margin
                        text_y = plot_pos.y + margin
                        # Background rectangle
                        bg_min = ImVec2(text_x - 5, text_y - 2)
                        bg_max = ImVec2(text_x + text_size.x + 5, text_y + text_size.y + 2)
                        dl.add_rect_filled(bg_min, bg_max, 0xAA000000, 4)
                        # Text
                        dl.add_text(ImVec2(text_x, text_y), 0xFFFFFFFF, pos_text)

                # Info panel: if crosshair x is set, draw on hover or globally when enabled
                if shared_crosshair.get("x") is not None and (implot.is_plot_hovered() or shared_crosshair.get("info_all")):
                    self.BuildPanelInfo(int(shared_crosshair.get("x")), ohlc_src, shared_labels, shared_crosshair)

        except Exception as e:
            # Debug: avoid swallowing errors silently
            try:
                print(f"[DEBUG] Panel info draw error: {e}")
            except Exception:
                pass

        # # Apply Y zoom if requested (scale around center)
        # try:
        #     if shared_xaxis and shared_xaxis.get("enabled") and shared_xaxis.get("y_mul") is not None:
        #         y_center = 0.5 * (y_min + y_max)
        #         y_range = max(1e-9, (y_max - y_min))
        #         scale = float(shared_xaxis["y_mul"])  # >1 expand, <1 shrink (due to sign above)
        #         new_half = 0.5 * y_range * scale
        #         implot.setup_axis_limits(implot.ImAxis_.y1, y_center - new_half, y_center + new_half, imgui.Cond_.always)
        # except Exception:
        #     pass

        # Custom X-axis time labels (legacy style) BEFORE ending plot
        try:
            # Publish LOD for hovered panel (for UI header area)
            try:
                if shared_lod is not None and imgui.is_plot_hovered():
                    shared_lod["panel_index"] = self.index
                    shared_lod["title"] = self.title
                    shared_lod["visible_bars"] = int(visible_bars)
                    shared_lod["plotted_bars"] = int(plotted_bars)
            except Exception:
                pass

            # Decide available data length for labeling
            # Prefer explicit per-panel labels, else shared labels from plotter (passed in), else OHLC/time_data
            if self.datetime_labels is not None:
                data_len = len(self.datetime_labels)
            elif shared_labels is not None:
                data_len = len(shared_labels)
            elif ohlc_src is not None:
                data_len = len(ohlc_src)
            else:
                data_len = len(time_data)

            if data_len > 0:
                limits = implot.get_plot_limits()
                t0 = max(0.0, limits.x.min)
                t1 = min(float(data_len - 1), limits.x.max)
                if t1 >= t0:
                    # Evenly spaced 6 ticks (legacy-like density)
                    ticks = np.linspace(t0, t1, 6)
                    draw_list = implot.get_plot_draw_list()
                    plot_pos = implot.get_plot_pos()
                    plot_size = implot.get_plot_size()
                    base_y = plot_pos.y + plot_size.y + 5
                    for t in ticks:
                        idx = int(round(t))
                        if 0 <= idx < data_len:
                            label = self._fmt_time_label(idx, shared_labels)
                            px = implot.plot_to_pixels(float(idx), limits.y.min).x
                            draw_list.add_text(imgui.ImVec2(px - 60, base_y), 0xFFFFFFFF, label)
                    # LOD info box: show at bottom-right for all panels
                    self.BuildLODPanelInfoBottomRight(visible_bars, plotted_bars)
        except Exception:
            pass

        # Basic per-plot event dispatch (hover/mouse) to DataPlotter.eventHandler
        try:
            if event_handler is not None:
                hovered = implot.is_plot_hovered()
                io = imgui.get_io()
                # Hover event
                if hovered:
                    mpos = implot.get_plot_mouse_pos()
                    event_handler({
                        "panel_id": self.index,
                        "type": "hover",
                        "x": float(mpos.x),
                        "y": float(mpos.y),
                    })
                    # Wheel event
                    wheel = getattr(io, 'mouse_wheel', 0.0)  # ensure we read from io (not a typo like 'o')
                    if wheel not in (0, 0.0):
                        event_handler({
                            "panel_id": self.index,
                            "type": "wheel",
                            "delta": float(wheel),
                            "x": float(mpos.x),
                            "y": float(mpos.y),
                            "ppx": float(plot_width_pixels),
                        })
                    # Click / DoubleClick / Release
                    def _btn_name(b: int) -> str:
                        return 'left' if b == 0 else ('right' if b == 1 else 'middle')
                    for b in (0, 1, 2):
                        if imgui.is_mouse_clicked(b):
                            event_handler({
                                "panel_id": self.index,
                                "type": "click",
                                "button": _btn_name(b),
                                "x": float(mpos.x),
                                "y": float(mpos.y),
                                "ppx": float(plot_width_pixels),
                            })
                        if hasattr(imgui, 'is_mouse_double_clicked') and imgui.is_mouse_double_clicked(b):
                            event_handler({
                                "panel_id": self.index,
                                "type": "double_click",
                                "button": _btn_name(b),
                                "x": float(mpos.x),
                                "y": float(mpos.y),
                                "ppx": float(plot_width_pixels),
                            })
                        if imgui.is_mouse_released(b):
                            event_handler({
                                "panel_id": self.index,
                                "type": "release",
                                "button": _btn_name(b),
                                "x": float(mpos.x),
                                "y": float(mpos.y),
                                "ppx": float(plot_width_pixels),
                            })

                    # Pan (left button)
                    if imgui.is_mouse_dragging(0, 0.0):
                        if not getattr(self, '_pan_active_left', False):
                            self._pan_active_left = True
                            event_handler({
                                "panel_id": self.index,
                                "type": "pan_start",
                                "x": float(mpos.x),
                                "y": float(mpos.y),
                                "ppx": float(plot_width_pixels),
                            })
                        else:
                            mdx = getattr(io, 'mouse_delta').x if hasattr(io, 'mouse_delta') else 0.0
                            mdy = getattr(io, 'mouse_delta').y if hasattr(io, 'mouse_delta') else 0.0
                            event_handler({
                                "panel_id": self.index,
                                "type": "panning",
                                "dx": float(mdx),
                                "dy": float(mdy),
                                "x": float(mpos.x),
                                "y": float(mpos.y),
                                "ppx": float(plot_width_pixels),
                            })
                    elif getattr(self, '_pan_active_left', False) and imgui.is_mouse_released(0):
                        self._pan_active_left = False
                        event_handler({
                            "panel_id": self.index,
                            "type": "pan_finished",
                            "x": float(mpos.x),
                            "y": float(mpos.y),
                            "ppx": float(plot_width_pixels),
                        })

                    # Drag (right button)
                    if imgui.is_mouse_dragging(1, 0.0):
                        if not getattr(self, '_drag_active_right', False):
                            self._drag_active_right = True
                            event_handler({
                                "panel_id": self.index,
                                "type": "drag_start",
                                "x": float(mpos.x),
                                "y": float(mpos.y),
                                "ppx": float(plot_width_pixels),
                            })
                        else:
                            mdx = getattr(io, 'mouse_delta').x if hasattr(io, 'mouse_delta') else 0.0
                            mdy = getattr(io, 'mouse_delta').y if hasattr(io, 'mouse_delta') else 0.0
                            event_handler({
                                "panel_id": self.index,
                                "type": "dragging",
                                "dx": float(mdx),
                                "dy": float(mdy),
                                "x": float(mpos.x),
                                "y": float(mpos.y),
                                "ppx": float(plot_width_pixels),
                            })
                    elif getattr(self, '_drag_active_right', False) and imgui.is_mouse_released(1):
                        self._drag_active_right = False
                        event_handler({
                            "panel_id": self.index,
                            "type": "drag_finished",
                            "x": float(mpos.x),
                            "y": float(mpos.y),
                            "ppx": float(plot_width_pixels),
                        })

                    # Selection (middle button)
                    if imgui.is_mouse_dragging(2, 0.0):
                        if not getattr(self, '_select_active_middle', False):
                            self._select_active_middle = True
                            event_handler({
                                "panel_id": self.index,
                                "type": "selection_start",
                                "x": float(mpos.x),
                                "y": float(mpos.y),
                                "ppx": float(plot_width_pixels),
                            })
                        else:
                            mdx = getattr(io, 'mouse_delta').x if hasattr(io, 'mouse_delta') else 0.0
                            mdy = getattr(io, 'mouse_delta').y if hasattr(io, 'mouse_delta') else 0.0
                            event_handler({
                                "panel_id": self.index,
                                "type": "selecting",
                                "dx": float(mdx),
                                "dy": float(mdy),
                                "x": float(mpos.x),
                                "y": float(mpos.y),
                                "ppx": float(plot_width_pixels),
                            })
                    elif getattr(self, '_select_active_middle', False) and imgui.is_mouse_released(2):
                        self._select_active_middle = False
                        event_handler({
                            "panel_id": self.index,
                            "type": "selection_finished",
                            "x": float(mpos.x),
                            "y": float(mpos.y),
                            "ppx": float(plot_width_pixels),
                        })
        except Exception:
            pass

        implot.end_plot()


class DataPlotterImgBundle:
    """
    Main orchestrator for multi-panel plotting with synchronized axes.
    Manages multiple Panel instances and renders them vertically.
    """

    def __init__(self):
        self.panels: Dict[int, Panel] = {}
        self.time_data: Optional[np.ndarray] = None
        self.window_title = "Multi-Panel Chart"
        self.grafik_sembol = "..."
        self.grafik_periyot = "..."
        self.grafik_periyot_extension = "..."
        # Vertical scroll mode for stacked panels (Yaplacak 3)
        self.enable_vertical_scrollbar: bool = False
        # Shared datetime labels (strings) for all panels
        self.datetime_labels: Optional[List[str]] = None
        # Shared OHLC (can be reader-like or direct array)
        self.shared_ohlc_array: Optional[np.ndarray] = None
        self.shared_ohlc_reader: Optional[Any] = None
        # Shared Volume array
        self.shared_volume_array: Optional[np.ndarray] = None
        # Shared Lot array
        self.shared_lot_array: Optional[np.ndarray] = None
        # Shared Delta and Delta% arrays
        self.shared_delta_array: Optional[np.ndarray] = None
        self.shared_delta_pct_array: Optional[np.ndarray] = None
        # Global info panel toggle: show info on all panels when crosshair x is set
        self.show_info_on_all_panels: bool = False
        # Shared crosshair flag
        self.enable_shared_crosshair: bool = False
        # Persisted crosshair X across frames (for global sync)
        self.shared_crosshair_x: Optional[float] = None
        # Shared X-axis sync flag (pan/zoom)
        self.enable_shared_xaxis: bool = False
        # Persisted shared crosshair X across frames
        self.shared_crosshair_x: Optional[float] = None
        # Shared X-axis sync flag
        self.enable_shared_xaxis: bool = False
        # Trade signals array (1=LONG, -1=SHORT, 0=FLAT)
        self.trade_signals: Optional[np.ndarray] = None
        # Show trade signals on OHLC bars
        self.show_trade_signals: bool = False
        # Source panel + button states for event logging
        self._src_panel = None
        self._btn_active = {"left": False, "right": False, "middle": False}
        # Config file path for LoadPlots functionality
        self.config_file_path: Optional[str] = None
        # Data dictionary for LoadPlots functionality
        self.data_dict: Optional[Dict[str, Any]] = None
        # Auto sync request flag (programmatic ReadSrcPlotParams + UpdateOtherPlotsX/Y)
        self._auto_sync_request: bool = False
        # Range slider (Plotly-style overview + selection)
        self.enable_range_slider: bool = False
        self.range_slider_height: float = 120.0  # Default height in pixels

    def AddPanel(self, index: int) -> Panel:
        """
        Create and add a new panel.

        Args:
            index: Panel index (used for ordering and retrieval)

        Returns:
            The created Panel instance
        """
        panel = Panel(index)
        self.panels[index] = panel
        return panel

    def GetPanel(self, index: int) -> Optional[Panel]:
        """
        Retrieve an existing panel by index.

        Args:
            index: Panel index

        Returns:
            Panel instance or None if not found
        """
        return self.panels.get(index)

    def ClearPlots(self) -> None:
        """
        Clear the contents of all plots but keep the panels visible.
        Only clears the data inside the plots, panels remain on screen.
        """
        # TODO: User will add code here to clear plot contents while keeping panels
        print("ClearPlots: Clearing plot contents (keeping panels)")
        pass

    def DeletePlots(self) -> None:
        """
        Delete all plot panels from display.
        ImApp window stays open with the data filename row and button row visible.
        All other plot panels are deleted.
        """
        # TODO: User will add code here to delete plot panels
        print("DeletePlots: Deleting plot panels")
        pass

    def setConfigFile(self, config_file_path: str):
        """Set the JSON config file path for LoadPlots functionality."""
        self.config_file_path = config_file_path

    def setDataDict(self, data_dict: Dict[str, Any]):
        """Set the data dictionary for LoadPlots functionality."""
        self.data_dict = data_dict

    def LoadPlots(self) -> None:
        """
        Load data into plots from JSON config file.
        - Clears existing panels
        - Recreates panels from JSON config
        - Fills panels with data from data_dict
        """
        if self.config_file_path is None:
            print("LoadPlots: No config file path set. Use setConfigFile() first.")
            return

        if self.data_dict is None:
            print("LoadPlots: No data dictionary set. Use setDataDict() first.")
            return

        try:
            # Import the helper function from main module
            import sys
            import os
            # Add project root to path if not already there
            project_root = os.path.dirname(os.path.dirname(__file__))
            if project_root not in sys.path:
                sys.path.insert(0, project_root)

            # Import createPanelsFromConfigFile from main
            from main import createPanelsFromConfigFile

            print(f"LoadPlots: Loading panels from {self.config_file_path}...")

            # Clear existing panels
            self.panels.clear()

            # Reload panels from JSON config
            createPanelsFromConfigFile(self, self.config_file_path, self.data_dict)

            print(f"LoadPlots: Successfully loaded {len(self.panels)} panels from config")

        except Exception as e:
            print(f"LoadPlots: Error loading plots: {e}")
            import traceback
            traceback.print_exc()

    # Centralized plot event handler (logs only src panel)
    def eventHandler(self, event: Dict[str, Any]) -> None:
        try:
            pid = int(event.get("panel_id", -1))
            etype = event.get("type")
            btn = event.get("button")

            if isinstance(btn, int):
                btn = 'left' if btn == 0 else ('right' if btn == 1 else ('middle' if btn == 2 else None))

            # wheel event: log and consider this panel as source; trigger auto-sync
            if etype == "wheel":
                self._src_panel = pid
                self._auto_sync_request = True # On scroll events, request auto sync (Tum plotlarda sync bu flag ile saglaniyor
                print(f"[EVENT] panel={pid} {etype} evt={ {k:v for k,v in event.items() if k!='ppx'} }")
                return

            # hover event: always log (no src_panel tracking, can be spammy)
            if etype == "hover" and 1 == 0:
                print(f"[EVENT] panel={pid} {etype} x={event.get('x'):.2f} y={event.get('y'):.2f}")
                return

            # set src on first actionable event (last clicked/interacted panel)
            if self._src_panel is None and etype in ("click", "pan_start", "drag_start", "selection_start"):
                self._src_panel = pid

            # button active states
            if etype in ("click", "pan_start", "drag_start", "selection_start") and btn in ("left","right","middle"):
                self._btn_active[btn] = True
            if etype in ("release", "pan_finished", "drag_finished", "selection_finished") and btn in ("left","right","middle"):
                self._btn_active[btn] = False
                # On finish events, request auto sync (Tum plotlarda sync bu flag ile saglaniyor
                self._auto_sync_request = True

            # clear src when nothing active
            if not any(self._btn_active.values()):
                self._src_panel = None

            # log only src panel events (hover excluded to avoid spam)
            if pid == self._src_panel and etype in (
                "click","double_click","release",
                "pan_start","panning","pan_finished",
                "drag_start","dragging","drag_finished",
                "selection_start","selecting","selection_finished",
            ):

                print(f"[EVENT] src={self._src_panel} {etype} evt={ {k:v for k,v in event.items() if k!='ppx'} }")
        except Exception:
            pass

        # aykut kod buraya eklendi: event sonunda otomatik UpdateOtherPlotsXY
        # self._event_end_update_other_plots_xy(event)

    def _event_end_update_other_plots_xy(self, event: Dict[str, Any]) -> None:
        """If shared X is enabled, simulate UpdateOtherPlotsXY after an event.

        - Reads src limits from static.panel_limits using last interacted or current event panel
        - Applies X window to all panels via one-shot overrides
        - Applies Y limits to same Y-sync group
        """
        try:
            if not getattr(self, "enable_shared_xaxis", False):
                return

            static = DataPlotterImgBundle.Plot

            # Determine source panel id
            src_id = getattr(self, "_src_panel", None)
            if src_id is None:
                try:
                    src_id = int(event.get("panel_id", -1)) if event is not None else None
                except Exception:
                    src_id = None
            if src_id is None:
                src_id = getattr(static, "src_panel_id", None)
            if src_id is None:
                return
            if not (hasattr(static, "panel_limits") and src_id in static.panel_limits):
                return

            # static.needs_update = True

        except Exception:
            pass  # aykut kod buraya eklendi

    def setTimeData(self, time_data: np.ndarray):
        """
        Set the shared time/X-axis data for all panels.

        Args:
            time_data: Array of time values or indices
        """
        self.time_data = time_data

    def setWindowTitle(self, title: str):
        """Set main window title."""
        self.window_title = title

    def setDateTimeLabels(self, labels: Any):
        """Set shared datetime labels for X-axis on all panels."""
        try:
            if labels is None:
                self.datetime_labels = None
                return
            if isinstance(labels, np.ndarray):
                self.datetime_labels = [str(x) for x in labels.tolist()]
            else:
                self.datetime_labels = [str(x) for x in list(labels)]
        except Exception:
            self.datetime_labels = None

    def setOHLCData(self, source: Any):
        """Set shared OHLC data from reader or direct array.

        Accepts either:
          - reader-like object (has 'bars' / 'ohlc' / 'ohlc_data')
          - numpy array-like with shape (N,4)
        """
        self.shared_ohlc_array = None
        self.shared_ohlc_reader = None
        if source is None:
            return
        try:
            arr = np.array(source)
            if arr.ndim == 2 and arr.shape[1] == 4:
                self.shared_ohlc_array = arr.astype(np.float64)
                return
        except Exception:
            pass
        # Treat as reader-like and store
        self.shared_ohlc_reader = source

    def getOHLCData(self) -> Optional[Any]:
        """Return a reader-like object for Panel.setOHLCData or None.

        If a reader was set, returns it. If an array was set, returns an adapter exposing .ohlc.
        """
        if self.shared_ohlc_reader is not None:
            return self.shared_ohlc_reader
        if self.shared_ohlc_array is not None:
            return _ArrayOHLCReader(self.shared_ohlc_array)
        return None

    def setVolumeData(self, source: Any):
        """Set shared Volume data array from various sources."""
        self.shared_volume_array = None
        if source is None:
            return
        try:
            arr = np.array(source, dtype=np.float64).reshape(-1)
            self.shared_volume_array = arr
            return
        except Exception:
            pass
        # Try reader-like
        try:
            if hasattr(source, 'volume_data'):
                self.shared_volume_array = np.array(source.volume_data, dtype=np.float64).reshape(-1)
                return
            if hasattr(source, 'bars'):
                self.shared_volume_array = np.array([b.volume for b in source.bars], dtype=np.float64).reshape(-1)
                return
        except Exception:
            self.shared_volume_array = None

    def getVolumeData(self) -> Optional[np.ndarray]:
        return self.shared_volume_array

    def setLotData(self, source: Any):
        """Set shared Lot data array from various sources."""
        self.shared_lot_array = None
        if source is None:
            return
        try:
            arr = np.array(source, dtype=np.float64).reshape(-1)
            self.shared_lot_array = arr
            return
        except Exception:
            pass
        # Try reader-like
        try:
            if hasattr(source, 'lot_data'):
                self.shared_lot_array = np.array(source.lot_data, dtype=np.float64).reshape(-1)
                return
            if hasattr(source, 'bars'):
                self.shared_lot_array = np.array([b.lot for b in source.bars], dtype=np.float64).reshape(-1)
                return
        except Exception:
            self.shared_lot_array = None

    def getLotData(self) -> Optional[np.ndarray]:
        return self.shared_lot_array

    def setDeltaData(self, source: Any):
        """Set shared Delta (Close-Open) array from various sources."""
        self.shared_delta_array = None
        if source is None:
            return
        # Try direct array-like
        try:
            arr = np.array(source, dtype=np.float64).reshape(-1)
            self.shared_delta_array = arr
            return
        except Exception:
            pass
        # Try reader-like
        try:
            if hasattr(source, 'delta'):
                self.shared_delta_array = np.array(source.delta, dtype=np.float64).reshape(-1)
                return
            if hasattr(source, 'bars'):
                self.shared_delta_array = np.array([getattr(b, 'delta', 0.0) for b in source.bars], dtype=np.float64).reshape(-1)
                return
        except Exception:
            self.shared_delta_array = None

    def getDeltaData(self) -> Optional[np.ndarray]:
        return self.shared_delta_array

    def setDeltaPctData(self, source: Any):
        """Set shared Delta Percent array from various sources."""
        self.shared_delta_pct_array = None
        if source is None:
            return
        # Try direct array-like
        try:
            arr = np.array(source, dtype=np.float64).reshape(-1)
            self.shared_delta_pct_array = arr
            return
        except Exception:
            pass
        # Try reader-like
        try:
            if hasattr(source, 'delta_pct'):
                self.shared_delta_pct_array = np.array(source.delta_pct, dtype=np.float64).reshape(-1)
                return
            if hasattr(source, 'bars'):
                self.shared_delta_pct_array = np.array([getattr(b, 'delta_pct', 0.0) for b in source.bars], dtype=np.float64).reshape(-1)
                return
        except Exception:
            self.shared_delta_pct_array = None

    def getDeltaPctData(self) -> Optional[np.ndarray]:
        return self.shared_delta_pct_array

    def setEnableVerticalScrollBar(self, enabled: bool):
        """Enable or disable vertical scrollbar for stacked panels."""
        self.enable_vertical_scrollbar = bool(enabled)

    # Yaplacak 5: Shared crosshair toggle (typo-compatible and proper)
    def setEnableSharedCrossHar(self, enabled: bool):  # noqa: N802
        self.enable_shared_crosshair = bool(enabled)

    def setEnableSharedCrossHair(self, enabled: bool):
        self.enable_shared_crosshair = bool(enabled)

    def setShowInfoOnAllPanels(self, enabled: bool):
        """When True, display info panels on all panels using shared crosshair x, without requiring hover."""
        self.show_info_on_all_panels = bool(enabled)

    def RegisterYSyncGroup(self, group_id: int, panel: Panel):
        """Register a panel to a Y-axis sync group. Panels in same group share Y-axis limits."""
        panel.setYSyncGroup(group_id)

    def RegisterXSyncGroup(self, group_id: int, panel: Panel):
        """Register a panel to an X-axis sync group. Panels in same group share X-axis limits."""
        panel.setXSyncGroup(group_id)

    def setTradeSignals(self, signals: np.ndarray):
        """
        Set trade signals array for visualization.

        Args:
            signals: numpy array with values: 1 = LONG, -1 = SHORT, 0 = FLAT
        """
        self.trade_signals = signals

    def setShowTradeSignals(self, enabled: bool):
        """
        Enable/disable trade signal visualization on OHLC bars.
        When enabled, bars are colored: GREEN for LONG, RED for SHORT.

        Args:
            enabled: True to show signals, False to hide
        """
        self.show_trade_signals = bool(enabled)

    def setEnableSharedXAxis(self, enabled: bool):
        """Enable/disable shared X-axis pan/zoom sync (scroll mode)."""
        self.enable_shared_xaxis = bool(enabled)

    def setEnableSharedXAxs(self, enabled: bool):  # noqa: N802 (typo alias)
        self.enable_shared_xaxis = bool(enabled)

    # Shared X-axis sync toggle (proper + typo alias requested)
    def setEnableSharedXAxis(self, enabled: bool):
        self.enable_shared_xaxis = bool(enabled)

    # def setEnableSharedXAxs(self, enabled: bool):  # noqa: N802
    #     self.enable_shared_xaxis = bool(enabled)

    # Range Slider toggle (Plotly-style overview + selection window)
    def setEnableRangeSlider(self, enabled: bool):
        """Enable or disable range slider (mini chart with selection window)."""
        self.enable_range_slider = bool(enabled)

    def setEnableRangeSldr(self, enabled: bool):  # noqa: N802 (typo alias)
        """Typo-compatible alias for setEnableRangeSlider."""
        self.enable_range_slider = bool(enabled)

    def setRangeSliderHeight(self, height: float):
        """Set range slider height in pixels (default 120.0)."""
        self.range_slider_height = float(max(50.0, height))

    def Plot(self):
        """
        Main rendering function. Creates vertical stack of panels with synchronized axes.
        This function is called repeatedly by immapp.run().
        """
        if self.time_data is None or len(self.panels) == 0:
            imgui.text("No data to plot")
            return

        static = DataPlotterImgBundle.Plot

        # Initialize static variables (like Legacy system)
        if not hasattr(static, "initialized"):
            bar_count = len(self.time_data)

            static.combo_items = [
                "FitToScreen (Normal)",
                "FitToScreen (Wide)",
                "FitToScreen (Ultra)",
                "Full Data",
                "Last N Data",
                "First N Data",
                "Range"
            ]
            static.combo_current = 2  # Default: FitToScreen (Ultra)

            # Pan controls
            static.pan_modes = ["VisibleScreenWidth", "UserDefined", "1 Bar", "10 Bar", "100 Bar", "10000 Bar"]
            static.pan_mode_current = 0  # Default: VisibleScreenWidth
            static.pan_step_value = "100"  # Default step for UserDefined
            static.n_value = "1000"
            static.n2_value = "2000"

            static.offset = 0
            static.visible_count = min(1000, bar_count)
            static.needs_update = False
            static.auto_apply = True
            static.src_panel_id = None  # Last clicked/interacted panel
            static.src_panel_limits = None  # Saved limits from ReadSrcPlotParams (x_min, x_max, y_min, y_max)
            static.panel_limits = {}  # Store X/Y limits per panel {panel_idx: (x_min, x_max, y_min, y_max)}
            static.panel_x_overrides = {}  # Per-panel X override {panel_idx: (offset, visible_count)}, excludes src
            static.panel_y_overrides = {}  # Per-panel Y override {panel_idx: (y_min, y_max)}, includes src + Y-sync group

            static.initialized = True

        imgui.bullet_text(
            f"{self.grafik_sembol}  |  {self.grafik_periyot} {self.grafik_periyot_extension}")

        # _, static.tooltip = imgui.checkbox("Show Tooltips", static.tooltip)
        # imgui.same_line()
        # _, static.positive_color = imgui.color_edit4("Positive", static.positive_color, imgui.ColorEditFlags_.no_inputs)
        # imgui.same_line()
        # _, static.negative_color = imgui.color_edit4("Negative", static.negative_color, imgui.ColorEditFlags_.no_inputs)

        # --- UI Controls (from Legacy) ---
        # ClearPlots and LoadPlots buttons (TODO 6)
        delete_plots_clicked = imgui.button("DeletePlots")
        imgui.same_line()
        clear_plots_clicked = imgui.button("ClearPlots")
        imgui.same_line()
        load_plots_clicked = imgui.button("LoadPlots")
        imgui.same_line()


        imgui.text("Multi-Panel Chart")
        imgui.same_line()
        imgui.text("Mode")
        imgui.same_line()
        imgui.set_next_item_width(160)
        _, static.combo_current = imgui.combo("##mode_combo", static.combo_current, static.combo_items)

        mode = static.combo_items[static.combo_current]

        # Conditional textboxes
        if mode in ["Last N Data", "First N Data", "Range"]:
            imgui.same_line()
            imgui.set_next_item_width(100)
            _, static.n_value = imgui.input_text("N", static.n_value, 64)

        if mode == "Range":
            imgui.same_line()
            imgui.set_next_item_width(100)
            _, static.n2_value = imgui.input_text("N2", static.n2_value, 64)

        imgui.same_line()
        apply_clicked = imgui.button("Apply")

        # Pan controls (right side)
        # imgui.same_line()
        # imgui.text(" | ")  # Separator
        # imgui.same_line()
        imgui.text("Pan Step:")
        imgui.same_line()
        imgui.set_next_item_width(140)
        _, static.pan_mode_current = imgui.combo("##pan_mode", static.pan_mode_current, static.pan_modes)

        # Show TextBox if UserDefined is selected
        if static.pan_modes[static.pan_mode_current] == "UserDefined":
            imgui.same_line()
            imgui.set_next_item_width(80)
            _, static.pan_step_value = imgui.input_text("##pan_step", static.pan_step_value, 64)

        imgui.same_line()
        pan_home_clicked = imgui.button("|< En Basa")
        imgui.same_line()
        pan_left_clicked = imgui.button("<< Sola")
        imgui.same_line()
        pan_right_clicked = imgui.button("Saga >>")
        imgui.same_line()
        pan_end_clicked = imgui.button("Sona >|")

        # Position indicator
        imgui.same_line()
        bar_count = len(self.time_data)
        start_pos = static.offset
        end_pos = min(static.offset + static.visible_count, bar_count)
        imgui.text(f"  [{start_pos}-{end_pos} / {bar_count}]")

        # Sync buttons
        imgui.same_line()
        imgui.text("\t | ")

        imgui.same_line()
        src_id = static.src_panel_id if static.src_panel_id is not None else "None"
        imgui.text(f"Src Plot/Panel Id: {src_id}")

        imgui.same_line()
        read_src_clicked = imgui.button("ReadSrcPlotParams")

        imgui.same_line()
        update_x_clicked = imgui.button("UpdateOtherPlotsX")

        imgui.same_line()
        update_y_clicked = imgui.button("UpdateOtherPlotsY")

        imgui.same_line()
        update_xy_clicked = imgui.button("UpdateOtherPlotsXY")

        imgui.same_line()
        imgui.text("|")

        imgui.same_line()
        adjust_y_clicked = imgui.button("Adjust Y Axis")

        imgui.same_line()
        imgui.text("|")
        imgui.same_line()
        _, self.enable_range_slider = imgui.checkbox("Show Range Slider", self.enable_range_slider)

        # Adjust Y Axis Logic (Manual Fit to Visible Range)
        if adjust_y_clicked:
            # Calculate visible X range
            start_idx = static.offset
            end_idx = min(len(self.time_data), static.offset + static.visible_count)
            
            if start_idx < end_idx:
                print(f"[Adjust Y Axis] Fitting Y-axis for visible range: [{start_idx}, {end_idx}]")
                
                # Iterate all panels and calculate local min/max for visible range
                for idx, panel in self.panels.items():
                    local_y_min = float('inf')
                    local_y_max = float('-inf')
                    found_data = False

                    # Check OHLC data
                    ohlc_src = None
                    if panel.ohlc_array is not None:
                        ohlc_src = panel.ohlc_array
                    elif panel.ohlc_data is not None:
                        ohlc_src = panel.ohlc_data.ohlc
                    
                    if ohlc_src is not None:
                        # Slice visible OHLC
                        visible_slice = ohlc_src[start_idx:end_idx]
                        if len(visible_slice) > 0:
                            try:
                                local_y_max = max(local_y_max, float(np.max(visible_slice[:, 1]))) # High
                                local_y_min = min(local_y_min, float(np.min(visible_slice[:, 2]))) # Low
                                found_data = True
                            except Exception:
                                pass

                    # Check other data items
                    for item in panel.data_items:
                        try:
                            # Helper to get visible data slice
                            d_len = len(item.data) if hasattr(item.data, '__len__') else 0
                            s_i = max(0, min(start_idx, d_len))
                            e_i = max(s_i, min(end_idx, d_len))
                            
                            if item.data_type in [DataType.Line, DataType.PnL, DataType.Balance]:
                                visible_data = np.asarray(item.data[s_i:e_i], dtype=np.float64)
                                if visible_data.size > 0 and np.any(~np.isnan(visible_data)):
                                    local_y_min = min(local_y_min, float(np.nanmin(visible_data)))
                                    local_y_max = max(local_y_max, float(np.nanmax(visible_data)))
                                    found_data = True
                                    
                            elif item.data_type in [DataType.Volume, DataType.Histogram]:
                                visible_data = np.asarray(item.data[s_i:e_i], dtype=np.float64)
                                if visible_data.size > 0:
                                    local_y_min = min(local_y_min, float(np.min(visible_data)))
                                    local_y_max = max(local_y_max, float(np.max(visible_data)))
                                    found_data = True
                                    
                            elif item.data_type == DataType.Bands:
                                upper, lower = item.data
                                v_upper = np.asarray(upper[s_i:e_i], dtype=np.float64)
                                v_lower = np.asarray(lower[s_i:e_i], dtype=np.float64)
                                if v_upper.size > 0:
                                    local_y_max = max(local_y_max, float(np.nanmax(v_upper)))
                                    found_data = True
                                if v_lower.size > 0:
                                    local_y_min = min(local_y_min, float(np.nanmin(v_lower)))
                                    found_data = True
                            
                            elif item.data_type == DataType.Levels:
                                levels = np.asarray(item.data, dtype=np.float64)
                                if levels.size > 0:
                                    local_y_min = min(local_y_min, float(np.min(levels)))
                                    local_y_max = max(local_y_max, float(np.max(levels)))
                                    found_data = True
                                    
                            # Note: Stairs/Signals usually handled by auto-scaling or fixed ranges
                        except Exception:
                            pass

                    # Apply if valid range found
                    if found_data and np.isfinite(local_y_min) and np.isfinite(local_y_max):
                        # Add small padding
                        rng = local_y_max - local_y_min
                        pad = rng * 0.05 if rng > 0 else 1.0
                        
                        # Apply override (skip if Volume panel as usually 0-based is better, but user asked for adjust)
                        # We will respect the calculated min/max
                        static.panel_y_overrides[idx] = (local_y_min - pad, local_y_max + pad)
                
                static.needs_update = True
                print(f"[Adjust Y Axis] Applied Y overrides to {len(static.panel_y_overrides)} panels")

        # ReadSrcPlotParams logic - Save current limits of src panel
        if read_src_clicked:
            if static.src_panel_id is not None and static.src_panel_id in static.panel_limits:
                static.src_panel_limits = static.panel_limits[static.src_panel_id]
                x_min, x_max, y_min, y_max = static.src_panel_limits
                print(f"[ReadSrcPlotParams] Saved Panel{static.src_panel_id} limits: X=[{x_min:.1f}, {x_max:.1f}], Y=[{y_min:.2f}, {y_max:.2f}]")
            else:
                print(f"[ReadSrcPlotParams] No source panel selected or limits not available")

        # UpdateOtherPlotsX logic (X-axis only) - Use saved src_panel_limits
        if update_x_clicked:
            # Auto: ReadSrcPlotParams
            if static.src_panel_id is not None and static.src_panel_id in static.panel_limits:
                static.src_panel_limits = static.panel_limits[static.src_panel_id]
                x_min, x_max, y_min, y_max = static.src_panel_limits
                print(f"[Auto-ReadSrcPlotParams] Saved Panel{static.src_panel_id} limits: X=[{x_min:.1f}, {x_max:.1f}], Y=[{y_min:.2f}, {y_max:.2f}]")
            else:
                print(f"[Auto-ReadSrcPlotParams] No source panel selected or limits not available")

            # Apply X update if we now have src limits
            if static.src_panel_limits is not None and static.src_panel_id is not None:
                src_x_min, src_x_max, src_y_min, src_y_max = static.src_panel_limits
                new_offset = int(src_x_min)
                new_visible = int(src_x_max - src_x_min)

                # Apply X override to ALL panels (including src with its own limits)
                # new_offset/new_visible are from src, so src stays fixed
                for idx in self.panels.keys():
                    static.panel_x_overrides[idx] = (new_offset, new_visible)

                static.needs_update = True
                print(f"[UpdateOtherPlotsX] Panel{static.src_panel_id} X limits: [{src_x_min:.1f}, {src_x_max:.1f}] -> applied to {len(static.panel_x_overrides)} panels (src included with own limits)")
            else:
                print(f"[UpdateOtherPlotsX] Please click ReadSrcPlotParams first")

        # UpdateOtherPlotsY logic (Y-axis only, same Y-sync group) - Use saved src_panel_limits
        if update_y_clicked:
            # Auto: ReadSrcPlotParams
            if static.src_panel_id is not None and static.src_panel_id in static.panel_limits:
                static.src_panel_limits = static.panel_limits[static.src_panel_id]
                x_min, x_max, y_min, y_max = static.src_panel_limits
                print(f"[Auto-ReadSrcPlotParams] Saved Panel{static.src_panel_id} limits: X=[{x_min:.1f}, {x_max:.1f}], Y=[{y_min:.2f}, {y_max:.2f}]")
            else:
                print(f"[Auto-ReadSrcPlotParams] No source panel selected or limits not available")
            if static.src_panel_limits is not None and static.src_panel_id is not None:
                src_x_min, src_x_max, src_y_min, src_y_max = static.src_panel_limits
                src_panel = self.panels.get(static.src_panel_id)

                # Apply Y override to src + same Y-sync group panels
                if src_panel and src_panel.y_sync_group is not None:
                    src_y_group = src_panel.y_sync_group
                    applied_count = 0

                    # Apply to all panels in same Y-sync group (including src)
                    for idx, panel in self.panels.items():
                        if panel.y_sync_group == src_y_group:
                            static.panel_y_overrides[idx] = (src_y_min, src_y_max)
                            applied_count += 1

                    static.needs_update = True
                    print(f"[UpdateOtherPlotsY] Panel{static.src_panel_id} Y limits: [{src_y_min:.2f}, {src_y_max:.2f}] -> applied to {applied_count} panels in Y-sync group {src_y_group}")
                else:
                    print(f"[UpdateOtherPlotsY] Panel{static.src_panel_id} has no Y-sync group")
            else:
                print(f"[UpdateOtherPlotsY] Auto ReadSrcPlotParams failed (no source)")

        # UpdateOtherPlotsXY logic (both X and Y axes) - Auto-run ReadSrcPlotParams, then apply
        if update_xy_clicked:
            # Auto: ReadSrcPlotParams
            if static.src_panel_id is not None and static.src_panel_id in static.panel_limits:
                static.src_panel_limits = static.panel_limits[static.src_panel_id]
                x_min, x_max, y_min, y_max = static.src_panel_limits
                print(f"[Auto-ReadSrcPlotParams] Saved Panel{static.src_panel_id} limits: X=[{x_min:.1f}, {x_max:.1f}], Y=[{y_min:.2f}, {y_max:.2f}]")
            else:
                print(f"[Auto-ReadSrcPlotParams] No source panel selected or limits not available")

            if static.src_panel_limits is not None and static.src_panel_id is not None:
                src_x_min, src_x_max, src_y_min, src_y_max = static.src_panel_limits
                new_offset = int(src_x_min)
                new_visible = int(src_x_max - src_x_min)

                # Apply X override to ALL panels (including src with its own limits)
                for idx in self.panels.keys():
                    static.panel_x_overrides[idx] = (new_offset, new_visible)

                # Apply Y override to src + same Y-sync group panels
                src_panel = self.panels.get(static.src_panel_id)
                applied_y_count = 0
                if src_panel and src_panel.y_sync_group is not None:
                    src_y_group = src_panel.y_sync_group
                    # Apply to all panels in same Y-sync group (including src)
                    for idx, panel in self.panels.items():
                        if panel.y_sync_group == src_y_group:
                            static.panel_y_overrides[idx] = (src_y_min, src_y_max)
                            applied_y_count += 1

                static.needs_update = True
                print(f"[UpdateOtherPlotsXY] Panel{static.src_panel_id} X limits: [{src_x_min:.1f}, {src_x_max:.1f}] -> applied to {len(static.panel_x_overrides)} panels")
                if applied_y_count > 0:
                    print(f"[UpdateOtherPlotsXY] Panel{static.src_panel_id} Y limits: [{src_y_min:.2f}, {src_y_max:.2f}] -> applied to {applied_y_count} panels in Y-sync group")
                else:
                    print(f"[UpdateOtherPlotsXY] Panel{static.src_panel_id} has no Y-sync group, Y sync skipped")
            else:
                print(f"[UpdateOtherPlotsXY] Auto ReadSrcPlotParams failed (no source)")

        # Pan logic
        if pan_home_clicked or pan_left_clicked or pan_right_clicked or pan_end_clicked:
            # Clear overrides when user manually navigates (exit sync mode)
            static.panel_x_overrides.clear()
            static.panel_y_overrides.clear()

            bar_count = len(self.time_data)

            if pan_home_clicked:
                # Go to beginning
                static.offset = 0
                static.needs_update = True
            elif pan_end_clicked:
                # Go to end
                max_offset = max(0, bar_count - static.visible_count)
                static.offset = max_offset
                static.needs_update = True
            else:
                # Pan left/right with step
                # Calculate step size
                pan_mode = static.pan_modes[static.pan_mode_current]
                if pan_mode == "VisibleScreenWidth":
                    step = static.visible_count
                elif pan_mode == "1 Bar":
                    step = 1
                elif pan_mode == "10 Bar":
                    step = 10
                elif pan_mode == "100 Bar":
                    step = 100
                elif pan_mode == "1000 Bar":
                    step = 1000
                else:  # UserDefined
                    try:
                        step = int(static.pan_step_value)
                    except ValueError:
                        step = 100  # Fallback

                # Apply pan
                if pan_left_clicked:
                    static.offset = max(0, static.offset - step)
                else:  # pan_right_clicked
                    max_offset = max(0, bar_count - static.visible_count)
                    static.offset = min(max_offset, static.offset + step)

                static.needs_update = True

        # Apply logic (from Legacy)
        if apply_clicked:
            bar_count = len(self.time_data)
            static.needs_update = True

            if mode.startswith("FitToScreen"):
                static.fit_mode = mode
                static.pending_fit = True
            elif mode == "Full Data":
                static.offset = 0
                static.visible_count = bar_count
            elif mode == "Last N Data":
                try:
                    n = int(static.n_value)
                    n = max(1, min(n, bar_count))
                    static.offset = max(0, bar_count - n)
                    static.visible_count = n
                except ValueError:
                    pass
            elif mode == "First N Data":
                try:
                    n = int(static.n_value)
                    n = max(1, min(n, bar_count))
                    static.offset = 0
                    static.visible_count = n
                except ValueError:
                    pass
            elif mode == "Range":
                try:
                    start = int(static.n_value)
                    count = int(static.n2_value)
                    start = max(0, min(start, bar_count - 1))
                    count = max(1, min(count, bar_count - start))
                    static.offset = start
                    static.visible_count = count
                except ValueError:
                    pass

        # Button handlers (TODO 6)
        if delete_plots_clicked:
            self.DeletePlots()

        if clear_plots_clicked:
            self.ClearPlots()

        if load_plots_clicked:
            self.LoadPlots()

        # Scroll Bar
        bar_count = len(self.time_data)
        # Shared LOD info store for this frame (updated by hovered panel)
        shared_lod = {
            "show_trade_signals": self.show_trade_signals,
            "trade_signals": self.trade_signals
        }

        if static.visible_count < bar_count:
            # Scrollbar header + inline LOD of hovered panel
            imgui.text(f"Scroll Bar  (Showing: {static.offset} to {static.offset + static.visible_count} of {bar_count})")
            try:
                # Inline LOD (same line, subtle)
                vb = shared_lod.get("visible_bars")
                pb = shared_lod.get("plotted_bars")
                ttl = shared_lod.get("title")
                if vb is not None and pb is not None:
                    lod_active = (pb > 0 and pb < max(1, vb))
                    lod_text = (f"  |  [{ttl}] LOD Active: {pb}/{vb}" if lod_active else f"  |  [{ttl}] Full Detail: {vb}")
                    imgui.same_line()
                    imgui.text(lod_text)
            except Exception:
                pass

            # imgui.new_line()
            # ========================================
            # RANGE SLIDER (Plotly-style overview + selection window)
            # ========================================
            if self.enable_range_slider:
                try:
                    imgui.separator()
                    imgui.text("Range Slider")

                    # Get close price data for mini chart
                    close_prices = None
                    if self.shared_ohlc_array is not None and len(self.shared_ohlc_array) > 0:
                        close_prices = self.shared_ohlc_array[:, 3]  # Close column
                    elif self.shared_ohlc_reader is not None:
                        try:
                            ohlc_data = self.shared_ohlc_reader.ohlc
                            if ohlc_data is not None and len(ohlc_data) > 0:
                                close_prices = ohlc_data[:, 3]
                        except Exception:
                            pass

                    if close_prices is not None and len(close_prices) > 0:
                        # Prepare LOD data for mini chart (cache for performance)
                        if not hasattr(static, 'range_slider_lod_cache'):
                            # Use aggressive LOD for entire dataset
                            full_x = np.arange(len(close_prices), dtype=np.float64)
                            full_range = (0, len(close_prices))

                            # Simple downsampling for mini chart
                            target_points = 300  # Max points for mini chart
                            if len(close_prices) > target_points:
                                step = max(1, len(close_prices) // target_points)
                                lod_indices = np.arange(0, len(close_prices), step, dtype=int)
                                lod_x = full_x[lod_indices]
                                lod_y = close_prices[lod_indices]
                            else:
                                lod_x = full_x
                                lod_y = close_prices

                            static.range_slider_lod_cache = (lod_x, lod_y)

                        lod_x, lod_y = static.range_slider_lod_cache

                        # Mini chart size
                        plot_size = imgui.ImVec2(-1, self.range_slider_height)
                        plot_flags = (
                            implot.Flags_.no_title |
                            implot.Flags_.no_legend |
                            implot.Flags_.no_mouse_text |
                            implot.Flags_.no_menus
                        )

                        if implot.begin_plot("##range_slider", plot_size, plot_flags):
                            # Setup axes
                            implot.setup_axis(implot.ImAxis_.x1, "")
                            implot.setup_axis(implot.ImAxis_.y1, "")
                            implot.setup_axis_limits(
                                implot.ImAxis_.x1,
                                0, len(self.time_data),
                                imgui.Cond_.always
                            )

                            # Auto-fit Y axis to data
                            if len(lod_y) > 0:
                                y_min = float(np.nanmin(lod_y))
                                y_max = float(np.nanmax(lod_y))
                                y_range = y_max - y_min
                                y_padding = y_range * 0.05 if y_range > 0 else 1.0
                                implot.setup_axis_limits(
                                    implot.ImAxis_.y1,
                                    y_min - y_padding, y_max + y_padding,
                                    imgui.Cond_.always
                                )

                            # Plot mini chart line
                            implot.set_next_line_style(imgui.ImVec4(0.5, 0.7, 1.0, 1.0))
                            implot.plot_line("Data", lod_x, lod_y)

                            # Get plot position and size for overlay drawing
                            plot_pos = implot.get_plot_pos()
                            plot_size_actual = implot.get_plot_size()

                            # Calculate selection window coordinates
                            x_start = static.offset
                            x_end = static.offset + static.visible_count
                            px_start = implot.plot_to_pixels(float(x_start), 0.0).x
                            px_end = implot.plot_to_pixels(float(x_end), 0.0).x

                            # Get draw list for overlay
                            draw_list = implot.get_plot_draw_list()

                            # 1. Dimmed areas (outside selection)
                            if x_start > 0:
                                draw_list.add_rect_filled(
                                    imgui.ImVec2(plot_pos.x, plot_pos.y),
                                    imgui.ImVec2(px_start, plot_pos.y + plot_size_actual.y),
                                    imgui.get_color_u32((0.0, 0.0, 0.0, 0.6))
                                )

                            if x_end < len(self.time_data):
                                draw_list.add_rect_filled(
                                    imgui.ImVec2(px_end, plot_pos.y),
                                    imgui.ImVec2(plot_pos.x + plot_size_actual.x, plot_pos.y + plot_size_actual.y),
                                    imgui.get_color_u32((0.0, 0.0, 0.0, 0.6))
                                )

                            # 2. Selection window (semi-transparent highlight)
                            draw_list.add_rect_filled(
                                imgui.ImVec2(px_start, plot_pos.y),
                                imgui.ImVec2(px_end, plot_pos.y + plot_size_actual.y),
                                imgui.get_color_u32((0.3, 0.5, 0.8, 0.25))
                            )

                            # 3. Border
                            draw_list.add_rect(
                                imgui.ImVec2(px_start, plot_pos.y),
                                imgui.ImVec2(px_end, plot_pos.y + plot_size_actual.y),
                                imgui.get_color_u32((1.0, 1.0, 1.0, 0.9)),
                                0.0, 0, 2.0
                            )

                            # 4. Edge handles (thick lines)
                            draw_list.add_line(
                                imgui.ImVec2(px_start, plot_pos.y),
                                imgui.ImVec2(px_start, plot_pos.y + plot_size_actual.y),
                                imgui.get_color_u32((1.0, 1.0, 1.0, 1.0)),
                                5.0
                            )
                            draw_list.add_line(
                                imgui.ImVec2(px_end, plot_pos.y),
                                imgui.ImVec2(px_end, plot_pos.y + plot_size_actual.y),
                                imgui.get_color_u32((1.0, 1.0, 1.0, 1.0)),
                                5.0
                            )

                            # 5. Labels
                            draw_list.add_text(
                                imgui.ImVec2(px_start + 5, plot_pos.y + 5),
                                imgui.get_color_u32((1.0, 1.0, 1.0, 1.0)),
                                f"[{x_start}]"
                            )
                            draw_list.add_text(
                                imgui.ImVec2(px_end - 50, plot_pos.y + 5),
                                imgui.get_color_u32((1.0, 1.0, 1.0, 1.0)),
                                f"[{x_end}]"
                            )

                            # 6. Mouse interaction (handle dragging/resizing)
                            # IMPORTANT: Must be BEFORE end_plot()!
                            if implot.is_plot_hovered():
                                mouse_pos = imgui.get_mouse_pos()
                                edge_tol = 8.0  # 8 pixel tolerance for edges

                                # Detect which region mouse is in
                                on_left = abs(mouse_pos.x - px_start) < edge_tol
                                on_right = abs(mouse_pos.x - px_end) < edge_tol
                                in_window = (px_start < mouse_pos.x < px_end)

                                # Change cursor based on region
                                if on_left or on_right:
                                    imgui.set_mouse_cursor(imgui.MouseCursor_.resize_ew)
                                elif in_window:
                                    imgui.set_mouse_cursor(imgui.MouseCursor_.resize_all)

                                # Initialize drag state if not exists
                                if not hasattr(static, 'rs_mode'):
                                    static.rs_mode = None

                                # Click: Start drag/resize operation
                                if imgui.is_mouse_clicked(0):
                                    if on_left:
                                        static.rs_mode = "resize_left"
                                        static.rs_start_mouse = mouse_pos.x
                                        static.rs_start_offset = x_start
                                    elif on_right:
                                        static.rs_mode = "resize_right"
                                        static.rs_start_mouse = mouse_pos.x
                                        static.rs_start_end = x_end
                                    elif in_window:
                                        static.rs_mode = "pan"
                                        static.rs_start_mouse = mouse_pos.x
                                        static.rs_start_offset = static.offset
                                    else:
                                        # Click outside: Jump to position
                                        mouse_plot = implot.get_plot_mouse_pos()
                                        new_center = int(mouse_plot.x)
                                        half = static.visible_count // 2
                                        static.offset = max(0, min(new_center - half,
                                                                   len(self.time_data) - static.visible_count))
                                        static.needs_update = True

                                # Dragging: Update offset/visible_count
                                if imgui.is_mouse_dragging(0) and static.rs_mode is not None:
                                    delta_px = mouse_pos.x - static.rs_start_mouse
                                    bar_count = len(self.time_data)
                                    px_per_bar = plot_size_actual.x / bar_count if bar_count > 0 else 1.0
                                    delta_bars = int(delta_px / px_per_bar)

                                    if static.rs_mode == "resize_left":
                                        # Resize from left edge
                                        new_offset = static.rs_start_offset + delta_bars
                                        new_offset = max(0, min(new_offset, x_end - 10))  # Min 10 bars
                                        static.offset = new_offset
                                        static.visible_count = x_end - new_offset
                                        static.needs_update = True

                                    elif static.rs_mode == "resize_right":
                                        # Resize from right edge
                                        new_end = static.rs_start_end + delta_bars
                                        new_end = max(x_start + 10, min(new_end, bar_count))  # Min 10 bars
                                        static.visible_count = new_end - x_start
                                        static.needs_update = True

                                    elif static.rs_mode == "pan":
                                        # Pan window
                                        new_offset = static.rs_start_offset + delta_bars
                                        new_offset = max(0, min(new_offset, bar_count - static.visible_count))
                                        static.offset = new_offset
                                        static.needs_update = True

                                # Release: Reset mode
                                if imgui.is_mouse_released(0):
                                    static.rs_mode = None

                            # End plot AFTER mouse interaction
                            implot.end_plot()
                except Exception as e:
                    # Silently handle range slider errors to avoid breaking main plot
                    try:
                        print(f"[DEBUG] Range slider error: {e}")
                    except Exception:
                        pass
            imgui.set_next_item_width(-1)
            old_offset = static.offset
            _, static.offset = imgui.slider_int(
                "##scrollbar",
                static.offset,
                0,
                max(0, bar_count - static.visible_count),
                ""
            )
            if old_offset != static.offset:
                static.needs_update = True

        # Get sorted panel indices
        sorted_indices = sorted(self.panels.keys())
        num_panels = len(sorted_indices)

        # If shared OHLC is provided and panel 0 lacks OHLC, attach it now
        try:
            if 0 in self.panels:
                p0 = self.panels[0]
                if getattr(p0, 'ohlc_array', None) is None and getattr(p0, 'ohlc_data', None) is None:
                    if self.shared_ohlc_array is not None:
                        p0.setOHLC(self.shared_ohlc_array)
                    elif self.shared_ohlc_reader is not None:
                        p0.setOHLCData(self.shared_ohlc_reader)
        except Exception:
            pass

        # Get current window size for LOD calculation
        window_size = imgui.get_window_size()
        plot_width_pixels = window_size.x - 80

        if not self.enable_vertical_scrollbar:
            # Begin subplots; optionally link X based on flag
            subplot_flags = (
                implot.SubplotFlags_.no_title |
                implot.SubplotFlags_.no_menus |
                implot.SubplotFlags_.no_resize
            )
            if getattr(self, "enable_shared_xaxis", False):
                subplot_flags |= implot.SubplotFlags_.link_all_x

            size = imgui.ImVec2(-1, -1)
            flags_int = subplot_flags
            # Build row ratios from panels' height_ratio (normalized)
            try:
                ratios = [max(0.01, float(self.panels[idx].height_ratio)) for idx in sorted_indices]
                total = sum(ratios)
                row_ratios_list = [r/total for r in ratios] if total > 0 else None

                print(f"[DEBUG] Panel height_ratios: {[self.panels[idx].height_ratio for idx in sorted_indices]}")
                print(f"[DEBUG] Normalized ratios: {row_ratios_list}")

                # Create SubplotsRowColRatios object if ratios are available
                if row_ratios_list is not None:
                    # Try creating SubplotsRowColRatios object with constructor
                    try:
                        row_col_ratios = implot.SubplotsRowColRatios(row_ratios_list, None)
                        print(f"[DEBUG] Created SubplotsRowColRatios object successfully")
                    except Exception as e1:
                        print(f"[DEBUG] Constructor with args failed: {e1}")
                        try:
                            # Try without arguments
                            row_col_ratios = implot.SubplotsRowColRatios()
                            print(f"[DEBUG] Created empty SubplotsRowColRatios, trying to set rows...")
                            # Try different methods to set ratios
                            if hasattr(row_col_ratios, 'set_row_ratios'):
                                row_col_ratios.set_row_ratios(row_ratios_list)
                            elif hasattr(row_col_ratios, 'row_ratios'):
                                row_col_ratios.row_ratios = row_ratios_list
                            print(f"[DEBUG] Set row_ratios on object")
                        except Exception as e2:
                            print(f"[DEBUG] Empty constructor failed: {e2}")
                            row_col_ratios = None
                else:
                    row_col_ratios = None
            except Exception as e:
                print(f"[DEBUG] Failed to create row_col_ratios: {e}")
                import traceback
                traceback.print_exc()
                row_col_ratios = None

            # FitToScreen calculation (if pending)
            if hasattr(static, "pending_fit") and static.pending_fit:
                # Will compute after subplot begins
                pass

            if implot.begin_subplots("##subplots", num_panels, 1, size, flags_int, row_col_ratios):  # row_col_ratios enabled
                # FitToScreen calculation (inside subplot context)
                if hasattr(static, "pending_fit") and static.pending_fit:
                    plot_width = plot_width_pixels
                    mode = static.fit_mode
                    if mode == "FitToScreen (Normal)":
                        bar_width = 4.0
                    elif mode == "FitToScreen (Wide)":
                        bar_width = 2.5
                    elif mode == "FitToScreen (Ultra)":
                        bar_width = 1.5
                    else:
                        bar_width = 4.0

                    n = int(plot_width / bar_width)
                    n = max(100, min(n, len(self.time_data)))
                    static.visible_count = n
                    static.offset = max(0, len(self.time_data) - n)
                    static.pending_fit = False
                    static.needs_update = True

                # Shared state objects per frame
                # shared_xaxis = {"enabled": getattr(self, "enable_shared_xaxis", False), "request": None}
                shared_cross = {
                    "enabled": getattr(self, "enable_shared_crosshair", False),
                    "x": getattr(self, "shared_crosshair_x", None),
                    "volume": self.shared_volume_array,
                    "lot": self.shared_lot_array,
                    "delta": self.shared_delta_array,
                    "delta_pct": self.shared_delta_pct_array,
                    "info_all": getattr(self, "show_info_on_all_panels", False),
                }
                for idx in sorted_indices:
                    panel = self.panels[idx]
                    panel.render(
                        self.time_data,
                        plot_width_pixels,
                        static.offset,
                        static.visible_count,
                        static.needs_update,
                        None,
                        getattr(self, "datetime_labels", None),
                        shared_cross,
                        shared_lod,
                        getattr(self, "eventHandler", None),
                        static.panel_x_overrides,
                        static.panel_y_overrides,
                        shared_xaxis_enabled=getattr(self, "enable_shared_xaxis", False),
                    )

                if static.needs_update:
                    static.needs_update = False
                    # Clear overrides after one-shot sync (UpdateOtherPlotsX/Y)
                    static.panel_x_overrides.clear()
                    static.panel_y_overrides.clear()

                # Copy panel limits from shared_lod to static for next frame (for programmatic sync)
                for idx in sorted_indices:
                    limit_key = f"limits_{idx}"
                    if limit_key in shared_lod:
                        static.panel_limits[idx] = shared_lod[limit_key]

                # Update src panel id from eventHandler tracking
                if self._src_panel is not None:
                    static.src_panel_id = self._src_panel

                # Persist crosshair x for next frame
                try:
                    self.shared_crosshair_x = shared_cross.get("x")
                except Exception:
                    pass

                # # Apply shared X-axis sync if requested
                # if getattr(self, "enable_shared_xaxis", False) and shared_xaxis.get("request") is not None:
                #     try:
                #         start, count = shared_xaxis["request"]
                #         static.offset = max(0, int(start))
                #         static.visible_count = max(1, int(count))
                #         static.needs_update = True
                #     except Exception:
                #         pass

                # Update offset/visible_count from hovered zoom (subplots)
                try:
                    if shared_lod.get("hover_limits") is not None:
                        bar_count = len(self.time_data)
                        h0, h1 = shared_lod.get("hover_limits")
                        if h0 is not None and h1 is not None:
                            if h1 < h0: h0, h1 = h1, h0
                            start = max(0, min(int(np.floor(h0)), bar_count - 1))
                            end = min(int(np.ceil(h1)), bar_count)
                            end = max(start + 1, end)
                            static.offset = start
                            static.visible_count = max(1, end - start)
                            static.needs_update = True
                except Exception:
                    pass

                implot.end_subplots()
        else:
            # Vertical scroll mode: render each panel with fixed pixel height in a scrollable child (Yaplacak 3)
            # begin_child() ne döndürürse döndürsün end_child() çağrılmalı (ImGui kuralı).
            imgui.begin_child("##plots_scroll", imgui.ImVec2(-1, -1), False, imgui.WindowFlags_.no_scroll_with_mouse)
            if True:
                # Shared crosshair state for this frame (scroll mode)
                shared_cross = {
                    "enabled": getattr(self, "enable_shared_crosshair", False),
                    "x": getattr(self, "shared_crosshair_x", None),
                    "volume": self.shared_volume_array,
                    "lot": self.shared_lot_array,
                    "delta": self.shared_delta_array,
                    "delta_pct": self.shared_delta_pct_array,
                    "info_all": getattr(self, "show_info_on_all_panels", False),
                }
                # Shared X-axis sync state for this frame (scroll mode)
                # shared_xaxis = {"enabled": getattr(self, "enable_shared_xaxis", False), "request": None}

                # FitToScreen calculation (resolve visible_count once based on width)
                if hasattr(static, "pending_fit") and static.pending_fit:
                    plot_width = plot_width_pixels
                    mode = static.fit_mode
                    if mode == "FitToScreen (Normal)":
                        bar_width = 4.0
                    elif mode == "FitToScreen (Wide)":
                        bar_width = 2.5
                    elif mode == "FitToScreen (Ultra)":
                        bar_width = 1.5
                    else:
                        bar_width = 4.0
                    n = int(plot_width / bar_width)
                    n = max(100, min(n, len(self.time_data)))
                    static.visible_count = n
                    static.offset = max(0, len(self.time_data) - n)
                    static.pending_fit = False
                    static.needs_update = True

                # Render panels with fixed or ratio-based pixel heights
                base_height = 320.0
                for idx in sorted_indices:
                    panel = self.panels[idx]

                    # Determine available width for LOD calc (content region)
                    avail_width = imgui.get_content_region_avail().x
                    lod_width = max(1.0, avail_width - 16.0)

                    # Desired draw width: if user setWidth -> fixed; else None = full available
                    if panel.size_mode == "absolute" and panel.fixed_width_px is not None:
                        desired_w = float(panel.fixed_width_px)
                    else:
                        desired_w = None  # full width

                    # Height
                    if panel.size_mode == "absolute" and panel.fixed_height_px is not None:
                        my_height_px = float(panel.fixed_height_px)
                    else:
                        # ratio mode: use base_height scaled by ratio
                        my_height_px = base_height * float(max(0.2, panel.height_ratio))

                    shared_cross = locals().get('shared_cross', {"enabled": getattr(self, "enable_shared_crosshair", False), "x": None})
                    panel.render(
                        self.time_data,
                        lod_width,
                        static.offset,
                        static.visible_count,
                        static.needs_update,
                        (desired_w, my_height_px),
                        getattr(self, "datetime_labels", None),
                        shared_cross,
                        shared_lod,
                        getattr(self, "eventHandler", None),
                        static.panel_x_overrides,
                        static.panel_y_overrides,
                        shared_xaxis_enabled=getattr(self, "enable_shared_xaxis", False),
                    )
                    # Add a small separator between panels
                    imgui.dummy(imgui.ImVec2(1, 6))

                # Copy panel limits from shared_lod to static for next frame
                for idx in sorted_indices:
                    limit_key = f"limits_{idx}"
                    if limit_key in shared_lod:
                        static.panel_limits[idx] = shared_lod[limit_key]

                # Update static.src_panel_id from eventHandler tracking
                if self._src_panel is not None:
                    static.src_panel_id = self._src_panel

                if static.needs_update:
                    static.needs_update = False
                    # Clear overrides after one-shot sync (UpdateOtherPlotsX/Y)
                    static.panel_x_overrides.clear()
                    static.panel_y_overrides.clear()

                # # Apply shared X-axis sync if requested
                # if getattr(self, "enable_shared_xaxis", False) and shared_xaxis.get("request") is not None:
                #     try:
                #         start, count = shared_xaxis["request"]
                #         static.offset = max(0, int(start))
                #         static.visible_count = max(1, int(count))
                #         static.needs_update = True
                #     except Exception:
                #         pass

                # Persist crosshair x for next frame
                try:
                    self.shared_crosshair_x = shared_cross.get("x")
                except Exception:
                    pass

                # Update offset/visible_count from hovered zoom (scroll)
                try:
                    if shared_lod.get("hover_limits") is not None:
                        bar_count = len(self.time_data)
                        h0, h1 = shared_lod.get("hover_limits")
                        if h0 is not None and h1 is not None:
                            if h1 < h0:
                                h0, h1 = h1, h0
                            start = max(0, min(int(np.floor(h0)), bar_count - 1))
                            end = min(int(np.ceil(h1)), bar_count)
                            end = max(start + 1, end)
                            static.offset = start
                            static.visible_count = max(1, end - start)
                            # One-shot push to all panels so they sync with source interaction
                            try:
                                for idx2 in self.panels.keys():
                                    static.panel_x_overrides[idx2] = (start, max(1, end - start))
                            except Exception:
                                pass
                            static.needs_update = True
                except Exception:
                    pass

                # Programmatic ReadSrcPlotParams + UpdateOtherPlotsX/Y on demand (scroll mode)
                try:
                    if getattr(self, '_auto_sync_request', False):
                        self._auto_sync_request = False
                        if static.src_panel_id is not None and static.src_panel_id in static.panel_limits:
                            # ReadSrcPlotParams
                            static.src_panel_limits = static.panel_limits[static.src_panel_id]
                            src_x_min, src_x_max, src_y_min, src_y_max = static.src_panel_limits
                            # UpdateOtherPlotsX
                            new_offset = int(src_x_min)
                            new_visible = int(max(1, src_x_max - src_x_min))
                            for idx2 in self.panels.keys():
                                static.panel_x_overrides[idx2] = (new_offset, new_visible)
                            # Update scroll bar position to match the sync
                            static.offset = new_offset
                            static.visible_count = new_visible
                            # UpdateOtherPlotsY (same Y-sync group)
                            src_panel = self.panels.get(static.src_panel_id)
                            if src_panel and src_panel.y_sync_group is not None:
                                src_y_group = src_panel.y_sync_group
                                for idx2, panel2 in self.panels.items():
                                    if panel2.y_sync_group == src_y_group:
                                        static.panel_y_overrides[idx2] = (src_y_min, src_y_max)
                            static.needs_update = True
                except Exception:
                    pass

                imgui.end_child()

        # Auto-apply on first run
        if getattr(static, "auto_apply", False):
            static.auto_apply = False
            mode = static.combo_items[static.combo_current]
            # Trigger apply logic
            static.fit_mode = mode
            static.pending_fit = True
            static.needs_update = True

















