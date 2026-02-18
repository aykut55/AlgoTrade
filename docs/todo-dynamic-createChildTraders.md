# TODO: createChildTraders() Dinamik Hale Getirilmesi

## Ozet

`AlgoTrader.createChildTraders()` su an **3 hardcoded child trader** yaratiyor (childId=0, 1, 2).
Her child icin strategy, query ve equity curve filter id bazli ataniyor. Ancak child sayisi kodda sabit.
Config dosyalarindan secilen strategy/query/filter sayisina gore **dinamik** child olusturmasi gerekiyor.

## Mevcut Durum

### createChildTraders() — AlgoTrader.cs (satir ~873)

```csharp
void createChildTraders()
{
    int childId = 0;
    {
        childId = 0;
        var childTrader = new SingleTrader(childId, "childTrader_0", ...);
        strategy = GetStrategy(0);
        query = GetQuery(0);
        SetSingleTraderConfigureEquityCurveFilter(childTrader, childId);
        // ... init, add to multipleTrader
    }
    {
        childId = 1;
        var childTrader = new SingleTrader(childId, "childTrader_1", ...);
        strategy = GetStrategy(1);
        query = GetQuery(1);
        SetSingleTraderConfigureEquityCurveFilter(childTrader, childId);
        // ... init, add to multipleTrader
    }
    {
        childId = 2;
        var childTrader = new SingleTrader(childId, "childTrader_2", ...);
        strategy = GetStrategy(1);  // Not: id=1 tekrar kullanilmis
        query = GetQuery(1);        // Not: id=1 tekrar kullanilmis
        SetSingleTraderConfigureEquityCurveFilter(childTrader, childId);
        // ... init, add to multipleTrader
    }
}
```

### Problem

- Child sayisi sabit 3
- Her child blogu ~50 satir copy-paste
- Config dosyalarindan 2 strategy + 1 query secilirse, 3. child icin `GetStrategy(2)` / `GetQuery(2)` bulunamaz → hata
- Secim sayisi ile child sayisi eslesmiyor

### Mevcut Config Listeleri (id bazli)

Program.cs'ten doldurulan listeler:
- `_strategyConfigs` → `AddStrategyConfig(id, name, params)` veya `ConfigureStrategiesFromConfig(path, selections)`
- `_queryConfigs` → `AddQueryConfig(id, name, params)` veya `ConfigureQueriesFromConfig(path, selections)`
- `_equityCurveFilterConfigs` → `AddEquityCurveFilterConfig(id, ...)` veya `ConfigureEquityCurveFilterFromConfig(path, version, id)`

## Hedef

`createChildTraders()` child sayisini config listelerinden otomatik belirlesin.
Tekrarlayan copy-paste bloklar yerine tek bir dongu olsun.

## Yaklasim

### Adim 1: Child sayisini belirle

En basit yaklasim: `_strategyConfigs.Count` child sayisini belirler.
Query ve filter sayisi eksikse son gecerli config tekrar kullanilir (veya hata verilir).

```csharp
void createChildTraders()
{
    int childCount = _strategyConfigs.Count;

    for (int childId = 0; childId < childCount; childId++)
    {
        var childTrader = new SingleTrader(childId, $"childTrader_{childId}", this.Data, indicators, _logger);

        childTrader.ClearCallbacks()
                   .SetCallbacks(OnSingleTraderReset, OnSingleTraderInit, OnSingleTraderRun,
                                 OnSingleTraderFinal, OnSingleTraderBeforeOrder,
                                 OnSingleTraderNotifySignal, OnSingleTraderAfterOrder,
                                 OnSingleTraderProgress);

        childTrader.RunMode = SingleTraderRunMode;

        // Strategy
        if (childTrader.RunMode == TraderRunMode.TradeOnly || childTrader.RunMode == TraderRunMode.TradeAndQuery)
        {
            strategy = GetStrategy(childId);
            childTrader.SetStrategy(strategy);
        }

        // Query
        if (childTrader.RunMode == TraderRunMode.TradeAndQuery || childTrader.RunMode == TraderRunMode.QueryOnly)
        {
            query = GetQuery(childId);
            childTrader.SetQuery(query);
        }

        childTrader.Reset();

        // Attributes
        childTrader.SymbolName = this.SymbolName;
        childTrader.SymbolPeriod = this.SymbolPeriod;
        childTrader.LastExecutionTime = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
        childTrader.LastExecutionTimeStart = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

        // Position sizing
        childTrader.initialTradeParams!.Reset()
            .SetBakiyeParams(ilkBakiye: 100000.0)
            .SetKontratParamsViopEndex(kontratSayisi: 1)
            .SetKomisyonParams(komisyonCarpan: 20.0)
            .SetKaymaParams(kaymaMiktari: 0.5);

        // User flags
        OnApplyUserFlags(childTrader);

        // Equity curve filter
        SetSingleTraderConfigureEquityCurveFilter(childTrader, childId);

        // Statistics
        childTrader.SaveStatisticsToFile = true;

        childTrader.Init();
        multipleTrader.AddTrader(childTrader);
    }
}
```

### Adim 2: Config sayisi uyusmazligi stratejisi

Uc seceneK:

**A) Strict (hata ver):**
```csharp
if (_strategyConfigs.Count != _queryConfigs.Count || _strategyConfigs.Count != _equityCurveFilterConfigs.Count)
    throw new InvalidOperationException("Strategy, Query ve EquityCurveFilter config sayilari esit olmali.");
```

**B) Flexible (eksik olani son gecerli ile doldur):**
```csharp
int strategyId = Math.Min(childId, _strategyConfigs.Count - 1);
int queryId = Math.Min(childId, _queryConfigs.Count - 1);
int filterId = Math.Min(childId, _equityCurveFilterConfigs.Count - 1);
```

**C) Cartesian product (tum kombinasyonlar):**
Ornek: 3 strategy x 2 query = 6 child trader
Bu daha karisik ama kapsamli optimizasyon icin faydali olabilir.

Hangi strateji secilecegi implementasyon sirasinda netlestirilecek.

### Adim 3: Program.cs tarafinda validasyon

`runMultipleTraderAlgoTrade()` icinde, config'ler yuklendikten sonra uyari/bilgi vermek:

```csharp
ConfigureStrategies();    // N adet
ConfigureQueries();       // M adet
ConfigureEquityCurveFilters();  // K adet

// Bilgi
LogManager.LogRaw($"\nMultiTrader config: {algoTrader.StrategyConfigs.Count} strategy, " +
                  $"{algoTrader.QueryConfigs.Count} query, " +
                  $"{algoTrader.EquityCurveFilterConfigs.Count} filter");
```

## Dosya Degisiklikleri

| Dosya | Degisiklik |
|-------|-----------|
| `AlgoTrader.cs` | `createChildTraders()` → dongu bazli dinamik |
| `Program.cs` | `runMultipleTraderAlgoTrade()` → config sayisi bilgi/validasyon |

## Bagimliliklr

- `ConfigureStrategiesFromConfig()` — tamamlandi
- `ConfigureQueriesFromConfig()` — tamamlandi
- `ConfigureEquityCurveFilterFromConfig()` — tamamlandi
- `SetSingleTraderConfigureEquityCurveFilter(trader, id)` — tamamlandi

## Acik Sorular

1. **Config sayisi uyusmazligi:** Strict mi, flexible mi, yoksa cartesian product mi?
2. **Position sizing:** Su an hardcoded (`ilkBakiye: 100000`, `kontratSayisi: 1`). Ileride bu da config'e alinacak mi?
3. **Commented-out attribute'lar:** `SystemId`, `SystemName`, `StrategyId`, `StrategyName`, `QueryId`, `QueryName` — bunlar kaldirilacak mi, yoksa dinamik olarak doldurulacak mi?
