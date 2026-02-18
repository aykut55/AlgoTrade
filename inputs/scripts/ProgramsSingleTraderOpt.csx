// =============================================================================
// ProgramsSingleTraderOpt.csx - SingleTraderOptimizer Konfigürasyon Scripti
// Parametre range'leri, strategy factory, optimization range ve diger ayarları
// burada tanimlayin
// =============================================================================
using System.Collections.Generic;
using System.Globalization;
using AlgoTrade.Core.Trading;

// =============================================================================
// Ayarlar
// =============================================================================
string stockDataFullFileName = @"C:\data\csvFiles\VIP\01\VIP-X030-T.csv";

// =============================================================================
// Optimization Parameter Ranges
// =============================================================================
var optimizationRanges = new List<(string name, double min, double max, double step)>
{
    ("period",  10, 50, 10),
    ("percent", 1.0, 3.0, 1.0)
};

// =============================================================================
// Fixed Parameters (optimize edilmeyen sabit degerler)
// =============================================================================
var fixedParams = new Dictionary<string, object>
{
    ["choice"] = 0
};

// =============================================================================
// Strategy Factory Configuration
// =============================================================================
string optimizationStrategyName = "SimpleMostStrategy";

// =============================================================================
// Optimization Range (PartialOpt)
// -1 = en bastan / en sona kadar (FullOpt)
// Ornek: from=5, to=10 -> sadece 5-10 arasi kombinasyonlari calistir
// =============================================================================
int optimizationFrom = -1;
int optimizationTo = -1;

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
