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
//   5) EditOfflineReplay.csx (BU SCRIPT, opsiyonel) - combined.npz'i OKUR (hic degistirmez),
//      her run'i "trader[]" dizisine cikarir, kullanicinin ELLE duzenledigi bolumde hangi
//      trader'in (olduğu gibi ya da HESAPLANMIS/DONUSTURULMUS haliyle) hangi panele/sirada
//      eklenecegine karar verilir. Sonucta YENI (combined ile CAKISMAYAN isimde) bir .npz +
//      .view.json ureti­lir - npz de yeniden yazilir (sadece view degil!) cunku kullanici
//      veri uzerinde islem yapmis olabilir, o zaman yeni seri combined.npz'de YOK, isimle
//      referans veren bir view onu bulamazdi. input.json hem bundle hem view alani icin
//      guncellenir. combined.npz/combined.view.json'a (MergeOfflineReplayPlaylist.csx'in
//      urettigi "tam/varsayilan" gorunum) HIC dokunulmaz.
//   6) RunOfflineReplay.csx - input.json'i okuyup hem yeni tip (DearPyGuiDataPlotter) hem
//      eski tip (PythonPlotter) plotter'i acar, EditOfflineReplay.csx'in urettigi ozel
//      gorunumu cizer (sadece cizer, hicbir sey uretmez).
// =============================================================================
// EditOfflineReplay.csx - combined.npz'deki TUM run'lari "trader[]" dizisine cikarir, siz
// hangisini (olduğu gibi ya da hesaplayarak) hangi panele ekleyeceginize asagidaki DUZENLE
// bolumunde karar verirsiniz, script sonunda YENI bir .npz + .view.json + guncellenmis
// input.json uretir. RunOfflineReplay.csx bunu bir sonraki calistirmasinda otomatik cizer.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Python.DearPyGuiDataPlotter;

Log("=== EditOfflineReplay ===");

// =============================================================================
// 1. playlist.json + combined.npz'i oku -> trader[] dizisini kur
// =============================================================================
string playlistPath  = Path.Combine(AppSettings.OfflineReplayDir, "playlist.json");
string inputJsonPath = Path.Combine(AppSettings.OfflineReplayDir, "input.json");

if (!File.Exists(playlistPath)) { Log($"[HATA] Playlist bulunamadi: {playlistPath}"); return; }
if (!File.Exists(inputJsonPath)) { Log($"[HATA] input.json bulunamadi: {inputJsonPath}. Once MergeOfflineReplayPlaylist.csx calistirin."); return; }

var playlistEntries = OfflineReplayPlaylist.Load(playlistPath, AppSettings.RootDir);
var (combinedBundlePath, _) = OfflineReplayPlaylist.LoadInputJson(inputJsonPath, AppSettings.RootDir);

if (!File.Exists(combinedBundlePath))
{
    Log($"[HATA] combined.npz bulunamadi: {combinedBundlePath}. Once MergeOfflineReplayPlaylist.csx calistirin.");
    return;
}

// trader[0], trader[1], ... trader[N-1] - playlist.json'daki sirayla, her biri kendi
// Label/Color/Signal (double[])/PnL (double[]?, olmayabilir) alanlarina sahip.
var trader = OfflineReplayPlaylist.ReadSources(combinedBundlePath, playlistEntries);

Log($"{trader.Count} trader okundu:");
for (int i = 0; i < trader.Count; i++)
    Log($"  trader[{i}] = \"{trader[i].Label}\" (Signal={trader[i].Signal.Length} bar, PnL={(trader[i].PnL != null ? "var" : "yok")})");

// =============================================================================
// 2. ------------------------- BURADAN ASAGISINI DUZENLEYIN -------------------------
// choice degerini 0-5 arasinda secin - her deger asagida ayri bir panel duzeni kurar
// (BuildChoiceN local fonksiyonlari). Kendi duzeninizi eklemek/degistirmek isterseniz
// ilgili BuildChoiceN fonksiyonunun icini duzenleyin - hepsi ayni trader[] dizisinden
// besleniyor, AddSignal/AddPnL/AddSeries hepsi serbest (AddSeries ile HESAPLANMIS bir
// diziyi de ekleyebilirsiniz, orn. trader[i].Signal.Select(v => v * 2).ToArray()).
//
//   0 = varsayilan   : sinyaller TEK panelde + PnL'ler TEK panelde (tum trader'lar)
//   1 = splitSignals : sinyaller 2-3'erli gruplarla ayri panellere bolunur (PnL yok)
//   2 = splitPnL     : PnL'ler 2-3'erli gruplarla ayri panellere bolunur (Signal yok)
//   3 = mixed        : 2 sinyal paneli + 2 PnL paneli, secili bir alt kume ile (10 yerine)
//   4 = (henuz tanimlanmadi - TODO, simdilik 0'a duser)
//   5 = (henuz tanimlanmadi - TODO, simdilik 0'a duser)
//
// OHLC panelindeki AL/SAT isaretleri (asagidaki ohlcSignal/includeOhlcSignal degiskenleri):
// varsayilan (ohlcSignal=null, includeOhlcSignal=true) combined.npz'deki (playlist'teki ILK
// entry'nin sinyali ya da MergeOfflineReplayPlaylist.csx'teki useMajorityConsensusSignal'a
// gore bileske sinyal) ile AYNI kalir. Degistirmek isterseniz:
//   ohlcSignal = trader[3].Signal;          // OHLC'de trader[3]'un sinyalini goster
//   includeOhlcSignal = false;              // OHLC'de HICBIR AL/SAT gostermeyi (duz mum grafigi)
// =============================================================================

int choice = 0;

List<ViewPanelBuilder> BuildChoice0()
{
    var signalsPanel = new ViewPanelBuilder("signals", "Signals", height: 260);
    var pnlPanel = new ViewPanelBuilder("pnl", "PnL", height: 260);
    foreach (var t in trader)
    {
        signalsPanel.AddSignal(t);
        pnlPanel.AddPnL(t);
    }
    return new List<ViewPanelBuilder> { signalsPanel, pnlPanel };
}

List<ViewPanelBuilder> BuildChoice1()
{
    var panels = new List<ViewPanelBuilder>();
    const int perPanel = 3;
    for (int i = 0; i < trader.Count; i += perPanel)
    {
        var p = new ViewPanelBuilder($"signals{i / perPanel}", $"Signals {i / perPanel + 1}", height: 200);
        for (int j = i; j < Math.Min(i + perPanel, trader.Count); j++)
            p.AddSignal(trader[j]);
        panels.Add(p);
    }
    return panels;
}

List<ViewPanelBuilder> BuildChoice2()
{
    var panels = new List<ViewPanelBuilder>();
    const int perPanel = 3;
    for (int i = 0; i < trader.Count; i += perPanel)
    {
        var p = new ViewPanelBuilder($"pnl{i / perPanel}", $"PnL {i / perPanel + 1}", height: 200);
        for (int j = i; j < Math.Min(i + perPanel, trader.Count); j++)
            p.AddPnL(trader[j]);
        panels.Add(p);
    }
    return panels;
}

List<ViewPanelBuilder> BuildChoice3()
{
    // 10 yerine secili 4 trader, 2'serli 2 sinyal + 2 PnL paneli
    var selected = new[] { trader[0], trader[5], trader[8], trader[2] };

    var sig1 = new ViewPanelBuilder("signals1", "Signals 1", height: 200);
    var sig2 = new ViewPanelBuilder("signals2", "Signals 2", height: 200);
    sig1.AddSignal(selected[0]).AddSignal(selected[1]);
    sig2.AddSignal(selected[2]).AddSignal(selected[3]);

    var pnl1 = new ViewPanelBuilder("pnl1", "PnL 1", height: 200);
    var pnl2 = new ViewPanelBuilder("pnl2", "PnL 2", height: 200);
    pnl1.AddPnL(selected[0]).AddPnL(selected[1]);
    pnl2.AddPnL(selected[2]).AddPnL(selected[3]);

    return new List<ViewPanelBuilder> { sig1, sig2, pnl1, pnl2 };
}

List<ViewPanelBuilder> customPanels = choice switch
{
    0 => BuildChoice0(),
    1 => BuildChoice1(),
    2 => BuildChoice2(),
    3 => BuildChoice3(),
    4 => BuildChoice0(), // TODO: henuz tanimlanmadi
    5 => BuildChoice0(), // TODO: henuz tanimlanmadi
    _ => BuildChoice0(),
};

double[]? ohlcSignal = null;      // null = varsayilan (combined.npz'deki ile ayni) kalsin
bool includeOhlcSignal = true;    // false = OHLC'de hic AL/SAT gosterme

// =============================================================================
// ------------------------- DUZENLEME BURADA BITTI -----------------------------
// =============================================================================

// =============================================================================
// 3. Yeni .npz + view.json'i uret + input.json'i buna isaret edecek sekilde guncelle
//    (combined.npz/combined.view.json'a HIC dokunulmuyor)
// =============================================================================
string fileBaseName = "edited"; // istersen degistirin - sadece "combined" OLAMAZ

string customBundlePath, customViewPath;
try
{
    (customBundlePath, customViewPath) = OfflineReplayPlaylist.WriteCustomBundle(
        combinedBundlePath, customPanels, AppSettings.OfflineReplayDir, fileBaseName,
        ohlcSignal, includeOhlcSignal);
}
catch (Exception ex)
{
    Log($"[HATA] Ozel bundle/view uretilemedi: {ex.Message}");
    return;
}
Log($"Ozel bundle yazildi: {customBundlePath}");
Log($"Ozel view yazildi  : {customViewPath}");

OfflineReplayPlaylist.WriteInputJson(inputJsonPath,
    $"inputs/python/offlineReplay/{fileBaseName}.npz",
    $"inputs/python/offlineReplay/{fileBaseName}.view.json");
Log($"input.json guncellendi (hem bundle hem view): {inputJsonPath}");

Log("\n=== Bitti - RunOfflineReplay.csx ile cizdirebilirsiniz ===");
