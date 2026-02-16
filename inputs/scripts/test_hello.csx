// Test Script - Basit kontrol
Log("Merhaba! Script calisiyor...");
Log($"StockData count: {stockData?.Count ?? 0}");
Log($"AlgoTrader name: {algoTrader?.Name ?? "N/A"}");

if (stockData != null && stockData.Count > 0)
{
    var first = stockData[0];
    var last = stockData[stockData.Count - 1];
    Log($"Ilk bar : {first.DateTime} O:{first.Open} H:{first.High} L:{first.Low} C:{first.Close}");
    Log($"Son bar : {last.DateTime} O:{last.Open} H:{last.High} L:{last.Low} C:{last.Close}");
}
else
{
    Log("StockData bos - once menu [1] ile data yukleyin.");
}

Log("Script tamamlandi.");
"OK"
