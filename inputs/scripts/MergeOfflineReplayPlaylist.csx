// =============================================================================
// Offline Replay pipeline'i - sirali:
//   1) GenerateReplaySampleBundles.csx - N stratejiyi arka planda calistirip
//      outputs/logs/replay_samples/<Strateji>/bundle.npz uretir (sadece test verisi).
//   2) (elle adim) outputs/logs/replay_samples/* -> inputs/python/offlineReplay/samples/'e
//      elle kopyalanir (bilerek otomatik degil - tekrar deneme kalici veriyi bozmasin diye).
//   3) inputs/python/offlineReplay/playlist.json - hangi bundle'lar, hangi etiket/renkle
//      overlay edilecek (elle duzenlenebilir config, script degil).
//   4) MergeOfflineReplayPlaylist.csx (BU SCRIPT) - playlist.json'i okuyup N bundle'i
//      combined.npz + combined.view.json + input.json'a birlestirir (uretir, hicbir sey cizmez).
//   5) EditOfflineReplay.csx (opsiyonel) - combined.npz'i okuyup ozel bir .npz + .view.json +
//      guncel input.json uretir (istediginiz alt kume/duzende, hatta HESAPLANMIS/DONUSTURULMUS
//      veriyle panel kurmak icin - uretir, cizmez).
//   6) RunOfflineReplay.csx - input.json'i okuyup hem yeni tip (DearPyGuiDataPlotter) hem
//      eski tip (PythonPlotter) plotter'i acar, hepsini overlay gosterir (sadece cizer,
//      hicbir sey uretmez/birlestirmez). 5. adim atlanirsa bu script'in urettigi varsayilan
//      (tum N trader'i tek panelde gosteren) combined.npz/view'i cizer.
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

// OHLC panelindeki AL/SAT isaretleri icin: true = tum entry'lerin bar-bar COGUNLUK OYUNDAN
// bileske sinyal (varsayilan), false = playlist'teki ILK entry'nin sinyali.
bool useMajorityConsensusSignal = true;

try
{
    var (bundlePath, viewPath) = OfflineReplayPlaylist.MergeToBundle(
        entries, AppSettings.OfflineReplayDir, fileBaseName: "combined",
        useMajorityConsensusSignal: useMajorityConsensusSignal);
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
