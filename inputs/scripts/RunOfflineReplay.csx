// =============================================================================
// Offline Replay pipeline'i - sirali:
//   1) GenerateReplaySampleBundles.csx - N stratejiyi arka planda calistirip
//      outputs/logs/replay_samples/<Strateji>/bundle.npz uretir (sadece test verisi).
//   2) (elle adim) outputs/logs/replay_samples/* -> inputs/python/offlineReplay/samples/'e
//      elle kopyalanir (bilerek otomatik degil - tekrar deneme kalici veriyi bozmasin diye).
//   3) inputs/python/offlineReplay/playlist.json - hangi bundle'lar, hangi etiket/renkle
//      overlay edilecek (elle duzenlenebilir config, script degil).
//   4) MergeOfflineReplayPlaylist.csx - playlist.json'i okuyup N bundle'i combined.npz +
//      combined.view.json + input.json'a birlestirir (uretir, hicbir sey cizmez).
//   5) EditOfflineReplay.csx (opsiyonel) - combined.npz'i okuyup ozel bir .npz + .view.json +
//      guncel input.json uretir (istediginiz alt kume/duzende, hatta HESAPLANMIS/DONUSTURULMUS
//      veriyle panel kurmak icin - uretir, cizmez).
//   6) RunOfflineReplay.csx (BU SCRIPT) - input.json'i okuyup hem yeni tip (DearPyGuiDataPlotter)
//      hem eski tip (PythonPlotter) plotter'i acar, hepsini overlay gosterir (sadece cizer,
//      hicbir sey uretmez/birlestirmez). 5. adim atlanirsa MergeOfflineReplayPlaylist.csx'in
//      urettigi varsayilan (tum N trader'i tek panelde gosteren) combined.npz/view'i cizer.
// =============================================================================
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Python;
using AlgoTrade.Core.Python.DearPyGuiDataPlotter;

Log("=== RunOfflineReplay ===");

string playlistPath = Path.Combine(AppSettings.OfflineReplayDir, "playlist.json");
if (!File.Exists(playlistPath))
{
    Log($"[HATA] Playlist bulunamadi: {playlistPath}");
    return;
}

List<PlaylistEntry> entries;
try
{
    entries = OfflineReplayPlaylist.Load(playlistPath, AppSettings.RootDir);
}
catch (Exception ex)
{
    Log($"[HATA] Playlist okunamadi: {ex.Message}");
    return;
}

Log($"Playlist: {entries.Count} girdi ({string.Join(", ", entries.Select(e => e.Label))})");

var missing = entries.Where(e => !File.Exists(e.BundlePath)).ToList();
if (missing.Count > 0)
{
    foreach (var m in missing)
        Log($"[HATA] Bundle bulunamadi: {m.Label} -> {m.BundlePath}");
    Log("Once GenerateReplaySampleBundles.csx calistirip eksik bundle'lari uretin.");
    return;
}

// =============================================================================
// 1. Yeni tip plotter: ONCEDEN uretilmis "combined" bundle'i LoadBundle ile ac
// =============================================================================
Log("\n--- Yeni tip plotter (DearPyGuiDataPlotter) ---");
string inputJsonPath = Path.Combine(AppSettings.OfflineReplayDir, "input.json");

if (!File.Exists(inputJsonPath))
{
    Log($"[HATA] input.json bulunamadi: {inputJsonPath}");
    Log("Once MergeOfflineReplayPlaylist.csx calistirin.");
}
else
try
{
    // Ne yuklenecegi input.json'dan okunuyor (hardcoded "combined.npz" DEGIL) - boylece
    // input.json gercekten "kaynak" oluyor, MergeOfflineReplayPlaylist.csx'in yazdigi
    // pointer'i takip ediyoruz.
    var (combinedBundlePath, combinedViewPath) = OfflineReplayPlaylist.LoadInputJson(
        inputJsonPath, AppSettings.RootDir);
    Log($"input.json okundu: bundle={combinedBundlePath}");

    var dearPyGuiPlotter = new DearPyGuiDataPlotter();
    dearPyGuiPlotter.SetLogger(LogManager.GetInstance());
    dearPyGuiPlotter.StartPlotter();
    dearPyGuiPlotter.LoadBundle(combinedBundlePath, combinedViewPath);
    Log("load_bundle komutu gonderildi (yeni tip plotter arka planda acik kaliyor).");
}
catch (Exception ex)
{
    Log($"[HATA] Yeni tip plotter: {ex.Message}");
}

// =============================================================================
// 2. Eski tip plotter: N bundle'i dogrudan bellekte okuyup ayni pencerede cizdir
// =============================================================================
Log("\n--- Eski tip plotter (PythonPlotter) ---");
try
{
    var plotter = new PythonPlotter();
    plotter.SetLogger(LogManager.GetInstance());

    string? pythonDll = AppSettings.ResolvePythonDll();
    if (string.IsNullOrEmpty(pythonDll))
    {
        Log("[HATA] Python DLL bulunamadi. Proje kokunde setupPythonEnvs.bat calistirip .venv kurun.");
    }
    else
    {
        plotter.PythonDll = pythonDll;
        Log("Python engine baslatiliyor...");
        plotter.Initialize();
        Log("✓ Python engine hazir.");

        Log("Playlist'ten cizim yapiliyor - pencere kapanana dek bekleniyor...");
        await Task.Run(() => plotter.PlotBundlePlaylist(entries.Select(e => e.BundlePath)));
    }
}
catch (Exception ex)
{
    Log($"[HATA] Eski tip plotter: {ex.Message}");
}

Log("\n=== Bitti ===");
