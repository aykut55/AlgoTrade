using AlgoTrade.Core;

namespace AlgoTrade.Core.Trading.Core
{
    public enum MarketTypes
    {
        BistEndex = 0,
        BistHisse = 1,
        BistParite = 2,
        BistMetal = 3,

        ViopEndex = 4,
        ViopHisse = 5,
        ViopParite = 6,
        ViopMetal = 7,

        FxEndex = 8,
        FxHisse = 9,
        FxParite = 10,
        FxMetal = 11,

        FxCrypto = 12,
        Crypto = 13
    }

    /// <summary>
    /// Position Size - Manages position sizing for different market types
    /// Pozisyon büyüklüğü yönetimi (Kontrat, Lot, Hisse)
    /// </summary>
    public class InitialTradeParams
    {
        #region Properties

        /// <summary>
        /// Kontrat sayısı (Futures/Options için)
        /// Kesirli değer olabilir (örn: 0.01 kontrat)
        /// </summary>
        public double KontratSayisi { get; set; }

        /// <summary>
        /// Lot sayısı (Forex için)
        /// Kesirli değer olabilir (örn: 0.01 lot = micro lot)
        /// </summary>
        public double LotSayisi { get; set; }

        /// <summary>
        /// Hisse sayısı (Stocks için)
        /// Kesirli değer olabilir (örn: 0.5 hisse)
        /// </summary>
        public double HisseSayisi { get; set; }

        /// <summary>
        /// Varlık adedi çarpanı (örn: 1 kontrat = 10 adet varlık)
        /// </summary>
        public double VarlikAdedCarpani { get; set; }

        /// <summary>
        /// Toplam varlık adedi sayısı
        /// </summary>
        public double VarlikAdedSayisi { get; set; }

        /// <summary>
        /// Komisyon hesaplanırken kullanılacak varlık adedi
        /// </summary>
        public double KomisyonVarlikAdedSayisi { get; set; }

        /// <summary>
        /// Market tipi
        /// </summary>
        public MarketTypes MarketType { get; set; }

        /// <summary>
        /// Micro lot desteği (0.01 lot gibi kesirli lotlar için)
        /// </summary>
        public bool MicroLotSizeEnabled { get; set; }

        /// <summary>
        /// Micro lot ile varlık adedi (kesirli değer)
        /// </summary>
        public double VarlikAdedSayisiMicro { get; set; }

        /// <summary>
        /// Micro lot ile komisyon varlık adedi (kesirli değer)
        /// </summary>
        public double KomisyonVarlikAdedSayisiMicro { get; set; }

        /// <summary>
        /// Komisyon çarpanı
        /// </summary>
        public double KomisyonCarpan { get; set; }

        /// <summary>
        /// Komisyonu hesaplamaya dahil et
        /// </summary>
        public bool KomisyonuDahilEt { get; set; }

        /// <summary>
        /// Kayma miktarı (slippage)
        /// </summary>
        public double KaymaMiktari { get; set; }

        /// <summary>
        /// Kaymayı hesaplamaya dahil et
        /// </summary>
        public bool KaymayiDahilEt { get; set; }

        /// <summary>
        /// İlk bakiye (fiyat cinsinden)
        /// </summary>
        public double IlkBakiyeFiyat { get; set; }

        /// <summary>
        /// İlk bakiye (puan cinsinden)
        /// </summary>
        public double IlkBakiyePuan { get; set; }

        /// <summary>
        /// Son bakiye (fiyat cinsinden)
        /// </summary>
        public double SonBakiyeFiyat { get; set; }

        /// <summary>
        /// Net bakiye (fiyat cinsinden)
        /// </summary>
        public double NetBakiyeFiyat { get; set; }

        /// <summary>
        /// Getiri fiyat tipi ("TL", "$", "€", vb.)
        /// </summary>
        public string GetiriFiyatTipi { get; set; }

        #endregion

        #region Pyramiding (Pozisyon Artırma)

        /// <summary>
        /// Pyramiding (pozisyon artırma) desteği
        /// true: A→A veya S→S durumlarında mevcut pozisyona ekleme yapılır
        /// false: A→A ve S→S sinyalleri göz ardı edilir (varsayılan)
        /// </summary>
        public bool PyramidingEnabled { get; set; }

        /// <summary>
        /// Maksimum pozisyon büyüklüğü kontrolü
        /// true: MaxPositionSize limiti uygulanır
        /// false: Sınırsız pozisyon artırma (varsayılan)
        /// </summary>
        public bool MaxPositionSizeEnabled { get; set; }

        /// <summary>
        /// Maksimum pozisyon büyüklüğü (normal lot için)
        /// Pyramiding ile toplam pozisyon bu değeri geçemez
        /// </summary>
        public double MaxPositionSize { get; set; }

        /// <summary>
        /// Maksimum pozisyon büyüklüğü (micro lot için)
        /// Pyramiding ile toplam pozisyon bu değeri geçemez
        /// </summary>
        public double MaxPositionSizeMicro { get; set; }

        #endregion

        #region Constructor

        public InitialTradeParams()
        {
            Reset();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Reset all values to defaults
        /// </summary>
        public InitialTradeParams Reset()
        {
            KontratSayisi = 0;
            LotSayisi = 0;
            HisseSayisi = 0;
            VarlikAdedCarpani = 1;
            VarlikAdedSayisi = 0;
            KomisyonVarlikAdedSayisi = 0;
            MarketType = MarketTypes.ViopEndex;
            MicroLotSizeEnabled = false;
            VarlikAdedSayisiMicro = 0.0;
            KomisyonVarlikAdedSayisiMicro = 0.0;
            KomisyonCarpan = 0.0;
            KomisyonuDahilEt = false;
            KaymaMiktari = 0.0;
            KaymayiDahilEt = false;
            IlkBakiyeFiyat = 0.0;
            IlkBakiyePuan = 0.0;
            SonBakiyeFiyat = 0.0;
            NetBakiyeFiyat = 0.0;
            GetiriFiyatTipi = "TL";

            // Pyramiding defaults
            PyramidingEnabled = false;
            MaxPositionSizeEnabled = false;
            MaxPositionSize = 0.0;
            MaxPositionSizeMicro = 0.0;

            return this;
        }

        /// <summary>
        /// Initialize - Returns this for method chaining
        /// </summary>
        public InitialTradeParams Init()
        {
            return this;
        }

        public InitialTradeParams InitOrReuse()
        {
            // No internal buffers; reuse
            return this;
        }

        /// <summary>
        /// Set market type and automatically configure position size parameters
        /// </summary>
        /// <param name="marketType">Market type to configure</param>
        /// <param name="kontratSayisi">Contract count (for futures/options) - can be fractional (0.01, 0.1, etc.)</param>
        /// <param name="lotSayisi">Lot count (for forex) - can be fractional (0.01 = micro lot)</param>
        /// <param name="hisseSayisi">Stock count (for stocks) - can be fractional</param>
        public InitialTradeParams SetMarketType(MarketTypes marketType, double kontratSayisi = 1.0, double lotSayisi = 1.0, double hisseSayisi = 1000.0)
        {
            MarketType = marketType;

            switch (marketType)
            {
                case MarketTypes.ViopEndex:
                    KontratSayisi = kontratSayisi;
                    VarlikAdedCarpani = 10;
                    VarlikAdedSayisi = KontratSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = KontratSayisi;
                    KomisyonCarpan = 0.0;
                    break;

                case MarketTypes.ViopHisse:
                    KontratSayisi = kontratSayisi;
                    VarlikAdedCarpani = 100;
                    VarlikAdedSayisi = KontratSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = KontratSayisi;
                    KomisyonCarpan = 0.0;
                    break;

                case MarketTypes.ViopParite:
                    KontratSayisi = kontratSayisi;
                    VarlikAdedCarpani = 1000;
                    VarlikAdedSayisi = KontratSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = KontratSayisi;
                    KomisyonCarpan = 0.0;
                    break;

                case MarketTypes.ViopMetal:
                    KontratSayisi = kontratSayisi;
                    VarlikAdedCarpani = 100;
                    VarlikAdedSayisi = KontratSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = KontratSayisi;
                    KomisyonCarpan = 0.0;
                    break;

                case MarketTypes.BistHisse:
                    HisseSayisi = hisseSayisi;
                    VarlikAdedCarpani = 1;
                    VarlikAdedSayisi = HisseSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = HisseSayisi;
                    KomisyonCarpan = 0.0;
                    break;

                case MarketTypes.BistEndex:
                    HisseSayisi = hisseSayisi;
                    VarlikAdedCarpani = 1;
                    VarlikAdedSayisi = HisseSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = HisseSayisi;
                    KomisyonCarpan = 0.0;
                    break;

                case MarketTypes.BistParite:
                    HisseSayisi = hisseSayisi;
                    VarlikAdedCarpani = 1;
                    VarlikAdedSayisi = HisseSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = HisseSayisi;
                    KomisyonCarpan = 0.0;
                    break;

                case MarketTypes.BistMetal:
                    HisseSayisi = hisseSayisi;
                    VarlikAdedCarpani = 1;
                    VarlikAdedSayisi = HisseSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = HisseSayisi;
                    KomisyonCarpan = 0.0;
                    break;

                case MarketTypes.FxEndex:
                case MarketTypes.FxHisse:
                case MarketTypes.FxParite:
                case MarketTypes.FxMetal:
                case MarketTypes.FxCrypto:
                    LotSayisi = lotSayisi;
                    VarlikAdedCarpani = 100000; // Standard lot = 100,000 units
                    VarlikAdedSayisi = LotSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = LotSayisi;
                    KomisyonCarpan = 0.0;
                    MicroLotSizeEnabled = true; // Forex supports micro lots
                    break;

                case MarketTypes.Crypto:
                    LotSayisi = lotSayisi;
                    VarlikAdedCarpani = 1;
                    VarlikAdedSayisi = LotSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = LotSayisi;
                    KomisyonCarpan = 0.0;
                    MicroLotSizeEnabled = true; // Crypto supports fractional amounts
                    break;

                default:
                    // Default configuration
                    KontratSayisi = kontratSayisi;
                    VarlikAdedCarpani = 1;
                    VarlikAdedSayisi = KontratSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = KontratSayisi;
                    KomisyonCarpan = 0.0;
                    break;
            }

            return this;
        }

        /// <summary>
        /// Calculate VarlikAdedSayisi based on current settings
        /// </summary>
        public InitialTradeParams CalculateVarlikAdedSayisi()
        {
            if (MicroLotSizeEnabled)
            {
                VarlikAdedSayisiMicro = LotSayisi * VarlikAdedCarpani;
                KomisyonVarlikAdedSayisiMicro = LotSayisi;          // eskisi KomisyonVarlikAdedSayisiMicro = LotSayisi * VarlikAdedCarpani;
            }
            else
            {
                if (MarketType >= MarketTypes.ViopEndex && MarketType <= MarketTypes.ViopMetal)
                {
                    VarlikAdedSayisi = KontratSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = KontratSayisi;
                }
                else if (MarketType >= MarketTypes.BistEndex && MarketType <= MarketTypes.BistMetal)
                {
                    VarlikAdedSayisi = HisseSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = HisseSayisi;
                }
                else // Forex, Crypto
                {
                    VarlikAdedSayisi = LotSayisi * VarlikAdedCarpani;
                    KomisyonVarlikAdedSayisi = LotSayisi;       // eskisi KomisyonVarlikAdedSayisi = LotSayisi * VarlikAdedCarpani;
                }
            }
            return this;
        }

        /// <summary>
        /// Set komisyon parametreleri
        /// </summary>
        public InitialTradeParams SetKomisyonParams(double komisyonCarpan = 20.0)
        {
            KomisyonCarpan = komisyonCarpan;
            KomisyonuDahilEt = komisyonCarpan != 0.0;
            return this;
        }

        /// <summary>
        /// Set kayma (slippage) parametreleri
        /// </summary>
        public InitialTradeParams SetKaymaParams(double kaymaMiktari = 0.0)
        {
            KaymaMiktari = kaymaMiktari;
            KaymayiDahilEt = kaymaMiktari != 0.0;
            return this;
        }

        /// <summary>
        /// Set bakiye parametreleri
        /// </summary>
        public InitialTradeParams SetBakiyeParams(double ilkBakiye = 100000.0, double ilkBakiyePuan = 100000.0)
        {
            IlkBakiyeFiyat = ilkBakiye;
            IlkBakiyePuan = ilkBakiyePuan;
            return this;
        }

        // ====================================================================
        // MarketTypes enum sırasına göre SetKontratParams methodları
        // ====================================================================

        /// <summary>
        /// BistEndex (0) için kontrat parametrelerini ayarla
        /// </summary>
        public InitialTradeParams SetKontratParamsBistEndex(double hisseSayisi = 1000.0, double varlikAdedCarpani = 1.0)
        {
            MarketType = MarketTypes.BistEndex;
            HisseSayisi = hisseSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = HisseSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = HisseSayisi;
            GetiriFiyatTipi = "TL";
            MicroLotSizeEnabled = false;
            return this;
        }

        /// <summary>
        /// BistHisse (1) için kontrat parametrelerini ayarla
        /// </summary>
        public InitialTradeParams SetKontratParamsBistHisse(double hisseSayisi = 1000.0, double varlikAdedCarpani = 1.0)
        {
            MarketType = MarketTypes.BistHisse;
            HisseSayisi = hisseSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = HisseSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = HisseSayisi;
            GetiriFiyatTipi = "TL";
            MicroLotSizeEnabled = false;
            return this;
        }

        /// <summary>
        /// BistParite (2) için kontrat parametrelerini ayarla
        /// </summary>
        public InitialTradeParams SetKontratParamsBistParite(double hisseSayisi = 1000.0, double varlikAdedCarpani = 1.0)
        {
            MarketType = MarketTypes.BistParite;
            HisseSayisi = hisseSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = HisseSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = HisseSayisi;
            GetiriFiyatTipi = "TL";
            MicroLotSizeEnabled = false;
            return this;
        }

        /// <summary>
        /// BistMetal (3) için kontrat parametrelerini ayarla
        /// </summary>
        public InitialTradeParams SetKontratParamsBistMetal(double hisseSayisi = 1000.0, double varlikAdedCarpani = 1.0)
        {
            MarketType = MarketTypes.BistMetal;
            HisseSayisi = hisseSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = HisseSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = HisseSayisi;
            GetiriFiyatTipi = "TL";
            MicroLotSizeEnabled = false;
            return this;
        }

        /// <summary>
        /// ViopEndex (4) için kontrat parametrelerini ayarla
        /// </summary>
        public InitialTradeParams SetKontratParamsViopEndex(double kontratSayisi = 1.0, double varlikAdedCarpani = 10.0)
        {
            MarketType = MarketTypes.ViopEndex;
            KontratSayisi = kontratSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = KontratSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = KontratSayisi;
            GetiriFiyatTipi = "TL";
            MicroLotSizeEnabled = false;
            return this;
        }

        /// <summary>
        /// ViopHisse (5) için kontrat parametrelerini ayarla
        /// </summary>
        public InitialTradeParams SetKontratParamsViopHisse(double kontratSayisi = 1.0, double varlikAdedCarpani = 100.0)
        {
            MarketType = MarketTypes.ViopHisse;
            KontratSayisi = kontratSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = KontratSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = KontratSayisi;
            GetiriFiyatTipi = "TL";
            MicroLotSizeEnabled = false;
            return this;
        }

        /// <summary>
        /// ViopParite (6) için kontrat parametrelerini ayarla
        /// </summary>
        public InitialTradeParams SetKontratParamsViopParite(double kontratSayisi = 1.0, double varlikAdedCarpani = 1000.0)
        {
            MarketType = MarketTypes.ViopParite;
            KontratSayisi = kontratSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = KontratSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = KontratSayisi;
            GetiriFiyatTipi = "TL";
            MicroLotSizeEnabled = false;
            return this;
        }

        /// <summary>
        /// ViopMetal (7) için kontrat parametrelerini ayarla
        /// </summary>
        public InitialTradeParams SetKontratParamsViopMetal(double kontratSayisi = 1.0, double varlikAdedCarpani = 1.0)
        {
            MarketType = MarketTypes.ViopMetal;
            KontratSayisi = kontratSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = KontratSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = KontratSayisi;
            GetiriFiyatTipi = "TL";
            MicroLotSizeEnabled = false;
            return this;
        }

        /// <summary>
        /// FxEndex (8) için kontrat parametrelerini ayarla
        /// Micro lot desteği aktif (0.01, 0.1 gibi kesirli lotlar)
        /// </summary>
        public InitialTradeParams SetKontratParamsFxEndex(double lotSayisi = 1.0, double varlikAdedCarpani = 100000.0)
        {
            MarketType = MarketTypes.FxEndex;
            LotSayisi = lotSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = LotSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = LotSayisi;
            GetiriFiyatTipi = "$";
            MicroLotSizeEnabled = true;
            return this;
        }

        /// <summary>
        /// FxHisse (9) için kontrat parametrelerini ayarla
        /// Micro lot desteği aktif (0.01, 0.1 gibi kesirli lotlar)
        /// </summary>
        public InitialTradeParams SetKontratParamsFxHisse(double lotSayisi = 1.0, double varlikAdedCarpani = 100000.0)
        {
            MarketType = MarketTypes.FxHisse;
            LotSayisi = lotSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = LotSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = LotSayisi;
            GetiriFiyatTipi = "$";
            MicroLotSizeEnabled = true;
            return this;
        }

        /// <summary>
        /// FxParite (10) için kontrat parametrelerini ayarla
        /// Micro lot desteği aktif (0.01, 0.1 gibi kesirli lotlar)
        /// </summary>
        public InitialTradeParams SetKontratParamsFxParite(double lotSayisi = 1.0, double varlikAdedCarpani = 100000.0)
        {
            MarketType = MarketTypes.FxParite;
            LotSayisi = lotSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = LotSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = LotSayisi;
            GetiriFiyatTipi = "$";
            MicroLotSizeEnabled = true;
            return this;
        }

        /// <summary>
        /// FxMetal (11) için kontrat parametrelerini ayarla
        /// Micro lot desteği aktif (0.01, 0.1 gibi kesirli lotlar)
        /// </summary>
        public InitialTradeParams SetKontratParamsFxMetal(double lotSayisi = 1.0, double varlikAdedCarpani = 100000.0)
        {
            MarketType = MarketTypes.FxMetal;
            LotSayisi = lotSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = LotSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = LotSayisi;
            GetiriFiyatTipi = "$";
            MicroLotSizeEnabled = true;
            return this;
        }

        /// <summary>
        /// FxCrypto (12) için kontrat parametrelerini ayarla
        /// Micro lot desteği aktif (0.01, 0.1 gibi kesirli lotlar)
        /// </summary>
        public InitialTradeParams SetKontratParamsFxCrypto(double lotSayisi = 1.0, double varlikAdedCarpani = 100.0)
        {
            MarketType = MarketTypes.FxCrypto;
            LotSayisi = lotSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = LotSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = LotSayisi;
            GetiriFiyatTipi = "$";
            MicroLotSizeEnabled = true;
            return this;
        }

        /// <summary>
        /// Crypto (13) için kontrat parametrelerini ayarla
        /// Micro lot desteği aktif (0.01, 0.1 gibi kesirli lotlar)
        /// </summary>
        public InitialTradeParams SetKontratParamsCrypto(double lotSayisi = 1.0, double varlikAdedCarpani = 1.0)
        {
            MarketType = MarketTypes.Crypto;
            LotSayisi = lotSayisi;
            VarlikAdedCarpani = varlikAdedCarpani;
            VarlikAdedSayisi = LotSayisi * VarlikAdedCarpani;
            KomisyonVarlikAdedSayisi = LotSayisi;
            GetiriFiyatTipi = "$";
            MicroLotSizeEnabled = true;
            return this;
        }

        /// <summary>
        /// Tüm alanları kaynak nesneden kopyalar.
        /// initialTradeParams'ın private set'i olduğu durumlarda direkt atama yerine kullanılır.
        /// </summary>
        public InitialTradeParams ApplyFrom(InitialTradeParams src)
        {
            KontratSayisi                 = src.KontratSayisi;
            LotSayisi                     = src.LotSayisi;
            HisseSayisi                   = src.HisseSayisi;
            VarlikAdedCarpani             = src.VarlikAdedCarpani;
            VarlikAdedSayisi              = src.VarlikAdedSayisi;
            KomisyonVarlikAdedSayisi      = src.KomisyonVarlikAdedSayisi;
            MarketType                    = src.MarketType;
            MicroLotSizeEnabled           = src.MicroLotSizeEnabled;
            VarlikAdedSayisiMicro         = src.VarlikAdedSayisiMicro;
            KomisyonVarlikAdedSayisiMicro = src.KomisyonVarlikAdedSayisiMicro;
            KomisyonCarpan                = src.KomisyonCarpan;
            KomisyonuDahilEt              = src.KomisyonuDahilEt;
            KaymaMiktari                  = src.KaymaMiktari;
            KaymayiDahilEt                = src.KaymayiDahilEt;
            IlkBakiyeFiyat                = src.IlkBakiyeFiyat;
            IlkBakiyePuan                 = src.IlkBakiyePuan;
            SonBakiyeFiyat                = src.SonBakiyeFiyat;
            NetBakiyeFiyat                = src.NetBakiyeFiyat;
            GetiriFiyatTipi               = src.GetiriFiyatTipi;
            PyramidingEnabled             = src.PyramidingEnabled;
            MaxPositionSizeEnabled        = src.MaxPositionSizeEnabled;
            MaxPositionSize               = src.MaxPositionSize;
            MaxPositionSizeMicro          = src.MaxPositionSizeMicro;
            return this;
        }

        #endregion
    }
}
