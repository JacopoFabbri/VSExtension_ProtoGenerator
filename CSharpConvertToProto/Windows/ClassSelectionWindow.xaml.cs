using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using CSharpConvertToProto.Models.Enum;

namespace CSharpConvertToProto
{
    /// <summary>
    /// Logica di interazione per ClassSelectionWindow.xaml
    /// </summary>
    public partial class ClassSelectionWindow : Window
    {
        public string SelectedClass { get; private set; }
        private List<string> _classNames;
        private List<string> _filteredClassNames;

        public ClassSelectionWindow(List<string> classNames)
        {
            InitializeComponent();
            _classNames = classNames.OrderBy(x => x).ToList();
            _filteredClassNames = new List<string>(_classNames);
            ClassList.ItemsSource = _filteredClassNames;
        }

        private void OnSelectClick(object sender, RoutedEventArgs e)
        {
            if (ClassList.SelectedItem != null)
            {
                SelectedClass = ClassList.SelectedItem.ToString();
                DialogResult = true;
            }
        }

        private void OnSearchTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower();
            _filteredClassNames = _classNames.Where(className => className.ToLower().Contains(searchText)).ToList();
            ClassList.ItemsSource = _filteredClassNames;
        }
    }
}