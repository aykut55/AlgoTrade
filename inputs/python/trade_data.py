class TradeData:
    """C# tarafından pythonnet üzerinden setter'larla doldurulan veri transfer nesnesi."""

    def __init__(self):
        self.date_times             = []
        self.dates                  = []
        self.times                  = []
        self.opens                  = []
        self.highs                  = []
        self.lows                   = []
        self.closes                 = []
        self.volumes                = []
        self.lots                   = []
        self.sinyal_list            = []
        self.kar_zarar_fiyat_list   = []
        self.bakiye_fiyat_list      = []
        self.getiri_fiyat_list      = []
        self.komisyon_fiyat_list    = []
        self.bakiye_fiyat_net_list      = []
        self.getiri_fiyat_net_list      = []
        self.kar_zarar_fiyat_yuzde_list = []
        self.getiri_fiyat_yuzde_list    = []
        self.getiri_fiyat_net_yuzde_list = []
        self.strategy_indicators        = {}
        self.title                  = "AlgoTrade"
        self.periyot                = "1H"
