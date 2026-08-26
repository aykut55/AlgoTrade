# Offline Replay

> Birden fazla **farklı zamanlarda çalıştırılmış** strateji run'ının çıktısını (sinyal/PnL
> serilerini) tek bir pencerede overlay olarak çizmek için — canlı bir `MultipleTrader` run'ı
> DEĞİL, elde zaten var olan (ya da bu amaçla üretilmiş) `.npz` bundle'ları birleştirip
> gösteren, tamamen script-tabanlı bir pipeline. Yazım tarihi: 2026-08-26. Menü karşılığı
> **yok** — bkz. [§6 Neden Menüde Değil](#6-neden-menüde-değil).

## İçindekiler

1. [Ne İşe Yarar](#1-ne-i̇şe-yarar)
2. [Pipeline: 4 Script, Sıralı](#2-pipeline-4-script-sıralı)
3. [Dosya Yerleşimi](#3-dosya-yerleşimi)
4. [playlist.json Formatı](#4-playlistjson-formatı)
5. [EditOfflineReplay.csx — Panel Düzeni Seçimi (choice 0-5)](#5-editofflinereplaycsx--panel-düzeni-seçimi-choice-0-5)
6. [Neden Menüde Değil](#6-neden-menüde-değil)
7. [Bilinen Sınırlamalar](#7-bilinen-sınırlamalar)

---

## 1. Ne İşe Yarar

Elimizde N tane **ayrı ayrı çalıştırılmış** `SingleTrader` run'ı var (farklı stratejiler, farklı
zamanlarda üretilmiş `.npz` bundle'lar) — amaç bunların Signal/PnL serilerini **tek bir OHLC
grafiği üzerinde overlay** olarak görmek (örn. "10 farklı strateji aynı sembol/periyotta ne zaman
AL/SAT demiş, PnL eğrileri nasıl karşılaştı"). Gerçek bir `MultipleTrader` çalıştırmaya gerek yok
— sadece disk'teki bundle'lar birleştirilip çiziliyor.

Kullanılan iki plotter da destekleniyor:
- **Yeni tip (`DearPyGuiDataPlotter`)**: birleştirilmiş `combined.npz` + `.view.json`'ı
  `LoadBundle` ile doğrudan açar.
- **Eski tip (`PythonPlotter`)**: `PlotBundlePlaylist(bundlePaths)` ile N bundle'ı disk'e ara
  dosya yazmadan bellekte okuyup aynı pencerede (multi-trader render yoluyla) çizer.

## 2. Pipeline: 4 Script, Sıralı

Her script **tek bir iş** yapar (üret **XOR** çiz — hiçbiri ikisini birden yapmaz), `[8] Run
Script` ile sırayla çalıştırılır:

| # | Script | Girdi | Çıktı | Ne yapar |
|---|---|---|---|---|
| 1 | `GenerateReplaySampleBundles.csx` | mevcut `Config_01` veri dosyası | `outputs/logs/replay_samples/<Strateji>/bundle.npz` (her strateji kendi klasöründe) | **(opsiyonel, sadece test verisi yoksa)** 10 farklı stratejiyle `SingleTrader`'ı arka planda (Python/pencere yok) çalıştırır. |
| — | *(elle adım)* | `outputs/logs/replay_samples/*` | `inputs/python/offlineReplay/samples/` | Kullanıcı **elle** kopyalar — bilinçli olarak otomatik değil, script'i tekrar deneyip kalıcı playlist verisini bozmasın diye. |
| — | *(elle config)* | — | `inputs/python/offlineReplay/playlist.json` | Hangi bundle'lar, hangi etiket/renkle overlay edilecek — elle düzenlenen JSON, script değil (bkz. [§4](#4-playlistjson-formatı)). |
| 2 | `MergeOfflineReplayPlaylist.csx` | `playlist.json` | `combined.npz` + `combined.view.json` + `input.json` | N bundle'ı tek "combined" bundle'a birleştirir. **SADECE ÜRETİR, çizmez.** |
| 3 | `EditOfflineReplay.csx` *(opsiyonel)* | `combined.npz` (salt okunur) | `edited.npz` + `edited.view.json` + günceller `input.json` | `combined.npz`'deki tüm run'ları `trader[]` dizisine çıkarır; kullanıcı script içindeki DÜZENLE bölümünde hangi trader'ın (olduğu gibi ya da hesaplanmış/dönüştürülmüş haliyle) hangi panele gideceğine karar verir (bkz. [§5](#5-editofflinereplaycsx--panel-düzeni-seçimi-choice-0-5)). `combined.npz`'ye **hiç dokunmaz**. |
| 4 | `RunOfflineReplay.csx` | `input.json` | — (iki plotter penceresi) | `input.json`'ın işaret ettiği bundle/view'i her iki plotter'da da açar. **SADECE ÇİZER**, hiçbir şey üretmez/birleştirmez. Adım 3 atlanırsa adım 2'nin ürettiği varsayılan (tüm N trader tek panelde) görünümü çizer. |

`input.json` her zaman **üretici** script tarafından yazılır (adım 2 ya da adım 3), `RunOfflineReplay.csx`
sadece okur — çizen taraf hiçbir zaman "neyi çizeceğine" kendi karar vermiyor, pointer'ı takip
ediyor.

## 3. Dosya Yerleşimi

`AppSettings.OfflineReplayDir` → `inputs/python/offlineReplay/` — playlist+merge çıktısının ortak
konumu, hiçbir plotter'a "ait" değil, ikisi de buradan tüketiyor.

```
inputs/python/offlineReplay/
├── playlist.json          # elle düzenlenir, git'e COMMIT'li
├── samples/                # GenerateReplaySampleBundles.csx çıktısının elle kopyalandığı yer, git'e COMMIT'siz
├── combined.npz             # MergeOfflineReplayPlaylist.csx üretir, git'e COMMIT'li
├── combined.view.json       # "                                     git'e COMMIT'li
├── edited.npz                # EditOfflineReplay.csx üretir (opsiyonel, fileBaseName'e göre isim değişir)
├── edited.view.json          # "
└── input.json               # merge ya da edit hangisi son çalıştıysa o yazar, git'e COMMIT'li
```

Sadece `samples/` `.gitignore`'da — `GenerateReplaySampleBundles.csx`'in büyük, playlist'ten
bağımsız elle üretilen/kopyalanan örnek verisi olduğu için (kullanıcı kararı, 2026-08-26).
`playlist.json`/`input.json`/`combined.npz`/`combined.view.json` bilerek **commit'li** kalıyor
(`.gitignore:101-104`) — `git ls-files inputs/python/offlineReplay/` ile doğrulandı. `edited.*`
`.gitignore`'da ayrıca listelenmiyor (henüz üretilmiş/commit'lenmiş bir örneği yok).

## 4. playlist.json Formatı

```json
{
  "entries": [
    { "bundle": "inputs/python/offlineReplay/samples/SimpleMostStrategy/bundle.npz", "label": "MOST", "color": [255, 255, 255, 255] },
    { "bundle": "inputs/python/offlineReplay/samples/SimpleRSIStrategy/bundle.npz",  "label": "RSI",  "color": [51, 204, 255, 255] }
  ]
}
```

- `bundle` — `AppSettings.RootDir`'e göre göreli (ya da mutlak) yol.
- `label` — panel/seri isimlerinde kullanılır (`"{label} Signal"` / `"{label} PnL"`); verilmezse
  dosya adından türetilir.
- `color` — `[R, G, B, A]`, verilmezse gri (`[200,200,200,255]`).

Okuma/yazma: `OfflineReplayPlaylist.Load(...)`
(`src/AlgoTrade.Core/Python/DearPyGuiDataPlotter/OfflineReplayPlaylist.cs:85`).

## 5. EditOfflineReplay.csx — Panel Düzeni Seçimi (choice 0-5)

Script içinde `int choice = 0;` değişkeni ile 4 hazır düzenden biri seçilir
(`EditOfflineReplay.csx:88`):

| choice | Ad | Ne yapar |
|---|---|---|
| 0 | varsayılan | Tüm trader'ların sinyalleri TEK panelde + PnL'leri TEK panelde |
| 1 | splitSignals | Sinyaller 3'erli gruplarla ayrı panellere bölünür (PnL yok) |
| 2 | splitPnL | PnL'ler 3'erli gruplarla ayrı panellere bölünür (Signal yok) |
| 3 | mixed | 2 sinyal paneli + 2 PnL paneli, 10 yerine seçili 4 trader'lık alt kümeyle |
| 4, 5 | — | **Henüz tanımlanmadı (TODO)** — şimdilik 0'a düşer |

Her `BuildChoiceN()` yerel fonksiyonu aynı `trader[]` dizisinden besleniyor —
`ViewPanelBuilder.AddSignal`/`AddPnL`/`AddSeries` serbestçe kullanılabiliyor, `AddSeries` ile
**hesaplanmış** bir diziyi de eklemek mümkün (örn. `trader[i].Signal.Select(v => v * 2).ToArray()`).
Kendi düzenini eklemek istersen ilgili `BuildChoiceN()`'in içini düzenlemek ya da 4/5'i doldurmak
yeterli.

OHLC panelindeki AL/SAT işaretleri ayrıca kontrol edilebiliyor (`ohlcSignal`/`includeOhlcSignal`
değişkenleri, `EditOfflineReplay.csx:159-160`): varsayılan `combined.npz`'deki (playlist'in ilk
entry'si ya da çoğunluk-oyu bileşke sinyali) ile aynı kalır; `ohlcSignal = trader[3].Signal` gibi
bir atama ile değiştirilebilir, `includeOhlcSignal = false` ile tamamen kapatılabilir (düz mum
grafiği).

## 6. Neden Menüde Değil

Bilinçli bir tercih — workflow henüz oturmadı:

- `EditOfflineReplay.csx`'te 4-5 numaralı layout seçenekleri hâlâ TODO.
- `playlist.json` elle düzenlenen bir config, arayüzü yok.
- Diğer script'lerde izlenen pattern zaten "önce script'te otur, sonra menüyle senkronla" (bkz.
  [07-menu-vs-script-parity.md](07-menu-vs-script-parity.md) — `[5]`/`[6]`/`[7]` için önce
  script yazılmış, sonra menüyle eşitlenmiş). Offline Replay için de aynı sıra izlenecek: layout
  seçenekleri tamamlanıp playlist akışı oturunca menüye eklemek, önce değil.

## 7. Bilinen Sınırlamalar

Ayrıntılar için `docs/todo.md` — "Offline Replay" başlıklı maddeler:

- **X ekseni senkronu** (öncelik değil, 2026-08-26): yeni tip plotterda paneller bazen senkron
  olmayabiliyor (`[5]`/`[6]` akışlarında gözlemlenmedi) — kesin sebep bulunamadı, ipucu:
  `combined.view.json`'daki sinyal serisi `"source": "indicator"` iken çalışan `latest_bundle.view.json`'da
  `"source": "signalsteps"`.
- **Farklı sembol/periyot overlay desteklenmiyor**: `MergeToBundle` OHLC referansını hep **ilk**
  playlist entry'sinden alıyor, hepsinin aynı sembol/timeframe olduğu varsayılıyor.
- **Runtime'da dinamik panel değiştirme** henüz yok (öncelik değil) — ilk taslak
  `EditOfflineReplay.csx`'te bu şekilde denenmişti, kullanıcı statik (önceden view.json üreten)
  yaklaşımı öncelikli istedi; fikir `docs/todo.md`'de not olarak duruyor.
