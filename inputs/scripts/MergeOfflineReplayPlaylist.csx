// =============================================================================
// MergeOfflineReplayPlaylist.csx - "Offline Replay" ozelligi (bkz. docs/todo.md "Yeni Ozellik
// Fikri: Gecmis (Offline) Trader Verilerinden Hizli Sinyal Plot'u" > Option C).
// inputs/python/offlineReplay/playlist.json'daki N bundle'i (ayrik strateji run'lari) OKUYUP,
// yeni tip plotter'in (DearPyGuiDataPlotter) dogrudan acabildigi TEK bir "combined" bundle'a
// (inputs/python/offlineReplay/combined.npz + .view.json) birlestirir.
//
// SADECE URETIR - hicbir sey CIZMEZ/ACMAZ (Python engine'i hic baslatmaz). Cizim
// RunOfflineReplay.csx'in isi - bu ayrim GenerateReplaySampleBundles.csx (uret) /
// RunOfflineReplay.csx (tuket+ciz) ayrimiyla ayni felsefede.
//
// Onceden GenerateReplaySampleBundles.csx ile ornek bundle'lar uretilmis olmali.
// =============================================================================
using System;
using System.IO;
using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Python.DearPyGuiDataPlotter;

Log("=== MergeOfflineReplayPlaylist ===");

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

try
{
    var (bundlePath, viewPath) = OfflineReplayPlaylist.MergeToBundle(
        entries, AppSettings.OfflineReplayDir, fileBaseName: "combined");
    Log($"Combined bundle yazildi: {bundlePath}");
    Log($"Combined view yazildi  : {viewPath}");

    // input.json - pythonPlotter/ ve dearPyGuiDataPlotter/'daki ayni formatta (bkz.
    // src/DearPyGuiDataPlotter/docs/InputConfig.md), ROOT_DIR-relative path'lerle.
    // BURADA yaziliyor (RunOfflineReplay.csx'te DEGIL) cunku input.json'i GUNCELLEMEK de
    // "uretim" isinin bir parcasi - combined.npz/.view.json'i kim uretiyorsa pointer'i da o
    // guncellemeli, cizen script (RunOfflineReplay.csx) sadece OKUR/CIZER (bkz. LoadInputJson).
    string inputJsonPath = Path.Combine(AppSettings.OfflineReplayDir, "input.json");
    OfflineReplayPlaylist.WriteInputJson(inputJsonPath,
        "inputs/python/offlineReplay/combined.npz", "inputs/python/offlineReplay/combined.view.json");
    Log($"input.json yazildi     : {inputJsonPath}");
}
catch (Exception ex)
{
    Log($"[HATA] Merge basarisiz: {ex.Message}");
    return;
}

Log("\n=== Bitti - RunOfflineReplay.csx ile cizdirebilirsiniz ===");
