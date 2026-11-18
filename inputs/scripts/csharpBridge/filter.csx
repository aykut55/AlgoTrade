// Filter asamasi - globals: Data (List<StockData>), RawSignals (int[], Signal
// asamasindan - 1=AL, -1=SAT, 0=yok)
// return: int[] (filtrelenmis sinyal dizisi, Data.Count uzunlugunda)
// Kodlar: 0=yok, 1=AL(Buy), -1=SAT(Sell), 3=KarAl(TakeProfit), 4=ZararKes(StopLoss),
//         5=FlatOl, 6=PasGec(Skip)
//
// 7 BAGIMSIZ METOD - her biri KENDI enable parametresiyle acilir/kapanir,
// sirayla cagrilir (asagidaki "Ana akis" bolumu). Her metod ONCEKI metodun
// ciktisini (signals) girdi alir, kendi kopyasini yapar, dondurur.

double[] close = Data.Select(d => d.Close).ToArray();

// ============================================================================
// 1) Pozisyon Filtresi - acilis-kapanis arasi |%degisim| esikten kucukse sil
//    (gercek sistemde karsiligi YOK - bizim eklememiz, hindsight filtresi)
// ============================================================================
int[] ApplyPositionFilter(int[] signals, bool enabled, double minChangePct)
{
    if (!enabled) return signals;
    var result = (int[])signals.Clone();

    var sigIndices = new List<int>();
    for (int i = 0; i < result.Length; i++)
        if (result[i] == 1 || result[i] == -1) sigIndices.Add(i);

    for (int k = 1; k < sigIndices.Count; k++)
    {
        int prevIdx = sigIndices[k - 1];
        int curIdx = sigIndices[k];
        if (result[prevIdx] == 0) continue; // onceki adimda zaten silindiyse atla

        double changePct = Math.Abs(close[curIdx] - close[prevIdx]) / close[prevIdx] * 100.0;
        if (changePct < minChangePct)
        {
            result[prevIdx] = 0;  // o pozisyonun acilis sinyalini sil
            result[curIdx] = 0;   // kapanis sinyalini de sil
        }
    }
    return result;
}

// ============================================================================
// 2) Kar Al / Zarar Kes - acik pozisyonda anlik %degisim esigi gecince erken
//    cikis (gercek sistemde strateji SEVIYESINDE - orn. SimpleMAStrategy'nin
//    kendi skip>flat>TP>SL>buy>sell zinciri - ResolveFilterDecisions'IN
//    DISINDA, ondan ONCE karar verilir).
// ============================================================================
int[] ApplyTakeProfitStopLoss(int[] signals, bool enabled, double takeProfitPct, double stopLossPct)
{
    if (!enabled) return signals;
    var result = (int[])signals.Clone();

    int position = 0;        // 0 = flat, 1 = long, -1 = short
    double entryPrice = 0.0;
    for (int i = 0; i < result.Length; i++)
    {
        if (result[i] == 1) { position = 1; entryPrice = close[i]; }
        else if (result[i] == -1) { position = -1; entryPrice = close[i]; }
        else if (result[i] == 3 || result[i] == 4 || result[i] == 5) { position = 0; }
        else if (position != 0)
        {
            double changePct = (close[i] - entryPrice) / entryPrice * 100.0 * position;
            if (changePct >= takeProfitPct) { result[i] = 3; position = 0; }       // KarAl
            else if (changePct <= -stopLossPct) { result[i] = 4; position = 0; }   // ZararKes
        }
    }
    return result;
}

// ============================================================================
// 3) PozKapat - belirtilen bar index'lerinde pozisyon aciksa FlatOl uretir
//    (SingleTrader.cs Oncelik 1 - manuel/disaridan zorla kapat, hard override)
// ============================================================================
int[] ApplyManualPositionClose(int[] signals, bool enabled, HashSet<int> closeAtBars)
{
    if (!enabled || closeAtBars == null || closeAtBars.Count == 0) return signals;
    var result = (int[])signals.Clone();

    int position = 0;
    for (int i = 0; i < result.Length; i++)
    {
        if (result[i] == 1) position = 1;
        else if (result[i] == -1) position = -1;
        else if (result[i] == 3 || result[i] == 4 || result[i] == 5) position = 0;

        if (closeAtBars.Contains(i) && position != 0)
        {
            result[i] = 5; // FlatOl
            position = 0;
        }
    }
    return result;
}

// ============================================================================
// 4) Gun Sonu Pozisyon Kapat - gunun son barinda acik pozisyon varsa FlatOl
//    uretir (SingleTrader.cs Oncelik 2 - hard override, Timing'den ONCE
//    kontrol edilir ki Timing'in hard-block'u bu FlatOl'u silmesin)
// ============================================================================
int[] ApplyEndOfDayClose(int[] signals, bool enabled)
{
    if (!enabled) return signals;
    var result = (int[])signals.Clone();

    int position = 0; // 0 = flat, 1 = long, -1 = short
    for (int i = 0; i < result.Length; i++)
    {
        if (result[i] == 1) position = 1;
        else if (result[i] == -1) position = -1;
        else if (result[i] == 3 || result[i] == 4 || result[i] == 5) position = 0;

        bool isLastBarOfDay = (i == result.Length - 1) ||
            Data[i + 1].DateTime.Date != Data[i].DateTime.Date;
        if (position != 0 && isLastBarOfDay && result[i] == 0)
        {
            result[i] = 5; // FlatOl
            position = 0;
        }
    }
    return result;
}

// ============================================================================
// 5) Zaman Penceresi - pencere disinda YENI AL/SAT girisi engellenir
//    (SingleTrader.cs Oncelik 3 - gercekte HARD BLOCK: pencere disinda TUM
//    sinyaller None olur; burada SADECE yeni giris engellenir, mevcut
//    FlatOl/KarAl/ZararKes kodlarina DOKUNULMAZ - cagirma sirasi (4'ten
//    SONRA) bu farki zararsiz kilar, bkz. Ana akis yorumu)
// ============================================================================
int[] ApplyTimeWindow(int[] signals, bool enabled, TimeSpan windowStart, TimeSpan windowEnd)
{
    if (!enabled) return signals;
    var result = (int[])signals.Clone();

    for (int i = 0; i < result.Length; i++)
    {
        if (result[i] != 1 && result[i] != -1) continue;
        var timeOfDay = Data[i].DateTime.TimeOfDay;
        bool inWindow = timeOfDay >= windowStart && timeOfDay < windowEnd;
        if (!inWindow) result[i] = 0;
    }
    return result;
}

// ============================================================================
// 6) Warmup - belirtilen bar index'inden ONCE TUM sinyaller silinir
//    (SingleTrader.cs Oncelik 3b - hard block, TradeStartBarIndex)
// ============================================================================
int[] ApplyWarmup(int[] signals, bool enabled, int startBarIndex)
{
    if (!enabled) return signals;
    var result = (int[])signals.Clone();
    for (int i = 0; i < result.Length && i < startBarIndex; i++)
        result[i] = 0;
    return result;
}

// ============================================================================
// 7) Equity Curve Filtresi - pozisyon acildiktan sonra anlik kar/zarar esigi
//    gecilene kadar YON DEGISTIREN yeni giris sinyalleri bloklanir (soft block -
//    SingleTrader.ApplyEquityCurveFilter/ConfigureEquityCurveFilter ile AYNI
//    mantik - SingleTrader.cs Oncelik 4, en son/en yumusak kural)
//    trigger: "Profit", "Loss" veya "Both"
// ============================================================================
int[] ApplyEquityCurveFilter(int[] signals, bool enabled, bool isPercent,
    double profitThreshold, double lossThreshold, string trigger)
{
    if (!enabled) return signals;
    var result = (int[])signals.Clone();

    int position = 0;
    double entryPrice = 0.0;
    bool confirmed = false;

    for (int i = 0; i < result.Length; i++)
    {
        if (result[i] == 1 || result[i] == -1)
        {
            int newPosition = result[i] == 1 ? 1 : -1;
            if (!confirmed && position != 0 && newPosition != position)
            {
                result[i] = 0; // onceki pozisyon henuz onaylanmadi - yon degisimini blokla
                continue;
            }
            position = newPosition;
            entryPrice = close[i];
            confirmed = false;
            continue;
        }
        if (result[i] == 3 || result[i] == 4 || result[i] == 5)
        {
            position = 0;
            confirmed = false;
            continue;
        }
        if (position != 0 && !confirmed)
        {
            double changePct = (close[i] - entryPrice) / entryPrice * 100.0 * position;
            double changePrice = (close[i] - entryPrice) * position;
            bool karTetiklendi = isPercent ? changePct >= profitThreshold : changePrice >= profitThreshold;
            bool zararTetiklendi = isPercent ? changePct <= lossThreshold : changePrice <= lossThreshold;
            bool esikGecildi = trigger == "Profit" ? karTetiklendi
                : trigger == "Loss" ? zararTetiklendi
                : (karTetiklendi || zararTetiklendi);
            if (esikGecildi) confirmed = true;
        }
    }
    return result;
}

// ============================================================================
// Ana akis - metodlar SIRAYLA cagrilir, her biri enabled=false ise oldugu gibi
// geçer. SIRA, SingleTrader.cs ResolveFilterDecisions'daki GERCEK oncelikle
// AYNI: once strateji-seviyesi TP/SL, sonra hard override'lar (PozKapat >
// GunSonuPozKapat > Timing > Warmup), en son soft block (EquityCurve). Her
// metod sadece O ANDA 0 olan barlarda karar verdigi icin bir kod ONCE
// olusmussa SONRAKI metod o bari DEGISTIRMEZ - FlatOl/KarAl/ZararKes/Sell
// birbirini otomatik "ignore" eder.
// ============================================================================
int[] signals = (int[])RawSignals.Clone();

signals = ApplyPositionFilter(signals, enabled: false, minChangePct: 1.0);
signals = ApplyTakeProfitStopLoss(signals, enabled: false, takeProfitPct: 2.0, stopLossPct: 1.0);
signals = ApplyManualPositionClose(signals, enabled: false, closeAtBars: new HashSet<int>());
signals = ApplyEndOfDayClose(signals, enabled: false);
signals = ApplyTimeWindow(signals, enabled: false, windowStart: TimeSpan.FromHours(10), windowEnd: TimeSpan.FromHours(18));
signals = ApplyWarmup(signals, enabled: false, startBarIndex: 0);
signals = ApplyEquityCurveFilter(signals, enabled: false, isPercent: true, profitThreshold: 0.05, lossThreshold: -0.05, trigger: "Both");

return signals;
