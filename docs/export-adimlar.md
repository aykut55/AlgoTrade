# Export Sistemi — Kalan Adımlar

## Durum
- ✅ ADIM 1 — AppConfig.json (Export node düzeltmeleri)
- ✅ ADIM 2 — StatisticsExporterConfig.json (yeni versiyon mimarisi)
- ✅ ADIM 3 — AppConfig.cs (`TraderExportConfig` class + 6 class'a `Export?` property)
- ⏳ ADIM 4 — SingleTrader.cs
- ⏳ ADIM 5 — StatisticsExporter.cs
- ⏳ ADIM 6 — AppConfigApplier.cs

---

## AppConfig.json'daki Export nodu (referans)
```json
"Export": {
  "ExportEnabled": true,
  "ConfigFile": "StatisticsExporterConfig.json",
  "Version": "v1"
}
```
**Not:** `Name` alanı yok — kullanıcı JSON'dan kaldırdı.

## StatisticsExporterConfig.json yeni yapısı (referans)
```json
{
  "SingleTrader": {
    "SingleTraderLists": {
      "v1": { "Name": "...", "Description": "...", "columns": [...] },
      "v2": { "Name": "...", "Description": "...", "columns": [...] }
    },
    "SingleTraderPerformans": {
      "v1": { "columns": [...] },
      "v2": { "columns": [...] }
    },
    "SingleTraderStatistics": {
      "v1": { "columns": [...] }
    }
  },
  "SingleTraderOptimization": { "Full": { ... } }   // değişmez
}
```
**Not:** `SingleTraderLists.v1` = full kolonlar, `SingleTraderLists.v2` = minimal kolonlar (Name alanından anlaşılıyor).

---

## ADIM 4 — SingleTrader.cs
**Dosya:** `src/AlgoTrade.Core/Trading/Traders/SingleTrader.cs`

### Eklenecek property'ler (public property'ler bölümüne):
```csharp
public bool   ExportEnabled    { get; set; } = false;
public string ExportConfigFile { get; set; } = "StatisticsExporterConfig.json";
public string ExportVersion    { get; set; } = "v1";
```

### `WriteStatisticsToFile()` içinde (SaveStatisticsToFile bloğunun sonuna):
```csharp
if (ExportEnabled && !string.IsNullOrEmpty(ExportConfigFile))
{
    exporter.SaveListsToTxtFromConfig(outputPath, ExportConfigFile, ExportVersion);
    exporter.SavePerformansToTxtFromConfig(outputPath, ExportConfigFile, ExportVersion);
}
```
- `outputPath` = mevcut `_saveConfig.PerformansTxtFileName` gibi path'lerden türetilir — tam path'i bulmak için SingleTrader.cs okuna!
- `exporter` = mevcut `StatisticsExporter` nesnesi (nasıl oluşturulduğuna bak)

---

## ADIM 5 — StatisticsExporter.cs
**Dosya:** `src/AlgoTrade.Core/Trading/Utils/StatisticsExporter.cs`

### Mevcut C# model vs yeni JSON yapısı uyumsuzluğu:
- Mevcut: `SingleTraderListsNode.Full` (TraderListProfile) ve `.Minimal` (TraderListProfile)
- Yeni JSON: `SingleTraderLists.v1`, `SingleTraderLists.v2` (string key → dictionary)

### Değiştirilecek C# model sınıfları:

**`SingleTraderListsNode`** → Dictionary tabanlı hale getir:
```csharp
private sealed class SingleTraderListsNode
{
    // Eski yapı (backward compat)
    public TraderListProfile? Full    { get; set; }
    public TraderListProfile? Minimal { get; set; }
    public ListOutputs? listOutputs   { get; set; }
    public ListOutputs? lstOutputs    { get; set; }

    // Yeni yapı: "v1", "v2" vb. string key → version node
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Versions { get; set; }

    public List<ColumnConfig>? GetColumnsByVersion(string version)
    {
        if (Versions != null && Versions.TryGetValue(version, out var el))
        {
            var node = el.Deserialize<VersionedColumnsNode>();
            return node?.columns;
        }
        return null;
    }
}
```

**Yeni sınıf `VersionedColumnsNode`:**
```csharp
private sealed class VersionedColumnsNode
{
    public string? Name        { get; set; }
    public string? Description { get; set; }
    public List<ColumnConfig>? columns { get; set; }
}
```

**`SingleTraderPerformansNode`** — aynı yaklaşım (`[JsonExtensionData]` + `GetColumnsByVersion()`):
```csharp
private sealed class SingleTraderPerformansNode
{
    public TraderListProfile? Full    { get; set; }
    public TraderListProfile? Minimal { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Versions { get; set; }

    public List<ColumnConfig>? GetColumnsByVersion(string version) { ... }
}
```

**`SingleTraderStatisticsNode`** — aynı yaklaşım:
```csharp
private sealed class SingleTraderStatisticsNode
{
    public List<ColumnConfig> columns { get; set; } = new();  // eski yapı

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Versions { get; set; }

    public List<ColumnConfig>? GetColumnsByVersion(string version) { ... }
}
```

### Güncellenen metodlar:

**`TryGetColumns(string version = "v1")`:**
```csharp
public List<ColumnConfig>? TryGetColumns(string version = "v1")
{
    var st = SingleTrader ?? ...;
    var lists = st?.SingleTraderLists ?? st?.SngleTraderLsts;

    // Yeni: version lookup
    var versionCols = lists?.GetColumnsByVersion(version);
    if (versionCols != null && versionCols.Count > 0) return versionCols;

    // Mevcut fallback'ler aynen kalır (Full, Minimal, listOutputs...)
}
```

**`TryGetColumnsFromPerformans(string? profile = null, string version = "v1")`:**
```csharp
// Yeni: version lookup → perf.GetColumnsByVersion(version)
// Mevcut fallback: Full, Minimal profile'ları
```

**`GetEnabledStatisticsColumns(string version = "v1")`:**
```csharp
// Yeni: node.GetColumnsByVersion(version)
// Mevcut fallback: node.columns (eski düz liste)
```

### Güncellenen public metod imzaları:
```csharp
// version parametresi eklenir (default = "v1" → geriye dönük uyum)
public void SaveListsToTxtFromConfig(string filePath, string configPath = "...", string version = "v1")
public void SaveListsToCsvFromConfig(string filePath, string configPath = "...", string version = "v1")
public void SavePerformansToTxtFromConfig(string filePath, string configPath = "...", string profile = "Full", string version = "v1")
public void SavePerformansToCsvFromConfig(string filePath, string configPath = "...", string profile = "Full", string version = "v1")
```
Bu metotlar içinde `cfg.GetEnabledColumns(version)` / `cfg.GetEnabledColumnsFromPerformans(profile, version)` çağrılır.

**Not:** `GetEnabledColumns()` ve `GetEnabledColumnsFromPerformans()` da version parametresi alacak şekilde güncellenir.

---

## ADIM 6 — AppConfigApplier.cs
**Dosya:** `src/AlgoTrade.Core/AppConfig/AppConfigApplier.cs`

### Gerekli ön koşul:
ADIM 4'ten sonra AlgoTrader/SingleTrader'da Export property'leri set eden bir metot gerekir.
Muhtemelen `SetSingleTraderExportConfig()` gibi bir metot eklenecek ya da property'ler doğrudan set edilecek.

### `ApplySingleTrader()` sonuna ekle:
```csharp
if (cfg.Export != null)
{
    algoTrader.SetSingleTraderExportConfig(new SingleTraderExportConfig
    {
        ExportEnabled    = cfg.Export.ExportEnabled,
        ExportConfigFile = Path.Combine(configsDir, cfg.Export.ConfigFile),
        ExportVersion    = cfg.Export.Version,
    });
}
```

### `ApplyMultipleTrader()` — MainTrader:
```csharp
if (cfg.MainTrader.Export != null)
{
    algoTrader.SetSingleTraderExportConfig(new SingleTraderExportConfig
    {
        ExportEnabled    = cfg.MainTrader.Export.ExportEnabled,
        ExportConfigFile = Path.Combine(configsDir, cfg.MainTrader.Export.ConfigFile),
        ExportVersion    = cfg.MainTrader.Export.Version,
    });
}
```

### `ApplyMultipleTrader()` — ChildTrader (SetChildTraderCount lambda içinde):
```csharp
if (child.Export != null)
{
    entry.ExportEnabled    = child.Export.ExportEnabled;
    entry.ExportConfigFile = Path.Combine(configsDir, child.Export.ConfigFile);
    entry.ExportVersion    = child.Export.Version;
}
```
Bu çalışması için `ChildTraderConfigEntry` sınıfına da Export property'leri eklenmesi gerekebilir → bak: `AlgoTrader.cs`

### `ApplySingleTraderOpt()` sonuna ekle:
```csharp
// cfg.Export → optimizer seviyesi export
// cfg.SingleTrader.Export → best trader export
if (cfg.SingleTrader.Export != null)
{
    algoTrader.SetSingleTraderExportConfig(new SingleTraderExportConfig { ... });
}
```

---

## Bağımlılık Sırası
```
ADIM 4 (SingleTrader.cs) → ADIM 5 (StatisticsExporter.cs) → ADIM 6 (AppConfigApplier.cs)
```
ADIM 5, ADIM 4'ten bağımsız uygulanabilir. ADIM 6 her ikisine de bağımlı.

## Kritik Dosyalar
- `src/AlgoTrade.Core/Trading/Traders/SingleTrader.cs` (~2100 satır)
- `src/AlgoTrade.Core/Trading/Utils/StatisticsExporter.cs` (~1500 satır)
- `src/AlgoTrade.Core/AppConfig/AppConfigApplier.cs`
- `src/AlgoTrade.Core/Trading/AlgoTrader.cs` (ChildTraderConfigEntry için)
