// =============================================================================
// 05_RunDearPyGuiDataPlotterTest.csx - DearPyGuiDataPlotter (Start/Stop Test) script hali
// Ana menudeki [9] DearPyGuiDataPlotter (Start/Stop Test) ile ayni akisi yapar:
// process baslat -> test bundle yukle -> bir sure bekle (ESC ile erken durdurulabilir) ->
// process durdur. Interaktif ReadMenuInput() yerine bekleme + iptal kontrolu kullanilir,
// cunku script'ler Program.cs'in konsol input fonksiyonlarina erisemiyor.
// Bundle yoksa once 04_GenerateDearPyGuiDataPlotterBundle.csx calistirin.
// =============================================================================
using System;
using System.IO;
using System.Threading.Tasks;
using AlgoTrade.Core.Python.DearPyGuiDataPlotter;

Log("=== DearPyGuiDataPlotter (Start/Stop Test) ===");

var plotter = new DearPyGuiDataPlotter();

try
{
    Log("Process baslatiliyor...");
    plotter.StartPlotter();
    Log($"Baslatildi. IsRunning={plotter.IsRunning}");
}
catch (Exception ex)
{
    Log($"[HATA] Baslatma hatasi: {ex.Message}");
    return;
}

// Test amacli ornek bundle: src/DearPyGuiDataPlotter/inputs/latest_bundle.npz
// (eski "full_pipeline_bundle.npz" adi artik uretilmiyor - .gitignore'da bile listeli;
//  gercekte var olan/commit edilen dosya latest_bundle.npz - bkz. inputs/input.json)
string testBundlePath = Path.Combine(plotter.ProjectDir, "inputs", "latest_bundle.npz");
string testViewPath   = Path.Combine(plotter.ProjectDir, "inputs", "latest_bundle.view.json");

if (File.Exists(testBundlePath))
{
    plotter.LoadBundle(testBundlePath, File.Exists(testViewPath) ? testViewPath : null);
    Log($"load_bundle komutu gonderildi: {Path.GetFileName(testBundlePath)}");
}
else
{
    Log($"Test bundle bulunamadi, load_bundle atlandi: {testBundlePath}");
}

// Pencereyi acik tutup gozlemlenebilmesi icin bekle - ESC ile erken sonlandirilabilir.
int waitSeconds = 10;
Log($"{waitSeconds} saniye bekleniyor (ESC ile erken durdurulabilir)...");
for (int s = 0; s < waitSeconds; s++)
{
    if (IsCancellationRequested)
    {
        Log("ESC ile iptal edildi, erken durduruluyor.");
        break;
    }
    await Task.Delay(1000);
}

plotter.ClearPanel(0);
Log("clear_panel komutu gonderildi: panelId=0");
await Task.Delay(500);

plotter.StopPlotter();
Log($"Process durduruldu. IsRunning={plotter.IsRunning}");

Log("=== Bitti ===");
