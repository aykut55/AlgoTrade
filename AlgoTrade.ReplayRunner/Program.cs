using AlgoTrade.Core;
using AlgoTrade.Core.AppConfig;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Python;
using AlgoTrade.Core.Python.DearPyGuiDataPlotter;
using AlgoTrade.Core.StockDataReader;
using AlgoTrade.Core.Timer;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using System.Globalization;
using System.Text;
using System.Text.Json;

// ReplaySignalRunner: Python plotter'dan subprocess ile cagrilir.
//
//   AlgoTrade.ReplayRunner.exe <inputJsonPath> <outputJsonPath>
//
// Amac: Python'da filtrelenmis/duzenlenmis (veya bastan uretilmis) bir AL/SAT/FLAT
// sinyal dizisini, GERCEK SingleTrader execution motoruna (komisyon, pozisyon
// state-machine, kar-al/zarar-kes) geri verip equity/bakiye/istatistikleri ayni
// motordan taze urettirmek - boylece "tek dogru kaynak" execution tarafinda kalir,
// Python'da ayrica bir equity hesaplamasi YAZILMAZ. Sinyal uretimi disaridan
// (ReplaySignalStrategy, bkz. src/AlgoTrade.Core/Trading/Strategies/ReplayStrategies)
// gelir, calistiran motor AYNI motordur.
//
// Input JSON:
//   {
//     "stockDataFilePath": "...",          // zorunlu - orijinal run'da kullanilan veri dosyasi
//     "appConfigPath": "...",              // opsiyonel - varsayilan: inputs/configs/AppConfig/AppConfig.json
//     "signals": ["None","Buy","Sell","Flat", ...],  // zorunlu - bar sayisiyla ayni uzunlukta
//     "outputBundleDir": "...",            // zorunlu - .npz/.view.json nereye yazilacak
//     "outputBundleName": "replay_bundle"  // opsiyonel - varsayilan "replay_bundle"
//   }
//
// Output JSON (outputJsonPath'e yazilir):
//   { "success": true,  "bundlePath": "...", "viewPath": "..." }
//   { "success": false, "error": "..." }

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding  = Encoding.UTF8;

var ci = new CultureInfo(CultureInfo.CurrentCulture.Name);
ci.NumberFormat.NumberDecimalSeparator = ".";
ci.NumberFormat.NumberGroupSeparator   = ",";
CultureInfo.DefaultThreadCurrentCulture = ci;
Thread.CurrentThread.CurrentCulture      = ci;

if (args.Length < 2)
{
    Console.Error.WriteLine("Kullanim: AlgoTrade.ReplayRunner.exe <inputJsonPath> <outputJsonPath>");
    return 1;
}

string inputJsonPath  = args[0];
string outputJsonPath = args[1];

try
{
    var input = ReplayInput.LoadFromFile(inputJsonPath);

    string appConfigPath = string.IsNullOrWhiteSpace(input.AppConfigPath)
        ? Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json")
        : input.AppConfigPath;

    AppConfig appConfig = AppConfigLoader.Load(appConfigPath);

    var stockDataReader = new StockDataReader();
    stockDataReader.Clear();
    stockDataReader.ReStartTimer();
    var stockMetaData = stockDataReader.ReadMetaData(input.StockDataFilePath);
    stockDataReader.StopTimer();

    if (!stockDataReader.IsMetaDataRead)
        throw new InvalidOperationException($"Meta data okunamadi: {input.StockDataFilePath}");

    stockDataReader.ReStartTimer();
    stockDataReader.ReadDataFast(input.StockDataFilePath, StockDataReader.FilterMode.All, 0, 0, null, null);
    stockDataReader.StopTimer();

    var stockDataList = stockDataReader.GetData();
    if (stockDataList is null || stockDataList.Count == 0)
        throw new InvalidOperationException("Stock data bos geldi.");

    if (input.Signals.Length != stockDataList.Count)
    {
        throw new InvalidOperationException(
            $"Sinyal dizisi uzunlugu ({input.Signals.Length}) bar sayisiyla ({stockDataList.Count}) uyusmuyor.");
    }

    TradeSignals[] signals = input.Signals
        .Select(s => Enum.Parse<TradeSignals>(s, ignoreCase: true))
        .ToArray();

    var logger = LogManager.GetInstance();
    var timer  = TimeManager.GetInstance();

    var algoTrader = new AlgoTrader("ReplaySignalRunner");
    algoTrader.RegisterLogger(logger);
    algoTrader.RegisterTimer(timer);
    algoTrader.Reset();
    algoTrader.SetData(stockDataList);
    algoTrader.SymbolName   = stockMetaData.GetValueOrDefault("GrafikSembol",  "N/A");
    algoTrader.SymbolPeriod = stockMetaData.GetValueOrDefault("GrafikPeriyot", "N/A");

    // Baseline: bakiye/komisyon/sinyal-gate/vb. gercek calisan konfigurasyonla AYNI
    // (AppConfig.json -> SingleTrader) - sadece stratejiyi asagida ReplaySignalStrategy
    // ile degistiriyoruz, execution ekonomisi degismiyor.
    AppConfigApplier.ApplySingleTrader(algoTrader, appConfig.SingleTrader, AppSettings.ConfigsDir);
    algoTrader.ConfigureStrategy("ReplaySignalStrategy", new Dictionary<string, object>
    {
        ["signals"] = signals,
    });
    algoTrader.SingleTraderRunMode = TraderRunMode.TradeOnly;

    algoTrader.Initialize();
    await algoTrader.RunSingleTraderWithProgressAsync();

    // Istatistik/Lists/Performans dosyalari - production akisindaki (menu [5]/[6]/[8])
    // AYNI adimla AYNI yere (AppSettings.LogsDir) yazilir; bu klasor zaten her
    // runda "son calismanin raporlari" olarak ustune yazilan, run'a ozel olmayan
    // paylasilan bir konum - replay de ayni kurala uyar.
    await algoTrader.WriteTraderDataToFilesAsync(algoTrader.SingleTrader);

    string outputBundleName = string.IsNullOrWhiteSpace(input.OutputBundleName) ? "replay_bundle" : input.OutputBundleName;
    Directory.CreateDirectory(input.OutputBundleDir);

    var converter = new TradeDataBundleConverter();
    var (bundlePath, viewPath) = converter.ConvertSingleTrader(algoTrader.SingleTrader, input.OutputBundleDir, outputBundleName);

    if (appConfig.AppSettings.BundleReportsEnabled)
    {
        BundleReportPackager.Create(bundlePath, AppSettings.LogsDir, AppSettings.ConfigsDir, "SingleTrader");
    }

    WriteResult(outputJsonPath, new ReplayResult { Success = true, BundlePath = bundlePath, ViewPath = viewPath });
    return 0;
}
catch (Exception ex)
{
    WriteResult(outputJsonPath, new ReplayResult { Success = false, Error = ex.ToString() });
    return 1;
}

static void WriteResult(string path, ReplayResult result)
{
    string json = JsonSerializer.Serialize(result, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    });
    File.WriteAllText(path, json, Encoding.UTF8);
}

internal sealed class ReplayInput
{
    public string StockDataFilePath { get; set; } = "";
    public string AppConfigPath     { get; set; } = "";
    public string[] Signals         { get; set; } = Array.Empty<string>();
    public string OutputBundleDir   { get; set; } = "";
    public string OutputBundleName  { get; set; } = "";

    public static ReplayInput LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Input JSON bulunamadi: {path}");

        string raw = File.ReadAllText(path, Encoding.UTF8);
        var input = JsonSerializer.Deserialize<ReplayInput>(raw, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (input is null)
            throw new InvalidOperationException($"Input JSON parse edilemedi: {path}");

        if (string.IsNullOrWhiteSpace(input.StockDataFilePath))
            throw new ArgumentException("Input JSON: 'stockDataFilePath' zorunlu.");

        if (input.Signals.Length == 0)
            throw new ArgumentException("Input JSON: 'signals' zorunlu ve bos olamaz.");

        if (string.IsNullOrWhiteSpace(input.OutputBundleDir))
            throw new ArgumentException("Input JSON: 'outputBundleDir' zorunlu.");

        return input;
    }
}

internal sealed class ReplayResult
{
    public bool Success        { get; set; }
    public string? BundlePath  { get; set; }
    public string? ViewPath    { get; set; }
    public string? Error       { get; set; }
}
