"""Python -> C# koprusu: Python'da filtrelenmis/duzenlenmis (veya bastan uretilmis)
bir AL/SAT/FLAT sinyal dizisini AlgoTrade.ReplayRunner.exe'ye (bkz.
AlgoTrade.ReplayRunner/Program.cs) verir; o da bu diziyi ReplaySignalStrategy
(src/AlgoTrade.Core/Trading/Strategies/ReplayStrategies/ReplaySignalStrategy.cs)
uzerinden GERCEK SingleTrader execution motoruna besler ve equity/bakiye/trade
istatistiklerini (komisyon, pozisyon state-machine dahil) O motordan taze
urettirip bir .npz/.view.json bundle'i olarak geri yazar.

"Tek dogru kaynak" execution tarafinda kalsin diye - burada equity/bakiye ile
ilgili HICBIR hesap YAPILMAZ, sadece subprocess cagirma + JSON IO var.
"""

import json
import os
import re
import subprocess

# TradeSignals enum (bkz. AlgoTrade.Core/Trading/Core/TradeSignals.cs) adlariyla
# BIREBIR AYNI olmali - ReplayRunner Enum.Parse<TradeSignals> ile coziyor.
SIGNAL_NONE = "None"
SIGNAL_BUY = "Buy"
SIGNAL_SELL = "Sell"
SIGNAL_TAKE_PROFIT = "TakeProfit"
SIGNAL_STOP_LOSS = "StopLoss"
SIGNAL_FLAT = "Flat"
SIGNAL_SKIP = "Skip"


class ReplayRunnerError(RuntimeError):
    pass


def loadJsonc(path):
    """AppConfig.json gibi C#'in elle duzenledigi dosyalar JSONC (// yorumlar +
    trailing comma) icerir - standart json modulu bunlari KABUL ETMEZ. Yorumlari
    (string icindekiler haric) ve kapanis parantezinden once gelen trailing
    comma'lari temizleyip standart json.loads'a verir."""
    with open(path, "r", encoding="utf-8-sig") as f:
        raw = f.read()

    noComments = []
    inString = False
    i = 0
    n = len(raw)
    while i < n:
        ch = raw[i]
        if inString:
            noComments.append(ch)
            if ch == "\\" and i + 1 < n:
                noComments.append(raw[i + 1])
                i += 2
                continue
            if ch == '"':
                inString = False
            i += 1
            continue
        if ch == '"':
            inString = True
            noComments.append(ch)
            i += 1
            continue
        if ch == "/" and i + 1 < n and raw[i + 1] == "/":
            while i < n and raw[i] != "\n":
                i += 1
            continue
        noComments.append(ch)
        i += 1

    withoutTrailingCommas = re.sub(r",(\s*[}\]])", r"\1", "".join(noComments))
    return json.loads(withoutTrailingCommas)


def findRunnerExePath(rootDir):
    """AlgoTrade.ReplayRunner.exe'yi bilinen build cikti klasorlerinde arar
    (Debug'i Release'den once dener - gelistirme sirasinda genelde o guncel)."""
    candidates = (
        os.path.join(rootDir, "AlgoTrade.ReplayRunner", "bin", "Debug", "net8.0", "AlgoTrade.ReplayRunner.exe"),
        os.path.join(rootDir, "AlgoTrade.ReplayRunner", "bin", "Release", "net8.0", "AlgoTrade.ReplayRunner.exe"),
    )
    for path in candidates:
        if os.path.isfile(path):
            return path
    raise ReplayRunnerError(
        "AlgoTrade.ReplayRunner.exe bulunamadi. Once 'dotnet build AlgoTrade.ReplayRunner' calistirin.\n"
        f"Aranan yollar:\n  " + "\n  ".join(candidates))


def runReplay(rootDir, signals, stockDataFilePath, outputBundleDir,
              outputBundleName="replay_bundle", appConfigPath=None,
              exchangeDir=None, timeoutSeconds=180):
    """signals: TradeSignals adlarindan (SIGNAL_* sabitleri) olusan, bar sayisiyla
    AYNI uzunlukta bir liste. stockDataFilePath: orijinal run'da kullanilan veri
    dosyasi (bundle'daki OHLC'nin kaynagi) - AppConfig.json'daki o anki ayar DEGIL,
    acikca verilmeli (konfig degismis olabilir).

    Donus: {"bundlePath": ..., "viewPath": ...} - cagiran taraf bunu
    inputs/input.json'a yazip gm.scriptPanel.runScriptFile("default.py") ile
    (ayni rendering kodunu tekrar kullanarak) cizdirebilir.
    """
    runnerExePath = findRunnerExePath(rootDir)

    exchangeDir = exchangeDir or os.path.join(rootDir, "inputs", "replay_runner")
    os.makedirs(exchangeDir, exist_ok=True)

    inputPath = os.path.join(exchangeDir, "replay_input.json")
    outputPath = os.path.join(exchangeDir, "replay_output.json")

    inputPayload = {
        "stockDataFilePath": stockDataFilePath,
        "signals": list(signals),
        "outputBundleDir": outputBundleDir,
        "outputBundleName": outputBundleName,
    }
    if appConfigPath:
        inputPayload["appConfigPath"] = appConfigPath

    with open(inputPath, "w", encoding="utf-8") as f:
        json.dump(inputPayload, f, ensure_ascii=False)

    if os.path.isfile(outputPath):
        os.remove(outputPath)

    process = subprocess.run(
        [runnerExePath, inputPath, outputPath],
        capture_output=True, text=True, timeout=timeoutSeconds)

    if not os.path.isfile(outputPath):
        raise ReplayRunnerError(
            f"ReplayRunner output yazmadi (exit={process.returncode}).\n"
            f"stdout:\n{process.stdout}\nstderr:\n{process.stderr}")

    with open(outputPath, "r", encoding="utf-8-sig") as f:
        result = json.load(f)

    if not result.get("success"):
        raise ReplayRunnerError(f"ReplayRunner hata verdi: {result.get('error')}")

    return {"bundlePath": result["bundlePath"], "viewPath": result["viewPath"]}
