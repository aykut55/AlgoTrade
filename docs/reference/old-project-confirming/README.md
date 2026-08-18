# Eski Proje Referansı — "Confirmation Mode" (Sanal Pozisyon Konfirmasyonu)

Bu klasördeki dosyalar **AlgoTrade projesinin bir parçası değil** — eski projeden
(`D:\Aykut\Projects\AlgoTradeWithOptimizationSupport`) 2026-08-18'de kopyalanmış, salt-okunur
referans materyali. Amaç: eski projeye erişim olmadan (örn. farklı bir bilgisayarda) da
`ConfirmingSingleTrader`/`ConfirmingMultipleTrader`/`ConfirmingSingleTraderOptimizer`/
`ConfirmingMultipleTraderOptimizer` sınıflarını yeni projede yazarken referans kaynağa
ulaşabilmek.

## Dosyalar

- **`ConfirmingSingleTrader.cs`** — Eski projedeki **tek çalışan** konfirmasyon implementasyonu
  (`buildConsensusSignal()` içinde eşik kontrolü yapan `MultipleTrader` benzeri sınıf). Bu, yeni
  sınıfların temel referansı olmalı.
- **`ConfirmationMode_Implementation_Plan.md`** — Orijinal tasarım planı (2026-01-21). Bu
  plandaki `SingleTrader`'a gömülü yaklaşım (`ProcessConfirmationMode`/`UpdateSanalPozisyon`/
  `CheckConfirmation`) eski projede **yazıldı ama hiç bağlanmadı** (ölü kod, `Run()`'daki çağrı
  noktası yorum satırında kalmış) — yine de terminoloji ve orijinal niyet için değerli.

## Asıl karar/analiz belgesi

Bu klasördeki dosyalar **ham kaynak**, karar/analiz burada değil. Yeni projeye özel tasarım
kararları, eski projeyle karşılaştırma, açık sorular ve netleşen kararlar için:

→ **`docs/todo.md`**, "Getiri Eğrisi / KarZarar Eğrisi Konfirmasyonu (Madde 3) — Tasarım Fikri"
bölümü.
