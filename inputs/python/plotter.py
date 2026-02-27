"""
plotter.py — AlgoTrade Python Plotter
======================================
Called by C# PythonPlotter via pythonnet.

Public API:
    show_optimization_results(data)   →  Optimization results window
    show_single_trader_data(data)     →  SingleTrader equity curve window

Requirements:
    pip install imgui-bundle
"""

from __future__ import annotations
from typing import Any

from imgui_bundle import imgui, implot, hello_imgui, immapp

# ──────────────────────────────────────────────────────────────────────────────
# Session state
# ──────────────────────────────────────────────────────────────────────────────
_results: list[dict] = []          # for show_optimization_results
_trader_data: dict   = {}          # for show_single_trader_data


# ──────────────────────────────────────────────────────────────────────────────
# Public API — function called from C#
# ──────────────────────────────────────────────────────────────────────────────

def show_optimization_results(data: Any) -> None:
    """
    Shows optimization results in an imgui_bundle window.
    Blocks until the window is closed (C# thread waits).

    Parameters
    ----------
    data :
        Python list of dict — output of json.loads().
        Each element contains the following keys:
            parameters        : dict[str, str]   — tested parameter combination
            values            : dict[str, str]   — all statistic values
            net_profit        : float
            win_rate          : float
            profit_factor     : float
            profit_factor_net : float
            max_drawdown      : float
            strategy_name     : str
    """
    global _results

    # pythonnet PyObject → saf Python list[dict]
    _results = [dict(item) for item in data]

    # Default sort: net_profit descending
    _results.sort(key=lambda r: r.get("net_profit", 0.0), reverse=True)

    runner = hello_imgui.RunnerParams()
    runner.app_window_params.window_title             = "AlgoTrade — Optimization Results"
    runner.app_window_params.window_geometry.size     = (1440, 860)
    runner.callbacks.show_gui                         = _gui
    runner.fps_idling.enable_idling                   = True
    runner.fps_idling.fps_idle                        = 20

    immapp.run(runner_params=runner, with_implot=True)


# ──────────────────────────────────────────────────────────────────────────────
# GUI — frame callback
# ──────────────────────────────────────────────────────────────────────────────

def _gui() -> None:
    """Called by imgui every frame."""

    vp = imgui.get_main_viewport()
    imgui.set_next_window_pos(vp.work_pos)
    imgui.set_next_window_size(vp.work_size)

    win_flags = (
        imgui.WindowFlags_.no_title_bar
        | imgui.WindowFlags_.no_resize
        | imgui.WindowFlags_.no_move
        | imgui.WindowFlags_.no_scrollbar
        | imgui.WindowFlags_.no_saved_settings
    )
    imgui.begin("##root", flags=win_flags)

    if not _results:
        imgui.text_colored((1.0, 0.4, 0.4, 1.0), "Sonuç yok.")
        imgui.end()
        return

    _summary_bar()
    imgui.separator()

    avail   = imgui.get_content_region_avail()
    tbl_w   = avail.x * 0.48
    spacing = imgui.get_style().item_spacing.x

    # Sol panel: sonuç tablosu
    imgui.begin_child("##tbl_panel", (tbl_w, avail.y))
    _table()
    imgui.end_child()

    imgui.same_line()

    # Sağ panel: grafikler
    imgui.begin_child("##chart_panel", (avail.x - tbl_w - spacing, avail.y))
    _charts()
    imgui.end_child()

    imgui.end()


# ──────────────────────────────────────────────────────────────────────────────
# Özet satırı
# ──────────────────────────────────────────────────────────────────────────────

def _summary_bar() -> None:
    best  = _results[0]
    worst = _results[-1]
    imgui.text(
        f"Kombinasyon: {len(_results)}"
        f"  │  Best  → "
        f"NetProfit={best.get('net_profit', 0):.2f}  "
        f"Win%={best.get('win_rate', 0):.1f}  "
        f"PFNet={best.get('profit_factor_net', 0):.2f}  "
        f"MaxDD={best.get('max_drawdown', 0):.2f}"
        f"  │  Worst NetProfit={worst.get('net_profit', 0):.2f}"
    )


# ──────────────────────────────────────────────────────────────────────────────
# Sonuç tablosu
# ──────────────────────────────────────────────────────────────────────────────

_COLOR_BEST  = (0.25, 0.90, 0.25, 1.0)   # yeşil — en iyi
_COLOR_POS   = (0.90, 0.90, 0.90, 1.0)   # beyaz — pozitif
_COLOR_NEG   = (1.00, 0.40, 0.40, 1.0)   # kırmızı — negatif


def _table() -> None:
    param_keys = list(_results[0].get("parameters", {}).keys()) if _results else []
    ncols      = len(param_keys) + 5       # params | NetProfit | Win% | PFNet | MaxDD | İşlem

    tbl_flags = (
        imgui.TableFlags_.borders_inner_v
        | imgui.TableFlags_.row_bg
        | imgui.TableFlags_.scroll_y
        | imgui.TableFlags_.sizing_stretch_prop
    )

    if not imgui.begin_table("##opt_tbl", ncols, tbl_flags):
        return

    # Başlıklar
    for k in param_keys:
        imgui.table_setup_column(k)
    imgui.table_setup_column("NetProfit")
    imgui.table_setup_column("Win%")
    imgui.table_setup_column("PFNet")
    imgui.table_setup_column("MaxDD")
    imgui.table_setup_column("İşlem")
    imgui.table_setup_scroll_freeze(0, 1)     # başlık satırını dondur
    imgui.table_headers_row()

    for i, r in enumerate(_results):
        params = r.get("parameters", {})
        values = r.get("values", {})
        imgui.table_next_row()

        # Parametre sütunları
        for col, k in enumerate(param_keys):
            imgui.table_set_column_index(col)
            imgui.text_unformatted(str(params.get(k, "")))

        base_col = len(param_keys)

        # NetProfit — renk kodlu
        np_val = r.get("net_profit", 0.0)
        color  = _COLOR_BEST if i == 0 else (_COLOR_NEG if np_val < 0 else _COLOR_POS)
        imgui.table_set_column_index(base_col)
        imgui.push_style_color(imgui.Col_.text, color)
        imgui.text(f"{np_val:.2f}")
        imgui.pop_style_color()

        # Kalan sütunlar
        imgui.table_set_column_index(base_col + 1)
        imgui.text(f"{r.get('win_rate', 0):.1f}")

        imgui.table_set_column_index(base_col + 2)
        imgui.text(f"{r.get('profit_factor_net', 0):.2f}")

        imgui.table_set_column_index(base_col + 3)
        imgui.text(f"{r.get('max_drawdown', 0):.2f}")

        imgui.table_set_column_index(base_col + 4)
        imgui.text(values.get("IslemSayisi", "N/A"))

    imgui.end_table()


# ──────────────────────────────────────────────────────────────────────────────
# Grafikler
# ──────────────────────────────────────────────────────────────────────────────

def _charts() -> None:
    avail   = imgui.get_content_region_avail()
    chart_h = (avail.y - imgui.get_style().item_spacing.y) / 2.0

    xs          = [float(i) for i in range(len(_results))]
    net_profits = [r.get("net_profit",        0.0) for r in _results]
    pf_nets     = [r.get("profit_factor_net", 0.0) for r in _results]
    max_dds     = [abs(r.get("max_drawdown",  0.0)) for r in _results]

    # ── Grafik 1: Net Profit bar chart ──────────────────────────────────────
    if implot.begin_plot("Net Profit (sıralı, en iyi → en kötü)", (-1.0, chart_h)):
        implot.setup_axes("Kombinasyon #", "Net Profit (TL)")
        implot.setup_axis_limits(
            implot.ImAxis_.x1,
            -0.5,
            float(len(xs)) - 0.5,
            implot.Cond_.always,
        )
        implot.plot_bars("Net Profit", xs, net_profits, 0.7)
        implot.end_plot()

    # ── Grafik 2: PFNet vs MaxDrawdown scatter ───────────────────────────────
    if implot.begin_plot("PFNet vs MaxDrawdown", (-1.0, chart_h)):
        implot.setup_axes("MaxDrawdown (|TL|)", "ProfitFactor Net")
        implot.plot_scatter("Kombinasyonlar", max_dds, pf_nets)
        implot.end_plot()


# ══════════════════════════════════════════════════════════════════════════════
# show_single_trader_data — SingleTrader equity curve görselleştirici
# ══════════════════════════════════════════════════════════════════════════════

def show_single_trader_data(data: Any) -> None:
    """
    SingleTrader koşum sonuçlarını imgui_bundle penceresiyle gösterir.
    Pencere kapanana dek bloklar (C# thread bekler).

    Parameters
    ----------
    data : dict  (json.loads çıktısı)
        Beklenen anahtarlar:
            symbol_name, symbol_period, strategy_name, bar_count,
            equity_gross, equity_net, getiri_net,
            ilk_bakiye, net_profit, net_profit_yuzde,
            islem_sayisi, kazanilan_islem, kaybedilen_islem, win_rate, komisyon
    """
    global _trader_data
    _trader_data = dict(data)

    runner = hello_imgui.RunnerParams()
    runner.app_window_params.window_title         = (
        f"AlgoTrade — {_trader_data.get('symbol_name','?')} "
        f"{_trader_data.get('symbol_period','?')} — "
        f"{_trader_data.get('strategy_name','?')}"
    )
    runner.app_window_params.window_geometry.size = (1200, 720)
    runner.callbacks.show_gui                     = _st_gui
    runner.fps_idling.enable_idling               = True
    runner.fps_idling.fps_idle                    = 20

    immapp.run(runner_params=runner, with_implot=True)


# ── SingleTrader GUI frame callback ───────────────────────────────────────────

def _st_gui() -> None:
    vp = imgui.get_main_viewport()
    imgui.set_next_window_pos(vp.work_pos)
    imgui.set_next_window_size(vp.work_size)
    win_flags = (
        imgui.WindowFlags_.no_title_bar
        | imgui.WindowFlags_.no_resize
        | imgui.WindowFlags_.no_move
        | imgui.WindowFlags_.no_scrollbar
        | imgui.WindowFlags_.no_saved_settings
    )
    imgui.begin("##st_root", flags=win_flags)

    if not _trader_data:
        imgui.text("Veri yok.")
        imgui.end()
        return

    _st_header()
    imgui.separator()
    _st_charts()

    imgui.end()


def _st_header() -> None:
    """Üst bilgi satırı: sembol, istatistikler."""
    d          = _trader_data
    np_val     = d.get("net_profit", 0.0)
    np_yuzde   = d.get("net_profit_yuzde", 0.0)
    win_rate   = d.get("win_rate", 0.0)
    komisyon   = d.get("komisyon", 0.0)
    islem      = d.get("islem_sayisi", 0)
    kazanilan  = d.get("kazanilan_islem", 0)

    color = (0.25, 0.90, 0.25, 1.0) if np_val >= 0 else (1.0, 0.40, 0.40, 1.0)
    imgui.text(
        f"{d.get('symbol_name','?')}  {d.get('symbol_period','?')}  │  "
        f"Strateji: {d.get('strategy_name','?')}  │  "
        f"Bar: {d.get('bar_count', 0)}  │  "
        f"İşlem: {islem}  ({kazanilan}W)  │  "
        f"Win%: {win_rate:.1f}  │  "
        f"Komisyon: {komisyon:.2f} TL"
    )
    imgui.same_line()
    imgui.push_style_color(imgui.Col_.text, color)
    imgui.text(f"  NetProfit: {np_val:.2f} TL  ({np_yuzde:.2f}%)")
    imgui.pop_style_color()


def _st_charts() -> None:
    """Equity curve (gross + net) ve drawdown (getiri_net)."""
    d          = _trader_data
    eq_gross   = list(d.get("equity_gross", []))
    eq_net     = list(d.get("equity_net",   []))
    getiri_net = list(d.get("getiri_net",   []))

    if not eq_net:
        imgui.text("Liste verisi yok.")
        return

    n    = len(eq_net)
    xs   = [float(i) for i in range(n)]

    avail   = imgui.get_content_region_avail()
    chart_h = (avail.y - imgui.get_style().item_spacing.y) * 0.65

    # ── Grafik 1: Equity Curve ────────────────────────────────────────────────
    if implot.begin_plot("Equity Curve", (-1.0, chart_h)):
        implot.setup_axes("Bar #", "Bakiye (TL)")
        if eq_gross:
            implot.plot_line("Brüt",  xs, eq_gross)
        implot.plot_line("Net",   xs, eq_net)
        implot.end_plot()

    # ── Grafik 2: Getiri Net (drawdown görünümü) ──────────────────────────────
    dd_h = avail.y - chart_h - imgui.get_style().item_spacing.y
    if implot.begin_plot("Net Getiri (TL)", (-1.0, dd_h)):
        implot.setup_axes("Bar #", "Getiri Net (TL)")
        implot.plot_line("Getiri Net", xs, getiri_net)
        implot.end_plot()
