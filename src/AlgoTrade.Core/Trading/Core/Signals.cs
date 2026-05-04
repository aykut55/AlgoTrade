namespace AlgoTrade.Core.Trading.Core
{
    /// <summary>
    /// Trading signals - Tracks current and previous trading signals and states
    /// Converted from Python backtesting system
    /// </summary>
    public class Signals
    {
        #region Signal Flags
        public bool None { get; set; }
        public bool Al { get; set; }
        public bool Sat { get; set; }
        public bool FlatOl { get; set; }
        public bool KarAl { get; set; }
        public bool ZararKes { get; set; }
        public bool PasGec { get; set; }

        #endregion

        #region Signal Info

        public string Sinyal { get; set; } = "";

        #endregion

        #region Direction

        public string SonYon { get; set; } = "F";
        public string PrevYon { get; set; } = "F";

        #endregion

        #region Current Prices (Son)

        public double SonFiyat { get; set; }
        public double SonAFiyat { get; set; }
        public double SonSFiyat { get; set; }
        public double SonFFiyat { get; set; }
        public double SonPFiyat { get; set; }

        #endregion

        #region Previous Prices (Prev)

        public double PrevFiyat { get; set; }
        public double PrevAFiyat { get; set; }
        public double PrevSFiyat { get; set; }
        public double PrevFFiyat { get; set; }
        public double PrevPFiyat { get; set; }

        #endregion

        #region Trailing Stop Tracking (İzleyen Stop Takibi)

        /// <summary>
        /// Long pozisyonlarda izleyen stop için en yüksek fiyat takibi
        /// Pozisyon açıldıktan sonraki en yüksek fiyat
        /// </summary>
        public double IzleyenStopEnYuksekFiyat { get; set; }

        /// <summary>
        /// Short pozisyonlarda izleyen stop için en düşük fiyat takibi
        /// Pozisyon açıldıktan sonraki en düşük fiyat
        /// </summary>
        public double IzleyenStopEnDusukFiyat { get; set; }

        #endregion

        #region Current Bar Numbers (Son)

        public int SonBarNo { get; set; }
        public int SonABarNo { get; set; }
        public int SonSBarNo { get; set; }
        public int SonFBarNo { get; set; }
        public int SonPBarNo { get; set; }

        #endregion

        #region Previous Bar Numbers (Prev)

        public int PrevBarNo { get; set; }
        public int PrevABarNo { get; set; }
        public int PrevSBarNo { get; set; }
        public int PrevFBarNo { get; set; }
        public int PrevPBarNo { get; set; }

        #endregion

        #region Position Size (Pozisyon Büyüklüğü)

        /// <summary>
        /// Son açılan pozisyonun büyüklüğü - Normal lot (integer based)
        /// Her pozisyon açıldığında güncellenir
        /// </summary>
        public double SonVarlikAdedSayisi { get; set; }

        /// <summary>
        /// Son açılan pozisyonun büyüklüğü - Micro lot (fractional based)
        /// Her pozisyon açıldığında güncellenir
        /// </summary>
        public double SonVarlikAdedSayisiMicro { get; set; }

        /// <summary>
        /// Önceki pozisyonun büyüklüğü - Normal lot
        /// Pozisyon değişiminde önceki değer burada saklanır
        /// </summary>
        public double PrevVarlikAdedSayisi { get; set; }

        /// <summary>
        /// Önceki pozisyonun büyüklüğü - Micro lot
        /// Pozisyon değişiminde önceki değer burada saklanır
        /// </summary>
        public double PrevVarlikAdedSayisiMicro { get; set; }

        #endregion

        #region Order Status

        public int EmirKomut { get; set; }
        public int EmirStatus { get; set; }

        #endregion

        #region Signal Status

        public bool AlEnabled { get; set; }
        public bool SatEnabled { get; set; }
        public bool FlatOlEnabled { get; set; }
        public bool PasGecEnabled { get; set; }
        public bool KarAlEnabled { get; set; }
        public bool ZararKesEnabled { get; set; }

        public bool Alindi { get; set; }
        public bool Satildi { get; set; }
        public bool FlatOlundu { get; set; }
        public bool PasGecildi { get; set; }
        public bool KarAlindi { get; set; }
        public bool ZararKesildi { get; set; }

        public bool PozAcilabilir { get; set; }
        public bool PozAcildi { get; set; }

        public bool PozKapatilabilir { get; set; }
        public bool PozKapatildi { get; set; }

        public bool PozAcilabilirAlis { get; set; }
        public bool PozAcilabilirSatis { get; set; }

        public bool PozAcildiAlis { get; set; }
        public bool PozAcildiSatis { get; set; }

        public bool GunSonuPozKapatEnabled { get; set; }

        public bool GunSonuPozKapatildi { get; set; }

        public bool TimeFilteringEnabled { get; set; }

        public bool TradeStartBarIndexEnabled { get; set; }
        public int  TradeStartBarIndex        { get; set; }

        public bool EquityCurveFilteringEnabled { get; set; }           // ajax_bak

        public bool IsTradeEnabled { get; set; }

        public bool IsPozKapatEnabled { get; set; }

        public bool IsTimingFiltersTradeEnabled { get; set; }

        public bool IsEquityCurveTradeEnabled { get; set; }

        #endregion

        #region Constructor

        public Signals()
        {
            // Initialize with default values
            Reset();

            // Bunlar burada olacak ve baska hicbir sekilde resetlenmeyecek
            AlEnabled = false;
            SatEnabled = false;
            FlatOlEnabled = false;
            PasGecEnabled = false;
            KarAlEnabled = false;
            ZararKesEnabled = false;

            Alindi = false;
            Satildi = false;
            FlatOlundu = false;
            PasGecildi = false;
            KarAlindi = false;
            ZararKesildi = false;

            PozAcilabilir = false;
            PozAcildi = false;
            PozKapatilabilir = false;
            PozKapatildi = false;
            PozAcilabilirAlis = false;
            PozAcilabilirSatis = false;
            PozAcildiAlis = false;
            PozAcildiSatis = false;
            GunSonuPozKapatEnabled = false;
            GunSonuPozKapatildi = false;
            TimeFilteringEnabled = false;
            TradeStartBarIndexEnabled = false;
            TradeStartBarIndex = 0;
            EquityCurveFilteringEnabled = false;
            IsTradeEnabled = false;
            IsPozKapatEnabled = false;
            IsTimingFiltersTradeEnabled = false;
            IsEquityCurveTradeEnabled = false;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Reset all signal values to defaults
        /// </summary>
        public Signals Reset()
        {
            Al = false;
            Sat = false;
            FlatOl = false;
            PasGec = false;
            KarAl = false;
            ZararKes = false;

            Sinyal = "";

            SonYon = "F";
            PrevYon = "F";

            SonFiyat = 0.0;
            SonAFiyat = 0.0;
            SonSFiyat = 0.0;
            SonFFiyat = 0.0;
            SonPFiyat = 0.0;

            PrevFiyat = 0.0;
            PrevAFiyat = 0.0;
            PrevSFiyat = 0.0;
            PrevFFiyat = 0.0;
            PrevPFiyat = 0.0;

            IzleyenStopEnYuksekFiyat = 0.0;
            IzleyenStopEnDusukFiyat = 0.0;

            SonBarNo = 0;
            SonABarNo = 0;
            SonSBarNo = 0;
            SonFBarNo = 0;
            SonPBarNo = 0;

            PrevBarNo = 0;
            PrevABarNo = 0;
            PrevSBarNo = 0;
            PrevFBarNo = 0;
            PrevPBarNo = 0;

            EmirKomut = 0;
            EmirStatus = 0;

            return this;
        }

        /// <summary>
        /// Initialize signals - Does nothing currently but returns this for method chaining
        /// </summary>
        public Signals Init()
        {
            return this;
        }

        #endregion
    }
}
