namespace AlgoTrade.Core.Trading.Core
{
    /// <summary>
    /// Trading status - Tracks trading statistics and performance metrics
    /// Converted from Python backtesting system
    /// </summary>
    public class Status
    {
        #region Trade Counts

        public int IslemSayisi { get; set; }
        public int KazandiranIslemSayisi { get; set; }
        public int KaybettirenIslemSayisi { get; set; }
        public int NotrIslemSayisi { get; set; }

        public int KazandiranAlisSayisi { get; set; }
        public int KaybettirenAlisSayisi { get; set; }
        public int NotrAlisSayisi { get; set; }

        public int KazandiranSatisSayisi { get; set; }
        public int KaybettirenSatisSayisi { get; set; }
        public int NotrSatisSayisi { get; set; }

        public int AlisSayisi { get; set; }
        public int SatisSayisi { get; set; }
        public int FlatSayisi { get; set; }
        public int PassSayisi { get; set; }

        public int KarAlSayisi { get; set; }
        public int ZararKesSayisi { get; set; }

        #endregion

        #region Command Counts

        public int AlKomutSayisi { get; set; }
        public int SatKomutSayisi { get; set; }
        public int PasGecKomutSayisi { get; set; }
        public int KarAlKomutSayisi { get; set; }
        public int ZararKesKomutSayisi { get; set; }
        public int FlatOlKomutSayisi { get; set; }

        #endregion

        #region Bar Counts

        public int KardaBarSayisi { get; set; }
        public int ZarardaBarSayisi { get; set; }

        #endregion

        #region Profit/Loss

        //public double KarZararPuan { get; set; }       // Silinecek
        public double KarZararFiyat { get; set; }
        //public double KarZararPuanYuzde { get; set; }  // Silinecek
        public double KarZararFiyatYuzde { get; set; }

        #endregion

        #region Commission

        public int KomisyonIslemSayisi { get; set; }
        public double KomisyonVarlikAdedSayisi { get; set; }  // double for micro lot support
        public double KomisyonVarlikAdedSayisiMicro { get; set; } // double for micro lot support
        public double KomisyonCarpan { get; set; }
        public double KomisyonFiyat { get; set; }

        #endregion

        #region Slippage

        public double KaymaMiktari { get; set; }

        #endregion

        #region Position Size

        public double VarlikAdedSayisi { get; set; }     // double for micro lot support (0.01 lot, etc.)
        public double VarlikAdedSayisiMicro { get; set; } // double for micro lot support
        public double VarlikAdedCarpani { get; set; }    // double for fractional multipliers
        public double KontratSayisi { get; set; }        // double for fractional contracts
        public double HisseSayisi { get; set; }          // double for fractional shares

        #endregion

        #region Balance

        public double IlkBakiyeFiyat { get; set; }
        //public double IlkBakiyePuan { get; set; }  // Silinecek
        public double BakiyeFiyat { get; set; }
        //public double BakiyePuan { get; set; }     // Silinecek

        #endregion

        #region Returns (Getiri)

        public double GetiriFiyat { get; set; }
        //public double GetiriPuan { get; set; }       // Silinecek
        public double GetiriFiyatYuzde { get; set; }
        //public double GetiriPuanYuzde { get; set; }  // Silinecek

        #endregion

        #region Net Values

        public double BakiyeFiyatNet { get; set; }
        //public double BakiyePuanNet { get; set; }         // Silinecek
        public double GetiriFiyatNet { get; set; }
        //public double GetiriPuanNet { get; set; }         // Silinecek
        public double GetiriFiyatYuzdeNet { get; set; }
        //public double GetiriPuanYuzdeNet { get; set; }    // Silinecek

        #endregion

        #region KZ System

        public double GetiriKz { get; set; }
        public double GetiriKzNet { get; set; }
        public double GetiriKzSistem { get; set; }
        public double GetiriKzNetSistem { get; set; }

        #endregion

        #region Return Type

        public string GetiriFiyatTipi { get; set; } = "TL";

        public bool MicroLotSizeEnabled { get; set; }

        #endregion

        #region Summary

        //public double NetKarPuan { get; set; }        // Silinecek
        //public double ToplamKarPuan { get; set; }     // Silinecek
        //public double ToplamZararPuan { get; set; }   // Silinecek

        public double NetKarFiyat { get; set; }
        public double ToplamKarFiyat { get; set; }
        public double ToplamZararFiyat { get; set; }

        #endregion

        #region Constructor

        public Status()
        {
            // Initialize with default values
            Reset();
        }

        #endregion

        #region Methods

        public Status Reset()
        {
            IslemSayisi = 0;
            KazandiranIslemSayisi = 0;
            KaybettirenIslemSayisi = 0;
            NotrIslemSayisi = 0;

            KazandiranAlisSayisi = 0;
            KaybettirenAlisSayisi = 0;
            NotrAlisSayisi = 0;

            KazandiranSatisSayisi = 0;
            KaybettirenSatisSayisi = 0;
            NotrSatisSayisi = 0;

            AlisSayisi = 0;
            SatisSayisi = 0;
            FlatSayisi = 0;
            PassSayisi = 0;

            KarAlSayisi = 0;
            ZararKesSayisi = 0;

            AlKomutSayisi = 0;
            SatKomutSayisi = 0;
            PasGecKomutSayisi = 0;
            KarAlKomutSayisi = 0;
            ZararKesKomutSayisi = 0;
            FlatOlKomutSayisi = 0;

            KardaBarSayisi = 0;
            ZarardaBarSayisi = 0;

            //KarZararPuan = 0.0;    // Silinecek
            KarZararFiyat = 0.0;
            //KarZararPuanYuzde = 0.0;  // Silinecek
            KarZararFiyatYuzde = 0.0;

            KomisyonIslemSayisi = 0;
            KomisyonVarlikAdedSayisi = 0;
            KomisyonVarlikAdedSayisiMicro = 0;
            KomisyonCarpan = 0.0;
            KomisyonFiyat = 0.0;

            KaymaMiktari = 0.0;

            VarlikAdedSayisi = 0;
            VarlikAdedSayisiMicro = 0;
            VarlikAdedCarpani = 0;
            KontratSayisi = 0;
            HisseSayisi = 0;

            IlkBakiyeFiyat = 0.0;
            //IlkBakiyePuan = 0.0;  // Silinecek
            BakiyeFiyat = 0.0;
            //BakiyePuan = 0.0;     // Silinecek

            GetiriFiyat = 0.0;
            //GetiriPuan = 0.0;       // Silinecek
            GetiriFiyatYuzde = 0.0;
            //GetiriPuanYuzde = 0.0;  // Silinecek

            BakiyeFiyatNet = 0.0;
            //BakiyePuanNet = 0.0;       // Silinecek
            GetiriFiyatNet = 0.0;
            //GetiriPuanNet = 0.0;       // Silinecek
            GetiriFiyatYuzdeNet = 0.0;
            //GetiriPuanYuzdeNet = 0.0;  // Silinecek

            GetiriKz = 0.0;
            GetiriKzNet = 0.0;
            GetiriKzSistem = 0.0;
            GetiriKzNetSistem = 0.0;

            GetiriFiyatTipi = "TL";

            //NetKarPuan = 0.0;      // Silinecek
            //ToplamKarPuan = 0.0;   // Silinecek
            //ToplamZararPuan = 0.0; // Silinecek

            NetKarFiyat = 0.0;
            ToplamKarFiyat = 0.0;
            ToplamZararFiyat = 0.0;

            return this;
        }

        public Status Init()
        {
            return this;
        }

        #endregion
    }
}
