using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using CSharpConvertToProto.Models.Enum;

namespace CSharpConvertToProto.Windows
{
    public partial class ServicesSelectorWindow : Window
    {
        public List<ServiceTOAddAtProtoEnum> SelectItems { get; set; }

        public ServicesSelectorWindow()
        {
            InitializeComponent();
            EnumListBox.ItemsSource = Enum.GetValues(typeof(ServiceTOAddAtProtoEnum)).Cast<ServiceTOAddAtProtoEnum>().Select(e => new EnumWrapper { Value = e, IsSelected = true }).ToList();
        }

        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (EnumWrapper item in EnumListBox.Items)
            {
                item.IsSelected = true;
            }
            EnumListBox.Items.Refresh();
        }

        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (EnumWrapper item in EnumListBox.Items)
            {
                item.IsSelected = false;
            }
            EnumListBox.Items.Refresh();
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            SelectItems = EnumListBox.Items.Cast<EnumWrapper>().Where(item => item.IsSelected).Select(item => item.Value).ToList();
            // Esegui l'azione desiderata con gli elementi selezionati
            DialogResult = true;
        }
    }

    public class EnumWrapper
    {
        public ServiceTOAddAtProtoEnum Value { get; set; }
        public bool IsSelected { get; set; }
    }
}
