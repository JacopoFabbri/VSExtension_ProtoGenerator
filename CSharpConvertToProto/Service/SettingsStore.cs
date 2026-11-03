using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using CSharpConvertToProto.Models;
using EnvDTE80;

namespace CSharpConvertToProto.Service
{
    public static class SettingsStore
    {
        public static event Action<CSharpConvertToProto.Models.Settings> SettingsChanged;

        private static string GetSettingsPath()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CSharpConvertToProto");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "settings.shared.xml");
        }

        public static string ResolveSolutionNameKey(DTE2 dte)
        {
            try
            {
                var fullName = dte?.Solution?.FullName;
                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    return NormalizeSolutionName(Path.GetFileNameWithoutExtension(fullName));
                }
            }
            catch { }

            return "default";
        }

        private static string NormalizeSolutionName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "default";
            return value.Trim().ToUpperInvariant();
        }

        public static void Save(Settings settings, string solutionNameKey)
        {
            var path = GetSettingsPath();
            try
            {
                var store = LoadStore(path);
                var key = NormalizeSolutionName(solutionNameKey);
                var existing = store.Entries.FirstOrDefault(e => string.Equals(e.SolutionName, key, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    existing = new SettingsEntry { SolutionName = key, Conf = settings ?? new Settings() };
                    store.Entries.Add(existing);
                }
                else
                {
                    existing.Conf = settings ?? new Settings();
                }

                var serializer = new XmlSerializer(typeof(SettingsStoreModel));
                using (var stream = File.Create(path))
                {
                    serializer.Serialize(stream, store);
                }

                try
                {
                    SettingsChanged?.Invoke(settings);
                }
                catch { }
            }
            catch { }
        }

        public static Settings Load(string solutionNameKey)
        {
            var path = GetSettingsPath();
            try
            {
                var store = LoadStore(path);
                var key = NormalizeSolutionName(solutionNameKey);

                var bySolutionName = store.Entries.FirstOrDefault(e => string.Equals(e.SolutionName, key, StringComparison.OrdinalIgnoreCase))?.Conf;
                if (bySolutionName != null)
                {
                    return bySolutionName;
                }

                // backward compatibility with previous key name in persisted XML
                return store.Entries.FirstOrDefault(e => string.Equals(e.ProjectGuid, key, StringComparison.OrdinalIgnoreCase))?.Conf;
            }
            catch
            {
                return null;
            }
        }

        private static SettingsStoreModel LoadStore(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return new SettingsStoreModel();
                }

                var serializer = new XmlSerializer(typeof(SettingsStoreModel));
                using (var stream = File.OpenRead(path))
                {
                    return serializer.Deserialize(stream) as SettingsStoreModel ?? new SettingsStoreModel();
                }
            }
            catch
            {
                return new SettingsStoreModel();
            }
        }

        [Serializable]
        public class SettingsStoreModel
        {
            public List<SettingsEntry> Entries { get; set; } = new List<SettingsEntry>();
        }

        [Serializable]
        public class SettingsEntry
        {
            public string SolutionName { get; set; }
            public string ProjectGuid { get; set; }
            public Settings Conf { get; set; } = new Settings();
        }
    }
}
