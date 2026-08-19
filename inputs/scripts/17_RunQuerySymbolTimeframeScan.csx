// =============================================================================
// 17_RunQuerySymbolTimeframeScan.csx - QuerySymbolTimeframeScan (Sorgu Tarama) Standalone Execution
// Console menu [19] Sorgu Tarama > Query Symbol-Timeframe Scan ile ayni islevi
// gorur. handleQuerySymbolTimeframeScan() sadece interaktif config-ozet dongusu,
// onu PORT ETMIYORUZ - burada sadece Program.cs'teki
// runQuerySymbolTimeframeScan() govdesi var, tek seferlik, straight-line calisir.
// Konfigurasyon AppConfig.json'daki "QuerySymbolTimeframeScan" bolumunden
// okunur. WriteSortedResults/GetBestResult yok (QuerySymbolTimeframeScanner'da bu
// metodlar tanimli degil, sadece Results listesi var).
// =============================================================================
using AlgoTrade.Core.AppConfig;

try
{
    // =============================================================================
    // 1. AppConfig.json Yukle
    // =============================================================================
    Log("=== 17_RunQuerySymbolTimeframeScan.csx ===");

    string appConfigPath = Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");
    AppConfigLoader.CreateSampleIfNotExists(appConfigPath);
    var appConfig = AppConfigLoader.Load(appConfigPath);
    Log($"[AppConfig] Yuklendi: {appConfigPath}");

    // =============================================================================
    // 2. QuerySymbolTimeframeScannerOptions Olustur
    // =============================================================================
    Log("");
    Log("Running QuerySymbolTimeframeScan (Tarama)");

    var cfg     = appConfig.QuerySymbolTimeframeScan;
    var options = AppConfigApplier.BuildQuerySymbolTimeframeScanOptions(cfg, AppSettings.ConfigsDir);

    // =============================================================================
    // 3. Scanner Olustur ve Calistir
    // =============================================================================
    // NOT: QuerySymbolTimeframeScanner.Run(...) icin IsCancellationRequested
    // baglanmiyor - tarama basladiktan sonra ESC ile yari yolda durdurulamiyor
    // (console'daki [19] ile ayni sinirlama).
    using var scanner = new QuerySymbolTimeframeScanner(LogManager.GetInstance());
    scanner.OnProgress = (current, total, cell) =>
    {
        LogManager.GetConsoleLogger().Write($"\r\t[{current}/{total}] {cell}".PadRight(60));
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
    Log($"=== Tarama tamamlandı: {scanner.Results.Count} hücre ({successCount} başarılı, {failCount} hata) ===");

    foreach (var r in scanner.Results)
    {
        if (r.Success)
            Log($"  {r.Symbol,-20} {r.Timeframe,-8} {r.SorguOzeti}");
        else
            Log($"  {r.Symbol,-20} {r.Timeframe,-8} HATA: {r.ErrorMessage}");
    }

    Log("");
    Log($"Sonuçlar : {csvPath}");
}
catch (Exception ex)
{
    Log($"[HATA] {ex}");
}

Log("=== Bitti ===");
