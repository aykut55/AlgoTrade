using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AlgoTrade.Core.Trading;

namespace AlgoTrade.Core.Trading.Statistics
{
    /// <summary>
    /// Statistics - Comprehensive trading statistics calculator
    /// İstatistik hesaplamaları ve raporlama
    /// </summary>
    public class Statistics
    {
        #region Private Fields

        private SingleTrader Trader { get; set; }

        #endregion

        #region Identification
        public int Id => Trader?.Id ?? 0;
        public string Name => Trader?.Name ?? "...";
        #endregion

        #region System Info
        public string GrafikSembol => Trader?.SymbolName ?? "...";
        public string GrafikPeriyot => Trader?.SymbolPeriod.ToString() ?? "...";
        public string SistemId => Trader?.SystemId ?? "...";
        public string SistemName => Trader?.SystemName ?? "...";
        public string StrategyId => Trader?.StrategyId ?? "...";
        public string StrategyName => Trader?.StrategyName ?? "...";
        #endregion

        #region Execution Info

        public string LastExecutionId => Trader?.LastExecutionId ?? "...";
        public string LastExecutionTime => Trader?.LastExecutionTime ?? "";
        public string LastExecutionTimeStart => Trader?.LastExecutionTimeStart ?? "";
        public string LastExecutionTimeStop => Trader?.LastExecutionTimeStop ?? "";
        public string LastExecutionTimeInMSec => Trader?.LastExecutionTimeInMSec ?? "";
        public string LastResetTime => Trader?.LastResetTime ?? "";
        public string LastStatisticsCalculationTime => Trader?.LastStatisticsCalculationTime ?? "";

        #endregion

        #region Bar Info

        public int ToplamBarSayisi { get; set; }
        public int IlkBarIndex { get; set; }
        public int SonBarIndex { get; set; }
        public int SecilenBarNumarasi { get; set; }

        public string IlkBarTarihSaati { get; set; }
        public string IlkBarTarihi { get; set; }
        public string IlkBarSaati { get; set; }

        public string SonBarTarihSaati { get; set; }
        public string SonBarTarihi { get; set; }
        public string SonBarSaati { get; set; }

        public string SecilenBarTarihSaati { get; set; }
        public string SecilenBarTarihi { get; set; }
        public string SecilenBarSaati { get; set; }

        public double SecilenBarAcilisFiyati { get; set; }
        public double SecilenBarYuksekFiyati { get; set; }
        public double SecilenBarDusukFiyati { get; set; }
        public double SecilenBarKapanisFiyati { get; set; }
        public double SonBarAcilisFiyati { get; set; }
        public double SonBarYuksekFiyati { get; set; }
        public double SonBarDusukFiyati { get; set; }
        public double SonBarKapanisFiyati { get; set; }

        #endregion

        #region Time Statistics

        public double ToplamGecenSureAy { get; set; }
        public int ToplamGecenSureGun { get; set; }
        public int ToplamGecenSureSaat { get; set; }
        public int ToplamGecenSureDakika { get; set; }
        public double OrtAylikIslemSayisi { get; set; }
        public double OrtHaftalikIslemSayisi { get; set; }
        public double OrtGunlukIslemSayisi { get; set; }
        public double OrtSaatlikIslemSayisi { get; set; }

        #endregion

        #region Trade Counts

        public int IslemSayisi => Trader?.status?.IslemSayisi ?? 0;
        public int AlisSayisi => Trader?.status?.AlisSayisi ?? 0;
        public int SatisSayisi => Trader?.status?.SatisSayisi ?? 0;
        public int FlatSayisi => Trader?.status?.FlatSayisi ?? 0;
        public int PassSayisi => Trader?.status?.PassSayisi ?? 0;
        public int KarAlSayisi => Trader?.status?.KarAlSayisi ?? 0;
        public int ZararKesSayisi => Trader?.status?.ZararKesSayisi ?? 0;
        public int KazandiranIslemSayisi => Trader?.status?.KazandiranIslemSayisi ?? 0;
        public int KaybettirenIslemSayisi => Trader?.status?.KaybettirenIslemSayisi ?? 0;
        public int NotrIslemSayisi => Trader?.status?.NotrIslemSayisi ?? 0;
        public int KazandiranAlisSayisi => Trader?.status?.KazandiranAlisSayisi ?? 0;
        public int KaybettirenAlisSayisi => Trader?.status?.KaybettirenAlisSayisi ?? 0;
        public int NotrAlisSayisi => Trader?.status?.NotrAlisSayisi ?? 0;
        public int KazandiranSatisSayisi => Trader?.status?.KazandiranSatisSayisi ?? 0;
        public int KaybettirenSatisSayisi => Trader?.status?.KaybettirenSatisSayisi ?? 0;
        public int NotrSatisSayisi => Trader?.status?.NotrSatisSayisi ?? 0;

        #endregion

        #region Command Counts

        public int AlKomutSayisi => Trader?.status?.AlKomutSayisi ?? 0;
        public int SatKomutSayisi => Trader?.status?.SatKomutSayisi ?? 0;
        public int PasGecKomutSayisi => Trader?.status?.PasGecKomutSayisi ?? 0;
        public int KarAlKomutSayisi => Trader?.status?.KarAlKomutSayisi ?? 0;
        public int ZararKesKomutSayisi => Trader?.status?.ZararKesKomutSayisi ?? 0;
        public int FlatOlKomutSayisi => Trader?.status?.FlatOlKomutSayisi ?? 0;

        #endregion

        #region Bar Status

        public int KardaBarSayisi => Trader?.status?.KardaBarSayisi ?? 0;
        public int ZarardaBarSayisi => Trader?.status?.ZarardaBarSayisi ?? 0;

        #endregion

        #region PnL

        public double KarZararFiyat => Trader?.status?.KarZararFiyat ?? 0;
        public double KarZararPuan => Trader?.status?.KarZararPuan ?? 0;
        public double KarZararFiyatYuzde => Trader?.status?.KarZararFiyatYuzde ?? 0;
        public double ToplamKarFiyat => Trader?.status?.ToplamKarFiyat ?? 0;
        public double ToplamZararFiyat => Trader?.status?.ToplamZararFiyat ?? 0;
        public double NetKarFiyat => Trader?.status?.NetKarFiyat ?? 0;
        public double ToplamKarPuan => Trader?.status?.ToplamKarPuan ?? 0;
        public double ToplamZararPuan => Trader?.status?.ToplamZararPuan ?? 0;
        public double NetKarPuan => Trader?.status?.NetKarPuan ?? 0;
        public double MaxKarFiyat { get; set; }
        public double MaxZararFiyat { get; set; }
        public double MaxKarPuan { get; set; }
        public double MaxZararPuan { get; set; }
        public int MaxZararFiyatIndex { get; set; }
        public int MaxKarFiyatIndex { get; set; }
        public int MaxZararPuanIndex { get; set; }
        public int MaxKarPuanIndex { get; set; }

        #endregion

        #region Commission

        public int KomisyonIslemSayisi => Trader?.status?.KomisyonIslemSayisi ?? 0;
        public double KomisyonVarlikAdedSayisi => Trader?.status?.KomisyonVarlikAdedSayisi ?? 0;
        public double KomisyonVarlikAdedSayisiMicro => Trader?.status?.KomisyonVarlikAdedSayisiMicro ?? 0;
        public double KomisyonCarpan => Trader?.status?.KomisyonCarpan ?? 0;
        public double KomisyonFiyat { get; set; }  // Toplam komisyon (Hesapla() metodunda hesaplanır)
        public double KomisyonFiyatYuzde { get; set; }
        public bool KomisyonuDahilEt => Trader?.flags?.KomisyonuDahilEt ?? false;

        #endregion

        #region Balance

        public double IlkBakiyeFiyat => Trader?.status?.IlkBakiyeFiyat ?? 0;
        public double IlkBakiyePuan => Trader?.status?.IlkBakiyePuan ?? 0;
        public double BakiyeFiyat => Trader?.status?.BakiyeFiyat ?? 0;
        public double BakiyePuan => Trader?.status?.BakiyePuan ?? 0;
        public double GetiriFiyat => Trader?.status?.GetiriFiyat ?? 0;
        public double GetiriPuan => Trader?.status?.GetiriPuan ?? 0;
        public double GetiriFiyatYuzde => Trader?.status?.GetiriFiyatYuzde ?? 0;
        public double GetiriPuanYuzde => Trader?.status?.GetiriPuanYuzde ?? 0;
        public double BakiyeFiyatNet => Trader?.status?.BakiyeFiyatNet ?? 0;
        public double BakiyePuanNet => Trader?.status?.BakiyePuanNet ?? 0;
        public double GetiriFiyatNet => Trader?.status?.GetiriFiyatNet ?? 0;
        public double GetiriPuanNet => Trader?.status?.GetiriPuanNet ?? 0;
        public double GetiriFiyatYuzdeNet => Trader?.status?.GetiriFiyatYuzdeNet ?? 0;
        public double GetiriPuanYuzdeNet => Trader?.status?.GetiriPuanYuzdeNet ?? 0;
        public double GetiriKz => Trader?.status?.GetiriKz ?? 0;
        public double GetiriKzNet => Trader?.status?.GetiriKzNet ?? 0;
        public double GetiriKzSistem => Trader?.status?.GetiriKzSistem ?? 0;
        public double GetiriKzNetSistem => Trader?.status?.GetiriKzNetSistem ?? 0;
        public double GetiriKzSistemYuzde { get; set; }
        public double GetiriKzNetSistemYuzde { get; set; }
        public int GetiriFiyatTipi { get; set; }

        #endregion

        #region Balance Min/Max

        public double MinBakiyeFiyat { get; set; }
        public double MaxBakiyeFiyat { get; set; }
        public double MinBakiyePuan { get; set; }
        public double MaxBakiyePuan { get; set; }
        public double MinBakiyeFiyatYuzde { get; set; }
        public double MaxBakiyeFiyatYuzde { get; set; }
        public int MinBakiyeFiyatIndex { get; set; }
        public int MaxBakiyeFiyatIndex { get; set; }
        public int MinBakiyePuanIndex { get; set; }
        public int MaxBakiyePuanIndex { get; set; }

        public double MinBakiyeFiyatNet { get; set; }
        public double MaxBakiyeFiyatNet { get; set; }
        public int MinBakiyeFiyatNetIndex { get; set; }
        public int MaxBakiyeFiyatNetIndex { get; set; }
        public double MinBakiyeFiyatNetYuzde { get; set; }
        public double MaxBakiyeFiyatNetYuzde { get; set; }

        #endregion

        #region Drawdown

        public double GetiriMaxDD { get; set; }
        public string GetiriMaxDDTarih { get; set; }
        public double GetiriMaxKayip { get; set; }

        #endregion

        #region Performance Metrics

        public double ProfitFactor { get; set; }
        public double ProfitFactorNet { get; set; }  // Commission-adjusted profit factor
        public double ProfitFactorSistem { get; set; }
        public double KarliIslemOrani { get; set; }

        #endregion

        #region Asset Info

        public double HisseSayisi => Trader?.status?.HisseSayisi ?? 0;
        public double KontratSayisi => Trader?.status?.KontratSayisi ?? 0;
        public double VarlikAdedCarpani => Trader?.status?.VarlikAdedCarpani ?? 0;
        public double VarlikAdedSayisi => Trader?.status?.VarlikAdedSayisi ?? 0;
        public double VarlikAdedSayisiMicro => Trader?.status?.VarlikAdedSayisiMicro ?? 0;
        public double KaymaMiktari => Trader?.status?.KaymaMiktari ?? 0;
        public bool KaymayiDahilEt => Trader?.flags?.KaymayiDahilEt ?? false;

        // New Pyramiding fields
        public bool PyramidingEnabled => Trader?.initialTradeParams?.PyramidingEnabled ?? false;
        public bool MaxPositionSizeEnabled => Trader?.initialTradeParams?.MaxPositionSizeEnabled ?? false;
        public double MaxPositionSize => Trader?.initialTradeParams?.MaxPositionSize ?? 0;
        public double MaxPositionSizeMicro => Trader?.initialTradeParams?.MaxPositionSizeMicro ?? 0;
        public bool MicroLotSizeEnabled => Trader?.initialTradeParams?.MicroLotSizeEnabled ?? false;
        #endregion

        #region Signals

        public string Sinyal => Trader?.signals?.Sinyal ?? "";
        public string SonYon => Trader?.signals?.SonYon ?? "";
        public string PrevYon => Trader?.signals?.PrevYon ?? "";
        public double SonFiyat => Trader?.signals?.SonFiyat ?? 0;
        public double SonAFiyat => Trader?.signals?.SonAFiyat ?? 0;
        public double SonSFiyat => Trader?.signals?.SonSFiyat ?? 0;
        public double SonFFiyat => Trader?.signals?.SonFFiyat ?? 0;
        public double SonPFiyat => Trader?.signals?.SonPFiyat ?? 0;
        public double PrevFiyat => Trader?.signals?.PrevFiyat ?? 0;
        public double PrevAFiyat => Trader?.signals?.PrevAFiyat ?? 0;
        public double PrevSFiyat => Trader?.signals?.PrevSFiyat ?? 0;
        public double PrevFFiyat => Trader?.signals?.PrevFFiyat ?? 0;
        public double PrevPFiyat => Trader?.signals?.PrevPFiyat ?? 0;
        public int SonBarNo => Trader?.signals?.SonBarNo ?? 0;
        public int SonABarNo => Trader?.signals?.SonABarNo ?? 0;
        public int SonSBarNo => Trader?.signals?.SonSBarNo ?? 0;
        public int SonFBarNo => Trader?.signals?.SonFBarNo ?? 0;
        public int SonPBarNo => Trader?.signals?.SonPBarNo ?? 0;
        public int PrevBarNo => Trader?.signals?.PrevBarNo ?? 0;
        public int PrevABarNo => Trader?.signals?.PrevABarNo ?? 0;
        public int PrevSBarNo => Trader?.signals?.PrevSBarNo ?? 0;
        public int PrevFBarNo => Trader?.signals?.PrevFBarNo ?? 0;
        public int PrevPBarNo => Trader?.signals?.PrevPBarNo ?? 0;
        public int EmirKomut => Trader?.signals?.EmirKomut ?? 0;
        public int EmirStatus => Trader?.signals?.EmirStatus ?? 0;

        // Dynamic lot size signal fields
        public double SonVarlikAdedSayisi => Trader?.signals?.SonVarlikAdedSayisi ?? 0;
        public double SonVarlikAdedSayisiMicro => Trader?.signals?.SonVarlikAdedSayisiMicro ?? 0;
        public double PrevVarlikAdedSayisiMicro => Trader?.signals?.PrevVarlikAdedSayisiMicro ?? 0;
        #endregion

        #region Periodic Returns - Month

        public double GetiriFiyatBuAy { get; set; }
        public double GetiriFiyatAy1 { get; set; }
        public double GetiriFiyatAy2 { get; set; }
        public double GetiriFiyatAy3 { get; set; }
        public double GetiriFiyatAy4 { get; set; }
        public double GetiriFiyatAy5 { get; set; }
        public double GetiriPuanBuAy { get; set; }
        public double GetiriPuanAy1 { get; set; }
        public double GetiriPuanAy2 { get; set; }
        public double GetiriPuanAy3 { get; set; }
        public double GetiriPuanAy4 { get; set; }
        public double GetiriPuanAy5 { get; set; }

        #endregion

        #region Periodic Returns - Week

        public double GetiriFiyatBuHafta { get; set; }
        public double GetiriFiyatHafta1 { get; set; }
        public double GetiriFiyatHafta2 { get; set; }
        public double GetiriFiyatHafta3 { get; set; }
        public double GetiriFiyatHafta4 { get; set; }
        public double GetiriFiyatHafta5 { get; set; }
        public double GetiriPuanBuHafta { get; set; }
        public double GetiriPuanHafta1 { get; set; }
        public double GetiriPuanHafta2 { get; set; }
        public double GetiriPuanHafta3 { get; set; }
        public double GetiriPuanHafta4 { get; set; }
        public double GetiriPuanHafta5 { get; set; }

        #endregion

        #region Periodic Returns - Day

        public double GetiriFiyatBuGun { get; set; }
        public double GetiriFiyatGun1 { get; set; }
        public double GetiriFiyatGun2 { get; set; }
        public double GetiriFiyatGun3 { get; set; }
        public double GetiriFiyatGun4 { get; set; }
        public double GetiriFiyatGun5 { get; set; }
        public double GetiriPuanBuGun { get; set; }
        public double GetiriPuanGun1 { get; set; }
        public double GetiriPuanGun2 { get; set; }
        public double GetiriPuanGun3 { get; set; }
        public double GetiriPuanGun4 { get; set; }
        public double GetiriPuanGun5 { get; set; }

        #endregion

        #region Periodic Returns - Hour

        public double GetiriFiyatBuSaat { get; set; }
        public double GetiriFiyatSaat1 { get; set; }
        public double GetiriFiyatSaat2 { get; set; }
        public double GetiriFiyatSaat3 { get; set; }
        public double GetiriFiyatSaat4 { get; set; }
        public double GetiriFiyatSaat5 { get; set; }
        public double GetiriPuanBuSaat { get; set; }
        public double GetiriPuanSaat1 { get; set; }
        public double GetiriPuanSaat2 { get; set; }
        public double GetiriPuanSaat3 { get; set; }
        public double GetiriPuanSaat4 { get; set; }
        public double GetiriPuanSaat5 { get; set; }

        #endregion

        #region Statistics Map

        private const string SEPARATOR = "#SEPARATOR#";

        public Dictionary<string, string> StatisticsMap { get; set; }
        public Dictionary<string, string> StatisticsMapMinimal { get; set; }

        #endregion

        #region Constructor

        public Statistics()
        {
            StatisticsMap = new Dictionary<string, string>();
            StatisticsMapMinimal = new Dictionary<string, string>();
            IlkBarIndex = 0;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Dosyayı FileShare.ReadWrite ile yazar - başka process (Notepad vb.) açıkken de yazabilir.
        /// </summary>
        private static void WriteAllTextShared(string filePath, string content)
        {
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(fs, Encoding.UTF8);
            writer.Write(content);
        }

        /// <summary>
        /// FileShare.ReadWrite ile StreamWriter döndürür - başka process açıkken de yazabilir.
        /// </summary>
        private static StreamWriter CreateSharedWriter(string filePath)
        {
            var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            return new StreamWriter(fs, Encoding.UTF8);
        }

        /// <summary>
        /// Dosya sonuna ekleme yapar - başka process açıkken de yazabilir.
        /// </summary>
        private static void AppendAllTextShared(string filePath, string content)
        {
            using var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(fs, Encoding.UTF8);
            writer.Write(content);
        }

        public Statistics Initialize(SingleTrader trader)
        {
            Trader = trader;
            return this;
        }

        public Statistics Init(SingleTrader trader)
        {
            Trader = trader;
            return this;
        }

        public Statistics Reset()
        {
            StatisticsMap.Clear();
            StatisticsMapMinimal.Clear();
            return this;
        }

        public int Hesapla(int secilenBarNumarasi)
        {
            int result = 0;

            if (Trader == null)
                return result;

            ReadValues();

            Trader.LastStatisticsCalculationTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

            int firstBarIndex = 0;
            int lastBarIndex  = Trader.Data.Count - 1;
            ToplamBarSayisi   = Trader.Data.Count;

            this.SecilenBarNumarasi = secilenBarNumarasi;
            if (this.SecilenBarNumarasi < firstBarIndex) {
                this.SecilenBarNumarasi = firstBarIndex;
            }
            else if (this.SecilenBarNumarasi > lastBarIndex) {
                this.SecilenBarNumarasi = lastBarIndex;
            }

            IlkBarTarihSaati        = Trader.Data[firstBarIndex].DateTime.ToString("yyyy.MM.dd HH:mm:ss");
            IlkBarTarihi            = Trader.Data[firstBarIndex].DateTime.ToString("yyyy.MM.dd");
            IlkBarSaati             = Trader.Data[firstBarIndex].DateTime.ToString("HH:mm:ss");

            SonBarTarihSaati        = Trader.Data[lastBarIndex].DateTime.ToString("yyyy.MM.dd HH:mm:ss");
            SonBarTarihi            = Trader.Data[lastBarIndex].DateTime.ToString("yyyy.MM.dd");
            SonBarSaati             = Trader.Data[lastBarIndex].DateTime.ToString("HH:mm:ss");

            SecilenBarTarihSaati    = Trader.Data[this.SecilenBarNumarasi].DateTime.ToString("yyyy.MM.dd HH:mm:ss");
            SecilenBarTarihi        = Trader.Data[this.SecilenBarNumarasi].DateTime.ToString("yyyy.MM.dd");
            SecilenBarSaati         = Trader.Data[this.SecilenBarNumarasi].DateTime.ToString("HH:mm:ss");

            SecilenBarAcilisFiyati  = Trader.Data[this.SecilenBarNumarasi].Open;
            SecilenBarYuksekFiyati  = Trader.Data[this.SecilenBarNumarasi].High;
            SecilenBarDusukFiyati   = Trader.Data[this.SecilenBarNumarasi].Low;
            SecilenBarKapanisFiyati = Trader.Data[this.SecilenBarNumarasi].Close;

            SonBarAcilisFiyati      = Trader.Data[lastBarIndex].Open;
            SonBarYuksekFiyati      = Trader.Data[lastBarIndex].High;
            SonBarDusukFiyati       = Trader.Data[lastBarIndex].Low;
            SonBarKapanisFiyati     = Trader.Data[lastBarIndex].Close;
            SonBarIndex             = lastBarIndex;

            // Calculate time elapsed
            DateTime firstDate = Trader.Data[0].Date;
            TimeSpan elapsed = DateTime.Now - firstDate;
            double sureDakika = elapsed.TotalMinutes;
            double sureSaat = elapsed.TotalHours;
            int sureGun = elapsed.Days;
            double sureAy = sureGun / 30.4;

            ToplamGecenSureAy      = sureAy;
            ToplamGecenSureGun     = sureGun;
            ToplamGecenSureSaat    = (int)sureSaat;
            ToplamGecenSureDakika  = (int)sureDakika;
            OrtAylikIslemSayisi    = ToplamGecenSureAy > 0 ? 1.0 * IslemSayisi / ToplamGecenSureAy : 0;
            OrtHaftalikIslemSayisi = 0.0;
            OrtGunlukIslemSayisi   = ToplamGecenSureGun > 0 ? 1.0 * IslemSayisi / ToplamGecenSureGun : 0;
            OrtSaatlikIslemSayisi  = ToplamGecenSureSaat > 0 ? 1.0 * IslemSayisi / ToplamGecenSureSaat : 0;

            // Maximum Drawdown hesaplaması
            double maxBakiye = IlkBakiyeFiyat;
            double maxDD = 0.0;
            double maxDDYuzde = 0.0;
            string maxDDTarih = "";

            for (int i = 0; i < Trader.Data.Count; i++)
            {
                double mevcutBakiye = Trader.lists.BakiyeFiyatList[i];

                // Yeni maksimum bakiye kontrolü
                if (mevcutBakiye > maxBakiye)
                {
                    maxBakiye = mevcutBakiye;
                }

                // Drawdown hesapla (maksimum bakiyeden ne kadar düşmüş)
                double drawdown = maxBakiye - mevcutBakiye;
                double drawdownYuzde = maxBakiye > 0 ? (drawdown / maxBakiye) * 100.0 : 0.0;

                // En büyük drawdown kontrolü
                if (drawdownYuzde > maxDDYuzde)
                {
                    maxDDYuzde = drawdownYuzde;
                    maxDD = drawdown;
                    maxDDTarih = Trader.Data[i].DateTime.ToString("yyyy.MM.dd HH:mm:ss");
                }
            }

            GetiriMaxDD            = maxDDYuzde;
            GetiriMaxDDTarih       = maxDDTarih;
            GetiriMaxKayip         = maxDD;

            MaxKarFiyat            = 0.0;
            MaxZararFiyat          = 0.0;
            MaxKarPuan             = 0.0;
            MaxZararPuan           = 0.0;
            MinBakiyeFiyat         = IlkBakiyeFiyat;
            MaxBakiyeFiyat         = IlkBakiyeFiyat;
            MinBakiyeFiyatNet      = IlkBakiyeFiyat;
            MaxBakiyeFiyatNet      = IlkBakiyeFiyat;
            MinBakiyePuan          = IlkBakiyePuan;
            MaxBakiyePuan          = IlkBakiyePuan;

            // Toplam komisyonu al (son bar'daki kümülatif değer)
            KomisyonFiyat = lastBarIndex >= 0 ? Trader.lists.KomisyonFiyatList[lastBarIndex] : 0.0;

            // Find min/max values
            for (int i = 1; i < Trader.Data.Count; i++)
            {
                if (Trader.lists.KarZararFiyatList[i] < MaxZararFiyat)
                {
                    MaxZararFiyat = Trader.lists.KarZararFiyatList[i];
                    MaxZararFiyatIndex = i;
                }
                if (Trader.lists.KarZararFiyatList[i] > MaxKarFiyat)
                {
                    MaxKarFiyat = Trader.lists.KarZararFiyatList[i];
                    MaxKarFiyatIndex = i;
                }
                if (Trader.lists.KarZararPuanList[i] < MaxZararPuan)
                {
                    MaxZararPuan = Trader.lists.KarZararPuanList[i];
                    MaxZararPuanIndex = i;
                }
                if (Trader.lists.KarZararPuanList[i] > MaxKarPuan)
                {
                    MaxKarPuan = Trader.lists.KarZararPuanList[i];
                    MaxKarPuanIndex = i;
                }
                if (Trader.lists.BakiyeFiyatList[i] < MinBakiyeFiyat)
                {
                    MinBakiyeFiyat = Trader.lists.BakiyeFiyatList[i];
                    MinBakiyeFiyatIndex = i;
                }
                if (Trader.lists.BakiyeFiyatList[i] > MaxBakiyeFiyat)
                {
                    MaxBakiyeFiyat = Trader.lists.BakiyeFiyatList[i];
                    MaxBakiyeFiyatIndex = i;
                }
                if (Trader.lists.BakiyeFiyatNetList[i] < MinBakiyeFiyatNet)
                {
                    MinBakiyeFiyatNet = Trader.lists.BakiyeFiyatNetList[i];
                    MinBakiyeFiyatNetIndex = i;
                }
                if (Trader.lists.BakiyeFiyatNetList[i] > MaxBakiyeFiyatNet)
                {
                    MaxBakiyeFiyatNet = Trader.lists.BakiyeFiyatNetList[i];
                    MaxBakiyeFiyatNetIndex = i;
                }
                if (Trader.lists.BakiyePuanList[i] < MinBakiyePuan)
                {
                    MinBakiyePuan = Trader.lists.BakiyePuanList[i];
                    MinBakiyePuanIndex = i;
                }
                if (Trader.lists.BakiyePuanList[i] > MaxBakiyePuan)
                {
                    MaxBakiyePuan = Trader.lists.BakiyePuanList[i];
                    MaxBakiyePuanIndex = i;
                }
            }

            // Calculate performance metrics
            ProfitFactor           = Math.Abs(ToplamZararPuan) > 0 ? ToplamKarPuan / Math.Abs(ToplamZararPuan) : 0;
            // ProfitFactorNet: Commission-adjusted profit factor (commission added to loss side)
            double totalLossWithCommission = Math.Abs(ToplamZararPuan) + KomisyonFiyat;
            ProfitFactorNet        = totalLossWithCommission > 0 ? ToplamKarPuan / totalLossWithCommission : 0;
            ProfitFactorSistem     = 0.0;
            KarliIslemOrani        = IslemSayisi > 0 ? (1.0 * KazandiranIslemSayisi) / (1.0 * IslemSayisi) * 100.0 : 0;

            MinBakiyeFiyatYuzde    = IlkBakiyeFiyat != 0 ? (MinBakiyeFiyat - IlkBakiyeFiyat) * 100.0 / IlkBakiyeFiyat : 0;
            MaxBakiyeFiyatYuzde    = IlkBakiyeFiyat != 0 ? (MaxBakiyeFiyat - IlkBakiyeFiyat) * 100.0 / IlkBakiyeFiyat : 0;
            MinBakiyeFiyatNetYuzde = IlkBakiyeFiyat != 0 ? (MinBakiyeFiyatNet - IlkBakiyeFiyat) * 100.0 / IlkBakiyeFiyat : 0;
            MaxBakiyeFiyatNetYuzde = IlkBakiyeFiyat != 0 ? (MaxBakiyeFiyatNet - IlkBakiyeFiyat) * 100.0 / IlkBakiyeFiyat : 0;
            KomisyonFiyatYuzde     = GetiriFiyatYuzde - GetiriFiyatYuzdeNet;
            GetiriKzSistemYuzde    = 0.0;
            GetiriKzNetSistemYuzde = 0.0;

            GetiriIstatistikleriHesapla();

            AssignToMap();

            AssignToMapMinimal();

            return result;
        }

        private void ReadValues()
        {
            // All identification, system, and execution properties are now proxies.
            // No manual assignments needed here.
        }

        private void GetiriIstatistikleriHesapla()
        {
            // TODO: Implement periodic return calculations
            // This would calculate monthly, weekly, daily, and hourly returns
        }

        private void AssignToMap()
        {
            int keyId = 0;

            StatisticsMap.Clear();

            // Helper to add null-safe and formatted values
            void Add(string key, object value, string format = "")
            {
                if (value == null || (value is string s && string.IsNullOrEmpty(s))) 
                {
                    StatisticsMap[key] = "...";
                    return;
                }
                StatisticsMap[key] = string.IsNullOrEmpty(format) ? value.ToString() : string.Format("{0:" + format + "}", value);
            }

            // --- Identification ---
            Add("TraderId", Id);
            Add("TraderName", Name);

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- System & Execution Info ---
            Add("SymbolName", GrafikSembol);
            Add("SymbolPeriod", GrafikPeriyot);
            Add("SystemId", SistemId);
            Add("SystemName", SistemName);
            Add("StrategyId", StrategyId);
            Add("StrategyName", StrategyName);

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            Add("LastExecutionId", LastExecutionId);
            Add("LastExecutionTime", LastExecutionTime);
            Add("LastExecutionTimeStart", LastExecutionTimeStart);
            Add("LastExecutionTimeStop", LastExecutionTimeStop);
            Add("LastExecutionTimeInMSec", LastExecutionTimeInMSec);
            Add("LastResetTime", LastResetTime);
            Add("LastStatisticsCalculationTime", LastStatisticsCalculationTime);

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- Bar Info ---
            Add("ToplamBarSayisi", ToplamBarSayisi);
            Add("SecilenBarNumarasi", SecilenBarNumarasi);
            Add("SecilenBarTarihSaati", SecilenBarTarihSaati);
            Add("SecilenBarTarihi", SecilenBarTarihi);
            Add("SecilenBarSaati", SecilenBarSaati);

            Add("IlkBarTarihSaati", IlkBarTarihSaati);
            Add("IlkBarTarihi", IlkBarTarihi);
            Add("IlkBarSaati", IlkBarSaati);

            Add("SonBarTarihSaati", SonBarTarihSaati);
            Add("SonBarTarihi", SonBarTarihi);
            Add("SonBarSaati", SonBarSaati);

            Add("IlkBarIndex", IlkBarIndex);
            Add("SonBarIndex", SonBarIndex);
            Add("SonBarAcilisFiyati", SonBarAcilisFiyati, "F4");
            Add("SonBarYuksekFiyati", SonBarYuksekFiyati, "F4");
            Add("SonBarDusukFiyati", SonBarDusukFiyati, "F4");
            Add("SonBarKapanisFiyati", SonBarKapanisFiyati, "F4");

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- Time Statistics ---
            Add("ToplamGecenSureAy", ToplamGecenSureAy, "F1");
            Add("ToplamGecenSureGun", ToplamGecenSureGun);
            Add("ToplamGecenSureSaat", ToplamGecenSureSaat);
            Add("ToplamGecenSureDakika", ToplamGecenSureDakika);
            Add("OrtAylikIslemSayisi", OrtAylikIslemSayisi, "F2");
            Add("OrtHaftalikIslemSayisi", OrtHaftalikIslemSayisi, "F2");
            Add("OrtGunlukIslemSayisi", OrtGunlukIslemSayisi, "F2");
            Add("OrtSaatlikIslemSayisi", OrtSaatlikIslemSayisi, "F2");

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- Balance & Returns ---
            Add("IlkBakiyeFiyat", IlkBakiyeFiyat, "F2");
            Add("IlkBakiyePuan", IlkBakiyePuan, "F2");
            Add("BakiyeFiyat", BakiyeFiyat, "F2");
            Add("BakiyePuan", BakiyePuan, "F2");
            Add("GetiriFiyat", GetiriFiyat, "F2");
            Add("GetiriPuan", GetiriPuan, "F2");
            Add("GetiriFiyatYuzde", GetiriFiyatYuzde, "F2");
            Add("GetiriPuanYuzde", GetiriPuanYuzde, "F2");
            Add("BakiyeFiyatNet", BakiyeFiyatNet, "F2");
            Add("BakiyePuanNet", BakiyePuanNet, "F2");
            Add("GetiriFiyatNet", GetiriFiyatNet, "F2");
            Add("GetiriPuanNet", GetiriPuanNet, "F2");
            Add("GetiriFiyatYuzdeNet", GetiriFiyatYuzdeNet, "F2");
            Add("GetiriPuanYuzdeNet", GetiriPuanYuzdeNet, "F2");
            Add("GetiriKz", GetiriKz, "F4");
            Add("GetiriKzNet", GetiriKzNet, "F4");
            Add("GetiriKzSistem", GetiriKzSistem, "F4");
            Add("GetiriKzSistemYuzde", GetiriKzSistemYuzde, "F2");
            Add("GetiriKzNetSistem", GetiriKzNetSistem, "F4");
            Add("GetiriKzNetSistemYuzde", GetiriKzNetSistemYuzde, "F2");

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- Min/Max Balance ---
            Add("MinBakiyeFiyat", MinBakiyeFiyat, "F2");
            Add("MaxBakiyeFiyat", MaxBakiyeFiyat, "F2");
            Add("MinBakiyePuan", MinBakiyePuan, "F2");
            Add("MaxBakiyePuan", MaxBakiyePuan, "F2");
            Add("MinBakiyeFiyatYuzde", MinBakiyeFiyatYuzde, "F2");
            Add("MaxBakiyeFiyatYuzde", MaxBakiyeFiyatYuzde, "F2");
            Add("MinBakiyeFiyatIndex", MinBakiyeFiyatIndex);
            Add("MaxBakiyeFiyatIndex", MaxBakiyeFiyatIndex);
            Add("MinBakiyeFiyatNet", MinBakiyeFiyatNet, "F2");
            Add("MaxBakiyeFiyatNet", MaxBakiyeFiyatNet, "F2");
            Add("MinBakiyeFiyatNetIndex", MinBakiyeFiyatNetIndex);
            Add("MaxBakiyeFiyatNetIndex", MaxBakiyeFiyatNetIndex);
            Add("MinBakiyeFiyatNetYuzde", MinBakiyeFiyatNetYuzde, "F2");
            Add("MaxBakiyeFiyatNetYuzde", MaxBakiyeFiyatNetYuzde, "F2");

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- Trade Counts ---
            Add("IslemSayisi", IslemSayisi);
            Add("AlisSayisi", AlisSayisi);
            Add("SatisSayisi", SatisSayisi);
            Add("FlatSayisi", FlatSayisi);
            Add("PassSayisi", PassSayisi);
            Add("KarAlSayisi", KarAlSayisi);
            Add("ZararKesSayisi", ZararKesSayisi);
            Add("KazandiranIslemSayisi", KazandiranIslemSayisi);
            Add("KaybettirenIslemSayisi", KaybettirenIslemSayisi);
            Add("NotrIslemSayisi", NotrIslemSayisi);
            Add("KazandiranAlisSayisi", KazandiranAlisSayisi);
            Add("KaybettirenAlisSayisi", KaybettirenAlisSayisi);
            Add("NotrAlisSayisi", NotrAlisSayisi);
            Add("KazandiranSatisSayisi", KazandiranSatisSayisi);
            Add("KaybettirenSatisSayisi", KaybettirenSatisSayisi);
            Add("NotrSatisSayisi", NotrSatisSayisi);

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- Command Counts ---
            Add("AlKomutSayisi", AlKomutSayisi);
            Add("SatKomutSayisi", SatKomutSayisi);
            Add("PasGecKomutSayisi", PasGecKomutSayisi);
            Add("KarAlKomutSayisi", KarAlKomutSayisi);
            Add("ZararKesKomutSayisi", ZararKesKomutSayisi);
            Add("FlatOlKomutSayisi", FlatOlKomutSayisi);

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- Commission ---
            Add("KomisyonIslemSayisi", KomisyonIslemSayisi);
            Add("KomisyonVarlikAdedSayisi", KomisyonVarlikAdedSayisi, "F2");
            Add("KomisyonVarlikAdedSayisiMicro", KomisyonVarlikAdedSayisiMicro, "F4");
            Add("KomisyonCarpan", KomisyonCarpan, "F4");
            Add("KomisyonFiyat", KomisyonFiyat, "F2");
            Add("KomisyonFiyatYuzde", KomisyonFiyatYuzde, "F4");
            Add("KomisyonuDahilEt", KomisyonuDahilEt);

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- PnL Aggregates ---
            Add("KarZararFiyat", KarZararFiyat, "F2");
            Add("KarZararFiyatYuzde", KarZararFiyatYuzde, "F2");
            Add("KarZararPuan", KarZararPuan, "F4");
            Add("ToplamKarFiyat", ToplamKarFiyat, "F2");
            Add("ToplamZararFiyat", ToplamZararFiyat, "F2");
            Add("NetKarFiyat", NetKarFiyat, "F2");
            Add("ToplamKarPuan", ToplamKarPuan, "F4");
            Add("ToplamZararPuan", ToplamZararPuan, "F4");
            Add("NetKarPuan", NetKarPuan, "F4");
            Add("MaxKarFiyat", MaxKarFiyat, "F2");
            Add("MaxZararFiyat", MaxZararFiyat, "F2");
            Add("MaxKarPuan", MaxKarPuan, "F4");
            Add("MaxZararPuan", MaxZararPuan, "F4");
            Add("KardaBarSayisi", KardaBarSayisi);
            Add("ZarardaBarSayisi", ZarardaBarSayisi);
            Add("KarliIslemOrani", KarliIslemOrani, "F2");

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- Risk Metrics ---
            Add("GetiriMaxDD", GetiriMaxDD, "F2");
            Add("GetiriMaxDDTarih", GetiriMaxDDTarih);
            Add("GetiriMaxKayip", GetiriMaxKayip, "F2");
            Add("ProfitFactor", ProfitFactor, "F2");
            Add("ProfitFactorNet", ProfitFactorNet, "F2");
            Add("ProfitFactorSistem", ProfitFactorSistem, "F2");

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- Signals & Execution ---
            Add("Sinyal", Sinyal);
            Add("SonYon", SonYon);
            Add("PrevYon", PrevYon);
            Add("SonFiyat", SonFiyat, "F4");
            Add("SonAFiyat", SonAFiyat, "F4");
            Add("SonSFiyat", SonSFiyat, "F4");
            Add("SonFFiyat", SonFFiyat, "F4");
            Add("SonPFiyat", SonPFiyat, "F4");
            Add("PrevFiyat", PrevFiyat, "F4");
            Add("SonBarNo", SonBarNo);
            Add("SonABarNo", SonABarNo);
            Add("SonSBarNo", SonSBarNo);
            Add("EmirKomut", EmirKomut);
            Add("EmirStatus", EmirStatus);

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- Asset & Position Info ---
            Add("HisseSayisi", HisseSayisi, "F2");
            Add("KontratSayisi", KontratSayisi, "F2");
            Add("VarlikAdedCarpani", VarlikAdedCarpani, "F2");
            Add("VarlikAdedSayisi", VarlikAdedSayisi, "F2");
            Add("VarlikAdedSayisiMicro", VarlikAdedSayisiMicro, "F4");
            Add("SonVarlikAdedSayisi", SonVarlikAdedSayisi, "F2");
            Add("SonVarlikAdedSayisiMicro", SonVarlikAdedSayisiMicro, "F4");
            Add("KaymaMiktari", KaymaMiktari, "F4");
            Add("KaymayiDahilEt", KaymayiDahilEt);

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            Add("MicroLotSizeEnabled", MicroLotSizeEnabled);
            Add("PyramidingEnabled", PyramidingEnabled);
            Add("MaxPositionSizeEnabled", MaxPositionSizeEnabled);
            Add("MaxPositionSize", MaxPositionSize, "F4");
            Add("MaxPositionSizeMicro", MaxPositionSizeMicro, "F4");

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- Periodic Returns ---
            Add("GetiriFiyatBuAy", GetiriFiyatBuAy, "F2");
            Add("GetiriFiyatAy1", GetiriFiyatAy1, "F2");
            Add("GetiriFiyatBuHafta", GetiriFiyatBuHafta, "F2");
            Add("GetiriFiyatHafta1", GetiriFiyatHafta1, "F2");
            Add("GetiriFiyatBuGun", GetiriFiyatBuGun, "F2");
            Add("GetiriFiyatGun1", GetiriFiyatGun1, "F2");
            Add("GetiriFiyatBuSaat", GetiriFiyatBuSaat, "F2");
            Add("GetiriFiyatSaat1", GetiriFiyatSaat1, "F2");

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            Add("GetiriPuanBuAy", GetiriPuanBuAy, "F4");
            Add("GetiriPuanAy1", GetiriPuanAy1, "F4");
            Add("GetiriPuanBuHafta", GetiriPuanBuHafta, "F4");
            Add("GetiriPuanHafta1", GetiriPuanHafta1, "F4");
            Add("GetiriPuanBuGun", GetiriPuanBuGun, "F4");
            Add("GetiriPuanGun1", GetiriPuanGun1, "F4");
            Add("GetiriPuanBuSaat", GetiriPuanBuSaat, "F4");
            Add("GetiriPuanSaat1", GetiriPuanSaat1, "F4");
        }

        public void SaveToTxt(string filePath)
        {
            AssignToMap();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"TRADING STATISTICS REPORT - {SistemName} ({GrafikSembol})");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
            sb.AppendLine("================================================================================");
            sb.AppendLine($"{"Property Name".PadRight(40)} : Value");
            sb.AppendLine("--------------------------------------------------------------------------------");

            foreach (var kvp in StatisticsMap)
            {
                if (kvp.Key.StartsWith(SEPARATOR))
                    sb.AppendLine();  // Boş satır
                else
                    sb.AppendLine($"{kvp.Key.PadRight(40)} : {kvp.Value}");
            }

            sb.AppendLine("================================================================================");
            WriteAllTextShared(filePath, sb.ToString());
        }

        public void SaveToCsv(string filePath)
        {
            AssignToMap();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Key;Value");

            foreach (var kvp in StatisticsMap)
            {
                if (!kvp.Key.StartsWith(SEPARATOR))  // SEPARATOR satırlarını CSV'ye ekleme
                    sb.AppendLine($"{kvp.Key};{kvp.Value}");
            }

            WriteAllTextShared(filePath, sb.ToString());
        }

        // Save bar-by-bar lists to TXT file (tabular format with fixed-width columns)
        public void SaveListsToTxt(string filePath)
        {
            if (Trader == null || Trader.Data == null || Trader.Data.Count == 0)
                return;

            using (StreamWriter writer = CreateSharedWriter(filePath))
            {
                // Title
                writer.WriteLine($"BAR-BY-BAR TRADING DATA (ALL) - {SistemName} ({GrafikSembol})");
                writer.WriteLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
                writer.WriteLine("".PadRight(500, '='));

                // Header
                writer.WriteLine(
                    $"{"BarNo",7} | " +
                    $"{"Date",10} | " +
                    $"{"Time",8} | " +
                    $"{"Open",10} | " +
                    $"{"High",10} | " +
                    $"{"Low",10} | " +
                    $"{"Close",10} | " +
                    $"{"Volume",10} | " +
                    $"{"Yon",3} | " +
                    $"{"Seviye",10} | " +
                    $"{"Sinyal",6} | " +
                    $"{"KzPuan",10} | " +
                    $"{"KzFiyat",10} | " +
                    $"{"KzPuan%",10} | " +
                    $"{"KzFiyat%",10} | " +
                    $"{"KarAl",10} | " +
                    $"{"ZararKes",10} | " +
                    $"{"IzStop",10} | " +
                    $"{"Islem",6} | " +
                    $"{"Alis",6} | " +
                    $"{"Satis",6} | " +
                    $"{"Flat",6} | " +
                    $"{"Pass",6} | " +
                    $"{"Kontrat",8} | " +
                    $"{"VarAded",8} | " +
                    $"{"VarAdedM",9} | " +
                    $"{"SnVarAd",8} | " +
                    $"{"SnVarAdM",9} | " +
                    $"{"KomVAded",9} | " +
                    $"{"KomVAdM",9} | " +
                    $"{"KomIslem",9} | " +
                    $"{"KomFiyat",10} | " +
                    $"{"KarBar",7} | " +
                    $"{"ZarBar",7} | " +
                    $"{"BakPuan",12} | " +
                    $"{"BakFiyat",12} | " +
                    $"{"GetPuan",12} | " +
                    $"{"GetFiyat",12} | " +
                    $"{"GetPuan%",10} | " +
                    $"{"GetFiyat%",10} | " +
                    $"{"BakPuanN",12} | " +
                    $"{"BakFiyatN",12} | " +
                    $"{"GetPuanN",12} | " +
                    $"{"GetFiyatN",12} | " +
                    $"{"GetPuan%N",10} | " +
                    $"{"GetFiyat%N",10} | " +
                    $"{"GetKz",10} | " +
                    $"{"GetKzNet",10} | " +
                    $"{"GetKzSis",10} | " +
                    $"{"GetKzSisN",10} | " +
                    $"{"EmirKmt",7} | " +
                    $"{"EmirSts",7} | " +
                    $"{"TrdEnbl",7} | " +
                    $"{"PozKpEnbl",9}"
                );
                writer.WriteLine("".PadRight(500, '-'));

                // Data rows - her satırı direkt dosyaya yaz, bellekte biriktirme
                for (int i = 0; i < Trader.Data.Count; i++)
                {
                    var bar = Trader.Data[i];

                    writer.WriteLine(
                        $"{i,7} | " +
                        $"{bar.Date:yyyy.MM.dd} | " +
                        $"{bar.DateTime:HH:mm:ss} | " +
                        $"{bar.Open,10:F2} | " +
                        $"{bar.High,10:F2} | " +
                        $"{bar.Low,10:F2} | " +
                        $"{bar.Close,10:F2} | " +
                        $"{bar.Volume,10:F0} | " +
                        $"{Trader.lists.YonList[i],3} | " +
                        $"{Trader.lists.SeviyeList[i],10:F2} | " +
                        $"{Trader.lists.SinyalList[i],6:F1} | " +
                        $"{Trader.lists.KarZararPuanList[i],10:F2} | " +
                        $"{Trader.lists.KarZararFiyatList[i],10:F2} | " +
                        $"{Trader.lists.KarZararPuanYuzdeList[i],10:F2} | " +
                        $"{Trader.lists.KarZararFiyatYuzdeList[i],10:F2} | " +
                        $"{Trader.lists.KarAlList[i],10:F2} | " +
                        $"{Trader.lists.ZararKesList[i],10:F2} | " +
                        $"{Trader.lists.IzleyenStopList[i],10:F2} | " +
                        $"{Trader.lists.IslemSayisiList[i],6} | " +
                        $"{Trader.lists.AlisSayisiList[i],6} | " +
                        $"{Trader.lists.SatisSayisiList[i],6} | " +
                        $"{Trader.lists.FlatSayisiList[i],6} | " +
                        $"{Trader.lists.PassSayisiList[i],6} | " +
                        $"{Trader.lists.KontratSayisiList[i],8:F2} | " +
                        $"{Trader.lists.VarlikAdedSayisiList[i],8:F2} | " +
                        $"{Trader.lists.VarlikAdedSayisiMicroList[i],9:F4} | " +
                        $"{Trader.lists.SonVarlikAdedSayisiList[i],8:F2} | " +
                        $"{Trader.lists.SonVarlikAdedSayisiMicroList[i],9:F4} | " +
                        $"{Trader.lists.KomisyonVarlikAdedSayisiList[i],9:F2} | " +
                        $"{Trader.lists.KomisyonVarlikAdedSayisiMicroList[i],9:F4} | " +
                        $"{Trader.lists.KomisyonIslemSayisiList[i],9} | " +
                        $"{Trader.lists.KomisyonFiyatList[i],10:F2} | " +
                        $"{Trader.lists.KardaBarSayisiList[i],7} | " +
                        $"{Trader.lists.ZarardaBarSayisiList[i],7} | " +
                        $"{Trader.lists.BakiyePuanList[i],12:F2} | " +
                        $"{Trader.lists.BakiyeFiyatList[i],12:F2} | " +
                        $"{Trader.lists.GetiriPuanList[i],12:F2} | " +
                        $"{Trader.lists.GetiriFiyatList[i],12:F2} | " +
                        $"{Trader.lists.GetiriPuanYuzdeList[i],10:F2} | " +
                        $"{Trader.lists.GetiriFiyatYuzdeList[i],10:F2} | " +
                        $"{Trader.lists.BakiyePuanNetList[i],12:F2} | " +
                        $"{Trader.lists.BakiyeFiyatNetList[i],12:F2} | " +
                        $"{Trader.lists.GetiriPuanNetList[i],12:F2} | " +
                        $"{Trader.lists.GetiriFiyatNetList[i],12:F2} | " +
                        $"{Trader.lists.GetiriPuanYuzdeNetList[i],10:F2} | " +
                        $"{Trader.lists.GetiriFiyatYuzdeNetList[i],10:F2} | " +
                        $"{Trader.lists.GetiriKz[i],10:F2} | " +
                        $"{Trader.lists.GetiriKzNet[i],10:F2} | " +
                        $"{Trader.lists.GetiriKzSistem[i],10:F2} | " +
                        $"{Trader.lists.GetiriKzNetSistem[i],10:F2} | " +
                        $"{Trader.lists.EmirKomutList[i],7:F0} | " +
                        $"{Trader.lists.EmirStatusList[i],7:F0} | " +
                        $"{Trader.lists.IsTradeEnabledList[i],7} | " +
                        $"{Trader.lists.IsPozKapatEnabledList[i],9}"
                    );
                }

                writer.WriteLine("".PadRight(500, '='));
            }
        }

        // Save bar-by-bar lists to CSV file (semicolon separated) - ALL COLUMNS
        public void SaveListsToCsv(string filePath)
        {
            if (Trader == null || Trader.Data == null || Trader.Data.Count == 0)
                return;

            using (StreamWriter writer = CreateSharedWriter(filePath))
            {
                // Header
                writer.WriteLine(
                    "BarNo;Date;Time;Open;High;Low;Close;Volume;" +
                    "Yon;Seviye;Sinyal;" +
                    "KarZararPuan;KarZararFiyat;KarZararPuanYuzde;KarZararFiyatYuzde;" +
                    "KarAl;ZararKes;IzleyenStop;" +
                    "IslemSayisi;AlisSayisi;SatisSayisi;FlatSayisi;PassSayisi;" +
                    "KontratSayisi;VarlikAdedSayisi;VarlikAdedSayisiMicro;SonVarlikAdedSayisi;SonVarlikAdedSayisiMicro;KomisyonVarlikAdedSayisi;KomisyonVarlikAdedSayisiMicro;KomisyonIslemSayisi;KomisyonFiyat;" +
                    "KardaBarSayisi;ZarardaBarSayisi;" +
                    "BakiyePuan;BakiyeFiyat;GetiriPuan;GetiriFiyat;GetiriPuanYuzde;GetiriFiyatYuzde;" +
                    "BakiyePuanNet;BakiyeFiyatNet;GetiriPuanNet;GetiriFiyatNet;GetiriPuanYuzdeNet;GetiriFiyatYuzdeNet;" +
                    "GetiriKz;GetiriKzNet;GetiriKzSistem;GetiriKzNetSistem;" +
                    "EmirKomut;EmirStatus;" +
                    "IsTradeEnabled;IsPozKapatEnabled"
                );

                // Data rows - her satırı direkt dosyaya yaz, bellekte biriktirme
                for (int i = 0; i < Trader.Data.Count; i++)
                {
                    var bar = Trader.Data[i];

                    writer.WriteLine(
                        $"{i};" +
                        $"{bar.Date:yyyy.MM.dd};" +
                        $"{bar.DateTime:HH:mm:ss};" +
                        $"{bar.Open:F2};" +
                        $"{bar.High:F2};" +
                        $"{bar.Low:F2};" +
                        $"{bar.Close:F2};" +
                        $"{bar.Volume:F0};" +
                        $"{Trader.lists.YonList[i]};" +
                        $"{Trader.lists.SeviyeList[i]:F2};" +
                        $"{Trader.lists.SinyalList[i]:F1};" +
                        $"{Trader.lists.KarZararPuanList[i]:F2};" +
                        $"{Trader.lists.KarZararFiyatList[i]:F2};" +
                        $"{Trader.lists.KarZararPuanYuzdeList[i]:F2};" +
                        $"{Trader.lists.KarZararFiyatYuzdeList[i]:F2};" +
                        $"{Trader.lists.KarAlList[i]:F2};" +
                        $"{Trader.lists.ZararKesList[i]:F2};" +
                        $"{Trader.lists.IzleyenStopList[i]:F2};" +
                        $"{Trader.lists.IslemSayisiList[i]};" +
                        $"{Trader.lists.AlisSayisiList[i]};" +
                        $"{Trader.lists.SatisSayisiList[i]};" +
                        $"{Trader.lists.FlatSayisiList[i]};" +
                        $"{Trader.lists.PassSayisiList[i]};" +
                        $"{Trader.lists.KontratSayisiList[i]:F2};" +
                        $"{Trader.lists.VarlikAdedSayisiList[i]:F2};" +
                        $"{Trader.lists.VarlikAdedSayisiMicroList[i]:F4};" +
                        $"{Trader.lists.SonVarlikAdedSayisiList[i]:F2};" +
                        $"{Trader.lists.SonVarlikAdedSayisiMicroList[i]:F4};" +
                        $"{Trader.lists.KomisyonVarlikAdedSayisiList[i]:F2};" +
                        $"{Trader.lists.KomisyonVarlikAdedSayisiMicroList[i]:F4};" +
                        $"{Trader.lists.KomisyonIslemSayisiList[i]};" +
                        $"{Trader.lists.KomisyonFiyatList[i]:F2};" +
                        $"{Trader.lists.KardaBarSayisiList[i]};" +
                        $"{Trader.lists.ZarardaBarSayisiList[i]};" +
                        $"{Trader.lists.BakiyePuanList[i]:F2};" +
                        $"{Trader.lists.BakiyeFiyatList[i]:F2};" +
                        $"{Trader.lists.GetiriPuanList[i]:F2};" +
                        $"{Trader.lists.GetiriFiyatList[i]:F2};" +
                        $"{Trader.lists.GetiriPuanYuzdeList[i]:F2};" +
                        $"{Trader.lists.GetiriFiyatYuzdeList[i]:F2};" +
                        $"{Trader.lists.BakiyePuanNetList[i]:F2};" +
                        $"{Trader.lists.BakiyeFiyatNetList[i]:F2};" +
                        $"{Trader.lists.GetiriPuanNetList[i]:F2};" +
                        $"{Trader.lists.GetiriFiyatNetList[i]:F2};" +
                        $"{Trader.lists.GetiriPuanYuzdeNetList[i]:F2};" +
                        $"{Trader.lists.GetiriFiyatYuzdeNetList[i]:F2};" +
                        $"{Trader.lists.GetiriKz[i]:F2};" +
                        $"{Trader.lists.GetiriKzNet[i]:F2};" +
                        $"{Trader.lists.GetiriKzSistem[i]:F2};" +
                        $"{Trader.lists.GetiriKzNetSistem[i]:F2};" +
                        $"{Trader.lists.EmirKomutList[i]:F0};" +
                        $"{Trader.lists.EmirStatusList[i]:F0};" +
                        $"{Trader.lists.IsTradeEnabledList[i]};" +
                        $"{Trader.lists.IsPozKapatEnabledList[i]}"
                    );
                }
            }
        }

        public void SaveToTxtFormatted(string filePath)
        {
            AssignToMap();
            StringBuilder sb = new StringBuilder();

            // Helper to get value safely
            string GetValue(string key)
            {
                return StatisticsMap.ContainsKey(key) ? StatisticsMap[key].ToString() : "...";
            }

            // === Header ===
            sb.AppendLine("================================================================================");
            sb.AppendLine("                    SINGLE TRADER RUN RESULTS - DETAILED REPORT");
            sb.AppendLine($"                    Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            // === Trader & System Information ===
            sb.AppendLine("┌─ TRADER & SYSTEM INFORMATION ──────────────────────────────────────────────┐");
            sb.AppendLine($"│ Trader ID          : {GetValue("TraderId"),-60} │");
            sb.AppendLine($"│ Trader Name        : {GetValue("TraderName"),-60} │");
            sb.AppendLine($"│ Symbol Name        : {GetValue("SymbolName"),-60} │");
            sb.AppendLine($"│ Symbol Period      : {GetValue("SymbolPeriod"),-60} │");
            sb.AppendLine($"│ System ID          : {GetValue("SystemId"),-60} │");
            sb.AppendLine($"│ System Name        : {GetValue("SystemName"),-60} │");
            sb.AppendLine($"│ Strategy ID        : {GetValue("StrategyId"),-60} │");
            sb.AppendLine($"│ Strategy Name      : {GetValue("StrategyName"),-60} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Execution Information ===
            sb.AppendLine("┌─ EXECUTION INFORMATION ────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Last Execution ID        : {GetValue("LastExecutionId"),-50} │");
            sb.AppendLine($"│ Last Execution Time      : {GetValue("LastExecutionTime"),-50} │");
            sb.AppendLine($"│ Execution Start          : {GetValue("LastExecutionTimeStart"),-50} │");
            sb.AppendLine($"│ Execution Stop           : {GetValue("LastExecutionTimeStop"),-50} │");
            sb.AppendLine($"│ Execution Time (ms)      : {GetValue("LastExecutionTimeInMSec"),-50} │");
            sb.AppendLine($"│ Last Reset Time          : {GetValue("LastResetTime"),-50} │");
            sb.AppendLine($"│ Statistics Calc Time     : {GetValue("LastStatisticsCalculationTime"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Bar Information ===
            sb.AppendLine("┌─ BAR INFORMATION ──────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Total Bars               : {GetValue("ToplamBarSayisi"),-50} │");
            sb.AppendLine($"│ Selected Bar Number      : {GetValue("SecilenBarNumarasi"),-50} │");
            sb.AppendLine($"│ Selected Bar DateTime    : {GetValue("SecilenBarTarihSaati"),-50} │");
            sb.AppendLine($"│ Selected Bar Date        : {GetValue("SecilenBarTarihi"),-50} │");
            sb.AppendLine($"│ Selected Bar Time        : {GetValue("SecilenBarSaati"),-50} │");
            sb.AppendLine($"│ First Bar DateTime       : {GetValue("IlkBarTarihSaati"),-50} │");
            sb.AppendLine($"│ First Bar Date           : {GetValue("IlkBarTarihi"),-50} │");
            sb.AppendLine($"│ First Bar Time           : {GetValue("IlkBarSaati"),-50} │");
            sb.AppendLine($"│ Last Bar DateTime        : {GetValue("SonBarTarihSaati"),-50} │");
            sb.AppendLine($"│ Last Bar Date            : {GetValue("SonBarTarihi"),-50} │");
            sb.AppendLine($"│ Last Bar Time            : {GetValue("SonBarSaati"),-50} │");
            sb.AppendLine($"│ First Bar Index          : {GetValue("IlkBarIndex"),-50} │");
            sb.AppendLine($"│ Last Bar Index           : {GetValue("SonBarIndex"),-50} │");
            sb.AppendLine($"│ Last Bar Open            : {GetValue("SonBarAcilisFiyati"),-50} │");
            sb.AppendLine($"│ Last Bar High            : {GetValue("SonBarYuksekFiyati"),-50} │");
            sb.AppendLine($"│ Last Bar Low             : {GetValue("SonBarDusukFiyati"),-50} │");
            sb.AppendLine($"│ Last Bar Close           : {GetValue("SonBarKapanisFiyati"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Time Statistics ===
            sb.AppendLine("┌─ TIME STATISTICS ──────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Total Months             : {GetValue("ToplamGecenSureAy"),-50} │");
            sb.AppendLine($"│ Total Days               : {GetValue("ToplamGecenSureGun"),-50} │");
            sb.AppendLine($"│ Total Hours              : {GetValue("ToplamGecenSureSaat"),-50} │");
            sb.AppendLine($"│ Total Minutes            : {GetValue("ToplamGecenSureDakika"),-50} │");
            sb.AppendLine($"│ Avg Monthly Trades       : {GetValue("OrtAylikIslemSayisi"),-50} │");
            sb.AppendLine($"│ Avg Weekly Trades        : {GetValue("OrtHaftalikIslemSayisi"),-50} │");
            sb.AppendLine($"│ Avg Daily Trades         : {GetValue("OrtGunlukIslemSayisi"),-50} │");
            sb.AppendLine($"│ Avg Hourly Trades        : {GetValue("OrtSaatlikIslemSayisi"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Balance & Returns ===
            sb.AppendLine("┌─ BALANCE & RETURNS ────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Initial Balance (Price)  : {GetValue("IlkBakiyeFiyat"),-50} │");
            sb.AppendLine($"│ Initial Balance (Points) : {GetValue("IlkBakiyePuan"),-50} │");
            sb.AppendLine($"│ Final Balance (Price)    : {GetValue("BakiyeFiyat"),-50} │");
            sb.AppendLine($"│ Final Balance (Points)   : {GetValue("BakiyePuan"),-50} │");
            sb.AppendLine($"│ Gross Return (Price)     : {GetValue("GetiriFiyat"),-50} │");
            sb.AppendLine($"│ Gross Return (Points)    : {GetValue("GetiriPuan"),-50} │");
            sb.AppendLine($"│ Gross Return % (Price)   : {GetValue("GetiriFiyatYuzde"),-50} │");
            sb.AppendLine($"│ Gross Return % (Points)  : {GetValue("GetiriPuanYuzde"),-50} │");
            sb.AppendLine($"│ Commission               : {GetValue("KomisyonFiyat"),-50} │");
            sb.AppendLine($"│ Net Balance (Price)      : {GetValue("BakiyeFiyatNet"),-50} │");
            sb.AppendLine($"│ Net Balance (Points)     : {GetValue("BakiyePuanNet"),-50} │");
            sb.AppendLine($"│ Net Return (Price)       : {GetValue("GetiriFiyatNet"),-50} │");
            sb.AppendLine($"│ Net Return (Points)      : {GetValue("GetiriPuanNet"),-50} │");
            sb.AppendLine($"│ Net Return % (Price)     : {GetValue("GetiriFiyatYuzdeNet"),-50} │");
            sb.AppendLine($"│ Net Return % (Points)    : {GetValue("GetiriPuanYuzdeNet"),-50} │");
            sb.AppendLine($"│ Return Kz                : {GetValue("GetiriKz"),-50} │");
            sb.AppendLine($"│ Return Kz Net            : {GetValue("GetiriKzNet"),-50} │");
            sb.AppendLine($"│ Return Kz System         : {GetValue("GetiriKzSistem"),-50} │");
            sb.AppendLine($"│ Return Kz System %       : {GetValue("GetiriKzSistemYuzde"),-50} │");
            sb.AppendLine($"│ Return Kz Net System     : {GetValue("GetiriKzNetSistem"),-50} │");
            sb.AppendLine($"│ Return Kz Net System %   : {GetValue("GetiriKzNetSistemYuzde"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Balance Min/Max ===
            sb.AppendLine("┌─ BALANCE MIN/MAX ──────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Min Balance (Price)      : {GetValue("MinBakiyeFiyat"),-50} │");
            sb.AppendLine($"│ Max Balance (Price)      : {GetValue("MaxBakiyeFiyat"),-50} │");
            sb.AppendLine($"│ Min Balance (Points)     : {GetValue("MinBakiyePuan"),-50} │");
            sb.AppendLine($"│ Max Balance (Points)     : {GetValue("MaxBakiyePuan"),-50} │");
            sb.AppendLine($"│ Min Balance %            : {GetValue("MinBakiyeFiyatYuzde"),-50} │");
            sb.AppendLine($"│ Max Balance %            : {GetValue("MaxBakiyeFiyatYuzde"),-50} │");
            sb.AppendLine($"│ Min Balance Index        : {GetValue("MinBakiyeFiyatIndex"),-50} │");
            sb.AppendLine($"│ Max Balance Index        : {GetValue("MaxBakiyeFiyatIndex"),-50} │");
            sb.AppendLine($"│ Min Balance Net          : {GetValue("MinBakiyeFiyatNet"),-50} │");
            sb.AppendLine($"│ Max Balance Net          : {GetValue("MaxBakiyeFiyatNet"),-50} │");
            sb.AppendLine($"│ Min Balance Net Index    : {GetValue("MinBakiyeFiyatNetIndex"),-50} │");
            sb.AppendLine($"│ Max Balance Net Index    : {GetValue("MaxBakiyeFiyatNetIndex"),-50} │");
            sb.AppendLine($"│ Min Balance Net %        : {GetValue("MinBakiyeFiyatNetYuzde"),-50} │");
            sb.AppendLine($"│ Max Balance Net %        : {GetValue("MaxBakiyeFiyatNetYuzde"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Trade Counts ===
            sb.AppendLine("┌─ TRADE COUNTS ─────────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Total Trades             : {GetValue("IslemSayisi"),-50} │");
            sb.AppendLine($"│ Buy Trades               : {GetValue("AlisSayisi"),-50} │");
            sb.AppendLine($"│ Sell Trades              : {GetValue("SatisSayisi"),-50} │");
            sb.AppendLine($"│ Flat Count               : {GetValue("FlatSayisi"),-50} │");
            sb.AppendLine($"│ Pass Count               : {GetValue("PassSayisi"),-50} │");
            sb.AppendLine($"│ Take Profit Count        : {GetValue("KarAlSayisi"),-50} │");
            sb.AppendLine($"│ Stop Loss Count          : {GetValue("ZararKesSayisi"),-50} │");
            sb.AppendLine($"│ Winning Trades           : {GetValue("KazandiranIslemSayisi"),-50} │");
            sb.AppendLine($"│ Losing Trades            : {GetValue("KaybettirenIslemSayisi"),-50} │");
            sb.AppendLine($"│ Neutral Trades           : {GetValue("NotrIslemSayisi"),-50} │");
            sb.AppendLine($"│ Winning Buys             : {GetValue("KazandiranAlisSayisi"),-50} │");
            sb.AppendLine($"│ Losing Buys              : {GetValue("KaybettirenAlisSayisi"),-50} │");
            sb.AppendLine($"│ Neutral Buys             : {GetValue("NotrAlisSayisi"),-50} │");
            sb.AppendLine($"│ Winning Sells            : {GetValue("KazandiranSatisSayisi"),-50} │");
            sb.AppendLine($"│ Losing Sells             : {GetValue("KaybettirenSatisSayisi"),-50} │");
            sb.AppendLine($"│ Neutral Sells            : {GetValue("NotrSatisSayisi"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Command Counts ===
            sb.AppendLine("┌─ COMMAND COUNTS ───────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Buy Commands             : {GetValue("AlKomutSayisi"),-50} │");
            sb.AppendLine($"│ Sell Commands            : {GetValue("SatKomutSayisi"),-50} │");
            sb.AppendLine($"│ Pass Commands            : {GetValue("PasGecKomutSayisi"),-50} │");
            sb.AppendLine($"│ Take Profit Commands     : {GetValue("KarAlKomutSayisi"),-50} │");
            sb.AppendLine($"│ Stop Loss Commands       : {GetValue("ZararKesKomutSayisi"),-50} │");
            sb.AppendLine($"│ Flat Commands            : {GetValue("FlatOlKomutSayisi"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Commission Details ===
            sb.AppendLine("┌─ COMMISSION DETAILS ───────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Commission Trades        : {GetValue("KomisyonIslemSayisi"),-50} │");
            sb.AppendLine($"│ Commission Asset Count   : {GetValue("KomisyonVarlikAdedSayisi"),-50} │");
            sb.AppendLine($"│ Commission Micro         : {GetValue("KomisyonVarlikAdedSayisiMicro"),-50} │");
            sb.AppendLine($"│ Commission Multiplier    : {GetValue("KomisyonCarpan"),-50} │");
            sb.AppendLine($"│ Total Commission         : {GetValue("KomisyonFiyat"),-50} │");
            sb.AppendLine($"│ Commission %             : {GetValue("KomisyonFiyatYuzde"),-50} │");
            sb.AppendLine($"│ Include Commission       : {GetValue("KomisyonuDahilEt"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Profit & Loss ===
            sb.AppendLine("┌─ PROFIT & LOSS ────────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ P&L (Price)              : {GetValue("KarZararFiyat"),-50} │");
            sb.AppendLine($"│ P&L % (Price)            : {GetValue("KarZararFiyatYuzde"),-50} │");
            sb.AppendLine($"│ P&L (Points)             : {GetValue("KarZararPuan"),-50} │");
            sb.AppendLine($"│ Total Profit (Price)     : {GetValue("ToplamKarFiyat"),-50} │");
            sb.AppendLine($"│ Total Loss (Price)       : {GetValue("ToplamZararFiyat"),-50} │");
            sb.AppendLine($"│ Net Profit (Price)       : {GetValue("NetKarFiyat"),-50} │");
            sb.AppendLine($"│ Total Profit (Points)    : {GetValue("ToplamKarPuan"),-50} │");
            sb.AppendLine($"│ Total Loss (Points)      : {GetValue("ToplamZararPuan"),-50} │");
            sb.AppendLine($"│ Net Profit (Points)      : {GetValue("NetKarPuan"),-50} │");
            sb.AppendLine($"│ Max Profit (Price)       : {GetValue("MaxKarFiyat"),-50} │");
            sb.AppendLine($"│ Max Loss (Price)         : {GetValue("MaxZararFiyat"),-50} │");
            sb.AppendLine($"│ Max Profit (Points)      : {GetValue("MaxKarPuan"),-50} │");
            sb.AppendLine($"│ Max Loss (Points)        : {GetValue("MaxZararPuan"),-50} │");
            sb.AppendLine($"│ Bars in Profit           : {GetValue("KardaBarSayisi"),-50} │");
            sb.AppendLine($"│ Bars in Loss             : {GetValue("ZarardaBarSayisi"),-50} │");
            sb.AppendLine($"│ Win Rate                 : {GetValue("KarliIslemOrani"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Risk Metrics ===
            sb.AppendLine("┌─ RISK METRICS ─────────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Max Drawdown             : {GetValue("GetiriMaxDD"),-50} │");
            sb.AppendLine($"│ Max Drawdown Date        : {GetValue("GetiriMaxDDTarih"),-50} │");
            sb.AppendLine($"│ Max Loss                 : {GetValue("GetiriMaxKayip"),-50} │");
            sb.AppendLine($"│ Profit Factor            : {GetValue("ProfitFactor"),-50} │");
            sb.AppendLine($"│ Profit Factor (Net)      : {GetValue("ProfitFactorNet"),-50} │");
            sb.AppendLine($"│ Profit Factor (System)   : {GetValue("ProfitFactorSistem"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Signals & Execution Status ===
            sb.AppendLine("┌─ SIGNALS & EXECUTION STATUS ───────────────────────────────────────────────┐");
            sb.AppendLine($"│ Signal                   : {GetValue("Sinyal"),-50} │");
            sb.AppendLine($"│ Last Direction           : {GetValue("SonYon"),-50} │");
            sb.AppendLine($"│ Previous Direction       : {GetValue("PrevYon"),-50} │");
            sb.AppendLine($"│ Last Price               : {GetValue("SonFiyat"),-50} │");
            sb.AppendLine($"│ Last Buy Price           : {GetValue("SonAFiyat"),-50} │");
            sb.AppendLine($"│ Last Sell Price          : {GetValue("SonSFiyat"),-50} │");
            sb.AppendLine($"│ Last Flat Price          : {GetValue("SonFFiyat"),-50} │");
            sb.AppendLine($"│ Last Pass Price          : {GetValue("SonPFiyat"),-50} │");
            sb.AppendLine($"│ Previous Price           : {GetValue("PrevFiyat"),-50} │");
            sb.AppendLine($"│ Last Bar Number          : {GetValue("SonBarNo"),-50} │");
            sb.AppendLine($"│ Last Buy Bar Number      : {GetValue("SonABarNo"),-50} │");
            sb.AppendLine($"│ Last Sell Bar Number     : {GetValue("SonSBarNo"),-50} │");
            sb.AppendLine($"│ Order Command            : {GetValue("EmirKomut"),-50} │");
            sb.AppendLine($"│ Order Status             : {GetValue("EmirStatus"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Asset & Position Info ===
            sb.AppendLine("┌─ ASSET & POSITION INFO ────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Share Count              : {GetValue("HisseSayisi"),-50} │");
            sb.AppendLine($"│ Contract Count           : {GetValue("KontratSayisi"),-50} │");
            sb.AppendLine($"│ Asset Multiplier         : {GetValue("VarlikAdedCarpani"),-50} │");
            sb.AppendLine($"│ Asset Count              : {GetValue("VarlikAdedSayisi"),-50} │");
            sb.AppendLine($"│ Asset Count (Micro)      : {GetValue("VarlikAdedSayisiMicro"),-50} │");
            sb.AppendLine($"│ Last Asset Count         : {GetValue("SonVarlikAdedSayisi"),-50} │");
            sb.AppendLine($"│ Last Asset Count (Micro) : {GetValue("SonVarlikAdedSayisiMicro"),-50} │");
            sb.AppendLine($"│ Slippage Amount          : {GetValue("KaymaMiktari"),-50} │");
            sb.AppendLine($"│ Include Slippage         : {GetValue("KaymayiDahilEt"),-50} │");
            sb.AppendLine($"│ Micro Lot Size Enabled   : {GetValue("MicroLotSizeEnabled"),-50} │");
            sb.AppendLine($"│ Pyramiding Enabled       : {GetValue("PyramidingEnabled"),-50} │");
            sb.AppendLine($"│ Max Position Size Enabled: {GetValue("MaxPositionSizeEnabled"),-50} │");
            sb.AppendLine($"│ Max Position Size        : {GetValue("MaxPositionSize"),-50} │");
            sb.AppendLine($"│ Max Position Size (Micro): {GetValue("MaxPositionSizeMicro"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Periodic Returns (Price) ===
            sb.AppendLine("┌─ PERIODIC RETURNS (PRICE) ─────────────────────────────────────────────────┐");
            sb.AppendLine($"│ This Month               : {GetValue("GetiriFiyatBuAy"),-50} │");
            sb.AppendLine($"│ Last Month               : {GetValue("GetiriFiyatAy1"),-50} │");
            sb.AppendLine($"│ This Week                : {GetValue("GetiriFiyatBuHafta"),-50} │");
            sb.AppendLine($"│ Last Week                : {GetValue("GetiriFiyatHafta1"),-50} │");
            sb.AppendLine($"│ Today                    : {GetValue("GetiriFiyatBuGun"),-50} │");
            sb.AppendLine($"│ Yesterday                : {GetValue("GetiriFiyatGun1"),-50} │");
            sb.AppendLine($"│ This Hour                : {GetValue("GetiriFiyatBuSaat"),-50} │");
            sb.AppendLine($"│ Last Hour                : {GetValue("GetiriFiyatSaat1"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Periodic Returns (Points) ===
            sb.AppendLine("┌─ PERIODIC RETURNS (POINTS) ────────────────────────────────────────────────┐");
            sb.AppendLine($"│ This Month (Pts)         : {GetValue("GetiriPuanBuAy"),-50} │");
            sb.AppendLine($"│ Last Month (Pts)         : {GetValue("GetiriPuanAy1"),-50} │");
            sb.AppendLine($"│ This Week (Pts)          : {GetValue("GetiriPuanBuHafta"),-50} │");
            sb.AppendLine($"│ Last Week (Pts)          : {GetValue("GetiriPuanHafta1"),-50} │");
            sb.AppendLine($"│ Today (Pts)              : {GetValue("GetiriPuanBuGun"),-50} │");
            sb.AppendLine($"│ Yesterday (Pts)          : {GetValue("GetiriPuanGun1"),-50} │");
            sb.AppendLine($"│ This Hour (Pts)          : {GetValue("GetiriPuanBuSaat"),-50} │");
            sb.AppendLine($"│ Last Hour (Pts)          : {GetValue("GetiriPuanSaat1"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            sb.AppendLine("================================================================================");
            sb.AppendLine("                              END OF REPORT");
            sb.AppendLine("================================================================================");

            WriteAllTextShared(filePath, sb.ToString());
        }

        private void AssignToMapMinimal()
        {
            int keyId = 0;

            StatisticsMapMinimal.Clear();

            // Helper to add null-safe and formatted values
            void Add(string key, object value, string format = "")
            {
                if (value == null || (value is string s && string.IsNullOrEmpty(s)))
                {
                    StatisticsMapMinimal[key] = "...";
                    return;
                }
                StatisticsMapMinimal[key] = string.IsNullOrEmpty(format) ? value.ToString() : string.Format("{0:" + format + "}", value);
            }

            // --- Identification ---
            Add("TraderId", Id);
            Add("TraderName", Name);

            StatisticsMapMinimal[SEPARATOR + keyId++.ToString()] = "";

            // --- System & Execution Info ---
            Add("SymbolName", GrafikSembol);
            Add("SymbolPeriod", GrafikPeriyot);
            Add("SystemId", SistemId);
            Add("SystemName", SistemName);
            Add("StrategyId", StrategyId);
            Add("StrategyName", StrategyName);

            StatisticsMapMinimal[SEPARATOR + keyId++.ToString()] = "";

            Add("LastExecutionId", LastExecutionId);
            Add("LastExecutionTime", LastExecutionTime);
            Add("LastExecutionTimeStart", LastExecutionTimeStart);
            Add("LastExecutionTimeStop", LastExecutionTimeStop);
            Add("LastExecutionTimeInMSec", LastExecutionTimeInMSec);
            Add("LastResetTime", LastResetTime);
            Add("LastStatisticsCalculationTime", LastStatisticsCalculationTime);

            StatisticsMapMinimal[SEPARATOR + keyId++.ToString()] = "";

            // --- Bar Info ---
            Add("ToplamBarSayisi", ToplamBarSayisi);
            Add("IlkBarTarihSaati", IlkBarTarihSaati);
            Add("IlkBarTarihi", IlkBarTarihi);
            Add("IlkBarSaati", IlkBarSaati);
            Add("SonBarTarihSaati", SonBarTarihSaati);
            Add("SonBarTarihi", SonBarTarihi);
            Add("SonBarSaati", SonBarSaati);
            Add("IlkBarIndex", IlkBarIndex);
            Add("SonBarIndex", SonBarIndex);

            StatisticsMapMinimal[SEPARATOR + keyId++.ToString()] = "";

            // --- Time Statistics ---
            Add("ToplamGecenSureAy", ToplamGecenSureAy, "F1");
            Add("ToplamGecenSureGun", ToplamGecenSureGun);
            Add("ToplamGecenSureSaat", ToplamGecenSureSaat);
            Add("ToplamGecenSureDakika", ToplamGecenSureDakika);
            Add("OrtAylikIslemSayisi", OrtAylikIslemSayisi, "F2");
            Add("OrtHaftalikIslemSayisi", OrtHaftalikIslemSayisi, "F2");
            Add("OrtGunlukIslemSayisi", OrtGunlukIslemSayisi, "F2");
            Add("OrtSaatlikIslemSayisi", OrtSaatlikIslemSayisi, "F2");

            StatisticsMapMinimal[SEPARATOR + keyId++.ToString()] = "";

            // --- Balance (Initial) ---
            Add("IlkBakiyeFiyat", IlkBakiyeFiyat, "F2");
            // --- Balance (Current) ---
            Add("BakiyeFiyat", BakiyeFiyat, "F2");
            Add("GetiriFiyat", GetiriFiyat, "F2");
            Add("GetiriFiyatYuzde", GetiriFiyatYuzde, "F2");
            Add("KomisyonFiyat", KomisyonFiyat, "F2");
            Add("BakiyeFiyatNet", BakiyeFiyatNet, "F2");
            Add("GetiriFiyatNet", GetiriFiyatNet, "F2");
            Add("GetiriFiyatYuzdeNet", GetiriFiyatYuzdeNet, "F2");

            StatisticsMapMinimal[SEPARATOR + keyId++.ToString()] = "";

            // --- Balance (Min/Max) ---
            Add("MinBakiyeFiyat", MinBakiyeFiyat, "F2");
            Add("MaxBakiyeFiyat", MaxBakiyeFiyat, "F2");
            Add("MinBakiyeFiyatYuzde", MinBakiyeFiyatYuzde, "F2");
            Add("MaxBakiyeFiyatYuzde", MaxBakiyeFiyatYuzde, "F2");
            Add("MinBakiyeFiyatIndex", MinBakiyeFiyatIndex);
            Add("MaxBakiyeFiyatIndex", MaxBakiyeFiyatIndex);
            Add("MinBakiyeFiyatNet", MinBakiyeFiyatNet, "F2");
            Add("MaxBakiyeFiyatNet", MaxBakiyeFiyatNet, "F2");
            Add("MinBakiyeFiyatNetIndex", MinBakiyeFiyatNetIndex);
            Add("MaxBakiyeFiyatNetIndex", MaxBakiyeFiyatNetIndex);
            Add("MinBakiyeFiyatNetYuzde", MinBakiyeFiyatNetYuzde, "F2");
            Add("MaxBakiyeFiyatNetYuzde", MaxBakiyeFiyatNetYuzde, "F2");

            StatisticsMapMinimal[SEPARATOR + keyId++.ToString()] = "";

            // --- Trade Counts ---
            Add("IslemSayisi", IslemSayisi);
            Add("AlisSayisi", AlisSayisi);
            Add("SatisSayisi", SatisSayisi);
            Add("FlatSayisi", FlatSayisi);
            Add("PassSayisi", PassSayisi);
            Add("KarAlSayisi", KarAlSayisi);
            Add("ZararKesSayisi", ZararKesSayisi);
            Add("KazandiranIslemSayisi", KazandiranIslemSayisi);
            Add("KaybettirenIslemSayisi", KaybettirenIslemSayisi);
            Add("NotrIslemSayisi", NotrIslemSayisi);

            StatisticsMapMinimal[SEPARATOR + keyId++.ToString()] = "";

            // --- Commission ---
            Add("KomisyonIslemSayisi", KomisyonIslemSayisi);
            Add("KomisyonVarlikAdedSayisi", KomisyonVarlikAdedSayisi, "F2");
            Add("KomisyonVarlikAdedSayisiMicro", KomisyonVarlikAdedSayisiMicro, "F4");
            Add("KomisyonCarpan", KomisyonCarpan, "F4");
            Add("KomisyonFiyat2", KomisyonFiyat, "F2");
            Add("KomisyonFiyatYuzde", KomisyonFiyatYuzde, "F4");
            Add("KomisyonuDahilEt", KomisyonuDahilEt);

            StatisticsMapMinimal[SEPARATOR + keyId++.ToString()] = "";

            // --- Performance Metrics ---
            Add("KarliIslemOrani", KarliIslemOrani, "F2");
            Add("GetiriMaxDD", GetiriMaxDD, "F2");
            Add("GetiriMaxDDTarih", GetiriMaxDDTarih);
            Add("GetiriMaxKayip", GetiriMaxKayip, "F2");
            Add("ProfitFactor", ProfitFactor, "F2");
            Add("ProfitFactorNet", ProfitFactorNet, "F2");

            StatisticsMapMinimal[SEPARATOR + keyId++.ToString()] = "";

            // --- Asset & Position Info ---
            Add("HisseSayisi", HisseSayisi, "F2");
            Add("KontratSayisi", KontratSayisi, "F2");
            Add("VarlikAdedCarpani", VarlikAdedCarpani, "F2");
            Add("VarlikAdedSayisi", VarlikAdedSayisi, "F2");
            Add("VarlikAdedSayisiMicro", VarlikAdedSayisiMicro, "F4");
            Add("SonVarlikAdedSayisi", SonVarlikAdedSayisi, "F2");
            Add("SonVarlikAdedSayisiMicro", SonVarlikAdedSayisiMicro, "F4");
            Add("KaymaMiktari", KaymaMiktari, "F4");
            Add("KaymayiDahilEt", KaymayiDahilEt);

            StatisticsMapMinimal[SEPARATOR + keyId++.ToString()] = "";
            Add("MicroLotSizeEnabled", MicroLotSizeEnabled);
            Add("PyramidingEnabled", PyramidingEnabled);
            Add("MaxPositionSizeEnabled", MaxPositionSizeEnabled);
            Add("MaxPositionSize", MaxPositionSize, "F4");
            Add("MaxPositionSizeMicro", MaxPositionSizeMicro, "F4");
        }
        
        public void SaveToTxtMinimal(string filePath)
        {
            AssignToMapMinimal();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"TRADING STATISTICS REPORT (MINIMAL) - {SistemName} ({GrafikSembol})");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
            sb.AppendLine("================================================================================");
            sb.AppendLine($"{"Property Name".PadRight(40)} : Value");
            sb.AppendLine("--------------------------------------------------------------------------------");

            foreach (var kvp in StatisticsMapMinimal)
            {
                if (kvp.Key.StartsWith(SEPARATOR))
                    sb.AppendLine();  // Boş satır
                else
                    sb.AppendLine($"{kvp.Key.PadRight(40)} : {kvp.Value}");
            }

            sb.AppendLine("================================================================================");
            WriteAllTextShared(filePath, sb.ToString());
        }

        public void SaveToCsvMinimal(string filePath)
        {
            AssignToMapMinimal();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Key;Value");

            foreach (var kvp in StatisticsMapMinimal)
            {
                if (!kvp.Key.StartsWith(SEPARATOR))  // SEPARATOR satırlarını CSV'ye ekleme
                    sb.AppendLine($"{kvp.Key};{kvp.Value}");
            }

            WriteAllTextShared(filePath, sb.ToString());
        }

        // Save bar-by-bar lists to TXT file (tabular format with fixed-width columns)
        public void SaveListsToTxtMinimal(string filePath)
        {
            if (Trader == null || Trader.Data == null || Trader.Data.Count == 0)
                return;

            using (StreamWriter writer = CreateSharedWriter(filePath))
            {
                // Title
                writer.WriteLine($"BAR-BY-BAR TRADING DATA - {SistemName} ({GrafikSembol})");
                writer.WriteLine($"Generated: {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
                writer.WriteLine("".PadRight(200, '='));

                // Header
                writer.WriteLine(
                    $"{"BarNo",7} | " +
                    $"{"Date",10} | " +
                    $"{"Time",8} | " +
                    $"{"Open",10} | " +
                    $"{"High",10} | " +
                    $"{"Low",10} | " +
                    $"{"Close",10} | " +
                    $"{"Volume",10} | " +
                    $"{"Yon",3} | " +
                    $"{"Seviye",10} | " +
                    $"{"Sinyal",6} | " +
                    $"{"KarZarar",10} | " +
                    $"{"Bakiye",12} | " +
                    $"{"Getiri",12} | " +
                    $"{"Komisyon",10} | " +
                    $"{"BakiyeNet",12} | " +
                    $"{"GetiriNet",12} | " +
                    $"{"IslemSay",8} | " +
                    $"{"EmirKmt",7} | " +
                    $"{"EmirSts",7} | " +
                    $"{"TrdEnbl",7} | " +
                    $"{"PozKpEnbl",9}"
                );
                writer.WriteLine("".PadRight(200, '-'));

                // Data rows - her satırı direkt dosyaya yaz, bellekte biriktirme
                for (int i = 0; i < Trader.Data.Count; i++)
                {
                    var bar = Trader.Data[i];

                    writer.WriteLine(
                        $"{i,7} | " +
                        $"{bar.Date:yyyy.MM.dd} | " +
                        $"{bar.DateTime:HH:mm:ss} | " +
                        $"{bar.Open,10:F2} | " +
                        $"{bar.High,10:F2} | " +
                        $"{bar.Low,10:F2} | " +
                        $"{bar.Close,10:F2} | " +
                        $"{bar.Volume,10:F0} | " +
                        $"{Trader.lists.YonList[i],3} | " +
                        $"{Trader.lists.SeviyeList[i],10:F2} | " +
                        $"{Trader.lists.SinyalList[i],6:F1} | " +
                        $"{Trader.lists.KarZararFiyatList[i],10:F2} | " +
                        $"{Trader.lists.BakiyeFiyatList[i],12:F2} | " +
                        $"{Trader.lists.GetiriFiyatList[i],12:F2} | " +
                        $"{Trader.lists.KomisyonFiyatList[i],10:F2} | " +
                        $"{Trader.lists.BakiyeFiyatNetList[i],12:F2} | " +
                        $"{Trader.lists.GetiriFiyatNetList[i],12:F2} | " +
                        $"{Trader.lists.IslemSayisiList[i],8} | " +
                        $"{Trader.lists.EmirKomutList[i],7} | " +
                        $"{Trader.lists.EmirStatusList[i],7} | " +
                        $"{Trader.lists.IsTradeEnabledList[i],7} | " +
                        $"{Trader.lists.IsPozKapatEnabledList[i],9}"
                    );
                }

                writer.WriteLine("".PadRight(200, '='));
            }
        }

        // Save bar-by-bar lists to CSV file (semicolon separated) - MINIMAL
        public void SaveListsToCsvMinimal(string filePath)
        {
            if (Trader == null || Trader.Data == null || Trader.Data.Count == 0)
                return;

            using (StreamWriter writer = CreateSharedWriter(filePath))
            {
                // Header
                writer.WriteLine(
                    "BarNo;Date;Time;Open;High;Low;Close;Volume;" +
                    "Yon;Seviye;Sinyal;" +
                    "KarZarar;Bakiye;Getiri;Komisyon;BakiyeNet;GetiriNet;" +
                    "IslemSayisi;EmirKomut;EmirStatus;" +
                    "IsTradeEnabled;IsPozKapatEnabled"
                );

                // Data rows - her satırı direkt dosyaya yaz, bellekte biriktirme
                for (int i = 0; i < Trader.Data.Count; i++)
                {
                    var bar = Trader.Data[i];

                    writer.WriteLine(
                        $"{i};" +
                        $"{bar.Date:yyyy.MM.dd};" +
                        $"{bar.DateTime:HH:mm:ss};" +
                        $"{bar.Open:F2};" +
                        $"{bar.High:F2};" +
                        $"{bar.Low:F2};" +
                        $"{bar.Close:F2};" +
                        $"{bar.Volume:F0};" +
                        $"{Trader.lists.YonList[i]};" +
                        $"{Trader.lists.SeviyeList[i]:F2};" +
                        $"{Trader.lists.SinyalList[i]:F1};" +
                        $"{Trader.lists.KarZararFiyatList[i]:F2};" +
                        $"{Trader.lists.BakiyeFiyatList[i]:F2};" +
                        $"{Trader.lists.GetiriFiyatList[i]:F2};" +
                        $"{Trader.lists.KomisyonFiyatList[i]:F2};" +
                        $"{Trader.lists.BakiyeFiyatNetList[i]:F2};" +
                        $"{Trader.lists.GetiriFiyatNetList[i]:F2};" +
                        $"{Trader.lists.IslemSayisiList[i]};" +
                        $"{Trader.lists.EmirKomutList[i]:F0};" +
                        $"{Trader.lists.EmirStatusList[i]:F0};" +
                        $"{Trader.lists.IsTradeEnabledList[i]};" +
                        $"{Trader.lists.IsPozKapatEnabledList[i]}"
                    );
                }
            }
        }

        public void SaveToTxtMinimalFormatted(string filePath)
        {
            AssignToMapMinimal();
            StringBuilder sb = new StringBuilder();

            // Helper to get value safely
            string GetValue(string key)
            {
                return StatisticsMapMinimal.ContainsKey(key) ? StatisticsMapMinimal[key].ToString() : "...";
            }

            // === Header ===
            sb.AppendLine("================================================================================");
            sb.AppendLine("                  SINGLE TRADER RUN RESULTS - MINIMAL REPORT");
            sb.AppendLine($"                    Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            // === Trader & System Information ===
            sb.AppendLine("┌─ TRADER & SYSTEM INFORMATION ──────────────────────────────────────────────┐");
            sb.AppendLine($"│ Trader ID          : {GetValue("TraderId"),-60} │");
            sb.AppendLine($"│ Trader Name        : {GetValue("TraderName"),-60} │");
            sb.AppendLine($"│ Symbol Name        : {GetValue("SymbolName"),-60} │");
            sb.AppendLine($"│ Symbol Period      : {GetValue("SymbolPeriod"),-60} │");
            sb.AppendLine($"│ System ID          : {GetValue("SystemId"),-60} │");
            sb.AppendLine($"│ System Name        : {GetValue("SystemName"),-60} │");
            sb.AppendLine($"│ Strategy ID        : {GetValue("StrategyId"),-60} │");
            sb.AppendLine($"│ Strategy Name      : {GetValue("StrategyName"),-60} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Execution Information ===
            sb.AppendLine("┌─ EXECUTION INFORMATION ────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Last Execution ID        : {GetValue("LastExecutionId"),-50} │");
            sb.AppendLine($"│ Last Execution Time      : {GetValue("LastExecutionTime"),-50} │");
            sb.AppendLine($"│ Execution Start          : {GetValue("LastExecutionTimeStart"),-50} │");
            sb.AppendLine($"│ Execution Stop           : {GetValue("LastExecutionTimeStop"),-50} │");
            sb.AppendLine($"│ Execution Time (ms)      : {GetValue("LastExecutionTimeInMSec"),-50} │");
            sb.AppendLine($"│ Last Reset Time          : {GetValue("LastResetTime"),-50} │");
            sb.AppendLine($"│ Statistics Calc Time     : {GetValue("LastStatisticsCalculationTime"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Bar Information ===
            sb.AppendLine("┌─ BAR INFORMATION ──────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Total Bars               : {GetValue("ToplamBarSayisi"),-50} │");
            sb.AppendLine($"│ First Bar DateTime       : {GetValue("IlkBarTarihSaati"),-50} │");
            sb.AppendLine($"│ First Bar Date           : {GetValue("IlkBarTarihi"),-50} │");
            sb.AppendLine($"│ First Bar Time           : {GetValue("IlkBarSaati"),-50} │");
            sb.AppendLine($"│ Last Bar DateTime        : {GetValue("SonBarTarihSaati"),-50} │");
            sb.AppendLine($"│ Last Bar Date            : {GetValue("SonBarTarihi"),-50} │");
            sb.AppendLine($"│ Last Bar Time            : {GetValue("SonBarSaati"),-50} │");
            sb.AppendLine($"│ First Bar Index          : {GetValue("IlkBarIndex"),-50} │");
            sb.AppendLine($"│ Last Bar Index           : {GetValue("SonBarIndex"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Time Statistics ===
            sb.AppendLine("┌─ TIME STATISTICS ──────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Total Months             : {GetValue("ToplamGecenSureAy"),-50} │");
            sb.AppendLine($"│ Total Days               : {GetValue("ToplamGecenSureGun"),-50} │");
            sb.AppendLine($"│ Total Hours              : {GetValue("ToplamGecenSureSaat"),-50} │");
            sb.AppendLine($"│ Total Minutes            : {GetValue("ToplamGecenSureDakika"),-50} │");
            sb.AppendLine($"│ Avg Monthly Trades       : {GetValue("OrtAylikIslemSayisi"),-50} │");
            sb.AppendLine($"│ Avg Weekly Trades        : {GetValue("OrtHaftalikIslemSayisi"),-50} │");
            sb.AppendLine($"│ Avg Daily Trades         : {GetValue("OrtGunlukIslemSayisi"),-50} │");
            sb.AppendLine($"│ Avg Hourly Trades        : {GetValue("OrtSaatlikIslemSayisi"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Balance & Returns (Price Only) ===
            sb.AppendLine("┌─ BALANCE & RETURNS ────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Initial Balance          : {GetValue("IlkBakiyeFiyat"),-50} │");
            sb.AppendLine($"│ Final Balance            : {GetValue("BakiyeFiyat"),-50} │");
            sb.AppendLine($"│ Gross Return             : {GetValue("GetiriFiyat"),-50} │");
            sb.AppendLine($"│ Gross Return %           : {GetValue("GetiriFiyatYuzde"),-50} │");
            sb.AppendLine($"│ Commission               : {GetValue("KomisyonFiyat"),-50} │");
            sb.AppendLine($"│ Net Balance              : {GetValue("BakiyeFiyatNet"),-50} │");
            sb.AppendLine($"│ Net Return               : {GetValue("GetiriFiyatNet"),-50} │");
            sb.AppendLine($"│ Net Return %             : {GetValue("GetiriFiyatYuzdeNet"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Balance Min/Max ===
            sb.AppendLine("┌─ BALANCE MIN/MAX ──────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Min Balance              : {GetValue("MinBakiyeFiyat"),-50} │");
            sb.AppendLine($"│ Max Balance              : {GetValue("MaxBakiyeFiyat"),-50} │");
            sb.AppendLine($"│ Min Balance %            : {GetValue("MinBakiyeFiyatYuzde"),-50} │");
            sb.AppendLine($"│ Max Balance %            : {GetValue("MaxBakiyeFiyatYuzde"),-50} │");
            sb.AppendLine($"│ Min Balance Index        : {GetValue("MinBakiyeFiyatIndex"),-50} │");
            sb.AppendLine($"│ Max Balance Index        : {GetValue("MaxBakiyeFiyatIndex"),-50} │");
            sb.AppendLine($"│ Min Balance Net          : {GetValue("MinBakiyeFiyatNet"),-50} │");
            sb.AppendLine($"│ Max Balance Net          : {GetValue("MaxBakiyeFiyatNet"),-50} │");
            sb.AppendLine($"│ Min Balance Net Index    : {GetValue("MinBakiyeFiyatNetIndex"),-50} │");
            sb.AppendLine($"│ Max Balance Net Index    : {GetValue("MaxBakiyeFiyatNetIndex"),-50} │");
            sb.AppendLine($"│ Min Balance Net %        : {GetValue("MinBakiyeFiyatNetYuzde"),-50} │");
            sb.AppendLine($"│ Max Balance Net %        : {GetValue("MaxBakiyeFiyatNetYuzde"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Trade Counts ===
            sb.AppendLine("┌─ TRADE COUNTS ─────────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Total Trades             : {GetValue("IslemSayisi"),-50} │");
            sb.AppendLine($"│ Buy Trades               : {GetValue("AlisSayisi"),-50} │");
            sb.AppendLine($"│ Sell Trades              : {GetValue("SatisSayisi"),-50} │");
            sb.AppendLine($"│ Flat Count               : {GetValue("FlatSayisi"),-50} │");
            sb.AppendLine($"│ Pass Count               : {GetValue("PassSayisi"),-50} │");
            sb.AppendLine($"│ Take Profit Count        : {GetValue("KarAlSayisi"),-50} │");
            sb.AppendLine($"│ Stop Loss Count          : {GetValue("ZararKesSayisi"),-50} │");
            sb.AppendLine($"│ Winning Trades           : {GetValue("KazandiranIslemSayisi"),-50} │");
            sb.AppendLine($"│ Losing Trades            : {GetValue("KaybettirenIslemSayisi"),-50} │");
            sb.AppendLine($"│ Neutral Trades           : {GetValue("NotrIslemSayisi"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Profit & Loss ===
            sb.AppendLine("┌─ PROFIT & LOSS ────────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Total Profit             : {GetValue("ToplamKarFiyat"),-50} │");
            sb.AppendLine($"│ Total Loss               : {GetValue("ToplamZararFiyat"),-50} │");
            sb.AppendLine($"│ Net Profit               : {GetValue("NetKarFiyat"),-50} │");
            sb.AppendLine($"│ Max Profit               : {GetValue("MaxKarFiyat"),-50} │");
            sb.AppendLine($"│ Max Loss                 : {GetValue("MaxZararFiyat"),-50} │");
            sb.AppendLine($"│ Win Rate                 : {GetValue("KarliIslemOrani"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // === Risk Metrics ===
            sb.AppendLine("┌─ RISK METRICS ─────────────────────────────────────────────────────────────┐");
            sb.AppendLine($"│ Max Drawdown             : {GetValue("GetiriMaxDD"),-50} │");
            sb.AppendLine($"│ Max Drawdown Date        : {GetValue("GetiriMaxDDTarih"),-50} │");
            sb.AppendLine($"│ Max Loss                 : {GetValue("GetiriMaxKayip"),-50} │");
            sb.AppendLine($"│ Profit Factor            : {GetValue("ProfitFactor"),-50} │");
            sb.AppendLine($"│ Profit Factor (Net)      : {GetValue("ProfitFactorNet"),-50} │");
            sb.AppendLine("└────────────────────────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            sb.AppendLine("================================================================================");
            sb.AppendLine("                              END OF REPORT");
            sb.AppendLine("================================================================================");

            WriteAllTextShared(filePath, sb.ToString());
        }

        #region Optimization Summary

        /// <summary>
        /// Complete optimization summary structure with all statistics
        /// Based on AssignToMap() - contains comprehensive trading metrics
        /// </summary>
        public struct OptimizationSummary
        {
            // --- Identification ---
            public int TraderId;
            public string TraderName;

            // --- System & Execution Info ---
            public string SymbolName;
            public string SymbolPeriod;
            public string SystemId;
            public string SystemName;
            public string StrategyId;
            public string StrategyName;

            public string LastExecutionId;
            public string LastExecutionTime;
            public string LastExecutionTimeStart;
            public string LastExecutionTimeStop;
            public string LastExecutionTimeInMSec;
            public string LastResetTime;
            public string LastStatisticsCalculationTime;

            // --- Bar Info ---
            public int ToplamBarSayisi;
            public int SecilenBarNumarasi;
            public string SecilenBarTarihSaati;
            public string SecilenBarTarihi;
            public string SecilenBarSaati;

            public string IlkBarTarihSaati;
            public string IlkBarTarihi;
            public string IlkBarSaati;

            public string SonBarTarihSaati;
            public string SonBarTarihi;
            public string SonBarSaati;

            public int IlkBarIndex;
            public int SonBarIndex;
            public double SonBarAcilisFiyati;
            public double SonBarYuksekFiyati;
            public double SonBarDusukFiyati;
            public double SonBarKapanisFiyati;

            // --- Time Statistics ---
            public double ToplamGecenSureAy;
            public int ToplamGecenSureGun;
            public int ToplamGecenSureSaat;
            public int ToplamGecenSureDakika;
            public double OrtAylikIslemSayisi;
            public double OrtHaftalikIslemSayisi;
            public double OrtGunlukIslemSayisi;
            public double OrtSaatlikIslemSayisi;

            // --- Balance & Returns ---
            public double IlkBakiyeFiyat;
            public double IlkBakiyePuan;
            public double BakiyeFiyat;
            public double BakiyePuan;
            public double GetiriFiyat;
            public double GetiriPuan;
            public double GetiriFiyatYuzde;
            public double GetiriPuanYuzde;
            public double BakiyeFiyatNet;
            public double BakiyePuanNet;
            public double GetiriFiyatNet;
            public double GetiriPuanNet;
            public double GetiriFiyatYuzdeNet;
            public double GetiriPuanYuzdeNet;
            public double GetiriKz;
            public double GetiriKzNet;
            public double GetiriKzSistem;
            public double GetiriKzSistemYuzde;
            public double GetiriKzNetSistem;
            public double GetiriKzNetSistemYuzde;

            // --- Min/Max Balance ---
            public double MinBakiyeFiyat;
            public double MaxBakiyeFiyat;
            public double MinBakiyePuan;
            public double MaxBakiyePuan;
            public double MinBakiyeFiyatYuzde;
            public double MaxBakiyeFiyatYuzde;
            public int MinBakiyeFiyatIndex;
            public int MaxBakiyeFiyatIndex;
            public double MinBakiyeFiyatNet;
            public double MaxBakiyeFiyatNet;
            public int MinBakiyeFiyatNetIndex;
            public int MaxBakiyeFiyatNetIndex;
            public double MinBakiyeFiyatNetYuzde;
            public double MaxBakiyeFiyatNetYuzde;

            // --- Trade Counts ---
            public int IslemSayisi;
            public int AlisSayisi;
            public int SatisSayisi;
            public int FlatSayisi;
            public int PassSayisi;
            public int KarAlSayisi;
            public int ZararKesSayisi;
            public int KazandiranIslemSayisi;
            public int KaybettirenIslemSayisi;
            public int NotrIslemSayisi;
            public int KazandiranAlisSayisi;
            public int KaybettirenAlisSayisi;
            public int NotrAlisSayisi;
            public int KazandiranSatisSayisi;
            public int KaybettirenSatisSayisi;
            public int NotrSatisSayisi;

            // --- Command Counts ---
            public int AlKomutSayisi;
            public int SatKomutSayisi;
            public int PasGecKomutSayisi;
            public int KarAlKomutSayisi;
            public int ZararKesKomutSayisi;
            public int FlatOlKomutSayisi;

            // --- Commission ---
            public int KomisyonIslemSayisi;
            public double KomisyonVarlikAdedSayisi;
            public double KomisyonVarlikAdedSayisiMicro;
            public double KomisyonCarpan;
            public double KomisyonFiyat;
            public double KomisyonFiyatYuzde;
            public bool KomisyonuDahilEt;

            // --- PnL Aggregates ---
            public double KarZararFiyat;
            public double KarZararFiyatYuzde;
            public double KarZararPuan;
            public double ToplamKarFiyat;
            public double ToplamZararFiyat;
            public double NetKarFiyat;
            public double ToplamKarPuan;
            public double ToplamZararPuan;
            public double NetKarPuan;
            public double MaxKarFiyat;
            public double MaxZararFiyat;
            public double MaxKarPuan;
            public double MaxZararPuan;
            public int KardaBarSayisi;
            public int ZarardaBarSayisi;
            public double KarliIslemOrani;

            // --- Risk Metrics ---
            public double GetiriMaxDD;
            public string GetiriMaxDDTarih;
            public double GetiriMaxKayip;
            public double ProfitFactor;
            public double ProfitFactorNet;
            public double ProfitFactorSistem;

            // --- Signals & Execution ---
            public string Sinyal;
            public string SonYon;
            public string PrevYon;
            public double SonFiyat;
            public double SonAFiyat;
            public double SonSFiyat;
            public double SonFFiyat;
            public double SonPFiyat;
            public double PrevFiyat;
            public int SonBarNo;
            public int SonABarNo;
            public int SonSBarNo;
            public string EmirKomut;
            public string EmirStatus;

            // --- Asset & Position Info ---
            public double HisseSayisi;
            public double KontratSayisi;
            public double VarlikAdedCarpani;
            public double VarlikAdedSayisi;
            public double VarlikAdedSayisiMicro;
            public double SonVarlikAdedSayisi;
            public double SonVarlikAdedSayisiMicro;
            public double KaymaMiktari;
            public bool KaymayiDahilEt;

            public bool MicroLotSizeEnabled;
            public bool PyramidingEnabled;
            public bool MaxPositionSizeEnabled;
            public double MaxPositionSize;
            public double MaxPositionSizeMicro;

            // --- Periodic Returns ---
            public double GetiriFiyatBuAy;
            public double GetiriFiyatAy1;
            public double GetiriFiyatBuHafta;
            public double GetiriFiyatHafta1;
            public double GetiriFiyatBuGun;
            public double GetiriFiyatGun1;
            public double GetiriFiyatBuSaat;
            public double GetiriFiyatSaat1;

            public double GetiriPuanBuAy;
            public double GetiriPuanAy1;
            public double GetiriPuanBuHafta;
            public double GetiriPuanHafta1;
            public double GetiriPuanBuGun;
            public double GetiriPuanGun1;
            public double GetiriPuanBuSaat;
            public double GetiriPuanSaat1;

            /// <summary>
            /// Get CSV header (semicolon separated) - comprehensive version
            /// </summary>
            public static string GetCsvHeader()
            {
                return "TraderId;TraderName;SymbolName;SymbolPeriod;SystemId;SystemName;StrategyId;StrategyName;" +
                       "LastExecutionId;LastExecutionTime;LastExecutionTimeStart;LastExecutionTimeStop;LastExecutionTimeInMSec;LastResetTime;LastStatisticsCalculationTime;" +
                       "ToplamBarSayisi;SecilenBarNumarasi;SecilenBarTarihSaati;SecilenBarTarihi;SecilenBarSaati;" +
                       "IlkBarTarihSaati;IlkBarTarihi;IlkBarSaati;SonBarTarihSaati;SonBarTarihi;SonBarSaati;" +
                       "IlkBarIndex;SonBarIndex;SonBarAcilisFiyati;SonBarYuksekFiyati;SonBarDusukFiyati;SonBarKapanisFiyati;" +
                       "ToplamGecenSureAy;ToplamGecenSureGun;ToplamGecenSureSaat;ToplamGecenSureDakika;" +
                       "OrtAylikIslemSayisi;OrtHaftalikIslemSayisi;OrtGunlukIslemSayisi;OrtSaatlikIslemSayisi;" +
                       "IlkBakiyeFiyat;IlkBakiyePuan;BakiyeFiyat;BakiyePuan;GetiriFiyat;GetiriPuan;GetiriFiyatYuzde;GetiriPuanYuzde;" +
                       "BakiyeFiyatNet;BakiyePuanNet;GetiriFiyatNet;GetiriPuanNet;GetiriFiyatYuzdeNet;GetiriPuanYuzdeNet;" +
                       "GetiriKz;GetiriKzNet;GetiriKzSistem;GetiriKzSistemYuzde;GetiriKzNetSistem;GetiriKzNetSistemYuzde;" +
                       "MinBakiyeFiyat;MaxBakiyeFiyat;MinBakiyePuan;MaxBakiyePuan;MinBakiyeFiyatYuzde;MaxBakiyeFiyatYuzde;" +
                       "MinBakiyeFiyatIndex;MaxBakiyeFiyatIndex;MinBakiyeFiyatNet;MaxBakiyeFiyatNet;MinBakiyeFiyatNetIndex;MaxBakiyeFiyatNetIndex;MinBakiyeFiyatNetYuzde;MaxBakiyeFiyatNetYuzde;" +
                       "IslemSayisi;AlisSayisi;SatisSayisi;FlatSayisi;PassSayisi;KarAlSayisi;ZararKesSayisi;" +
                       "KazandiranIslemSayisi;KaybettirenIslemSayisi;NotrIslemSayisi;" +
                       "KazandiranAlisSayisi;KaybettirenAlisSayisi;NotrAlisSayisi;KazandiranSatisSayisi;KaybettirenSatisSayisi;NotrSatisSayisi;" +
                       "AlKomutSayisi;SatKomutSayisi;PasGecKomutSayisi;KarAlKomutSayisi;ZararKesKomutSayisi;FlatOlKomutSayisi;" +
                       "KomisyonIslemSayisi;KomisyonVarlikAdedSayisi;KomisyonVarlikAdedSayisiMicro;KomisyonCarpan;KomisyonFiyat;KomisyonFiyatYuzde;KomisyonuDahilEt;" +
                       "KarZararFiyat;KarZararFiyatYuzde;KarZararPuan;ToplamKarFiyat;ToplamZararFiyat;NetKarFiyat;" +
                       "ToplamKarPuan;ToplamZararPuan;NetKarPuan;MaxKarFiyat;MaxZararFiyat;MaxKarPuan;MaxZararPuan;" +
                       "KardaBarSayisi;ZarardaBarSayisi;KarliIslemOrani;" +
                       "GetiriMaxDD;GetiriMaxDDTarih;GetiriMaxKayip;ProfitFactor;ProfitFactorNet;ProfitFactorSistem;" +
                       "Sinyal;SonYon;PrevYon;SonFiyat;SonAFiyat;SonSFiyat;SonFFiyat;SonPFiyat;PrevFiyat;" +
                       "SonBarNo;SonABarNo;SonSBarNo;EmirKomut;EmirStatus;" +
                       "HisseSayisi;KontratSayisi;VarlikAdedCarpani;VarlikAdedSayisi;VarlikAdedSayisiMicro;SonVarlikAdedSayisi;SonVarlikAdedSayisiMicro;KaymaMiktari;KaymayiDahilEt;" +
                       "MicroLotSizeEnabled;PyramidingEnabled;MaxPositionSizeEnabled;MaxPositionSize;MaxPositionSizeMicro;" +
                       "GetiriFiyatBuAy;GetiriFiyatAy1;GetiriFiyatBuHafta;GetiriFiyatHafta1;GetiriFiyatBuGun;GetiriFiyatGun1;GetiriFiyatBuSaat;GetiriFiyatSaat1;" +
                       "GetiriPuanBuAy;GetiriPuanAy1;GetiriPuanBuHafta;GetiriPuanHafta1;GetiriPuanBuGun;GetiriPuanGun1;GetiriPuanBuSaat;GetiriPuanSaat1";
            }

            /// <summary>
            /// Convert to CSV row (semicolon separated) - comprehensive version
            /// </summary>
            public string ToCsvRow()
            {
                return $"{TraderId};{TraderName};{SymbolName};{SymbolPeriod};{SystemId};{SystemName};{StrategyId};{StrategyName};" +
                       $"{LastExecutionId};{LastExecutionTime};{LastExecutionTimeStart};{LastExecutionTimeStop};{LastExecutionTimeInMSec};{LastResetTime};{LastStatisticsCalculationTime};" +
                       $"{ToplamBarSayisi};{SecilenBarNumarasi};{SecilenBarTarihSaati};{SecilenBarTarihi};{SecilenBarSaati};" +
                       $"{IlkBarTarihSaati};{IlkBarTarihi};{IlkBarSaati};{SonBarTarihSaati};{SonBarTarihi};{SonBarSaati};" +
                       $"{IlkBarIndex};{SonBarIndex};{SonBarAcilisFiyati:F4};{SonBarYuksekFiyati:F4};{SonBarDusukFiyati:F4};{SonBarKapanisFiyati:F4};" +
                       $"{ToplamGecenSureAy:F1};{ToplamGecenSureGun};{ToplamGecenSureSaat};{ToplamGecenSureDakika};" +
                       $"{OrtAylikIslemSayisi:F2};{OrtHaftalikIslemSayisi:F2};{OrtGunlukIslemSayisi:F2};{OrtSaatlikIslemSayisi:F2};" +
                       $"{IlkBakiyeFiyat:F2};{IlkBakiyePuan:F2};{BakiyeFiyat:F2};{BakiyePuan:F2};{GetiriFiyat:F2};{GetiriPuan:F4};{GetiriFiyatYuzde:F2};{GetiriPuanYuzde:F2};" +
                       $"{BakiyeFiyatNet:F2};{BakiyePuanNet:F2};{GetiriFiyatNet:F2};{GetiriPuanNet:F4};{GetiriFiyatYuzdeNet:F2};{GetiriPuanYuzdeNet:F2};" +
                       $"{GetiriKz:F4};{GetiriKzNet:F4};{GetiriKzSistem:F4};{GetiriKzSistemYuzde:F2};{GetiriKzNetSistem:F4};{GetiriKzNetSistemYuzde:F2};" +
                       $"{MinBakiyeFiyat:F2};{MaxBakiyeFiyat:F2};{MinBakiyePuan:F2};{MaxBakiyePuan:F2};{MinBakiyeFiyatYuzde:F2};{MaxBakiyeFiyatYuzde:F2};" +
                       $"{MinBakiyeFiyatIndex};{MaxBakiyeFiyatIndex};{MinBakiyeFiyatNet:F2};{MaxBakiyeFiyatNet:F2};{MinBakiyeFiyatNetIndex};{MaxBakiyeFiyatNetIndex};{MinBakiyeFiyatNetYuzde:F2};{MaxBakiyeFiyatNetYuzde:F2};" +
                       $"{IslemSayisi};{AlisSayisi};{SatisSayisi};{FlatSayisi};{PassSayisi};{KarAlSayisi};{ZararKesSayisi};" +
                       $"{KazandiranIslemSayisi};{KaybettirenIslemSayisi};{NotrIslemSayisi};" +
                       $"{KazandiranAlisSayisi};{KaybettirenAlisSayisi};{NotrAlisSayisi};{KazandiranSatisSayisi};{KaybettirenSatisSayisi};{NotrSatisSayisi};" +
                       $"{AlKomutSayisi};{SatKomutSayisi};{PasGecKomutSayisi};{KarAlKomutSayisi};{ZararKesKomutSayisi};{FlatOlKomutSayisi};" +
                       $"{KomisyonIslemSayisi};{KomisyonVarlikAdedSayisi:F2};{KomisyonVarlikAdedSayisiMicro:F4};{KomisyonCarpan:F4};{KomisyonFiyat:F2};{KomisyonFiyatYuzde:F4};{KomisyonuDahilEt};" +
                       $"{KarZararFiyat:F2};{KarZararFiyatYuzde:F2};{KarZararPuan:F4};{ToplamKarFiyat:F2};{ToplamZararFiyat:F2};{NetKarFiyat:F2};" +
                       $"{ToplamKarPuan:F4};{ToplamZararPuan:F4};{NetKarPuan:F4};{MaxKarFiyat:F2};{MaxZararFiyat:F2};{MaxKarPuan:F4};{MaxZararPuan:F4};" +
                       $"{KardaBarSayisi};{ZarardaBarSayisi};{KarliIslemOrani:F2};" +
                       $"{GetiriMaxDD:F2};{GetiriMaxDDTarih};{GetiriMaxKayip:F2};{ProfitFactor:F2};{ProfitFactorNet:F2};{ProfitFactorSistem:F2};" +
                       $"{Sinyal};{SonYon};{PrevYon};{SonFiyat:F4};{SonAFiyat:F4};{SonSFiyat:F4};{SonFFiyat:F4};{SonPFiyat:F4};{PrevFiyat:F4};" +
                       $"{SonBarNo};{SonABarNo};{SonSBarNo};{EmirKomut};{EmirStatus};" +
                       $"{HisseSayisi:F2};{KontratSayisi:F2};{VarlikAdedCarpani:F2};{VarlikAdedSayisi:F2};{VarlikAdedSayisiMicro:F4};{SonVarlikAdedSayisi:F2};{SonVarlikAdedSayisiMicro:F4};{KaymaMiktari:F4};{KaymayiDahilEt};" +
                       $"{MicroLotSizeEnabled};{PyramidingEnabled};{MaxPositionSizeEnabled};{MaxPositionSize:F4};{MaxPositionSizeMicro:F4};" +
                       $"{GetiriFiyatBuAy:F2};{GetiriFiyatAy1:F2};{GetiriFiyatBuHafta:F2};{GetiriFiyatHafta1:F2};{GetiriFiyatBuGun:F2};{GetiriFiyatGun1:F2};{GetiriFiyatBuSaat:F2};{GetiriFiyatSaat1:F2};" +
                       $"{GetiriPuanBuAy:F4};{GetiriPuanAy1:F4};{GetiriPuanBuHafta:F4};{GetiriPuanHafta1:F4};{GetiriPuanBuGun:F4};{GetiriPuanGun1:F4};{GetiriPuanBuSaat:F4};{GetiriPuanSaat1:F4}";
            }

            /// <summary>
            /// Convert to TXT row (tabular format with fixed-width columns) - comprehensive version
            /// </summary>
            public string ToTxtRow()
            {
                return $"{TraderId,5} | " +
                       $"{TraderName,20} | " +
                       $"{SymbolName,10} | " +
                       $"{SymbolPeriod,6} | " +
                       $"{StrategyName,30} | " +
                       $"{LastExecutionTimeInMSec,10} | " +
                       $"{IslemSayisi,6} | " +
                       $"{KazandiranIslemSayisi,6} | " +
                       $"{KaybettirenIslemSayisi,6} | " +
                       $"{GetiriFiyat,12:F2} | " +
                       $"{GetiriFiyatYuzde,10:F2} | " +
                       $"{GetiriFiyatNet,12:F2} | " +
                       $"{GetiriFiyatYuzdeNet,10:F2} | " +
                       $"{KomisyonFiyat,10:F2} | " +
                       $"{ProfitFactor,8:F2} | " +
                       $"{ProfitFactorNet,8:F2} | " +
                       $"{GetiriMaxDD,10:F2} | " +
                       $"{KarliIslemOrani,10:F2}";
            }

            /// <summary>
            /// Get TXT header (tabular format with fixed-width columns)
            /// </summary>
            public static string GetTxtHeader()
            {
                return $"{"ID",5} | " +
                       $"{"Trader Name",20} | " +
                       $"{"Symbol",10} | " +
                       $"{"Period",6} | " +
                       $"{"Strategy Name",30} | " +
                       $"{"ExecMs",10} | " +
                       $"{"Islem",6} | " +
                       $"{"Kaz",6} | " +
                       $"{"Kayb",6} | " +
                       $"{"GetiriFiyat",12} | " +
                       $"{"Getiri%",10} | " +
                       $"{"GetiriNet",12} | " +
                       $"{"GetiriNet%",10} | " +
                       $"{"Komisyon",10} | " +
                       $"{"ProfitF",8} | " +
                       $"{"ProfitFNet",8} | " +
                       $"{"MaxDD%",10} | " +
                       $"{"KarliOran",10}";
            }

            /// <summary>
            /// Get TXT separator line
            /// </summary>
            public static string GetTxtSeparator()
            {
                return "".PadRight(230, '-');
            }
        }

        /// <summary>
        /// Get complete optimization summary structure with all statistics
        /// Call this after Hesapla() to get comprehensive optimization metrics
        /// </summary>
        public OptimizationSummary GetOptimizationSummary()
        {
            // Ensure maps are populated (in case Hesapla wasn't called yet)
            if (StatisticsMap.Count == 0)
                AssignToMap();

            return new OptimizationSummary
            {
                // --- Identification ---
                TraderId = Id,
                TraderName = Name ?? "...",

                // --- System & Execution Info ---
                SymbolName = GrafikSembol ?? "...",
                SymbolPeriod = GrafikPeriyot ?? "...",
                SystemId = SistemId ?? "...",
                SystemName = SistemName ?? "...",
                StrategyId = StrategyId ?? "...",
                StrategyName = StrategyName ?? "...",

                LastExecutionId = LastExecutionId ?? "...",
                LastExecutionTime = LastExecutionTime ?? "...",
                LastExecutionTimeStart = LastExecutionTimeStart ?? "...",
                LastExecutionTimeStop = LastExecutionTimeStop ?? "...",
                LastExecutionTimeInMSec = LastExecutionTimeInMSec ?? "...",
                LastResetTime = LastResetTime ?? "...",
                LastStatisticsCalculationTime = LastStatisticsCalculationTime ?? "...",

                // --- Bar Info ---
                ToplamBarSayisi = ToplamBarSayisi,
                SecilenBarNumarasi = SecilenBarNumarasi,
                SecilenBarTarihSaati = SecilenBarTarihSaati ?? "...",
                SecilenBarTarihi = SecilenBarTarihi ?? "...",
                SecilenBarSaati = SecilenBarSaati ?? "...",

                IlkBarTarihSaati = IlkBarTarihSaati ?? "...",
                IlkBarTarihi = IlkBarTarihi ?? "...",
                IlkBarSaati = IlkBarSaati ?? "...",

                SonBarTarihSaati = SonBarTarihSaati ?? "...",
                SonBarTarihi = SonBarTarihi ?? "...",
                SonBarSaati = SonBarSaati ?? "...",

                IlkBarIndex = IlkBarIndex,
                SonBarIndex = SonBarIndex,
                SonBarAcilisFiyati = SonBarAcilisFiyati,
                SonBarYuksekFiyati = SonBarYuksekFiyati,
                SonBarDusukFiyati = SonBarDusukFiyati,
                SonBarKapanisFiyati = SonBarKapanisFiyati,

                // --- Time Statistics ---
                ToplamGecenSureAy = ToplamGecenSureAy,
                ToplamGecenSureGun = ToplamGecenSureGun,
                ToplamGecenSureSaat = ToplamGecenSureSaat,
                ToplamGecenSureDakika = ToplamGecenSureDakika,
                OrtAylikIslemSayisi = OrtAylikIslemSayisi,
                OrtHaftalikIslemSayisi = OrtHaftalikIslemSayisi,
                OrtGunlukIslemSayisi = OrtGunlukIslemSayisi,
                OrtSaatlikIslemSayisi = OrtSaatlikIslemSayisi,

                // --- Balance & Returns ---
                IlkBakiyeFiyat = IlkBakiyeFiyat,
                IlkBakiyePuan = IlkBakiyePuan,
                BakiyeFiyat = BakiyeFiyat,
                BakiyePuan = BakiyePuan,
                GetiriFiyat = GetiriFiyat,
                GetiriPuan = GetiriPuan,
                GetiriFiyatYuzde = GetiriFiyatYuzde,
                GetiriPuanYuzde = GetiriPuanYuzde,
                BakiyeFiyatNet = BakiyeFiyatNet,
                BakiyePuanNet = BakiyePuanNet,
                GetiriFiyatNet = GetiriFiyatNet,
                GetiriPuanNet = GetiriPuanNet,
                GetiriFiyatYuzdeNet = GetiriFiyatYuzdeNet,
                GetiriPuanYuzdeNet = GetiriPuanYuzdeNet,
                GetiriKz = GetiriKz,
                GetiriKzNet = GetiriKzNet,
                GetiriKzSistem = GetiriKzSistem,
                GetiriKzSistemYuzde = GetiriKzSistemYuzde,
                GetiriKzNetSistem = GetiriKzNetSistem,
                GetiriKzNetSistemYuzde = GetiriKzNetSistemYuzde,

                // --- Min/Max Balance ---
                MinBakiyeFiyat = MinBakiyeFiyat,
                MaxBakiyeFiyat = MaxBakiyeFiyat,
                MinBakiyePuan = MinBakiyePuan,
                MaxBakiyePuan = MaxBakiyePuan,
                MinBakiyeFiyatYuzde = MinBakiyeFiyatYuzde,
                MaxBakiyeFiyatYuzde = MaxBakiyeFiyatYuzde,
                MinBakiyeFiyatIndex = MinBakiyeFiyatIndex,
                MaxBakiyeFiyatIndex = MaxBakiyeFiyatIndex,
                MinBakiyeFiyatNet = MinBakiyeFiyatNet,
                MaxBakiyeFiyatNet = MaxBakiyeFiyatNet,
                MinBakiyeFiyatNetIndex = MinBakiyeFiyatNetIndex,
                MaxBakiyeFiyatNetIndex = MaxBakiyeFiyatNetIndex,
                MinBakiyeFiyatNetYuzde = MinBakiyeFiyatNetYuzde,
                MaxBakiyeFiyatNetYuzde = MaxBakiyeFiyatNetYuzde,

                // --- Trade Counts ---
                IslemSayisi = IslemSayisi,
                AlisSayisi = AlisSayisi,
                SatisSayisi = SatisSayisi,
                FlatSayisi = FlatSayisi,
                PassSayisi = PassSayisi,
                KarAlSayisi = KarAlSayisi,
                ZararKesSayisi = ZararKesSayisi,
                KazandiranIslemSayisi = KazandiranIslemSayisi,
                KaybettirenIslemSayisi = KaybettirenIslemSayisi,
                NotrIslemSayisi = NotrIslemSayisi,
                KazandiranAlisSayisi = KazandiranAlisSayisi,
                KaybettirenAlisSayisi = KaybettirenAlisSayisi,
                NotrAlisSayisi = NotrAlisSayisi,
                KazandiranSatisSayisi = KazandiranSatisSayisi,
                KaybettirenSatisSayisi = KaybettirenSatisSayisi,
                NotrSatisSayisi = NotrSatisSayisi,

                // --- Command Counts ---
                AlKomutSayisi = AlKomutSayisi,
                SatKomutSayisi = SatKomutSayisi,
                PasGecKomutSayisi = PasGecKomutSayisi,
                KarAlKomutSayisi = KarAlKomutSayisi,
                ZararKesKomutSayisi = ZararKesKomutSayisi,
                FlatOlKomutSayisi = FlatOlKomutSayisi,

                // --- Commission ---
                KomisyonIslemSayisi = KomisyonIslemSayisi,
                KomisyonVarlikAdedSayisi = KomisyonVarlikAdedSayisi,
                KomisyonVarlikAdedSayisiMicro = KomisyonVarlikAdedSayisiMicro,
                KomisyonCarpan = KomisyonCarpan,
                KomisyonFiyat = KomisyonFiyat,
                KomisyonFiyatYuzde = KomisyonFiyatYuzde,
                KomisyonuDahilEt = KomisyonuDahilEt,

                // --- PnL Aggregates ---
                KarZararFiyat = KarZararFiyat,
                KarZararFiyatYuzde = KarZararFiyatYuzde,
                KarZararPuan = KarZararPuan,
                ToplamKarFiyat = ToplamKarFiyat,
                ToplamZararFiyat = ToplamZararFiyat,
                NetKarFiyat = NetKarFiyat,
                ToplamKarPuan = ToplamKarPuan,
                ToplamZararPuan = ToplamZararPuan,
                NetKarPuan = NetKarPuan,
                MaxKarFiyat = MaxKarFiyat,
                MaxZararFiyat = MaxZararFiyat,
                MaxKarPuan = MaxKarPuan,
                MaxZararPuan = MaxZararPuan,
                KardaBarSayisi = KardaBarSayisi,
                ZarardaBarSayisi = ZarardaBarSayisi,
                KarliIslemOrani = KarliIslemOrani,

                // --- Risk Metrics ---
                GetiriMaxDD = GetiriMaxDD,
                GetiriMaxDDTarih = GetiriMaxDDTarih ?? "...",
                GetiriMaxKayip = GetiriMaxKayip,
                ProfitFactor = ProfitFactor,
                ProfitFactorNet = ProfitFactorNet,
                ProfitFactorSistem = ProfitFactorSistem,

                // --- Signals & Execution ---
                Sinyal = Sinyal ?? "...",
                SonYon = SonYon ?? "...",
                PrevYon = PrevYon ?? "...",
                SonFiyat = SonFiyat,
                SonAFiyat = SonAFiyat,
                SonSFiyat = SonSFiyat,
                SonFFiyat = SonFFiyat,
                SonPFiyat = SonPFiyat,
                PrevFiyat = PrevFiyat,
                SonBarNo = SonBarNo,
                SonABarNo = SonABarNo,
                SonSBarNo = SonSBarNo,
                EmirKomut = EmirKomut.ToString(),
                EmirStatus = EmirStatus.ToString(),

                // --- Asset & Position Info ---
                HisseSayisi = HisseSayisi,
                KontratSayisi = KontratSayisi,
                VarlikAdedCarpani = VarlikAdedCarpani,
                VarlikAdedSayisi = VarlikAdedSayisi,
                VarlikAdedSayisiMicro = VarlikAdedSayisiMicro,
                SonVarlikAdedSayisi = SonVarlikAdedSayisi,
                SonVarlikAdedSayisiMicro = SonVarlikAdedSayisiMicro,
                KaymaMiktari = KaymaMiktari,
                KaymayiDahilEt = KaymayiDahilEt,

                MicroLotSizeEnabled = MicroLotSizeEnabled,
                PyramidingEnabled = PyramidingEnabled,
                MaxPositionSizeEnabled = MaxPositionSizeEnabled,
                MaxPositionSize = MaxPositionSize,
                MaxPositionSizeMicro = MaxPositionSizeMicro,

                // --- Periodic Returns ---
                GetiriFiyatBuAy = GetiriFiyatBuAy,
                GetiriFiyatAy1 = GetiriFiyatAy1,
                GetiriFiyatBuHafta = GetiriFiyatBuHafta,
                GetiriFiyatHafta1 = GetiriFiyatHafta1,
                GetiriFiyatBuGun = GetiriFiyatBuGun,
                GetiriFiyatGun1 = GetiriFiyatGun1,
                GetiriFiyatBuSaat = GetiriFiyatBuSaat,
                GetiriFiyatSaat1 = GetiriFiyatSaat1,

                GetiriPuanBuAy = GetiriPuanBuAy,
                GetiriPuanAy1 = GetiriPuanAy1,
                GetiriPuanBuHafta = GetiriPuanBuHafta,
                GetiriPuanHafta1 = GetiriPuanHafta1,
                GetiriPuanBuGun = GetiriPuanBuGun,
                GetiriPuanGun1 = GetiriPuanGun1,
                GetiriPuanBuSaat = GetiriPuanBuSaat,
                GetiriPuanSaat1 = GetiriPuanSaat1
            };
        }

        /// <summary>
        /// Optimization summary structure (Minimal) for fast CSV/TXT export during optimization runs
        /// Contains essential metrics for strategy optimization and comparison
        /// </summary>
        public struct OptimizationSummaryMinimal
        {
            // Identification
            public int TraderId;
            public string TraderName;
            public string Symbol;
            public string Period;
            public string StrategyId;
            public string StrategyName;

            // Execution Info
            public string ExecutionId;
            public string ExecutionTime;
            public string ExecutionTimeMs;

            // Bar Info
            public int ToplamBarSayisi;
            public string IlkBarTarihi;
            public string SonBarTarihi;

            // Trade Counts
            public int IslemSayisi;
            public int AlisSayisi;
            public int SatisSayisi;
            public int FlatSayisi;
            public int PassSayisi;
            public int KazandiranIslemSayisi;
            public int KaybettirenIslemSayisi;
            public int NotrIslemSayisi;

            // Balance & Returns (Gross)
            public double IlkBakiyeFiyat;
            public double BakiyeFiyat;
            public double GetiriFiyat;
            public double GetiriFiyatYuzde;

            // Commission
            public double KomisyonFiyat;
            public double KomisyonFiyatYuzde;

            // Balance & Returns (Net)
            public double BakiyeFiyatNet;
            public double GetiriFiyatNet;
            public double GetiriFiyatYuzdeNet;

            // Min/Max
            public double MinBakiyeFiyat;
            public double MaxBakiyeFiyat;
            public double MinBakiyeFiyatYuzde;
            public double MaxBakiyeFiyatYuzde;
            public double MinBakiyeFiyatNet;
            public double MaxBakiyeFiyatNet;
            public double MinBakiyeFiyatNetYuzde;
            public double MaxBakiyeFiyatNetYuzde;

            // Performance Metrics
            public double ProfitFactor;
            public double ProfitFactorNet;
            public double KarliIslemOrani;
            public double GetiriMaxDD;
            public double GetiriMaxKayip;
            public string GetiriMaxDDTarih;

            // Position Info
            public double VarlikAdedSayisi;
            public double VarlikAdedSayisiMicro;
            public double SonVarlikAdedSayisi;
            public double SonVarlikAdedSayisiMicro;
            public double KomisyonCarpan;
            public bool MicroLotSizeEnabled;
            public bool PyramidingEnabled;
            public bool MaxPositionSizeEnabled;

            /// <summary>
            /// Get CSV header (semicolon separated)
            /// </summary>
            public static string GetCsvHeader()
            {
                return "TraderId;TraderName;Symbol;Period;StrategyId;StrategyName;" +
                       "ExecutionId;ExecutionTime;ExecutionTimeMs;" +
                       "ToplamBarSayisi;IlkBarTarihi;SonBarTarihi;" +
                       "IslemSayisi;AlisSayisi;SatisSayisi;FlatSayisi;PassSayisi;" +
                       "KazandiranIslemSayisi;KaybettirenIslemSayisi;NotrIslemSayisi;" +
                       "IlkBakiyeFiyat;BakiyeFiyat;GetiriFiyat;GetiriFiyatYuzde;" +
                       "KomisyonFiyat;KomisyonFiyatYuzde;" +
                       "BakiyeFiyatNet;GetiriFiyatNet;GetiriFiyatYuzdeNet;" +
                       "MinBakiyeFiyat;MaxBakiyeFiyat;MinBakiyeFiyatYuzde;MaxBakiyeFiyatYuzde;" +
                       "MinBakiyeFiyatNet;MaxBakiyeFiyatNet;MinBakiyeFiyatNetYuzde;MaxBakiyeFiyatNetYuzde;" +
                       "ProfitFactor;ProfitFactorNet;KarliIslemOrani;GetiriMaxDD;GetiriMaxKayip;GetiriMaxDDTarih;" +
                       "VarlikAdedSayisi;VarlikAdedSayisiMicro;SonVarlikAdedSayisi;SonVarlikAdedSayisiMicro;KomisyonCarpan;" +
                       "MicroLotSizeEnabled;PyramidingEnabled;MaxPositionSizeEnabled";
            }

            /// <summary>
            /// Convert to CSV row (semicolon separated)
            /// </summary>
            public string ToCsvRow()
            {
                return $"{TraderId};{TraderName};{Symbol};{Period};{StrategyId};{StrategyName};" +
                       $"{ExecutionId};{ExecutionTime};{ExecutionTimeMs};" +
                       $"{ToplamBarSayisi};{IlkBarTarihi};{SonBarTarihi};" +
                       $"{IslemSayisi};{AlisSayisi};{SatisSayisi};{FlatSayisi};{PassSayisi};" +
                       $"{KazandiranIslemSayisi};{KaybettirenIslemSayisi};{NotrIslemSayisi};" +
                       $"{IlkBakiyeFiyat:F2};{BakiyeFiyat:F2};{GetiriFiyat:F2};{GetiriFiyatYuzde:F2};" +
                       $"{KomisyonFiyat:F2};{KomisyonFiyatYuzde:F4};" +
                       $"{BakiyeFiyatNet:F2};{GetiriFiyatNet:F2};{GetiriFiyatYuzdeNet:F2};" +
                       $"{MinBakiyeFiyat:F2};{MaxBakiyeFiyat:F2};{MinBakiyeFiyatYuzde:F2};{MaxBakiyeFiyatYuzde:F2};" +
                       $"{MinBakiyeFiyatNet:F2};{MaxBakiyeFiyatNet:F2};{MinBakiyeFiyatNetYuzde:F2};{MaxBakiyeFiyatNetYuzde:F2};" +
                       $"{ProfitFactor:F2};{ProfitFactorNet:F2};{KarliIslemOrani:F2};{GetiriMaxDD:F2};{GetiriMaxKayip:F2};{GetiriMaxDDTarih};" +
                       $"{VarlikAdedSayisi:F2};{VarlikAdedSayisiMicro:F4};{SonVarlikAdedSayisi:F2};{SonVarlikAdedSayisiMicro:F4};{KomisyonCarpan:F4};" +
                       $"{MicroLotSizeEnabled};{PyramidingEnabled};{MaxPositionSizeEnabled}";
            }

            /// <summary>
            /// Convert to TXT row (tabular format with fixed-width columns)
            /// </summary>
            public string ToTxtRow()
            {
                return $"{TraderId,5} | " +
                       $"{TraderName,20} | " +
                       $"{Symbol,10} | " +
                       $"{Period,6} | " +
                       $"{StrategyName,30} | " +
                       $"{ExecutionTimeMs,10} | " +
                       $"{IslemSayisi,6} | " +
                       $"{KazandiranIslemSayisi,6} | " +
                       $"{KaybettirenIslemSayisi,6} | " +
                       $"{GetiriFiyat,12:F2} | " +
                       $"{GetiriFiyatYuzde,10:F2} | " +
                       $"{GetiriFiyatNet,12:F2} | " +
                       $"{GetiriFiyatYuzdeNet,10:F2} | " +
                       $"{KomisyonFiyat,10:F2} | " +
                       $"{ProfitFactor,8:F2} | " +
                       $"{ProfitFactorNet,8:F2} | " +
                       $"{GetiriMaxDD,10:F2} | " +
                       $"{KarliIslemOrani,10:F2}";
            }

            /// <summary>
            /// Get TXT header (tabular format with fixed-width columns)
            /// </summary>
            public static string GetTxtHeader()
            {
                return $"{"ID",5} | " +
                       $"{"Trader Name",20} | " +
                       $"{"Symbol",10} | " +
                       $"{"Period",6} | " +
                       $"{"Strategy Name",30} | " +
                       $"{"ExecMs",10} | " +
                       $"{"Islem",6} | " +
                       $"{"Kaz",6} | " +
                       $"{"Kayb",6} | " +
                       $"{"GetiriFiyat",12} | " +
                       $"{"Getiri%",10} | " +
                       $"{"GetiriNet",12} | " +
                       $"{"GetiriNet%",10} | " +
                       $"{"Komisyon",10} | " +
                       $"{"ProfitF",8} | " +
                       $"{"ProfitFNet",8} | " +
                       $"{"MaxDD%",10} | " +
                       $"{"KarliOran",10}";
            }

            /// <summary>
            /// Get TXT separator line
            /// </summary>
            public static string GetTxtSeparator()
            {
                return "".PadRight(230, '-');
            }
        }

        /// <summary>
        /// Get optimization summary structure (Minimal)
        /// Call this after Hesapla() to get essential optimization metrics
        /// </summary>
        public OptimizationSummaryMinimal GetOptimizationSummaryMinimal()
        {
            // Ensure maps are populated (in case Hesapla wasn't called yet)
            if (StatisticsMapMinimal.Count == 0)
                AssignToMapMinimal();

            return new OptimizationSummaryMinimal
            {
                // Identification
                TraderId = Id,
                TraderName = Name ?? "...",
                Symbol = GrafikSembol ?? "...",
                Period = GrafikPeriyot ?? "...",
                StrategyId = StrategyId ?? "...",
                StrategyName = StrategyName ?? "...",

                // Execution Info
                ExecutionId = LastExecutionId ?? "...",
                ExecutionTime = LastExecutionTime ?? "...",
                ExecutionTimeMs = LastExecutionTimeInMSec ?? "...",

                // Bar Info
                ToplamBarSayisi = ToplamBarSayisi,
                IlkBarTarihi = IlkBarTarihi ?? "...",
                SonBarTarihi = SonBarTarihi ?? "...",

                // Trade Counts
                IslemSayisi = IslemSayisi,
                AlisSayisi = AlisSayisi,
                SatisSayisi = SatisSayisi,
                FlatSayisi = FlatSayisi,
                PassSayisi = PassSayisi,
                KazandiranIslemSayisi = KazandiranIslemSayisi,
                KaybettirenIslemSayisi = KaybettirenIslemSayisi,
                NotrIslemSayisi = NotrIslemSayisi,

                // Balance & Returns (Gross)
                IlkBakiyeFiyat = IlkBakiyeFiyat,
                BakiyeFiyat = BakiyeFiyat,
                GetiriFiyat = GetiriFiyat,
                GetiriFiyatYuzde = GetiriFiyatYuzde,

                // Commission
                KomisyonFiyat = KomisyonFiyat,
                KomisyonFiyatYuzde = KomisyonFiyatYuzde,

                // Balance & Returns (Net)
                BakiyeFiyatNet = BakiyeFiyatNet,
                GetiriFiyatNet = GetiriFiyatNet,
                GetiriFiyatYuzdeNet = GetiriFiyatYuzdeNet,

                // Min/Max
                MinBakiyeFiyat = MinBakiyeFiyat,
                MaxBakiyeFiyat = MaxBakiyeFiyat,
                MinBakiyeFiyatYuzde = MinBakiyeFiyatYuzde,
                MaxBakiyeFiyatYuzde = MaxBakiyeFiyatYuzde,
                MinBakiyeFiyatNet = MinBakiyeFiyatNet,
                MaxBakiyeFiyatNet = MaxBakiyeFiyatNet,
                MinBakiyeFiyatNetYuzde = MinBakiyeFiyatNetYuzde,
                MaxBakiyeFiyatNetYuzde = MaxBakiyeFiyatNetYuzde,

                // Performance Metrics
                ProfitFactor = ProfitFactor,
                ProfitFactorNet = ProfitFactorNet,
                KarliIslemOrani = KarliIslemOrani,
                GetiriMaxDD = GetiriMaxDD,
                GetiriMaxKayip = GetiriMaxKayip,
                GetiriMaxDDTarih = GetiriMaxDDTarih ?? "...",

                // Position Info
                VarlikAdedSayisi = VarlikAdedSayisi,
                VarlikAdedSayisiMicro = VarlikAdedSayisiMicro,
                SonVarlikAdedSayisi = SonVarlikAdedSayisi,
                SonVarlikAdedSayisiMicro = SonVarlikAdedSayisiMicro,
                KomisyonCarpan = KomisyonCarpan,
                MicroLotSizeEnabled = MicroLotSizeEnabled,
                PyramidingEnabled = PyramidingEnabled,
                MaxPositionSizeEnabled = MaxPositionSizeEnabled
            };
        }

        #endregion

        #region Optimization Summary - Helper Methods (Optional)

        /// <summary>
        /// Append optimization summary to CSV file
        /// Helper method - alternatively use GetOptimizationSummaryMinimal() and handle file writing in Optimization Manager
        /// </summary>
        public void AppendToOptimizationCsv(string filePath, bool writeHeader = false, bool useMinimal = false)
        {
            if (useMinimal)
            {
                var summary = GetOptimizationSummaryMinimal();

                if (writeHeader)
                {
                    WriteAllTextShared(filePath, OptimizationSummaryMinimal.GetCsvHeader() + Environment.NewLine);
                }

                AppendAllTextShared(filePath, summary.ToCsvRow() + Environment.NewLine);
            }
            else
            {
                var summary = GetOptimizationSummary();

                if (writeHeader)
                {
                    WriteAllTextShared(filePath, OptimizationSummary.GetCsvHeader() + Environment.NewLine);
                }

                AppendAllTextShared(filePath, summary.ToCsvRow() + Environment.NewLine);
            }
        }

        /// <summary>
        /// Append optimization summary to TXT file (tabular format)
        /// Helper method - alternatively use GetOptimizationSummaryMinimal() and handle file writing in Optimization Manager
        /// </summary>
        public void AppendToOptimizationTxt(string filePath, bool writeHeader = false, bool useMinimal = false)
        {
            if (useMinimal)
            {
                var summary = GetOptimizationSummaryMinimal();

                if (writeHeader)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"OPTIMIZATION RESULTS - {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
                    sb.AppendLine(OptimizationSummaryMinimal.GetTxtSeparator());
                    sb.AppendLine(OptimizationSummaryMinimal.GetTxtHeader());
                    sb.AppendLine(OptimizationSummaryMinimal.GetTxtSeparator());
                    WriteAllTextShared(filePath, sb.ToString());
                }

                AppendAllTextShared(filePath, summary.ToTxtRow() + Environment.NewLine);
            }
            else
            {
                var summary = GetOptimizationSummary();

                if (writeHeader)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"OPTIMIZATION RESULTS - {DateTime.Now:yyyy.MM.dd HH:mm:ss}");
                    sb.AppendLine(OptimizationSummary.GetTxtSeparator());
                    sb.AppendLine(OptimizationSummary.GetTxtHeader());
                    sb.AppendLine(OptimizationSummary.GetTxtSeparator());
                    WriteAllTextShared(filePath, sb.ToString());
                }

                AppendAllTextShared(filePath, summary.ToTxtRow() + Environment.NewLine);
            }
        }

        #endregion


        #endregion
    }
}
