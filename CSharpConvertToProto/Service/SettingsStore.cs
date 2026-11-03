using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using CSharpConvertToProto.Models;
using EnvDTE;
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

        public static string ResolveProjectGuid(DTE2 dte)
        {
            try
            {
                var selected = (dte?.ToolWindows?.SolutionExplorer?.SelectedItems as object[])?.FirstOrDefault() as UIHierarchyItem;
                if (selected?.Object is Project selectedProject)
                {
                    var guid = TryGetProjectGuid(selectedProject);
                    if (!string.IsNullOrWhiteSpace(guid)) return guid;
                }

                if (selected?.Object is ProjectItem selectedItem && selectedItem.ContainingProject != null)
                {
                    var guid = TryGetProjectGuid(selectedItem.ContainingProject);
                    if (!string.IsNullOrWhiteSpace(guid)) return guid;
                }

                if (dte?.ActiveSolutionProjects is Array activeProjects && activeProjects.Length > 0)
                {
                    var activeProject = activeProjects.GetValue(0) as Project;
                    var guid = TryGetProjectGuid(activeProject);
                    if (!string.IsNullOrWhiteSpace(guid)) return guid;
                }

                foreach (Project project in dte?.Solution?.Projects)
                {
                    var guid = TryGetProjectGuid(project);
                    if (!string.IsNullOrWhiteSpace(guid)) return guid;
                }
            }
            catch { }

            return "default";
        }

        private static string TryGetProjectGuid(Project project)
        {
            try
            {
                if (project == null) return null;

                try
                {
                    var value = project.Properties?.Item("ProjectGuid")?.Value as string;
                    var normalized = NormalizeGuid(value);
                    if (!string.IsNullOrWhiteSpace(normalized)) return normalized;
                }
                catch { }

                if (!string.IsNullOrWhiteSpace(project.FullName) && File.Exists(project.FullName))
                {
                    var doc = XDocument.Load(project.FullName);
                    var projectGuid = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "ProjectGuid")?.Value;
                    var normalized = NormalizeGuid(projectGuid);
                    if (!string.IsNullOrWhiteSpace(normalized)) return normalized;
                }
            }
            catch { }

            return null;
        }

        private static string NormalizeGuid(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (Guid.TryParse(value, out var guid))
            {
                return guid.ToString("D").ToUpperInvariant();
            }

            return value.Trim().ToUpperInvariant();
        }

        public static void Save(Settings settings, string projectGuid)
        {
            var path = GetSettingsPath();
            try
            {
                var store = LoadStore(path);
                var key = string.IsNullOrWhiteSpace(projectGuid) ? "default" : NormalizeGuid(projectGuid);
                var existing = store.Entries.FirstOrDefault(e => string.Equals(e.ProjectGuid, key, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    existing = new SettingsEntry { ProjectGuid = key, Conf = settings ?? new Settings() };
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

        public static Settings Load(string projectGuid)
        {
            var path = GetSettingsPath();
            try
            {
                var store = LoadStore(path);
                var key = string.IsNullOrWhiteSpace(projectGuid) ? "default" : NormalizeGuid(projectGuid);
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
            public string ProjectGuid { get; set; }
            public Settings Conf { get; set; } = new Settings();
        }
    }
}
