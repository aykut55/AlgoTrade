using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace AlgoTrade.Core.Utils
{
    /// <summary>
    /// Dosya işleminin sonucunu raporlar.
    /// </summary>
    public class FileOpResult
    {
        public bool      Success      { get; }
        public string    FilePath     { get; }
        public string    ErrorMessage { get; }
        public Exception Exception    { get; }

        private FileOpResult(bool success, string filePath, string errorMessage, Exception exception)
        {
            Success      = success;
            FilePath     = filePath;
            ErrorMessage = errorMessage;
            Exception    = exception;
        }

        public static FileOpResult Ok(string filePath)
            => new FileOpResult(true, filePath, null, null);

        public static FileOpResult Fail(string filePath, string message, Exception ex = null)
            => new FileOpResult(false, filePath, message, ex);

        public override string ToString()
            => Success
                ? $"[OK]   {FilePath}"
                : $"[FAIL] {FilePath} => {ErrorMessage}";
    }

    /// <summary>
    /// TXT ve CSV dosyaları için merkezi yazma/okuma/silme yardımcısı.
    /// - Tüm yazma işlemleri FileShare.ReadWrite ile yapılır;
    ///   başka bir process (Excel, Notepad vb.) dosyayı açıkken de çalışır.
    /// - Kilitli dosya veya başka bir IOException durumunda try-catch devreye girer,
    ///   işlem sonucu FileOpResult ile raporlanır ve Debug çıktısına yazılır.
    /// </summary>
    public static class FileUtils
    {
        private static readonly Encoding DefaultEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        // ─────────────────────────────────────────────────────────────
        // TXT
        // ─────────────────────────────────────────────────────────────

        /// <summary>Dosyayı baştan yazar (overwrite). Klasör yoksa oluşturur.</summary>
        public static FileOpResult WriteTxt(string filePath, string content)
            => WriteInternal(filePath, content, append: false);

        /// <summary>Dosyanın sonuna metin ekler. Dosya yoksa oluşturur.</summary>
        public static FileOpResult AppendTxt(string filePath, string content)
            => WriteInternal(filePath, content, append: true);

        /// <summary>Dosyanın sonuna tek satır ekler (otomatik newline).</summary>
        public static FileOpResult AppendLineTxt(string filePath, string line)
            => WriteInternal(filePath, line + Environment.NewLine, append: true);

        /// <summary>Dosyanın sonuna birden fazla satır ekler.</summary>
        public static FileOpResult AppendLinesTxt(string filePath, IEnumerable<string> lines)
            => WriteInternal(filePath, string.Join(Environment.NewLine, lines) + Environment.NewLine, append: true);

        // ─────────────────────────────────────────────────────────────
        // Eski isimlerle uyumluluk (WriteTextShared, AppendLineShared vb.)
        // ─────────────────────────────────────────────────────────────

        /// <summary>Dosyayı baştan yazar veya sona ekler. (FileShare.ReadWrite)</summary>
        public static FileOpResult WriteTextShared(string filePath, string content, bool append = false)
            => WriteInternal(filePath, content, append);

        /// <summary>Dosyanın sonuna tek satır ekler.</summary>
        public static FileOpResult AppendLineShared(string filePath, string line)
            => WriteInternal(filePath, line + Environment.NewLine, append: true);

        /// <summary>Dosyanın sonuna birden fazla satır ekler.</summary>
        public static FileOpResult AppendLinesShared(string filePath, IEnumerable<string> lines)
            => WriteInternal(filePath, string.Join(Environment.NewLine, lines) + Environment.NewLine, append: true);

        /// <summary>Dosyadan paylaşımlı modda okur. Hata durumunda null döner.</summary>
        public static string ReadTextShared(string filePath)
            => ReadText(filePath);

        /// <summary>Dosyadan satır dizisi okur. Hata durumunda boş dizi döner.</summary>
        public static string[] ReadLinesShared(string filePath)
            => ReadLines(filePath);

        // ─────────────────────────────────────────────────────────────
        // CSV
        // ─────────────────────────────────────────────────────────────

        /// <summary>CSV dosyasını baştan yazar (overwrite). Klasör yoksa oluşturur.</summary>
        public static FileOpResult WriteCsv(string filePath, string content)
            => WriteInternal(filePath, content, append: false);

        /// <summary>CSV dosyasının sonuna satır(lar) ekler.</summary>
        public static FileOpResult AppendCsv(string filePath, string row)
            => WriteInternal(filePath, row + Environment.NewLine, append: true);

        /// <summary>CSV dosyasını baştan yazar; önce header, sonra satırlar.</summary>
        public static FileOpResult WriteCsvWithHeader(string filePath, string header, IEnumerable<string> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(header);
            foreach (var row in rows)
                sb.AppendLine(row);
            return WriteInternal(filePath, sb.ToString(), append: false);
        }

        // ─────────────────────────────────────────────────────────────
        // StreamWriter — büyük/satır-satır yazımlar için
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Paylaşımlı modda (FileShare.ReadWrite) StreamWriter döndürür.
        /// Büyük dosyalar için satır satır yazmak üzere kullanın.
        /// Caller using bloğuyla kapatmalıdır.
        /// Başarısız olursa null döner ve hata Debug çıktısına yazılır.
        /// </summary>
        public static StreamWriter CreateWriter(string filePath, bool append = false)
        {
            try
            {
                EnsureDirectory(filePath);
                FileMode mode = append ? FileMode.Append : FileMode.Create;
                var fs = new FileStream(filePath, mode, FileAccess.Write, FileShare.ReadWrite);
                return new StreamWriter(fs, DefaultEncoding);
            }
            catch (Exception ex)
            {
                var result = FileOpResult.Fail(filePath, ex.Message, ex);
                Report(result);
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Okuma
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Dosyadan paylaşımlı modda (FileShare.ReadWrite) okur.
        /// Başka process yazıyor olsa bile okunabilir.
        /// Hata durumunda null döner.
        /// </summary>
        public static string ReadText(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Report(FileOpResult.Fail(filePath, "Dosya bulunamadı."));
                return null;
            }

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, DefaultEncoding);
                return sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                Report(FileOpResult.Fail(filePath, ex.Message, ex));
                return null;
            }
        }

        /// <summary>Dosyadan satır dizisi okur. Hata durumunda boş dizi döner.</summary>
        public static string[] ReadLines(string filePath)
        {
            string content = ReadText(filePath);
            if (content == null)
                return Array.Empty<string>();
            return content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        // ─────────────────────────────────────────────────────────────
        // Silme
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Dosyayı siler. Kilitli veya bulunamadıysa hata fırlatmaz, raporlar.
        /// </summary>
        public static FileOpResult DeleteFile(string filePath)
        {
            if (!File.Exists(filePath))
                return FileOpResult.Ok(filePath); // zaten yok, sorun değil

            try
            {
                File.Delete(filePath);
                return FileOpResult.Ok(filePath);
            }
            catch (IOException ex)
            {
                var result = FileOpResult.Fail(filePath, $"Kilitli (başka process kullanıyor): {ex.Message}", ex);
                Report(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = FileOpResult.Fail(filePath, ex.Message, ex);
                Report(result);
                return result;
            }
        }

        /// <summary>
        /// Klasördeki tüm dosyaları siler. Kilitli olanları atlar ve raporlar.
        /// </summary>
        /// <returns>(silinen sayısı, atlanan sayısı)</returns>
        public static (int deleted, int skipped) DeleteAllFiles(string directoryPath, bool includeSubdirectories = false)
        {
            if (!Directory.Exists(directoryPath))
                return (0, 0);

            var option = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(directoryPath, "*", option);

            int deleted = 0, skipped = 0;
            foreach (var file in files)
            {
                var result = DeleteFile(file);
                if (result.Success) deleted++;
                else                skipped++;
            }
            return (deleted, skipped);
        }

        // ─────────────────────────────────────────────────────────────
        // Yardımcı
        // ─────────────────────────────────────────────────────────────

        public static bool FileExists(string filePath)      => File.Exists(filePath);
        public static bool DirectoryExists(string dirPath)  => Directory.Exists(dirPath);

        public static void EnsureDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        // ─────────────────────────────────────────────────────────────
        // Internal
        // ─────────────────────────────────────────────────────────────

        private static FileOpResult WriteInternal(string filePath, string content, bool append)
        {
            if (string.IsNullOrEmpty(filePath))
                return FileOpResult.Fail(filePath, "Dosya yolu boş olamaz.");

            try
            {
                EnsureDirectory(filePath);
                FileMode mode = append ? FileMode.Append : FileMode.Create;

                using var fs = new FileStream(filePath, mode, FileAccess.Write, FileShare.ReadWrite);
                using var sw = new StreamWriter(fs, DefaultEncoding);
                sw.Write(content);
                sw.Flush();

                return FileOpResult.Ok(filePath);
            }
            catch (IOException ex)
            {
                var result = FileOpResult.Fail(filePath, $"Kilitli (başka process kullanıyor): {ex.Message}", ex);
                Report(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = FileOpResult.Fail(filePath, ex.Message, ex);
                Report(result);
                return result;
            }
        }

        private static void Report(FileOpResult result)
        {
            Debug.WriteLine($"[FileUtils] {result}");
        }
    }
}
