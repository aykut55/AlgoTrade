# SingleTrader Tam Akis

## Program.cs → AlgoTrader → SingleTrader → Statistics → Dosyalar

```
runAlgoTrade() [Program.cs]
  ├─ ConfigureStrategy()          → StrategyConfig.txt'den menu ile sec
  ├─ ConfigureQuery()             → QueryConfig.txt'den menu ile sec
  ├─ ConfigureEquityCurveFilter() → EquityCurveFilterConfig.txt'den menu ile sec
  ├─ algoTrader.Initialize()
  └─ algoTrader.RunSingleTraderWithProgressAsync()
       │
       ├─ [Run] her bar icin singleTrader.Run(i)
       │
       ├─ [Screening] TaramaOzeti (Finalize oncesi erisilebilir)
       │    ├─ SonYon
       │    ├─ SonSinyaldenBeriBarSayisi
       │    ├─ SonKarZararFiyat
       │    └─ SonKarZararYuzde
       │
       ├─ [Finalize] singleTrader.Finalize()
       │    ├─ OnFinal callback (mode 0 - before)
       │    ├─ CalculateStatistics()
       │    │    └─ statistics.Hesapla(lastBarIndex)
       │    │         ├─ ReadValues()
       │    │         ├─ Zaman istatistikleri (ay, gun, saat bazli)
       │    │         ├─ Maximum Drawdown hesapla
       │    │         ├─ Min/Max degerler (kar, zarar, bakiye)
       │    │         ├─ Performans metrikleri (ProfitFactor, WinRate vs.)
       │    │         ├─ GetiriIstatistikleriHesapla() — TODO: bos
       │    │         ├─ AssignToMap()         → full stats map
       │    │         └─ AssignToMapMinimal()  → minimal stats map
       │    ├─ SorguOzeti olustur (TradeAndQuery/QueryOnly modlarinda)
       │    └─ OnFinal callback (mode 1 - after)
       │
       └─ [Save] if (!IsStopRequested && SaveStatisticsToFile)
            └─ WriteStatisticsToFile(AppSettings.LogsDir)
                 ├─ SingleTraderStatistics.txt                (full, key-value)
                 ├─ SingleTraderStatistics.csv                (full, CSV)
                 ├─ SingleTraderStatisticsMinimal.txt         (minimal, key-value)
                 ├─ SingleTraderStatisticsMinimal.csv         (minimal, CSV)
                 ├─ SingleTraderLists.txt                     (bar-by-bar, tum kolonlar)
                 ├─ SingleTraderLists.csv                     (bar-by-bar, tum kolonlar, CSV)
                 ├─ SingleTraderListsMinimal.txt              (bar-by-bar, onemli kolonlar)
                 ├─ SingleTraderListsMinimal.csv              (bar-by-bar, onemli kolonlar, CSV)
                 ├─ SingleTraderStatisticsFormatted.txt       (full, kutu cizimli guzel format)
                 └─ SingleTraderStatisticsFormattedMinimal.txt (minimal, kutu cizimli)
```

## Dosya Konumlari

| Ayar | Deger |
|------|-------|
| BaseDir | Proje koku (AppContext.BaseDirectory'den 4 ust dizin) |
| InputsDir | `{BaseDir}/inputs/` |
| OutputsDir | `{BaseDir}/outputs/` |
| LogsDir | `{BaseDir}/outputs/logs/` |

Tum cikti dosyalari `outputs/logs/` altina yazilir.

## WriteStatisticsToFile() Parametreleri

```csharp
public void WriteStatisticsToFile(
    string outputDir,
    bool saveFullStatsTxt = true,              // → SingleTraderStatistics.txt
    bool saveFullStatsCsv = true,              // → SingleTraderStatistics.csv
    bool saveMinimalStatsTxt = true,           // → SingleTraderStatisticsMinimal.txt
    bool saveMinimalStatsCsv = true,           // → SingleTraderStatisticsMinimal.csv
    bool saveFullListsTxt = true,              // → SingleTraderLists.txt
    bool saveFullListsCsv = true,              // → SingleTraderLists.csv
    bool saveMinimalListsTxt = true,           // → SingleTraderListsMinimal.txt
    bool saveMinimalListsCsv = true,           // → SingleTraderListsMinimal.csv
    bool saveFullStatsTxtFormatted = true,     // → SingleTraderStatisticsFormatted.txt
    bool saveMinimalStatsTxtFormatted = true)  // → SingleTraderStatisticsFormattedMinimal.txt
```

Default: 10 boolean hepsi `true` → **10 dosya** uretilir.

## Statistics.cs — Save Metodlari

### Istatistik Dosyalari (tek satirlik ozet bilgiler)

| Metod | Dosya | Format | Icerik |
|-------|-------|--------|--------|
| `SaveToTxt()` | SingleTraderStatistics.txt | Key = Value | Tum istatistikler |
| `SaveToCsv()` | SingleTraderStatistics.csv | Key;Value (CSV) | Tum istatistikler |
| `SaveToTxtMinimal()` | SingleTraderStatisticsMinimal.txt | Key = Value | Onemli istatistikler |
| `SaveToCsvMinimal()` | SingleTraderStatisticsMinimal.csv | Key;Value (CSV) | Onemli istatistikler |
| `SaveToTxtFormatted()` | SingleTraderStatisticsFormatted.txt | Kutu cizimli | Tum istatistikler, bolumlu |
| `SaveToTxtMinimalFormatted()` | SingleTraderStatisticsFormattedMinimal.txt | Kutu cizimli | Onemli istatistikler, bolumlu |

### Liste Dosyalari (bar-by-bar detay)

| Metod | Dosya | Format | Icerik |
|-------|-------|--------|--------|
| `SaveListsToTxt()` | SingleTraderLists.txt | Sabit genislik tablo | Tum kolonlar |
| `SaveListsToCsv()` | SingleTraderLists.csv | CSV (;) | Tum kolonlar |
| `SaveListsToTxtMinimal()` | SingleTraderListsMinimal.txt | Sabit genislik tablo | Onemli kolonlar |
| `SaveListsToCsvMinimal()` | SingleTraderListsMinimal.csv | CSV (;) | Onemli kolonlar |

### Liste Kolonlari

**Full (tum kolonlar):**
BarNo, Date, Time, Open, High, Low, Close, Volume, Yon, Seviye, Sinyal, KarZararPuan, KarZararFiyat, KarZararYuzde, KarAl, ZararKes, IzleyenStop, IslemSayisi, AlisSayisi, SatisSayisi, FlatSayisi, PassSayisi, KontratSayisi, VarlikAdedSayisi, Komisyon, BakiyePuan, BakiyeFiyat, GetiriPuan, GetiriFiyat, BakiyeNet, GetiriNet, EmirKomut, EmirStatus, IsTradeEnabled, IsPozKapatEnabled

**Minimal (onemli kolonlar):**
BarNo, Date, Time, Open, High, Low, Close, Volume, Yon, Seviye, Sinyal, KarZarar, Bakiye, Getiri, Komisyon, BakiyeNet, GetiriNet, IslemSayisi, EmirKomut, EmirStatus, IsTradeEnabled, IsPozKapatEnabled

## Bilinen Sorunlar / Iyilestirme Alanlari

1. **10 boolean parametre** — bir options/flags objesi veya enum ile degistirilmeli
2. **Dosya isimleri sabit** — trader id/ismi yok, MultiTrader'da ayni isimle ustu yazilir
3. **GetiriIstatistikleriHesapla()** — body bos (TODO: periodic return calculations)
4. **Cok fazla dosya** — 10 dosya default uretiliyor, hangilerinin gercekten gerekli oldugu belirlenmeli
5. **Full vs Minimal vs Formatted** — 3 varyant x 2 format (txt/csv) = kombinasyon patlamasi
