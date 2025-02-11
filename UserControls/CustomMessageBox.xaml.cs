using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Memory_InSchritten.UserControls
{
    /// <summary>
    /// Interaktionslogik für CustomMessageBox.xaml
    /// </summary>
    public partial class CustomMessageBox : Window
    {
        public string Result { get; private set; }
        public CustomMessageBox()
        {
            InitializeComponent();
            Result = "";
        }

        private void Comics(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            Result = "Comics";
        }

        private void Harry_Potter(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            Result = "Harry Potter";
        }

        private void Popstars(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            Result = "Popstars";
        }
    }
}
