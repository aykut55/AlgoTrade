// =============================================================================
// Test 1: Basit kontrol (Interactive - data yuklu olmali)
// =============================================================================
// Log($"Data: {stockData.Count} bar");
// Log($"Trader: {Trader?.Name ?? "yok"}");
// Log($"AlgoTrader: {algoTrader.Name}");

// =============================================================================
// Test 2: Tam AlgoTrader calistirma (Interactive - data yuklu olmali)
// =============================================================================
// algoTrader.SetData(stockData);
// algoTrader.Initialize();
// algoTrader.ConfigureStrategy("SimpleMostStrategy", new Dictionary<string, object> { ["period"] = 21, ["percent"] = 1.0, ["signalModeIndex"] = 0 });
// algoTrader.SingleTraderRunMode = TraderRunMode.TradeOnly;
// await algoTrader.RunSingleTraderWithProgressAsync();
// Log($"Trader: {Trader?.Name ?? "yok"}");
// Log($"Sonuc: {algoTrader.SingleTrader?.TaramaOzeti ?? "N/A"}");
