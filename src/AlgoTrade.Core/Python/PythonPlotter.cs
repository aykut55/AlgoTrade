using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Python.DearPyGuiDataPlotter;
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
/// src/PythonPlotter/ klasöründeki Python script'lerini çağırır.
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
    /// Varsayılan: AppSettings.PythonScriptsDir (src/PythonPlotter/).
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

    // NOT (2026-08-25): PlotSingleTraderData/PlotMultipleTraderData (aşağıda) hâlâ TEK aktif/
    // varsayılan yol — 01_RunSingleTraderWithProgressAsync.csx (Menü [5]), 02_RunMultipleTrader...
    // (Menü [6]) ve tüm diğer menü/script'ler bunları çağırıyor. "Bundle Plot Methods" region'ındaki
    // PlotBundleFile/PlotBundleFileFromDisk/SaveBundleToDisk EK/opsiyonel olarak eklendi (bkz.
    // docs/todo.md "Geçmiş (Offline) Trader Verilerinden Hızlı Sinyal Plot'u") — hiçbir mevcut
    // menü/script'in davranışını değiştirmiyor, sadece TestOldPlotterFromBundle.csx gibi yeni
    // yazılan kodlardan çağrılıyor. Birini kullanmak istiyorsan açıkça o metodu çağırman gerekiyor.

    /// <summary>
    /// src/PythonPlotter/main.py içindeki hello() fonksiyonunu çağırır.
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
    /// src/PythonPlotter/plotter.py içindeki show_optimization_results(data) fonksiyonunu çağırır.
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
    /// src/PythonPlotter/plotter.py içindeki show_single_trader_data(data) fonksiyonunu çağırır.
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

    #region Bundle Plot Methods (Offline Replay — .npz/.view.json'dan çizim)

    /// <summary>
    /// .npz/.view.json bundle dosyasından (TradeDataBundleConverter'ın ürettiği formatta)
    /// SingleTrader'ı hiç yeniden çalıştırmadan çizim yapar. "Memory" yol: bundle C# tarafında
    /// (<see cref="NpzReader"/> ile) belleğe okunur, sonra mevcut
    /// <see cref="BuildPyTradeData"/>/<see cref="CallPlotDataImgBundleNew"/> render pipeline'ı
    /// HİÇ değiştirilmeden reuse edilir. Şimdilik asıl kullanılan/aktif yol bu.
    /// bkz. <see cref="PlotBundleFileFromDisk"/> — aynı işi Python tarafında numpy.load ile
    /// yapan alternatif/karşılaştırma yolu.
    /// Pencere kapanana dek bloklar.
    /// </summary>
    /// <param name="bundlePath">.npz bundle dosyasının yolu.</param>
    /// <param name="viewPath">
    /// .view.json dosyasının yolu (opsiyonel). Şu an KULLANILMIYOR — eski tip plotter'ın kendi
    /// sabit panel yerleşimi var (bkz. src/PythonPlotter/data_plotter.py); ileride view.json'daki
    /// panel/seri seçimini yansıtmak için ayrılmış bir parametre.
    /// </param>
    public void PlotBundleFile(string bundlePath, string? viewPath = null)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(bundlePath))
            throw new ArgumentException("bundlePath boş olamaz.", nameof(bundlePath));
        if (!File.Exists(bundlePath))
            throw new FileNotFoundException($"Bundle dosyası bulunamadı: {bundlePath}", bundlePath);

        _logger?.WriteRaw($"  [Plot] Bundle okunuyor (memory/NpzReader): {bundlePath}");

        var reader = new NpzReader(bundlePath);
        ExtractBundleData(reader);

        var closes = _closes.ToArray();
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

        _logger?.WriteRaw($"  [Plot] Bundle verisi hazır ({_closes.Count:N0} bar). Pencere açılıyor...");

        using (Py.GIL())
        {
            dynamic tradeData = BuildPyTradeData();
            SetPyIndicators(tradeData, indicatorsToPlot);

            CallPlotDataImgBundleNew(tradeData);
            _logger?.WriteRaw($"  [Plot] Pencere kapandı.");
        }
    }

    /// <summary>
    /// <see cref="PlotBundleFile"/> ile aynı sonucu üretir, ama okuma C# tarafında
    /// (<see cref="NpzReader"/>) değil, Python tarafında numpy.load(...) ile yapılır
    /// (src/PythonPlotter/bundle_loader.py: build_trade_data_from_bundle). NpzReader'a hiç ihtiyaç
    /// duymayan, numpy'nin kendi .npz parser'ını kullanan alternatif/karşılaştırma yolu — MA5/8/...
    /// gibi indikatör overlay'lerini HESAPLAMAZ (sadece bundle'da zaten var olan seriler doldurulur).
    /// Pencere kapanana dek bloklar.
    /// </summary>
    /// <param name="bundlePath">.npz bundle dosyasının yolu.</param>
    /// <param name="viewPath">.view.json dosyasının yolu (opsiyonel, şu an kullanılmıyor).</param>
    public void PlotBundleFileFromDisk(string bundlePath, string? viewPath = null)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(bundlePath))
            throw new ArgumentException("bundlePath boş olamaz.", nameof(bundlePath));
        if (!File.Exists(bundlePath))
            throw new FileNotFoundException($"Bundle dosyası bulunamadı: {bundlePath}", bundlePath);

        _logger?.WriteRaw($"  [Plot] Bundle okunuyor (disk/numpy.load): {bundlePath}");

        using (Py.GIL())
        {
            dynamic bundleLoader = Py.Import("bundle_loader");
            dynamic tradeData = bundleLoader.build_trade_data_from_bundle(
                new PyString(bundlePath),
                viewPath != null ? new PyString(viewPath) : null);

            _logger?.WriteRaw($"  [Plot] Bundle verisi hazır. Pencere açılıyor...");
            CallPlotDataImgBundleNew(tradeData);
            _logger?.WriteRaw($"  [Plot] Pencere kapandı.");
        }
    }

    /// <summary>
    /// N farklı bundle'ı (playlist — bkz. docs/todo.md "Offline Replay") TEK pencerede,
    /// <see cref="PlotMultipleTraderData"/>'nın kullandığı aynı multi-trader render yoluyla
    /// (main+child overlay) çizdirir. Disk'e ara bir "combined" dosya yazmadan, her bundle'dan
    /// bağımsız bir tradeData PyDict kurup doğrudan <c>CallPlotMultipleTraderData</c>'ya verir —
    /// gerçek bir <see cref="MultipleTrader"/> nesnesine ihtiyaç YOK (önceki inceleme:
    /// <see cref="PlotMultipleTraderData"/> zaten sadece OHLC+Lists kullanıyordu, bu metod o
    /// bulgunun doğal sonucu). Pencere kapanana dek bloklar.
    /// </summary>
    /// <param name="bundlePaths">.npz bundle dosyalarının yolları (playlist sırasıyla).</param>
    public void PlotBundlePlaylist(IEnumerable<string> bundlePaths)
    {
        EnsureInitialized();

        var paths = bundlePaths?.ToList() ?? new List<string>();
        if (paths.Count == 0)
            throw new ArgumentException("bundlePaths boş olamaz.", nameof(bundlePaths));

        _logger?.WriteRaw($"  [Plot] Playlist okunuyor ({paths.Count} bundle, memory/NpzReader)...");

        using (Py.GIL())
        {
            var pyTraderList = new PyList();
            foreach (var bundlePath in paths)
            {
                if (!File.Exists(bundlePath))
                {
                    _logger?.WriteRaw($"  [Plot] [ATLANDI] Bundle bulunamadı: {bundlePath}");
                    continue;
                }

                var reader = new NpzReader(bundlePath);
                ExtractBundleData(reader);
                dynamic tradeData = BuildPyTradeData();
                pyTraderList.Append(tradeData);
            }

            _logger?.WriteRaw($"  [Plot] Playlist verisi hazır. Pencere açılıyor...");
            CallPlotMultipleTraderData(pyTraderList);
            _logger?.WriteRaw($"  [Plot] Pencere kapandı.");
        }
    }

    /// <summary>
    /// SingleTrader sonuçlarını .npz/.view.json bundle çiftine yazar — DearPyGuiDataPlotter'ın
    /// da okuyabildiği aynı format. Yazma mantığını burada TEKRARLAMAZ, mevcut
    /// <see cref="TradeDataBundleConverter"/>'a ince bir sarmalayıcıdır; eski tip plotter'ın da
    /// artık bundle üretebilmesi (sonradan <see cref="PlotBundleFile"/>/
    /// <see cref="PlotBundleFileFromDisk"/> ile tekrar okunabilmesi) için eklendi.
    /// </summary>
    /// <param name="trader">Finalize() çağrılmış SingleTrader instance'ı.</param>
    /// <param name="outputDir">.npz/.view.json'ın yazılacağı klasör.</param>
    /// <param name="fileBaseName">Uzantısız dosya adı (varsayılan: "latest_bundle").</param>
    /// <returns>Yazılan .npz ve .view.json dosyalarının tam yolları.</returns>
    public (string bundlePath, string viewPath) SaveBundleToDisk(SingleTrader trader,
        string outputDir, string fileBaseName = "latest_bundle")
    {
        var converter = new TradeDataBundleConverter();
        var (bundlePath, viewPath) = converter.ConvertSingleTrader(trader, outputDir, fileBaseName);
        _logger?.WriteRaw($"  [Plot] Bundle yazıldı: {bundlePath}");
        return (bundlePath, viewPath);
    }

    /// <summary>MultipleTrader sonuçları için <see cref="SaveBundleToDisk(SingleTrader, string, string)"/> ile aynı işi yapar.</summary>
    public (string bundlePath, string viewPath) SaveBundleToDisk(MultipleTrader multipleTrader,
        string outputDir, string fileBaseName = "latest_bundle")
    {
        var converter = new TradeDataBundleConverter();
        var (bundlePath, viewPath) = converter.ConvertMultipleTrader(multipleTrader, outputDir, fileBaseName);
        _logger?.WriteRaw($"  [Plot] Bundle yazıldı: {bundlePath}");
        return (bundlePath, viewPath);
    }

    /// <summary>
    /// <see cref="ExtractTraderData"/>'nın bundle-tabanlı eşdeğeri: canlı bir SingleTrader yerine
    /// bir .npz bundle'dan (<see cref="NpzReader"/> ile) okunan dizilerle aynı private field seti
    /// doldurulur — sonrasında <see cref="BuildPyTradeData"/>/<see cref="CallPlotDataImgBundleNew"/>
    /// hiç değişmeden reuse edilir.
    /// </summary>
    private void ExtractBundleData(NpzReader reader)
    {
        var timestamps = reader.ReadStringArray("timestamps");
        int n = timestamps.Length;

        _dateTimes = new List<DateTime>(n);
        _dates     = new List<DateTime>(n);
        _times     = new List<TimeSpan>(n);
        foreach (var ts in timestamps)
        {
            var dt = DateTime.Parse(ts, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind);
            _dateTimes.Add(dt);
            _dates.Add(dt.Date);
            _times.Add(dt.TimeOfDay);
        }

        _opens   = reader.ReadDoubleArray("open").ToList();
        _highs   = reader.ReadDoubleArray("high").ToList();
        _lows    = reader.ReadDoubleArray("low").ToList();
        _closes  = reader.ReadDoubleArray("close").ToList();
        _volumes = reader.ReadDoubleArray("volume").Select(v => (long)Math.Round(v)).ToList();
        _lots    = reader.ReadLongArray("size").ToList();

        _sinyalList = reader.ReadLongArray("signal_steps").Select(v => (double)v).ToList();

        // indicator_names/indicator_values: TradeDataBundleConverter'ın yazdığı isimlendirilmiş
        // seriler (PnL/PnL %/Return/Net Return/Return %/Net Return %/Balance/Commission/Net Balance
        // + varsa strateji indikatörleri). Bilinen 9 isim kendi alanına, geri kalanı
        // strategy_indicators'a gider. Bundle bu isimlerden birini içermiyorsa (örn. eski, bu alanlar
        // eklenmeden ÖNCE üretilmiş bir bundle) o alan boş kalır — Clear() bunu garanti ediyor.
        _karZararFiyatList.Clear();
        _karZararFiyatYuzdeList.Clear();
        _getiriFiyatList.Clear();
        _getiriFiyatNetList.Clear();
        _getiriFiyatYuzdeList.Clear();
        _getiriFiyatNetYuzdeList.Clear();
        _bakiyeFiyatList.Clear();
        _komisyonFiyatList.Clear();
        _bakiyeFiyatNetList.Clear();

        var strategyIndicators = new Dictionary<string, double[]>();
        if (reader.Contains("indicator_names") && reader.Contains("indicator_values"))
        {
            var names = reader.ReadStringArray("indicator_names");
            var matrix = reader.ReadDouble2DArray("indicator_values");

            for (int r = 0; r < names.Length; r++)
            {
                var row = new double[n];
                for (int c = 0; c < n; c++) row[c] = matrix[r, c];

                switch (names[r])
                {
                    case "PnL":          _karZararFiyatList       = row.ToList(); break;
                    case "PnL %":        _karZararFiyatYuzdeList  = row.ToList(); break;
                    case "Return":       _getiriFiyatList         = row.ToList(); break;
                    case "Net Return":   _getiriFiyatNetList      = row.ToList(); break;
                    case "Return %":     _getiriFiyatYuzdeList    = row.ToList(); break;
                    case "Net Return %": _getiriFiyatNetYuzdeList = row.ToList(); break;
                    case "Balance":      _bakiyeFiyatList         = row.ToList(); break;
                    case "Commission":   _komisyonFiyatList       = row.ToList(); break;
                    case "Net Balance":  _bakiyeFiyatNetList      = row.ToList(); break;
                    default:             strategyIndicators[names[r]] = row; break;
                }
            }
        }
        _strategyIndicators = strategyIndicators;

        string title = "AlgoTrade";
        string periyot = "1H";
        if (reader.Contains("meta_json"))
        {
            try
            {
                var meta = Newtonsoft.Json.Linq.JObject.Parse(reader.ReadScalarString("meta_json"));
                title   = meta.Value<string>("symbol")  ?? title;
                periyot = meta.Value<string>("periyot") ?? periyot;
            }
            catch (Exception ex)
            {
                _logger?.WriteRaw($"  [Plot] meta_json parse edilemedi: {ex.Message}");
            }
        }
        _title   = title;
        _periyot = periyot;
    }

    #endregion

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



