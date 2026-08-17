# inputs/input.json semasi

`scripts/default.py` calisirken `inputs/input.json` (yoksa `inputs/src.json`) dosyasini okur ve
oradan hangi `.npz` bundle + `.view.json` view description'in yuklenecegini cozer (bkz.
`loadInputConfig`, `resolveBundlePath`, `resolveViewPath`).

Ornek/sablon icin bkz. `inputs/input.example.json`. Gercek `inputs/input.json` calisma zamaninda
(`runtimeCommandManager.py` tarafindan `load_bundle` komutunda) otomatik uretilir, bu yuzden repoya
commit edilmez (bkz. .gitignore).

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
