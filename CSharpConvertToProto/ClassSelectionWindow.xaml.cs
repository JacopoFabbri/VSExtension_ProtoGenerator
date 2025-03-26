using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CSharpConvertToProto
{
    /// <summary>
    /// Logica di interazione per ClassSelectionWindow.xaml
    /// </summary>
    public partial class ClassSelectionWindow : Window
    {
        public string SelectedClass { get; private set; }

        public ClassSelectionWindow(List<string> classNames)
        {
            InitializeComponent();
            ClassList.ItemsSource = classNames.OrderBy(x => x);
        }

        private void OnSelectClick(object sender, RoutedEventArgs e)
        {
            if (ClassList.SelectedItem != null)
            {
                SelectedClass = ClassList.SelectedItem.ToString();
                DialogResult = true;
            }
        }
    }
}
