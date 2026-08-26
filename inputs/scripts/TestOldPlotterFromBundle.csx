// =============================================================================
// TestOldPlotterFromBundle.csx - Eski tip plotter'i (pythonnet/imgui_bundle) canli SingleTrader
// yerine dogrudan .npz/.view.json bundle dosyasindan calistirir (PythonPlotter.PlotBundleFile).
// Amac: 01_RunSingleTraderWithProgressAsync.csx'in urettigi bundle'in eski tip plotter
// tarafindan da okunabildigini dogrulamak - bkz. docs/todo.md "Yeni Ozellik Fikri: Gecmis
// (Offline) Trader Verilerinden Hizli Sinyal Plot'u" > Option C.
// Bundle konumu: AppSettings.PythonPlotterBundleDir (inputs/python/pythonPlotter/) - eski tip
// plotter'in kendi AlgoTrade-native runtime klasoru (2026-08-26, bkz. docs/todo.md "Kalinti
// cift ROOT yapisi"). Onceden bir kez [5] (01_RunSingleTraderWithProgressAsync.csx) calistirip
// bundle'i uretmis olun.
// =============================================================================
using System;
using System.IO;
using System.Threading.Tasks;
using AlgoTrade.Core;
using AlgoTrade.Core.Logging;
using AlgoTrade.Core.Python;

Log("=== TestOldPlotterFromBundle ===");

string bundlePath = Path.Combine(AppSettings.PythonPlotterBundleDir, "latest_bundle.npz");
string viewPath   = Path.Combine(AppSettings.PythonPlotterBundleDir, "latest_bundle.view.json");

if (!File.Exists(bundlePath))
{
    Log($"[HATA] Bundle bulunamadi: {bundlePath}");
    Log("Once [5] (01_RunSingleTraderWithProgressAsync.csx) calistirip bundle uretin.");
    return;
}

Log($"Bundle : {bundlePath}");
Log($"View   : {(File.Exists(viewPath) ? viewPath : "(yok, kullanilmiyor)")}");

var plotter = new PythonPlotter();
plotter.SetLogger(LogManager.GetInstance());

string? pythonDll = AppSettings.ResolvePythonDll();
if (string.IsNullOrEmpty(pythonDll))
{
    Log("[HATA] Python DLL bulunamadi. Proje kokunde setupPythonEnvs.bat calistirip .venv kurun.");
    return;
}
plotter.PythonDll = pythonDll;

Log("Python engine baslatiliyor...");
plotter.Initialize();
Log("✓ Python engine hazir.");

Log("Bundle'dan cizim yapiliyor (memory/NpzReader yolu) - pencere kapanana dek bekleniyor...");
await Task.Run(() => plotter.PlotBundleFile(bundlePath, viewPath));

Log("=== Bitti ===");
