// =============================================================================
// 15_RunQueryTimeframeScan.csx - QueryTimeframeScan (Sorgu Tarama) Standalone Execution
// Console menu [17] Sorgu Tarama > Query Timeframe Scan ile ayni islevi gorur.
// handleQueryTimeframeScan() sadece interaktif config-ozet dongusu, onu PORT
// ETMIYORUZ - burada sadece Program.cs'teki runQueryTimeframeScan() govdesi var,
// tek seferlik, straight-line calisir. Konfigurasyon AppConfig.json'daki
// "QueryTimeframeScan" bolumunden okunur. WriteSortedResults/GetBestResult yok
// (QueryTimeframeScanner'da bu metodlar tanimli degil, sadece Results listesi var).
// =============================================================================
using System.IO;
using AlgoTrade.Core.AppConfig;

try
{
    // =============================================================================
    // 1. AppConfig.json Yukle
    // =============================================================================
    Log("=== 15_RunQueryTimeframeScan.csx ===");

    string appConfigPath = Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");
    AppConfigLoader.CreateSampleIfNotExists(appConfigPath);
    var appConfig = AppConfigLoader.Load(appConfigPath);
    Log($"[AppConfig] Yuklendi: {appConfigPath}");

    // =============================================================================
    // 2. QueryTimeframeScannerOptions Olustur
    // =============================================================================
    Log("");
    Log("Running QueryTimeframeScan (Tarama)");

    var cfg     = appConfig.QueryTimeframeScan;
    var options = AppConfigApplier.BuildQueryTimeframeScanOptions(cfg, AppSettings.ConfigsDir);

    // =============================================================================
    // 3. Scanner Olustur ve Calistir
    // =============================================================================
    // NOT: QueryTimeframeScanner.Run(...) icin IsCancellationRequested baglanmiyor -
    // tarama basladiktan sonra ESC ile yari yolda durdurulamiyor (console'daki [17]
    // ile ayni sinirlama).
    using var scanner = new QueryTimeframeScanner(LogManager.GetInstance());
    scanner.OnProgress = (current, total, tf) =>
    {
        LogManager.GetConsoleLogger().Write($"\r\t[{current}/{total}] {tf}".PadRight(60));
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
    Log($"=== Tarama tamamlandı: {scanner.Results.Count} zaman dilimi ({successCount} başarılı, {failCount} hata) ===");

    foreach (var r in scanner.Results)
    {
        if (r.Success)
            Log($"  {r.Timeframe,-8} {r.SorguOzeti}");
        else
            Log($"  {r.Timeframe,-8} HATA: {r.ErrorMessage}");
    }

    Log("");
    Log($"Sonuçlar : {csvPath}");
}
catch (Exception ex)
{
    Log($"[HATA] {ex}");
}

Log("=== Bitti ===");
