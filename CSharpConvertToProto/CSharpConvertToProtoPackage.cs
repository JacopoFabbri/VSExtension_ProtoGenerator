using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace CSharpConvertToProto
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(CSharpConvertToProtoPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class CSharpConvertToProtoPackage : AsyncPackage
    {
        /// <summary>
        /// CSharpConvertToProtoPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "5074d942-c26e-400b-b76e-453e472d4d64";

        private Service.SolutionFolderWatcher _currentWatcher;

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // Registra MSBuild all'inizio
            //if (!MSBuildLocator.IsRegistered)
            //{
            //    MSBuildLocator.RegisterDefaults();
            //}
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await ConvertToProtoCommand.InitializeAsync(this);
            // Initialize settings command (menu command) so users can open settings
            await SettingsCommand.InitializeAsync(this);

            // Load settings; if missing, ask the user for configuration
            var settings = Service.SettingsStore.Load();
            // Subscribe to settings changes to reconfigure the watcher at runtime
            Service.SettingsStore.SettingsChanged += async (s) => await OnSettingsChangedAsync(s);
            if (settings == null)
            {
                try
                {
                    var settingsWindow = new Windows.SettingsWindow();
                    var res = settingsWindow.ShowDialog();
                    if (res == true)
                    {
                        settings = Service.SettingsStore.Load();
                    }
                }
                catch { }
            }

            // Start a file watcher that follows a folder referenced by Visual Studio
            // Prefer the currently selected folder in Solution Explorer, then the
            // active project folder, then the solution folder.
            try
            {
                var dte = await GetServiceAsync(typeof(DTE)) as DTE2;
                string sourceFolder = null;

                if (dte != null)
                {
                    // Ensure we're on the UI thread for DTE calls
                    await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                    UIHierarchyItem selectedItem = (dte.ToolWindows.SolutionExplorer.SelectedItems as object[])?.FirstOrDefault() as UIHierarchyItem;
                    if (selectedItem?.Object is ProjectItem projectItem && projectItem.Kind == EnvDTE.Constants.vsProjectItemKindPhysicalFolder)
                    {
                        sourceFolder = projectItem.FileNames[1];
                    }

                    if (string.IsNullOrEmpty(sourceFolder))
                    {
                        var activeProject = dte.ActiveSolutionProjects is Array activeProjects && activeProjects.Length > 0
                            ? activeProjects.GetValue(0) as Project
                            : null;

                        if (activeProject != null && !string.IsNullOrEmpty(activeProject.FullName))
                        {
                            sourceFolder = Path.GetDirectoryName(activeProject.FullName);
                        }
                    }

                    if (string.IsNullOrEmpty(sourceFolder) && dte.Solution != null && !string.IsNullOrEmpty(dte.Solution.FullName))
                    {
                        sourceFolder = Path.GetDirectoryName(dte.Solution.FullName);
                    }
                }

                if (!string.IsNullOrEmpty(sourceFolder))
                {
                    // Determine folder to watch and output folder from settings (if provided) or defaults
                    string folderToWatch = null;
                    string outputFolder = null;

                    if (settings != null && !string.IsNullOrEmpty(settings.SourceFolder) && Directory.Exists(settings.SourceFolder))
                    {
                        folderToWatch = settings.SourceFolder;
                        outputFolder = string.IsNullOrEmpty(settings.OutputFolder) ? Path.Combine(sourceFolder, "GeneratedProto") : settings.OutputFolder;
                    }
                    else
                    {
                        string targetFolderName = settings?.TargetFolderName ?? "ModelsForGeneration";
                        string solutionRoot = sourceFolder;
                        string[] matches = Directory.GetDirectories(solutionRoot, targetFolderName, SearchOption.AllDirectories);
                        if (matches.Length > 0)
                        {
                            folderToWatch = matches[0];
                            outputFolder = Path.Combine(solutionRoot, "GeneratedProto");
                        }
                    }

                    if (!string.IsNullOrEmpty(folderToWatch) && !string.IsNullOrEmpty(outputFolder))
                    {
                        _currentWatcher = new Service.SolutionFolderWatcher(dte, folderToWatch, outputFolder, settings?.NameSpace ?? "Generated");
                        _currentWatcher.Start();
                    }
                }
            }
            catch (System.Exception)
            {
                // swallow exceptions during package init to avoid breaking VS load
            }
        }

        private async System.Threading.Tasks.Task OnSettingsChangedAsync(CSharpConvertToProto.Models.Settings newSettings)
        {
            try
            {
                await this.JoinableTaskFactory.SwitchToMainThreadAsync(this.DisposalToken);
                var dte = await GetServiceAsync(typeof(DTE)) as DTE2;
                if (dte == null) return;

                // stop existing
                try { _currentWatcher?.Stop(); _currentWatcher = null; } catch { }

                // determine solution root
                string sourceFolder = null;
                UIHierarchyItem selectedItem = (dte.ToolWindows.SolutionExplorer.SelectedItems as object[])?.FirstOrDefault() as UIHierarchyItem;
                if (selectedItem?.Object is ProjectItem projectItem && projectItem.Kind == EnvDTE.Constants.vsProjectItemKindPhysicalFolder)
                {
                    sourceFolder = projectItem.FileNames[1];
                }

                if (string.IsNullOrEmpty(sourceFolder))
                {
                    var activeProject = dte.ActiveSolutionProjects is Array activeProjects && activeProjects.Length > 0
                        ? activeProjects.GetValue(0) as Project
                        : null;

                    if (activeProject != null && !string.IsNullOrEmpty(activeProject.FullName))
                    {
                        sourceFolder = Path.GetDirectoryName(activeProject.FullName);
                    }
                }

                if (string.IsNullOrEmpty(sourceFolder) && dte.Solution != null && !string.IsNullOrEmpty(dte.Solution.FullName))
                {
                    sourceFolder = Path.GetDirectoryName(dte.Solution.FullName);
                }

                if (string.IsNullOrEmpty(sourceFolder)) return;

                string folderToWatch = null;
                string outputFolder = null;

                if (newSettings != null && !string.IsNullOrEmpty(newSettings.SourceFolder) && Directory.Exists(newSettings.SourceFolder))
                {
                    folderToWatch = newSettings.SourceFolder;
                    outputFolder = string.IsNullOrEmpty(newSettings.OutputFolder) ? Path.Combine(sourceFolder, "GeneratedProto") : newSettings.OutputFolder;
                }
                else
                {
                    string targetFolderName = newSettings?.TargetFolderName ?? "ModelsForGeneration";
                    string solutionRoot = sourceFolder;
                    string[] matches = Directory.GetDirectories(solutionRoot, targetFolderName, SearchOption.AllDirectories);
                    if (matches.Length > 0)
                    {
                        folderToWatch = matches[0];
                        outputFolder = Path.Combine(solutionRoot, "GeneratedProto");
                    }
                }

                if (!string.IsNullOrEmpty(folderToWatch) && !string.IsNullOrEmpty(outputFolder))
                {
                    _currentWatcher = new Service.SolutionFolderWatcher(dte, folderToWatch, outputFolder, newSettings?.NameSpace ?? "Generated");
                    _currentWatcher.Start();
                }
            }
            catch { }
        }

        #endregion
    }
}
