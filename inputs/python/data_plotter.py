from trade_data  import TradeData
from panel       import Panel
from panel_data  import PanelData


# ======================================================================
# DataPlotter
# ======================================================================

class DataPlotter:
    """
    TradeData nesnesini alıp panel bazlı görselleştirme yapan sınıf.

    Otomatik oluşturulan paneller (TradeData dolu ise):
        Panel 0 : OHLC          (her zaman)
        Panel 1 : Sinyal
        Panel 2 : Kar/Zarar
        Panel 3 : Getiri / Getiri Net
        Panel 4 : Bakiye / Bakiye Net

    Kullanım (C# → pythonnet):
        dp     = DataPlotter(trade_data)
        result = dp.plot()                  # → bool

    Kullanım (Python — indikatör ekleme):
        dp = DataPlotter(trade_data)
        dp.AddIndicatorToPanel(0, "ma5")            # OHLC üzerine çiz
        dp.AddIndicatorToPanel(0, "ma8")
        dp.AddPanel(5, "MOST")                      # yeni panel
        dp.AddIndicatorToPanel(5, "Most")
        dp.AddIndicatorToPanel(5, "mostEma")
        dp.plot()
    """

    def __init__(self, trade_data: TradeData):
        self.td = trade_data
        self._panels: dict[int, Panel]  = {}   # id   → Panel
        self._by_name: dict[str, Panel] = {}   # name → Panel
        self._setup_default_panels()

    # ------------------------------------------------------------------
    # Public API — Panel yönetimi
    # ------------------------------------------------------------------

    def AddPanel(self, panel_id: int, name: str) -> None:
        """Yeni panel oluşturur. panel_id kullanıcının sorumluluğundadır."""
        if panel_id in self._panels:
            raise ValueError(f"Panel id={panel_id} zaten mevcut.")
        if name in self._by_name:
            raise ValueError(f"Panel name='{name}' zaten mevcut.")
        panel = Panel(panel_id, name)
        self._panels[panel_id]  = panel
        self._by_name[name]     = panel

    def RemovePanel(self, panel_id: int) -> None:
        """Id ile paneli kaldırır. Panel 0 (OHLC) kaldırılamaz."""
        if panel_id == 0:
            raise ValueError("Panel 0 (OHLC) kaldırılamaz.")
        panel = self._panels.pop(panel_id, None)
        if panel:
            self._by_name.pop(panel.name, None)

    def GetPanelById(self, panel_id: int) -> "Panel | None":
        """Id ile Panel nesnesini döndürür. Bulunamazsa None döner."""
        return self._panels.get(panel_id)

    def GetPanelByName(self, name: str) -> "Panel | None":
        """İsim ile Panel nesnesini döndürür. Bulunamazsa None döner."""
        return self._by_name.get(name)

    @property
    def panel_count(self) -> int:
        return len(self._panels)

    # ------------------------------------------------------------------
    # Public API — Görselleştirme
    # ------------------------------------------------------------------

    def plot(self) -> bool:
        """Ana görselleştirme metodu."""
        self.print_info()
        self._plot_imgui()
        return True

    def print_info(self) -> None:
        """Ticaret verisi özetini stdout'a yazar (C# stdout capture ile yakalanır)."""
        td  = self.td
        n   = len(td.closes)
        sep = "-" * 52

        print()
        print(sep)
        print(f"  {td.title}  |  {td.periyot}  |  {n} bar")
        print(sep)

        if n > 0:
            print(f"  Tarih araligi  : {td.dates[0]}  →  {td.dates[-1]}")
            print(f"  Close          : "
                  f"min={min(td.closes):.4f}  "
                  f"max={max(td.closes):.4f}  "
                  f"son={td.closes[-1]:.4f}")

        buy_count  = sum(1 for v in td.sinyal_list if v > 0)
        sell_count = sum(1 for v in td.sinyal_list if v < 0)
        print(f"  Sinyaller      : Al={buy_count}  Sat={sell_count}")

        if td.bakiye_fiyat_list:
            b = td.bakiye_fiyat_list
            print(f"  Bakiye         : "
                  f"baslangic={b[0]:.2f}  son={b[-1]:.2f}  "
                  f"min={min(b):.2f}  max={max(b):.2f}")

        if td.bakiye_fiyat_net_list:
            bn = td.bakiye_fiyat_net_list
            print(f"  Bakiye Net     : "
                  f"baslangic={bn[0]:.2f}  son={bn[-1]:.2f}  "
                  f"min={min(bn):.2f}  max={max(bn):.2f}")

        if td.komisyon_fiyat_list:
            print(f"  Komisyon       : toplam={sum(td.komisyon_fiyat_list):.2f}")

        if td.getiri_fiyat_net_list:
            print(f"  Getiri Net     : toplam={sum(td.getiri_fiyat_net_list):.2f}")

        if td.strategy_indicators:
            keys = list(td.strategy_indicators.keys()) \
                   if hasattr(td.strategy_indicators, 'keys') else []
            print(f"  Indikatörler   : "
                  f"{', '.join(f'{k}({len(td.strategy_indicators[k])})' for k in keys)}")
        else:
            print(f"  Indikatörler   : (yok)")

        print(sep)
        print()

    # ------------------------------------------------------------------
    # Public API — İndikatör yönetimi
    # ------------------------------------------------------------------

    def AddIndicatorToPanel(self, panel_id: int, indicator_name: str,
                            data_type: str = "line") -> None:
        """
        td.strategy_indicators içindeki bir indikatörü belirtilen panele ekler.

        Örnek:
            dp.AddIndicatorToPanel(0, "ma5")          # OHLC üzerine
            dp.AddIndicatorToPanel(0, "ma8")
            dp.AddIndicatorToPanel(5, "Most")         # ayrı panel (önceden AddPanel ile oluşturulmuş olmalı)
            dp.AddIndicatorToPanel(5, "mostEma")
        """
        panel = self.GetPanelById(panel_id)
        if panel is None:
            raise ValueError(f"Panel id={panel_id} bulunamadı.")
        values = self.td.strategy_indicators.get(indicator_name)
        if values is None:
            raise KeyError(f"strategy_indicators içinde '{indicator_name}' yok.")
        data_id = panel.data_count          # bir sonraki serbest id
        pd = panel.AddData(data_id, indicator_name, data_type)
        pd.values = list(values)

    # ------------------------------------------------------------------
    # Private
    # ------------------------------------------------------------------

    def _setup_default_panels(self) -> None:
        """
        Pre-defined TradeData alanlarından panelleri otomatik oluşturur.

        Panel 0 : OHLC          — her zaman
        Panel 1 : Sinyal        — sinyal_list doluysa
        Panel 2 : Kar/Zarar     — kar_zarar_fiyat_list doluysa
        Panel 3 : Getiri        — getiri_fiyat_list veya getiri_fiyat_net_list doluysa
        Panel 4 : Bakiye        — bakiye_fiyat_list veya bakiye_fiyat_net_list doluysa

        strategy_indicators otomatik eklenmez; kullanıcı AddIndicatorToPanel ile
        istediği panele istediği indikatörü ekler.
        """
        td = self.td

        # --- Panel 0: OHLC ------------------------------------------------
        self.AddPanel(0, "OHLC")
        ohlc = self.GetPanelById(0)
        ohlc_data = ohlc.AddData(0, "OHLC", "candlestick")
        ohlc_data.values = list(zip(td.opens, td.highs, td.lows, td.closes))

        # --- Panel 1: Sinyal ----------------------------------------------
        if td.sinyal_list:
            self.AddPanel(1, "Sinyal")
            p = self.GetPanelById(1)
            p.height = 0.35
            d = p.AddData(0, "Sinyal", "bar")
            d.values = list(td.sinyal_list)

        # --- Panel 2: Kar/Zarar ------------------------------------------
        if td.kar_zarar_fiyat_list:
            self.AddPanel(2, "Kar/Zarar")
            p = self.GetPanelById(2)
            p.height = 0.5
            d = p.AddData(0, "Kar/Zarar", "bar")
            d.values = list(td.kar_zarar_fiyat_list)

        # --- Panel 3: Getiri ---------------------------------------------
        if td.getiri_fiyat_list or td.getiri_fiyat_net_list:
            self.AddPanel(3, "Getiri")
            p = self.GetPanelById(3)
            p.height = 0.5
            if td.getiri_fiyat_list:
                d = p.AddData(0, "Getiri", "line")
                d.values = list(td.getiri_fiyat_list)
            if td.getiri_fiyat_net_list:
                d = p.AddData(1, "Getiri Net", "line")
                d.values = list(td.getiri_fiyat_net_list)

        # --- Panel 4: Bakiye ---------------------------------------------
        if td.bakiye_fiyat_list or td.bakiye_fiyat_net_list:
            self.AddPanel(4, "Bakiye")
            p = self.GetPanelById(4)
            p.height = 0.5
            if td.bakiye_fiyat_list:
                d = p.AddData(0, "Bakiye", "line")
                d.values = list(td.bakiye_fiyat_list)
            if td.bakiye_fiyat_net_list:
                d = p.AddData(1, "Bakiye Net", "line")
                d.values = list(td.bakiye_fiyat_net_list)

    def _plot_imgui(self) -> None:
        """imgui_bundle ile interaktif grafik penceresi açar."""
        pass
