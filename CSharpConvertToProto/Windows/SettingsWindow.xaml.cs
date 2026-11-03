using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Forms;
using CSharpConvertToProto.Models;
using CSharpConvertToProto.Service;
using EnvDTE80;
using System.IO;

namespace CSharpConvertToProto.Windows
{
    public partial class SettingsWindow : System.Windows.Window
    {
        public Settings CurrentSettings { get; private set; }
        private readonly DTE2 _dte;

        public SettingsWindow(Settings settings = null, DTE2 dte = null)
        {
            InitializeComponent();
            CurrentSettings = settings ?? new Settings();
            _dte = dte;

            string solutionFolder = null;
            try
            {
                if (_dte?.Solution != null && !string.IsNullOrEmpty(_dte.Solution.FullName))
                {
                    solutionFolder = Path.GetDirectoryName(_dte.Solution.FullName);
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(CurrentSettings.SourceFolder))
            {
                CurrentSettings.SourceFolder = !string.IsNullOrEmpty(solutionFolder) ? solutionFolder : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(CurrentSettings.OutputFolder))
            {
                CurrentSettings.OutputFolder = !string.IsNullOrEmpty(solutionFolder) ? solutionFolder : string.Empty;
            }

            SourceFolderText.Text = CurrentSettings.SourceFolder ?? string.Empty;
            OutputFolderText.Text = CurrentSettings.OutputFolder ?? string.Empty;
            NamespaceText.Text = CurrentSettings.NameSpace ?? "Generated";
            ReplaceFromText.Text = string.IsNullOrWhiteSpace(CurrentSettings.ReplaceClassTextFrom) ? "infra" : CurrentSettings.ReplaceClassTextFrom;
            ReplaceToText.Text = string.IsNullOrWhiteSpace(CurrentSettings.ReplaceClassTextTo) ? "grpc" : CurrentSettings.ReplaceClassTextTo;
        }

        private void ProjectCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
        }

        private void SourceFolderCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
        }

        private void OutputFolderCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
        }

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.SelectedPath = SourceFolderText.Text;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SourceFolderText.Text = dlg.SelectedPath;
                }
            }
        }

        private void BrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.SelectedPath = OutputFolderText.Text;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    OutputFolderText.Text = dlg.SelectedPath;
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            CurrentSettings.SourceFolder = SourceFolderText.Text;
            CurrentSettings.OutputFolder = OutputFolderText.Text;
            CurrentSettings.NameSpace = string.IsNullOrWhiteSpace(NamespaceText.Text) ? "Generated" : NamespaceText.Text;
            CurrentSettings.ReplaceClassTextFrom = string.IsNullOrWhiteSpace(ReplaceFromText.Text) ? "infra" : ReplaceFromText.Text;
            CurrentSettings.ReplaceClassTextTo = string.IsNullOrWhiteSpace(ReplaceToText.Text) ? "grpc" : ReplaceToText.Text;
            var solutionNameKey = SettingsStore.ResolveSolutionNameKey(_dte);
            SettingsStore.Save(CurrentSettings, solutionNameKey);
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
