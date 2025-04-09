using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CSharpConvertToProto
{
    /// <summary>
    /// Logica di interazione per ClassSelectionWindow.xaml
    /// </summary>
    public partial class CustomizationWindow : Window
    {
        public string CustomizationValue;
        public string ToRemoveValue;

        public CustomizationWindow()
        {
            InitializeComponent();
        }

        private void OnSelectClick(object sender, RoutedEventArgs e)
        {
            CustomizationValue = Customization.Text;
            ToRemoveValue = ToRemove.Text;
            DialogResult = true;
        }
    }
}