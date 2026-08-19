// =============================================================================
// 08_RunSymbolScan.csx - SymbolScan (Tarama) Standalone Execution
// Console menu [10] Tarama > Symbol Scan ile ayni islevi gorur.
// handleSymbolScan() sadece interaktif config-ozet dongusu (E/R/ENTER/B), onu
// PORT ETMIYORUZ - burada sadece Program.cs'teki runSymbolScan() govdesi var,
// tek seferlik, straight-line calisir. Konfigurasyon tamamen AppConfig.json'daki
// "SymbolScan" bolumunden okunur.
//
// AppConfig/AppConfigApplier namespace'i script'e otomatik import edilmiyor,
// bu yuzden asagida elle "using AlgoTrade.Core.AppConfig;" var.
// =============================================================================
using AlgoTrade.Core.AppConfig;

try
{
    // =============================================================================
    // 1. AppConfig.json Yukle
    // =============================================================================
    Log("=== 08_RunSymbolScan.csx ===");

    string appConfigPath = Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");
    AppConfigLoader.CreateSampleIfNotExists(appConfigPath);
    var appConfig = AppConfigLoader.Load(appConfigPath);
    Log($"[AppConfig] Yuklendi: {appConfigPath}");

    // =============================================================================
    // 2. SymbolScanOptions Olustur
    // =============================================================================
    Log("");
    Log("Running SymbolScan (Tarama)");

    var cfg     = appConfig.SymbolScan;
    var options = AppConfigApplier.BuildSymbolScanOptions(cfg, AppSettings.ConfigsDir);

    // =============================================================================
    // 3. Scanner Olustur ve Calistir
    // =============================================================================
    // NOT: SymbolScanner.Run(...) sadece basit bir CancellationToken parametresi kabul
    // ediyor, IsCancellationRequested (script ESC iptali) buraya elle baglanmiyor -
    // tarama basladiktan sonra ESC ile yari yolda durdurulamiyor (console'daki [10]
    // ile ayni sinirlama).
    using var scanner = new SymbolScanner(LogManager.GetInstance());
    scanner.OnProgress = (current, total, symbol) =>
    {
        LogManager.GetConsoleLogger().Write($"\r\t[{current}/{total}] {symbol}".PadRight(60));
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
    Log($"=== Tarama tamamlandı: {scanner.Results.Count} sembol ({successCount} başarılı, {failCount} hata) ===");

    foreach (var r in scanner.Results)
    {
        if (r.Success)
            Log($"  {r.Symbol,-20} {r.TaramaOzeti}");
        else
            Log($"  {r.Symbol,-20} HATA: {r.ErrorMessage}");
    }

    var best = scanner.GetBestResult(options);
    if (best != null)
    {
        Log("");
        Log($"En iyi ({cfg.Sort.SortField}): {best.Symbol}  ->  {best.TaramaOzeti}");
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
