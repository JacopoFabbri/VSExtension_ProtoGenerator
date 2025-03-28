using System.Windows;

namespace CSharpConvertToProto.Windows
{
    public partial class SpecifyNamespaceWindow : Window
    {
        public string NameSpaceInput { get; set; }
        public SpecifyNamespaceWindow()
        {
            InitializeComponent();
        }
        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            NameSpaceInput = NamespaceTextBox.Text;
            DialogResult = true;
        }
    }
}
