// =============================================================================
// 19_RunMultiQuerySymbolTimeframeScan.csx - MultiQuerySymbolTimeframeScan (Sorgu Tarama) Standalone Execution
// Console menu [21] Sorgu Tarama > Multi-Query Symbol-Timeframe Scan ile ayni
// islevi gorur. handleMultiQuerySymbolTimeframeScan() sadece interaktif
// config-ozet dongusu, onu PORT ETMIYORUZ - burada sadece Program.cs'teki
// runMultiQuerySymbolTimeframeScan() govdesi var, tek seferlik, straight-line
// calisir. Konfigurasyon AppConfig.json'daki "MultiQuerySymbolTimeframeScan"
// bolumunden okunur (Queries listesi - AppConfigApplier.BuildMultiQuerySymbolTimeframeScanOptions
// ile 16/18'deki ile ayni desende her QueryRef'i sirayla 0,1,2... QueryId ile
// bir QueryEntry'ye ceviriyor). WriteSortedResults/GetBestResult yok
// (MultiQuerySymbolTimeframeScanner'da bu metodlar tanimli degil, sadece
// Results listesi var).
// =============================================================================
using AlgoTrade.Core.AppConfig;

try
{
    // =============================================================================
    // 1. AppConfig.json Yukle
    // =============================================================================
    Log("=== 19_RunMultiQuerySymbolTimeframeScan.csx ===");

    string appConfigPath = Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");
    AppConfigLoader.CreateSampleIfNotExists(appConfigPath);
    var appConfig = AppConfigLoader.Load(appConfigPath);
    Log($"[AppConfig] Yuklendi: {appConfigPath}");

    // =============================================================================
    // 2. MultiQuerySymbolTimeframeScannerOptions Olustur
    // =============================================================================
    Log("");
    Log("Running MultiQuerySymbolTimeframeScan (Tarama)");

    var cfg     = appConfig.MultiQuerySymbolTimeframeScan;
    var options = AppConfigApplier.BuildMultiQuerySymbolTimeframeScanOptions(cfg, AppSettings.ConfigsDir);

    // =============================================================================
    // 3. Scanner Olustur ve Calistir
    // =============================================================================
    // NOT: MultiQuerySymbolTimeframeScanner.Run(...) icin IsCancellationRequested
    // baglanmiyor - tarama basladiktan sonra ESC ile yari yolda durdurulamiyor
    // (console'daki [21] ile ayni sinirlama).
    using var scanner = new MultiQuerySymbolTimeframeScanner(LogManager.GetInstance());
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
        {
            Log($"  {r.Symbol,-20} {r.Timeframe,-8}");
            foreach (var q in r.QuerySignals)
                Log($"           Query{q.QueryId} ({q.QueryName}): {q.SorguOzeti}");
        }
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
