// =============================================================================
// ProgramsMultipleTrader.csx - MultipleTrader Konfigürasyon Scripti
// Strategy, query, ECF listelerini ve diger ayarları burada tanimlayin
// =============================================================================
using System.Collections.Generic;
using AlgoTrade.Core.Trading;

// =============================================================================
// Ayarlar
// =============================================================================
string stockDataFullFileName = @"C:\data\csvFiles\VIP\01\VIP-X030-T.csv";
TraderRunMode selectedRunMode = TraderRunMode.TradeAndQuery;

// =============================================================================
// Strategy Configurations (Id bazli)
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
// Query Configurations (Id bazli)
// =============================================================================
var queryConfigs = new List<(int id, string name, Dictionary<string, object> parameters)>
{
    (0, "SimpleQuery1", new Dictionary<string, object>
    {
        ["ma8Period"] = 8,
        ["ma200Period"] = 200,
        ["choice"] = 0
    }),
    (1, "SimpleQuery1", new Dictionary<string, object>
    {
        ["ma8Period"] = 5,
        ["ma200Period"] = 100,
        ["choice"] = 0
    })
};

// =============================================================================
// Equity Curve Filter Configuration
// =============================================================================
bool ecfEnabled = false;
bool ecfThresholdTypeIsPercent = true;
double ecfProfitThreshold = 0.05;
double ecfLossThreshold = -0.05;
ConfirmationTrigger ecfTrigger = ConfirmationTrigger.Both;

// =============================================================================
// Trade Params
// =============================================================================
double ilkBakiye = 100000.0;
int kontratSayisi = 1;
double komisyonCarpan = 20.0;
double kaymaMiktari = 0.5;

// =============================================================================
// Symbol Info
// =============================================================================
string symbolName = "...";
string symbolPeriod = "...";

// =============================================================================
// Save Statistics
// =============================================================================
bool saveMainTraderStatistics = true;
bool saveChildTraderStatistics = true;
