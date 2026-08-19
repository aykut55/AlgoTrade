// =============================================================================
// 12_RunMultiStrategySymbolScan.csx - MultiStrategySymbolScan (Tarama) Standalone Execution
// Console menu [14] Tarama > Multi-Strategy Symbol Scan ile ayni islevi gorur.
// handleMultiStrategySymbolScan() sadece interaktif config-ozet dongusu, onu PORT
// ETMIYORUZ - burada sadece Program.cs'teki runMultiStrategySymbolScan() govdesi
// var, tek seferlik, straight-line calisir. Bu scanner AppConfigApplier'in
// Build*ScanOptions helper'ini KULLANMIYOR (10_RunMultiStrategyTimeframeScan.csx
// ile ayni desende) - MultiStrategySymbolScannerOptions elle olusturuluyor ve
// ConfigureAlgoTrader callback'i icinde her sembol icin tam bir MultipleTrader
// (N child + consensus) kuruluyor, tipki console'daki gibi.
// =============================================================================
using AlgoTrade.Core.AppConfig;

try
{
    // =============================================================================
    // 1. AppConfig.json Yukle
    // =============================================================================
    Log("=== 12_RunMultiStrategySymbolScan.csx ===");

    string appConfigPath = Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");
    AppConfigLoader.CreateSampleIfNotExists(appConfigPath);
    var appConfig = AppConfigLoader.Load(appConfigPath);
    Log($"[AppConfig] Yuklendi: {appConfigPath}");

    // =============================================================================
    // 2. MultiStrategySymbolScannerOptions Olustur (elle, ConfigureAlgoTrader ile)
    // =============================================================================
    Log("");
    Log("Running MultiStrategySymbolScan (Tarama)");

    var cfg = appConfig.MultiStrategySymbolScan;

    var options = new MultiStrategySymbolScannerOptions
    {
        DataFolder     = cfg.DataFolder,
        AutoDiscover   = cfg.AutoDiscover,
        SymbolList     = cfg.SymbolList,
        SortField      = cfg.Sort.SortField,
        SortDescending = cfg.Sort.SortDescending,
        ConfigureAlgoTrader = at =>
        {
            AppConfigApplier.ApplyMultipleTrader(at, cfg.MultipleTrader, AppSettings.ConfigsDir);
            // Her sembol icin ayni dosya adlarina yazildigi icin (collision) ve zaten kendi
            // ozet CSV/TXT'imiz asil ciktimiz oldugu icin MultipleTrader'in kendi dosya
            // yazimlari kapatiliyor.
            at.SetMultipleTraderSaveConfig(new MultipleTraderObjectSaveConfig
            {
                SaveStatisticsToFile              = false,
                SaveMultipleTraderListsTxtEnabled = false,
                SaveMultipleTraderListsCsvEnabled = false,
                WriteChildTradersDataToFiles      = false,
            });
            // AlgoTrader.ProgressLoggingEnabled varsayilan false, ama burada bilincli true
            // yapiliyor - [12]'deki ile ayni sebep (bkz. 10_RunMultiStrategyTimeframeScan.csx):
            // her sembol icin MultipleTrader gecikmeli olabiliyor, hicbir geri bildirim olmadan
            // ekran donmus gibi gorunuyordu. ConsoleLogger.Write uzerinden yaziyor (sink
            // sisteminden bagimsiz), consensus debug spam'i geri gelmiyor.
            at.ProgressLoggingEnabled = true;
        },
    };

    if (Enum.TryParse<StockDataReader.FilterMode>(cfg.ReadData.FilterMode, ignoreCase: true, out var filterMode))
        options.ReadFilterMode = filterMode;
    options.N1 = cfg.ReadData.N1;
    options.N2 = cfg.ReadData.N2;
    if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt1)) options.Dt1 = DateTime.Parse(cfg.ReadData.Dt1);
    if (!string.IsNullOrWhiteSpace(cfg.ReadData.Dt2)) options.Dt2 = DateTime.Parse(cfg.ReadData.Dt2);

    // =============================================================================
    // 3. Scanner Olustur ve Calistir
    // =============================================================================
    // NOT: MultiStrategySymbolScanner.RunAsync(...) icin IsCancellationRequested
    // baglanmiyor - tarama basladiktan sonra ESC ile yari yolda durdurulamiyor
    // (console'daki [14] ile ayni sinirlama).
    using var scanner = new MultiStrategySymbolScanner(LogManager.GetInstance());
    scanner.OnProgress = (current, total, symbol) =>
    {
        LogManager.GetConsoleLogger().Write($"\r\t[{current}/{total}] {symbol}".PadRight(60));
    };

    string csvPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.CsvFileName);
    string txtPath       = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.TxtFileName);
    string sortedCsvPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedCsvFileName);
    string sortedTxtPath = Path.Combine(AppSettings.ScanLogsDir, cfg.Save.SortedTxtFileName);

    // [12]'deki (MultiStrategyTimeframeScan) ayni sebep: MultipleTrader.BuildConsensusSignal()
    // her bar'da bir Debug log basiyor, N sembol art arda calistigi icin konsolu dolduruyor.
    // Console sink'i sadece RunAsync suresince kapatip hemen sonra geri aciyoruz.
    LogManager.DisableConsoleSink();
    try
    {
        await scanner.RunAsync(options, csvPath, txtPath);
    }
    finally
    {
        LogManager.EnableConsoleSink();
    }
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
        {
            Log($"  {r.Symbol,-20} Bileşke: {r.TaramaOzeti}");
            foreach (var child in r.ChildSignals)
                Log($"           Child{child.ChildId} (bağımsız): {child.TaramaOzeti}");
        }
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
