# Configure Query Dynamically - Yapilacaklar

## Hedef
- Strategy mantigina paralel olarak Query mantigini da dinamik calistirmak.
- Query iki sekilde konfigure edilebilsin:
  1. Koddan (`ConfigureQuery`)
  2. Dosyadan (`inputs/QueryConfig.txt`)
- Query, `AlgoTrader -> SingleTrader` akisinda her bar calisarak sutun bazli sonuc uretebilsin.

## Asamalar ve Durum
- [x] Asama 1: Mevcut Query kodlarini inceleme ve proje namespace/altyapi uyumu saglama. (yapildi)
- [x] Asama 2: Query icin registry/factory yapisini ekleme (`QueryRegistry`). (yapildi)
- [x] Asama 3: Query config dosyasi parser'ini ekleme (`QueryConfigLoader`) ve `inputs/QueryConfig.txt` olusturma. (yapildi)
- [x] Asama 4: `AlgoTrader` icine `ConfigureQuery` + `ConfigureQueryFromConfig` + lifecycle (create/dispose/reset) ekleme. (yapildi)
- [x] Asama 5: `SingleTrader` icine query assign/execute ve sonuclarin satir/sutun bazli tutulmasi ekleme. (yapildi)
- [x] Asama 6: `Program.cs` seviyesinde query konfigrasyonu (dosya veya kod fallback) baglama. (yapildi)
- [ ] Asama 7: Derleme dogrulamasi ve kalan compile/runtime sorunlarini temizleme.

## Notlar
- Query, strategy sinyali uretmez; bunun yerine bar bazinda sutun degerleri uretir.
- Simdilik query sonuclari `SingleTrader.QueryResults` listesine satir bazinda yazilir, son satir ozeti loglanir.
