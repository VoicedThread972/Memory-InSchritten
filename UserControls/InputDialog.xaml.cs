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
    /// Interaktionslogik für InputDialog.xaml
    /// </summary>
    public partial class InputDialog : Window, IDisposable
    {
        public string? Text
        {
            get => TitleText.Text;
            set => TitleText.Text = value;
        }

        private string? _default;
        public string? Default
        {
            get => _default;
            set
            {
                _default = value;
                Input.Text = value;
            }
        }
        public string? InputText { get; private set; }
        public bool IsAlive { get; private set; }
        public InputDialog()
        {
            IsAlive = true;
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            InputText = Input.Text;
            IsAlive = false;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        ~InputDialog()
        {
            Dispose();
        }
    }
}
