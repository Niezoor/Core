using System;
using System.IO;
using System.Text;

namespace Core.SaveSystem.Backends
{
    /// <summary>
    /// A save file written through a temp file and swapped in, keeping the previous one as <c>.bak</c>.
    /// </summary>
    public sealed class FileSaveBackend : ISaveBackend
    {
        private static readonly UTF8Encoding Utf8 = new(false);

        public string FilePath { get; }
        public string BackupPath => FilePath + ".bak";
        private string TempPath => FilePath + ".tmp";

        public FileSaveBackend(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("Save path is empty.", nameof(filePath));
            FilePath = filePath;
        }

        public string Read() => ReadIfExists(FilePath);

        public string ReadBackup() => ReadIfExists(BackupPath);

        public void Write(string text)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var bytes = Utf8.GetBytes(text);
            using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (!File.Exists(FilePath))
            {
                File.Move(TempPath, FilePath);
                return;
            }

            try
            {
                File.Replace(TempPath, FilePath, BackupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                // Not atomic, but a crash between the steps still leaves the backup, which the store falls back to.
                File.Copy(FilePath, BackupPath, true);
                File.Delete(FilePath);
                File.Move(TempPath, FilePath);
            }
        }

        public void QuarantineCorrupt()
        {
            if (!File.Exists(FilePath)) return;
            File.Move(FilePath, $"{FilePath}.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}");
        }

        public void Delete()
        {
            DeleteIfExists(FilePath);
            DeleteIfExists(BackupPath);
            DeleteIfExists(TempPath);
        }

        public override string ToString() => FilePath;

        private static string ReadIfExists(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path, Utf8) : null;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
