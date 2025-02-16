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
    /// Interaktionslogik für Dialog.xaml
    /// </summary>
    public partial class Dialog : Window, IDisposable
    {
        public Dialog()
        {
            InitializeComponent();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        ~Dialog()
        {
            Dispose();
        }
    }
}
