# ChartStudies

`ChartStudy`, strateji çalıştırmadan grafik üzerinde indikatörleri birlikte
incelemek için kullanılan bağımsız katmandır.

- `IChartStudy` ve `BaseChartStudy`: çalışma sözleşmesi.
- `ChartStudyRegistry`: somut çalışmaları reflection ile otomatik bulur.
- `ChartStudyBundleConverter`: ham OHLC verisini plotter bundle'ına çevirir.
- `IndicatorDashboardChartStudy`: EMA, MACD, RSI, Stochastic ve Momentum demosu.

Console menüsündeki **28** mevcut veriyi, **29** ise önce veri okuyup çalışmayı
açar. Seçilen çalışma, Dear ImGui plotter'ın `scripts` klasöründe düzenlenebilir
bir Python dosyasına yalnızca ilk kullanımda dönüştürülür. Kullanıcı dosyası
sonraki açılışlarda ezilmez. Açık uygulamadaki **Script Panel > Run**, mevcut
OHLC üzerinde indikatörleri yeniden hesaplayıp panel düzenini yeniden kurar.
