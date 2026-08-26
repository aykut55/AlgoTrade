// =============================================================================
// RunOfflineReplay.csx - "Offline Replay" ozelliginin ANA/asil script'i.
//
// AMAC: onceden (farkli zamanlarda, farkli stratejilerle) calistirilmis N adet SingleTrader
// run'inin sonucunu, trader'lari HIC YENIDEN CALISTIRMADAN, tek pencerede ust uste (overlay)
// gosterip karsilastirmak - hem eski tip (pythonnet/imgui_bundle) hem yeni tip
// (DearPyGuiDataPlotter) plotter'da. bkz. docs/todo.md "Yeni Ozellik Fikri: Gecmis (Offline)
// Trader Verilerinden Hizli Sinyal Plot'u" > Option C.
//
// TUM AKIS (bu script'i calistirmadan ONCE asagidaki adimlarin tamamlanmis olmasi lazim):
//
//   1) Ornek/gercek bundle'lari elde et.
//      - Test/gelistirme icin: GenerateReplaySampleBundles.csx calistir - Config_01'deki veriyi
//        N farkli stratejiyle (SingleTrader'i pencere/Python ACMADAN, arka planda) calistirip
//        her birini outputs/logs/replay_samples/<StrategyName>/bundle.npz olarak yazar.
//      - Gercek kullanimda: herhangi bir [5]/[6] run'inin urettigi bundle (bkz.
//        AppSettings.DearPyGuiPlotterBundleDir/PythonPlotterBundleDir) de kullanilabilir.
//
//   2) Bu bundle'lari (outputs/logs/replay_samples/... ELLE, kalici degil - bkz. asagidaki NOT)
//      inputs/python/offlineReplay/samples/<StrategyName>/bundle.npz altina KOPYALA. Bu, playlist
//      icin kullanilacak "kalici/gercek" konum - GenerateReplaySampleBundles.csx BILINCLI olarak
//      buraya YAZMIYOR (tekrar tekrar denerken playlist'in kullandigi veriyi bozmasin diye),
//      kopyalama islemi kullanicinin (senin) sorumlulugunda.
//
//   3) inputs/python/offlineReplay/playlist.json'i duzenle - hangi bundle'lar (samples/ altindaki),
//      hangi etiket/renkle overlay edilecek (bkz. dosyanin kendisi, format: entries: [{bundle,
//      label, color}, ...]). Zaten 10 ornek strateji ile dolu, degistirmek istersen elle duzenle.
//
//   4) MergeOfflineReplayPlaylist.csx calistir - playlist.json'daki N bundle'i OKUYUP, yeni tip
//      plotter'in dogrudan acabildigi TEK bir "combined" bundle'a birlestirir
//      (inputs/python/offlineReplay/combined.npz + combined.view.json).
//
//   5) BU SCRIPT (RunOfflineReplay.csx) - playlist.json + combined.npz'i (ikisi de ONCEDEN
//      hazir olmali, bu script UretMEZ/BirlestirMEZ) okuyup HEM yeni tip plotter'i
//      (LoadBundle ile combined.npz) HEM eski tip plotter'i (PlotBundlePlaylist ile playlist'teki
//      N bundle'i bellekte ayri ayri okuyup) acar - iki ayri pencere, ayni 10 stratejiyi
//      overlay gosteriyor olmali.
//
// KISACA SIRA: GenerateReplaySampleBundles.csx (uret) -> elle kopyala (samples/) ->
//              playlist.json (duzenle, opsiyonel) -> MergeOfflineReplayPlaylist.csx (birlestir) ->
//              RunOfflineReplay.csx (bu script - sadece CIZER, hicbir sey uretmez/birlestirmez).
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
