using Newtonsoft.Json;
using Python.Runtime;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;

namespace AlgoTrade.Core.Python;

/// <summary>
/// pythonnet üzerinden Python tabanlı görselleştirme işlemlerini yönetir.
/// inputs/python/ klasöründeki Python script'lerini çağırır.
/// </summary>
public class PythonPlotter : IDisposable
{
    #region Properties

    /// <summary>Python runtime başlatıldı mı?</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Python DLL yolu.
    /// Boş bırakılırsa sistem PATH'inden çözümlenir (örn. "python" komutuyla bulunan Python).
    /// Explicit olarak verilmek istenirse: "python312.dll" veya tam yol.
    /// </summary>
    public string PythonDll { get; set; } = "";

    /// <summary>
    /// Python script'lerinin bulunduğu klasör.
    /// sys.path'e eklenir; buraya konan .py dosyaları import edilebilir.
    /// Varsayılan: AppSettings.PythonScriptsDir (inputs/python/).
    /// </summary>
    public string PythonScriptsDir { get; set; } = AppSettings.PythonScriptsDir;

    // PythonEngine process başına tek seferlik başlatılır/kapatılır.
    private static bool          _engineStarted = false;
    private static readonly object _engineLock  = new();

    private bool _disposed;

    #endregion

    #region Constructor

    public PythonPlotter() { }

    /// <param name="pythonDll">Python DLL yolu (örn. "python312.dll" veya tam path).</param>
    public PythonPlotter(string pythonDll)
    {
        PythonDll = pythonDll;
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Python engine'i başlatır.
    /// Aynı process içinde birden fazla çağrılsa da yalnızca ilk çağrı etkilidir.
    /// </summary>
    public void Initialize()
    {
        lock (_engineLock)
        {
            if (_engineStarted)
            {
                IsInitialized = true;
                return;
            }

            if (!string.IsNullOrEmpty(PythonDll))
                Runtime.PythonDLL = PythonDll;

            PythonEngine.Initialize();
            // Initialize() sonrası GIL bu thread'de — sys.path'i şimdi ekle (bir kez yeterli)
            dynamic sys = Py.Import("sys");
            sys.path.insert(0, new PyString(PythonScriptsDir));
            PythonEngine.BeginAllowThreads(); // GIL'i serbest bırak → Task.Run thread'i alabilsin
            _engineStarted = true;
        }

        IsInitialized = true;
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

        Lists  lists  = trader.lists  ?? throw new ArgumentException("trader.lists is null",  nameof(trader));
        Status status = trader.status ?? throw new ArgumentException("trader.status is null", nameof(trader));

        double winRate = status.IslemSayisi > 0
            ? (double)status.KazandiranIslemSayisi / status.IslemSayisi * 100.0
            : 0.0;

        var payload = new
        {
            symbol_name       = trader.SymbolName,
            symbol_period     = trader.SymbolPeriod,
            strategy_name     = trader.StrategyName,
            bar_count         = lists.BarCount,
            // Equity curves — bar bazında
            equity_gross      = lists.BakiyeFiyatList,
            equity_net        = lists.BakiyeFiyatNetList,
            getiri_net        = lists.GetiriFiyatNetList,
            // Özet istatistikler
            ilk_bakiye        = status.IlkBakiyeFiyat,
            net_profit        = status.GetiriFiyatNet,
            net_profit_yuzde  = status.GetiriFiyatYuzdeNet,
            islem_sayisi      = status.IslemSayisi,
            kazanilan_islem   = status.KazandiranIslemSayisi,
            kaybedilen_islem  = status.KaybettirenIslemSayisi,
            win_rate          = winRate,
            komisyon          = status.KomisyonFiyat,
        };

        string jsonStr = JsonConvert.SerializeObject(payload);

        using var gil = Py.GIL();

        dynamic json_module = Py.Import("json");
        dynamic pyData      = json_module.loads(jsonStr);

        dynamic plotter = Py.Import("plotter");
        plotter.show_single_trader_data(pyData);
    }

    #endregion

    #region Private

    private void EnsureInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException(
                "PythonPlotter başlatılmadı. Önce Initialize() çağrın.");
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        // PythonEngine.Shutdown() global/static state olduğu için Dispose içinde çağrılmaz.
        // Uygulama sonunda explicit olarak PythonPlotter.Shutdown() çağrılmalı.
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
