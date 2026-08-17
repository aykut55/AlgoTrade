# inputs/input.json semasi

`scripts/default.py` calisirken `inputs/input.json` (yoksa `inputs/src.json`) dosyasini okur ve
oradan hangi `.npz` bundle + `.view.json` view description'in yuklenecegini cozer (bkz.
`loadInputConfig`, `resolveBundlePath`, `resolveViewPath`).

`inputs/input.json`, `inputs/latest_bundle.npz` ve `inputs/latest_bundle.view.json` BILEREK repoya
commitli - `runMe.bat`'in C#'siz, tamamen standalone acilista da gercek veriyle calismasi icin
(C#'siz akista `[9]`/`load_bundle` komutu hic gelmeyecegi icin baska turlu bos panel layout ile acilirdi).

C# calisip `load_bundle` komutu gonderdiginde `runtimeCommandManager.py` bu dosyalarin uzerine
yazar (bkz. `_handleLoadBundle`) - bu YENIDEN OLUSTURULAN hali tekrar commit'lemek (ya da
commit'lememek) kullanicinin tercihine birakilmistir. `full_pipeline_bundle.*` gibi diger
alternatif bundle'lar ve `runtime_commands/` (IPC kuyrugu) hala runtime verisi olarak ignore'da.

## Alanlar

| Alan | Aciklama | Kabul edilen es anlamli anahtarlar |
|---|---|---|
| bundle | `.npz` bundle dosyasinin yolu | `path`, `bundle_path`, `bundlePath`, `source`, `file` |
| view | `.view.json` view description dosyasinin yolu | `view_path`, `viewPath` |

## Yol formati

Hem `bundle` hem `view` icin **relative** ya da **mutlak (absolute)** yol verilebilir
(`_resolvePath`, satir 171-175):

- Relative verilirse `inputs/` klasorune gore cozulur. Ornek: `"latest_bundle.npz"`
- Mutlak verilirse oldugu gibi kullanilir. Ornek: `"D:\\Aykut\\Projects\\AlgoTrade\\src\\DearPyGuiDataPlotter\\inputs\\full_pipeline_bundle.npz"`

`view` verilmezse `bundle`'in adindan otomatik turetilir (`X.npz` -> `X.view.json`).
