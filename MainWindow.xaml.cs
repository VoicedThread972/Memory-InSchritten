using Microsoft.VisualBasic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Memory_InSchritten
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public void SetNames()
        {
            var p1name = Interaction.InputBox("Spieler 1 Name", "Memory", "Player 1");
            while (p1name.Length is > 10 or 0)
            {
                MessageBox.Show("Ungültiger Name", "Memory", MessageBoxButton.OK, MessageBoxImage.Error);
                p1name = Interaction.InputBox("Spieler 1 Name", "Memory", "Player 1");
            }

            var p2name = Interaction.InputBox("Spieler 2 Name", "Memory", "Player 2");
            while (p2name.Length is > 10 or 0)
            {
                MessageBox.Show("Ungültiger Name", "Memory", MessageBoxButton.OK, MessageBoxImage.Error);
                p2name = Interaction.InputBox("Spieler 2 Name", "Memory", "Player 1");
            }

            Player1.PlayerName.Text = p1name;
            Player2.PlayerName.Text = p2name;
        }

        private void Reset()
        {
            Player1.Score.Content = "0";
            Player2.Score.Content = "0";

            SetNames();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Reset();
        }
    }
}