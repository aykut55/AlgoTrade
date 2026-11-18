// Bundle asamasi - globals: Trader (Calculate asamasindan donen AlgoTrader).
using AlgoTrade.Core.Python.DearPyGuiDataPlotter;

var converter = new TradeDataBundleConverter();
var (bundlePath, viewPath) = converter.ConvertSingleTrader(
    Trader.SingleTrader, AppSettings.LogsDir, fileBaseName: "SingleTraderBundle");
return new[] { bundlePath, viewPath };
