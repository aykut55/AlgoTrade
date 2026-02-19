using System.Collections.Generic;

namespace AlgoTrade.Core.Trading.Core
{
    /// <summary>
    /// Trading lists - Stores historical trading data for analysis and statistics
    /// Converted from Python backtesting system
    /// </summary>
    public class Lists
    {
        #region General

        public int BarCount { get; set; }
        public List<int> BarIndexList { get; set; }

        #endregion

        #region Signal Data

        public List<string> YonList { get; set; }
        public List<double> SeviyeList { get; set; }
        public List<double> SinyalList { get; set; }

        #endregion

        #region Time Filter Data

        public List<int> IsTradeEnabledList { get; set; }
        public List<int> IsPozKapatEnabledList { get; set; }

        #endregion

        #region Profit/Loss Data

        public List<double> KarZararPuanList { get; set; }
        public List<double> KarZararFiyatList { get; set; }
        //public List<double> KarZararPuanYuzdeList { get; set; }   // Silinecek
        public List<double> KarZararFiyatYuzdeList { get; set; }

        #endregion

        #region Trading Actions

        public List<double> KarAlList { get; set; }
        public List<double> ZararKesList { get; set; }
        public List<double> IzleyenStopList { get; set; }

        #endregion

        #region Trade Counts

        public List<int> IslemSayisiList { get; set; }
        public List<int> AlisSayisiList { get; set; }
        public List<int> SatisSayisiList { get; set; }
        public List<int> FlatSayisiList { get; set; }
        public List<int> PassSayisiList { get; set; }

        #endregion

        #region Position Size

        public List<double> KontratSayisiList { get; set; }
        public List<double> VarlikAdedSayisiList { get; set; }
        public List<double> VarlikAdedSayisiMicroList { get; set; }
        public List<double> SonVarlikAdedSayisiList { get; set; }
        public List<double> SonVarlikAdedSayisiMicroList { get; set; }

        #endregion

        #region Commission

        public List<double> KomisyonVarlikAdedSayisiList { get; set; }
        public List<double> KomisyonVarlikAdedSayisiMicroList { get; set; }
        public List<int> KomisyonIslemSayisiList { get; set; }
        public List<double> KomisyonFiyatList { get; set; }

        #endregion

        #region Bar Counts in Profit/Loss

        public List<int> KardaBarSayisiList { get; set; }
        public List<int> ZarardaBarSayisiList { get; set; }

        #endregion

        #region Balance Data

        //public List<double> BakiyePuanList { get; set; }  // Silinecek
        public List<double> BakiyeFiyatList { get; set; }

        #endregion

        #region Return Data (Getiri)

        //public List<double> GetiriPuanList { get; set; }      // Silinecek
        public List<double> GetiriFiyatList { get; set; }
        //public List<double> GetiriPuanYuzdeList { get; set; } // Silinecek
        public List<double> GetiriFiyatYuzdeList { get; set; }

        #endregion

        #region Net Balance Data

        //public List<double> BakiyePuanNetList { get; set; }  // Silinecek
        public List<double> BakiyeFiyatNetList { get; set; }

        #endregion

        #region Net Return Data

        //public List<double> GetiriPuanNetList { get; set; }      // Silinecek
        public List<double> GetiriFiyatNetList { get; set; }
        //public List<double> GetiriPuanYuzdeNetList { get; set; } // Silinecek
        public List<double> GetiriFiyatYuzdeNetList { get; set; }

        #endregion

        #region KZ System Data

        public List<double> GetiriKz { get; set; }
        public List<double> GetiriKzNet { get; set; }
        public List<double> GetiriKzSistem { get; set; }
        public List<double> GetiriKzNetSistem { get; set; }

        #endregion

        #region Order Commands

        public List<double> EmirKomutList { get; set; }
        public List<double> EmirStatusList { get; set; }

        #endregion

        #region Constructor

        public Lists()
        {
            BarCount = 0;

            // Initialize all lists
            BarIndexList = new List<int>();
            YonList = new List<string>();
            SeviyeList = new List<double>();
            SinyalList = new List<double>();

            IsTradeEnabledList = new List<int>();
            IsPozKapatEnabledList = new List<int>();

            KarZararPuanList = new List<double>();
            KarZararFiyatList = new List<double>();
            //KarZararPuanYuzdeList = new List<double>();  // Silinecek
            KarZararFiyatYuzdeList = new List<double>();

            KarAlList = new List<double>();
            ZararKesList = new List<double>();
            IzleyenStopList = new List<double>();

            IslemSayisiList = new List<int>();
            AlisSayisiList = new List<int>();
            SatisSayisiList = new List<int>();
            FlatSayisiList = new List<int>();
            PassSayisiList = new List<int>();

            KontratSayisiList = new List<double>();
            VarlikAdedSayisiList = new List<double>();
            VarlikAdedSayisiMicroList = new List<double>();

            SonVarlikAdedSayisiList = new List<double>();
            SonVarlikAdedSayisiMicroList = new List<double>();

            KomisyonVarlikAdedSayisiList = new List<double>();
            KomisyonVarlikAdedSayisiMicroList = new List<double>();
            KomisyonIslemSayisiList = new List<int>();
            KomisyonFiyatList = new List<double>();

            KardaBarSayisiList = new List<int>();
            ZarardaBarSayisiList = new List<int>();

            //BakiyePuanList = new List<double>();  // Silinecek
            BakiyeFiyatList = new List<double>();

            //GetiriPuanList = new List<double>();      // Silinecek
            GetiriFiyatList = new List<double>();
            //GetiriPuanYuzdeList = new List<double>(); // Silinecek
            GetiriFiyatYuzdeList = new List<double>();

            //BakiyePuanNetList = new List<double>();  // Silinecek
            BakiyeFiyatNetList = new List<double>();

            //GetiriPuanNetList = new List<double>();      // Silinecek
            GetiriFiyatNetList = new List<double>();
            //GetiriPuanYuzdeNetList = new List<double>(); // Silinecek
            GetiriFiyatYuzdeNetList = new List<double>();

            GetiriKz = new List<double>();
            GetiriKzNet = new List<double>();
            GetiriKzSistem = new List<double>();
            GetiriKzNetSistem = new List<double>();

            EmirKomutList = new List<double>();
            EmirStatusList = new List<double>();

            IsTradeEnabledList = new List<int>();
            IsPozKapatEnabledList = new List<int>();
        }

        #endregion

        #region Methods

        public Lists Reset()
        {
            // Clear all existing lists before recreating
            ClearAllLists();

            return this;
        }

        private void ClearAllLists()
        {
            BarCount = 0;

            BarIndexList?.Clear();
            YonList?.Clear();
            SeviyeList?.Clear();
            SinyalList?.Clear();

            KarZararPuanList?.Clear();
            KarZararFiyatList?.Clear();
            //KarZararPuanYuzdeList?.Clear();  // Silinecek
            KarZararFiyatYuzdeList?.Clear();

            KarAlList?.Clear();
            ZararKesList?.Clear();
            IzleyenStopList?.Clear();

            IslemSayisiList?.Clear();
            AlisSayisiList?.Clear();
            SatisSayisiList?.Clear();
            FlatSayisiList?.Clear();
            PassSayisiList?.Clear();

            KontratSayisiList?.Clear();
            VarlikAdedSayisiList?.Clear();
            VarlikAdedSayisiMicroList?.Clear();
            SonVarlikAdedSayisiList?.Clear();
            SonVarlikAdedSayisiMicroList?.Clear();

            KomisyonVarlikAdedSayisiList?.Clear();
            KomisyonVarlikAdedSayisiMicroList?.Clear();
            KomisyonIslemSayisiList?.Clear();
            KomisyonFiyatList?.Clear();

            KardaBarSayisiList?.Clear();
            ZarardaBarSayisiList?.Clear();

            //BakiyePuanList?.Clear();  // Silinecek
            BakiyeFiyatList?.Clear();

            //GetiriPuanList?.Clear();      // Silinecek
            GetiriFiyatList?.Clear();
            //GetiriPuanYuzdeList?.Clear(); // Silinecek
            GetiriFiyatYuzdeList?.Clear();

            //BakiyePuanNetList?.Clear();  // Silinecek
            BakiyeFiyatNetList?.Clear();

            //GetiriPuanNetList?.Clear();      // Silinecek
            GetiriFiyatNetList?.Clear();
            //GetiriPuanYuzdeNetList?.Clear(); // Silinecek
            GetiriFiyatYuzdeNetList?.Clear();

            GetiriKz?.Clear();
            GetiriKzNet?.Clear();
            GetiriKzSistem?.Clear();
            GetiriKzNetSistem?.Clear();

            EmirKomutList?.Clear();
            EmirStatusList?.Clear();

            IsTradeEnabledList?.Clear();
            IsPozKapatEnabledList?.Clear();
        }

        public Lists Init(int barCount)
        {
            CreateLists(barCount);
            return this;
        }

        // Initialize lists with given size, but reuse existing buffers if size matches.
        // When sizes match, only zero/fill defaults without reallocating.
        public Lists InitOrReuse(int barCount)
        {
            if (barCount <= 0)
            {
                ClearAllLists();
                return this;
            }

            if (BarCount == barCount &&
                BarIndexList != null && BarIndexList.Count == BarCount)
            {
                ZeroFillValues();
                return this;
            }

            CreateLists(barCount);
            return this;
        }

        private void ZeroFillValues()
        {
            int n = BarCount;
            // Guard in case lists are not created yet
            if (n <= 0) return;

            for (int i = 0; i < n; i++)
            {
                if (BarIndexList != null && BarIndexList.Count == n) BarIndexList[i] = 0;
                if (YonList != null && YonList.Count == n) YonList[i] = "";
                if (SeviyeList != null && SeviyeList.Count == n) SeviyeList[i] = 0.0;
                if (SinyalList != null && SinyalList.Count == n) SinyalList[i] = 0.0;

                if (KarZararPuanList != null && KarZararPuanList.Count == n) KarZararPuanList[i] = 0.0;
                if (KarZararFiyatList != null && KarZararFiyatList.Count == n) KarZararFiyatList[i] = 0.0;
                //if (KarZararPuanYuzdeList != null && KarZararPuanYuzdeList.Count == n) KarZararPuanYuzdeList[i] = 0.0;  // Silinecek
                if (KarZararFiyatYuzdeList != null && KarZararFiyatYuzdeList.Count == n) KarZararFiyatYuzdeList[i] = 0.0;

                if (KarAlList != null && KarAlList.Count == n) KarAlList[i] = 0.0;
                if (ZararKesList != null && ZararKesList.Count == n) ZararKesList[i] = 0.0;
                if (IzleyenStopList != null && IzleyenStopList.Count == n) IzleyenStopList[i] = 0.0;

                if (IslemSayisiList != null && IslemSayisiList.Count == n) IslemSayisiList[i] = 0;
                if (AlisSayisiList != null && AlisSayisiList.Count == n) AlisSayisiList[i] = 0;
                if (SatisSayisiList != null && SatisSayisiList.Count == n) SatisSayisiList[i] = 0;
                if (FlatSayisiList != null && FlatSayisiList.Count == n) FlatSayisiList[i] = 0;
                if (PassSayisiList != null && PassSayisiList.Count == n) PassSayisiList[i] = 0;

                if (KontratSayisiList != null && KontratSayisiList.Count == n) KontratSayisiList[i] = 0.0;
                if (VarlikAdedSayisiList != null && VarlikAdedSayisiList.Count == n) VarlikAdedSayisiList[i] = 0.0;
                if (VarlikAdedSayisiMicroList != null && VarlikAdedSayisiMicroList.Count == n) VarlikAdedSayisiMicroList[i] = 0.0;
                if (SonVarlikAdedSayisiList != null && SonVarlikAdedSayisiList.Count == n) SonVarlikAdedSayisiList[i] = 0.0;
                if (SonVarlikAdedSayisiMicroList != null && SonVarlikAdedSayisiMicroList.Count == n) SonVarlikAdedSayisiMicroList[i] = 0.0;

                if (KomisyonVarlikAdedSayisiList != null && KomisyonVarlikAdedSayisiList.Count == n) KomisyonVarlikAdedSayisiList[i] = 0.0;
                if (KomisyonVarlikAdedSayisiMicroList != null && KomisyonVarlikAdedSayisiMicroList.Count == n) KomisyonVarlikAdedSayisiMicroList[i] = 0.0;
                if (KomisyonIslemSayisiList != null && KomisyonIslemSayisiList.Count == n) KomisyonIslemSayisiList[i] = 0;
                if (KomisyonFiyatList != null && KomisyonFiyatList.Count == n) KomisyonFiyatList[i] = 0.0;

                if (KardaBarSayisiList != null && KardaBarSayisiList.Count == n) KardaBarSayisiList[i] = 0;
                if (ZarardaBarSayisiList != null && ZarardaBarSayisiList.Count == n) ZarardaBarSayisiList[i] = 0;

                //if (BakiyePuanList != null && BakiyePuanList.Count == n) BakiyePuanList[i] = 0.0;  // Silinecek
                if (BakiyeFiyatList != null && BakiyeFiyatList.Count == n) BakiyeFiyatList[i] = 0.0;

                //if (GetiriPuanList != null && GetiriPuanList.Count == n) GetiriPuanList[i] = 0.0;          // Silinecek
                if (GetiriFiyatList != null && GetiriFiyatList.Count == n) GetiriFiyatList[i] = 0.0;
                //if (GetiriPuanYuzdeList != null && GetiriPuanYuzdeList.Count == n) GetiriPuanYuzdeList[i] = 0.0;  // Silinecek
                if (GetiriFiyatYuzdeList != null && GetiriFiyatYuzdeList.Count == n) GetiriFiyatYuzdeList[i] = 0.0;

                //if (BakiyePuanNetList != null && BakiyePuanNetList.Count == n) BakiyePuanNetList[i] = 0.0;  // Silinecek
                if (BakiyeFiyatNetList != null && BakiyeFiyatNetList.Count == n) BakiyeFiyatNetList[i] = 0.0;

                //if (GetiriPuanNetList != null && GetiriPuanNetList.Count == n) GetiriPuanNetList[i] = 0.0;          // Silinecek
                if (GetiriFiyatNetList != null && GetiriFiyatNetList.Count == n) GetiriFiyatNetList[i] = 0.0;
                //if (GetiriPuanYuzdeNetList != null && GetiriPuanYuzdeNetList.Count == n) GetiriPuanYuzdeNetList[i] = 0.0; // Silinecek
                if (GetiriFiyatYuzdeNetList != null && GetiriFiyatYuzdeNetList.Count == n) GetiriFiyatYuzdeNetList[i] = 0.0;

                if (GetiriKz != null && GetiriKz.Count == n) GetiriKz[i] = 0.0;
                if (GetiriKzNet != null && GetiriKzNet.Count == n) GetiriKzNet[i] = 0.0;
                if (GetiriKzSistem != null && GetiriKzSistem.Count == n) GetiriKzSistem[i] = 0.0;
                if (GetiriKzNetSistem != null && GetiriKzNetSistem.Count == n) GetiriKzNetSistem[i] = 0.0;

                if (EmirKomutList != null && EmirKomutList.Count == n) EmirKomutList[i] = 0.0;
                if (EmirStatusList != null && EmirStatusList.Count == n) EmirStatusList[i] = 0.0;

                if (IsTradeEnabledList != null && IsTradeEnabledList.Count == n) IsTradeEnabledList[i] = 0;
                if (IsPozKapatEnabledList != null && IsPozKapatEnabledList.Count == n) IsPozKapatEnabledList[i] = 0;
            }
        }

        public void CreateLists(int barCount)
        {
            BarCount = barCount;

            if (barCount > 0)
            {
                // Create lists with default values (Python: [0] * BarCount, [""] * BarCount)
                BarIndexList = new List<int>(new int[barCount]);
                YonList = new List<string>(new string[barCount]);
                SeviyeList = new List<double>(new double[barCount]);
                SinyalList = new List<double>(new double[barCount]);

                KarZararPuanList = new List<double>(new double[barCount]);
                KarZararFiyatList = new List<double>(new double[barCount]);
                //KarZararPuanYuzdeList = new List<double>(new double[barCount]);  // Silinecek
                KarZararFiyatYuzdeList = new List<double>(new double[barCount]);

                KarAlList = new List<double>(new double[barCount]);
                ZararKesList = new List<double>(new double[barCount]);
                IzleyenStopList = new List<double>(new double[barCount]);

                IslemSayisiList = new List<int>(new int[barCount]);
                AlisSayisiList = new List<int>(new int[barCount]);
                SatisSayisiList = new List<int>(new int[barCount]);
                FlatSayisiList = new List<int>(new int[barCount]);
                PassSayisiList = new List<int>(new int[barCount]);

                KontratSayisiList = new List<double>(new double[barCount]);
                VarlikAdedSayisiList = new List<double>(new double[barCount]);
                VarlikAdedSayisiMicroList = new List<double>(new double[barCount]);
                SonVarlikAdedSayisiList = new List<double>(new double[barCount]);
                SonVarlikAdedSayisiMicroList = new List<double>(new double[barCount]);

                KomisyonVarlikAdedSayisiList = new List<double>(new double[barCount]);
                KomisyonVarlikAdedSayisiMicroList = new List<double>(new double[barCount]);
                KomisyonIslemSayisiList = new List<int>(new int[barCount]);
                KomisyonFiyatList = new List<double>(new double[barCount]);

                KardaBarSayisiList = new List<int>(new int[barCount]);
                ZarardaBarSayisiList = new List<int>(new int[barCount]);

                //BakiyePuanList = new List<double>(new double[barCount]);  // Silinecek
                BakiyeFiyatList = new List<double>(new double[barCount]);

                //GetiriPuanList = new List<double>(new double[barCount]);      // Silinecek
                GetiriFiyatList = new List<double>(new double[barCount]);
                //GetiriPuanYuzdeList = new List<double>(new double[barCount]); // Silinecek
                GetiriFiyatYuzdeList = new List<double>(new double[barCount]);

                //BakiyePuanNetList = new List<double>(new double[barCount]);  // Silinecek
                BakiyeFiyatNetList = new List<double>(new double[barCount]);

                //GetiriPuanNetList = new List<double>(new double[barCount]);      // Silinecek
                GetiriFiyatNetList = new List<double>(new double[barCount]);
                //GetiriPuanYuzdeNetList = new List<double>(new double[barCount]); // Silinecek
                GetiriFiyatYuzdeNetList = new List<double>(new double[barCount]);

                GetiriKz = new List<double>(new double[barCount]);
                GetiriKzNet = new List<double>(new double[barCount]);
                GetiriKzSistem = new List<double>(new double[barCount]);
                GetiriKzNetSistem = new List<double>(new double[barCount]);

                EmirKomutList = new List<double>(new double[barCount]);
                EmirStatusList = new List<double>(new double[barCount]);

                IsTradeEnabledList = new List<int>(new int[barCount]);
                IsPozKapatEnabledList = new List<int>(new int[barCount]);

                // Initialize string arrays with empty strings (null -> "")
                for (int i = 0; i < barCount; i++)
                {
                    YonList[i] = "";
                    SinyalList[i] = 0.0;
                }
            }
        }

        #endregion
    }
}
