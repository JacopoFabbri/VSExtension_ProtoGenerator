using System;
using System.IO;
using System.Xml.Serialization;
using CSharpConvertToProto.Models;

namespace CSharpConvertToProto.Service
{
    public static class SettingsStore
    {
        public static event Action<CSharpConvertToProto.Models.Settings> SettingsChanged;

        private static string GetSettingsPath()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CSharpConvertToProto");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "settings.json");
        }

        public static void Save(Settings settings)
        {
            var path = GetSettingsPath();
            try
            {
                var serializer = new XmlSerializer(typeof(Settings));
                using (var stream = File.Create(path))
                {
                    serializer.Serialize(stream, settings);
                }
                try
                {
                    SettingsChanged?.Invoke(settings);
                }
                catch { }
            }
            catch { }
        }

        public static Settings Load()
        {
            var path = GetSettingsPath();
            if (!File.Exists(path)) return null;
            try
            {
                var serializer = new XmlSerializer(typeof(Settings));
                using (var stream = File.OpenRead(path))
                {
                    return serializer.Deserialize(stream) as Settings;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
