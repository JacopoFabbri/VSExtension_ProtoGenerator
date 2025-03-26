using System;
using System.ComponentModel.Design;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using Task = System.Threading.Tasks.Task;

namespace CSharpConvertToProto
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class ConvertToProtoCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("48e4d2bf-8218-4446-92b0-2e21cd5e5113");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConvertToProtoCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private ConvertToProtoCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static ConvertToProtoCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in ConvertToProtoCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ConvertToProtoCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var serviceProvider = this.package as IServiceProvider;
            var dte = serviceProvider?.GetService(typeof(DTE)) as DTE2;
            string solutionPath = dte?.Solution?.FullName;

            if (string.IsNullOrEmpty(solutionPath))
            {
                VsShellUtilities.ShowMessageBox(
                    this.package, "Nessuna soluzione aperta.", "ConvertToProtoCommand",
                    OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            CSharpClassParser cSharpClassParser = new CSharpClassParser();
            var classMap = cSharpClassParser.ParseSolution(solutionPath);

            var selectionWindow = new ClassSelectionWindow(classMap.Keys.ToList());
            if (selectionWindow.ShowDialog() == true)
            {
                string selectedClass = selectionWindow.SelectedClass;
                if (!string.IsNullOrEmpty(selectedClass))
                {
                    ProtoGenerator generator = new ProtoGenerator();

                    classMap.TryGetValue(selectedClass, out var classNodes);

                    string protoContent = generator.GenerateProto(classMap.Values.ToList(), selectedClass);

                    // Salva il file .proto nella cartella della soluzione
                    string protoPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(solutionPath), $"{selectedClass}.proto");
                    System.IO.File.WriteAllText(protoPath, protoContent);

                    // Aggiungi il file alla soluzione e al progetto
                    AddFileToSolution(dte, protoPath);

                    VsShellUtilities.ShowMessageBox(
                        this.package, $"File proto generato e aggiunto alla soluzione: {protoPath}", "ConvertToProtoCommand",
                        OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            }
        }

        private void AddFileToSolution(DTE2 dte, string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (dte.Solution != null && dte.Solution.IsOpen)
            {
                Project activeProject = dte.ActiveSolutionProjects is Array activeProjects && activeProjects.Length > 0
                    ? activeProjects.GetValue(0) as Project
                    : null;

                if (activeProject != null)
                {
                    activeProject.ProjectItems.AddFromFile(filePath);
                }
                else
                {
                    dte.Solution.AddFromFile(filePath);
                }
            }
        }

    }
}
