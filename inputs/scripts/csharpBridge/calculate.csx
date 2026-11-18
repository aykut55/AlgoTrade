// Hesapla asamasi - globals: Data (List<StockData>), SignalCodes (int[], TradeSignals
// raw degerleri: 0=None,1=Buy,2=Sell), SymbolName, SymbolPeriod (string)
// return: AlgoTrader (SingleTrader'i .SingleTrader uzerinden tasir)
//
// AlgoTrade.ReplayRunner/Program.cs ile AYNI mantik - sinyal dizisini GERCEK
// SingleTrader motoruna (ReplaySignalStrategy uzerinden) besler.
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

if (TimingWindow is { } timingWindow)
{
    var timing = appConfig.SingleTrader.Signals;
    timing.TimeFilteringEnabled = true;
    timing.TimeFilterMode = timingWindow.Mode;
    const string format = "yyyy.MM.dd HH:mm:ss";
    timing.StartDateTime = timingWindow.Start.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
    timing.StopDateTime = timingWindow.EndExclusive.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
}

AppConfigApplier.ApplySingleTrader(algoTrader, appConfig.SingleTrader, AppSettings.ConfigsDir);

var strategyParams = new Dictionary<string, object> { ["signals"] = signals };
if (ExitRule != null)
    strategyParams["exitRule"] = ExitRule;
algoTrader.ConfigureStrategy("ReplaySignalStrategy", strategyParams);

algoTrader.SingleTraderRunMode = TraderRunMode.TradeOnly;

algoTrader.Initialize();

await algoTrader.RunSingleTraderWithProgressAsync();

return algoTrader;
