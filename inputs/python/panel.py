from panel_data import PanelData


class Panel:
    """
    Bir grafik panelini (alt grafiği) temsil eder.

    Kullanım:
        panel.AddData(0, "SMA20", "line")
        data = panel.GetDataById(0)
        data = panel.GetDataByName("SMA20")
        data.values = [...]
        data.color  = (0.2, 0.6, 1.0, 1.0)
    """

    def __init__(self, panel_id: int, name: str):
        self.id     = panel_id
        self.name   = name
        self.height = 1.0
        self._data_by_id:   dict[int, PanelData] = {}
        self._data_by_name: dict[str, PanelData] = {}

    # ------------------------------------------------------------------
    # Data yönetimi
    # ------------------------------------------------------------------

    def AddData(self, data_id: int, name: str, data_type: str = "line") -> PanelData:
        """Panele yeni veri serisi ekler; PanelData nesnesini döndürür."""
        if data_id in self._data_by_id:
            raise ValueError(f"Data id={data_id} zaten mevcut.")
        if name in self._data_by_name:
            raise ValueError(f"Data name='{name}' zaten mevcut.")
        pd = PanelData(data_id, name, data_type)
        self._data_by_id[data_id]  = pd
        self._data_by_name[name]   = pd
        return pd

    def RemoveData(self, data_id: int) -> None:
        """Id ile veri serisini kaldırır."""
        pd = self._data_by_id.pop(data_id, None)
        if pd:
            self._data_by_name.pop(pd.name, None)

    def GetDataById(self, data_id: int) -> "PanelData | None":
        """Id ile PanelData döndürür. Bulunamazsa None."""
        return self._data_by_id.get(data_id)

    def GetDataByName(self, name: str) -> "PanelData | None":
        """İsim ile PanelData döndürür. Bulunamazsa None."""
        return self._data_by_name.get(name)

    def ResetData(self) -> None:
        """Tüm veri serilerinin values listesini temizler."""
        for pd in self._data_by_id.values():
            pd.values = []

    def ResetDataById(self, data_id: int) -> None:
        """Id ile ilgili veri serisinin values listesini temizler."""
        pd = self._data_by_id.get(data_id)
        if pd:
            pd.values = []

    def ResetDataByName(self, name: str) -> None:
        """İsim ile ilgili veri serisinin values listesini temizler."""
        pd = self._data_by_name.get(name)
        if pd:
            pd.values = []

    def Clear(self) -> None:
        """Tüm PanelData nesnelerini kaldırır; panel boşalır."""
        self._data_by_id.clear()
        self._data_by_name.clear()

    def Reset(self) -> None:
        """Tüm veri serilerinin values listesini temizler; nesneler korunur."""
        self.ResetData()

    # ------------------------------------------------------------------

    @property
    def data_count(self) -> int:
        return len(self._data_by_id)

    def __repr__(self):
        return (f"Panel(id={self.id}, name={self.name!r}, "
                f"data_count={self.data_count})")
