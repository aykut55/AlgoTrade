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
// trader[i]'leri istediginiz panele, istediginiz sirada ekleyin. Hardcoded secim, dongu,
// veri uzerinde hesaplama/donusturme (orn. trader[i].Signal.Select(v => v * 2).ToArray())
// - hepsi serbest, bu tamamen C# kodu. AddSeries ile HESAPLANMIS bir diziyi de
// ekleyebilirsiniz - script sonunda YENI bir .npz'ye o hesaplanmis veri de yazilacak.
//
// Ornek: sadece trader[0], trader[5], trader[8], trader[2]'yi TEK panelde (olduğu gibi) gormek:
//
//   var panel1 = new ViewPanelBuilder("panel1", "Panel 1", height: 300);
//   panel1.AddSignal(trader[0]).AddSignal(trader[5]).AddSignal(trader[8]).AddSignal(trader[2]);
//   var customPanels = new List<ViewPanelBuilder> { panel1 };
//
// Ornek: 10 trader'i 2'serli 5 panelde gormek:
//
//   var customPanels = new List<ViewPanelBuilder>();
//   for (int i = 0; i < trader.Count; i += 2)
//   {
//       var p = new ViewPanelBuilder($"panel{i / 2}", $"Panel {i / 2 + 1}", height: 220);
//       p.AddSignal(trader[i]);
//       if (i + 1 < trader.Count) p.AddSignal(trader[i + 1]);
//       customPanels.Add(p);
//   }
//
// Ornek: trader[0]'in sinyalini 2 ile carpip ozel bir seri olarak eklemek:
//
//   var panel2 = new ViewPanelBuilder("panel2", "Carpilmis Sinyal");
//   var carpilmis = trader[0].Signal.Select(v => v * 2).ToArray();
//   panel2.AddSeries("MOST Signal x2", "MOST x2", trader[0].Color, carpilmis);
//
// OHLC panelindeki AL/SAT isaretleri (asagidaki ohlcSignal/includeOhlcSignal degiskenleri):
// varsayilan (ohlcSignal=null, includeOhlcSignal=true) combined.npz'deki (playlist'teki ILK
// entry'nin sinyali) ile AYNI kalir. Degistirmek isterseniz:
//   ohlcSignal = trader[3].Signal;          // OHLC'de trader[3]'un sinyalini goster
//   includeOhlcSignal = false;              // OHLC'de HICBIR AL/SAT gostermeyi (duz mum grafigi)
// =============================================================================

var panel1 = new ViewPanelBuilder("panel1", "Panel 1", height: 300);
panel1.AddSignal(trader[0]).AddSignal(trader[5]).AddSignal(trader[8]).AddSignal(trader[2]);

var customPanels = new List<ViewPanelBuilder> { panel1 };

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
