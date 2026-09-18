using System;
using System.IO;

namespace ost_uploader
{
    public class EventFilePreferenceStore
    {
        private readonly string _filePath;

        public EventFilePreferenceStore()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var directory = Path.Combine(local, "ost-uploader");
            Directory.CreateDirectory(directory);
            _filePath = Path.Combine(directory, "last-event-file.txt");
        }

        public EventFilePreferenceStore(string filePath)
        {
            _filePath = filePath;
        }

        public bool TryGetExistingPath(out string path)
        {
            path = string.Empty;

            try
            {
                if (!File.Exists(_filePath))
                    return false;

                var savedPath = File.ReadAllText(_filePath).Trim();
                if (string.IsNullOrWhiteSpace(savedPath) || !File.Exists(savedPath))
                    return false;

                path = savedPath;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool SavePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(_filePath, path);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}