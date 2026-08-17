# Ileride Yapilacaklar

## Python Entegrasyonu (TAMAMLANDI - iki yaklasim da benimsendi)

C# ve Python arasinda veri paylasimi icin asagidaki 3 yaklasim degerlendirilmisti. Sonucta
REST/gRPC disindaki iki yaklasim da fiilen projeye girdi, birbirini disliyor degil,
farkli senaryolar icin bir arada kullaniliyor:

### 1. Ortak Veri Dosyalari + Subprocess (BENIMSENDI - DearPyGuiDataPlotter)
- `src/AlgoTrade.Core/Python/DearPyGuiDataPlotter/` (NpzWriter, TradeDataBundleConverter) C#
  tarafinda `.npz` bundle + `.view.json` uretir, `src/DearPyGuiDataPlotter/` Python process'i
  `Process.Start` ile ayri bir process olarak baslatilir.
- Iki taraf `inputs/runtime_commands/` altina yazilan JSON komut dosyalariyla (load_bundle,
  clear_panel, shutdown vb.) haberlesir - dosya tabanli, kuyruklu bir IPC.
- Bkz. `docs/InputConfig.md` (DearPyGuiDataPlotter/docs), `AlgoTrade.Console/Program.cs`
  (`runSingleTraderAlgoTrade()`, `[9] DearPyGuiDataPlotter Test`).

### 2. pythonnet (Python.NET) (BENIMSENDI - PythonPlotter)
- `src/AlgoTrade.Core/Python/PythonPlotter.cs`: `Python.Runtime` (`PythonEngine`, `Py.Import`)
  ile `inputs/python/*.py` (imgui_bundle tabanli plotter) dogrudan in-process cagriliyor.
- Venv: `inputs/python/.venv` (bkz. `setupPythonEnvs.bat`).

### 3. REST API / gRPC (KULLANILMADI - kapsam disi)
- C# tarafi bir servis olarak calistirilip Python HTTP ile cagirma fikri degerlendirilmedi/kullanilmadi.
- Mevcut iki yaklasim ihtiyaci karsiladigi icin su an icin gerek yok.
