using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CSharpConvertToProto.Windows;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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
            var dte = serviceProvider?.GetService(typeof(DTE)) as DTE2;
            string selectedFolder = GetSelectedFolder(dte);

            if (string.IsNullOrEmpty(selectedFolder))
            {
                VsShellUtilities.ShowMessageBox(
                    this.package, "Seleziona una cartella valida all'interno della soluzione.", "ConvertToProtoCommand",
                    OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            CSharpClassParser parser = new CSharpClassParser();
            var classMap = parser.ParseFolder(selectedFolder);

            var selectionWindow = new ClassSelectionWindow(classMap.Keys.ToList());
            if (selectionWindow.ShowDialog() == true)
            {
                string selectedClass = selectionWindow.SelectedClass;
                if (!string.IsNullOrEmpty(selectedClass))
                {
                    ProtoGenerator generator = new ProtoGenerator();
                    classMap.TryGetValue(selectedClass, out var classNode);

                    ServicesSelectorWindow servicesSelectorWindow = new ServicesSelectorWindow();
                    if (servicesSelectorWindow.ShowDialog() == true)
                    {
                        var serviceTOAddAtProtoEnums = servicesSelectorWindow.SelectItems;
                        if (serviceTOAddAtProtoEnums.Any())
                        {
                            SpecifyNamespaceWindow specifyNamespaceWindow = new SpecifyNamespaceWindow();
                            if (specifyNamespaceWindow.ShowDialog() == true)
                            {
                                var nameSpace = specifyNamespaceWindow.NameSpaceInput;
                                if (string.IsNullOrEmpty(nameSpace))
                                {
                                    nameSpace = "Generated";
                                }

                                string protoContent = generator.GenerateProto(classMap.Values.ToList(), selectedClass, serviceTOAddAtProtoEnums, nameSpace);

                                using (var dialog = new FolderBrowserDialog())
                                {
                                    dialog.SelectedPath = selectedFolder;
                                    DialogResult result = dialog.ShowDialog();
                                    if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                                    {
                                        string protoPath = Path.Combine(dialog.SelectedPath, $"{selectedClass}.proto");
                                        File.WriteAllText(protoPath, protoContent);

                                        AddFileToSolution(dte, protoPath);

                                        VsShellUtilities.ShowMessageBox(
                                            this.package, $"File proto generato: {protoPath}", "ConvertToProtoCommand",
                                            OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private string GetSelectedFolder(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            UIHierarchyItem selectedItem = (dte.ToolWindows.SolutionExplorer.SelectedItems as object[])?.FirstOrDefault() as UIHierarchyItem;
            if (selectedItem?.Object is ProjectItem projectItem && projectItem.Kind == EnvDTE.Constants.vsProjectItemKindPhysicalFolder)
            {
                return projectItem.FileNames[1];
            }
            return null;
        }

        private void AddFileToSolution(DTE2 dte, string filePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (dte.Solution != null && dte.Solution.IsOpen)
            {
                EnvDTE.Project activeProject = dte.ActiveSolutionProjects is Array activeProjects && activeProjects.Length > 0
                    ? activeProjects.GetValue(0) as EnvDTE.Project
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
