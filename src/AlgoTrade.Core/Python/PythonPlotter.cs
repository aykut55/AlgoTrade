using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using Newtonsoft.Json;
using Python.Runtime;
using ScottPlot;
using ScottPlot.Plottables;
using Serilog.Sinks.File;
using System.Linq;
using System.Reflection.Metadata;
using Tulip;

namespace AlgoTrade.Core.Python;

/// <summary>
/// pythonnet üzerinden Python tabanlı görselleştirme işlemlerini yönetir.
/// inputs/python/ klasöründeki Python script'lerini çağırır.
/// </summary>
public class PythonPlotter : IDisposable
{
    #region Properties

    /// <summary>Python runtime baÅŸlatıldı mı?</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Python DLL yolu.
    /// BoÅŸ bırakılırsa sistem PATH'inden çözümlenir (örn. "python" komutuyla bulunan Python).
    /// Explicit olarak verilmek istenirse: "python312.dll" veya tam yol.
    /// </summary>
    public string PythonDll { get; set; } = "";

    /// <summary>
    /// Python script'lerinin bulunduÄŸu klasör.
    /// sys.path'e eklenir; buraya konan .py dosyaları import edilebilir.
    /// Varsayılan: AppSettings.PythonScriptsDir (inputs/python/).
    /// </summary>
    public string PythonScriptsDir { get; set; } = AppSettings.PythonScriptsDir;

    // PythonEngine process baÅŸına tek seferlik baÅŸlatılır/kapatılır.
    private static bool          _engineStarted = false;
    private static readonly object _engineLock  = new();

    private bool              _disposed;
    private LogManager?       _logger;
    private IndicatorManager? _indicators;

    #endregion

    #region Data Fields

    private List<DateTime>                _dateTimes               = new();
    private List<DateTime>                _dates                   = new();
    private List<TimeSpan>                _times                   = new();
    private List<double>                  _opens                   = new();
    private List<double>                  _highs                   = new();
    private List<double>                  _lows                    = new();
    private List<double>                  _closes                  = new();
    private List<long>                    _volumes                 = new();
    private List<long>                    _lots                    = new();
    private List<double>                  _sinyalList              = new();
    private List<double>                  _karZararFiyatList       = new();
    private List<double>                  _bakiyeFiyatList         = new();
    private List<double>                  _getiriFiyatList         = new();
    private List<double>                  _komisyonFiyatList       = new();
    private List<double>                  _bakiyeFiyatNetList      = new();
    private List<double>                  _getiriFiyatNetList      = new();
    private List<double>                  _karZararFiyatYuzdeList  = new();
    private List<double>                  _getiriFiyatYuzdeList    = new();
    private List<double>                  _getiriFiyatNetYuzdeList = new();
    private Dictionary<string, double[]>? _strategyIndicators;
    private string                        _title                   = "AlgoTrade";
    private string                        _periyot                 = "1H";

    #endregion

    #region Constructor

    public PythonPlotter() { }

    /// <param name="pythonDll">Python DLL yolu (örn. "python312.dll" veya tam path).</param>
    public PythonPlotter(string pythonDll)
    {
        PythonDll = pythonDll;
    }

    public void SetLogger(LogManager? logger)         => _logger     = logger;
    public void SetIndicators(IndicatorManager? ind)  => _indicators = ind;

    #endregion

    #region Initialization

    /// <summary>
    /// Python engine'i başlatır.
    /// Aynı process içinde birden fazla çağrılsa da yalnızca ilk çağrı etkilidir.
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
                // DLL yolunu belirle: önce property, sonra otomatik tespit
                string dll = !string.IsNullOrEmpty(PythonDll)
                    ? PythonDll
                    : FindPythonDll()
                      ?? throw new InvalidOperationException(
                          "Python DLL bulunamadı. PythonPlotter.PythonDll'i açıkça set edin " +
                          "veya PYTHONNET_PYDLL ortam değişkenini tanımlayın.\n" +
                          "Örnek: plotter.PythonDll = @\"C:\\Python312\\python312.dll\"");

                if (!File.Exists(dll))
                    throw new FileNotFoundException($"Python DLL bulunamadı: {dll}");

                if (!Directory.Exists(PythonScriptsDir))
                {
                    Directory.CreateDirectory(PythonScriptsDir);
                    _logger?.WriteLog($"Python script dizini oluşturuldu: {PythonScriptsDir}");
                }

                Runtime.PythonDLL = dll;
                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads(); // Multi-threading desteği

                using (Py.GIL())
                {
                    // sys.path'e script dizinini ekle
                    dynamic sys = Py.Import("sys");
                    sys.path.insert(0, new PyString(PythonScriptsDir));

                    // Venv site-packages — proje kökündeki tek ortak .venv
                    string[] venvPaths =
                    {
                        Path.Combine(AppSettings.VenvDir, "Lib", "site-packages")
                    };

                    dynamic os = Py.Import("os");

                    foreach (var venvPath in venvPaths)
                    {
                        if (Directory.Exists(venvPath))
                        {
                            sys.path.insert(0, new PyString(venvPath));
                            _logger?.WriteRaw($"✓ Venv site-packages added: {venvPath}");

                            // imgui_bundle native DLL'leri için kendi dizinini arama yoluna ekle
                            string imguiBundleDir = Path.Combine(venvPath, "imgui_bundle");
                            if (Directory.Exists(imguiBundleDir))
                            {
                                os.add_dll_directory(imguiBundleDir);
                                _logger?.WriteRaw($"✓ imgui_bundle DLL directory added: {imguiBundleDir}");
                            }

                            break;
                        }
                    }

                    // string[] srcPaths =
                    // {
                    //     @"D:\sage1\AlgoTrade\AlgoTradeWithPaythonWithGemini\src",
                    //     @"D:\Aykut\Projects\AlgoTradeWithPaythonWithGemini\src",
                    // };
                    // foreach (var srcPath in srcPaths)
                    // {
                    //     if (Directory.Exists(srcPath))
                    //     {
                    //         sys.path.insert(0, new PyString(srcPath));
                    //         _logger?.WriteRaw($"✓ AlgoTradeWithPythonWithGemini/src eklendi: {srcPath}");
                    //         break;
                    //     }
                    // }
                }

                _engineStarted = true;
                IsInitialized = true;
                _logger?.WriteRaw("✓ Python Engine initialized successfully (global singleton)");
            }
            catch (Exception ex)
            {
                throw new Exception($"Python initialization failed: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Python engine'i kapatır. Process sonunda bir kez çağrılmalı.
    /// PythonPlotter.Shutdown() şeklinde explicit çağrılır; Dispose() içinde çağrılmaz.
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
    /// inputs/python/main.py içindeki hello() fonksiyonunu çağırır.
    /// Konsola "Hello Python" yazdırmak için basit bir test.
    /// </summary>
    public void RunHello()
    {
        EnsureInitialized();

        using var gil = Py.GIL();
        dynamic main = Py.Import("main");
        main.hello();
    }

    /// <summary>
    /// Optimizasyon sonuçlarını Python'a aktarıp görselleştirir.
    /// inputs/python/plotter.py içindeki show_optimization_results(data) fonksiyonunu çağırır.
    /// </summary>
    /// <param name="results">SingleTraderOptimizer.Results listesi.</param>
    public void PlotOptimizationResults(List<OptimizationResult> results)
    {
        EnsureInitialized();

        // C# -> JSON -> Python (pythonnet type dönüşümünden bağımsız, güvenli yol)
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
    /// SingleTrader koşum sonuçlarını Python'a aktarıp görselleştirir.
    /// inputs/python/plotter.py içindeki show_single_trader_data(data) fonksiyonunu çağırır.
    /// Pencere kapanana dek bloklar.
    /// </summary>
    /// <param name="trader">Finalize() çağrılmış SingleTrader instance'ı.</param>
    public void PlotSingleTraderData(SingleTrader trader)
    {
        EnsureInitialized();

        if (trader == null)
            throw new ArgumentNullException(nameof(trader));

        if (trader.Data == null || trader.Data.Count == 0)
            throw new InvalidOperationException("Trader data is empty. Initialize() sonra çağırın.");

        Lists lists = trader.lists ?? throw new ArgumentException("trader.lists is null", nameof(trader));

        var closes = trader.GetClosePrices();

        var indicatorsToPlot = new Dictionary<string, double[]?>
        {
            ["ma5"] = _indicators?.MA.SMA(closes, 5),
            ["ma8"] = _indicators?.MA.SMA(closes, 8),
            ["ma13"] = _indicators?.MA.SMA(closes, 13),
            ["ma21"] = _indicators?.MA.SMA(closes, 21),
            ["ma34"] = _indicators?.MA.SMA(closes, 34),
            ["ma50"] = _indicators?.MA.SMA(closes, 50),
            ["ma100"] = _indicators?.MA.SMA(closes, 100),
            ["ma200"] = _indicators?.MA.SMA(closes, 200),
        };

        _logger?.WriteRaw($"  [Plot] Preparing data ({trader.Data.Count:N0} bars)...");

        using (Py.GIL())
        {
            ExtractTraderData(trader, lists);
            dynamic tradeData = BuildPyTradeData();
            SetPyIndicators(tradeData, indicatorsToPlot);

            _logger?.WriteRaw($"  [Plot] Data ready. Opening plot window...");
            CallPlotDataImgBundleNew(tradeData);
            _logger?.WriteRaw($"  [Plot] Window closed.");
        }
    }

    /// <summary>
    /// MultipleTrader koşum sonuçlarını Python'a aktarıp görselleştirir.
    /// mainTrader ve tüm child trader verileri PyList olarak gönderilir.
    /// Pencere kapanana dek bloklar.
    /// </summary>
    public void PlotMultipleTraderData(MultipleTrader multipleTrader)
    {
        EnsureInitialized();

        if (multipleTrader == null)
            throw new ArgumentNullException(nameof(multipleTrader));

        var mainTrader = multipleTrader.GetMainTrader()
            ?? throw new ArgumentException("multipleTrader.GetMainTrader() null döndü.", nameof(multipleTrader));

        int totalTraders = 1 + multipleTrader.Traders.Count;
        int doneTraders  = 0;
        int totalBars    = mainTrader.Data?.Count ?? 0;
        int totalAllBars = totalBars * totalTraders;

        _logger?.WriteRaw($"  [Plot] Preparing data for {totalTraders} traders ({totalBars:N0} bars each, {totalAllBars:N0} bars total)...");

        using (Py.GIL())
        {
            var pyTraderList = new PyList();

            // mainTrader
            _logger?.WriteRaw($"  [Plot] [{++doneTraders}/{totalTraders}] mainTrader → Python...");
            ExtractTraderData(mainTrader, mainTrader.lists
                ?? throw new ArgumentException("mainTrader.lists is null"));
            var mainData   = BuildPyTradeData();
            var mainCloses = mainTrader.GetClosePrices();
            SetPyIndicators(mainData, new Dictionary<string, double[]?>
            {
                ["ma5"]   = _indicators?.MA.SMA(mainCloses, 5),
                ["ma20"]  = _indicators?.MA.SMA(mainCloses, 20),
                ["ma50"]  = _indicators?.MA.SMA(mainCloses, 50),
                ["ma200"] = _indicators?.MA.SMA(mainCloses, 200),
            });
            pyTraderList.Append(mainData);

            // child traders
            foreach (var child in multipleTrader.Traders)
            {
                if (child?.lists == null) continue;

                _logger?.WriteRaw($"  [Plot] [{++doneTraders}/{totalTraders}] child[{doneTraders - 2}] → Python...");
                ExtractTraderData(child, child.lists);
                var childData   = BuildPyTradeData();
                var childCloses = child.GetClosePrices();
                SetPyIndicators(childData, new Dictionary<string, double[]?>
                {
                    ["ma5"]   = _indicators?.MA.SMA(childCloses, 5),
                    ["ma20"]  = _indicators?.MA.SMA(childCloses, 20),
                    ["ma50"]  = _indicators?.MA.SMA(childCloses, 50),
                    ["ma200"] = _indicators?.MA.SMA(childCloses, 200),
                });
                pyTraderList.Append(childData);
            }

            _logger?.WriteRaw($"  [Plot] Data ready. Opening plot window...");
            CallPlotMultipleTraderData(pyTraderList);
            _logger?.WriteRaw($"  [Plot] Window closed.");
        }
    }

    private void ExtractTraderData(SingleTrader trader, Lists lists)
    {
        _dateTimes               = trader.Data.Select(d => d.DateTime).ToList();
        _dates                   = trader.Data.Select(d => d.Date).ToList();
        _times                   = trader.Data.Select(d => d.Time).ToList();
        _opens                   = trader.Data.Select(d => d.Open).ToList();
        _highs                   = trader.Data.Select(d => d.High).ToList();
        _lows                    = trader.Data.Select(d => d.Low).ToList();
        _closes                  = trader.Data.Select(d => d.Close).ToList();
        _volumes                 = trader.Data.Select(d => d.Volume).ToList();
        _lots                    = trader.Data.Select(d => d.Size).ToList();
        _sinyalList              = lists.SinyalList;
        _karZararFiyatList       = lists.KarZararFiyatList;
        _bakiyeFiyatList         = lists.BakiyeFiyatList;
        _getiriFiyatList         = lists.GetiriFiyatList;
        _komisyonFiyatList       = lists.KomisyonFiyatList;
        _bakiyeFiyatNetList      = lists.BakiyeFiyatNetList;
        _getiriFiyatNetList      = lists.GetiriFiyatNetList;
        _karZararFiyatYuzdeList  = lists.KarZararFiyatYuzdeList;
        _getiriFiyatYuzdeList    = lists.GetiriFiyatYuzdeList;
        _getiriFiyatNetYuzdeList = lists.GetiriFiyatYuzdeNetList;
        _strategyIndicators      = trader.Strategy?.GetPlotIndicators();
        _title                   = trader.SymbolName   ?? "AlgoTrade";
        _periyot                 = trader.SymbolPeriod ?? "1H";
    }

    /// <summary>
    /// C# listelerini Python PyList/PyDict'e dönüştürür, trade_data.TradeData() instance'ını
    /// setter'lar üzerinden doldurur ve Python nesnesini döndürür.
    /// GIL çağrı öncesinde edinilmiş olmalıdır.
    /// </summary>
    private dynamic BuildPyTradeData()
    {
        var pyDateTimes      = new PyList();
        var pyDates          = new PyList();
        var pyTimes          = new PyList();
        var pyOpens          = new PyList();
        var pyHighs          = new PyList();
        var pyLows           = new PyList();
        var pyCloses         = new PyList();
        var pyVolumes        = new PyList();
        var pyLots           = new PyList();
        var pySinyal         = new PyList();
        var pyKarZarar       = new PyList();
        var pyBakiye         = new PyList();
        var pyGetiri         = new PyList();
        var pyKomisyon       = new PyList();
        var pyBakiyeNet      = new PyList();
        var pyGetiriNet      = new PyList();
        var pyKarZararYuzde  = new PyList();
        var pyGetiriYuzde    = new PyList();
        var pyGetiriNetYuzde = new PyList();

        foreach (var d in _dateTimes)               pyDateTimes.Append(new PyString(d.ToString("yyyy.MM.dd HH:mm:ss")));
        foreach (var d in _dates)                   pyDates.Append(new PyString(d.ToString("yyyy.MM.dd")));
        foreach (var t in _times)                   pyTimes.Append(new PyString(t.ToString(@"hh\:mm\:ss")));
        foreach (var v in _opens)                   pyOpens.Append(new PyFloat(v));
        foreach (var v in _highs)                   pyHighs.Append(new PyFloat(v));
        foreach (var v in _lows)                    pyLows.Append(new PyFloat(v));
        foreach (var v in _closes)                  pyCloses.Append(new PyFloat(v));
        foreach (var v in _volumes)                 pyVolumes.Append(new PyFloat(v));
        foreach (var v in _lots)                    pyLots.Append(new PyFloat(v));
        foreach (var v in _sinyalList)              pySinyal.Append(new PyFloat(v));
        foreach (var v in _karZararFiyatList)       pyKarZarar.Append(new PyFloat(v));
        foreach (var v in _bakiyeFiyatList)         pyBakiye.Append(new PyFloat(v));
        foreach (var v in _getiriFiyatList)         pyGetiri.Append(new PyFloat(v));
        foreach (var v in _komisyonFiyatList)       pyKomisyon.Append(new PyFloat(v));
        foreach (var v in _bakiyeFiyatNetList)      pyBakiyeNet.Append(new PyFloat(v));
        foreach (var v in _getiriFiyatNetList)      pyGetiriNet.Append(new PyFloat(v));
        foreach (var v in _karZararFiyatYuzdeList)  pyKarZararYuzde.Append(new PyFloat(v));
        foreach (var v in _getiriFiyatYuzdeList)    pyGetiriYuzde.Append(new PyFloat(v));
        foreach (var v in _getiriFiyatNetYuzdeList) pyGetiriNetYuzde.Append(new PyFloat(v));

        // strategy_indicators
        var pyStrategyIndicators = new PyDict();
        if (_strategyIndicators != null)
        {
            foreach (var kvp in _strategyIndicators)
            {
                if (kvp.Value != null && kvp.Value.Length > 0)
                {
                    var pyList = new PyList();
                    foreach (var v in kvp.Value) pyList.Append(new PyFloat(v));
                    pyStrategyIndicators[new PyString(kvp.Key)] = pyList;
                }
            }
        }
/*
 *      Performans sorunu yasatır gibi duruyor, o yuzden commentledim
 *      
        // indicators (strateji bağımsız — IndicatorManager cache'i)
        var pyIndicators = new PyDict();
        if (_indicators != null)
        {
            foreach (var kvp in _indicators.GetCachedIndicators())
            {
                if (kvp.Value != null && kvp.Value.Length > 0)
                {
                    var pyList = new PyList();
                    foreach (var v in kvp.Value) pyList.Append(new PyFloat(v));
                    pyIndicators[new PyString(kvp.Key)] = pyList;
                }
            }
        }
*/
        dynamic tradeDataModule = Py.Import("trade_data");
        dynamic td = tradeDataModule.TradeData();

        td.date_times                  = pyDateTimes;
        td.dates                       = pyDates;
        td.times                       = pyTimes;
        td.opens                       = pyOpens;
        td.highs                       = pyHighs;
        td.lows                        = pyLows;
        td.closes                      = pyCloses;
        td.volumes                     = pyVolumes;
        td.lots                        = pyLots;
        td.sinyal_list                 = pySinyal;
        td.kar_zarar_fiyat_list        = pyKarZarar;
        td.bakiye_fiyat_list           = pyBakiye;
        td.getiri_fiyat_list           = pyGetiri;
        td.komisyon_fiyat_list         = pyKomisyon;
        td.bakiye_fiyat_net_list       = pyBakiyeNet;
        td.getiri_fiyat_net_list       = pyGetiriNet;
        td.kar_zarar_fiyat_yuzde_list  = pyKarZararYuzde;
        td.getiri_fiyat_yuzde_list     = pyGetiriYuzde;
        td.getiri_fiyat_net_yuzde_list = pyGetiriNetYuzde;
        td.indicators                  = new PyDict();          // td.indicators = pyIndicators
        td.strategy_indicators         = pyStrategyIndicators;
        td.title                       = _title;
        td.periyot                     = _periyot;

        return td;
    }

    private bool CallPlotDataImgBundleNew(dynamic tradeData)
    {
        bool success = false;

        try
        {
            // -----------------------------------------
            dynamic sys_module = Py.Import("sys");
            dynamic old_stdout = sys_module.stdout;

            try
            {
                // Python stdout'u yakala
                dynamic io = Py.Import("io");
                dynamic stdout = io.StringIO();
                sys_module.stdout = stdout;

                // imgui_bundle kontrolü
                try
                {
                    Py.Import("imgui_bundle");
                    _logger?.WriteRaw("✓ imgui_bundle imported successfully.\n");
                }
                catch (PythonException)
                {
                    throw new Exception(
                        "imgui_bundle yüklü değil!\n\n" +
                        "Proje kökünde ortak .venv artık burada: " + AppSettings.VenvDir + "\n" +
                        "Lütfen proje kökünde şunu çalıştırın:\n" +
                        "  setupPythonEnvs.bat"
                    );
                }

                dynamic mainModule = Py.Import("main");
                var result = mainModule.print_data_info(tradeData);

                // plotDataImgBundleNew modülünü import et
                // dynamic plotModule = Py.Import("plotDataImgBundleNew");
                // (AlgoTradeWithPaythonWithGemini/src sys.path'e Initialize() içinde eklendi)
                // plotModule.plot_data_img_bundle_new(tradeData);

                // Python stdout'u al
                string pythonOutput = stdout.getvalue().ToString();
                if (!string.IsNullOrEmpty(pythonOutput))
                {
                    _logger?.WriteRaw("=== PYTHON OUTPUT ===");
                    _logger?.WriteRaw(pythonOutput);
                    _logger?.WriteRaw("=== END PYTHON OUTPUT ===");
                }

                success = (bool)result;
                if (!success)
                {
                    _logger?.WriteLog("❌ Python plot_data_img_bundle_new returned False!");
                }

            }
            finally
            {
                // stdout'u geri yükle
                sys_module.stdout = old_stdout;
            }
            // -----------------------------------------
        }
        catch (PythonException pyEx)
        {
            throw new Exception($"Python plotting error: {pyEx.Message}\n{pyEx.StackTrace}", pyEx);
        }

        return success;
    }

    /// <summary>
    /// mainTrader + child trader listesini Python'a gönderir.
    /// Python tarafında main.print_multiple_trader_data(trader_list) çağrılır.
    /// GIL çağrı öncesinde edinilmiş olmalıdır.
    /// </summary>
    private void CallPlotMultipleTraderData(PyList traderList)
    {
        try
        {
            dynamic sys_module = Py.Import("sys");
            dynamic old_stdout = sys_module.stdout;

            try
            {
                dynamic io = Py.Import("io");
                sys_module.stdout = io.StringIO();

                dynamic mainModule = Py.Import("main");
                mainModule.print_multiple_trader_data(traderList);

                string pythonOutput = sys_module.stdout.getvalue().ToString();
                if (!string.IsNullOrEmpty(pythonOutput))
                {
                    _logger?.WriteRaw("=== PYTHON OUTPUT ===");
                    _logger?.WriteRaw(pythonOutput);
                    _logger?.WriteRaw("=== END PYTHON OUTPUT ===");
                }
            }
            finally
            {
                sys_module.stdout = old_stdout;
            }
        }
        catch (PythonException pyEx)
        {
            throw new Exception($"Python MultipleTrader plotting error: {pyEx.Message}\n{pyEx.StackTrace}", pyEx);
        }
    }

    #endregion

    #region Private

    /// <summary>
    /// Verilen indikatör sözlüğünü tradeData.indicators'a PyList olarak ekler.
    /// Null veya boş olan değerler atlanır. GIL çağrı öncesinde edinilmiş olmalıdır.
    /// </summary>
    private static void SetPyIndicators(dynamic tradeData, Dictionary<string, double[]?> indicators)
    {
        foreach (var kvp in indicators)
        {
            if (kvp.Value == null || kvp.Value.Length == 0) continue;

            var pyList = new PyList();
            foreach (var v in kvp.Value) pyList.Append(new PyFloat(v));
            tradeData.indicators[kvp.Key] = pyList;
        }
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException(
                "PythonPlotter başlatılmadı. Önce Initialize() çağırın.");
    }

    /// <summary>
    /// Python DLL yolunu otomatik tespit eder.
    /// Tek kaynak: proje kökündeki ortak .venv'in pyvenv.cfg'sinden türetilen DLL — venv hangi
    /// Python sürümüyle kurulduysa (bkz. setupPythonEnvs.bat) otomatik onu bulur. Sistem geneli
    /// kurulum yollarına bilerek bakılmıyor: pythonnet'in embed ettiği sürüm venv'in paket
    /// wheel'leriyle (ABI) eşleşmek zorunda — sys.path her zaman venv/Lib/site-packages'a sabit
    /// eklendiğinden, yanlış (ör. başka bir işten kalma sistem geneli) bir DLL bulunması "DLL yok"
    /// hatası yerine çok daha kafa karıştırıcı bir ABI uyuşmazlığı/crash'e yol açardı.
    /// </summary>
    private string? FindPythonDll()
    {
        var fromVenv = AppSettings.ResolvePythonDll();
        if (fromVenv != null)
        {
            _logger?.WriteLog($"Python DLL (venv'den çözümlendi): {fromVenv}");
            return fromVenv;
        }

        throw new FileNotFoundException(
            "Python DLL bulunamadı! Proje kökünde setupPythonEnvs.bat'ı çalıştırıp .venv'i kurun,\n" +
            "veya PYTHONNET_PYDLL ortam değişkenini elle ayarlayın."
        );
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        // PythonEngine.Shutdown() global/static state olduÄŸu için Dispose içinde çaÄŸrılmaz.
        // Uygulama sonunda explicit olarak PythonPlotter.Shutdown() çaÄŸrılmalı.
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}



