// Bundle asamasi - globals: Trader (Calculate asamasindan donen AlgoTrader).
using AlgoTrade.Core.Python;
using AlgoTrade.Core.Python.DearPyGuiDataPlotter;

var converter = new TradeDataBundleConverter();

var (bundlePath, viewPath) = StrategyIndicators is null
    ? converter.ConvertSingleTrader(Trader.SingleTrader, AppSettings.LogsDir, "SingleTraderBundle")
    : converter.ConvertSingleTrader(Trader.SingleTrader, AppSettings.LogsDir, "SingleTraderBundle", StrategyIndicators);

string reportsPath = BundleReportPackager.IndexExistingReports(bundlePath, "SingleTrader");
return new[] { bundlePath, viewPath, reportsPath };
