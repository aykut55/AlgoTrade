# Hazir: gm, pm, pool, tsr, Panel, PanelData
#
# KULLANIM: Script Panel'de "Open" -> bu dosyayi sec -> "Run". Su anda plotter'da
# ACIK olan bundle'in AL/SAT/FLAT sinyallerini okur, asagidaki esiklerle filtreler,
# GERCEK SingleTrader motorundan (AlgoTrade.ReplayRunner.exe) yeniden calistirip
# equity/bakiye/komisyoyu TAZE urettirir, sonucu inputs/input.json'a yazip
# default.py'yi tekrar calistirarak PLOTU YENIDEN CIZER (ayni "Run" tuyla).
#
# Once AlgoTrade.ReplayRunner derlenmis olmali: `dotnet build AlgoTrade.ReplayRunner`

import json
import os
import shutil
import sys
import time

import numpy as np

SCRIPT_DIR = getattr(gm.scriptPanel, "_scripts_dir", os.path.join(os.getcwd(), "DearImGuiBundleDataPlotter", "scripts"))
PROJECT_DIR = os.path.dirname(SCRIPT_DIR)
INPUTS_DIR = os.path.join(PROJECT_DIR, "inputs")
ROOT_DIR = os.path.dirname(os.path.dirname(PROJECT_DIR))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

import replay_bridge  # noqa: E402
import signal_filters as sf  # noqa: E402

# =============================================================================
# AYARLAR - deneme yaparken sadece bu blogu degistir
# =============================================================================

# Yon filtresi: "A" (sadece long), "S" (sadece short) veya "AS" (ikisi de, filtre yok).
DIRECTION_FILTER = "AS"

# Filtre A: trade'in kapanis anindaki yon-duzeltilmis kar%'i esigin ALTINDAYSA
# (kar da olsa zarar da olsa) o trade silinir. Long/short icin AYRI esik.
FILTER_A_ENABLED = True
LONG_RETURN_THRESHOLD_PCT = 2.0
SHORT_RETURN_THRESHOLD_PCT = 2.0

# Filtre A sonrasi: aralarinda silinmis trade(ler) olan, AYNI yondeki iki
# "hayatta kalan" trade'i zorla ayirir (kapat+yeniden ac) - sessiz kaynasmayi
# engeller. FILTER_A_ENABLED kapaliyken anlamsiz (silinen trade yoksa gerek yok).
FORCE_SEPARATE_ENABLED = True

# Filtre B: acik pozisyonun anlik (floating) PnL'i (TL) bu esige ULASTIGI an
# pozisyon erken kapatilir - dogal kapanisindan once ise.
FILTER_B_ENABLED = True
PROFIT_TAKE_THRESHOLD_TL = 500.0

# Bos birakilirsa AppConfig.json'daki AppSettings.StockDataFile kullanilir -
# orijinal run'da BASKA bir veri dosyasi kullanildiysa burada ACIKCA belirt.
STOCK_DATA_FILE_OVERRIDE = ""

OUTPUT_BUNDLE_NAME = "replay_filtered_bundle"

# Bos birakilirsa asagidaki NORMAL FILTRE akisi calisir ve sonunda konsola
# "Yedek: ..." satiriyla o anki (filtre-ONCESI) bundle'in yedek yolu yazilir.
# Beğenmediğin bir filtre sonucundan GERI DONMEK icin o yedek yolunu buraya
# yapistirip tekrar Run'a bas - HICBIR filtre uygulanmadan sadece o bundle
# geri yuklenir. "Mevcuttan devam et" icin bu alani BOS birak - script zaten
# her zaman o an ekranda acik olan bundle'i baz alir (zincirleme/chaining
# dogal olarak calisir).
LOAD_BACKUP_PATH = r"D:\Aykut\Projects\AlgoTrade\src\DearImGuiBundleDataPlotter\inputs\replay_backups\1795631517831_latest_bundle.npz"
LOAD_BACKUP_PATH = ""

# =============================================================================


def resolveStockDataFilePath():
    if STOCK_DATA_FILE_OVERRIDE:
        return STOCK_DATA_FILE_OVERRIDE

    appConfigPath = os.path.join(ROOT_DIR, "inputs", "configs", "AppConfig", "AppConfig.json")
    appConfig = replay_bridge.loadJsonc(appConfigPath)
    return appConfig["AppSettings"]["StockDataFile"]


def deriveViewPath(bundlePath):
    base, _ext = os.path.splitext(bundlePath)
    return base + ".view.json"


def loadBundleIntoPlotter(bundlePath, viewPath):
    inputConfigPath = os.path.join(INPUTS_DIR, "input.json")
    with open(inputConfigPath, "w", encoding="utf-8") as f:
        json.dump({"bundle": bundlePath, "view": viewPath}, f, ensure_ascii=False, indent=2)
    gm.scriptPanel.runScriptFile("default.py")


def backupCurrentBundle(bundlePath, viewPath):
    """Filtre uygulamadan ONCE, o anki bundle+view'i inputs/replay_backups/'a
    zaman damgali kopyalar - beğenilmeyen bir filtre sonucundan LOAD_BACKUP_PATH
    ile buraya geri donulebilsin diye."""
    backupsDir = os.path.join(INPUTS_DIR, "replay_backups")
    os.makedirs(backupsDir, exist_ok=True)

    stamp = int(time.time() * 1000)
    baseName = os.path.splitext(os.path.basename(bundlePath))[0]
    backupBundlePath = os.path.join(backupsDir, f"{stamp}_{baseName}.npz")
    shutil.copy2(bundlePath, backupBundlePath)

    if os.path.isfile(viewPath):
        backupViewPath = deriveViewPath(backupBundlePath)
        shutil.copy2(viewPath, backupViewPath)

    return backupBundlePath


def run():
    bundlePath = getattr(gm, "currentBundlePath", None)
    if not bundlePath or not os.path.isfile(bundlePath):
        print("[ReplaySignalFilter] Once bir bundle yuklu olmali (gm.currentBundlePath yok/gecersiz).")
        return

    if LOAD_BACKUP_PATH:
        print(f"[ReplaySignalFilter] Yedekten geri yukleniyor: {LOAD_BACKUP_PATH}")
        loadBundleIntoPlotter(LOAD_BACKUP_PATH, deriveViewPath(LOAD_BACKUP_PATH))
        return

    backupPath = backupCurrentBundle(bundlePath, deriveViewPath(bundlePath))
    print(f"[ReplaySignalFilter] Yedek: {backupPath}")

    bundle = np.load(bundlePath, allow_pickle=False)
    if "signal_codes" not in bundle or "signal_steps" not in bundle:
        print("[ReplaySignalFilter] Bundle'da signal_codes/signal_steps yok, filtre uygulanamaz.")
        return

    signalCodes = bundle["signal_codes"]
    signalSteps = bundle["signal_steps"]
    indicatorNames = bundle["indicator_names"].tolist()
    indicatorValues = bundle["indicator_values"]
    pnl = indicatorValues[indicatorNames.index("PnL")]
    pnlPct = indicatorValues[indicatorNames.index("PnL %")]

    edited = signalCodes
    totalTrades = 0
    anyDeletionFilterApplied = False

    if DIRECTION_FILTER != "AS":
        keepDirection = 1 if DIRECTION_FILTER == "A" else -1
        edited, removed, totalTrades = sf.filterByDirection(edited, signalSteps, keepDirection)
        print(f"[ReplaySignalFilter] Yon filtresi ({DIRECTION_FILTER}): {totalTrades} trade, {removed} silindi, {totalTrades - removed} kaldi")
        anyDeletionFilterApplied = True

    if FILTER_A_ENABLED:
        edited, removed, totalTrades = sf.filterByReturnThreshold(
            edited, signalSteps, pnlPct, LONG_RETURN_THRESHOLD_PCT, SHORT_RETURN_THRESHOLD_PCT)
        print(f"[ReplaySignalFilter] Filtre A: {totalTrades} trade, {removed} silindi, {totalTrades - removed} kaldi")
        anyDeletionFilterApplied = True

    if anyDeletionFilterApplied and FORCE_SEPARATE_ENABLED:
        edited, forcedSeparations = sf.forceSeparateSameDirectionReentries(edited, signalSteps)
        print(f"[ReplaySignalFilter] Zorla ayirma: {forcedSeparations} ayni-yon zincirlenmesi koparildi")

    if FILTER_B_ENABLED:
        edited, earlyClosed, liveTrades = sf.applyEarlyExitOnProfit(edited, signalSteps, pnl, PROFIT_TAKE_THRESHOLD_TL)
        print(f"[ReplaySignalFilter] Filtre B: {liveTrades} canli trade icinde {earlyClosed} erken kapatildi "
              f"(>= {PROFIT_TAKE_THRESHOLD_TL} TL)")

    changedBars = int(np.count_nonzero(signalCodes != edited))
    print(f"[ReplaySignalFilter] Toplam degisen bar: {changedBars} / {len(signalCodes)}")

    stockDataFilePath = resolveStockDataFilePath()
    signals = sf.signalCodesToNames(edited)

    # Dosya adina her run'da degisen bir damga eklenir - default.py onceki
    # bundle'i np.load() ile actiginda o .npz dosyasinin handle'ini (lazy
    # erisim icin, bkz. numpy NpzFile) acik tutuyor; AYNI dosya adini hemen
    # arka arkaya tekrar yazmaya calismak Windows'ta "process cannot access
    # the file" hatasi veriyordu (bkz. onceki hata). Benzersiz ad bu
    # cakismayi tamamen ortadan kaldirir.
    uniqueBundleName = f"{OUTPUT_BUNDLE_NAME}_{int(time.time() * 1000)}"

    print("[ReplaySignalFilter] AlgoTrade.ReplayRunner calistiriliyor (gercek motor)...")
    result = replay_bridge.runReplay(
        ROOT_DIR, signals, stockDataFilePath, INPUTS_DIR,
        outputBundleName=uniqueBundleName, timeoutSeconds=600)

    print(f"[ReplaySignalFilter] Yeni bundle: {result['bundlePath']}")
    print("[ReplaySignalFilter] default.py yeniden calistiriliyor, plot guncelleniyor...")
    loadBundleIntoPlotter(result["bundlePath"], result["viewPath"])


run()
