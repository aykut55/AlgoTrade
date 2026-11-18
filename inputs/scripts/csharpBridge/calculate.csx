// Hesapla asamasi - globals: Data (List<StockData>), SignalCodes (int[], TradeSignals
// raw degerleri: 0=None,1=Buy,2=Sell), SymbolName, SymbolPeriod (string)
// return: AlgoTrader (SingleTrader'i .SingleTrader uzerinden tasir)
//
// AlgoTrade.ReplayRunner/Program.cs ile AYNI mantik - sinyal dizisini GERCEK
// SingleTrader motoruna (ReplaySignalStrategy uzerinden) besler.
// false: AppConfig.json | true: asagidaki manuel islem parametreleri.

bool useManualTradeParams = false;

var signals = SignalCodes.Select(c => (TradeSignals)c).ToArray();

var logger = LogManager.GetInstance();
var timer = TimeManager.GetInstance();

string appConfigPath = System.IO.Path.Combine(AppSettings.ConfigsDir, "AppConfig", "AppConfig.json");

var appConfig = AppConfigLoader.Load(appConfigPath);

var algoTrader = new AlgoTrader("CSharpBridgeReplay");

algoTrader.RegisterLogger(logger);

algoTrader.RegisterTimer(timer);

algoTrader.Reset();

algoTrader.SetData(Data);

algoTrader.SymbolName = SymbolName;

algoTrader.SymbolPeriod = SymbolPeriod;

if (TimingWindow is { } timingWindow)
{
    var timing = appConfig.SingleTrader.Signals;
    timing.TimeFilteringEnabled = true;
    timing.TimeFilterMode = timingWindow.Mode;
    const string format = "yyyy.MM.dd HH:mm:ss";
    timing.StartDateTime = timingWindow.Start.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
    timing.StopDateTime = timingWindow.EndExclusive.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
}

AppConfigApplier.ApplySingleTrader(algoTrader, appConfig.SingleTrader, AppSettings.ConfigsDir);

if (useManualTradeParams)
{
    var tradeParams = new InitialTradeParams()
        .SetBakiyeParams(ilkBakiye: 100_000.0)
        .SetKontratParamsViopEndex(kontratSayisi: 1.0, varlikAdedCarpani: 10.0)
        .SetKomisyonParams(komisyonCarpan: 20.0)
        .SetKaymaParams(kaymaMiktari: 0.0);

    algoTrader.SetSingleTraderTradeParams(tradeParams);
}

var strategyParams = new Dictionary<string, object> { ["signals"] = signals };
if (ExitRule != null)
    strategyParams["exitRule"] = ExitRule;
algoTrader.ConfigureStrategy("ReplaySignalStrategy", strategyParams);

algoTrader.SingleTraderRunMode = TraderRunMode.TradeOnly;

algoTrader.Initialize();

await algoTrader.RunSingleTraderWithProgressAsync();

return algoTrader;

















// TradeParams gibi bir ayar grubu olarak düşünürsek, AppConfig.json içindeki SingleTrader bölümünde şunlar var:

// | Ayar | Neyi belirler? |
// |---|---|
// | TradeParams | Piyasa tipi, başlangıç bakiyesi, kontrat/lot/hisse adedi, komisyon, kayma ve pozisyona ekleme |
// | Signals | AL, SAT, FlatOl, Pas Geç, Kâr Al ve Zarar Kes izinleri, gün sonu kapatma, zaman penceresi ve başlangıç barı |
// | Strategy | Strateji adı, profil sürümü ve parametre dosyası |
// | Query | Sorgu adı, profil sürümü ve parametre dosyası |
// | EquityCurveFilter | Özsermaye eğrisi filtresinin profili |
// | Plot | Grafik açma ayarı |
// | Save | Hangi raporların yazılacağı ve dosya adları |
// | Export | Dışa aktarımın açık olup olmadığı, sütun ayar dosyası ve sürümü |
// | Optimization | Optimizasyon bayrağı |
// | RunMode | Yalnız işlem, işlem ve sorgu veya yalnız sorgu |