"""
pythonnet koprusu: Plotter process'i icinden, AlgoTrade (orijinal repo)
C# kodunu (AlgoTrade.Core.dll) DOGRUDAN cagirir - dosya/bundle round-trip'i
YOK. AlgoTrade.Console'un build ciktisi kullanilir (class library'nin kendi
runtimeconfig.json'u olmadigi icin; AlgoTrade.Core.dll + tum NuGet
bagimliliklari o klasorde zaten hazir bulunuyor).

Kullanim:
    from src.trading.csharpBridge import read_data, run_signal_script
    data, meta = read_data(r"C:\\data\\...csv", last_n=20000)
    ok, signals, error = run_signal_script(csharp_code, data)
"""
import os
import sys
from datetime import datetime

REPO_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
BIN_DIR = os.path.join(REPO_DIR, "AlgoTrade.Console", "bin", "Debug", "net8.0")
RUNTIME_CONFIG = os.path.join(BIN_DIR, "AlgoTrade.Console.runtimeconfig.json")

_loaded = False
_StockDataReader = None
_FilterMode = None
_SignalScriptGlobals = None
_ScriptExecutor = None
_ReplaySimulationGlobals = None
_DotNetSystem = None
_ReadDataScriptGlobals = None
_WriteToDiskScriptGlobals = None
_FilterScriptGlobals = None


def _ensure_loaded():
    global _loaded, _StockDataReader, _FilterMode, _SignalScriptGlobals, _ScriptExecutor
    global _ReplaySimulationGlobals, _DotNetSystem, _ReadDataScriptGlobals, _WriteToDiskScriptGlobals
    global _FilterScriptGlobals
    if _loaded:
        return

    # AlgoTrade.Core'daki AppSettings, RootDir'i AppContext.BaseDirectory'den
    # 4 ust dizin olarak hesaplayan STATIC (readonly, bir kere hesaplanan) bir
    # alan kullanir (normal dotnet-launched exe icin bu AlgoTrade.Console/bin/
    # Debug/net8.0'dir). pythonnet/clr_loader coreclr host'unda AppContext.
    # BaseDirectory bunun yerine process'in o anki CALISMA DIZININE esitleniyor
    # - bu yuzden CLR'i yuklerken cwd'yi BIN_DIR'e alip, static alan ilk
    # kullanimda (asagidaki AppSettings.RootDir erisimiyle, HALA BIN_DIR'deyken)
    # dogru (AlgoTrade repo) klasore cozulmesini sagliyoruz; sonrasinda deger
    # process omru boyunca cache'li kaldigi icin cwd'yi geri alabiliyoruz.
    _prevCwd = os.getcwd()
    os.chdir(BIN_DIR)
    try:
        from pythonnet import load
        load("coreclr", runtime_config=RUNTIME_CONFIG)

        import clr
        if BIN_DIR not in sys.path:
            sys.path.append(BIN_DIR)
        clr.AddReference("AlgoTrade.Core")

        # SADECE AppSettings'e dokun (static alanin cwd==BIN_DIR iken
        # cache'lenmesini zorla) - ScriptExecutor/Roslyn importu asagida,
        # cwd geri alindiktan SONRA yapiliyor (Roslyn'in assembly resolution'i
        # cwd degisikligiyle calisiyordu, bu yuzden pencereyi minimumda tutuyoruz).
        from AlgoTrade.Core import AppSettings
        _ = AppSettings.RootDir
    finally:
        os.chdir(_prevCwd)

    import System
    from AlgoTrade.Core.StockDataReader import StockDataReader
    from AlgoTrade.Core.Scripting import (
        SignalScriptGlobals, ScriptExecutor, ReplaySimulationGlobals,
        ReadDataScriptGlobals, WriteToDiskScriptGlobals, FilterScriptGlobals,
    )

    _StockDataReader = StockDataReader
    _FilterMode = StockDataReader.FilterMode
    _SignalScriptGlobals = SignalScriptGlobals
    _ScriptExecutor = ScriptExecutor
    _ReplaySimulationGlobals = ReplaySimulationGlobals
    _ReadDataScriptGlobals = ReadDataScriptGlobals
    _WriteToDiskScriptGlobals = WriteToDiskScriptGlobals
    _FilterScriptGlobals = FilterScriptGlobals
    _DotNetSystem = System
    _loaded = True


def run_csharp_file(path, globals_obj):
    """path'teki C# dosyasini okur, globals_obj'a karsi Roslyn ile calistirir -
    tum dosya-tabanli asamalarin (readData/signal/hesapla/diskeYaz.csx) ortak
    cekirdegi. Donus: (success, returnValue, error)."""
    _ensure_loaded()
    with open(path, "r", encoding="utf-8") as f:
        code = f.read()

    executor = _ScriptExecutor()
    result = executor.ExecuteAsync(code, globals_obj).GetAwaiter().GetResult()

    if not result.Success:
        error = result.Error or ""
        if result.CompilationErrors:
            error = "\n".join(str(e) for e in result.CompilationErrors)
        return False, None, error

    return True, result.ReturnValue, None


def read_data_from_file(path, csv_path, last_n=0):
    """inputs/scripts/csharpBridge/readData.csx dosyasini (ReadDataScriptGlobals:
    FilePath, LastN) calistirir.

    Script object[] { data, metaSummary, symbolName, symbolPeriod } dondurur.
    Eski iki elemanli sonuc ve dogrudan List<StockData> da desteklenir.

    Donus: (success, netData, metaSummary, symbolName, symbolPeriod, error)."""
    _ensure_loaded()
    globals_ = _ReadDataScriptGlobals(csv_path, int(last_n))
    ok, ret, err = run_csharp_file(path, globals_)
    if not ok:
        return False, None, "", "", "", err
    if ret is None:
        return False, None, "", "", "", "Script bir deger dondurmedi."

    metaSummary = ""
    symbolName = os.path.splitext(os.path.basename(csv_path))[0]
    symbolPeriod = ""
    netData = ret
    if isinstance(ret, _DotNetSystem.Array) and ret.Length in (2, 4) and hasattr(ret[0], "Count"):
        netData, metaSummary = ret[0], str(ret[1])
        if ret.Length == 4:
            symbolName, symbolPeriod = str(ret[2]), str(ret[3])

    return True, netData, metaSummary, symbolName, symbolPeriod, None


def _to_bridge_signal_codes(signals):
    element_type = signals.GetType().GetElementType() if hasattr(signals, "GetType") else None
    codes = [int(signal) for signal in signals]
    if element_type is not None and element_type.FullName == "AlgoTrade.Core.Trading.Core.TradeSignals":
        from AlgoTrade.Core.Trading.Core import TradeSignals
        sell_code = int(TradeSignals.Sell)
        return [-1 if code == sell_code else code for code in codes]
    return codes


def run_signal_script_from_file(path, net_data):
    """inputs/scripts/csharpBridge/signal.csx dosyasini (SignalScriptGlobals:
    Data, Indicators) calistirir.

    Beklenen return: object[] { signals, indicatorNames, indicatorValues } -
    signals int[] (Data.Count), indicatorNames string[], indicatorValues her
    elemani bir double[] olan dizi (OHLC panelinde cizilecek AL/SAT'i ureten
    MA/indikator serileri - orn. EMA Fast/EMA Slow). Geriye donuk uyumluluk:
    script TradeSignals[] veya eski int[] dondururse de calisir, indicators bos liste olur.

    Donus: (success, signals_list_or_None, indicators, error) - indicators:
    [(name, values_list), ...]."""
    _ensure_loaded()
    globals_ = _SignalScriptGlobals(net_data)
    ok, ret, err = run_csharp_file(path, globals_)
    if not ok:
        return False, None, [], err
    if ret is None:
        return False, None, [], "Script bir deger dondurmedi (return signals; eksik olabilir)."

    try:
        items = list(ret)
    except TypeError:
        return False, None, [], "Script'in dondurdugu deger bir dizi degil."

    indicators = []
    signalsRaw = ret
    if len(items) == 3:
        # Yeni format: object[] { signals, indicatorNames, indicatorValues }
        signalsRaw, namesRaw, valuesRaw = items
        try:
            names = [str(n) for n in namesRaw]
            values = [list(v) for v in valuesRaw]
            indicators = list(zip(names, values))
        except TypeError:
            signalsRaw = ret  # 3-elemanli ama beklenen sekilde degil - eski format varsay
            indicators = []

    try:
        signals = _to_bridge_signal_codes(signalsRaw)
    except TypeError:
        return False, None, [], "Script'in dondurdugu sinyal degeri bir dizi degil (int[] donmeli)."

    if len(signals) != net_data.Count:
        return False, None, [], (
            f"Sinyal dizisi uzunlugu veriyle uyusmuyor: {len(signals)} != {net_data.Count}")
    return True, signals, indicators, None


def run_filter_script_from_file(path, net_data, raw_signals):
    """inputs/scripts/csharpBridge/filter.csx dosyasini (FilterScriptGlobals:
    Data, RawSignals) calistirir. Donus: (success, filtered_signals_list_or_None, error)."""
    _ensure_loaded()
    net_raw_signals = _DotNetSystem.Array[int]([int(s) for s in raw_signals])
    globals_ = _FilterScriptGlobals(net_data, net_raw_signals)
    ok, ret, err = run_csharp_file(path, globals_)
    if not ok:
        return False, None, err
    if ret is None:
        return False, None, "Script bir deger dondurmedi (return signals; eksik olabilir)."
    try:
        signals = list(ret)
    except TypeError:
        return False, None, "Script'in dondurdugu deger bir dizi degil (int[] donmeli)."
    if len(signals) != net_data.Count:
        return False, None, (
            f"Sinyal dizisi uzunlugu veriyle uyusmuyor: {len(signals)} != {net_data.Count}")
    return True, [int(s) for s in signals], None


def run_replay_from_file(path, net_data, signals, symbol_name="", symbol_period=""):
    """scripts_bridge/hesapla.csx dosyasini (ReplaySimulationGlobals: Data,
    SignalCodes, SymbolName, SymbolPeriod) calistirir. Donus: (success, algoTrader, error)."""
    _ensure_loaded()
    # .get(s, s): 1/-1 (basitlestirilmis AL/SAT) Buy(1)/Sell(2)'ye cevrilir;
    # digerleri (0=None, 3=TakeProfit, 4=StopLoss, 5=Flat, 6=Skip) TradeSignals
    # raw degerleriyle ZATEN AYNI oldugu icin OLDUGU GIBI gecer (filter.csx
    # bunlari dogrudan uretebilir - bkz. filter.csx Kar Al/Zarar Kes/Flat bolumleri).
    raw_codes = [_SIGNAL_CODE_TO_TRADESIGNAL.get(s, s) for s in signals]
    net_signal_codes = _DotNetSystem.Array[int](raw_codes)
    globals_ = _ReplaySimulationGlobals(net_data, net_signal_codes, symbol_name or "", symbol_period or "")
    return run_csharp_file(path, globals_)


def read_run_steps(path):
    _ensure_loaded()
    ok, steps, error = run_csharp_file(path, _DotNetSystem.Object())
    if not ok:
        raise RuntimeError(error)
    run_steps = [(str(step.Item1), int(step.Item2)) for step in steps]
    for name, enabled in run_steps:
        if enabled not in (0, 1):
            raise ValueError(f"{name}: Enabled must be 0 or 1.")
    return run_steps


def create_bundle_from_file(path, algo_trader, signal_indicators=()):
    _ensure_loaded()
    globals_ = _WriteToDiskScriptGlobals(algo_trader)
    if signal_indicators:
        from System.Collections.Generic import Dictionary

        double_array = _DotNetSystem.Array[_DotNetSystem.Double]
        indicators = Dictionary[_DotNetSystem.String, double_array]()
        for name, values in signal_indicators:
            indicators[name] = double_array(values)
        globals_ = _WriteToDiskScriptGlobals(algo_trader, indicators)

    ok, result, error = run_csharp_file(path, globals_)
    if not ok:
        return False, [], error
    return True, [str(file_path) for file_path in result], None


def write_to_disk_from_file(path, algo_trader, progress_callback=None):
    """scripts_bridge/diskeYaz.csx dosyasini (WriteToDiskScriptGlobals: Trader)
    calistirir; oncesinde/sonrasinda SingleTrader'in dosya-adi property'lerinden
    (bkz. _STATS_FILENAME_PROPS) beklenen/yazilan dosya listesini cikarip
    progress_callback("start"/"file"/"done", payload, total) ile bildirir.
    Donus: (success, written_list, error) - written_list: [(propName, fullPath, sizeBytes), ...]."""
    _ensure_loaded()
    from AlgoTrade.Core import AppSettings
    logs_dir = str(AppSettings.LogsDir)
    single_trader = algo_trader.SingleTrader

    expected = []
    for prop in _STATS_FILENAME_PROPS:
        name = getattr(single_trader, prop, None)
        if name:
            expected.append((prop, os.path.join(logs_dir, str(name))))

    if progress_callback:
        progress_callback("start", None, len(expected))

    globals_ = _WriteToDiskScriptGlobals(algo_trader)
    ok, _ret, err = run_csharp_file(path, globals_)
    if not ok:
        return False, [], err

    written = []
    for prop, filePath in expected:
        if os.path.isfile(filePath):
            size = os.path.getsize(filePath)
            written.append((prop, filePath, size))
            if progress_callback:
                progress_callback("file", (prop, filePath, size), len(expected))

    if progress_callback:
        progress_callback("done", written, len(expected))
    return True, written, None


def read_data(file_path, last_n=0):
    """CSV'den (C# StockDataReader ile) veri okur.

    last_n <= 0 ise TUM veri okunur (FilterMode.All); last_n > 0 ise sadece
    son N bar (FilterMode.LastN).

    Donus: (netData, meta) - netData C# tarafindaki List<StockData> (pythonnet
    nesnesi, Open/High/Low/Close/Volume/Size/DateTime PascalCase property'leri
    var), meta dict (symbol, period, barCount).
    """
    _ensure_loaded()

    reader = _StockDataReader()
    reader.ReadMetaData(file_path)
    if not reader.IsMetaDataRead:
        raise RuntimeError(f"MetaData okunamadi: {file_path}")

    last_n = int(last_n)
    if last_n > 0:
        reader.ReadDataFast(file_path, _FilterMode.LastN, last_n, 0, None, None)
    else:
        reader.ReadDataFast(file_path, _FilterMode.All, 0, 0, None, None)
    net_data = reader.GetData()

    rawMeta = reader.GetMetaData()
    meta = {
        "symbol": rawMeta["GrafikSembol"] if rawMeta.ContainsKey("GrafikSembol") else "",
        "period": rawMeta["GrafikPeriyot"] if rawMeta.ContainsKey("GrafikPeriyot") else "",
        "barCount": net_data.Count,
    }
    return net_data, meta


def run_signal_script(code, net_data):
    """C# script'i (Data: List<StockData> globaline karsi) calistirir.

    Script TradeSignals[] veya eski int[] (1=AL, -1=SAT, 0=yok)
    dondurmelidir. Dizi uzunlugu net_data.Count olmalidir. Donus: (success, signals_list_or_None, error_message_or_None).
    """
    _ensure_loaded()

    globals_ = _SignalScriptGlobals(net_data)
    executor = _ScriptExecutor()
    result = executor.ExecuteAsync(code, globals_).GetAwaiter().GetResult()

    if not result.Success:
        error = result.Error or ""
        if result.CompilationErrors:
            error = "\n".join(str(e) for e in result.CompilationErrors)
        return False, None, error

    if result.ReturnValue is None:
        return False, None, "Script bir deger dondurmedi (return signals; eksik olabilir)."

    try:
        signals = _to_bridge_signal_codes(result.ReturnValue)
    except TypeError:
        return False, None, "Script'in dondurdugu deger bir dizi degil (int[] donmeli)."

    if len(signals) != net_data.Count:
        return False, None, (
            f"Sinyal dizisi uzunlugu veriyle uyusmuyor: {len(signals)} != {net_data.Count}")

    return True, signals, None


_BULK_EXTRACT_SCRIPT = """
int n = Data.Count;
var opens = new double[n];
var highs = new double[n];
var lows = new double[n];
var closes = new double[n];
var volumes = new double[n];
var sizes = new long[n];
var timestamps = new string[n];
for (int i = 0; i < n; i++)
{
    var d = Data[i];
    opens[i] = d.Open;
    highs[i] = d.High;
    lows[i] = d.Low;
    closes[i] = d.Close;
    volumes[i] = d.Volume;
    sizes[i] = d.Size;
    timestamps[i] = d.DateTime.ToString("o");
}
return new object[] { opens, highs, lows, closes, volumes, sizes, timestamps };
"""


def to_ohlc_lists(net_data):
    """net_data (C# List<StockData>) -> Python list'lerine (open/high/low/close/
    volume/size/timestamps) cevirir - Panel.setCandleData'ya dogrudan verilebilir.

    Her bar icin tek tek StockData property'si okumak (eski yontem) her bar X 7
    alan icin pythonnet sinirini asiyordu (565k bar icin ~4M gecis) - bunun
    yerine donusum TOPLU olarak C# icinde (Roslyn ile, SignalScriptGlobals.Data
    uzerinden) yapilir, Python'a sadece 7 primitive dizi (double[]/long[]/
    string[]) gecer - cok daha az sinir gecisi."""
    _ensure_loaded()

    globals_ = _SignalScriptGlobals(net_data)
    executor = _ScriptExecutor()
    result = executor.ExecuteAsync(_BULK_EXTRACT_SCRIPT, globals_).GetAwaiter().GetResult()
    if not result.Success:
        error = result.Error or ""
        if result.CompilationErrors:
            error = "\n".join(str(e) for e in result.CompilationErrors)
        raise RuntimeError(f"Bulk OHLC cikarma scripti basarisiz: {error}")

    opensNet, highsNet, lowsNet, closesNet, volumesNet, sizesNet, timestampsNet = result.ReturnValue

    opens = list(opensNet)
    highs = list(highsNet)
    lows = list(lowsNet)
    closes = list(closesNet)
    volumes = list(volumesNet)
    sizes = [int(s) for s in sizesNet]
    timestamps = [datetime.fromisoformat(str(t)) for t in timestampsNet]
    return opens, highs, lows, closes, volumes, sizes, timestamps


# TradeSignals raw degerleri (bkz. AlgoTrade.Core.Trading.Core.TradeSignals):
# None=0, Buy=1, Sell=2. Bizim sparse sinyal kodumuz (1=AL,-1=SAT,0=yok) buna eslenir.
_SIGNAL_CODE_TO_TRADESIGNAL = {1: 1, -1: 2}

_REPLAY_SIMULATION_SCRIPT = """
// AlgoTrade.ReplayRunner/Program.cs ile AYNI mantik - sinyal dizisini GERCEK
// SingleTrader motoruna (ReplaySignalStrategy uzerinden) besler; subprocess+JSON
// yerine in-process+bellek.
var signals = SignalCodes.Select(c => (TradeSignals)c).ToArray();

var logger = LogManager.GetInstance();
var timer = TimeManager.GetInstance();

string appConfigPath = System.IO.Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");
var appConfig = AppConfigLoader.Load(appConfigPath);

var algoTrader = new AlgoTrader("CSharpBridgeReplay");
algoTrader.RegisterLogger(logger);
algoTrader.RegisterTimer(timer);
algoTrader.Reset();
algoTrader.SetData(Data);
algoTrader.SymbolName = SymbolName;
algoTrader.SymbolPeriod = SymbolPeriod;

AppConfigApplier.ApplySingleTrader(algoTrader, appConfig.SingleTrader, AppSettings.ConfigsDir);
algoTrader.ConfigureStrategy("ReplaySignalStrategy", new Dictionary<string, object> { ["signals"] = signals });
algoTrader.SingleTraderRunMode = TraderRunMode.TradeOnly;

algoTrader.Initialize();
await algoTrader.RunSingleTraderWithProgressAsync();

return algoTrader;
"""


def run_replay_simulation(net_data, signals, symbol_name="", symbol_period=""):
    """signals (Python int list, 1=AL/-1=SAT/0=yok) dizisini GERCEK SingleTrader
    motoruna (ReplaySignalStrategy) besler - AppConfig.json'daki SingleTrader
    ayarlariyla (bakiye/komisyon/vb.) AYNI ekonomi. Donus: (success, algoTrader,
    error) - algoTrader basarili ise .SingleTrader uzerinden istatistiklere
    erisilebilir, write_replay_to_disk'e verilebilir."""
    _ensure_loaded()

    # .get(s, s): 1/-1 (basitlestirilmis AL/SAT) Buy(1)/Sell(2)'ye cevrilir;
    # digerleri (0=None, 3=TakeProfit, 4=StopLoss, 5=Flat, 6=Skip) TradeSignals
    # raw degerleriyle ZATEN AYNI oldugu icin OLDUGU GIBI gecer (filter.csx
    # bunlari dogrudan uretebilir - bkz. filter.csx Kar Al/Zarar Kes/Flat bolumleri).
    raw_codes = [_SIGNAL_CODE_TO_TRADESIGNAL.get(s, s) for s in signals]
    net_signal_codes = _DotNetSystem.Array[int](raw_codes)

    globals_ = _ReplaySimulationGlobals(net_data, net_signal_codes, symbol_name or "", symbol_period or "")
    executor = _ScriptExecutor()
    result = executor.ExecuteAsync(_REPLAY_SIMULATION_SCRIPT, globals_).GetAwaiter().GetResult()

    if not result.Success:
        error = result.Error or ""
        if result.CompilationErrors:
            error = "\n".join(str(e) for e in result.CompilationErrors)
        return False, None, error

    return True, result.ReturnValue, None


def get_replay_summary(algo_trader):
    """run_replay_simulation'dan donen algoTrader'dan kisa bir ozet dict cikarir."""
    status = algo_trader.SingleTrader.status
    return {
        "tradeCount": int(status.IslemSayisi),
        "winCount": int(status.KazandiranIslemSayisi),
        "lossCount": int(status.KaybettirenIslemSayisi),
        "pnl": float(status.KarZararFiyat),
        "startBalance": float(status.IlkBakiyeFiyat),
        "endBalance": float(status.BakiyeFiyat),
    }


# SingleTrader'daki dosya-adi property'leri (bkz. SingleTrader.cs:275-290) -
# WriteTraderDataToFilesAsync'in AppSettings.LogsDir altina yazdigi dosyalar bunlardir.
_STATS_FILENAME_PROPS = [
    "FullStatsTxtFileName", "FullStatsCsvFileName",
    "MinimalStatsTxtFileName", "MinimalStatsCsvFileName",
    "FullListsTxtFileName", "FullListsCsvFileName",
    "MinimalListsTxtFileName", "MinimalListsCsvFileName",
    "FullStatsTxtFormattedFileName", "MinimalStatsTxtFormattedFileName",
    "GridStatsTxtFileName", "MinimalGridStatsTxtFileName",
    "PerformansTxtFileName", "PerformansCsvFileName",
]


def write_replay_to_disk(algo_trader, progress_callback=None):
    """run_replay_simulation'dan donen algoTrader icin istatistik/liste/performans
    dosyalarini diske yazar - production akisindaki (menu [5]/[6]/[8]) AYNI adim
    (AlgoTrader.WriteTraderDataToFilesAsync), AYNI klasore (AppSettings.LogsDir,
    orijinal AlgoTrade repo'nun outputs/logs'u).

    WriteTraderDataToFilesAsync TEK bir C# cagrisi oldugu icin (dosya-bazli ara
    callback yok) yazim sirasinda GERCEK canli ilerleme veremiyoruz; bunun yerine
    progress_callback(event, payload, total) ile "start" (beklenen dosya sayisi)
    ve ardindan her yazilmis dosya icin "file" (ad, yol, boyut) bildirilir -
    kullaniciya "diske ne yazildi" listesini verir.

    Donus: [(propName, fullPath, sizeBytes), ...] - gercekten diskte bulunan dosyalar."""
    _ensure_loaded()
    from AlgoTrade.Core import AppSettings
    logs_dir = str(AppSettings.LogsDir)
    single_trader = algo_trader.SingleTrader

    expected = []
    for prop in _STATS_FILENAME_PROPS:
        name = getattr(single_trader, prop, None)
        if name:
            expected.append((prop, os.path.join(logs_dir, str(name))))

    if progress_callback:
        progress_callback("start", None, len(expected))

    algo_trader.WriteTraderDataToFilesAsync(single_trader).GetAwaiter().GetResult()

    written = []
    for prop, path in expected:
        if os.path.isfile(path):
            size = os.path.getsize(path)
            written.append((prop, path, size))
            if progress_callback:
                progress_callback("file", (prop, path, size), len(expected))

    if progress_callback:
        progress_callback("done", written, len(expected))
    return written


def get_logs_dir():
    """AppSettings.LogsDir'i Python string olarak dondurur (status mesaji icin)."""
    _ensure_loaded()
    from AlgoTrade.Core import AppSettings
    return str(AppSettings.LogsDir)


_EXTRACT_TRADER_SERIES_SCRIPT = """
// Calculate asamasindan donen AlgoTrader'in SingleTrader.lists'inden
// (PnL/Return/Balance/Commission/SignalStep) TOPLU array cikarir - Plot
// asamasinin zengin panel seti (PnL/Return panelleri) icin, tek tek eleman
// okumanin pythonnet sinirini asmasini onlemek icin (bkz. to_ohlc_lists).
var lists = Trader.SingleTrader.lists;
int n = Trader.SingleTrader.Data.Count;

double[] ToArr(List<double> l)
{
    var arr = new double[n];
    for (int i = 0; i < n && i < l.Count; i++) arr[i] = l[i];
    return arr;
}

return new object[] {
    ToArr(lists.KarZararFiyatList), ToArr(lists.KarZararFiyatYuzdeList),
    ToArr(lists.GetiriFiyatList), ToArr(lists.GetiriFiyatNetList),
    ToArr(lists.GetiriFiyatYuzdeList), ToArr(lists.GetiriFiyatYuzdeNetList),
    ToArr(lists.BakiyeFiyatList), ToArr(lists.BakiyeFiyatNetList),
    ToArr(lists.KomisyonFiyatList), ToArr(lists.SinyalList)
};
"""


def extract_trader_series(algo_trader):
    """Calculate asamasindan donen algoTrader'in SingleTrader.lists'inden
    (PnL/PnL%/Return/NetReturn/Return%/NetReturn%/Balance/NetBalance/
    Commission/SignalStep) bulk array cikarir - Plot script'inin zengin panel
    seti (default.py'nin ciktigi gercek panel setiyle AYNI seriler) icin.
    Donus: dict (her deger Python float list'i, Data.Count uzunlugunda)."""
    _ensure_loaded()
    globals_ = _WriteToDiskScriptGlobals(algo_trader)
    executor = _ScriptExecutor()
    result = executor.ExecuteAsync(_EXTRACT_TRADER_SERIES_SCRIPT, globals_).GetAwaiter().GetResult()
    if not result.Success:
        error = result.Error or ""
        if result.CompilationErrors:
            error = "\n".join(str(e) for e in result.CompilationErrors)
        raise RuntimeError(f"extract_trader_series basarisiz: {error}")

    pnl, pnlPct, ret, retNet, retPct, retPctNet, balance, balanceNet, commission, signalStep = result.ReturnValue
    return {
        "pnl": list(pnl), "pnlPct": list(pnlPct),
        "returnFiyat": list(ret), "returnNet": list(retNet),
        "returnPct": list(retPct), "returnPctNet": list(retPctNet),
        "balance": list(balance), "balanceNet": list(balanceNet),
        "commission": list(commission), "signalStep": list(signalStep),
    }
