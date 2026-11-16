using AlgoTrade.Core;
using AlgoTrade.Core.Trading.Core;
using AlgoTrade.Core.Trading.Indicators;
using AlgoTrade.Core.Trading.Indicators.Base;
using AlgoTrade.Core.Trading.Strategy;
using System;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Strategies
{
    /// <summary>
    /// Strategy003_SaatReferansi - gunun referansSaat barindaki fiyatini o gunun
    /// long/short esigi olarak kullanir: fiyat referansin ustundeyse AL (long),
    /// altindaysa SAT (short) - kosul surdukce her bar (state), yon DEGISMEDIKCE
    /// trader yeni islem acmaz (bkz. Strategy002_MaDamar ile ayni "state" felsefesi).
    ///
    /// Kurallar (sabit, mod secimi yok - tek, net bir kural seti):
    /// - Referans: her gun referansSaat barindaki priceSource fiyati (varsayilan 10:00, Close).
    /// - Islem penceresi: pencereBaslangic &lt;= saat &lt; pencereBitis (varsayilan 10:00-18:00).
    ///   Pencere disinda (18:00 dahil) giris yok, acik pozisyon kapatilir.
    /// - Zarar kes YOK (kasitli - kullanicinin acik talebi).
    /// - Kar al: takeProfitTL (varsayilan 2000) - KOMISYON SONRASI net TL hedefi. KarZararFiyatList
    ///   brut oldugu icin (bkz. Strategy002_MaDamar.GidisDonusKomisyonFiyat) esik olarak
    ///   "net hedef + gidis-donus komisyon" verilir.
    /// </summary>
    public class Strategy003_SaatReferansi : BaseStrategy
    {
        public override string Name => "Strategy003 (Saat Referansi)";

        private readonly PriceSource priceSource;
        private readonly double takeProfitTL;

        private double[]? source;
        private double[]? referansSeries;   // plotting icin - o gunun referans fiyati, her barda
        private double gununReferansFiyati;
        private bool referansBelirlendiMi;
        private bool salterUyarisiYazildi;

        public Strategy003_SaatReferansi(List<StockData> data, IndicatorManager indicators,
            PriceSource priceSource = PriceSource.Close, double takeProfitTL = 2000.0,
            string referansSaat = "10:00:00", string pencereBaslangic = "10:00:00", string pencereBitis = "18:00:00")
        {
            this.priceSource  = priceSource;
            this.takeProfitTL = takeProfitTL;

            // Referans/pencere - strateji kurali, Trader salteri degil (bkz. docs/strateji-ayar-kurallari.md).
            triggerTime          = SaatOku(referansSaat, nameof(referansSaat));
            isTriggerTimeEnabled = true;
            startTime             = SaatOku(pencereBaslangic, nameof(pencereBaslangic));
            stopTime              = SaatOku(pencereBitis, nameof(pencereBitis));
            isTimeEnabled         = true;
            isDayEnabled          = false;

            // Zarar kes YOK (kasitli). Kar al her zaman acik, sabit tek mod.
            stopLossExitModeEnabled  = false;
            takeProfitExitModeEnabled = true;
            flatModeEnabled          = false;
            skipModeEnabled          = false;

            Parameters["PriceSource"]      = priceSource;
            Parameters["TakeProfitTL"]     = takeProfitTL;
            Parameters["ReferansSaat"]     = referansSaat;
            Parameters["PencereBaslangic"] = pencereBaslangic;
            Parameters["PencereBitis"]     = pencereBitis;

            Initialize(data, indicators);
        }

        public override void OnInit()
        {
            if (!IsInitialized)
                return;

            source = Indicators.Trend.ResolvePriceSource(priceSource);
            if (source.Length != barCount)
            {
                throw new InvalidOperationException(
                    $"Seri uzunlugu uyusmuyor (barCount={barCount}): source={source.Length}");
            }
            referansSeries = new double[barCount];
        }

        /// <summary>"HH:mm" ya da "HH:mm:ss" metnini TimeOnly'ye cevirir; gecersizse acik hata verir.</summary>
        private static TimeOnly SaatOku(string deger, string parametreAdi)
        {
            var formatlar = new[] { "HH:mm:ss", "HH:mm" };
            if (TimeOnly.TryParseExact(deger?.Trim(), formatlar, System.Globalization.CultureInfo.InvariantCulture,
                                       System.Globalization.DateTimeStyles.None, out var saat))
                return saat;
            throw new ArgumentException($"{parametreAdi} gecersiz saat: '{deger}'. Beklenen bicim HH:mm ya da HH:mm:ss.");
        }

        /// <summary>
        /// Pozisyonun gidis-donus (acilis + kapanis) komisyonu, TL. SingleTrader'in komisyon
        /// formuluyle ayni - bkz. Strategy002_MaDamar.GidisDonusKomisyonFiyat (ayni mantik, bu
        /// stratejiye ozel kopya - ortak bir yardimci yok).
        /// </summary>
        private double GidisDonusKomisyonFiyat()
        {
            if (Trader?.status == null)
                return 0.0;
            bool isMicroLot = Trader.initialTradeParams?.MicroLotSizeEnabled ?? false;
            double volume = isMicroLot
                ? Trader.status.KomisyonVarlikAdedSayisiMicro
                : Trader.status.KomisyonVarlikAdedSayisi;
            return 2.0 * Trader.status.KomisyonCarpan * volume;
        }

        public override TradeSignals OnStep(int currentIndex)
        {
            bool buy        = false;
            bool sell       = false;
            bool takeProfit = false;
            bool flat       = false;

            ResolveRunContext(currentIndex);

            if (source == null)
                return TradeSignals.None;

            if (isFirstOfDay)
            {
                referansBelirlendiMi = false;
                gununReferansFiyati  = 0.0;
            }

            TimeOnly currentTime = times![currentIndex];
            // Bitis HARIC (baslangic <= saat < bitis): bitis barinda pozisyon kapatilir - "18:00'de kapat".
            bool isWithinTimeWindow = !isTimeEnabled || (currentTime >= startTime && currentTime < stopTime);
            bool isTriggerTime      = isTriggerTimeEnabled && currentTime == triggerTime;

            if (isTriggerTime && !referansBelirlendiMi)
            {
                gununReferansFiyati  = source[currentIndex];
                referansBelirlendiMi = true;
            }

            referansSeries![currentIndex] = referansBelirlendiMi ? gununReferansFiyati : double.NaN;

            if (referansBelirlendiMi)
            {
                double currentPrice = source[currentIndex];
                if (currentPrice > gununReferansFiyati)      buy  = true;
                else if (currentPrice < gununReferansFiyati) sell = true;
            }

            if (takeProfitExitModeEnabled && Trader != null)
            {
                // Komisyon SONRASI net TL kar al - bkz. Strategy002_MaDamar.OnStep mod 6 ile ayni gerekce:
                // KarZararFiyatList brut oldugu icin esik "net hedef + gidis-donus komisyon" olarak verilir.
                takeProfit = Trader.karAlZararKes.KarZararFiyatSeviyesindenKarAlHesapla(
                    currentIndex, takeProfitTL + GidisDonusKomisyonFiyat()) != 0;
            }

            // Sinyal gate'i (SimpleMAStrategy/Strategy002_MaDamar ile ayni gerekce).
            if (Trader?.signals?.AlEnabled    == false) buy        = false;
            if (Trader?.signals?.SatEnabled   == false) sell       = false;
            if (Trader?.signals?.KarAlEnabled == false) takeProfit = false;
            if (Trader?.signals?.FlatOlEnabled == false) flat      = false;

            if (isTimeEnabled && !isWithinTimeWindow) { buy = false; sell = false; flat = true; }

            // Strateji bir kapatma/kar al kurali istiyor ama trader salteri kapaliysa bir kez uyar
            // (bkz. docs/strateji-ayar-kurallari.md, Kural 4) - kural sessizce uygulanmamis olmasin.
            if (!salterUyarisiYazildi && Trader?.signals != null)
            {
                salterUyarisiYazildi = true;
                if (isTimeEnabled && Trader.signals.FlatOlEnabled == false)
                    LogWarning($"[{Name}] UYARI: islem penceresi acik ama Trader.FlatOlEnabled=false; 18:00 kapatma UYGULANMAYACAK.");
                if (Trader.signals.KarAlEnabled == false)
                    LogWarning($"[{Name}] UYARI: Trader.KarAlEnabled=false; {takeProfitTL} TL kar al UYGULANMAYACAK.");
            }

            if (flat)       return TradeSignals.Flat;
            if (takeProfit) return TradeSignals.TakeProfit;
            if (buy)        return TradeSignals.Buy;
            if (sell)       return TradeSignals.Sell;
            return TradeSignals.None;
        }

        public override Dictionary<string, double[]>? GetPlotIndicators()
        {
            if (referansSeries == null)
                return null;
            return new Dictionary<string, double[]> { ["Gunun Referansi"] = referansSeries };
        }
    }
}
