using EnvDTE80;
using Microsoft.VisualStudio.Shell;
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

            var settings = SettingsStore.Load() ?? new Models.Settings();
            var window = new SettingsWindow(settings);
            window.Owner = null;
            var result = window.ShowDialog();
            if (result == true)
            {
                // restart watcher? For simplicity, user can restart VS; or future enhancement to reconfigure watcher at runtime.
            }
        }
    }
}
