using System;
using System.IO;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using CSharpConvertToProto.Models;

namespace CSharpConvertToProto.Service
{
    // Watches Visual Studio DocumentSaved events and triggers proto generation
    // when a file inside the monitored folder is saved.
    public class SolutionFolderWatcher : IDisposable
    {
        private readonly DTE2 _dte;
        private readonly string _folderToWatch;
        private readonly string _outputFolder;
        private readonly string _nameSpace;
        private DocumentEvents _documentEvents;
        private bool _started;

        public SolutionFolderWatcher(DTE2 dte, string folderToWatch, string outputFolder, string nameSpace)
        {
            _dte = dte ?? throw new ArgumentNullException(nameof(dte));
            _folderToWatch = folderToWatch ?? throw new ArgumentNullException(nameof(folderToWatch));
            _outputFolder = outputFolder ?? throw new ArgumentNullException(nameof(outputFolder));
            _nameSpace = string.IsNullOrWhiteSpace(nameSpace) ? "Generated" : nameSpace;
        }

        public void Start()
        {
            if (_started) return;
            _documentEvents = _dte.Events.DocumentEvents as DocumentEvents;
            if (_documentEvents != null)
            {
                _documentEvents.DocumentSaved += OnDocumentSaved;
                _started = true;

                // initial generation
                TryGenerate();
            }
        }

        private void OnDocumentSaved(Document document)
        {
            try
            {
                if (document == null) return;
                string path = document.FullName;
                if (string.IsNullOrEmpty(path)) return;

                if (!Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase)) return;

                // Only respond if file is inside monitored folder
                if (IsPathUnderFolder(path, _folderToWatch))
                {
                    TryGenerate();
                }
            }
            catch
            {
                // swallow
            }
        }

        private bool IsPathUnderFolder(string filePath, string folderPath)
        {
            var fullFile = Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullFolder = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullFile.StartsWith(fullFolder, StringComparison.OrdinalIgnoreCase);
        }

        private void TryGenerate()
        {
            try
            {
                ShowStatus("Generating .proto from models...");
                var parser = new CSharpClassParser();
                var classes = parser.ParseFolder(_folderToWatch);

                var generator = new ProtoGenerator();
                var proto = generator.GenerateProto(classes.Values, _nameSpace);

                if (!Directory.Exists(_outputFolder)) Directory.CreateDirectory(_outputFolder);
                var outPath = Path.Combine(_outputFolder, "GeneratedProtoModels.proto");
                File.WriteAllText(outPath, proto);
                ShowStatus($"Generated .proto: {outPath}");

                // Try to add or overwrite the file in the containing project
                TryAddFileToProject(outPath);
                ShowStatus($"Updated project with generated .proto");
            }
            catch
            {
                ShowError("Failed to generate .proto from models.");
            }
        }

        private void TryAddFileToProject(string filePath)
        {
            try
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    Project containingProject = null;

                    foreach (Project proj in _dte.Solution.Projects)
                    {
                        try
                        {
                            var projDir = System.IO.Path.GetDirectoryName(proj.FullName);
                            if (!string.IsNullOrEmpty(projDir) && filePath.StartsWith(projDir, StringComparison.OrdinalIgnoreCase))
                            {
                                containingProject = proj;
                                break;
                            }
                        }
                        catch { }
                    }

                    // fallback to active project
                    if (containingProject == null)
                    {
                        var active = _dte.ActiveSolutionProjects as Array;
                        if (active != null && active.Length > 0) containingProject = active.GetValue(0) as Project;
                    }

                    if (containingProject != null)
                    {
                        // Ensure there is a folder named 'Generated' in the project. If not, create it.
                        ProjectItem generatedFolder = null;
                        try
                        {
                            foreach (ProjectItem pi in containingProject.ProjectItems)
                            {
                                if (string.Equals(pi.Name, "Generated", StringComparison.OrdinalIgnoreCase))
                                {
                                    generatedFolder = pi;
                                    break;
                                }
                            }
                        }
                        catch { }

                        try
                        {
                            if (generatedFolder == null)
                            {
                                generatedFolder = containingProject.ProjectItems.AddFolder("Generated");
                            }

                            ShowStatus("Adding generated .proto to project...");

                            // If the file is already part of the Generated folder or project, remove it first so it will be added fresh
                            try
                            {
                                ProjectItem existing = FindProjectItemByPath(containingProject, filePath);
                                if (existing != null)
                                {
                                    existing.Delete();
                                }
                            }
                            catch { }

                            // Add file inside Generated folder
                            if (generatedFolder != null)
                            {
                                generatedFolder.ProjectItems.AddFromFile(filePath);
                            }
                            else
                            {
                                containingProject.ProjectItems.AddFromFile(filePath);
                            }

                            ShowStatus("Generated .proto added to project.");
                        }
                        catch (Exception ex)
                        {
                            ShowError($"Failed to add generated .proto to project: {ex.Message}");
                        }
                    }
                });
            }
            catch
            {
                // swallow
            }
        }

        private void ShowStatus(string text)
        {
            try
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var status = ServiceProvider.GlobalProvider.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
                    try
                    {
                        status?.SetText(text);
                    }
                    catch { }
                });
            }
            catch { }
        }

        private void ShowError(string message)
        {
            try
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    VsShellUtilities.ShowMessageBox(ServiceProvider.GlobalProvider, message, "Proto Generator", OLEMSGICON.OLEMSGICON_CRITICAL, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                });
            }
            catch { }
        }

        private ProjectItem FindProjectItemByPath(Project project, string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (ProjectItem item in project.ProjectItems)
            {
                var itemPath = GetFullPathFromProjectItem(item);
                if (string.Equals(itemPath, filePath, StringComparison.OrdinalIgnoreCase)) return item;
                // search subitems
                var found = FindInProjectItems(item.ProjectItems, filePath);
                if (found != null) return found;
            }
            return null;
        }

        private ProjectItem FindInProjectItems(ProjectItems items, string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (items == null) return null;
            foreach (ProjectItem item in items)
            {
                var itemPath = GetFullPathFromProjectItem(item);
                if (string.Equals(itemPath, filePath, StringComparison.OrdinalIgnoreCase)) return item;
                var found = FindInProjectItems(item.ProjectItems, filePath);
                if (found != null) return found;
            }
            return null;
        }

        private string GetFullPathFromProjectItem(ProjectItem item)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (item == null) return null;
                if (item.FileCount > 0) return item.FileNames[1];
            }
            catch { }
            return null;
        }

        public void Stop()
        {
            if (!_started) return;
            if (_documentEvents != null)
            {
                _documentEvents.DocumentSaved -= OnDocumentSaved;
                _documentEvents = null;
            }
            _started = false;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
