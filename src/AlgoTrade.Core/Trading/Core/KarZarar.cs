using System;
using System.Collections.Generic;
using AlgoTrade.Core.DataProvider;
using AlgoTrade.Core.Trading;

namespace AlgoTrade.Core.Trading.Core
{
    /// <summary>
    /// Profit/Loss Calculator - Real-time P&L calculation and TP/SL management
    /// Anlık kar/zarar hesaplama ve Kar Al/Zarar Kes yönetimi
    /// </summary>
    public class KarZarar
    {
        #region Dependencies

        public List<StockData>? Data { get; set; }
        public Flags? Flags { get; set; }
        public Signals? Signals { get; set; }
        public Status? Status { get; set; }
        public Lists? Lists { get; set; }
        public InitialTradeParams? PozisyonBuyuklugu { get; set; }
        public SingleTrader? Trader { get; set; }

        #endregion

        #region TP/SL Properties

        public double TakeProfitPrice { get; set; }
        public double StopLossPrice { get; set; }
        public double TakeProfitPct { get; set; }
        public double StopLossPct { get; set; }
        public bool IsEnabled { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor - Creates KarZarar without dependencies
        /// Use Init() to set dependencies later
        /// </summary>
        public KarZarar()
        {
            Reset();
        }

        /// <summary>
        /// Constructor with SingleTrader dependency (RECOMMENDED)
        /// Creates and initializes KarZarar with all required dependencies from SingleTrader
        /// </summary>
        public KarZarar(SingleTrader trader)
        {
            Reset();
            Init(trader);
        }

        /// <summary>
        /// Constructor with individual dependencies (for advanced use)
        /// </summary>
        public KarZarar(List<StockData> data, Flags flags, Signals signals, Status status, Lists lists, InitialTradeParams initialTradeParams)
        {
            Reset();
            Init(data, flags, signals, status, lists, initialTradeParams);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Reset only KarZarar's own state (TP/SL values)
        /// Does NOT reset dependencies - they are owned by SingleTrader and shared
        /// </summary>
        public KarZarar Reset()
        {
            // Do NOT reset dependencies (Data, Flags, Signals, Status, Lists, InitialTradeParams)
            // They are shared references owned by SingleTrader
            // Resetting them would affect the entire trading system

            // Reset only our own TP/SL state
            IsEnabled = false;
            TakeProfitPrice = 0;
            StopLossPrice = 0;
            TakeProfitPct = 0;
            StopLossPct = 0;

            return this;
        }

        /// <summary>
        /// Initialize with SingleTrader instance (RECOMMENDED)
        /// Gets all required dependencies from SingleTrader
        /// </summary>
        public KarZarar Init(SingleTrader trader)
        {
            if (trader == null)
                throw new ArgumentNullException(nameof(trader));

            Trader = trader;

            Data = trader.Data;
            Flags = trader.flags;
            Signals = trader.signals;
            Status = trader.status;
            Lists = trader.lists;
            PozisyonBuyuklugu = trader.initialTradeParams;
            return this;
        }

        public KarZarar InitOrReuse(SingleTrader trader)
        {
            // Rebind dependencies if needed; no allocations here
            return Init(trader);
        }

        /// <summary>
        /// Initialize with individual dependencies (for advanced use)
        /// </summary>
        public KarZarar Init(List<StockData> data, Flags flags, Signals signals, Status status, Lists lists, InitialTradeParams initialTradeParams)
        {
            Data = data;
            Flags = flags;
            Signals = signals;
            Status = status;
            Lists = lists;
            PozisyonBuyuklugu = initialTradeParams;
            return this;
        }

        /// <summary>
        /// Calculate real-time profit/loss for the current position
        /// Anlık kar/zarar hesaplama
        /// </summary>
        /// <param name="barIndex">Current bar index</param>
        /// <param name="type">Price type: "C" (Close), "O" (Open), "H" (High), "L" (Low)</param>
        /// <returns>Always returns 0 (for compatibility)</returns>
        public double AnlikKarZararHesapla(int barIndex, string type = "C")
        {
            double result = 0.0;

            if (Trader != null)
            {
                if (Trader.initialTradeParams.MicroLotSizeEnabled)
                {
                    result = anlikKarZararHesaplaMicro(barIndex, type);
                }
                else
                {
                    result = anlikKarZararHesapla(barIndex, type);
                }
            }

            return result;
        }
        /// <summary>
        /// Anlık kar/zarar hesaplama - Normal version (BIST, VIOP için)
        /// Integer varlık adedi kullanır
        /// </summary>
        private double anlikKarZararHesapla(int barIndex, string type = "C")
        {
            // Validate dependencies
            if (Data == null || Flags == null || Signals == null || Status == null || Lists == null || PozisyonBuyuklugu == null)
                return 0.0;

            // Validate bar index
            if (barIndex < 0 || barIndex >= Data.Count)
                return 0.0;

            double result = 0.0;
            int i = barIndex;

            // Get current price based on type
            double anlikFiyat = Data[i].Close;
            bool anlikKarZararHesaplaEnabled = Flags.AnlikKarZararHesaplaEnabled;
            string sonYon = Signals.SonYon;
            double sonFiyat = Signals.SonFiyat;
            // Dinamik lot desteği: Son açılan pozisyonun büyüklüğünü kullan
            double varlikAdedSayisi = Signals.SonVarlikAdedSayisi;

            if (!anlikKarZararHesaplaEnabled)
                return result;

            // Select price based on type
            if (type != "C")
            {
                if (type == "O")
                    anlikFiyat = Data[i].Open;
                else if (type == "H")
                    anlikFiyat = Data[i].High;
                else if (type == "L")
                    anlikFiyat = Data[i].Low;
            }

            // Calculate profit/loss based on position direction
            if (sonYon == "A")  // Long position (Al - Buy)
            {
                Status.KarZararPuan = anlikFiyat - sonFiyat;
                Status.KarZararFiyat = Status.KarZararPuan * varlikAdedSayisi;
                Lists.KarZararPuanList[i] = Status.KarZararPuan;
                Lists.KarZararFiyatList[i] = Status.KarZararFiyat;

                if (sonFiyat != 0)
                    Status.KarZararFiyatYuzde = 100.0 * Status.KarZararPuan / sonFiyat;
                else
                    Status.KarZararFiyatYuzde = 0.0;

                Lists.KarZararFiyatYuzdeList[i] = Status.KarZararFiyatYuzde;

                Status.KarZararPuanYuzde = Status.KarZararFiyatYuzde;
                Lists.KarZararPuanYuzdeList[i] = Status.KarZararPuanYuzde;
            }
            else if (sonYon == "S")  // Short position (Sat - Sell)
            {
                Status.KarZararPuan = sonFiyat - anlikFiyat;
                Status.KarZararFiyat = Status.KarZararPuan * varlikAdedSayisi;
                Lists.KarZararPuanList[i] = Status.KarZararPuan;
                Lists.KarZararFiyatList[i] = Status.KarZararFiyat;

                if (sonFiyat != 0)
                    Status.KarZararFiyatYuzde = 100.0 * Status.KarZararPuan / sonFiyat;
                else
                    Status.KarZararFiyatYuzde = 0.0;

                Lists.KarZararFiyatYuzdeList[i] = Status.KarZararFiyatYuzde;

                Status.KarZararPuanYuzde = Status.KarZararFiyatYuzde;
                Lists.KarZararPuanYuzdeList[i] = Status.KarZararPuanYuzde;
            }

            // Update bar count statistics
            if (Status.KarZararPuan > 0)
            {
                Status.KardaBarSayisi += 1;
                Status.ZarardaBarSayisi -= 1;
            }
            else if (Status.KarZararPuan == 0)
            {
                Status.KardaBarSayisi = 0;
                Status.ZarardaBarSayisi = 0;
            }
            else  // KarZararPuan < 0
            {
                Status.KardaBarSayisi -= 1;
                Status.ZarardaBarSayisi += 1;
            }

            return result;
        }
        /// <summary>
        /// Anlık kar/zarar hesaplama - Micro version (FX, Crypto için)
        /// Kesirli varlık adedi (0.01 lot, 0.1 lot, vb.) kullanır
        /// </summary>
        private double anlikKarZararHesaplaMicro(int barIndex, string type = "C")
        {
            // Validate dependencies
            if (Data == null || Flags == null || Signals == null || Status == null || Lists == null || PozisyonBuyuklugu == null)
                return 0.0;

            // Validate bar index
            if (barIndex < 0 || barIndex >= Data.Count)
                return 0.0;

            double result = 0.0;
            int i = barIndex;

            // Get current price based on type
            double anlikFiyat = Data[i].Close;
            bool anlikKarZararHesaplaEnabled = Flags.AnlikKarZararHesaplaEnabled;
            string sonYon = Signals.SonYon;
            double sonFiyat = Signals.SonFiyat;
            // Dinamik lot desteği: Son açılan pozisyonun büyüklüğünü kullan (MICRO)
            double varlikAdedSayisi = Signals.SonVarlikAdedSayisiMicro;

            if (!anlikKarZararHesaplaEnabled)
                return result;

            // Select price based on type
            if (type != "C")
            {
                if (type == "O")
                    anlikFiyat = Data[i].Open;
                else if (type == "H")
                    anlikFiyat = Data[i].High;
                else if (type == "L")
                    anlikFiyat = Data[i].Low;
            }

            // Calculate profit/loss based on position direction
            if (sonYon == "A")  // Long position (Al - Buy)
            {
                Status.KarZararPuan = anlikFiyat - sonFiyat;
                Status.KarZararFiyat = Status.KarZararPuan * varlikAdedSayisi;
                Lists.KarZararPuanList[i] = Status.KarZararPuan;
                Lists.KarZararFiyatList[i] = Status.KarZararFiyat;

                if (sonFiyat != 0)
                    Status.KarZararFiyatYuzde = 100.0 * Status.KarZararPuan / sonFiyat;
                else
                    Status.KarZararFiyatYuzde = 0.0;

                Lists.KarZararFiyatYuzdeList[i] = Status.KarZararFiyatYuzde;

                Status.KarZararPuanYuzde = Status.KarZararFiyatYuzde;
                Lists.KarZararPuanYuzdeList[i] = Status.KarZararPuanYuzde;
            }
            else if (sonYon == "S")  // Short position (Sat - Sell)
            {
                Status.KarZararPuan = sonFiyat - anlikFiyat;
                Status.KarZararFiyat = Status.KarZararPuan * varlikAdedSayisi;
                Lists.KarZararPuanList[i] = Status.KarZararPuan;
                Lists.KarZararFiyatList[i] = Status.KarZararFiyat;

                if (sonFiyat != 0)
                    Status.KarZararFiyatYuzde = 100.0 * Status.KarZararPuan / sonFiyat;
                else
                    Status.KarZararFiyatYuzde = 0.0;

                Lists.KarZararFiyatYuzdeList[i] = Status.KarZararFiyatYuzde;

                Status.KarZararPuanYuzde = Status.KarZararFiyatYuzde;
                Lists.KarZararPuanYuzdeList[i] = Status.KarZararPuanYuzde;
            }

            // Update bar count statistics
            if (Status.KarZararPuan > 0)
            {
                Status.KardaBarSayisi += 1;
                Status.ZarardaBarSayisi -= 1;
            }
            else if (Status.KarZararPuan == 0)
            {
                Status.KardaBarSayisi = 0;
                Status.ZarardaBarSayisi = 0;
            }
            else  // KarZararPuan < 0
            {
                Status.KardaBarSayisi -= 1;
                Status.ZarardaBarSayisi += 1;
            }

            return result;
        }

        #endregion

        #region TP/SL Methods

        /// <summary>
        /// Set take profit and stop loss by percentage
        /// </summary>
        public void SetByPercentage(double takeProfitPct, double stopLossPct)
        {
            TakeProfitPct = takeProfitPct;
            StopLossPct = stopLossPct;
            IsEnabled = true;
        }

        /// <summary>
        /// Set take profit and stop loss by price
        /// </summary>
        public void SetByPrice(double takeProfitPrice, double stopLossPrice)
        {
            TakeProfitPrice = takeProfitPrice;
            StopLossPrice = stopLossPrice;
            IsEnabled = true;
        }

        /// <summary>
        /// Calculate TP/SL prices from entry price
        /// </summary>
        public void CalculatePrices(double entryPrice, bool isLongPosition)
        {
            if (!IsEnabled) return;

            if (isLongPosition)
            {
                if (TakeProfitPct > 0)
                    TakeProfitPrice = entryPrice * (1 + TakeProfitPct / 100.0);
                if (StopLossPct > 0)
                    StopLossPrice = entryPrice * (1 - StopLossPct / 100.0);
            }
            else  // Short position
            {
                if (TakeProfitPct > 0)
                    TakeProfitPrice = entryPrice * (1 - TakeProfitPct / 100.0);
                if (StopLossPct > 0)
                    StopLossPrice = entryPrice * (1 + StopLossPct / 100.0);
            }
        }

        /// <summary>
        /// Check if take profit is hit
        /// </summary>
        public bool CheckKarAl(double currentPrice, bool isLongPosition)
        {
            if (!IsEnabled || TakeProfitPrice == 0) return false;

            if (isLongPosition)
                return currentPrice >= TakeProfitPrice;
            else
                return currentPrice <= TakeProfitPrice;
        }

        /// <summary>
        /// Check if stop loss is hit
        /// </summary>
        public bool CheckZararKes(double currentPrice, bool isLongPosition)
        {
            if (!IsEnabled || StopLossPrice == 0) return false;

            if (isLongPosition)
                return currentPrice <= StopLossPrice;
            else
                return currentPrice >= StopLossPrice;
        }

        #endregion
    }
}
