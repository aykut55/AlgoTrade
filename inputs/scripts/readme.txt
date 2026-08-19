=============================================================================
 inputs/scripts/ - Bu klasordeki dosyalarin ne ise yaradigi
=============================================================================
Bu klasordeki .csx dosyalari konsol uygulamasinin [8] Run Script menusuyle
calistirilir (Program.cs -> Available scripts listesi). Script'ler Roslyn C#
Scripting ile derlenir, AlgoTrade.Core kutuphanesine tam erisimi var.


-----------------------------------------------------------------------------
1) NUMARALI ANA SCRIPTLER (01-19) - konsol menusundeki islevlerin script hali
-----------------------------------------------------------------------------
Her biri Program.cs'teki ilgili handleXxx()/runXxx() fonksiyon ciftinin
interaktif dongusu olmadan (E/R/ENTER/B config-ozet menusu yok), tek seferlik
calisan halidir.

01_RunSingleTraderWithProgressAsync.csx
    -> Menu [2]/[5] SingleTrader. Config: Config_01_SingleTrader.csx (#load ile).
02_RunMultipleTraderWithProgressAsync.csx
    -> Menu [3]/[6] MultipleTrader. Config: Config_02_MultipleTrader.csx.
03_RunSingleTraderOptWithProgressAsync.csx
    -> Menu [4]/[7] SingleTraderOptimizer (parametre optimizasyonu).
       Config: Config_03_SingleTraderOpt.csx.
04_GenerateDearPyGuiDataPlotterBundle.csx
    -> Menu'de karsiligi yok. Config_01_SingleTrader.csx ayarlariyla kendi
       icinde bir SingleTrader calistirip DearPyGuiDataPlotter'in okudugu
       latest_bundle.npz / latest_bundle.view.json dosyalarini uretir.
       (05'in yukleyecegi bundle'i hazirlar.)
05_RunDearPyGuiDataPlotterTest.csx
    -> Menu [9] DearPyGuiDataPlotter (Start/Stop Test). Plotter process'ini
       baslatir, test bundle'ini yukler, bir sure bekler, durdurur.
       Once 04'un ürettigi bundle'in var olmasi gerekir.
06_RunConfirmingSingleTraderWithProgressAsync.csx
    -> Menu [22]/[23] ConfirmingSingleTrader (Sanal Pozisyon Konfirmasyonu).
       Config: Config_06_ConfirmingSingleTrader.csx.
07_RunConfirmingMultipleTraderWithProgressAsync.csx
    -> Menu [24]/[25] ConfirmingMultipleTrader.
       Config: Config_07_ConfirmingMultipleTrader.csx.

08_RunSymbolScan.csx                        -> Menu [10] Tarama: Symbol Scan
09_RunTimeframeScan.csx                     -> Menu [11] Tarama: Timeframe Scan
10_RunMultiStrategyTimeframeScan.csx        -> Menu [12] Tarama: Multi-Strategy Timeframe Scan
11_RunSymbolTimeframeScan.csx               -> Menu [13] Tarama: Symbol-Timeframe Scan
12_RunMultiStrategySymbolScan.csx           -> Menu [14] Tarama: Multi-Strategy Symbol Scan
13_RunMultiStrategySymbolTimeframeScan.csx  -> Menu [15] Tarama: Multi-Strategy Symbol-Timeframe Scan
14_RunQuerySymbolScan.csx                   -> Menu [16] Sorgu Tarama: Query Symbol Scan
15_RunQueryTimeframeScan.csx                -> Menu [17] Sorgu Tarama: Query Timeframe Scan
16_RunMultiQueryTimeframeScan.csx           -> Menu [18] Sorgu Tarama: Multi-Query Timeframe Scan
17_RunQuerySymbolTimeframeScan.csx          -> Menu [19] Sorgu Tarama: Query Symbol-Timeframe Scan
18_RunMultiQuerySymbolScan.csx              -> Menu [20] Sorgu Tarama: Multi-Query Symbol Scan
19_RunMultiQuerySymbolTimeframeScan.csx     -> Menu [21] Sorgu Tarama: Multi-Query Symbol-Timeframe Scan
    Bu 12 tarama scripti ayri bir Config_*.csx kullanmaz - konfigurasyonu
    dogrudan AppConfig.json'daki ilgili bolumden (SymbolScan, TimeframeScan,
    ... QuerySymbolTimeframeScan) okur. Hicbiri global algoTrader'a ihtiyac
    duymaz, kendi taze AlgoTrader'ini kendi icinde kurar (istisna: Multi-
    Strategy* varyantlarindaki ConfigureAlgoTrader callback'i, orada da yine
    kendi kurdugu bir AlgoTrader'i konfigure eder).
    Sinirlama: hicbirinde ESC ile yarida durdurma yok (konsoldaki ayni
    menulerle ayni sinirlama).


-----------------------------------------------------------------------------
2) CONFIG DOSYALARI - bunlar #load ile cagrilir, DOGRUDAN CALISTIRILMAZ
-----------------------------------------------------------------------------
Config_01_SingleTrader.csx           -> 01 ve 04'un kullandigi config
                                         (strateji, query, ECF, trade params,
                                         veri dosyasi yolu)
Config_02_MultipleTrader.csx         -> 02'nin config'i (strateji/query/ECF listeleri)
Config_03_SingleTraderOpt.csx        -> 03'un config'i (optimizasyon parametre range'leri)
Config_06_ConfirmingSingleTrader.csx -> 06'nin config'i
Config_07_ConfirmingMultipleTrader.csx -> 07'nin config'i

Bunlari [8] menusunden secip dogrudan calistirmanin bir anlami yok - sadece
degisken tanimlarindan olusuyorlar, kendi baslarina hicbir sey yapmazlar.


-----------------------------------------------------------------------------
3) ESKI "HEPSI BIR ARADA" DEMO SCRIPTLERI (01/02/03'un atasi, muhtemelen
   artik aktif kullanilmiyor - ama API uyumlulugu kontrol edildi, calisir
   durumdalar)
-----------------------------------------------------------------------------
mainScript.csx
    -> En eski/en buyuk dosya (800+ satir). SingleTrader calistirma +
       script-icinden-script calistirma demosu (ScriptExecutor'i script
       icinde kullanma ornegi) + interaktif script calistirma ornegi gibi
       birden fazla bolum bir arada.
mainScriptMultipleTrader.csx / mainScriptMultipleTraderSimplified.csx
    -> Ayni seyin MultipleTrader versiyonu (02'nin atasi). "Simplified" olan
       daha kisa/sade hali.
mainScriptSimplified.csx
    -> mainScript.csx'in kisaltilmis/sade versiyonu.


-----------------------------------------------------------------------------
4) BAGIMSIZ KUCUK ORNEK SCRIPTLER (Config_*.csx'e bagli degil, parametreler
   scriptin kendi icinde hardcoded)
-----------------------------------------------------------------------------
paramSweep.csx
    -> Tek strateji (SimpleMostStrategy), birden cok period/percent
       kombinasyonuyla art arda calistirilip sonuclar bir ozet tabloda
       basilir - config dosyalarindaki asil tarama scriptlerinden (08-19)
       cok daha minimal, hizli bir parametre denemesi.
runSingleTraderWithStrategy.csx
    -> Tek stratejiyle (hardcoded parametreler) tek seferlik SingleTrader
       calistirma ornegi - 01'in cok kisaltilmis hali.
runMultiTraderWithStrategies.csx
    -> 2 strateji + 2 query + 2 EquityCurveFilter config'i (hepsi hardcoded)
       ile tek seferlik MultipleTrader calistirma ornegi - 02'nin cok
       kisaltilmis hali.


-----------------------------------------------------------------------------
5) TEST / ORNEK AMACLI (uretim amacli degil)
-----------------------------------------------------------------------------
console_scripts.csx
    -> Tamamen yorum satiri (//), calisan hicbir kod yok. Script motorunun
       kabul ettigi syntax'lara ornek olarak birakilmis.
test_hello.csx
    -> Minimal "merhaba dunya" saglik kontrolu: Log(...) ve stockData/
       algoTrader globals'inin dogru geldigini test eder.


-----------------------------------------------------------------------------
NOT
-----------------------------------------------------------------------------
- Bu dosya elle guncelleniyor - yeni script eklerken/silerken/isim
  degistirirken burayi da guncellemeyi unutma.
- ScriptExecutor (src/AlgoTrade.Core/Scripting/ScriptExecutor.cs) su
  namespace'leri otomatik import ediyor, script icinde "using" yazmana
  gerek yok: System, System.Collections.Generic, System.Linq,
  System.Threading.Tasks, AlgoTrade.Core, AlgoTrade.Core.Trading,
  AlgoTrade.Core.Trading.Core, AlgoTrade.Core.Trading.Strategies,
  AlgoTrade.Core.Trading.Strategy, AlgoTrade.Core.Trading.Indicators,
  AlgoTrade.Core.Trading.Queries, AlgoTrade.Core.Trading.Query,
  AlgoTrade.Core.StockDataReader, AlgoTrade.Core.Logging,
  AlgoTrade.Core.Scripting.
  DIKKAT: System.IO ve AlgoTrade.Core.AppConfig ve AlgoTrade.Core.Timer
  bu listede YOK - Path/File, AppConfigLoader/AppConfigApplier, TimeManager
  kullanan her script bunlari kendi basinda "using" ile eklemeli.
- algoTrader.RunXxxWithProgressAsync() (Single/Multiple/SingleTraderOpt/
  ConfirmingSingle/ConfirmingMultiple) cagiran her script, cagirmadan once
  mutlaka algoTrader.RegisterLogger(LogManager.GetInstance()) ve
  algoTrader.RegisterTimer(TimeManager.GetInstance()) yapmali - yoksa
  AlgoTrader'in ic _timer alani null kalip NullReferenceException verir.
