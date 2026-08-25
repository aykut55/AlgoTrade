"""
bundle_loader.py - .npz/.view.json bundle dosyalarini (TradeDataBundleConverter'in urettigi
formatta) okuyup PythonPlotter.PlotBundleFileFromDisk (PythonPlotter.cs) icin bir TradeData
nesnesi olusturur.

NpzReader.cs (C#) ile ayni isi yapan, ama okumayi Python/numpy tarafinda (np.load) yapan
alternatif/karsilastirma yolu - bkz. PythonPlotter.PlotBundleFile (memory/NpzReader) vs
PlotBundleFileFromDisk (bu dosya).

NOT: bundle'da henuz bakiye/komisyon/net-bakiye serileri yok (bkz. docs/todo.md
"Kapatilmasi gereken kucuk bosluklar"), o yuzden bu 3 alan bos liste kalir. MA5/MA8/...
gibi indikator overlay'leri de (PlotSingleTraderData/PlotBundleFile'daki gibi) burada
HESAPLANMIYOR - sadece bundle'da zaten var olan seriler (OHLC/signal/PnL/Return/strateji
indikatorleri) doldurulur.
"""
import json
from datetime import datetime

import numpy as np

from trade_data import TradeData

_KNOWN_SERIES = {
    "PnL": "kar_zarar_fiyat_list",
    "PnL %": "kar_zarar_fiyat_yuzde_list",
    "Return": "getiri_fiyat_list",
    "Net Return": "getiri_fiyat_net_list",
    "Return %": "getiri_fiyat_yuzde_list",
    "Net Return %": "getiri_fiyat_net_yuzde_list",
}


def _parse_iso(ts: str) -> datetime:
    """C# DateTime.ToString("o") 7 haneli fractional-second (100ns tick) uretebilir;
    Python'un datetime.fromisoformat'i (<3.11) en fazla 6 hane (mikrosaniye) kabul eder -
    Python surumunden bagimsiz calismasi icin 6 haneye kirpiyoruz."""
    if "." in ts:
        head, frac = ts.split(".", 1)
        ts = f"{head}.{frac[:6]}"
    return datetime.fromisoformat(ts)


def build_trade_data_from_bundle(bundle_path, view_path=None):
    """view_path su an KULLANILMIYOR (eski tip plotter'in kendi sabit panel yerlesimi var,
    bkz. data_plotter.py) - ileride view.json'daki panel/seri secimini yansitmak icin
    ayrilmis parametre."""
    npz = np.load(bundle_path, allow_pickle=False)

    date_times = [_parse_iso(str(ts)) for ts in npz["timestamps"]]

    td = TradeData()
    td.date_times = [dt.strftime("%Y.%m.%d %H:%M:%S") for dt in date_times]
    td.dates      = [dt.strftime("%Y.%m.%d") for dt in date_times]
    td.times      = [dt.strftime("%H:%M:%S") for dt in date_times]

    td.opens   = npz["open"].tolist()
    td.highs   = npz["high"].tolist()
    td.lows    = npz["low"].tolist()
    td.closes  = npz["close"].tolist()
    td.volumes = [round(v) for v in npz["volume"].tolist()]
    td.lots    = npz["size"].tolist()

    td.sinyal_list = [float(v) for v in npz["signal_steps"].tolist()]

    # bakiye/komisyon/net-bakiye: bundle'da henuz yok, bos birakiliyor (bkz. yukaridaki NOT).

    strategy_indicators = {}
    if "indicator_names" in npz.files and "indicator_values" in npz.files:
        names = npz["indicator_names"]
        values = npz["indicator_values"]
        for i, raw_name in enumerate(names):
            name = str(raw_name)
            row = values[i].tolist()
            attr = _KNOWN_SERIES.get(name)
            if attr:
                setattr(td, attr, row)
            else:
                strategy_indicators[name] = row
    td.strategy_indicators = strategy_indicators

    if "meta_json" in npz.files:
        try:
            meta = json.loads(str(npz["meta_json"]))
            td.title = meta.get("symbol", td.title)
            td.periyot = meta.get("periyot", td.periyot)
        except Exception:
            pass

    return td
