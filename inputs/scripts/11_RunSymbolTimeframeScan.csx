// =============================================================================
// 11_RunSymbolTimeframeScan.csx - SymbolTimeframeScan (Tarama) Standalone Execution
// Console menu [13] Tarama > Symbol-Timeframe Scan ile ayni islevi gorur.
// handleSymbolTimeframeScan() sadece interaktif config-ozet dongusu, onu PORT
// ETMIYORUZ - burada sadece Program.cs'teki runSymbolTimeframeScan() govdesi var,
// tek seferlik, straight-line calisir. Konfigurasyon AppConfig.json'daki
// "SymbolTimeframeScan" bolumunden okunur.
// =============================================================================
using System.IO;
using AlgoTrade.Core.AppConfig;

try
{
    // =============================================================================
    // 1. AppConfig.json Yukle
    // =============================================================================
    Log("=== 11_RunSymbolTimeframeScan.csx ===");

    string appConfigPath = Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");
    AppConfigLoader.CreateSampleIfNotExists(appConfigPath);
    var appConfig = AppConfigLoader.Load(appConfigPath);
    Log($"[AppConfig] Yuklendi: {appConfigPath}");

    // =============================================================================
    // 2. SymbolTimeframeScanOptions Olustur
    // =============================================================================
    Log("");
    Log("Running SymbolTimeframeScan (Tarama)");

    var cfg     = appConfig.SymbolTimeframeScan;
    var options = AppConfigApplier.BuildSymbolTimeframeScanOptions(cfg, AppSettings.ConfigsDir);

    // =============================================================================
    // 3. Scanner Olustur ve Calistir
    // =============================================================================
    // NOT: SymbolTimeframeScanner.Run(...) icin IsCancellationRequested baglanmiyor -
    // tarama basladiktan sonra ESC ile yari yolda durdurulamiyor (console'daki [13]
    // ile ayni sinirlama).
    using var scanner = new SymbolTimeframeScanner(LogManager.GetInstance());
    scanner.OnProgress = (current, total, cell) =>
    {
        LogManager.GetConsoleLogger().Write($"\r\t[{current}/{total}] {cell}".PadRight(60));
    };

    string csvPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.CsvFileName);
    string txtPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.TxtFileName);
    string sortedCsvPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedCsvFileName);
    string sortedTxtPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedTxtFileName);

    await Task.Run(() => scanner.Run(options, csvPath, txtPath));
    Log("");

    // =============================================================================
    // 4. Sirali Sonuclari Yaz ve Ozetle
    // =============================================================================
    scanner.WriteSortedResults(options, sortedCsvPath, sortedTxtPath);

    int successCount = scanner.Results.Count(r => r.Success);
    int failCount    = scanner.Results.Count - successCount;

    Log("");
    Log($"=== Tarama tamamlandı: {scanner.Results.Count} hücre ({successCount} başarılı, {failCount} hata) ===");

    foreach (var r in scanner.Results)
    {
        if (r.Success)
            Log($"  {r.Symbol,-20} {r.Timeframe,-8} {r.TaramaOzeti}");
        else
            Log($"  {r.Symbol,-20} {r.Timeframe,-8} HATA: {r.ErrorMessage}");
    }

    var best = scanner.GetBestResult(options);
    if (best != null)
    {
        Log("");
        Log($"En iyi ({cfg.Sort.SortField}): {best.Symbol}/{best.Timeframe}  ->  {best.TaramaOzeti}");
    }

    Log("");
    Log($"Sonuçlar     : {csvPath}");
    Log($"Sıralı sonuç : {sortedCsvPath}");
}
catch (Exception ex)
{
    Log($"[HATA] {ex}");
}

Log("=== Bitti ===");
