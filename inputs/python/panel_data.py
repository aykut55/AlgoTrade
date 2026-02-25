class PanelData:
    """
    Bir panel içindeki tek bir veri serisini temsil eder.

    data_type: "line" | "bar" | "scatter" | "candlestick" | "histogram"
    """

    def __init__(self, data_id: int, name: str, data_type: str = "line"):
        self.id        = data_id
        self.name      = name
        self.data_type = data_type
        self.values    = []        # list[float] veya list[tuple[o,h,l,c]]
        self.color     = None      # str | tuple[r,g,b,a] | None
        self.visible   = True
        self.label     = name
        self.thickness = 1.0
        self.fill      = False

    def __repr__(self):
        return (f"PanelData(id={self.id}, name={self.name!r}, "
                f"type={self.data_type!r}, n={len(self.values)})")
