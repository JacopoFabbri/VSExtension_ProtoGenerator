using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using CSharpConvertToProto.Windows;
using CSharpConvertToProto.Service;

namespace CSharpConvertToProto
{
    internal sealed class SettingsCommand
    {
        public const int CommandId = 0x0101;
        public static readonly Guid CommandSet = new Guid("48e4d2bf-8218-4446-92b0-2e21cd5e5113");
        private readonly AsyncPackage _package;

        private SettingsCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static SettingsCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new SettingsCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var serviceProvider = _package as IServiceProvider;
            var dte = serviceProvider?.GetService(typeof(EnvDTE.DTE)) as DTE2;
            var solutionNameKey = SettingsStore.ResolveSolutionNameKey(dte);
            var settings = SettingsStore.Load(solutionNameKey) ?? new Models.Settings();
            var window = new SettingsWindow(settings, dte);
            window.Owner = null;
            var result = window.ShowDialog();
            if (result == true)
            {
                // watcher is restarted via SettingsStore.SettingsChanged subscription in package
            }
        }
    }

    internal sealed class StartWatcherCommand
    {
        public const int CommandId = 0x0102;
        public static readonly Guid CommandSet = new Guid("48e4d2bf-8218-4446-92b0-2e21cd5e5113");
        private readonly AsyncPackage _package;

        private StartWatcherCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static StartWatcherCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new StartWatcherCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_package is CSharpConvertToProtoPackage package)
            {
                _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => await package.StartWatcherFromSavedSettingsAsync());

                VsShellUtilities.ShowMessageBox(
                    _package,
                    "Watcher avviato.",
                    "CSharpConvertToProto",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }

    internal sealed class StopWatcherCommand
    {
        public const int CommandId = 0x0103;
        public static readonly Guid CommandSet = new Guid("48e4d2bf-8218-4446-92b0-2e21cd5e5113");
        private readonly AsyncPackage _package;

        private StopWatcherCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static StopWatcherCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new StopWatcherCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_package is CSharpConvertToProtoPackage package)
            {
                _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => await package.StopWatcherAsync());

                VsShellUtilities.ShowMessageBox(
                    _package,
                    "Watcher fermato.",
                    "CSharpConvertToProto",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }
}
