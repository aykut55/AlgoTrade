using AlgoTrade.Core.Trading;
using AlgoTrade.Core.Trading.Utils;
using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Core
{
    /// <summary>
    /// Take Profit and Stop Loss - Manages profit taking and loss cutting levels
    /// Kar Al (Take Profit) and Zarar Kes (Stop Loss) management
    /// Python'dan C#'a çevrilmiştir
    /// </summary>
    public class KarAlZararKes
    {
        #region Properties

        private SingleTrader Trader { get; set; }

        #endregion

        #region Constructor

        public KarAlZararKes()
        {
            Trader = null;
        }

        #endregion

        #region Initialization Methods

        public KarAlZararKes SetTrader(SingleTrader trader)
        {
            Trader = trader;
            return this;
        }

        public KarAlZararKes Init()
        {
            return this;
        }

        public KarAlZararKes Reset()
        {
            return this;
        }

        public KarAlZararKes InitOrReuse()
        {
            return this;
        }

        #endregion

        #region Kar Al (Take Profit) Methods - Yüzde

        /// <summary>
        /// Son fiyata göre kar al yüzde hesapla
        /// </summary>
        public int SonFiyataGoreKarAlYuzdeHesapla(int barIndex, double karAlYuzdesi = 2.0)
        {
            int result = 0;
            int i = barIndex;

            if (Trader.flags.KarAlYuzdeHesaplaEnabled)
            {
                if (Trader.is_son_yon_a() &&
                    (Trader.Data[i].Close > Trader.signals.SonFiyat * (1.0 + karAlYuzdesi * 0.01)))
                {
                    result = 1;
                }
                else if (Trader.is_son_yon_s() &&
                         (Trader.Data[i].Close < Trader.signals.SonFiyat * (1.0 - karAlYuzdesi * 0.01)))
                {
                    result = -1;
                }
            }

            return result;
        }

        /// <summary>
        /// Son fiyata göre kar al yüzde hesapla - seviyeli
        /// </summary>
        public int SonFiyataGoreKarAlYuzdeHesaplaSeviyeli(int barIndex, int seviyeBas = 2, int seviyeSon = 10, double carpan = 0.01)
        {
            int result = 0;
            int i = barIndex;

            if (Trader.flags.KarAlYuzdeHesaplaEnabled)
            {
                bool karAl = false;

                if (Trader.is_son_yon_a())
                {
                    for (int m = seviyeBas; m < seviyeSon; m++)
                    {
                        karAl = karAl || AsagiKestiClose(i, Trader.signals.SonFiyat * (1.0 + m * carpan));
                        if (karAl)
                            break;
                    }
                    if (karAl) result = 1;
                }
                else if (Trader.is_son_yon_s())
                {
                    for (int m = seviyeBas; m < seviyeSon; m++)
                    {
                        karAl = karAl || YukarıKestiClose(i, Trader.signals.SonFiyat * (1.0 - m * carpan));
                        if (karAl)
                            break;
                    }
                    if (karAl) result = -1;
                }
            }

            return result;
        }

        #endregion

        #region Zarar Kes (Stop Loss) Methods - Yüzde

        /// <summary>
        /// Son fiyata göre zarar kes yüzde hesapla
        /// </summary>
        public int SonFiyataGoreZararKesYuzdeHesapla(int barIndex, double zararKesYuzdesi = -1.0)
        {
            int result = 0;
            int i = barIndex;
            double zararKesYuzdesi_ = -1.0 * zararKesYuzdesi;

            if (Trader.flags.ZararKesYuzdeHesaplaEnabled)
            {
                if (Trader.is_son_yon_a() &&
                    (Trader.Data[i].Close < Trader.signals.SonFiyat * (1.0 - zararKesYuzdesi_ * 0.01)))
                {
                    result = 1;
                }
                else if (Trader.is_son_yon_s() &&
                         (Trader.Data[i].Close > Trader.signals.SonFiyat * (1.0 + zararKesYuzdesi_ * 0.01)))
                {
                    result = -1;
                }
            }

            return result;
        }

        /// <summary>
        /// Son fiyata göre zarar kes yüzde hesapla - seviyeli
        /// </summary>
        public int SonFiyataGoreZararKesYuzdeHesaplaSeviyeli(int barIndex, int seviyeBas = -2, int seviyeSon = -10, double carpan = 0.01)
        {
            int result = 0;
            int i = barIndex;
            int seviyeBas_ = -1 * seviyeBas;
            int seviyeSon_ = -1 * seviyeSon;

            if (Trader.flags.ZararKesYuzdeHesaplaEnabled)
            {
                bool zararKes = false;

                if (Trader.is_son_yon_a())
                {
                    for (int m = seviyeBas_; m < seviyeSon_; m++)
                    {
                        zararKes = zararKes || AsagiKestiClose(i, Trader.signals.SonFiyat * (1.0 - m * carpan));
                        if (zararKes)
                            break;
                    }
                    if (zararKes) result = 1;
                }
                else if (Trader.is_son_yon_s())
                {
                    for (int m = seviyeBas_; m < seviyeSon_; m++)
                    {
                        zararKes = zararKes || YukarıKestiClose(i, Trader.signals.SonFiyat * (1.0 + m * carpan));
                        if (zararKes)
                            break;
                    }
                    if (zararKes) result = -1;
                }
            }

            return result;
        }

        #endregion

        #region Kar Al (Take Profit) Methods - Seviye

        /// <summary>
        /// Son fiyata göre kar al seviye hesapla
        /// </summary>
        public int SonFiyataGoreKarAlSeviyeHesapla(int barIndex, double karAlSeviyesi = 2000.0)
        {
            int result = 0;
            int i = barIndex;

            if (Trader.flags.KarAlSeviyeHesaplaEnabled)
            {
                result = YukarıKestiKarZararFiyat(i, karAlSeviyesi) ? 1 : 0;
            }

            return result;
        }

        /// <summary>
        /// Son fiyata göre kar al seviye hesapla - seviyeli
        /// </summary>
        public int SonFiyataGoreKarAlSeviyeHesaplaSeviyeli(int barIndex, int seviyeBas = 5, int seviyeSon = 50, int carpan = 1000)
        {
            int result = 0;
            int i = barIndex;

            if (Trader.flags.KarAlSeviyeHesaplaEnabled)
            {
                bool karAl = false;

                for (int m = seviyeBas; m < seviyeSon; m++)
                {
                    karAl = karAl || AsagiKestiKarZararFiyat(i, m * carpan);
                    if (karAl)
                        break;
                }

                if (karAl) result = 1;
            }

            return result;
        }

        #endregion

        #region Zarar Kes (Stop Loss) Methods - Seviye

        /// <summary>
        /// Son fiyata göre zarar kes seviye hesapla
        /// </summary>
        public int SonFiyataGoreZararKesSeviyeHesapla(int barIndex, double zararKesSeviyesi = -1000.0)
        {
            int result = 0;
            int i = barIndex;

            if (Trader.flags.ZararKesSeviyeHesaplaEnabled)
            {
                result = AsagiKestiKarZararFiyat(i, zararKesSeviyesi) ? 1 : 0;
            }

            return result;
        }

        /// <summary>
        /// Son fiyata göre zarar kes seviye hesapla - seviyeli
        /// </summary>
        public int SonFiyataGoreZararKesSeviyeHesaplaSeviyeli(int barIndex, int seviyeBas = -1, int seviyeSon = -10, int carpan = 1000)
        {
            int result = 0;
            int i = barIndex;

            if (Trader.flags.ZararKesSeviyeHesaplaEnabled)
            {
                bool zararKes = false;

                for (int m = seviyeBas; m > seviyeSon; m--)  // Python'da range(SeviyeBas, SeviyeSon, -1)
                {
                    zararKes = zararKes || AsagiKestiKarZararFiyat(i, m * carpan);
                    if (zararKes)
                        break;
                }

                if (zararKes) result = 1;
            }

            return result;
        }

        #endregion

        #region Trailing Stop (İzleyen Stop) Methods

        /// <summary>
        /// İzleyen stop güncelle - Trailing stop tracking
        /// Long pozisyonda en yüksek fiyatı, short pozisyonda en düşük fiyatı takip eder
        /// Return: 0 = Devam, 1 = Long stop tetiklendi, -1 = Short stop tetiklendi
        /// </summary>
        public int IzleyenStopGuncelle(int barIndex, double izleyenStopYuzdesi)
        {
            int result = 0;
            int i = barIndex;

            if (!Trader.flags.IzleyenStopYuzdeHesaplaEnabled)
                return result;

            double currentPrice = Trader.Data[i].Close;

            if (Trader.is_son_yon_a())  // Long pozisyon
            {
                // Yeni rekor mu? Güncelle
                if (currentPrice > Trader.signals.IzleyenStopEnYuksekFiyat)
                {
                    Trader.signals.IzleyenStopEnYuksekFiyat = currentPrice;
                }

                // Stop seviyesini hesapla (en yüksek fiyattan yüzde aşağıda)
                double stopLevel = Trader.signals.IzleyenStopEnYuksekFiyat * (1.0 - izleyenStopYuzdesi / 100.0);
                Trader.lists.IzleyenStopList[i] = stopLevel;

                // Fiyat stop seviyesinin altına düştü mü?
                if (currentPrice < stopLevel)
                {
                    result = 1;  // Stop tetiklendi!
                }
            }
            else if (Trader.is_son_yon_s())  // Short pozisyon
            {
                // İlk kez veya yeni düşük rekor mu? Güncelle
                if (Trader.signals.IzleyenStopEnDusukFiyat == 0 || currentPrice < Trader.signals.IzleyenStopEnDusukFiyat)
                {
                    Trader.signals.IzleyenStopEnDusukFiyat = currentPrice;
                }

                // Stop seviyesini hesapla (en düşük fiyattan yüzde yukarıda)
                double stopLevel = Trader.signals.IzleyenStopEnDusukFiyat * (1.0 + izleyenStopYuzdesi / 100.0);
                Trader.lists.IzleyenStopList[i] = stopLevel;

                // Fiyat stop seviyesinin üstüne çıktı mı?
                if (currentPrice > stopLevel)
                {
                    result = -1;  // Stop tetiklendi!
                }
            }

            return result;
        }

        #endregion

        #region Kar Al / Zarar Kes - KarZarar Yüzdesi Bazlı

        /// <summary>
        /// KarZararFiyatYuzdeList'teki anlık kar yüzdesine göre kar al kontrolü
        /// Pozisyon açıldıktan sonra anlık kar yüzdesi hedefe ulaştıysa kar al sinyali verir
        /// </summary>
        /// <param name="barIndex">Bar index</param>
        /// <param name="karAlYuzdesi">Hedef kar yüzdesi (örn: 3.0 = %3)</param>
        /// <returns>0 = Bekle, 1 = Long kar al, -1 = Short kar al</returns>
        public int KarZararYuzdesindenKarAlHesapla(int barIndex, double karAlYuzdesi = 3.0)
        {
            int result = 0;
            int i = barIndex;

            if (!Trader.flags.KarAlYuzdeHesaplaEnabled)
                return result;

            // Anlık kar yüzdesi hedef kar yüzdesine ulaştı mı?
            double anlikKarZararYuzdesi = Trader.lists.KarZararFiyatYuzdeList[i];

            if (anlikKarZararYuzdesi >= karAlYuzdesi)
            {
                Trader.lists.KarAlList[i] = anlikKarZararYuzdesi;

                if (Trader.is_son_yon_a())
                {   
                    result = 1;  // Long pozisyon kar al
                }
                else if (Trader.is_son_yon_s())
                {                    
                    result = -1;  // Short pozisyon kar al
                }
            }

            return result;
        }

        /// <summary>
        /// KarZararFiyatYuzdeList'teki anlık zarar yüzdesine göre zarar kes kontrolü
        /// Pozisyon açıldıktan sonra anlık zarar yüzdesi hedefe ulaştıysa zarar kes sinyali verir
        /// </summary>
        /// <param name="barIndex">Bar index</param>
        /// <param name="zararKesYuzdesi">Hedef zarar yüzdesi (örn: -2.0 = %-2, yani %2 zarar)</param>
        /// <returns>0 = Bekle, 1 = Long zarar kes, -1 = Short zarar kes</returns>
        public int KarZararYuzdesindenZararKesHesapla(int barIndex, double zararKesYuzdesi = -2.0)
        {
            int result = 0;
            int i = barIndex;

            if (!Trader.flags.ZararKesYuzdeHesaplaEnabled)
                return result;

            // Anlık kar/zarar yüzdesi
            double anlikKarZararYuzdesi = Trader.lists.KarZararFiyatYuzdeList[i];

            // Zarar durumunda (negatif) ve hedef zarara ulaştıysa
            // Örn: anlikKarZararYuzdesi = -2.5, zararKesYuzdesi = -2.0
            // -2.5 <= -2.0 → Zarar daha büyük, kes!
            if (anlikKarZararYuzdesi <= zararKesYuzdesi)
            {
                Trader.lists.ZararKesList[i] = anlikKarZararYuzdesi;

                if (Trader.is_son_yon_a())
                {
                    result = 1;  // Long pozisyon zarar kes
                }
                else if (Trader.is_son_yon_s())
                {
                    result = -1;  // Short pozisyon zarar kes
                }
            }

            return result;
        }

        /// <summary>
        /// KarZararFiyatList'teki anlık kar seviyesine göre kar al kontrolü
        /// Pozisyon açıldıktan sonra anlık kar seviyesi hedefe ulaştıysa kar al sinyali verir
        /// </summary>
        /// <param name="barIndex">Bar index</param>
        /// <param name="karAlFiyatSeviyesi">Hedef kar seviyesi (örn: 1000.0 = 1000 TL kar)</param>
        /// <returns>0 = Bekle, 1 = Long kar al, -1 = Short kar al</returns>
        public int KarZararFiyatSeviyesindenKarAlHesapla(int barIndex, double karAlFiyatSeviyesi = 1000.0)
        {
            int result = 0;
            int i = barIndex;

            if (!Trader.flags.KarAlSeviyeHesaplaEnabled)
                return result;

            // Anlık kar/zarar fiyat seviyesi
            double anlikKarZararFiyat = Trader.lists.KarZararFiyatList[i];

            // Kar hedefe ulaştıysa
            if (anlikKarZararFiyat >= karAlFiyatSeviyesi)
            {
                Trader.lists.KarAlList[i] = anlikKarZararFiyat;

                if (Trader.is_son_yon_a())
                {
                    result = 1;  // Long pozisyon kar al
                }
                else if (Trader.is_son_yon_s())
                {
                    result = -1;  // Short pozisyon kar al
                }
            }

            return result;
        }

        /// <summary>
        /// KarZararFiyatList'teki anlık zarar seviyesine göre zarar kes kontrolü
        /// Pozisyon açıldıktan sonra anlık zarar seviyesi hedefe ulaştıysa zarar kes sinyali verir
        /// </summary>
        /// <param name="barIndex">Bar index</param>
        /// <param name="zararKesFiyatSeviyesi">Hedef zarar seviyesi (örn: -500.0 = 500 TL zarar)</param>
        /// <returns>0 = Bekle, 1 = Long zarar kes, -1 = Short zarar kes</returns>
        public int KarZararFiyatSeviyesindenZararKesHesapla(int barIndex, double zararKesFiyatSeviyesi = -500.0)
        {
            int result = 0;
            int i = barIndex;

            if (!Trader.flags.ZararKesSeviyeHesaplaEnabled)
                return result;

            // Anlık kar/zarar fiyat seviyesi
            double anlikKarZararFiyat = Trader.lists.KarZararFiyatList[i];

            // Zarar hedefe ulaştıysa
            // Örn: anlikKarZararFiyat = -600, zararKesFiyatSeviyesi = -500
            // -600 <= -500 → Zarar daha büyük, kes!
            if (anlikKarZararFiyat <= zararKesFiyatSeviyesi)
            {
                Trader.lists.ZararKesList[i] = anlikKarZararFiyat;

                if (Trader.is_son_yon_a())
                {
                    result = 1;  // Long pozisyon zarar kes
                }
                else if (Trader.is_son_yon_s())
                {
                    result = -1;  // Short pozisyon zarar kes
                }
            }

            return result;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Close price'ları array olarak al
        /// NOT: Performans sorunlarına yol açabilir (2M eleman kopyalama). Mümkünse AsagiKestiClose/YukarıKestiClose kullanın.
        /// </summary>
        private double[] GetClosePrices()
        {
            double[] closes = new double[Trader.Data.Count];
            for (int i = 0; i < Trader.Data.Count; i++)
            {
                closes[i] = Trader.Data[i].Close;
            }
            return closes;
        }

        /// <summary>
        /// i barında Close fiyatının bir değeri aşağı kesip kesmediğini kontrol eder
        /// Performans optimizasyonu: Array oluşturmadan direkt Data listesinden okur
        /// </summary>
        private bool AsagiKestiClose(int i, double threshold)
        {
            if (i < 1 || i >= Trader.Data.Count)
                return false;

            // Aşağı kesişim: önceki >= threshold && şimdiki < threshold
            return Trader.Data[i - 1].Close >= threshold && Trader.Data[i].Close < threshold;
        }

        /// <summary>
        /// i barında Close fiyatının bir değeri yukarı kesip kesmediğini kontrol eder
        /// Performans optimizasyonu: Array oluşturmadan direkt Data listesinden okur
        /// </summary>
        private bool YukarıKestiClose(int i, double threshold)
        {
            if (i < 1 || i >= Trader.Data.Count)
                return false;

            // Yukarı kesişim: önceki <= threshold && şimdiki > threshold
            return Trader.Data[i - 1].Close <= threshold && Trader.Data[i].Close > threshold;
        }

        /// <summary>
        /// i barında KarZararFiyatList'in bir değeri aşağı kesip kesmediğini kontrol eder
        /// Performans optimizasyonu: Array oluşturmadan direkt List'ten okur
        /// </summary>
        private bool AsagiKestiKarZararFiyat(int i, double threshold)
        {
            if (i < 1 || i >= Trader.lists.KarZararFiyatList.Count)
                return false;

            // Aşağı kesişim: önceki >= threshold && şimdiki < threshold
            return Trader.lists.KarZararFiyatList[i - 1] >= threshold && Trader.lists.KarZararFiyatList[i] < threshold;
        }

        /// <summary>
        /// i barında KarZararFiyatList'in bir değeri yukarı kesip kesmediğini kontrol eder
        /// Performans optimizasyonu: Array oluşturmadan direkt List'ten okur
        /// </summary>
        private bool YukarıKestiKarZararFiyat(int i, double threshold)
        {
            if (i < 1 || i >= Trader.lists.KarZararFiyatList.Count)
                return false;

            // Yukarı kesişim: önceki <= threshold && şimdiki > threshold
            return Trader.lists.KarZararFiyatList[i - 1] <= threshold && Trader.lists.KarZararFiyatList[i] > threshold;
        }

        #endregion
    }
}
