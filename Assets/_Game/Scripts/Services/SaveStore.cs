using System;
using System.IO;
using System.Text;

namespace BottleSort.Services
{
    // Запись файла так, чтобы при вылете или севшей батарее на диске всегда оставалась целая копия:
    // сначала пишем во временный файл, потом подменяем основной, прошлая версия уходит в .bak.
    public static class SaveStore
    {
        public static string TempPath(string path) => path + ".tmp";
        public static string BackupPath(string path) => path + ".bak";

        public static void WriteAtomic(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temp = TempPath(path);
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (!File.Exists(path))
            {
                File.Move(temp, path);
                return;
            }

            try
            {
                File.Replace(temp, path, BackupPath(path), true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceByCopy(temp, path);
            }
            catch (IOException)
            {
                ReplaceByCopy(temp, path);
            }
        }

        // Сначала основной файл, если он битый или пропал — резервная копия.
        public static bool TryRead(string path, Func<string, bool> accept, out string text)
        {
            foreach (string candidate in new[] { path, BackupPath(path) })
            {
                if (!File.Exists(candidate))
                    continue;
                try
                {
                    string content = File.ReadAllText(candidate, Encoding.UTF8);
                    if (accept(content))
                    {
                        text = content;
                        return true;
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            text = null;
            return false;
        }

        public static void DeleteAll(string path)
        {
            foreach (string file in new[] { path, BackupPath(path), TempPath(path) })
                if (File.Exists(file))
                    File.Delete(file);
        }

        static void ReplaceByCopy(string temp, string path)
        {
            File.Copy(path, BackupPath(path), true);
            File.Copy(temp, path, true);
            File.Delete(temp);
        }
    }
}
