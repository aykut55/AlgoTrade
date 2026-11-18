// Diske Yaz asamasi - globals: Trader (AlgoTrader, Hesapla asamasindan)
// return: int (yazilan dosya sayisi - opsiyonel, sadece bilgi icin)
//
// AlgoTrader.WriteTraderDataToFilesAsync - production akisindaki (menu [5]/[6]/[8])
// AYNI adim, AYNI klasor (AppSettings.LogsDir). Ek export eklemek istersen
// (orn. ozel bir CSV) buraya ekleyebilirsin.

await Trader.WriteTraderDataToFilesAsync(Trader.SingleTrader);

return 0;
