// =============================================================================
// Config_06_ConfirmingSingleTrader.csx - 06_RunConfirmingSingleTraderWithProgressAsync.csx icin Konfigurasyon Scripti
// Strategy, confirmation, trade params ve diger ayarlari burada tanimlayin
// =============================================================================
using System.Collections.Generic;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;

// =============================================================================
// Ayarlar
// =============================================================================
string stockDataFullFileName = @"C:\data\csvFiles\CRP\05\BTCUSDT_BNC.csv";

// =============================================================================
// SignalTrader Stratejisi (ham Al/Sat/Flat sinyalini uretir)
// =============================================================================
string strategyName = "SimpleMostStrategy";
var strategyParameters = new Dictionary<string, object>
{
    ["period"] = 21,
    ["percent"] = 1.0,
    ["choice"] = 0
};

// =============================================================================
// Sanal Pozisyon Konfirmasyon Ayarlari - bkz. docs/todo.md, "Getiri Egrisi /
// KarZarar Egrisi Konfirmasyonu (Madde 3)"
// =============================================================================
bool thresholdIsPercentage = false;
double profitThreshold = 5000.0;
double lossThreshold = -3000.0;
ConfirmationTrigger confirmationTrigger = ConfirmationTrigger.Both;
SignalConflictMode conflictMode = SignalConflictMode.CancelAndRestart;
bool flattenImmediatelyOnFlatSignal = true;

// =============================================================================
// Trade Params (mainTrader ve signalTrader ayni parametreleri kullanir)
// =============================================================================
double ilkBakiye = 100000.0;
double lotSayisi = 0.01;
double komisyonCarpan = 0.0;
double kaymaMiktari = 0.0;

// =============================================================================
// Symbol Info
// =============================================================================
string symbolName = "...";
string symbolPeriod = "...";

// =============================================================================
// Save Statistics
// =============================================================================
bool saveMainTraderStatistics = true;
bool saveConfirmingSingleTraderLists = true;
