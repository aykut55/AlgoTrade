using System.Diagnostics;
using AlgoTrade.Core.Logging;
using Newtonsoft.Json;

namespace AlgoTrade.Core.Python.DearPyGuiDataPlotter;

/// <summary>
/// DearPyGuiDataPlotter (src/DearPyGuiDataPlotter) projesini ayrı bir Python process'i
/// olarak başlatıp görselleştirme yapmayı yönetir.
/// PythonPlotter'ın aksine pythonnet ile in-process çalışmaz; proje kökündeki
/// ortak .venv ile bağımsız bir process olarak çalıştırılır. Veri aktarımı ileride
/// npz bundle + view.json dosyaları ve dosya tabanlı runtime command'lar üzerinden yapılacak.
/// </summary>
public class DearPyGuiDataPlotter : IDisposable
{
    #region Properties

    /// <summary>DearPyGuiDataPlotter proje klasörü (main.py'nin bulunduğu dizin).</summary>
    public string ProjectDir { get; set; } = AppSettings.DearPyGuiDataPlotterDir;

    /// <summary>Proje kökündeki ortak venv'deki python.exe yolu.</summary>
    public string PythonExePath => Path.Combine(AppSettings.VenvDir, "Scripts", "python.exe");

    /// <summary>Çalıştırılacak giriş script'i.</summary>
    public string MainScriptPath => Path.Combine(ProjectDir, "main.py");

    /// <summary>Plotter process'i şu anda çalışıyor mu?</summary>
    public bool IsRunning => _process is { HasExited: false };

    /// <summary>
    /// Python tarafındaki RuntimeCommandManager'ın izlediği komut klasörü.
    /// bkz. src/plotting/runtimeCommandManager.py.
    /// </summary>
    public string CommandsDir => Path.Combine(ProjectDir, "inputs", "runtime_commands");

    private Process? _process;
    private LogManager? _logger;
    private bool _disposed;
    private int _commandSequence;

    #endregion

    #region Constructor

    public DearPyGuiDataPlotter() { }

    public void SetLogger(LogManager? logger) => _logger = logger;

    #endregion

    #region Process Lifecycle

    /// <summary>
    /// DearPyGuiDataPlotter'ı ayrı bir process olarak başlatır.
    /// Zaten çalışıyorsa hiçbir şey yapmaz.
    /// </summary>
    public void StartPlotter()
    {
        if (IsRunning) return;

        if (!File.Exists(PythonExePath))
            throw new FileNotFoundException($"DearPyGuiDataPlotter venv python.exe bulunamadı: {PythonExePath}");

        if (!File.Exists(MainScriptPath))
            throw new FileNotFoundException($"DearPyGuiDataPlotter main.py bulunamadı: {MainScriptPath}");

        var psi = new ProcessStartInfo
        {
            FileName = PythonExePath,
            Arguments = $"\"{MainScriptPath}\"",
            WorkingDirectory = ProjectDir,
            UseShellExecute = false,
        };

        _process = Process.Start(psi);
        _logger?.WriteRaw($"✓ DearPyGuiDataPlotter process started (PID {_process?.Id}).");
    }

    /// <summary>
    /// Plotter process'ini kapatır.
    /// Önce "shutdown" komutuyla nazik kapatma denenir (dpg.stop_dearpygui());
    /// process <paramref name="gracefulTimeoutMs"/> içinde kapanmazsa kill edilir.
    /// </summary>
    public void StopPlotter(int gracefulTimeoutMs = 3000)
    {
        if (_process == null) return;

        try
        {
            if (!_process.HasExited)
            {
                Shutdown();
                if (!_process.WaitForExit(gracefulTimeoutMs))
                {
                    _logger?.WriteRaw("[DearPyGuiDataPlotter] Nazik kapatma zaman aşımına uğradı, process kill ediliyor.");
                    _process.Kill(entireProcessTree: true);
                }
            }
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    #endregion

    #region Runtime Commands

    /// <summary>
    /// Çalışan DearPyGuiDataPlotter process'ine yeni bir bundle+view yükleyip
    /// mevcut panelleri yeniden kurması için "load_bundle" komutu gönderir.
    /// Python tarafı bunu inputs/runtime_commands/ altından her frame poll eder.
    /// </summary>
    /// <param name="bundlePath">.npz bundle dosyasının yolu (mutlak veya relative olabilir).</param>
    /// <param name="viewPath">.view.json dosyasının yolu (mutlak veya relative, opsiyonel).</param>
    /// <param name="blockUntilClosed">
    /// true ise, komut yazıldıktan sonra plotter process'i kapanana kadar (kullanıcı pencereyi
    /// kapatana ya da <see cref="Shutdown"/> komutu işlenene kadar) bu çağrı bloklar — eski tip
    /// pythonnet plotter'ın (<c>PythonPlotter.PlotSingleTraderData</c>/<c>PlotMultipleTraderData</c>)
    /// davranışını taklit eder. false (varsayılan) ise komut yazılır yazılmaz hemen döner; process
    /// arka planda çalışmaya devam eder ve üstüne <see cref="LoadBundle"/>/<see cref="ReloadCurrent"/>
    /// ile başka komutlar gönderilebilir (hot-reload akışı).
    /// </param>
    public void LoadBundle(string bundlePath, string? viewPath = null, bool blockUntilClosed = false)
    {
        if (string.IsNullOrEmpty(bundlePath))
            throw new ArgumentException("bundlePath boş olamaz.", nameof(bundlePath));

        var payload = new Dictionary<string, object?>
        {
            ["command"] = "load_bundle",
            ["bundlePath"] = ToInputsRelativePath(bundlePath),
        };
        if (!string.IsNullOrEmpty(viewPath))
            payload["viewPath"] = ToInputsRelativePath(viewPath);

        WriteCommand("load_bundle", payload);

        if (blockUntilClosed)
            WaitForExit();
    }

    /// <summary>
    /// Çağıran thread'i, plotter process'i kapanana kadar bloklar (kullanıcı pencereyi kapatana
    /// ya da <see cref="Shutdown"/> komutu işlenene kadar). Process hiç başlatılmadıysa
    /// (<see cref="StartPlotter"/> çağrılmadıysa) hiçbir şey yapmadan hemen döner.
    /// </summary>
    public void WaitForExit()
    {
        _process?.WaitForExit();
    }

    /// <summary>
    /// Yol proje (repo) kökü altındaysa ona göre relative hale getirir
    /// (örn. "src/DearPyGuiDataPlotter/inputs/latest_bundle.npz"; Python tarafı bunu
    /// aynı köke göre çözüyor, bkz. docs/InputConfig.md); değilse mutlak yolu olduğu
    /// gibi bırakır. Bu sayede runtimeCommandManager.py'nin üzerine yazdığı
    /// inputs/input.json makineler arasında (farklı proje kök yolları) taşınabilir kalır.
    /// </summary>
    private string ToInputsRelativePath(string path)
    {
        try
        {
            string rel = Path.GetRelativePath(AppSettings.RootDir, Path.GetFullPath(path))
                .Replace('\\', '/');
            return rel.StartsWith("..") ? path : rel;
        }
        catch
        {
            return path;
        }
    }

    /// <summary>
    /// Belirtilen paneldeki tüm data/level'ları temizletir ("clear_panel" komutu).
    /// </summary>
    /// <param name="panelId">Temizlenecek panelin id'si.</param>
    public void ClearPanel(int panelId)
    {
        WriteCommand("clear_panel", new Dictionary<string, object?>
        {
            ["command"] = "clear_panel",
            ["panelId"] = panelId,
        });
    }

    /// <summary>
    /// Tüm panellerdeki data/level'ları temizletir ("clear_all_panels" komutu).
    /// </summary>
    public void ClearAllPanels()
    {
        WriteCommand("clear_all_panels", new Dictionary<string, object?>
        {
            ["command"] = "clear_all_panels",
        });
    }

    /// <summary>
    /// inputs/input.json'daki bundle/view path'lerini değiştirmeden default.py'yi
    /// yeniden çalıştırır ("reload_current" komutu). Örn. C# aynı dosyaları
    /// disk üzerinde güncelleyip tekrar çizdirmek istediğinde kullanılır.
    /// </summary>
    public void ReloadCurrent()
    {
        WriteCommand("reload_current", new Dictionary<string, object?>
        {
            ["command"] = "reload_current",
        });
    }

    /// <summary>
    /// Son yüklenen bundle'daki hazır bir seriyi (indikatör/OHLCV/signalSteps)
    /// hedef panele ekletir ("add_series_from_bundle" komutu).
    /// </summary>
    /// <param name="panelId">Serinin ekleneceği panel.</param>
    /// <param name="source">
    /// "indicator" | "open" | "high" | "low" | "close" | "volume" | "size" | "signalSteps".
    /// </param>
    /// <param name="name">source="indicator" için bundle'daki indikatör adı; diğerlerinde opsiyonel etiket.</param>
    /// <param name="dataId">Panel içi veri id'si (verilmezse Python tarafı otomatik atar).</param>
    public void AddSeriesFromBundle(int panelId, string source, string? name = null, int? dataId = null)
    {
        if (string.IsNullOrEmpty(source))
            throw new ArgumentException("source boş olamaz.", nameof(source));

        var payload = new Dictionary<string, object?>
        {
            ["command"] = "add_series_from_bundle",
            ["panelId"] = panelId,
            ["source"] = source,
        };
        if (!string.IsNullOrEmpty(name))
            payload["name"] = name;
        if (dataId.HasValue)
            payload["dataId"] = dataId.Value;

        WriteCommand("add_series_from_bundle", payload);
    }

    /// <summary>
    /// DearPyGuiDataPlotter uygulamasını nazikçe kapatması için "shutdown" komutu
    /// gönderir (dpg.stop_dearpygui()). Process'in kapandığını beklemek için
    /// StopPlotter() kullanılmalı; bu metod sadece komutu yazar.
    /// </summary>
    public void Shutdown()
    {
        WriteCommand("shutdown", new Dictionary<string, object?>
        {
            ["command"] = "shutdown",
        });
    }

    /// <summary>
    /// Komutu dosya olarak yazar: önce ".tmp" olarak yazılır, sonra atomik
    /// rename ile ".json" yapılır. Python tarafı yalnızca ".json" dosyalarını
    /// okuduğu için yarım yazılmış dosya asla okunmaz.
    /// </summary>
    private void WriteCommand(string commandName, object payload)
    {
        Directory.CreateDirectory(CommandsDir);

        int sequence = System.Threading.Interlocked.Increment(ref _commandSequence);
        string fileName = $"{sequence:D6}_{commandName}.json";
        string finalPath = Path.Combine(CommandsDir, fileName);
        string tempPath = finalPath + ".tmp";

        string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, finalPath);

        _logger?.WriteRaw($"[DearPyGuiDataPlotter] Command yazıldı: {fileName}");
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
            StopPlotter();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
