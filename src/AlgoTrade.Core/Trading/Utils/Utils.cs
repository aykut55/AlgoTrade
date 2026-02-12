using System;

namespace AlgoTrade.Core.Trading.Utils
{
    /// <summary>
    /// Trading utilities - Static helper methods for array comparisons and crossovers
    /// Useful for technical analysis and strategy development
    /// </summary>
    public static class Utils
    {
        #region Crossover Methods

        /// <summary>
        /// Yukarı kesişim - data1, data2'yi yukarı doğru kesiyor mu?
        /// Golden cross gibi durumlar için kullanılır
        /// </summary>
        /// <param name="i">Kontrol edilecek index</param>
        /// <param name="data1">İlk veri dizisi (örn: hızlı MA)</param>
        /// <param name="data2">İkinci veri dizisi (örn: yavaş MA)</param>
        /// <returns>True if data1 crosses above data2 at index i</returns>
        public static bool YukarıKesti(int i, double[] data1, double[] data2)
        {
            // Index kontrolü
            if (i < 1 || i >= data1.Length || i >= data2.Length)
                return false;

            // Null kontrolü
            if (data1 == null || data2 == null)
                return false;

            // Yukarı kesişim: önceki barda data1 <= data2 && şu anki barda data1 > data2
            return data1[i - 1] <= data2[i - 1] && data1[i] > data2[i];
        }

        /// <summary>
        /// Aşağı kesişim - data1, data2'yi aşağı doğru kesiyor mu?
        /// Death cross gibi durumlar için kullanılır
        /// </summary>
        /// <param name="i">Kontrol edilecek index</param>
        /// <param name="data1">İlk veri dizisi (örn: hızlı MA)</param>
        /// <param name="data2">İkinci veri dizisi (örn: yavaş MA)</param>
        /// <returns>True if data1 crosses below data2 at index i</returns>
        public static bool AsagiKesti(int i, double[] data1, double[] data2)
        {
            // Index kontrolü
            if (i < 1 || i >= data1.Length || i >= data2.Length)
                return false;

            // Null kontrolü
            if (data1 == null || data2 == null)
                return false;

            // Aşağı kesişim: önceki barda data1 >= data2 && şu anki barda data1 < data2
            return data1[i - 1] >= data2[i - 1] && data1[i] < data2[i];
        }

        /// <summary>
        /// Yukarı kesişim (değer ile) - data1, value değerini yukarı doğru kesiyor mu?
        /// Örnek: RSI'ın 30'u yukarı kesmesi (oversold'dan çıkış)
        /// </summary>
        /// <param name="i">Kontrol edilecek index</param>
        /// <param name="data1">Veri dizisi (örn: RSI değerleri)</param>
        /// <param name="value">Kesişim seviyesi (örn: 30, 50, 70)</param>
        /// <returns>True if data1 crosses above value at index i</returns>
        public static bool YukarıKesti(int i, double[] data1, double value)
        {
            // Index kontrolü
            if (i < 1 || i >= data1.Length)
                return false;

            // Null kontrolü
            if (data1 == null)
                return false;

            // Yukarı kesişim: önceki barda data1 <= value && şu anki barda data1 > value
            return data1[i - 1] <= value && data1[i] > value;
        }

        /// <summary>
        /// Aşağı kesişim (değer ile) - data1, value değerini aşağı doğru kesiyor mu?
        /// Örnek: RSI'ın 70'i aşağı kesmesi (overbought'tan çıkış)
        /// </summary>
        /// <param name="i">Kontrol edilecek index</param>
        /// <param name="data1">Veri dizisi (örn: RSI değerleri)</param>
        /// <param name="value">Kesişim seviyesi (örn: 30, 50, 70)</param>
        /// <returns>True if data1 crosses below value at index i</returns>
        public static bool AsagiKesti(int i, double[] data1, double value)
        {
            // Index kontrolü
            if (i < 1 || i >= data1.Length)
                return false;

            // Null kontrolü
            if (data1 == null)
                return false;

            // Aşağı kesişim: önceki barda data1 >= value && şu anki barda data1 < value
            return data1[i - 1] >= value && data1[i] < value;
        }

        #endregion

        #region Comparison Methods

        /// <summary>
        /// Büyük mü - data1[i] > data2[i]
        /// </summary>
        /// <param name="i">Kontrol edilecek index</param>
        /// <param name="data1">İlk veri dizisi</param>
        /// <param name="data2">İkinci veri dizisi</param>
        /// <returns>True if data1[i] > data2[i]</returns>
        public static bool Buyuk(int i, double[] data1, double[] data2)
        {
            // Index kontrolü
            if (i < 0 || i >= data1.Length || i >= data2.Length)
                return false;

            // Null kontrolü
            if (data1 == null || data2 == null)
                return false;

            return data1[i] > data2[i];
        }

        /// <summary>
        /// Büyük veya eşit mi - data1[i] >= data2[i]
        /// </summary>
        /// <param name="i">Kontrol edilecek index</param>
        /// <param name="data1">İlk veri dizisi</param>
        /// <param name="data2">İkinci veri dizisi</param>
        /// <returns>True if data1[i] >= data2[i]</returns>
        public static bool BuyukEsit(int i, double[] data1, double[] data2)
        {
            // Index kontrolü
            if (i < 0 || i >= data1.Length || i >= data2.Length)
                return false;

            // Null kontrolü
            if (data1 == null || data2 == null)
                return false;

            return data1[i] >= data2[i];
        }

        /// <summary>
        /// Küçük mü - data1[i] < data2[i]
        /// </summary>
        /// <param name="i">Kontrol edilecek index</param>
        /// <param name="data1">İlk veri dizisi</param>
        /// <param name="data2">İkinci veri dizisi</param>
        /// <returns>True if data1[i] < data2[i]</returns>
        public static bool Kucuk(int i, double[] data1, double[] data2)
        {
            // Index kontrolü
            if (i < 0 || i >= data1.Length || i >= data2.Length)
                return false;

            // Null kontrolü
            if (data1 == null || data2 == null)
                return false;

            return data1[i] < data2[i];
        }

        /// <summary>
        /// Küçük veya eşit mi - data1[i] <= data2[i]
        /// </summary>
        /// <param name="i">Kontrol edilecek index</param>
        /// <param name="data1">İlk veri dizisi</param>
        /// <param name="data2">İkinci veri dizisi</param>
        /// <returns>True if data1[i] <= data2[i]</returns>
        public static bool KucukEsit(int i, double[] data1, double[] data2)
        {
            // Index kontrolü
            if (i < 0 || i >= data1.Length || i >= data2.Length)
                return false;

            // Null kontrolü
            if (data1 == null || data2 == null)
                return false;

            return data1[i] <= data2[i];
        }

        /// <summary>
        /// Eşit mi - data1[i] == data2[i]
        /// </summary>
        /// <param name="i">Kontrol edilecek index</param>
        /// <param name="data1">İlk veri dizisi</param>
        /// <param name="data2">İkinci veri dizisi</param>
        /// <returns>True if data1[i] == data2[i]</returns>
        public static bool Esit(int i, double[] data1, double[] data2)
        {
            // Index kontrolü
            if (i < 0 || i >= data1.Length || i >= data2.Length)
                return false;

            // Null kontrolü
            if (data1 == null || data2 == null)
                return false;

            return Math.Abs(data1[i] - data2[i]) < double.Epsilon; // Floating point karşılaştırma
        }

        #endregion

        #region Value Comparison Methods (with scalar values)

        /// <summary>
        /// Büyük mü (değer ile) - data[i] > value
        /// </summary>
        public static bool Buyuk(int i, double[] data, double value)
        {
            if (i < 0 || i >= data.Length || data == null)
                return false;

            return data[i] > value;
        }

        /// <summary>
        /// Büyük veya eşit mi (değer ile) - data[i] >= value
        /// </summary>
        public static bool BuyukEsit(int i, double[] data, double value)
        {
            if (i < 0 || i >= data.Length || data == null)
                return false;

            return data[i] >= value;
        }

        /// <summary>
        /// Küçük mü (değer ile) - data[i] < value
        /// </summary>
        public static bool Kucuk(int i, double[] data, double value)
        {
            if (i < 0 || i >= data.Length || data == null)
                return false;

            return data[i] < value;
        }

        /// <summary>
        /// Küçük veya eşit mi (değer ile) - data[i] <= value
        /// </summary>
        public static bool KucukEsit(int i, double[] data, double value)
        {
            if (i < 0 || i >= data.Length || data == null)
                return false;

            return data[i] <= value;
        }

        /// <summary>
        /// Eşit mi (değer ile) - data[i] == value
        /// </summary>
        public static bool Esit(int i, double[] data, double value)
        {
            if (i < 0 || i >= data.Length || data == null)
                return false;

            return Math.Abs(data[i] - value) < double.Epsilon;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// İndex geçerli mi kontrol et
        /// </summary>
        public static bool IsValidIndex(int i, double[] data)
        {
            return data != null && i >= 0 && i < data.Length;
        }

        /// <summary>
        /// İki dizi için indeks geçerli mi kontrol et
        /// </summary>
        public static bool IsValidIndex(int i, double[] data1, double[] data2)
        {
            return data1 != null && data2 != null
                && i >= 0 && i < data1.Length && i < data2.Length;
        }

        /// <summary>
        /// Kesişim için indeks geçerli mi kontrol et (en az i=1 olmalı)
        /// </summary>
        public static bool IsValidCrossoverIndex(int i, double[] data1, double[] data2)
        {
            return data1 != null && data2 != null
                && i >= 1 && i < data1.Length && i < data2.Length;
        }

        #endregion
    }
}
