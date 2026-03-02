import numpy as np


# Her trader için renk paleti (mainTrader ilk renk, child'lar sırayla)
_TRADER_COLORS = [
    (1.0, 1.0, 1.0, 1.0),   # mainTrader — beyaz
    (0.2, 0.8, 1.0, 1.0),   # child 0    — cyan
    (1.0, 0.8, 0.0, 1.0),   # child 1    — sarı
    (0.3, 1.0, 0.3, 1.0),   # child 2    — yeşil
    (1.0, 0.4, 0.4, 1.0),   # child 3    — kırmızı
    (1.0, 0.5, 0.0, 1.0),   # child 4    — turuncu
    (0.7, 0.4, 1.0, 1.0),   # child 5    — mor
    (0.0, 1.0, 0.8, 1.0),   # child 6    — teal
    (1.0, 0.2, 0.8, 1.0),   # child 7    — pembe
]


class MultipleDataPlotter:
    """
    trader_list[0]  = mainTrader (TradeData)
    trader_list[1:] = child traders (TradeData)

    Tüm trader'lar aynı OHLC sembolünü paylaşır.
    Paneller:
        0 — OHLC (mainTrader'dan)
        1 — Sinyaller overlay  (mainTrader + child'lar)
        2 — Getiri Net overlay (mainTrader + child'lar)
        3 — Bakiye Net overlay (mainTrader + child'lar)
        4 — mainTrader PnL
    """

    def __init__(self, trader_list):
        if not trader_list or len(trader_list) == 0:
            raise ValueError("trader_list boş olamaz.")
        self._traders = list(trader_list)   # [mainTrader, child0, child1, ...]
        self._main    = self._traders[0]
        self._children = self._traders[1:]

    # ------------------------------------------------------------------
    # Public
    # ------------------------------------------------------------------

    def plot(self) -> bool:
        self._print_info()
        self._plot_imgui()
        return True

    # ------------------------------------------------------------------
    # Info
    # ------------------------------------------------------------------

    def _print_info(self) -> None:
        sep = "-" * 60
        print()
        print(sep)
        print(f"  MultipleTrader — {len(self._traders)} trader  "
              f"({len(self._children)} child)")
        print(sep)
        for i, td in enumerate(self._traders):
            label = "mainTrader" if i == 0 else f"child[{i - 1}]"
            n     = len(td.closes)
            buys  = sum(1 for v in td.sinyal_list if v > 0)
            sells = sum(1 for v in td.sinyal_list if v < 0)
            last_net = td.getiri_fiyat_net_list[-1] if td.getiri_fiyat_net_list else 0
            print(f"  {label:<14}  bars={n}  Al={buys}  Sat={sells}  "
                  f"GetiriNet={last_net:.2f}")
        print(sep)
        print()

    # ------------------------------------------------------------------
    # ImGui plot
    # ------------------------------------------------------------------

    def _plot_imgui(self) -> None:
        try:
            from data_plotter_img_bundle import DataPlotterImgBundle, DataType
            from imgui_bundle import immapp
        except ImportError as e:
            print(f"Import error: {e}")
            return

        main  = self._main
        n     = len(main.closes)
        t     = np.arange(n, dtype=np.float64)

        opens   = np.array(main.opens,   dtype=np.float64)
        highs   = np.array(main.highs,   dtype=np.float64)
        lows    = np.array(main.lows,    dtype=np.float64)
        closes  = np.array(main.closes,  dtype=np.float64)
        volumes = np.array(main.volumes, dtype=np.float64)
        ohlc    = np.column_stack([opens, highs, lows, closes])

        plotter = DataPlotterImgBundle()
        plotter.setTimeData(t)
        plotter.setOHLCData(ohlc)
        plotter.setVolumeData(volumes)
        plotter.setDateTimeLabels(main.date_times)
        plotter.setTradeSignals(np.array(main.sinyal_list, dtype=np.float64))
        plotter.setWindowTitle(f"{main.title}  {main.periyot}  — MultipleTrader")
        plotter.setEnableSharedCrossHair(True)
        plotter.setEnableSharedXAxis(True)
        plotter.setShowInfoOnAllPanels(True)
        plotter.setShowTradeSignals(True)
        plotter.setEnableRangeSlider(False)

        # ------------------------------------------------------------------
        # Panel 0: OHLC
        # ------------------------------------------------------------------
        p0 = plotter.AddPanel(0)
        p0.setTitle("Price Chart")
        p0.setYAxisLabel("Price")
        p0.setHeightRatio(1.0)
        p0.setOHLCData(plotter.getOHLCData())
        p0.setInfoPanelPosition(100, 2)
        p0.setInfoPanelOffsets(label_dx=5, value_dx=80)

        # ------------------------------------------------------------------
        # Panel 1: Sinyaller overlay
        # ------------------------------------------------------------------
        p1 = plotter.AddPanel(1)
        p1.setTitle("Signals")
        p1.setYAxisLabel("Signal")
        p1.setHeightRatio(0.4)
        p1.setInfoPanelPosition(120, 2)
        p1.setInfoPanelOffsets(label_dx=5, value_dx=80)

        for i, td in enumerate(self._traders):
            label  = "Main" if i == 0 else f"Child {i}"
            color  = _TRADER_COLORS[i % len(_TRADER_COLORS)]
            sig    = np.array(td.sinyal_list, dtype=np.float64)
            p1.setData(i, DataType.Stairs, sig, label, color)

        padding = np.full(n, 2.0, dtype=np.float64)
        p1.setData(998, DataType.Line,  padding,  "##pad+", (1, 1, 1, 0))
        p1.setData(999, DataType.Line, -padding,  "##pad-", (1, 1, 1, 0))

        # ------------------------------------------------------------------
        # Panel 2: Getiri Net overlay
        # ------------------------------------------------------------------
        p2 = plotter.AddPanel(2)
        p2.setTitle("Net Return")
        p2.setYAxisLabel("Getiri Net")
        p2.setHeightRatio(0.5)
        p2.setInfoPanelPosition(100, 2)
        p2.setInfoPanelOffsets(label_dx=5, value_dx=80)

        for i, td in enumerate(self._traders):
            if td.getiri_fiyat_net_list:
                label = "Main" if i == 0 else f"Child {i}"
                color = _TRADER_COLORS[i % len(_TRADER_COLORS)]
                arr   = np.array(td.getiri_fiyat_net_list, dtype=np.float64)
                p2.setData(i, DataType.Line, arr, label, color)

        # ------------------------------------------------------------------
        # Panel 3: Bakiye Net overlay
        # ------------------------------------------------------------------
        p3 = plotter.AddPanel(3)
        p3.setTitle("Net Balance")
        p3.setYAxisLabel("Bakiye Net")
        p3.setHeightRatio(0.5)
        p3.setInfoPanelPosition(100, 2)
        p3.setInfoPanelOffsets(label_dx=5, value_dx=80)

        for i, td in enumerate(self._traders):
            if td.bakiye_fiyat_net_list:
                label = "Main" if i == 0 else f"Child {i}"
                color = _TRADER_COLORS[i % len(_TRADER_COLORS)]
                arr   = np.array(td.bakiye_fiyat_net_list, dtype=np.float64)
                p3.setData(i, DataType.Line, arr, label, color)

        # ------------------------------------------------------------------
        # Panel 4: mainTrader PnL (Kar/Zarar)
        # ------------------------------------------------------------------
        if main.kar_zarar_fiyat_list:
            p4 = plotter.AddPanel(4)
            p4.setTitle("Main PnL")
            p4.setYAxisLabel("PnL")
            p4.setHeightRatio(0.5)
            p4.setInfoPanelPosition(100, 2)
            p4.setInfoPanelOffsets(label_dx=5, value_dx=80)
            arr = np.array(main.kar_zarar_fiyat_list, dtype=np.float64)
            p4.setData(0, DataType.Line, arr, "PnL", _TRADER_COLORS[0])

        # Y-axis sync
        plotter.RegisterYSyncGroup(0, p0)

        print(f"  {len(plotter.panels)} panels created, opening window...")
        immapp.run(plotter.Plot, with_implot=True, window_size=(1600, 2000))
