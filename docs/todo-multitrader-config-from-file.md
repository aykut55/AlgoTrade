# TODO: MultiTrader Config Dosyalarindan Yukleme Destegi

## Ozet

MultiTrader (`RunMultipleTraderWithProgressAsync`) icin strateji, query ve equity curve filter yapilandirmalarinin
config dosyalarindan yuklenmesi. Su an bu yapilandirmalar sadece kod icerisinde (`Program.cs`) hardcode edilebiliyor.
SingleTrader'daki `ConfigureStrategyFromConfig()` / `ConfigureQueryFromConfig()` benzeri bir yapiyi
MultiTrader tarafina da tasimak istiyoruz.

## Mevcut Durum

### SingleTrader (zaten calisiyior)

SingleTrader icin config dosyasindan yukleme altyapisi mevcut:

```
StrategyConfig.txt  -->  StrategyConfigLoader  -->  algoTrader.ConfigureStrategyFromConfig(path, name, version)
QueryConfig.txt     -->  QueryConfigLoader      -->  algoTrader.ConfigureQueryFromConfig(path, name, version)
OptimizationConfig.txt --> OptimizationConfigLoader --> algoTrader.ConfigureOptimizationFromConfig(path, name, version)
```

Her biri **tek bir** config secer (name + version) ve uygular.

### MultiTrader (simdi hardcode)

MultiTrader ise **birden fazla** config'i ayni anda yukler. Program.cs'te su sekilde:

```csharp
// Program.cs - ConfigureStrategies()
algoTrader.ClearStrategyConfigs();
algoTrader.AddStrategyConfig(0, "SimpleMostStrategy", new Dictionary<string, object>
{
    ["period"] = 21, ["percent"] = 1.0, ["choice"] = 0
});
algoTrader.AddStrategyConfig(1, "SimpleMostStrategy", new Dictionary<string, object>
{
    ["period"] = 14, ["percent"] = 0.5, ["choice"] = 0
});

// Program.cs - ConfigureQueries()
algoTrader.ClearQueryConfigs();
algoTrader.AddQueryConfig(0, "SimpleQuery1", new Dictionary<string, object>
{
    ["ma8Period"] = 8, ["ma200Period"] = 200, ["choice"] = 0
});
algoTrader.AddQueryConfig(1, "SimpleQuery1", new Dictionary<string, object>
{
    ["ma8Period"] = 5, ["ma200Period"] = 100, ["choice"] = 0
});

// Program.cs - ConfigureEquityCurveFilters()
algoTrader.ClearEquityCurveFilterConfigs();
algoTrader.AddEquityCurveFilterConfig(0, enabled: false, ...);
algoTrader.AddEquityCurveFilterConfig(1, enabled: false, ...);
```

AlgoTrader'daki mevcut API:

```csharp
// AlgoTrader.cs
void AddStrategyConfig(int id, string strategyName, Dictionary<string, object> parameters)
void ClearStrategyConfigs()
void AddQueryConfig(int id, string queryName, Dictionary<string, object> parameters)
void ClearQueryConfigs()
void AddEquityCurveFilterConfig(int id, bool enabled, bool thresholdTypeIsPercent, double profitThreshold, double lossThreshold, ConfirmationTrigger trigger)
void ClearEquityCurveFilterConfigs()
```

Entry siniflari:

```csharp
// AlgoTrader.cs alt kisim
public class StrategyConfigEntry { int Id, string StrategyName, Dictionary<string,object> Parameters }
public class QueryConfigEntry { int Id, string QueryName, Dictionary<string,object> Parameters }
public class EquityCurveFilterConfigEntry { int Id, bool Enabled, bool ThresholdTypeIsPercent, double ProfitConfirmationThreshold, double LossConfirmationThreshold, ConfirmationTrigger ConfirmationTrigger }
```

## Hedef

Kullanici config dosyasinda birden fazla entry tanimlayabilsin. Console menusunden (veya script'ten) bu entry'leri
toplu veya secmeli olarak MultiTrader'a yukleyebilelim.

## Yaklasim

### Adim 1: MultiTrader Config Dosya Formati Tasarimi

Yeni bir dosya formati GEREKMEZ. Mevcut `StrategyConfig.txt` ve `QueryConfig.txt` zaten birden fazla entry iceriyor.
Mesela `StrategyConfig.txt`:

```
SimpleMostStrategy|v1-Default|period:int:21|percent:double:1.0|choice:int:0
SimpleMostStrategy|v2-ExmovCross|period:int:21|percent:double:1.0|choice:int:1
SimpleMAStrategy|v1-Default|fastPeriod:int:10|slowPeriod:int:20|choice:int:0
SimpleMAStrategy|v2-Fast|fastPeriod:int:5|slowPeriod:int:13|choice:int:0
SimpleRSIStrategy|v1-Default|period:int:14|oversold:double:30|overbought:double:70
```

Bunlardan birden fazlasini secip MultiTrader'a Id=0, Id=1, ... olarak ekleyebiliriz.

Ayni mantik `QueryConfig.txt` icin de gecerli.

### Adim 2: EquityCurveFilter Config Dosya Formati (YENi)

EquityCurveFilter icin su an config dosyasi yok. Yeni bir dosya formati tanimlanmali:

**Dosya:** `inputs/EquityCurveFilterConfig.txt`

**Onerilen format:**
```
# EquityCurveFilter Configuration
# Format: Enabled:ThresholdTypeIsPercent:ProfitThreshold:LossThreshold:Trigger
# Trigger values: None, Profit, Loss, Both
#
false|true|0.05|-0.05|Both
false|true|0.10|-0.10|Profit
true|false|1000|-1000|Both
```

**Alternatif format (daha okunabilir, pipe + key:value):**
```
# EquityCurveFilter Configuration
# Format: Version|enabled:bool|thresholdType:string|profit:double|loss:double|trigger:string
v1-Default|enabled:bool:false|thresholdType:string:percent|profit:double:0.05|loss:double:-0.05|trigger:string:Both
v2-Aggressive|enabled:bool:true|thresholdType:string:percent|profit:double:0.10|loss:double:-0.10|trigger:string:Profit
```

Hangi formatin secilecegi implemetasyon sirasinda netlestirilecek. Mevcut loader pattern'ine (StrategyConfigLoader gibi)
uyumlu olan tercih edilmeli.

### Adim 3: AlgoTrader'a Yeni Metodlar Eklenmesi

```csharp
// ======== Strategy ========

/// Dosyadaki TUM config'leri MultiTrader'a yukler (Id otomatik atanir: 0, 1, 2, ...)
void ConfigureStrategiesFromConfig(string configFilePath)

/// Dosyadan belirli config'leri secerek yukler
/// selections: [(name, version)] listesi, Id siraya gore atanir
void ConfigureStrategiesFromConfig(string configFilePath, List<(string name, string version)> selections)


// ======== Query ========

/// Dosyadaki TUM config'leri MultiTrader'a yukler
void ConfigureQueriesFromConfig(string configFilePath)

/// Dosyadan belirli config'leri secerek yukler
void ConfigureQueriesFromConfig(string configFilePath, List<(string name, string version)> selections)


// ======== EquityCurveFilter ========

/// Dosyadaki TUM config'leri MultiTrader'a yukler
void ConfigureEquityCurveFiltersFromConfig(string configFilePath)

/// Dosyadan belirli config'leri secerek yukler
void ConfigureEquityCurveFiltersFromConfig(string configFilePath, List<(string name, string version)> selections)
```

**Implementasyon detayi (ornek: ConfigureStrategiesFromConfig):**

```csharp
public void ConfigureStrategiesFromConfig(string configFilePath, List<(string name, string version)>? selections = null)
{
    var loader = new StrategyConfigLoader(configFilePath);
    loader.LoadFromFile();

    List<StrategyConfiguration> configs;
    if (selections is null)
    {
        // Tum config'leri yukle
        configs = loader.GetAllConfigurations();
    }
    else
    {
        // Sadece secilenleri yukle
        configs = new List<StrategyConfiguration>();
        foreach (var (name, version) in selections)
        {
            var config = loader.GetConfiguration(name, version);
            if (config is null)
                throw new InvalidOperationException($"Strategy config not found: {name}|{version}");
            configs.Add(config);
        }
    }

    ClearStrategyConfigs();
    for (int i = 0; i < configs.Count; i++)
    {
        AddStrategyConfig(i, configs[i].StrategyName, configs[i].GetParameterValues());
    }
}
```

### Adim 4: EquityCurveFilterConfigLoader (YENi SINIF)

Mevcut loader'lar ile ayni pattern'de:

**Dosya:** `src/AlgoTrade.Core/Trading/Strategies/EquityCurveFilterConfigLoader.cs`
(veya `Trading/Core/` altinda — projedeki convention'a gore karar verilecek)

```csharp
public class EquityCurveFilterConfiguration
{
    public string Version { get; set; }
    public bool Enabled { get; set; }
    public bool ThresholdTypeIsPercent { get; set; }
    public double ProfitThreshold { get; set; }
    public double LossThreshold { get; set; }
    public ConfirmationTrigger Trigger { get; set; }

    public string GetDisplayString()
    {
        string type = ThresholdTypeIsPercent ? "percent" : "absolute";
        return $"enabled={Enabled}, type={type}, profit={ProfitThreshold}, loss={LossThreshold}, trigger={Trigger}";
    }
}

public class EquityCurveFilterConfigLoader
{
    void LoadFromFile()
    List<EquityCurveFilterConfiguration> GetAllConfigurations()
    EquityCurveFilterConfiguration? GetConfiguration(string version)
    // ... diger metodlar
}
```

### Adim 5: Program.cs — Console Menusu ile MultiTrader Config Secimi

Program.cs'teki `ConfigureStrategies()`, `ConfigureQueries()`, `ConfigureEquityCurveFilters()` metodlari guncellenir.

**Senaryo A: Dosyadaki tum config'leri yukle (basit)**
```csharp
void ConfigureStrategies()
{
    string configPath = Path.Combine(AppSettings.InputsDir, "StrategyConfig.txt");
    if (File.Exists(configPath))
    {
        algoTrader.ConfigureStrategiesFromConfig(configPath); // tumu yuklenir
    }
    else
    {
        // mevcut hardcode fallback
    }
}
```

**Senaryo B: Kullaniciya coklu secim yaptir (gelismis)**

`ShowConfigSelectionMenu` yerine `ShowMultiConfigSelectionMenu` eklenir:

```csharp
List<(string name, string version)>? ShowMultiConfigSelectionMenu(
    string configType,
    List<(string name, string version, string display)> configs)
{
    // Tum config'leri numarali listele
    // Kullanici virgul ile birden fazla numara girebilir: "1,3,5"
    // Veya "all" / bos Enter ile tumu secilir
    // Ornek cikti:
    //
    // Strategy Config Secimi (MultiTrader):
    //   [1] SimpleMostStrategy | v1-Default | period=21, percent=1.0, choice=0
    //   [2] SimpleMostStrategy | v2-ExmovCross | period=21, percent=1.0, choice=1
    //   [3] SimpleMAStrategy   | v1-Default | fastPeriod=10, slowPeriod=20, choice=0
    //   [4] SimpleMAStrategy   | v2-Fast    | fastPeriod=5, slowPeriod=13, choice=0
    //   [5] SimpleRSIStrategy  | v1-Default | period=14, oversold=30, overbought=70
    //
    // Seciminiz (virgul ile, ornek: 1,3 | all=tumunu sec) (default: all):
}
```

Sonra:
```csharp
void ConfigureStrategies()
{
    string configPath = Path.Combine(AppSettings.InputsDir, "StrategyConfig.txt");
    if (File.Exists(configPath))
    {
        var loader = new StrategyConfigLoader(configPath);
        loader.LoadFromFile();
        var allConfigs = loader.GetAllConfigurations();

        var menuItems = allConfigs
            .Select(c => (c.StrategyName, c.Version, c.GetParametersDisplayString()))
            .ToList();

        var selections = ShowMultiConfigSelectionMenu("Strategy", menuItems);
        if (selections is null) return;

        algoTrader.ConfigureStrategiesFromConfig(configPath, selections);
    }
    else { /* fallback */ }
}
```

### Adim 6: MultiTrader'da Strategy-Query-Filter Eslestirme Mantigi

**ONEMLI:** MultiTrader'da her `Id`, bir (Strategy, Query, EquityCurveFilter) uclususunu temsil eder.
Yani Id=0 icin strategy[0] + query[0] + filter[0] birlikte calisir.

Bu nedenle:
- Secilen strategy sayisi = secilen query sayisi = secilen filter sayisi olmali
- Veya: strategy N tane, query M tane ise, N*M kombinasyon uretilir (cartesian product) — bu AlgoTrader'in
  mevcut davranisina bagli, kontrol edilmeli

**Kontrol edilmesi gereken:** `RunMultipleTraderWithProgressAsync()` icinde `_strategyConfigs` ve `_queryConfigs`
nasil eslesiyor? Id bazli mi, index bazli mi, yoksa cartesian product mu?

Bu eslestirme mantigi, kullaniciya menude nasil soru sorulacagini belirler.

## Dosya Degisiklikleri Ozeti

| Dosya | Degisiklik |
|-------|-----------|
| `AlgoTrader.cs` | `ConfigureStrategiesFromConfig()`, `ConfigureQueriesFromConfig()`, `ConfigureEquityCurveFiltersFromConfig()` metodlari |
| `EquityCurveFilterConfigLoader.cs` | **Yeni dosya** — loader sinifi |
| `inputs/EquityCurveFilterConfig.txt` | **Yeni dosya** — ornek config |
| `Program.cs` | `ShowMultiConfigSelectionMenu()` + `ConfigureStrategies/Queries/EquityCurveFilters` guncelleme |

## Implementasyon Sirasi

1. `EquityCurveFilterConfigLoader` sinifini yaz (yeni loader)
2. `inputs/EquityCurveFilterConfig.txt` ornek dosyayi olustur
3. `AlgoTrader.cs`'e `ConfigureStrategiesFromConfig()` ekle
4. `AlgoTrader.cs`'e `ConfigureQueriesFromConfig()` ekle
5. `AlgoTrader.cs`'e `ConfigureEquityCurveFiltersFromConfig()` ekle
6. `Program.cs`'e `ShowMultiConfigSelectionMenu()` ekle
7. `Program.cs`'te `ConfigureStrategies()` guncelle
8. `Program.cs`'te `ConfigureQueries()` guncelle
9. `Program.cs`'te `ConfigureEquityCurveFilters()` guncelle
10. Test: MultiTrader menusunden config dosyasindan coklu secim yap, calistir

## Acik Sorular

1. **Eslestirme mantigi:** MultiTrader Id=0 strategy + Id=0 query mu yoksa cartesian product mu? (RunMultipleTraderWithProgressAsync incelenmeli)
2. **EquityCurveFilter dosya formati:** Mevcut loader pattern'ine (pipe-delimited, type-aware) mi uysun, yoksa daha basit bir format mi?
3. **Config sayisi uyusmazligi:** 3 strateji + 2 query secilirse ne olacak? Hata mi, eksik olanlari default ile mi doldur?
