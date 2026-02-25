from trade_data import TradeData


class DataPlotter:
    """
    TradeData nesnesini alıp görselleştirme / istatistik işlemlerini yürüten sınıf.

    Kullanım (C# → pythonnet):
        dp = DataPlotter(trade_data)
        result = dp.plot()          # → bool
    """

    def __init__(self, trade_data: TradeData):
        self.td = trade_data

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def plot(self) -> bool:
        """
        Ana görselleştirme metodu.
        Şu an print_info() çağırır; ileride imgui_bundle plot buraya eklenir.
        """
        self.print_info()
        self._plot_imgui()   # ← eklenecek
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
    # Private
    # ------------------------------------------------------------------

    def _plot_imgui(self) -> None:
        """imgui_bundle ile interaktif grafik penceresi açar."""
        pass
