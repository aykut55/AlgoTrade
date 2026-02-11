namespace AlgoTrade.Core.Trading.Core
{
    /// <summary>
    /// Trading flags - Boolean flags for controlling trading behavior
    /// Converted from Python backtesting system
    /// </summary>
    public class Flags
    {
        #region Update Flags

        public bool BakiyeGuncelle { get; set; }
        public bool KomisyonGuncelle { get; set; }
        public bool DonguSonuIstatistikGuncelle { get; set; }

        #endregion

        #region Calculation Flags

        public bool KomisyonuDahilEt { get; set; }
        public bool KaymayiDahilEt { get; set; }

        #endregion

        #region Enabled Flags

        public bool AnlikKarZararHesaplaEnabled { get; set; }
        public bool KarAlYuzdeHesaplaEnabled { get; set; }
        public bool IzleyenStopYuzdeHesaplaEnabled { get; set; }
        public bool ZararKesYuzdeHesaplaEnabled { get; set; }
        public bool KarAlSeviyeHesaplaEnabled { get; set; }
        public bool ZararKesSeviyeHesaplaEnabled { get; set; }

        #endregion

        #region Execution Flags

        public bool AGerceklesti { get; set; }
        public bool SGerceklesti { get; set; }
        public bool FGerceklesti { get; set; }
        public bool PGerceklesti { get; set; }

        #endregion

        #region Return Calculation Flags

        public bool IdealGetiriHesapla { get; set; }
        public bool IdealGetiriHesaplandi { get; set; }

        #endregion

        #region Constructor

        public Flags()
        {
            // Initialize with default values
            Reset();
        }

        #endregion

        #region Methods

        public Flags Reset()
        {
            BakiyeGuncelle = false;
            KomisyonGuncelle = false;
            DonguSonuIstatistikGuncelle = false;

            KomisyonuDahilEt = true;
            KaymayiDahilEt = false;

            AnlikKarZararHesaplaEnabled = false;
            KarAlYuzdeHesaplaEnabled = false;
            IzleyenStopYuzdeHesaplaEnabled = false;
            ZararKesYuzdeHesaplaEnabled = false;
            KarAlSeviyeHesaplaEnabled = false;
            ZararKesSeviyeHesaplaEnabled = false;

            AGerceklesti = false;
            SGerceklesti = false;
            FGerceklesti = false;
            PGerceklesti = false;

            IdealGetiriHesapla = true;
            IdealGetiriHesaplandi = false;

            return this;
        }

        public Flags Init()
        {
            return this;
        }

        #endregion
    }
}
