using System;
using System.Collections.Generic;
using System.Globalization;
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
        public double ToplamGecenSureHafta { get; set; }
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
        public double MaxKarFiyatNet { get; set; }
        public double MaxZararFiyatNet { get; set; }
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
        //public double GetiriKz => Trader?.status?.GetiriKz ?? 0;  // Silinecek
        //public double GetiriKzNet => Trader?.status?.GetiriKzNet ?? 0;  // Silinecek
        //public double GetiriKzSistem => Trader?.status?.GetiriKzSistem ?? 0;  // Silinecek
        //public double GetiriKzNetSistem => Trader?.status?.GetiriKzNetSistem ?? 0;  // Silinecek
        //public double GetiriKzSistemYuzde { get; set; }  // Silinecek
        //public double GetiriKzNetSistemYuzde { get; set; }  // Silinecek
        public int GetiriFiyatTipi { get; set; }

        #endregion

        #region Balance Min/Max

        public double MinBakiyeFiyat { get; set; }
        public double MaxBakiyeFiyat { get; set; }
        public double MinBakiyePuan { get; set; }
        public double MaxBakiyePuan { get; set; }
        public double MinBakiyeFiyatYuzde { get; set; }
        public double MaxBakiyeFiyatYuzde { get; set; }
        public double MinBakiyePuanYuzde { get; set; }
        public double MaxBakiyePuanYuzde { get; set; }
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
        public double GetiriMaxDDPuan { get; set; }
        public string GetiriMaxDDPuanTarih { get; set; }
        public double GetiriMaxKayipPuan { get; set; }
        public double GetiriMaxDDNet { get; set; }
        public string GetiriMaxDDNetTarih { get; set; }
        public double GetiriMaxKayipNet { get; set; }

        #endregion

        #region Performance Metrics

        public double ProfitFactor { get; set; }
        public double ProfitFactorPuan { get; set; }
        public double ProfitFactorNet { get; set; }  // Commission-adjusted profit factor
        public double ProfitFactorSistem { get; set; }
        public double KarliIslemOrani { get; set; }

        #endregion

        #region Performans Trades

        public sealed class PerformansRow
        {
            public int No { get; set; }
            public string Yon { get; set; } = "";
            public double KontratSayisi { get; set; }
            public DateTime AcilisTarihSaat { get; set; }
            public double AcilisFiyati { get; set; }
            public DateTime KapanisTarihSaat { get; set; }
            public double KapanisFiyati { get; set; }
            public double KarZararPuan { get; set; }
            public double BakiyePuan { get; set; }
            public double GetiriPuan { get; set; }
            public double GetiriPuanYuzde { get; set; }
        }

        public List<PerformansRow> PerformansRows { get; private set; } = new();

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
        public double GetiriFiyatNetBuAy { get; set; }
        public double GetiriFiyatNetAy1 { get; set; }
        public double GetiriFiyatNetAy2 { get; set; }
        public double GetiriFiyatNetAy3 { get; set; }
        public double GetiriFiyatNetAy4 { get; set; }
        public double GetiriFiyatNetAy5 { get; set; }
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
        public double GetiriFiyatNetBuHafta { get; set; }
        public double GetiriFiyatNetHafta1 { get; set; }
        public double GetiriFiyatNetHafta2 { get; set; }
        public double GetiriFiyatNetHafta3 { get; set; }
        public double GetiriFiyatNetHafta4 { get; set; }
        public double GetiriFiyatNetHafta5 { get; set; }
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
        public double GetiriFiyatNetBuGun { get; set; }
        public double GetiriFiyatNetGun1 { get; set; }
        public double GetiriFiyatNetGun2 { get; set; }
        public double GetiriFiyatNetGun3 { get; set; }
        public double GetiriFiyatNetGun4 { get; set; }
        public double GetiriFiyatNetGun5 { get; set; }
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
        public double GetiriFiyatNetBuSaat { get; set; }
        public double GetiriFiyatNetSaat1 { get; set; }
        public double GetiriFiyatNetSaat2 { get; set; }
        public double GetiriFiyatNetSaat3 { get; set; }
        public double GetiriFiyatNetSaat4 { get; set; }
        public double GetiriFiyatNetSaat5 { get; set; }
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
        public Dictionary<string, string> OptimizationResultsMap { get; set; }

        #endregion

        #region Constructor

        public Statistics()
        {
            StatisticsMap = new Dictionary<string, string>();
            StatisticsMapMinimal = new Dictionary<string, string>();
            OptimizationResultsMap = new Dictionary<string, string>();
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

        public void CalculatePerformances(double bakiyePuan = 100000, double lotSayisi = 1.0, double varlikAdedCarpani = 1.0)
        {
            var varlikAdedSayisi = lotSayisi * varlikAdedCarpani;

            if (Trader == null || Trader.Data == null || Trader.Data.Count == 0)
                throw new ArgumentException("Trader data cannot be null or empty");
            if (Trader.lists == null)
                throw new InvalidOperationException("Lists are not initialized.");

            PerformansRows.Clear();

            string currentYon = "F";
            int entryIndex = -1;
            string entryYon = "";
            double entryKontrat = 0.0;
            double entryFiyat = 0.0;
            DateTime entryTime = default;

            double balancePuan = bakiyePuan;

            int barCount = Trader.Data.Count;
            for (int i = 0; i < barCount; i++)
            {
                string yon = Trader.lists.YonList[i];
                if (string.IsNullOrWhiteSpace(yon))
                    continue;

                if (yon == "A" || yon == "S")
                {
                    if (currentYon == "A" || currentYon == "S")
                    {
                        double tradePnlPuan = Trader.lists.KarZararPuanList[i] * varlikAdedSayisi;
                        balancePuan += tradePnlPuan;
                        double getiriPuan = balancePuan - bakiyePuan;
                        double getiriPuanYuzde = bakiyePuan != 0.0 ? 100.0 * getiriPuan / bakiyePuan : 0.0;

                        PerformansRows.Add(new PerformansRow
                        {
                            No = PerformansRows.Count + 1,
                            Yon = entryYon,
                            KontratSayisi = varlikAdedSayisi,
                            AcilisTarihSaat = entryTime,
                            AcilisFiyati = entryFiyat,
                            KapanisTarihSaat = Trader.Data[i].DateTime,
                            KapanisFiyati = Trader.lists.SeviyeList[i],
                            KarZararPuan = tradePnlPuan,
                            BakiyePuan = balancePuan,
                            GetiriPuan = getiriPuan,
                            GetiriPuanYuzde = getiriPuanYuzde
                        });
                    }

                    currentYon = yon;
                    entryIndex = i;
                    entryYon = yon;
                    entryKontrat = varlikAdedSayisi;
                    entryFiyat = Trader.lists.SeviyeList[i];
                    entryTime = Trader.Data[i].DateTime;
                    continue;
                }

                if (yon == "F")
                {
                    if (currentYon == "A" || currentYon == "S")
                    {
                        double tradePnlPuan = Trader.lists.KarZararPuanList[i] * varlikAdedSayisi;
                        balancePuan += tradePnlPuan;
                        double getiriPuan = balancePuan - bakiyePuan;
                        double getiriPuanYuzde = bakiyePuan != 0.0 ? 100.0 * getiriPuan / bakiyePuan : 0.0;

                        PerformansRows.Add(new PerformansRow
                        {
                            No = PerformansRows.Count + 1,
                            Yon = entryYon,
                            KontratSayisi = varlikAdedSayisi,
                            AcilisTarihSaat = entryTime,
                            AcilisFiyati = entryFiyat,
                            KapanisTarihSaat = Trader.Data[i].DateTime,
                            KapanisFiyati = Trader.lists.SeviyeList[i],
                            KarZararPuan = tradePnlPuan,
                            BakiyePuan = balancePuan,
                            GetiriPuan = getiriPuan,
                            GetiriPuanYuzde = getiriPuanYuzde
                        });
                    }

                    currentYon = "F";
                    entryIndex = -1;
                    entryYon = "";
                    entryKontrat = 0.0;
                    entryFiyat = 0.0;
                    entryTime = default;
                }
            }
        }

        public Statistics Reset()
        {
            StatisticsMap.Clear();
            StatisticsMapMinimal.Clear();
            OptimizationResultsMap.Clear();
            return this;
        }

        internal SingleTrader TraderForExport => Trader;
        internal void AssignToMapForExport() => AssignToMap();
        internal void AssignToMapMinimalForExport() => AssignToMapMinimal();

        public int Hesapla(int secilenBarNumarasi)
        {
            int result = 0;

            if (Trader == null)
                return result;

            //ReadValues();

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
            DateTime firstDate      = Trader.Data[0].Date;
            TimeSpan elapsed        = DateTime.Now - firstDate;
            double sureDakika       = elapsed.TotalMinutes;
            double sureSaat         = elapsed.TotalHours;
            int sureGun             = elapsed.Days;
            double sureAy           = sureGun / 30.4;
            double sureHafta        = sureGun / 7.0;
                                    
            ToplamGecenSureAy       = sureAy;
            ToplamGecenSureHafta    = sureHafta;
            ToplamGecenSureGun      = sureGun;
            ToplamGecenSureSaat     = (int)sureSaat;
            ToplamGecenSureDakika   = (int)sureDakika;
            OrtAylikIslemSayisi     = ToplamGecenSureAy > 0 ? 1.0 * IslemSayisi / ToplamGecenSureAy : 0;
            OrtHaftalikIslemSayisi  = ToplamGecenSureHafta > 0 ? 1.0 * IslemSayisi / ToplamGecenSureHafta : 0;
            OrtGunlukIslemSayisi    = ToplamGecenSureGun > 0 ? 1.0 * IslemSayisi / ToplamGecenSureGun : 0;
            OrtSaatlikIslemSayisi   = ToplamGecenSureSaat > 0 ? 1.0 * IslemSayisi / ToplamGecenSureSaat : 0;

            // Maximum Drawdown hesaplaması (puan, brut fiyat, net fiyat)
            var maxDDPuan           = CalculateMaxDD(Trader.lists.BakiyePuanList, IlkBakiyePuan);
            var maxDDFiyat          = CalculateMaxDD(Trader.lists.BakiyeFiyatList, IlkBakiyeFiyat);
            var maxDDNet            = CalculateMaxDD(Trader.lists.BakiyeFiyatNetList, IlkBakiyeFiyat);
            
            GetiriMaxDDPuan         = maxDDPuan.maxDDYuzde;
            GetiriMaxDDPuanTarih    = maxDDPuan.maxDDTarih;
            GetiriMaxKayipPuan      = maxDDPuan.maxDD;

            GetiriMaxDD             = maxDDFiyat.maxDDYuzde;
            GetiriMaxDDTarih        = maxDDFiyat.maxDDTarih;
            GetiriMaxKayip          = maxDDFiyat.maxDD;

            GetiriMaxDDNet          = maxDDNet.maxDDYuzde;
            GetiriMaxDDNetTarih     = maxDDNet.maxDDTarih;
            GetiriMaxKayipNet       = maxDDNet.maxDD;
                                    
            MaxKarPuan              = 0.0;
            MaxZararPuan            = 0.0;
            MinBakiyePuan           = IlkBakiyePuan;
            MaxBakiyePuan           = IlkBakiyePuan;

            MaxKarFiyat             = 0.0;
            MaxZararFiyat           = 0.0;
            MinBakiyeFiyat          = IlkBakiyeFiyat;
            MaxBakiyeFiyat          = IlkBakiyeFiyat;
                                    
            MaxKarFiyatNet          = 0.0;
            MaxZararFiyatNet        = 0.0;
            MinBakiyeFiyatNet       = IlkBakiyeFiyat;
            MaxBakiyeFiyatNet       = IlkBakiyeFiyat;

            // Toplam komisyonu al (son bar'daki kümülatif değer)
            KomisyonFiyat = lastBarIndex >= 0 ? Trader.lists.KomisyonFiyatList[lastBarIndex] : 0.0;

            // Find min/max values
            for (int i = 1; i < Trader.Data.Count; i++)
            {
                if (Trader.lists.KarZararPuanList[i] > MaxKarPuan)
                {
                    MaxKarPuan = Trader.lists.KarZararPuanList[i];
                    MaxKarPuanIndex = i;
                }
                if (Trader.lists.KarZararPuanList[i] < MaxZararPuan)
                {
                    MaxZararPuan = Trader.lists.KarZararPuanList[i];
                    MaxZararPuanIndex = i;
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


                if (Trader.lists.KarZararFiyatList[i] > MaxKarFiyat)
                {
                    MaxKarFiyat = Trader.lists.KarZararFiyatList[i];
                    MaxKarFiyatIndex = i;
                }
                if (Trader.lists.KarZararFiyatList[i] < MaxZararFiyat)
                {
                    MaxZararFiyat = Trader.lists.KarZararFiyatList[i];
                    MaxZararFiyatIndex = i;
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

                double komisyonDelta = Trader.lists.KomisyonFiyatList[i] - Trader.lists.KomisyonFiyatList[i - 1];
                double karZararFiyatNet = Trader.lists.KarZararFiyatList[i] - komisyonDelta;

                if (karZararFiyatNet > MaxKarFiyatNet)
                {
                    MaxKarFiyatNet = karZararFiyatNet;
                }
                if (karZararFiyatNet < MaxZararFiyatNet)
                {
                    MaxZararFiyatNet = karZararFiyatNet;
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
            }

            // Calculate performance metrics
            var profitFactors      = CalculateProfitFactors();
            ProfitFactorPuan       = profitFactors.profitFactorPuan;
            ProfitFactor           = profitFactors.profitFactorFiyat;
            ProfitFactorNet        = profitFactors.profitFactorNet;
            ProfitFactorSistem     = 0.0;

            KarliIslemOrani        = IslemSayisi > 0 ? (1.0 * KazandiranIslemSayisi) / (1.0 * IslemSayisi) * 100.0 : 0;
            
            MinBakiyePuanYuzde     = IlkBakiyePuan != 0 ? (MinBakiyePuan - IlkBakiyePuan) * 100.0 / IlkBakiyePuan : 0;
            MaxBakiyePuanYuzde     = IlkBakiyePuan != 0 ? (MaxBakiyePuan - IlkBakiyePuan) * 100.0 / IlkBakiyePuan : 0;
            MinBakiyeFiyatYuzde    = IlkBakiyeFiyat != 0 ? (MinBakiyeFiyat - IlkBakiyeFiyat) * 100.0 / IlkBakiyeFiyat : 0;
            MaxBakiyeFiyatYuzde    = IlkBakiyeFiyat != 0 ? (MaxBakiyeFiyat - IlkBakiyeFiyat) * 100.0 / IlkBakiyeFiyat : 0;
            MinBakiyeFiyatNetYuzde = IlkBakiyeFiyat != 0 ? (MinBakiyeFiyatNet - IlkBakiyeFiyat) * 100.0 / IlkBakiyeFiyat : 0;
            MaxBakiyeFiyatNetYuzde = IlkBakiyeFiyat != 0 ? (MaxBakiyeFiyatNet - IlkBakiyeFiyat) * 100.0 / IlkBakiyeFiyat : 0;

            KomisyonFiyatYuzde     = GetiriFiyatYuzde - GetiriFiyatYuzdeNet;

            //GetiriKzSistemYuzde    = 0.0;  // Silinecek
            //GetiriKzNetSistemYuzde = 0.0;  // Silinecek

            //GetiriIstatistikleriHesapla();

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

        private (double maxDDYuzde, double maxDD, string maxDDTarih) CalculateMaxDD(IList<double> bakiyeList, double ilkBakiye)
        {
            if (Trader?.Data == null || bakiyeList == null)
            {
                return (0.0, 0.0, "");
            }

            int count = Math.Min(Trader.Data.Count, bakiyeList.Count);
            if (count <= 0)
            {
                return (0.0, 0.0, "");
            }

            double maxBakiye = ilkBakiye;
            double maxDD = 0.0;
            double maxDDYuzde = 0.0;
            string maxDDTarih = "";

            for (int i = 0; i < count; i++)
            {
                double mevcutBakiye = bakiyeList[i];

                if (mevcutBakiye > maxBakiye)
                {
                    maxBakiye = mevcutBakiye;
                }

                double drawdown = maxBakiye - mevcutBakiye;
                double drawdownYuzde = maxBakiye > 0 ? (drawdown / maxBakiye) * 100.0 : 0.0;

                if (drawdownYuzde > maxDDYuzde)
                {
                    maxDDYuzde = drawdownYuzde;
                    maxDD = drawdown;
                    maxDDTarih = Trader.Data[i].DateTime.ToString("yyyy.MM.dd HH:mm:ss");
                }
            }

            return (maxDDYuzde, maxDD, maxDDTarih);
        }

        private static double CalculateProfitFactor(double toplamKar, double toplamZarar)
        {
            return Math.Abs(toplamZarar) > 0 ? toplamKar / Math.Abs(toplamZarar) : 0.0;
        }

        private (double profitFactorFiyat, double profitFactorPuan, double profitFactorNet) CalculateProfitFactors()
        {
            double toplamKarFiyat      = 0.0;
            double toplamZararFiyat    = 0.0;
            double toplamKarPuan       = 0.0;
            double toplamZararPuan     = 0.0;
            double toplamKarFiyatNet   = 0.0;
            double toplamZararFiyatNet = 0.0;

            int count = Math.Min(
                Math.Min(Trader.lists.BakiyeFiyatList.Count, Trader.lists.BakiyePuanList.Count),
                Trader.lists.BakiyeFiyatNetList.Count
            );

            for (int i = 1; i < count; i++)
            {
                // Brüt equity delta (komisyon yok): BakiyeFiyat = realized + unrealized
                double deltaFiyat = Trader.lists.BakiyeFiyatList[i] - Trader.lists.BakiyeFiyatList[i - 1];
                if (deltaFiyat >= 0) toplamKarFiyat   += deltaFiyat;
                else                 toplamZararFiyat += deltaFiyat;

                // Brüt puan delta
                double deltaPuan = Trader.lists.BakiyePuanList[i] - Trader.lists.BakiyePuanList[i - 1];
                if (deltaPuan >= 0) toplamKarPuan   += deltaPuan;
                else                toplamZararPuan += deltaPuan;

                // Net equity delta (komisyon dahil): BakiyeFiyatNet = BakiyeFiyat - kümülatif komisyon
                double deltaFiyatNet = Trader.lists.BakiyeFiyatNetList[i] - Trader.lists.BakiyeFiyatNetList[i - 1];
                if (deltaFiyatNet >= 0) toplamKarFiyatNet   += deltaFiyatNet;
                else                    toplamZararFiyatNet += deltaFiyatNet;
            }

            double profitFactorFiyat = CalculateProfitFactor(toplamKarFiyat,    toplamZararFiyat);
            double profitFactorPuan  = CalculateProfitFactor(toplamKarPuan,     toplamZararPuan);
            double profitFactorNet   = CalculateProfitFactor(toplamKarFiyatNet, toplamZararFiyatNet);

            return (profitFactorFiyat, profitFactorPuan, profitFactorNet);
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
                StatisticsMap[key] = string.IsNullOrEmpty(format) ? value.ToString() : string.Format(CultureInfo.InvariantCulture, "{0:" + format + "}", value);
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
            Add("SecilenBarAcilisFiyati", SecilenBarAcilisFiyati, "F4");
            Add("SecilenBarYuksekFiyati", SecilenBarYuksekFiyati, "F4");
            Add("SecilenBarDusukFiyati", SecilenBarDusukFiyati, "F4");
            Add("SecilenBarKapanisFiyati", SecilenBarKapanisFiyati, "F4");

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
            Add("ToplamGecenSureHafta", ToplamGecenSureHafta, "F1");
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
            Add("GetiriFiyatTipi", GetiriFiyatTipi);
            //Add("GetiriKz", GetiriKz, "F4");  // Silinecek
            //Add("GetiriKzNet", GetiriKzNet, "F4");  // Silinecek
            //Add("GetiriKzSistem", GetiriKzSistem, "F4");  // Silinecek
            //Add("GetiriKzSistemYuzde", GetiriKzSistemYuzde, "F2");  // Silinecek
            //Add("GetiriKzNetSistem", GetiriKzNetSistem, "F4");  // Silinecek
            //Add("GetiriKzNetSistemYuzde", GetiriKzNetSistemYuzde, "F2");  // Silinecek

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- Min/Max Balance ---
            Add("MinBakiyeFiyat", MinBakiyeFiyat, "F2");
            Add("MaxBakiyeFiyat", MaxBakiyeFiyat, "F2");
            Add("MinBakiyePuan", MinBakiyePuan, "F2");
            Add("MaxBakiyePuan", MaxBakiyePuan, "F2");
            Add("MinBakiyeFiyatYuzde", MinBakiyeFiyatYuzde, "F2");
            Add("MaxBakiyeFiyatYuzde", MaxBakiyeFiyatYuzde, "F2");
            Add("MinBakiyePuanYuzde", MinBakiyePuanYuzde, "F2");
            Add("MaxBakiyePuanYuzde", MaxBakiyePuanYuzde, "F2");
            Add("MinBakiyeFiyatIndex", MinBakiyeFiyatIndex);
            Add("MaxBakiyeFiyatIndex", MaxBakiyeFiyatIndex);
            Add("MinBakiyePuanIndex", MinBakiyePuanIndex);
            Add("MaxBakiyePuanIndex", MaxBakiyePuanIndex);
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
            Add("MaxKarFiyatNet", MaxKarFiyatNet, "F2");
            Add("MaxZararFiyatNet", MaxZararFiyatNet, "F2");
            Add("MaxKarPuan", MaxKarPuan, "F4");
            Add("MaxZararPuan", MaxZararPuan, "F4");
            Add("MaxKarFiyatIndex", MaxKarFiyatIndex);
            Add("MaxZararFiyatIndex", MaxZararFiyatIndex);
            Add("MaxKarPuanIndex", MaxKarPuanIndex);
            Add("MaxZararPuanIndex", MaxZararPuanIndex);
            Add("KardaBarSayisi", KardaBarSayisi);
            Add("ZarardaBarSayisi", ZarardaBarSayisi);
            Add("KarliIslemOrani", KarliIslemOrani, "F2");

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            // --- Risk Metrics ---
            Add("GetiriMaxDDPuan", GetiriMaxDDPuan, "F2");
            Add("GetiriMaxDDPuanTarih", GetiriMaxDDPuanTarih);
            Add("GetiriMaxKayipPuan", GetiriMaxKayipPuan, "F2");
            Add("GetiriMaxDD", GetiriMaxDD, "F2");
            Add("GetiriMaxDDTarih", GetiriMaxDDTarih);
            Add("GetiriMaxKayip", GetiriMaxKayip, "F2");
            Add("GetiriMaxDDNet", GetiriMaxDDNet, "F2");
            Add("GetiriMaxDDNetTarih", GetiriMaxDDNetTarih);
            Add("GetiriMaxKayipNet", GetiriMaxKayipNet, "F2");
            Add("ProfitFactorPuan", ProfitFactorPuan, "F2");
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
            Add("GetiriPuanBuAy", GetiriPuanBuAy, "F2");
            Add("GetiriPuanAy1", GetiriPuanAy1, "F2");
            Add("GetiriPuanAy2", GetiriPuanAy2, "F2");
            Add("GetiriPuanAy3", GetiriPuanAy3, "F2");
            Add("GetiriPuanAy4", GetiriPuanAy4, "F2");
            Add("GetiriPuanAy5", GetiriPuanAy5, "F2");
            Add("GetiriPuanBuHafta", GetiriPuanBuHafta, "F2");
            Add("GetiriPuanHafta1", GetiriPuanHafta1, "F2");
            Add("GetiriPuanHafta2", GetiriPuanHafta2, "F2");
            Add("GetiriPuanHafta3", GetiriPuanHafta3, "F2");
            Add("GetiriPuanHafta4", GetiriPuanHafta4, "F2");
            Add("GetiriPuanHafta5", GetiriPuanHafta5, "F2");
            Add("GetiriPuanBuGun", GetiriPuanBuGun, "F2");
            Add("GetiriPuanGun1", GetiriPuanGun1, "F2");
            Add("GetiriPuanGun2", GetiriPuanGun2, "F2");
            Add("GetiriPuanGun3", GetiriPuanGun3, "F2");
            Add("GetiriPuanGun4", GetiriPuanGun4, "F2");
            Add("GetiriPuanGun5", GetiriPuanGun5, "F2");
            Add("GetiriPuanBuSaat", GetiriPuanBuSaat, "F2");
            Add("GetiriPuanSaat1", GetiriPuanSaat1, "F2");
            Add("GetiriPuanSaat2", GetiriPuanSaat2, "F2");
            Add("GetiriPuanSaat3", GetiriPuanSaat3, "F2");
            Add("GetiriPuanSaat4", GetiriPuanSaat4, "F2");
            Add("GetiriPuanSaat5", GetiriPuanSaat5, "F2");

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            Add("GetiriFiyatBuAy", GetiriFiyatBuAy, "F2");
            Add("GetiriFiyatAy1", GetiriFiyatAy1, "F2");
            Add("GetiriFiyatAy2", GetiriFiyatAy2, "F2");
            Add("GetiriFiyatAy3", GetiriFiyatAy3, "F2");
            Add("GetiriFiyatAy4", GetiriFiyatAy4, "F2");
            Add("GetiriFiyatAy5", GetiriFiyatAy5, "F2");
            Add("GetiriFiyatBuHafta", GetiriFiyatBuHafta, "F2");
            Add("GetiriFiyatHafta1", GetiriFiyatHafta1, "F2");
            Add("GetiriFiyatHafta2", GetiriFiyatHafta2, "F2");
            Add("GetiriFiyatHafta3", GetiriFiyatHafta3, "F2");
            Add("GetiriFiyatHafta4", GetiriFiyatHafta4, "F2");
            Add("GetiriFiyatHafta5", GetiriFiyatHafta5, "F2");
            Add("GetiriFiyatBuGun", GetiriFiyatBuGun, "F2");
            Add("GetiriFiyatGun1", GetiriFiyatGun1, "F2");
            Add("GetiriFiyatGun2", GetiriFiyatGun2, "F2");
            Add("GetiriFiyatGun3", GetiriFiyatGun3, "F2");
            Add("GetiriFiyatGun4", GetiriFiyatGun4, "F2");
            Add("GetiriFiyatGun5", GetiriFiyatGun5, "F2");
            Add("GetiriFiyatBuSaat", GetiriFiyatBuSaat, "F2");
            Add("GetiriFiyatSaat1", GetiriFiyatSaat1, "F2");
            Add("GetiriFiyatSaat2", GetiriFiyatSaat2, "F2");
            Add("GetiriFiyatSaat3", GetiriFiyatSaat3, "F2");
            Add("GetiriFiyatSaat4", GetiriFiyatSaat4, "F2");
            Add("GetiriFiyatSaat5", GetiriFiyatSaat5, "F2");

            StatisticsMap[SEPARATOR + keyId++.ToString()] = "";

            Add("GetiriFiyatNetBuAy", GetiriFiyatNetBuAy, "F2");
            Add("GetiriFiyatNetAy1", GetiriFiyatNetAy1, "F2");
            Add("GetiriFiyatNetAy2", GetiriFiyatNetAy2, "F2");
            Add("GetiriFiyatNetAy3", GetiriFiyatNetAy3, "F2");
            Add("GetiriFiyatNetAy4", GetiriFiyatNetAy4, "F2");
            Add("GetiriFiyatNetAy5", GetiriFiyatNetAy5, "F2");
            Add("GetiriFiyatNetBuHafta", GetiriFiyatNetBuHafta, "F2");
            Add("GetiriFiyatNetHafta1", GetiriFiyatNetHafta1, "F2");
            Add("GetiriFiyatNetHafta2", GetiriFiyatNetHafta2, "F2");
            Add("GetiriFiyatNetHafta3", GetiriFiyatNetHafta3, "F2");
            Add("GetiriFiyatNetHafta4", GetiriFiyatNetHafta4, "F2");
            Add("GetiriFiyatNetHafta5", GetiriFiyatNetHafta5, "F2");
            Add("GetiriFiyatNetBuGun", GetiriFiyatNetBuGun, "F2");
            Add("GetiriFiyatNetGun1", GetiriFiyatNetGun1, "F2");
            Add("GetiriFiyatNetGun2", GetiriFiyatNetGun2, "F2");
            Add("GetiriFiyatNetGun3", GetiriFiyatNetGun3, "F2");
            Add("GetiriFiyatNetGun4", GetiriFiyatNetGun4, "F2");
            Add("GetiriFiyatNetGun5", GetiriFiyatNetGun5, "F2");
            Add("GetiriFiyatNetBuSaat", GetiriFiyatNetBuSaat, "F2");
            Add("GetiriFiyatNetSaat1", GetiriFiyatNetSaat1, "F2");
            Add("GetiriFiyatNetSaat2", GetiriFiyatNetSaat2, "F2");
            Add("GetiriFiyatNetSaat3", GetiriFiyatNetSaat3, "F2");
            Add("GetiriFiyatNetSaat4", GetiriFiyatNetSaat4, "F2");
            Add("GetiriFiyatNetSaat5", GetiriFiyatNetSaat5, "F2");
        }

        public void SaveToTxt(string filePath)
        {
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).SaveToTxt(filePath);
        }

        public void SaveToCsv(string filePath)
        {
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).SaveToCsv(filePath);
        }

        // Save bar-by-bar lists to TXT file (tabular format with fixed-width columns)
        public void SaveListsToTxt(string filePath)
        {
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).SaveListsToTxt(filePath);
        }

        public void SaveListsToTxtFromConfig(string filePath, string configPath = "inputs/StatisticsExporterConfig.json")
        {
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).SaveListsToTxtFromConfig(filePath, configPath);
        }

        public void SaveListsToCsvFromConfig(string filePath, string configPath = "inputs/StatisticsExporterConfig.json")
        {
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).SaveListsToCsvFromConfig(filePath, configPath);
        }

        // Save bar-by-bar lists to CSV file (semicolon separated) - ALL COLUMNS
        public void SaveListsToCsv(string filePath)
        {
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).SaveListsToCsv(filePath);
        }

        public void SaveToTxtFormatted(string filePath)
        {
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).SaveToTxtFormatted(filePath);
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
            Add("ToplamGecenSureHafta", ToplamGecenSureHafta, "F1");
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
            Add("MinBakiyePuanYuzde", MinBakiyePuanYuzde, "F2");
            Add("MaxBakiyePuanYuzde", MaxBakiyePuanYuzde, "F2");
            Add("MinBakiyeFiyatIndex", MinBakiyeFiyatIndex);
            Add("MaxBakiyeFiyatIndex", MaxBakiyeFiyatIndex);
            Add("MinBakiyeFiyatNet", MinBakiyeFiyatNet, "F2");
            Add("MaxBakiyeFiyatNet", MaxBakiyeFiyatNet, "F2");
            Add("MinBakiyeFiyatNetIndex", MinBakiyeFiyatNetIndex);
            Add("MaxBakiyeFiyatNetIndex", MaxBakiyeFiyatNetIndex);
            Add("MinBakiyeFiyatNetYuzde", MinBakiyeFiyatNetYuzde, "F2");
            Add("MaxBakiyeFiyatNetYuzde", MaxBakiyeFiyatNetYuzde, "F2");
            Add("MaxKarFiyat", MaxKarFiyat, "F2");
            Add("MaxZararFiyat", MaxZararFiyat, "F2");
            Add("MaxKarFiyatNet", MaxKarFiyatNet, "F2");
            Add("MaxZararFiyatNet", MaxZararFiyatNet, "F2");

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
            Add("ProfitFactorPuan", ProfitFactorPuan, "F2");
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
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).SaveToTxtMinimal(filePath);
        }

        public void SaveToCsvMinimal(string filePath)
        {
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).SaveToCsvMinimal(filePath);
        }

        // Save bar-by-bar lists to TXT file (tabular format with fixed-width columns)
        public void SaveListsToTxtMinimal(string filePath)
        {
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).SaveListsToTxtMinimal(filePath);
        }

        // Save bar-by-bar lists to CSV file (semicolon separated) - MINIMAL
        public void SaveListsToCsvMinimal(string filePath)
        {
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).SaveListsToCsvMinimal(filePath);
        }

        public void SaveToTxtMinimalFormatted(string filePath)
        {
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).SaveToTxtMinimalFormatted(filePath);
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
            public double SecilenBarAcilisFiyati;
            public double SecilenBarYuksekFiyati;
            public double SecilenBarDusukFiyati;
            public double SecilenBarKapanisFiyati;

            // --- Time Statistics ---
            public double ToplamGecenSureAy;
            public double ToplamGecenSureHafta;
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
            public int GetiriFiyatTipi;
            //public double GetiriKz;  // Silinecek
            //public double GetiriKzNet;  // Silinecek
            //public double GetiriKzSistem;  // Silinecek
            //public double GetiriKzSistemYuzde;  // Silinecek
            //public double GetiriKzNetSistem;  // Silinecek
            //public double GetiriKzNetSistemYuzde;  // Silinecek

            // --- Min/Max Balance ---
            public double MinBakiyeFiyat;
            public double MaxBakiyeFiyat;
            public double MinBakiyePuan;
            public double MaxBakiyePuan;
            public double MinBakiyeFiyatYuzde;
            public double MaxBakiyeFiyatYuzde;
            public double MinBakiyePuanYuzde;
            public double MaxBakiyePuanYuzde;
            public int MinBakiyeFiyatIndex;
            public int MaxBakiyeFiyatIndex;
            public int MinBakiyePuanIndex;
            public int MaxBakiyePuanIndex;
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
            public double MaxKarFiyatNet;
            public double MaxZararFiyatNet;
            public double MaxKarPuan;
            public double MaxZararPuan;
            public int MaxZararFiyatIndex;
            public int MaxKarFiyatIndex;
            public int MaxZararPuanIndex;
            public int MaxKarPuanIndex;
            public int KardaBarSayisi;
            public int ZarardaBarSayisi;
            public double KarliIslemOrani;

            // --- Risk Metrics ---
            public double GetiriMaxDD;
            public string GetiriMaxDDTarih;
            public double GetiriMaxKayip;
            public double GetiriMaxDDPuan;
            public string GetiriMaxDDPuanTarih;
            public double GetiriMaxKayipPuan;
            public double GetiriMaxDDNet;
            public string GetiriMaxDDNetTarih;
            public double GetiriMaxKayipNet;
            public double ProfitFactor;
            public double ProfitFactorPuan;
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
            public int SonFBarNo;
            public int SonPBarNo;
            public int PrevBarNo;
            public int PrevABarNo;
            public int PrevSBarNo;
            public int PrevFBarNo;
            public int PrevPBarNo;
            public double PrevAFiyat;
            public double PrevSFiyat;
            public double PrevFFiyat;
            public double PrevPFiyat;
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
            public double PrevVarlikAdedSayisiMicro;
            public double KaymaMiktari;
            public bool KaymayiDahilEt;

            public bool MicroLotSizeEnabled;
            public bool PyramidingEnabled;
            public bool MaxPositionSizeEnabled;
            public double MaxPositionSize;
            public double MaxPositionSizeMicro;

            // --- Periodic Returns ---
            public double GetiriPuanBuAy;
            public double GetiriPuanAy1;
            public double GetiriPuanAy2;
            public double GetiriPuanAy3;
            public double GetiriPuanAy4;
            public double GetiriPuanAy5;
            public double GetiriPuanBuHafta;
            public double GetiriPuanHafta1;
            public double GetiriPuanHafta2;
            public double GetiriPuanHafta3;
            public double GetiriPuanHafta4;
            public double GetiriPuanHafta5;
            public double GetiriPuanBuGun;
            public double GetiriPuanGun1;
            public double GetiriPuanGun2;
            public double GetiriPuanGun3;
            public double GetiriPuanGun4;
            public double GetiriPuanGun5;
            public double GetiriPuanBuSaat;
            public double GetiriPuanSaat1;
            public double GetiriPuanSaat2;
            public double GetiriPuanSaat3;
            public double GetiriPuanSaat4;
            public double GetiriPuanSaat5;

            public double GetiriFiyatBuAy;
            public double GetiriFiyatAy1;
            public double GetiriFiyatAy2;
            public double GetiriFiyatAy3;
            public double GetiriFiyatAy4;
            public double GetiriFiyatAy5;
            public double GetiriFiyatBuHafta;
            public double GetiriFiyatHafta1;
            public double GetiriFiyatHafta2;
            public double GetiriFiyatHafta3;
            public double GetiriFiyatHafta4;
            public double GetiriFiyatHafta5;
            public double GetiriFiyatBuGun;
            public double GetiriFiyatGun1;
            public double GetiriFiyatGun2;
            public double GetiriFiyatGun3;
            public double GetiriFiyatGun4;
            public double GetiriFiyatGun5;
            public double GetiriFiyatBuSaat;
            public double GetiriFiyatSaat1;
            public double GetiriFiyatSaat2;
            public double GetiriFiyatSaat3;
            public double GetiriFiyatSaat4;
            public double GetiriFiyatSaat5;

            public double GetiriFiyatNetBuAy;
            public double GetiriFiyatNetAy1;
            public double GetiriFiyatNetAy2;
            public double GetiriFiyatNetAy3;
            public double GetiriFiyatNetAy4;
            public double GetiriFiyatNetAy5;
            public double GetiriFiyatNetBuHafta;
            public double GetiriFiyatNetHafta1;
            public double GetiriFiyatNetHafta2;
            public double GetiriFiyatNetHafta3;
            public double GetiriFiyatNetHafta4;
            public double GetiriFiyatNetHafta5;
            public double GetiriFiyatNetBuGun;
            public double GetiriFiyatNetGun1;
            public double GetiriFiyatNetGun2;
            public double GetiriFiyatNetGun3;
            public double GetiriFiyatNetGun4;
            public double GetiriFiyatNetGun5;
            public double GetiriFiyatNetBuSaat;
            public double GetiriFiyatNetSaat1;
            public double GetiriFiyatNetSaat2;
            public double GetiriFiyatNetSaat3;
            public double GetiriFiyatNetSaat4;
            public double GetiriFiyatNetSaat5;

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
                       "ToplamGecenSureAy;ToplamGecenSureHafta;ToplamGecenSureGun;ToplamGecenSureSaat;ToplamGecenSureDakika;" +
                       "OrtAylikIslemSayisi;OrtHaftalikIslemSayisi;OrtGunlukIslemSayisi;OrtSaatlikIslemSayisi;" +
                       "IlkBakiyeFiyat;IlkBakiyePuan;BakiyeFiyat;BakiyePuan;GetiriFiyat;GetiriPuan;GetiriFiyatYuzde;GetiriPuanYuzde;" +
                       "IlkBakiyeFiyat;BakiyeFiyat;GetiriFiyat;GetiriFiyatYuzde;" +
                       "BakiyeFiyatNet;BakiyePuanNet;GetiriFiyatNet;GetiriPuanNet;GetiriFiyatYuzdeNet;GetiriPuanYuzdeNet;" +
                       "BakiyeFiyatNet;GetiriFiyatNet;GetiriFiyatYuzdeNet;" +
                       //"GetiriKz;GetiriKzNet;GetiriKzSistem;GetiriKzSistemYuzde;GetiriKzNetSistem;GetiriKzNetSistemYuzde;" +  // Silinecek
                       "MinBakiyeFiyat;MaxBakiyeFiyat;MinBakiyePuan;MaxBakiyePuan;MinBakiyeFiyatYuzde;MaxBakiyeFiyatYuzde;MinBakiyePuanYuzde;MaxBakiyePuanYuzde;" +
                       "MinBakiyeFiyatIndex;MaxBakiyeFiyatIndex;MinBakiyeFiyatNet;MaxBakiyeFiyatNet;MinBakiyeFiyatNetIndex;MaxBakiyeFiyatNetIndex;MinBakiyeFiyatNetYuzde;MaxBakiyeFiyatNetYuzde;" +
                       "IslemSayisi;AlisSayisi;SatisSayisi;FlatSayisi;PassSayisi;KarAlSayisi;ZararKesSayisi;" +
                       "KazandiranIslemSayisi;KaybettirenIslemSayisi;NotrIslemSayisi;" +
                       "KazandiranAlisSayisi;KaybettirenAlisSayisi;NotrAlisSayisi;KazandiranSatisSayisi;KaybettirenSatisSayisi;NotrSatisSayisi;" +
                       "AlKomutSayisi;SatKomutSayisi;PasGecKomutSayisi;KarAlKomutSayisi;ZararKesKomutSayisi;FlatOlKomutSayisi;" +
                       "KomisyonIslemSayisi;KomisyonVarlikAdedSayisi;KomisyonVarlikAdedSayisiMicro;KomisyonCarpan;KomisyonFiyat;KomisyonFiyatYuzde;KomisyonuDahilEt;" +
                       "KarZararFiyat;KarZararFiyatYuzde;KarZararPuan;ToplamKarFiyat;ToplamZararFiyat;NetKarFiyat;" +
                       "KarZararFiyat;KarZararFiyatYuzde;ToplamKarFiyat;ToplamZararFiyat;NetKarFiyat;" +
                       "ToplamKarPuan;ToplamZararPuan;NetKarPuan;MaxKarFiyat;MaxZararFiyat;MaxKarFiyatNet;MaxZararFiyatNet;MaxKarPuan;MaxZararPuan;" +
                       "KardaBarSayisi;ZarardaBarSayisi;KarliIslemOrani;" +
                       "GetiriMaxDD;GetiriMaxDDTarih;GetiriMaxKayip;ProfitFactor;ProfitFactorPuan;ProfitFactorNet;ProfitFactorSistem;" +
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
                       $"{ToplamGecenSureAy:F1};{ToplamGecenSureHafta:F1};{ToplamGecenSureGun};{ToplamGecenSureSaat};{ToplamGecenSureDakika};" +
                       $"{OrtAylikIslemSayisi:F2};{OrtHaftalikIslemSayisi:F2};{OrtGunlukIslemSayisi:F2};{OrtSaatlikIslemSayisi:F2};" +
                       $"{IlkBakiyeFiyat:F2};{IlkBakiyePuan:F2};{BakiyeFiyat:F2};{BakiyePuan:F2};{GetiriFiyat:F2};{GetiriPuan:F4};{GetiriFiyatYuzde:F2};{GetiriPuanYuzde:F2};" +
                       $"{IlkBakiyeFiyat:F2};{BakiyeFiyat:F2};{GetiriFiyat:F2};{GetiriFiyatYuzde:F2};" +
                       $"{BakiyeFiyatNet:F2};{BakiyePuanNet:F2};{GetiriFiyatNet:F2};{GetiriPuanNet:F4};{GetiriFiyatYuzdeNet:F2};{GetiriPuanYuzdeNet:F2};" +
                       $"{BakiyeFiyatNet:F2};{GetiriFiyatNet:F2};{GetiriFiyatYuzdeNet:F2};" +
                       //$"{GetiriKz:F4};{GetiriKzNet:F4};{GetiriKzSistem:F4};{GetiriKzSistemYuzde:F2};{GetiriKzNetSistem:F4};{GetiriKzNetSistemYuzde:F2};" +  // Silinecek
                       $"{MinBakiyeFiyat:F2};{MaxBakiyeFiyat:F2};{MinBakiyePuan:F2};{MaxBakiyePuan:F2};{MinBakiyeFiyatYuzde:F2};{MaxBakiyeFiyatYuzde:F2};{MinBakiyePuanYuzde:F2};{MaxBakiyePuanYuzde:F2};" +
                       $"{MinBakiyeFiyatIndex};{MaxBakiyeFiyatIndex};{MinBakiyeFiyatNet:F2};{MaxBakiyeFiyatNet:F2};{MinBakiyeFiyatNetIndex};{MaxBakiyeFiyatNetIndex};{MinBakiyeFiyatNetYuzde:F2};{MaxBakiyeFiyatNetYuzde:F2};" +
                       $"{IslemSayisi};{AlisSayisi};{SatisSayisi};{FlatSayisi};{PassSayisi};{KarAlSayisi};{ZararKesSayisi};" +
                       $"{KazandiranIslemSayisi};{KaybettirenIslemSayisi};{NotrIslemSayisi};" +
                       $"{KazandiranAlisSayisi};{KaybettirenAlisSayisi};{NotrAlisSayisi};{KazandiranSatisSayisi};{KaybettirenSatisSayisi};{NotrSatisSayisi};" +
                       $"{AlKomutSayisi};{SatKomutSayisi};{PasGecKomutSayisi};{KarAlKomutSayisi};{ZararKesKomutSayisi};{FlatOlKomutSayisi};" +
                       $"{KomisyonIslemSayisi};{KomisyonVarlikAdedSayisi:F2};{KomisyonVarlikAdedSayisiMicro:F4};{KomisyonCarpan:F4};{KomisyonFiyat:F2};{KomisyonFiyatYuzde:F4};{KomisyonuDahilEt};" +
                       $"{KarZararFiyat:F2};{KarZararFiyatYuzde:F2};{KarZararPuan:F4};{ToplamKarFiyat:F2};{ToplamZararFiyat:F2};{NetKarFiyat:F2};" +
                       $"{KarZararFiyat:F2};{KarZararFiyatYuzde:F2};{ToplamKarFiyat:F2};{ToplamZararFiyat:F2};{NetKarFiyat:F2};" +
                       $"{ToplamKarPuan:F4};{ToplamZararPuan:F4};{NetKarPuan:F4};{MaxKarFiyat:F2};{MaxZararFiyat:F2};{MaxKarFiyatNet:F2};{MaxZararFiyatNet:F2};{MaxKarPuan:F4};{MaxZararPuan:F4};" +
                       $"{KardaBarSayisi};{ZarardaBarSayisi};{KarliIslemOrani:F2};" +
                       $"{GetiriMaxDD:F2};{GetiriMaxDDTarih};{GetiriMaxKayip:F2};{ProfitFactor:F2};{ProfitFactorPuan:F2};{ProfitFactorNet:F2};{ProfitFactorSistem:F2};" +
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
        /// Get optimization results map with key performance metrics.
        /// Returns OptimizationResultsMap (Dictionary&lt;string, string&gt;) populated with the latest statistics.
        /// Call this after Hesapla() to get optimization metrics.
        /// </summary>
        public Dictionary<string, string> GetOptimizationSummary()
        {
            // Ensure maps are populated (in case Hesapla wasn't called yet)
            if (StatisticsMap.Count == 0)
                AssignToMap();

            OptimizationResultsMap.Clear();

            // StatisticsMap'teki her şeyi OptimizationResultsMap'e kopyala
            foreach (var kvp in StatisticsMap)
                OptimizationResultsMap[kvp.Key] = kvp.Value;

            // Temel Performans Metrikleri
            OptimizationResultsMap["NetProfit"]                = GetiriFiyatNet.ToString("F2", CultureInfo.InvariantCulture);
            OptimizationResultsMap["WinRate"]                  = KarliIslemOrani.ToString("F2", CultureInfo.InvariantCulture);
            OptimizationResultsMap["ProfitFactor"]             = ProfitFactor.ToString("F2", CultureInfo.InvariantCulture);
            OptimizationResultsMap["ProfitFactorNet"]          = ProfitFactorNet.ToString("F2", CultureInfo.InvariantCulture);
            OptimizationResultsMap["MaxDrawdown"]              = GetiriMaxDD.ToString("F2", CultureInfo.InvariantCulture);

            // Bakiye
            OptimizationResultsMap["IlkBakiyeFiyat"]           = IlkBakiyeFiyat.ToString("F2", CultureInfo.InvariantCulture);
            OptimizationResultsMap["BakiyeFiyat"]              = BakiyeFiyat.ToString("F2", CultureInfo.InvariantCulture);
            OptimizationResultsMap["BakiyeFiyatNet"]           = BakiyeFiyatNet.ToString("F2", CultureInfo.InvariantCulture);
            OptimizationResultsMap["GetiriFiyat"]              = GetiriFiyat.ToString("F2", CultureInfo.InvariantCulture);
            OptimizationResultsMap["GetiriFiyatNet"]           = GetiriFiyatNet.ToString("F2", CultureInfo.InvariantCulture);
            OptimizationResultsMap["GetiriFiyatYuzde"]         = GetiriFiyatYuzde.ToString("F2", CultureInfo.InvariantCulture);
            OptimizationResultsMap["GetiriFiyatYuzdeNet"]      = GetiriFiyatYuzdeNet.ToString("F2", CultureInfo.InvariantCulture);
            OptimizationResultsMap["KomisyonFiyat"]            = KomisyonFiyat.ToString("F2", CultureInfo.InvariantCulture);

            // Islem Sayilari
            OptimizationResultsMap["IslemSayisi"]              = IslemSayisi.ToString();
            OptimizationResultsMap["KazandiranIslemSayisi"]    = KazandiranIslemSayisi.ToString();
            OptimizationResultsMap["KaybettirenIslemSayisi"]   = KaybettirenIslemSayisi.ToString();

            // Kar/Zarar
            OptimizationResultsMap["ToplamKarFiyat"]           = ToplamKarFiyat.ToString("F2", CultureInfo.InvariantCulture);
            OptimizationResultsMap["ToplamZararFiyat"]         = ToplamZararFiyat.ToString("F2", CultureInfo.InvariantCulture);
            OptimizationResultsMap["NetKarFiyat"]              = NetKarFiyat.ToString("F2", CultureInfo.InvariantCulture);

            // Bilgi
            OptimizationResultsMap["StrategyName"]             = StrategyName ?? "";

            return OptimizationResultsMap;
        }

        /// <summary>
        /// Optimizasyon döngüsü başında bir kez basılacak sütun başlıkları satırı.
        /// Format: [cur/tot] | param1 | param2 | ... | Getiri% | Trades | Win% | PFNet | MaxDD | ms
        ///sb.Append($" | Getiri%={GetiriFiyatYuzdeNet.ToString("F2", CultureInfo.InvariantCulture)}");
        ///sb.Append($" | Trades={IslemSayisi}");
        ///sb.Append($" | Win%={KarliIslemOrani.ToString("F2", CultureInfo.InvariantCulture)}");
        ///sb.Append($" | PFNet={ProfitFactorNet.ToString("F2", CultureInfo.InvariantCulture)}");
        ///sb.Append($" | MaxDD={GetiriMaxDD.ToString("F2", CultureInfo.InvariantCulture)}");
        ///sb.Append($" | {LastExecutionTimeInMSec}ms"); 
        /// </summary>
        public static StringBuilder GetOptimizationProgressHeader(IEnumerable<string> parameterNames)
        {
            var sb = new StringBuilder();
            var names = parameterNames.ToList();
            sb.Append("#".PadLeft(4));
            foreach (var name in names)
                sb.Append($" | {name.PadLeft(8)}");
            sb.Append($" | {"[cur/tot]".PadLeft(11)}");
            sb.Append($" | {"Net Return %".PadLeft(12)}");
            sb.Append($" | {"Gross Return %".PadLeft(14)}");
            sb.Append($" | {"Score Return %".PadLeft(14)}");
            sb.Append($" | {"Trades".PadLeft(6)}");
            sb.Append($" | {"Win%".PadLeft(7)}");
            sb.Append($" | {"PFNet".PadLeft(7)}");
            sb.Append($" | {"MaxDD".PadLeft(9)}");
            sb.Append($" | {"Comm Count".PadLeft(10)}");
            sb.Append($" | {"Commission".PadLeft(10)}");
            sb.Append($" | {"ExecTime(ms)".PadLeft(12)}"); 
            sb.Append($" | {"(Getiri FiyatNet%,Fiyat%,Puan%)".PadLeft(16)}");
            sb.Append('\n');
            sb.Append(new string('-', sb.Length - 1));
            return sb;
        }

        /// <summary>
        /// Optimizasyon döngüsünde her kombinasyon sonrası ekrana basılacak özet satırı.
        /// Format: [current/total] | param1=val | param2=val | ... | Getiri%=X | Trades=X | Win%=X | PFNet=X | MaxDD=X | Xms
        /// </summary>
        public StringBuilder GetOptimizationProgressLine(int current, int total, Dictionary<string, object> parameters)
        {
            var sb = new StringBuilder();

            sb.Append(current.ToString().PadLeft(4));

            foreach (var kvp in parameters)
                sb.Append($" | {(kvp.Value?.ToString() ?? "").PadLeft(8)}");

            sb.Append($" | {$"[{current}/{total}]".PadLeft(11)}");
            sb.Append($" | {GetiriFiyatYuzdeNet.ToString("F2", CultureInfo.InvariantCulture).PadLeft(12)}");    // "Net Return %"   = 12
            sb.Append($" | {GetiriFiyatYuzde.ToString("F2", CultureInfo.InvariantCulture).PadLeft(14)}");       // "Gross Return %" = 14
            sb.Append($" | {GetiriPuanYuzde.ToString("F2", CultureInfo.InvariantCulture).PadLeft(14)}");        // "Score Return %" = 14
            sb.Append($" | {IslemSayisi.ToString().PadLeft(6)}");
            sb.Append($" | {KarliIslemOrani.ToString("F2", CultureInfo.InvariantCulture).PadLeft(7)}");
            sb.Append($" | {ProfitFactorNet.ToString("F2", CultureInfo.InvariantCulture).PadLeft(7)}");
            sb.Append($" | {GetiriMaxDD.ToString("F2", CultureInfo.InvariantCulture).PadLeft(9)}");
            sb.Append($" | {KomisyonIslemSayisi.ToString().PadLeft(10)}");
            sb.Append($" | {KomisyonFiyat.ToString("F2", CultureInfo.InvariantCulture).PadLeft(10)}");
            sb.Append($" | {LastExecutionTimeInMSec.ToString().PadLeft(12)}");
            sb.Append($" | {"-".ToString().PadLeft(16)}");
            return sb;
        }

        // Legacy struct-based full summary - kept for backward compatibility with old methods.
        // Use GetOptimizationSummary() for the new map-based approach.
        internal OptimizationSummary GetOptimizationSummaryLegacy()
        {
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
                SecilenBarAcilisFiyati = SecilenBarAcilisFiyati,
                SecilenBarYuksekFiyati = SecilenBarYuksekFiyati,
                SecilenBarDusukFiyati = SecilenBarDusukFiyati,
                SecilenBarKapanisFiyati = SecilenBarKapanisFiyati,

                // --- Time Statistics ---
                ToplamGecenSureAy = ToplamGecenSureAy,
                ToplamGecenSureHafta = ToplamGecenSureHafta,
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
                GetiriFiyatTipi = GetiriFiyatTipi,
                //GetiriKz = GetiriKz,  // Silinecek
                //GetiriKzNet = GetiriKzNet,  // Silinecek
                //GetiriKzSistem = GetiriKzSistem,  // Silinecek
                //GetiriKzSistemYuzde = GetiriKzSistemYuzde,  // Silinecek
                //GetiriKzNetSistem = GetiriKzNetSistem,  // Silinecek
                //GetiriKzNetSistemYuzde = GetiriKzNetSistemYuzde,  // Silinecek

                // --- Min/Max Balance ---
                MinBakiyeFiyat = MinBakiyeFiyat,
                MaxBakiyeFiyat = MaxBakiyeFiyat,
                MinBakiyePuan = MinBakiyePuan,
                MaxBakiyePuan = MaxBakiyePuan,
                MinBakiyeFiyatYuzde = MinBakiyeFiyatYuzde,
                MaxBakiyeFiyatYuzde = MaxBakiyeFiyatYuzde,
                MinBakiyePuanYuzde = MinBakiyePuanYuzde,
                MaxBakiyePuanYuzde = MaxBakiyePuanYuzde,
                MinBakiyeFiyatIndex = MinBakiyeFiyatIndex,
                MaxBakiyeFiyatIndex = MaxBakiyeFiyatIndex,
                MinBakiyePuanIndex = MinBakiyePuanIndex,
                MaxBakiyePuanIndex = MaxBakiyePuanIndex,
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
                MaxKarFiyatNet = MaxKarFiyatNet,
                MaxZararFiyatNet = MaxZararFiyatNet,
                MaxKarPuan = MaxKarPuan,
                MaxZararPuan = MaxZararPuan,
                MaxZararFiyatIndex = MaxZararFiyatIndex,
                MaxKarFiyatIndex = MaxKarFiyatIndex,
                MaxZararPuanIndex = MaxZararPuanIndex,
                MaxKarPuanIndex = MaxKarPuanIndex,
                KardaBarSayisi = KardaBarSayisi,
                ZarardaBarSayisi = ZarardaBarSayisi,
                KarliIslemOrani = KarliIslemOrani,

                // --- Risk Metrics ---
                GetiriMaxDD = GetiriMaxDD,
                GetiriMaxDDTarih = GetiriMaxDDTarih ?? "...",
                GetiriMaxKayip = GetiriMaxKayip,
                GetiriMaxDDPuan = GetiriMaxDDPuan,
                GetiriMaxDDPuanTarih = GetiriMaxDDPuanTarih ?? "...",
                GetiriMaxKayipPuan = GetiriMaxKayipPuan,
                GetiriMaxDDNet = GetiriMaxDDNet,
                GetiriMaxDDNetTarih = GetiriMaxDDNetTarih ?? "...",
                GetiriMaxKayipNet = GetiriMaxKayipNet,
                ProfitFactor = ProfitFactor,
                ProfitFactorPuan = ProfitFactorPuan,
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
                SonFBarNo = SonFBarNo,
                SonPBarNo = SonPBarNo,
                PrevBarNo = PrevBarNo,
                PrevABarNo = PrevABarNo,
                PrevSBarNo = PrevSBarNo,
                PrevFBarNo = PrevFBarNo,
                PrevPBarNo = PrevPBarNo,
                PrevAFiyat = PrevAFiyat,
                PrevSFiyat = PrevSFiyat,
                PrevFFiyat = PrevFFiyat,
                PrevPFiyat = PrevPFiyat,
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
                PrevVarlikAdedSayisiMicro = PrevVarlikAdedSayisiMicro,
                KaymaMiktari = KaymaMiktari,
                KaymayiDahilEt = KaymayiDahilEt,

                MicroLotSizeEnabled = MicroLotSizeEnabled,
                PyramidingEnabled = PyramidingEnabled,
                MaxPositionSizeEnabled = MaxPositionSizeEnabled,
                MaxPositionSize = MaxPositionSize,
                MaxPositionSizeMicro = MaxPositionSizeMicro,

                // --- Periodic Returns ---
                GetiriPuanBuAy = GetiriPuanBuAy,
                GetiriPuanAy1 = GetiriPuanAy1,
                GetiriPuanAy2 = GetiriPuanAy2,
                GetiriPuanAy3 = GetiriPuanAy3,
                GetiriPuanAy4 = GetiriPuanAy4,
                GetiriPuanAy5 = GetiriPuanAy5,
                GetiriPuanBuHafta = GetiriPuanBuHafta,
                GetiriPuanHafta1 = GetiriPuanHafta1,
                GetiriPuanHafta2 = GetiriPuanHafta2,
                GetiriPuanHafta3 = GetiriPuanHafta3,
                GetiriPuanHafta4 = GetiriPuanHafta4,
                GetiriPuanHafta5 = GetiriPuanHafta5,
                GetiriPuanBuGun = GetiriPuanBuGun,
                GetiriPuanGun1 = GetiriPuanGun1,
                GetiriPuanGun2 = GetiriPuanGun2,
                GetiriPuanGun3 = GetiriPuanGun3,
                GetiriPuanGun4 = GetiriPuanGun4,
                GetiriPuanGun5 = GetiriPuanGun5,
                GetiriPuanBuSaat = GetiriPuanBuSaat,
                GetiriPuanSaat1 = GetiriPuanSaat1,
                GetiriPuanSaat2 = GetiriPuanSaat2,
                GetiriPuanSaat3 = GetiriPuanSaat3,
                GetiriPuanSaat4 = GetiriPuanSaat4,
                GetiriPuanSaat5 = GetiriPuanSaat5,

                GetiriFiyatBuAy = GetiriFiyatBuAy,
                GetiriFiyatAy1 = GetiriFiyatAy1,
                GetiriFiyatAy2 = GetiriFiyatAy2,
                GetiriFiyatAy3 = GetiriFiyatAy3,
                GetiriFiyatAy4 = GetiriFiyatAy4,
                GetiriFiyatAy5 = GetiriFiyatAy5,
                GetiriFiyatBuHafta = GetiriFiyatBuHafta,
                GetiriFiyatHafta1 = GetiriFiyatHafta1,
                GetiriFiyatHafta2 = GetiriFiyatHafta2,
                GetiriFiyatHafta3 = GetiriFiyatHafta3,
                GetiriFiyatHafta4 = GetiriFiyatHafta4,
                GetiriFiyatHafta5 = GetiriFiyatHafta5,
                GetiriFiyatBuGun = GetiriFiyatBuGun,
                GetiriFiyatGun1 = GetiriFiyatGun1,
                GetiriFiyatGun2 = GetiriFiyatGun2,
                GetiriFiyatGun3 = GetiriFiyatGun3,
                GetiriFiyatGun4 = GetiriFiyatGun4,
                GetiriFiyatGun5 = GetiriFiyatGun5,
                GetiriFiyatBuSaat = GetiriFiyatBuSaat,
                GetiriFiyatSaat1 = GetiriFiyatSaat1,
                GetiriFiyatSaat2 = GetiriFiyatSaat2,
                GetiriFiyatSaat3 = GetiriFiyatSaat3,
                GetiriFiyatSaat4 = GetiriFiyatSaat4,
                GetiriFiyatSaat5 = GetiriFiyatSaat5,

                GetiriFiyatNetBuAy = GetiriFiyatNetBuAy,
                GetiriFiyatNetAy1 = GetiriFiyatNetAy1,
                GetiriFiyatNetAy2 = GetiriFiyatNetAy2,
                GetiriFiyatNetAy3 = GetiriFiyatNetAy3,
                GetiriFiyatNetAy4 = GetiriFiyatNetAy4,
                GetiriFiyatNetAy5 = GetiriFiyatNetAy5,
                GetiriFiyatNetBuHafta = GetiriFiyatNetBuHafta,
                GetiriFiyatNetHafta1 = GetiriFiyatNetHafta1,
                GetiriFiyatNetHafta2 = GetiriFiyatNetHafta2,
                GetiriFiyatNetHafta3 = GetiriFiyatNetHafta3,
                GetiriFiyatNetHafta4 = GetiriFiyatNetHafta4,
                GetiriFiyatNetHafta5 = GetiriFiyatNetHafta5,
                GetiriFiyatNetBuGun = GetiriFiyatNetBuGun,
                GetiriFiyatNetGun1 = GetiriFiyatNetGun1,
                GetiriFiyatNetGun2 = GetiriFiyatNetGun2,
                GetiriFiyatNetGun3 = GetiriFiyatNetGun3,
                GetiriFiyatNetGun4 = GetiriFiyatNetGun4,
                GetiriFiyatNetGun5 = GetiriFiyatNetGun5,
                GetiriFiyatNetBuSaat = GetiriFiyatNetBuSaat,
                GetiriFiyatNetSaat1 = GetiriFiyatNetSaat1,
                GetiriFiyatNetSaat2 = GetiriFiyatNetSaat2,
                GetiriFiyatNetSaat3 = GetiriFiyatNetSaat3,
                GetiriFiyatNetSaat4 = GetiriFiyatNetSaat4,
                GetiriFiyatNetSaat5 = GetiriFiyatNetSaat5
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
            public double MinBakiyePuanYuzde;
            public double MaxBakiyePuanYuzde;
            public double MinBakiyeFiyatNet;
            public double MaxBakiyeFiyatNet;
            public double MinBakiyeFiyatNetYuzde;
            public double MaxBakiyeFiyatNetYuzde;

            // Performance Metrics
            public double ProfitFactor;
            public double ProfitFactorPuan;
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
                       "MinBakiyeFiyat;MaxBakiyeFiyat;MinBakiyeFiyatYuzde;MaxBakiyeFiyatYuzde;MinBakiyePuanYuzde;MaxBakiyePuanYuzde;" +
                       "MinBakiyeFiyatNet;MaxBakiyeFiyatNet;MinBakiyeFiyatNetYuzde;MaxBakiyeFiyatNetYuzde;" +
                       "ProfitFactor;ProfitFactorPuan;ProfitFactorNet;KarliIslemOrani;GetiriMaxDD;GetiriMaxKayip;GetiriMaxDDTarih;" +
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
                       $"{MinBakiyeFiyat:F2};{MaxBakiyeFiyat:F2};{MinBakiyeFiyatYuzde:F2};{MaxBakiyeFiyatYuzde:F2};{MinBakiyePuanYuzde:F2};{MaxBakiyePuanYuzde:F2};" +
                       $"{MinBakiyeFiyatNet:F2};{MaxBakiyeFiyatNet:F2};{MinBakiyeFiyatNetYuzde:F2};{MaxBakiyeFiyatNetYuzde:F2};" +
                       $"{ProfitFactor:F2};{ProfitFactorPuan:F2};{ProfitFactorNet:F2};{KarliIslemOrani:F2};{GetiriMaxDD:F2};{GetiriMaxKayip:F2};{GetiriMaxDDTarih};" +
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
                MinBakiyePuanYuzde = MinBakiyePuanYuzde,
                MaxBakiyePuanYuzde = MaxBakiyePuanYuzde,
                MinBakiyeFiyatNet = MinBakiyeFiyatNet,
                MaxBakiyeFiyatNet = MaxBakiyeFiyatNet,
                MinBakiyeFiyatNetYuzde = MinBakiyeFiyatNetYuzde,
                MaxBakiyeFiyatNetYuzde = MaxBakiyeFiyatNetYuzde,

                // Performance Metrics
                ProfitFactor = ProfitFactor,
                ProfitFactorPuan = ProfitFactorPuan,
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
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).AppendToOptimizationCsv(filePath, writeHeader, useMinimal);
        }

        /// <summary>
        /// Append optimization summary to TXT file (tabular format)
        /// Helper method - alternatively use GetOptimizationSummaryMinimal() and handle file writing in Optimization Manager
        /// </summary>
        public void AppendToOptimizationTxt(string filePath, bool writeHeader = false, bool useMinimal = false)
        {
            new AlgoTrade.Core.Trading.Utils.StatisticsExporter(this).AppendToOptimizationTxt(filePath, writeHeader, useMinimal);
        }

        #endregion


        #endregion
    }
}
