using System;
using System.IO;
using System.Text;

namespace AlgoTrade.Core.Trading.Utils
{
    /// <summary>
    /// Dosya işlemleri için yardımcı sınıf
    /// Paylaşımlı dosya erişimi sağlar (FileShare.ReadWrite)
    /// </summary>
    public static class FileUtils
    {
        /// <summary>
        /// Dosyaya paylaşımlı modda yazma yapar.
        /// Dosya başka bir program (metin editörü) tarafından açıkken bile yazma işlemi yapılabilir.
        /// </summary>
        /// <param name="filePath">Dosya yolu</param>
        /// <param name="content">Yazılacak içerik</param>
        /// <param name="append">true ise sona ekler, false ise dosyayı yeniden oluşturur</param>
        /// <param name="encoding">Karakter kodlaması (varsayılan: UTF8)</param>
        public static void WriteTextShared(string filePath, string content, bool append = false, Encoding encoding = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            // Klasör yoksa oluştur
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (encoding == null)
                encoding = Encoding.UTF8;

            FileMode mode = append ? FileMode.Append : FileMode.Create;

            using (FileStream fs = new FileStream(
                filePath,
                mode,
                FileAccess.Write,
                FileShare.ReadWrite))  // Diğer programların okuyabilmesini sağlar
            using (StreamWriter sw = new StreamWriter(fs, encoding))
            {
                if (append)
                    sw.WriteLine(content);
                else
                    sw.Write(content);

                sw.Flush();  // Buffer'ı hemen temizle
            }
        }

        /// <summary>
        /// Dosyaya satır ekler (sona ekler)
        /// </summary>
        /// <param name="filePath">Dosya yolu</param>
        /// <param name="line">Eklenecek satır</param>
        public static void AppendLineShared(string filePath, string line)
        {
            WriteTextShared(filePath, line, append: true);
        }

        /// <summary>
        /// Dosyaya birden fazla satır ekler
        /// </summary>
        /// <param name="filePath">Dosya yolu</param>
        /// <param name="lines">Eklenecek satırlar</param>
        public static void AppendLinesShared(string filePath, string[] lines)
        {
            if (lines == null || lines.Length == 0)
                return;

            WriteTextShared(filePath, string.Join(Environment.NewLine, lines) + Environment.NewLine, append: true);
        }

        /// <summary>
        /// Dosyadan paylaşımlı modda okuma yapar
        /// </summary>
        /// <param name="filePath">Dosya yolu</param>
        /// <param name="encoding">Karakter kodlaması (varsayılan: UTF8)</param>
        /// <returns>Dosya içeriği</returns>
        public static string ReadTextShared(string filePath, Encoding encoding = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Dosya bulunamadı", filePath);

            if (encoding == null)
                encoding = Encoding.UTF8;

            using (FileStream fs = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite))  // Başka programlar yazabilir
            using (StreamReader sr = new StreamReader(fs, encoding))
            {
                return sr.ReadToEnd();
            }
        }

        /// <summary>
        /// Dosyadan satırları okur
        /// </summary>
        /// <param name="filePath">Dosya yolu</param>
        /// <param name="encoding">Karakter kodlaması (varsayılan: UTF8)</param>
        /// <returns>Satırlar dizisi</returns>
        public static string[] ReadLinesShared(string filePath, Encoding encoding = null)
        {
            string content = ReadTextShared(filePath, encoding);
            return content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        /// <summary>
        /// Dosyanın var olup olmadığını kontrol eder
        /// </summary>
        public static bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }

        /// <summary>
        /// Klasörün var olup olmadığını kontrol eder, yoksa oluşturur
        /// </summary>
        public static void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        /// <summary>
        /// Dosyayı siler (varsa)
        /// </summary>
        public static void DeleteFileIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
