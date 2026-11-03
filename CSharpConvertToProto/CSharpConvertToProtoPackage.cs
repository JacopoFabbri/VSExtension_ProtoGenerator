using System;
using System.Runtime.InteropServices;
using System.Threading;
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

        private Service.FileWatcherService _currentWatcher;
        private bool _watcherEnabled = true;

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
            await SettingsCommand.InitializeAsync(this);
            await StartWatcherCommand.InitializeAsync(this);
            await StopWatcherCommand.InitializeAsync(this);

            var dte = await GetServiceAsync(typeof(EnvDTE.DTE)) as DTE2;
            var projectGuid = Service.SettingsStore.ResolveProjectGuid(dte);
            var settings = Service.SettingsStore.Load(projectGuid);

            Service.SettingsStore.SettingsChanged += (s) =>
            {
                _ = this.JoinableTaskFactory.RunAsync(async () => await OnSettingsChangedAsync(s));
            };

            if (settings == null)
            {
                try
                {
                    var settingsWindow = new Windows.SettingsWindow(null, dte);
                    var res = settingsWindow.ShowDialog();
                    if (res == true)
                    {
                        settings = Service.SettingsStore.Load(projectGuid);
                    }
                }
                catch { }
            }

            await OnSettingsChangedAsync(settings);
        }

        internal async Task StartWatcherFromSavedSettingsAsync()
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(this.DisposalToken);

            _watcherEnabled = true;
            var dte = await GetServiceAsync(typeof(EnvDTE.DTE)) as DTE2;
            var projectGuid = Service.SettingsStore.ResolveProjectGuid(dte);
            var settings = Service.SettingsStore.Load(projectGuid);
            await OnSettingsChangedAsync(settings);
        }

        internal async Task StopWatcherAsync()
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(this.DisposalToken);
            _watcherEnabled = false;
            try { _currentWatcher?.Stop(); _currentWatcher = null; } catch { }
        }

        private async Task OnSettingsChangedAsync(CSharpConvertToProto.Models.Settings newSettings)
        {
            try
            {
                await this.JoinableTaskFactory.SwitchToMainThreadAsync(this.DisposalToken);
                try { _currentWatcher?.Stop(); _currentWatcher = null; } catch { }

                if (!_watcherEnabled)
                {
                    return;
                }

                if (newSettings == null || string.IsNullOrWhiteSpace(newSettings.SourceFolder) || string.IsNullOrWhiteSpace(newSettings.OutputFolder))
                {
                    return;
                }

                _currentWatcher = new Service.FileWatcherService(
                    newSettings.SourceFolder,
                    newSettings.OutputFolder,
                    newSettings?.NameSpace ?? "Generated",
                    newSettings?.ReplaceClassTextFrom,
                    newSettings?.ReplaceClassTextTo);
                _currentWatcher.Start();
            }
            catch { }
        }

        #endregion
    }
}
