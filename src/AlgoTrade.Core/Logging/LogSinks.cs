using System;

namespace AlgoTrade.Core.Logging
{
    /// <summary>
    /// Log sink hedefleri - Bitwise flags ile kullanılır
    ///
    /// KULLANIM ÖRNEKLERİ:
    ///
    /// // 1. Tüm sink'lere gönder (default)
    /// LogManager.Log("Important event");
    ///
    /// // 2. Sadece Console ve File
    /// LogManager.Log("Debug info", sinks: LogSinks.Console | LogSinks.File);
    ///
    /// // 3. Sadece File (ekrana yazma)
    /// LogManager.Log("Sensitive data", sinks: LogSinks.File);
    ///
    /// // 4. Network hariç her yere
    /// LogManager.Log("Local only", sinks: LogSinks.AllButNetwork);
    /// </summary>
    [Flags]
    public enum LogSinks
    {
        /// <summary>
        /// Hiçbir sink'e gönderme
        /// </summary>
        None = 0,

        // ============================================================
        // BİREYSEL SINK'LER
        // ============================================================

        /// <summary>
        /// Console sink (Console.WriteLine)
        /// </summary>
        Console = 1 << 0,  // 1

        /// <summary>
        /// File sink (Dosyaya log yazma)
        /// </summary>
        File = 1 << 1,  // 2

        /// <summary>
        /// Network sink (UDP ile log gönderme)
        /// </summary>
        Network = 1 << 2,  // 4

        /// <summary>
        /// RichTextBox sink (Renkli, formatlı GUI log)
        /// </summary>
        RichTextBox = 1 << 3,  // 8

        /// <summary>
        /// TextBox sink (Basit text GUI log)
        /// </summary>
        TextBox = 1 << 4,  // 16

        /// <summary>
        /// ListBox sink (Liste halinde GUI log)
        /// </summary>
        ListBox = 1 << 5,  // 32

        /// <summary>
        /// Debug sink (Visual Studio Output penceresi - System.Diagnostics.Debug.WriteLine)
        /// </summary>
        Debug = 1 << 6,  // 64

        // ============================================================
        // KATEGORİK GRUPLAR
        // ============================================================

        /// <summary>
        /// Tüm GUI sink'leri (RichTextBox + TextBox + ListBox)
        /// </summary>
        Gui = RichTextBox | TextBox | ListBox,

        /// <summary>
        /// Persistent storage sink'leri (File)
        /// </summary>
        Storage = File,

        /// <summary>
        /// Remote sink'ler (Network)
        /// </summary>
        Remote = Network,

        // ============================================================
        // KULLANIM KOMBİNASYONLARI
        // ============================================================

        /// <summary>
        /// Sadece lokal sink'ler (Console + Debug + Gui)
        /// </summary>
        Local = Console | Debug | Gui,

        /// <summary>
        /// Network hariç tümü (Console + File + Debug + Gui)
        /// </summary>
        AllButNetwork = Console | File | Debug | Gui,

        /// <summary>
        /// Tüm sink'ler (Default değer)
        /// </summary>
        All = Console | File | Debug | Network | Gui
    }
}
