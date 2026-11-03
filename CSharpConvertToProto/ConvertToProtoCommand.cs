using CSharpConvertToProto.Windows;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace CSharpConvertToProto
{
    internal sealed class ConvertToProtoCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("48e4d2bf-8218-4446-92b0-2e21cd5e5113");
        private readonly AsyncPackage package;

        private ConvertToProtoCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static ConvertToProtoCommand Instance { get; private set; }

        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider => this.package;

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ConvertToProtoCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var serviceProvider = this.package as IServiceProvider;
            var dte = serviceProvider?.GetService(typeof(EnvDTE.DTE)) as DTE2;
            var solutionNameKey = Service.SettingsStore.ResolveSolutionNameKey(dte);
            var settings = Service.SettingsStore.Load(solutionNameKey) ?? new Models.Settings();
            var window = new SettingsWindow(settings, dte);
            var result = window.ShowDialog();
            if (result == true)
            {
                VsShellUtilities.ShowMessageBox(
                    this.package,
                    "Configurazione salvata. Il monitoraggio in background è stato aggiornato.",
                    "ConvertToProtoCommand",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }
}
