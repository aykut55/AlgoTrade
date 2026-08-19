// =============================================================================
// 16_RunMultiQueryTimeframeScan.csx - MultiQueryTimeframeScan (Sorgu Tarama) Standalone Execution
// Console menu [18] Sorgu Tarama > Multi-Query Timeframe Scan ile ayni islevi gorur.
// handleMultiQueryTimeframeScan() sadece interaktif config-ozet dongusu, onu PORT
// ETMIYORUZ - burada sadece Program.cs'teki runMultiQueryTimeframeScan() govdesi
// var, tek seferlik, straight-line calisir. Konfigurasyon AppConfig.json'daki
// "MultiQueryTimeframeScan" bolumunden okunur (Queries listesi -
// AppConfigApplier.BuildMultiQueryTimeframeScanOptions her QueryRef'i sirayla
// 0,1,2... QueryId ile bir QueryEntry'ye ceviriyor). WriteSortedResults/GetBestResult
// yok (MultiQueryTimeframeScanner'da bu metodlar tanimli degil, sadece Results
// listesi var).
// =============================================================================
using System.IO;
using AlgoTrade.Core.AppConfig;

try
{
    // =============================================================================
    // 1. AppConfig.json Yukle
    // =============================================================================
    Log("=== 16_RunMultiQueryTimeframeScan.csx ===");

    string appConfigPath = Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");
    AppConfigLoader.CreateSampleIfNotExists(appConfigPath);
    var appConfig = AppConfigLoader.Load(appConfigPath);
    Log($"[AppConfig] Yuklendi: {appConfigPath}");

    // =============================================================================
    // 2. MultiQueryTimeframeScannerOptions Olustur
    // =============================================================================
    Log("");
    Log("Running MultiQueryTimeframeScan (Tarama)");

    var cfg     = appConfig.MultiQueryTimeframeScan;
    var options = AppConfigApplier.BuildMultiQueryTimeframeScanOptions(cfg, AppSettings.ConfigsDir);

    // =============================================================================
    // 3. Scanner Olustur ve Calistir
    // =============================================================================
    // NOT: MultiQueryTimeframeScanner.Run(...) icin IsCancellationRequested
    // baglanmiyor - tarama basladiktan sonra ESC ile yari yolda durdurulamiyor
    // (console'daki [18] ile ayni sinirlama).
    using var scanner = new MultiQueryTimeframeScanner(LogManager.GetInstance());
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
        {
            Log($"  {r.Timeframe,-8}");
            foreach (var q in r.QuerySignals)
                Log($"           Query{q.QueryId} ({q.QueryName}): {q.SorguOzeti}");
        }
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
