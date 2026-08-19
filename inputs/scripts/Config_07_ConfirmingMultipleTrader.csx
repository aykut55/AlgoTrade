// =============================================================================
// Config_07_ConfirmingMultipleTrader.csx - 07_RunConfirmingMultipleTraderWithProgressAsync.csx icin Konfigurasyon Scripti
// Child stratejiler, consensus, confirmation ve trade params ayarlari burada
// =============================================================================
using System.Collections.Generic;
using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Core;

// =============================================================================
// Ayarlar
// =============================================================================
string stockDataFullFileName = @"C:\data\csvFiles\CRP\05\BTCUSDT_BNC.csv";

// =============================================================================
// Child Strategy Configurations (consensus'u ureten stratejiler)
// =============================================================================
var strategyConfigs = new List<(int id, string name, Dictionary<string, object> parameters)>
{
    (0, "SimpleMostStrategy", new Dictionary<string, object>
    {
        ["period"] = 21,
        ["percent"] = 1.0,
        ["choice"] = 0
    }),
    (1, "SimpleMostStrategy", new Dictionary<string, object>
    {
        ["period"] = 14,
        ["percent"] = 0.5,
        ["choice"] = 0
    })
};

// =============================================================================
// Consensus Ayarlari - bkz. MultipleTrader.BuildConsensusSignal()
// =============================================================================
string consensusMode = "Net";
int consensusMinNetCount = 1;

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
// Trade Params (mainTrader ve tum child'lar ayni parametreleri kullanir)
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
bool saveChildTraderStatistics = false;
bool saveConfirmingMultipleTraderLists = true;
