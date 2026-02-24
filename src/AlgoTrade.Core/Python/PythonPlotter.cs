using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Python.Runtime;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using Serilog.Sinks.File;

namespace AlgoTrade.Core.Python;

/// <summary>
/// pythonnet Ã¼zerinden Python tabanlÄ± gÃ¶rselleÅŸtirme iÅŸlemlerini yÃ¶netir.
/// inputs/python/ klasÃ¶rÃ¼ndeki Python script'lerini Ã§aÄŸÄ±rÄ±r.
/// </summary>
public class PythonPlotter : IDisposable
{
    #region Properties

    /// <summary>Python runtime baÅŸlatÄ±ldÄ± mÄ±?</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Python DLL yolu.
    /// BoÅŸ bÄ±rakÄ±lÄ±rsa sistem PATH'inden Ã§Ã¶zÃ¼mlenir (Ã¶rn. "python" komutuyla bulunan Python).
    /// Explicit olarak verilmek istenirse: "python312.dll" veya tam yol.
    /// </summary>
    public string PythonDll { get; set; } = "";

    /// <summary>
    /// Python script'lerinin bulunduÄŸu klasÃ¶r.
    /// sys.path'e eklenir; buraya konan .py dosyalarÄ± import edilebilir.
    /// VarsayÄ±lan: AppSettings.PythonScriptsDir (inputs/python/).
    /// </summary>
    public string PythonScriptsDir { get; set; } = AppSettings.PythonScriptsDir;

    // PythonEngine process baÅŸÄ±na tek seferlik baÅŸlatÄ±lÄ±r/kapatÄ±lÄ±r.
    private static bool          _engineStarted = false;
    private static readonly object _engineLock  = new();

    private bool _disposed;

    #endregion

    #region Constructor

    public PythonPlotter() { }

    /// <param name="pythonDll">Python DLL yolu (Ã¶rn. "python312.dll" veya tam path).</param>
    public PythonPlotter(string pythonDll)
    {
        PythonDll = pythonDll;
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Python engine'i baÅŸlatÄ±r.
    /// AynÄ± process iÃ§inde birden fazla Ã§aÄŸrÄ±lsa da yalnÄ±zca ilk Ã§aÄŸrÄ± etkilidir.
    /// </summary>
    public void Initialize()
    {
        // Fast path
        if (IsInitialized) return;

        lock (_engineLock)
        {
            if (_engineStarted)
            {
                IsInitialized = true;
                return;
            }

            try
            {
                // DLL yolunu belirle: Ã¶nce property, sonra otomatik tespit
                string dll = !string.IsNullOrEmpty(PythonDll)
                    ? PythonDll
                    : FindPythonDll()
                      ?? throw new InvalidOperationException(
                          "Python DLL bulunamadÄ±. PythonPlotter.PythonDll'i aÃ§Ä±kÃ§a set edin " +
                          "veya PYTHONNET_PYDLL ortam deÄŸiÅŸkenini tanÄ±mlayÄ±n.\n" +
                          "Ã–rn: plotter.PythonDll = @\"C:\\Python312\\python312.dll\"");

                if (!File.Exists(dll))
                    throw new FileNotFoundException($"Python DLL bulunamadÄ±: {dll}");

                if (!Directory.Exists(PythonScriptsDir))
                {
                    Directory.CreateDirectory(PythonScriptsDir);
                    System.Diagnostics.Debug.WriteLine(
                        $"Python script dizini oluÅŸturuldu: {PythonScriptsDir}");
                }

                Runtime.PythonDLL = dll;
                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads(); // Multi-threading desteÄŸi

                using (Py.GIL())
                {
                    // sys.path'e script dizinini ekle
                    dynamic sys = Py.Import("sys");
                    sys.path.insert(0, new PyString(PythonScriptsDir));

                    // AlgoTradeWithPaythonWithGemini venv/site-packages ve src (geÃ§ici)
                    string[] venvPaths =
                    {
                        @"D:\sage1\AlgoTrade\AlgoTradeWithPaythonWithGemini\.venv\Lib\site-packages",
                        @"D:\sage1\AlgoTrade\AlgoTradeWithPaythonWithGemini\venv\Lib\site-packages",
                        @"D:\Aykut\Projects\AlgoTradeWithPaythonWithGemini\venv\Lib\site-packages",
                        @"D:\Aykut\Projects\AlgoTradeWithPaythonWithGemini\.venv\Lib\site-packages",
                        @"D:\Aykut\Projects\AlgoTradeWithPaythonWithGemini\Aykut\venv\Lib\site-packages",
                    };

                    foreach (var venvPath in venvPaths)
                    {
                        if (Directory.Exists(venvPath))
                        {
                            sys.path.insert(0, new PyString(venvPath));
                            System.Diagnostics.Debug.WriteLine($"âœ“ Venv site-packages eklendi: {venvPath}");
                            break;
                        }
                    }

                    string[] srcPaths =
                    {
                        @"D:\sage1\AlgoTrade\AlgoTradeWithPaythonWithGemini\src",
                        @"D:\Aykut\Projects\AlgoTradeWithPaythonWithGemini\src",
                    };

                    foreach (var srcPath in srcPaths)
                    {
                        if (Directory.Exists(srcPath))
                        {
                            sys.path.insert(0, new PyString(srcPath));
                            System.Diagnostics.Debug.WriteLine($"âœ“ AlgoTradeWithPaythonWithGemini/src eklendi: {srcPath}");
                            break;
                        }
                    }
                }

                _engineStarted = true;
                IsInitialized = true;
                System.Diagnostics.Debug.WriteLine("âœ“ Python Engine initialized successfully (global singleton)");
            }
            catch (Exception ex)
            {
                throw new Exception($"Python initialization failed: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Python engine'i kapatÄ±r. Process sonunda bir kez Ã§aÄŸrÄ±lmalÄ±.
    /// PythonPlotter.Shutdown() ÅŸeklinde explicit Ã§aÄŸrÄ±lÄ±r; Dispose() iÃ§inde Ã§aÄŸrÄ±lmaz.
    /// </summary>
    public static void Shutdown()
    {
        lock (_engineLock)
        {
            if (!_engineStarted) return;
            PythonEngine.Shutdown();
            _engineStarted = false;
        }
    }

    #endregion

    #region Plot Methods

    /// <summary>
    /// inputs/python/main.py içindeki hello() fonksiyonunu Ã§aÄŸÄ±rÄ±r.
    /// Konsola "Hello Python" yazdÄ±rmak iÃ§in basit bir test.
    /// </summary>
    public void RunHello()
    {
        EnsureInitialized();

        using var gil = Py.GIL();
        dynamic main = Py.Import("main");
        main.hello();
    }

    /// <summary>
    /// Optimizasyon sonuÃ§larÄ±nÄ± Python'a aktarÄ±p gÃ¶rselleÅŸtirir.
    /// inputs/python/plotter.py içindeki show_optimization_results(data) fonksiyonunu Ã§aÄŸÄ±rÄ±r.
    /// </summary>
    /// <param name="results">SingleTraderOptimizer.Results listesi.</param>
    public void PlotOptimizationResults(List<OptimizationResult> results)
    {
        EnsureInitialized();

        // C# -> JSON -> Python (pythonnet type dÃ¶nÃ¼ÅŸÃ¼mÃ¼nden baÄŸÄ±msÄ±z, gÃ¼venli yol)
        var payload = results.Select(r => new
        {
            parameters        = r.Parameters,
            values            = r.Values,
            net_profit        = r.NetProfit,
            win_rate          = r.WinRate,
            profit_factor     = r.ProfitFactor,
            profit_factor_net = r.ProfitFactorNet,
            max_drawdown      = r.MaxDrawdown,
            strategy_name     = r.StrategyName,
        });

        string jsonStr = JsonConvert.SerializeObject(payload);

        using var gil = Py.GIL();

        dynamic json_module = Py.Import("json");
        dynamic pyData      = json_module.loads(jsonStr);

        dynamic plotter = Py.Import("plotter");
        plotter.show_optimization_results(pyData);
    }

    /// <summary>
    /// SingleTrader koşum sonuçlarını Python'a aktarÄ±p gÃ¶rselleÅŸtirir.
    /// inputs/python/plotter.py içindeki show_single_trader_data(data) fonksiyonunu Ã§aÄŸÄ±rÄ±r.
    /// Pencere kapanana dek bloklar.
    /// </summary>
    /// <param name="trader">Finalize() çağrılmış SingleTrader instance'Ä±.</param>
    public void PlotSingleTraderData(SingleTrader trader)
    {
        EnsureInitialized();

        if (trader == null)
            throw new ArgumentNullException(nameof(trader));

        if (trader.Data == null || trader.Data.Count == 0)
            throw new InvalidOperationException("Trader data is empty. Initialize() sonra çağırın.");

        Lists lists = trader.lists ?? throw new ArgumentException("trader.lists is null", nameof(trader));

        var (dateTimes, dates, times,
             opens, highs, lows, closes, volumes, lots,
             sinyalList, karZararFiyatList, bakiyeFiyatList,
             getiriFiyatList, getiriFiyatNetList) = ExtractTraderData(trader, lists);

        var strategyIndicators = GetStrategyIndicators(trader);

        string title   = trader.SymbolName   ?? "AlgoTrade";
        string periyot = trader.SymbolPeriod ?? "1H";

        CallPlotDataImgBundleNew(
            dateTimes, opens, highs, lows, closes, volumes, lots,
            sinyalList, karZararFiyatList, bakiyeFiyatList,
            getiriFiyatList, getiriFiyatNetList,
            strategyIndicators, title, periyot);
    }

    private static (
        List<DateTime> dateTimes,
        List<DateTime> dates,
        List<TimeSpan> times,
        List<double>   opens,
        List<double>   highs,
        List<double>   lows,
        List<double>   closes,
        List<long>     volumes,
        List<long>     lots,
        List<double>   sinyalList,
        List<double>   karZararFiyatList,
        List<double>   bakiyeFiyatList,
        List<double>   getiriFiyatList,
        List<double>   getiriFiyatNetList
    ) ExtractTraderData(SingleTrader trader, Lists lists)
    {
        List<DateTime> dateTimes = trader.Data.Select(d => d.DateTime).ToList();
        List<DateTime> dates     = trader.Data.Select(d => d.Date).ToList();
        List<TimeSpan> times     = trader.Data.Select(d => d.Time).ToList();
        List<double> opens   = trader.Data.Select(d => d.Open).ToList();
        List<double> highs   = trader.Data.Select(d => d.High).ToList();
        List<double> lows    = trader.Data.Select(d => d.Low).ToList();
        List<double> closes  = trader.Data.Select(d => d.Close).ToList();
        List<long> volumes = trader.Data.Select(d => d.Volume).ToList();
        List<long> lots    = trader.Data.Select(d => d.Size).ToList();

        List<double> sinyalList         = lists.SinyalList;
        List<double> karZararFiyatList  = lists.KarZararFiyatList;
        List<double> bakiyeFiyatList    = lists.BakiyeFiyatList;
        List<double> getiriFiyatList    = lists.GetiriFiyatList;
        List<double> getiriFiyatNetList = lists.GetiriFiyatNetList;

        return (dateTimes, dates, times,
                opens, highs, lows, closes, volumes, lots,
                sinyalList, karZararFiyatList, bakiyeFiyatList,
                getiriFiyatList, getiriFiyatNetList);
    }

    private static Dictionary<string, double[]>? GetStrategyIndicators(SingleTrader trader)
    {
        return trader.Strategy?.GetPlotIndicators();
    }

    private static void CallPlotDataImgBundleNew(
        List<DateTime> dateTimes,
        List<double>   opens,
        List<double>   highs,
        List<double>   lows,
        List<double>   closes,
        List<long>     volumes,
        List<long>     lots,
        List<double>   sinyalList,
        List<double>   karZararFiyatList,
        List<double>   bakiyeFiyatList,
        List<double>   getiriFiyatList,
        List<double>   getiriFiyatNetList,
        Dictionary<string, double[]>? strategyIndicators,
        string         title,
        string         periyot)
    {
        using (Py.GIL())
        {
            // (AlgoTradeWithPaythonWithGemini/src sys.path'e Initialize() içinde eklendi)
            dynamic plotModule = Py.Import("plotDataImgBundleNew");

            using var pyDates     = new PyList();
            using var pyOpens     = new PyList();
            using var pyHighs     = new PyList();
            using var pyLows      = new PyList();
            using var pyCloses    = new PyList();
            using var pyVolumes   = new PyList();
            using var pyLots      = new PyList();
            using var pySinyal    = new PyList();
            using var pyKarZarar  = new PyList();
            using var pyBakiye    = new PyList();
            using var pyGetiri    = new PyList();
            using var pyGetiriNet = new PyList();

            foreach (var d in dateTimes)            pyDates.Append(new PyString(d.ToString("yyyy.MM.dd HH:mm:ss")));
            foreach (var v in opens)                pyOpens.Append(new PyFloat(v));
            foreach (var v in highs)                pyHighs.Append(new PyFloat(v));
            foreach (var v in lows)                 pyLows.Append(new PyFloat(v));
            foreach (var v in closes)               pyCloses.Append(new PyFloat(v));
            foreach (var v in volumes)              pyVolumes.Append(new PyFloat(v));
            foreach (var v in lots)                 pyLots.Append(new PyFloat(v));
            foreach (var v in sinyalList)           pySinyal.Append(new PyFloat(v));
            foreach (var v in karZararFiyatList)    pyKarZarar.Append(new PyFloat(v));
            foreach (var v in bakiyeFiyatList)      pyBakiye.Append(new PyFloat(v));
            foreach (var v in getiriFiyatList)      pyGetiri.Append(new PyFloat(v));
            foreach (var v in getiriFiyatNetList)   pyGetiriNet.Append(new PyFloat(v));

            dynamic pyIndicators = new PyDict();
            if (strategyIndicators != null)
            {
                foreach (var kvp in strategyIndicators)
                {
                    if (kvp.Value != null && kvp.Value.Length > 0)
                    {
                        dynamic pyList = new PyList();
                        foreach (var v in kvp.Value) pyList.Append(new PyFloat(v));
                        pyIndicators[kvp.Key] = pyList;
                    }
                }
            }

            plotModule.plot_data_img_bundle_new(
                pyDates, pyOpens, pyHighs, pyLows, pyCloses,
                pyVolumes, pyLots,
                pySinyal, pyKarZarar, pyBakiye, pyGetiri, pyGetiriNet,
                bakiye_fiyat_net_list:          null,
                kar_zarar_fiyat_yuzde_list:     null,
                getiri_fiyat_yuzde_list:        null,
                komisyon_fiyat_list:            null,
                getiri_fiyat_yuzde_net_list:    null,
                strategy_indicators:            pyIndicators,
                title:                          title,
                periyot:                        periyot
            );
        }
    }

    #endregion

    #region Private

    private void EnsureInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException(
                "PythonPlotter baÅŸlatÄ±lmadÄ±. Ã–nce Initialize() Ã§aÄŸrÄ±n.");
    }

    /// <summary>
    /// Python DLL yolunu otomatik tespit eder.
    /// Ã–nce PYTHONNET_PYDLL ortam deÄŸiÅŸkenine, sonra PATH'deki 'python' komutuna bakar.
    /// </summary>
    private static string? FindPythonDll()
    {
        string[] possiblePaths = new[]
        {
            // Python 3.13
            @"C:\Program Files\Python313\python313.dll",
            @"C:\Python313\python313.dll",
            @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python313\python313.dll",
            // Python 3.12
            @"C:\Program Files\Python312\python312.dll",
            @"C:\Python312\python312.dll",
            @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python312\python312.dll",
            // Python 3.11
            @"C:\Program Files\Python311\python311.dll",
            @"C:\Python311\python311.dll",
            @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python311\python311.dll"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                System.Diagnostics.Debug.WriteLine($"Python DLL found: {path}");
                return path;
            }
        }

        throw new FileNotFoundException(
            "Python DLL bulunamadÄ±! LÃ¼tfen Python 3.11+ kurun.\n" +
            "Ä°ndirme: https://www.python.org/downloads/"
        );
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        // PythonEngine.Shutdown() global/static state olduÄŸu iÃ§in Dispose iÃ§inde Ã§aÄŸrÄ±lmaz.
        // Uygulama sonunda explicit olarak PythonPlotter.Shutdown() Ã§aÄŸrÄ±lmalÄ±.
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}



