// =============================================================================
// 18_RunMultiQuerySymbolScan.csx - MultiQuerySymbolScan (Sorgu Tarama) Standalone Execution
// Console menu [20] Sorgu Tarama > Multi-Query Symbol Scan ile ayni islevi gorur.
// handleMultiQuerySymbolScan() sadece interaktif config-ozet dongusu, onu PORT
// ETMIYORUZ - burada sadece Program.cs'teki runMultiQuerySymbolScan() govdesi
// var, tek seferlik, straight-line calisir. Konfigurasyon AppConfig.json'daki
// "MultiQuerySymbolScan" bolumunden okunur (Queries listesi -
// AppConfigApplier.BuildMultiQuerySymbolScanOptions ile 16'daki
// BuildMultiQueryTimeframeScanOptions ile ayni desende her QueryRef'i sirayla
// 0,1,2... QueryId ile bir QueryEntry'ye ceviriyor). WriteSortedResults/GetBestResult
// yok (MultiQuerySymbolScanner'da bu metodlar tanimli degil, sadece Results
// listesi var).
// =============================================================================
using AlgoTrade.Core.AppConfig;

try
{
    // =============================================================================
    // 1. AppConfig.json Yukle
    // =============================================================================
    Log("=== 18_RunMultiQuerySymbolScan.csx ===");

    string appConfigPath = Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");
    AppConfigLoader.CreateSampleIfNotExists(appConfigPath);
    var appConfig = AppConfigLoader.Load(appConfigPath);
    Log($"[AppConfig] Yuklendi: {appConfigPath}");

    // =============================================================================
    // 2. MultiQuerySymbolScannerOptions Olustur
    // =============================================================================
    Log("");
    Log("Running MultiQuerySymbolScan (Tarama)");

    var cfg     = appConfig.MultiQuerySymbolScan;
    var options = AppConfigApplier.BuildMultiQuerySymbolScanOptions(cfg, AppSettings.ConfigsDir);

    // =============================================================================
    // 3. Scanner Olustur ve Calistir
    // =============================================================================
    // NOT: MultiQuerySymbolScanner.Run(...) icin IsCancellationRequested
    // baglanmiyor - tarama basladiktan sonra ESC ile yari yolda durdurulamiyor
    // (console'daki [20] ile ayni sinirlama).
    using var scanner = new MultiQuerySymbolScanner(LogManager.GetInstance());
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
        {
            Log($"  {r.Symbol,-20}");
            foreach (var q in r.QuerySignals)
                Log($"           Query{q.QueryId} ({q.QueryName}): {q.SorguOzeti}");
        }
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
