# Strateji ve Ayar Kuralları

> Amaç: Aynı strateji SingleTrader (menü 5), MultipleTrader, SingleTraderOpt, Confirming trader'lar ve
> tarayıcılarda çalıştırıldığında **aynı şekilde davransın**. Aylar sonra bir rapora bakınca neyin
> uygulandığı tek yerden anlaşılsın. Config'i unutmak sonucu sessizce değiştirmesin.

## Kural 1: Stratejinin fikri stratejide durur

Stratejinin "nasıl işlem yaptığını" tanımlayan her şey **strateji parametresidir**:

- Sinyal / yön modu, MA periyotları, tetik
- Pozisyon boşken giriş yöntemi (state / kesişim / tetik)
- Kâr al eşiği, zarar kes (varsa)
- Stratejinin kendi saat kuralları (örn. "09:25-10:00 giriş yok", "vade günü 18:55'te kapat")

Değerleri `inputs/configs/StrategyConfig.txt`'de **hazır ayar** olarak tutulur (`StratejiAdı|v1|...`).
Bütün çalıştırma şekilleri aynı hazır ayarı okur.

## Kural 2: Çalıştırma config'lerinde yalnızca ortam durur

`AppConfig.json` ve `inputs/scripts/Config_0x_*.csx` şunları tutar:

- Veri dosyası ve tarih aralığı (`ReadData`)
- Kontrat, lot, komisyon, kayma (`TradeParams`)
- Çıktı dosyaları
- **Şalterler** (`AlEnabled`, `SatEnabled`, `FlatOlEnabled`, `KarAlEnabled`, `ZararKesEnabled`):
  **varsayılan olarak hep açık.** Kapatmak yalnızca bilinçli bir deney içindir ve o deneyin notunda yazar.
- Opt'ta: yalnızca **taranacak parametre aralıkları.** Kuralın kendisi değil.

`ReadData` gibi **genel** ayarlar bütün menüleri etkiler. Geçici değiştirilirse eski değer yanına
yorum olarak yazılır, iş bitince geri alınır.

## Kural 3: Bir kural tek yerde

Strateji, config'teki bir ayarı gizlice **ezmez.** Bir kural ya stratejide ya config'tedir, ikisinde birden değil.
Config'teki şalterler aslına sadık kalır (örn. `GunSonuPozKapatEnabled = false`, `TimeFilteringEnabled = false`).
Stratejiye özgü gün sonu kapatma ve işlem penceresi **strateji parametresidir**
(örnek: `Strategy002_MaDamar` → `gunSonuKapat`, `islemPenceresi`, `pencereBaslangic`, `pencereBitis`).
Strateji bunları `Flat` sinyaliyle uygular, bu yüzden trader'da `FlatOlEnabled` açık olmalı. Kapalıysa
strateji log'a uyarı yazar.

`StrategyConfig.txt` notu: Parametreler `ad:tür:değer` biçiminde. Yalnızca ilk iki `:` ayırıcıdır
(`StrategyConfigLoader`, `Split(':', 3)`), değer `:` içerebilir. Saatler metin olarak yazılır:
`pencereBaslangic:string:10:00:50` (`HH:mm` ya da `HH:mm:ss`).

## Kural 4: Rapor neyin uygulandığını söyler (yapılacak)

Her koşunun istatistik raporunda bir **"ETKİN AYARLAR"** bölümü olacak:
- Strateji adı, hazır ayar sürümü ve bütün parametreleri
- Trader şalterleri, gün sonu kapatma, zaman filtresi (ve penceresi), equity filtresi
- **Uyarı:** Strateji bir kural istediği halde şalter kapalıysa (örn. `takeProfitTL > 0` ama
  `KarAlEnabled = false`), raporda "UYGULANMADI" yazar.

## Kural 5: Her deney bir hazır ayar + bir satır not

Yeni bir kombinasyon denendiğinde:
1. `StrategyConfig.txt`'ye yeni sürüm satırı eklenir (`v3`, `v4`…).
2. İlgili çalışma kaydına tek satır eklenir: "v3: v1 + vade günü 18:55 kapanış. Sonuç: …".

Böylece hangi sonucun hangi ayardan geldiği hep izlenebilir kalır.

## Uygulama durumu

- [ ] Kural 4: rapora "ETKİN AYARLAR" bölümü (şimdilik `Strategy002_MaDamar` uyarıyı log'a yazıyor)
- [x] `Strategy002_MaDamar`: gün sonu kapatma ve işlem penceresi strateji parametresi oldu
- [x] `Config_03` optChoice 27'deki trader gün sonu ve zaman filtresi ayarları kaldırıldı. `optChoice` varsayılanı 1'e döndü
- [x] `AppConfig.json` oturum öncesi haline döndü
- [ ] Vade günü kuralı (çift ayın son iş günü 18:55 kapat), gerekirse strateji parametresi olarak
