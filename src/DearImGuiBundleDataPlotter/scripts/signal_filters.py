"""AL/SAT/FLAT sinyal dizisini duzenleyen filtreler.

Girdi/cikti HER ZAMAN bundle'daki ham "signal_codes" seması (int): 0=None,
1=AL(Buy), -1=SAT(Sell), 2=FLAT. Bu dizi SEYREK'tir (sadece strateji karar
verdigi barda 0 disi deger tasir) - "signal_steps" ise YOGUN pozisyon durumu
(-1/0/1, her barda doludur, C# execution motoru tarafindan uretilmistir).

Felsefe: equity/bakiye/PnL HESAPLANMAZ burada - sadece ORIJINAL calismanin
zaten urettigi "PnL"/"PnL %" serileri (bkz. bundle indicator_names) OKUNUP
hangi trade'in kalacagina/ne zaman erken kapanacagina karar verilir. Nihai
sayilar (filtre uygulandiktan SONRA) replay_bridge.runReplay() ile GERCEK
motordan yeniden uretilir - burada tahmini bir equity hesabi YOK.
"""

import numpy as np

NONE_CODE = 0
BUY_CODE = 1
SELL_CODE = -1
FLAT_CODE = 2


def extractSegments(signalSteps):
    """signal_steps (yogun, -1/0/1) dizisini pozisyon segmentlerine ayirir.
    Sadece state != 0 olan (ACIK pozisyon) segmentleri dondurur.

    Donus: [(startIdx, endIdx, direction), ...] - direction: 1=long, -1=short.
    endIdx DAHIL (segmentin son barı, dogal kapanistan ONCEKI bar ya da verinin
    son bari eger pozisyon hic kapanmadiysa).
    """
    n = len(signalSteps)
    segments = []
    i = 0
    while i < n:
        state = signalSteps[i]
        if state == 0:
            i += 1
            continue
        start = i
        while i + 1 < n and signalSteps[i + 1] == state:
            i += 1
        segments.append((start, i, int(state)))
        i += 1
    return segments


def filterByReturnThreshold(signalCodes, signalSteps, pnlPct,
                             longThresholdPct, shortThresholdPct):
    """Her pozisyon segmenti icin, kapanis anindaki (bkz. pnlPct[endIdx])
    yon-duzeltilmis getiri yuzdesi esigin ALTINDAYSA (kar da olsa, zarar da
    olsa) o trade'in ENTRY sinyalini siler (0'a cevirir) - "bu trade hic
    olmamis gibi" davranir. Esigin USTUNDEKI trade'ler DOKUNULMADAN kalir.

    longThresholdPct: long (direction=1) segmentler icin esik.
    shortThresholdPct: short (direction=-1) segmentler icin esik.

    Donus: (editedSignalCodes, silinenTradeSayisi, toplamTradeSayisi)
    """
    edited = np.array(signalCodes, copy=True)
    segments = extractSegments(signalSteps)

    removed = 0
    for startIdx, endIdx, direction in segments:
        threshold = longThresholdPct if direction == 1 else shortThresholdPct
        tradeReturnPct = float(pnlPct[endIdx])

        if tradeReturnPct < threshold:
            edited[startIdx] = NONE_CODE
            removed += 1

    return edited, removed, len(segments)


def forceSeparateSameDirectionReentries(signalCodes, signalSteps):
    """filterByReturnThreshold SONRASI uygulanir. Sinyaller zincir gibidir:
    iki hayatta kalan trade'in arasinda silinmis (NONE_CODE) segment(ler)
    varsa, pozisyon o aralikta "flat" olmaz - ONCEKI hayatta kalan trade'in
    yonunde donmus gibi kalir. Eger bir sonraki hayatta kalan trade AYNI
    yondeyse, motor onu yeni bir giris saymaz ("zaten o yondeyim") - iki
    BAGIMSIZ trade, sessizce TEK bir uzun pozisyona kaynasir (bkz. kullanici
    gozlemi: "trade sinyalleri zincir gibi, birini cikarinca arkadakini de
    etkiliyor").

    Bu fonksiyon, boyle bir aradaki ilk silinmis segmentin basina (zaten
    NONE_CODE olan, bos bir slot) bir FLAT_CODE yazarak pozisyonu GERCEKTEN
    kapatir - boylece bir sonraki hayatta kalan trade, kendi barinda
    sifirdan (flat'tan) BAGIMSIZ bir giris olarak acilir, onceki trade'le
    kaynasmaz. Yon DEGISEN gecislere (long->short gibi) DOKUNULMAZ - o zaten
    motor tarafindan dogal bir flip (kapat+ters ac) olarak dogru islenir,
    sadece AYNI yone donus (long->...->long) zincirlenme sorunu yaratir.

    Donus: (editedSignalCodes, zorlaAyrilanGecisSayisi)
    """
    edited = np.array(signalCodes, copy=True)
    segments = extractSegments(signalSteps)

    forcedSeparations = 0
    lastSurvivingDirection = None
    pendingFlatInjectionIdx = None

    for startIdx, _endIdx, direction in segments:
        if signalCodes[startIdx] == NONE_CODE:
            if pendingFlatInjectionIdx is None:
                pendingFlatInjectionIdx = startIdx  # bu "bosluk"taki ILK silinmis slot
            continue

        if (lastSurvivingDirection is not None
                and direction == lastSurvivingDirection
                and pendingFlatInjectionIdx is not None):
            edited[pendingFlatInjectionIdx] = FLAT_CODE
            forcedSeparations += 1

        lastSurvivingDirection = direction
        pendingFlatInjectionIdx = None

    return edited, forcedSeparations


def applyEarlyExitOnProfit(signalCodes, signalSteps, pnl, profitThresholdTl):
    """Her ACIK pozisyon segmentinde, o pozisyonun ANLIK (floating) PnL'i
    (bkz. bundle indicator "PnL") ilk kez profitThresholdTl'ye ULASTIGI barda
    sentetik bir FLAT sinyali enjekte eder - dogal kapanisindan ONCE ise.
    Dogal kapanis tetikleyicisine (FLAT ya da bir sonraki trade'in entry'si)
    DOKUNULMAZ - erken FLAT'tan sonra pozisyon zaten kapali oldugu icin
    motor onu ya no-op (FLAT) ya da sifirdan yeni bir giris (flip) olarak
    doğru şekilde ele alir.

    signalCodes BASKA bir filtreden (orn. filterByReturnThreshold,
    forceSeparateSameDirectionReentries) GECMIS olabilir - segmentin entry'si
    o filtrelerde zaten silinmisse (signalCodes bu noktada NONE_CODE) VEYA
    forceSeparateSameDirectionReentries tarafindan bir "zorla kapat" FLAT_CODE
    ile doldurulmussa, bu segment "hic acilmamis" sayilir ve ATLANIR - sadece
    gercekten BUY_CODE/SELL_CODE ile hala bir ENTRY olan segmentler "canli"
    kabul edilir. signalSteps HALA orijinal (filtre-ONCESI) calismanin yogun
    state'i oldugu icin bu kontrol olmazsa silinmis/zorla-kapatilmis bir
    trade'in zaman araligina anlamsizca "erken kapat" uygulanmaya calisilir
    (sonuc degismez ama sayim/loglama yanlis olur).

    Donus: (editedSignalCodes, erkenKapatilanTradeSayisi, canliTradeSayisi)
    """
    edited = np.array(signalCodes, copy=True)
    segments = extractSegments(signalSteps)

    earlyClosed = 0
    liveTradeCount = 0
    for startIdx, endIdx, _direction in segments:
        if signalCodes[startIdx] not in (BUY_CODE, SELL_CODE):
            continue  # onceki filtrede silinmis/zorla-kapatilmis - bu segment yok sayilir
        liveTradeCount += 1

        segmentPnl = pnl[startIdx:endIdx + 1]
        hits = np.where(segmentPnl >= profitThresholdTl)[0]
        if len(hits) == 0:
            continue

        hitIdx = startIdx + int(hits[0])
        if hitIdx >= endIdx:
            continue  # tam kapanis barinda zaten dogal olarak kapaniyor

        if edited[hitIdx] == NONE_CODE:
            edited[hitIdx] = FLAT_CODE
        earlyClosed += 1

    return edited, earlyClosed, liveTradeCount


SIGNAL_CODE_TO_NAME = {NONE_CODE: "None", BUY_CODE: "Buy", SELL_CODE: "Sell", FLAT_CODE: "Flat"}


def signalCodesToNames(signalCodes):
    return [SIGNAL_CODE_TO_NAME[int(c)] for c in signalCodes]
