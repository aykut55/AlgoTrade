from panel_data import PanelData


class Panel:
    """
    Represents a chart panel (sub-chart).

    Usage:
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
    # Data management
    # ------------------------------------------------------------------

    def AddData(self, data_id: int, name: str, data_type: str = "line") -> PanelData:
        """Adds a new data series to the panel; returns the PanelData object."""
        if data_id in self._data_by_id:
            raise ValueError(f"Data id={data_id} already exists.")
        if name in self._data_by_name:
            raise ValueError(f"Data name='{name}' already exists.")
        pd = PanelData(data_id, name, data_type)
        self._data_by_id[data_id]  = pd
        self._data_by_name[name]   = pd
        return pd

    def RemoveData(self, data_id: int) -> None:
        """Removes a data series by id."""
        pd = self._data_by_id.pop(data_id, None)
        if pd:
            self._data_by_name.pop(pd.name, None)

    def GetDataById(self, data_id: int) -> "PanelData | None":
        """Returns PanelData by id. Returns None if not found."""
        return self._data_by_id.get(data_id)

    def GetDataByName(self, name: str) -> "PanelData | None":
        """Returns PanelData by name. Returns None if not found."""
        return self._data_by_name.get(name)

    def ResetData(self) -> None:
        """Clears the values list of all data series."""
        for pd in self._data_by_id.values():
            pd.values = []

    def ResetDataById(self, data_id: int) -> None:
        """Clears the values list of the data series with the given id."""
        pd = self._data_by_id.get(data_id)
        if pd:
            pd.values = []

    def ResetDataByName(self, name: str) -> None:
        """Clears the values list of the data series with the given name."""
        pd = self._data_by_name.get(name)
        if pd:
            pd.values = []

    def Clear(self) -> None:
        """Removes all PanelData objects; the panel becomes empty."""
        self._data_by_id.clear()
        self._data_by_name.clear()

    def Reset(self) -> None:
        """Clears the values list of all data series; objects are retained."""
        self.ResetData()

    # ------------------------------------------------------------------

    @property
    def data_count(self) -> int:
        return len(self._data_by_id)

    def __repr__(self):
        return (f"Panel(id={self.id}, name={self.name!r}, "
                f"data_count={self.data_count})")
