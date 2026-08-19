// =============================================================================
// 14_RunQuerySymbolScan.csx - QuerySymbolScan (Sorgu Tarama) Standalone Execution
// Console menu [16] Sorgu Tarama > Query Symbol Scan ile ayni islevi gorur.
// handleQuerySymbolScan() sadece interaktif config-ozet dongusu, onu PORT ETMIYORUZ -
// burada sadece Program.cs'teki runQuerySymbolScan() govdesi var, tek seferlik,
// straight-line calisir. Konfigurasyon AppConfig.json'daki "QuerySymbolScan"
// bolumunden okunur. Sorgu tarayicilari Strategy/TradeParams degil sadece Query
// kullanir - WriteSortedResults/GetBestResult yok (QuerySymbolScanner'da bu
// metodlar tanimli degil, sadece Results listesi var).
// =============================================================================
using System.IO;
using AlgoTrade.Core.AppConfig;

try
{
    // =============================================================================
    // 1. AppConfig.json Yukle
    // =============================================================================
    Log("=== 14_RunQuerySymbolScan.csx ===");

    string appConfigPath = Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");
    AppConfigLoader.CreateSampleIfNotExists(appConfigPath);
    var appConfig = AppConfigLoader.Load(appConfigPath);
    Log($"[AppConfig] Yuklendi: {appConfigPath}");

    // =============================================================================
    // 2. QuerySymbolScanOptions Olustur
    // =============================================================================
    Log("");
    Log("Running QuerySymbolScan (Tarama)");

    var cfg     = appConfig.QuerySymbolScan;
    var options = AppConfigApplier.BuildQuerySymbolScanOptions(cfg, AppSettings.ConfigsDir);

    // =============================================================================
    // 3. Scanner Olustur ve Calistir
    // =============================================================================
    // NOT: QuerySymbolScanner.Run(...) icin IsCancellationRequested baglanmiyor -
    // tarama basladiktan sonra ESC ile yari yolda durdurulamiyor (console'daki [16]
    // ile ayni sinirlama).
    using var scanner = new QuerySymbolScanner(LogManager.GetInstance());
    scanner.OnProgress = (current, total, symbol) =>
    {
        LogManager.GetConsoleLogger().Write($"\r\t[{current}/{total}] {symbol}".PadRight(60));
    };

    string csvPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.CsvFileName);
    string txtPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.TxtFileName);

    await Task.Run(() => scanner.Run(options, csvPath, txtPath));
    Log("");

    // =============================================================================
    // 4. Sonuclari Ozetle
    // =============================================================================
    int successCount = scanner.Results.Count(r => r.Success);
    int failCount    = scanner.Results.Count - successCount;

    Log("");
    Log($"=== Tarama tamamlandı: {scanner.Results.Count} sembol ({successCount} başarılı, {failCount} hata) ===");

    foreach (var r in scanner.Results)
    {
        if (r.Success)
            Log($"  {r.Symbol,-20} {r.SorguOzeti}");
        else
            Log($"  {r.Symbol,-20} HATA: {r.ErrorMessage}");
    }

    Log("");
    Log($"Sonuçlar : {csvPath}");
}
catch (Exception ex)
{
    Log($"[HATA] {ex}");
}

Log("=== Bitti ===");
