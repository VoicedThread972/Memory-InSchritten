using Memory_InSchritten.UserControls;
using Microsoft.VisualBasic;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        private string cardPath = "";

        private bool Online;

        private bool _allowMove;

        private bool player1turn = true;

        private List<string> Open = [];

        private const string ServerIp = "192.168.178.34";

        private const int GamePort = 51322;

        private TcpClient? _client;

        private readonly ImageBrush Covered = new(new BitmapImage(new Uri(Directory.GetCurrentDirectory() + @"\bilder\starsolid.gif")));

        private List<string> Cards = [];
        public MainWindow()
        {
            InitializeComponent();
        }

        private async Task SendString(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);

            await SendInt(bytes.Length);
            await SendBytes(bytes);
        }

        private async Task<string> ReadString()
        {
            int length = await ReadInt();

            if (length <= 0)
                return "";

            byte[] dataBytes = await ReadBytes(length);
            return Encoding.UTF8.GetString(dataBytes);
        }

        private async Task SendInt(int n)
        {
            byte[] bytes = BitConverter.GetBytes(n);
            await SendBytes(bytes);
        }

        private async Task<int> ReadInt()
        {
            byte[] dataBytes = await ReadBytes(sizeof(int));
            return BitConverter.ToInt32(dataBytes, 0);
        }

        private async Task SendBytes(byte[] Object)
        {
            try
            {
                NetworkStream stream = _client!.GetStream();

                await stream.FlushAsync();
                await stream.WriteAsync(Object);
                await stream.FlushAsync();
            }
            catch
            {
                MessageBox.Show("Die Verbindung wurde getrennt!", "Memory", MessageBoxButton.OK, MessageBoxImage.Error);
                Reset();
            }
        }

        private async Task<byte[]> ReadBytes(int expectedSize)
        {
            if (expectedSize is <=0 or >256) return [];
            try
            {
                NetworkStream stream = _client!.GetStream();
                byte[] buffer = new byte[expectedSize];
                int totalRead = 0;

                while (totalRead < expectedSize)
                {
                    int bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, expectedSize - totalRead));
                    if (bytesRead == 0)
                    {
                        MessageBox.Show("Verbindung verloren!", "Memory", MessageBoxButton.OK, MessageBoxImage.Error);
                        Reset();
                        return [];
                    }
                    totalRead += bytesRead;
                }
                return buffer;
            }
            catch
            {
                MessageBox.Show("Die Verbindung wurde getrennt!", "Memory", MessageBoxButton.OK, MessageBoxImage.Error);
                Reset();
                return [];
            }
        }

        private async Task SendRowCol(int row, int column)
        {
            await SendInt(row);
            await SendInt(column);
        }

        private async Task<(int,int)> ReadRowCol()
        {
            int row = await ReadInt();
            int column = await ReadInt();
            return (row, column);
        }

        private async Task StartClient(string serverIp, int port)
        {
            if (!Online) return;

            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(serverIp, port);

                ShowDialog("Verbindung zum Server hergestellt!");
            }
            catch
            {
                MessageBox.Show("Verbindung zum Server fehlgeschlagen!", "Memory", MessageBoxButton.OK, MessageBoxImage.Error);
                Online = false;
            }
        }

        private async Task DecideStart()
        {
            if (Online)
            {
                player1turn = await ReadInt() == 0;
            }
            else
            {
                player1turn = new Random().Next(1) == 0;
            }

            if (player1turn)
            {
                Player1.Rect.Fill = Brushes.DeepSkyBlue;
                Player2.Rect.Fill = Brushes.LightGray;
                ShowDialog($"{Player1.PlayerName.Text} fängt an!");
            }
            else
            {
                Player1.Rect.Fill = Brushes.LightGray;
                Player2.Rect.Fill = Brushes.DeepSkyBlue;
                ShowDialog($"{Player2.PlayerName.Text} fängt an!");
            }
        }

        private async Task WaitForOpponent()
        {
            if (player1turn || !Online) return;

            (var row, var column) = await ReadRowCol();

            _allowMove = true;

            Button btn = Grid.Children.OfType<Button>().FirstOrDefault(b => (int)b.GetValue(Grid.RowProperty) == row && (int)b.GetValue(Grid.ColumnProperty) == column)!;
            ShowCard(btn, new RoutedEventArgs());

            try
            {
                await WaitForOpponent();
            }
            catch
            {
                Reset();
            }
        }

        private static async Task<string> ShowInputDialog(string def, string text)
        {
            var msg = new InputDialog
            {
                Default = def,
                Text = text
            };
            msg.Show();
            while (msg.IsAlive)
            {
                await Task.Delay(500);
            }
            msg.Close();
            msg.Dispose();
            return msg.InputText ?? "";
        }

        private async static void ShowDialog(string text)
        {
            var msg = new Dialog
            {
                Text =
                {
                    Text = text
                }
            };
            msg.Show();
            await Task.Delay(1000);
            msg.Close();
            msg.Dispose();
        }

        private async Task SetName()
        {
            var p1Name = await ShowInputDialog("Player 1", "Spieler 1 Name");
            while (p1Name.Length is > 10 or 0)
            {
                ShowDialog("Ungültiger Name");
                p1Name = await ShowInputDialog("Player 1", "Spieler 1 Name");
            }

            Player1.PlayerName.Text = p1Name;

            if (!Online)
            {
                var p2Name = await ShowInputDialog("Player 2", "Spieler 2 Name");
                while (p2Name.Length is > 10 or 0)
                {
                    ShowDialog("Ungültiger Name");
                    p2Name = await ShowInputDialog("Player 2", "Spieler 2 Name");
                }
                Player2.PlayerName.Text = p2Name;
            }
            else
            {
                await SendString(Player1.PlayerName.Text);
                Player2.PlayerName.Text = await ReadString();
                ShowDialog("Gegner gefunden!");
            }
        }

        private void SetCards()
        {
            var msg = new CustomMessageBox();
            if (msg.ShowDialog() == true)
            {
                cardPath += msg.Result switch
                {
                    "Comics" => "comics",
                    "Harry Potter" => "harrypotter",
                    "Popstars" => "popstars",
                    _ => "markus"
                };
            }
            else cardPath += "markus";
        }

        private async Task Shuffle()
        {
            if (!Online)
            {
                for (var i = 0; i < Cards.Count; i++)
                {
                    var index = new Random().Next(Cards.Count);
                    (Cards[i], Cards[index]) = (Cards[index], Cards[i]);
                }
            }
            else
            {
                await SendInt(Cards.Count);
                var cardCount = await ReadInt();
                for (var i = 0; i < cardCount; i++)
                {
                    var index = await ReadInt();
                    (Cards[i], Cards[index]) = (Cards[index], Cards[i]);
                }
            }
        }

        private void LoadCards()
        {
            var index = 0;
            foreach (var file in Directory.GetFiles(cardPath))
            {
                if (++index > 10) break;
                Cards.Add(file);
                Cards.Add(file);
            }
        }

        private void PlaceCards()
        {
            var index = -1;
            for (var i = 1; i < Grid.ColumnDefinitions.Count - 1; i++)
            {
                for (var j = 0; j < Grid.RowDefinitions.Count; j++)
                {
                    if (++index >= Cards.Count) return;

                    var btn = new Button
                    {
                        Content = Cards[index],
                        Background = Covered,
                        Foreground = Brushes.Transparent,
                        BorderBrush = Brushes.Black,
                        BorderThickness = new Thickness(1),
                    };

                    btn.SetValue(Grid.ColumnProperty, i);
                    btn.SetValue(Grid.RowProperty, j);

                    btn.Click += ShowCard;

                    Grid.Children.Add(btn);
                }
            }
        }

        private async Task CoverCards()
        {
            MessageBox.Show("Die Karten werden gedeckt", "Memory", MessageBoxButton.OK, MessageBoxImage.Information);
            
            foreach (var child in Grid.Children)
            {
                if (child is Button btn && Open.Contains(btn.Content.ToString() ?? ""))
                {
                    btn.Background = Covered;
                    btn.IsHitTestVisible = true;
                    btn.Focusable = true;
                }
            }
            player1turn = !player1turn;
            Player1.Rect.Fill = player1turn ? Brushes.DeepSkyBlue : Brushes.LightGray;
            Player2.Rect.Fill = player1turn ? Brushes.LightGray : Brushes.DeepSkyBlue;

            Open = [];

            if (!player1turn) await WaitForOpponent();
        }

        private void CardPair()
        {
            Open = [];
            MessageBox.Show("Paar gefunden", "Memory", MessageBoxButton.OK, MessageBoxImage.Information);

            if (int.TryParse(Player1.Score.Content.ToString() ?? "", out var score1) && int.TryParse(Player2.Score.Content.ToString() ?? "", out var score2))
            {
                if ((player1turn ? ++score1 + score2 : ++score2 + score1) >= Cards.Count / 2)
                {
                    Player1.Score.Content = score1;
                    Player2.Score.Content = score2;
                    MessageBox.Show($"Spiel beendet!{Environment.NewLine}{(score1 > score2 ? Player1.PlayerName.Text : score1 < score2 ? Player2.PlayerName.Text : "Niemand")} gewinnt");
                    Reset();
                    return;
                }
                Player1.Score.Content = score1;
                Player2.Score.Content = score2;
            }
        }

        private async void ShowCard(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            if (!_allowMove && Online && !player1turn) return;

            _allowMove = false;
            bool pair = false;
            bool cover = false;
            Open.Add(btn.Content.ToString() ?? "");
            btn.Background = new ImageBrush(new BitmapImage(new Uri(btn.Content.ToString() ?? "")));
            btn.IsHitTestVisible = false;
            btn.Focusable = false;

            if (Open.Count >= 2)
            {
                if (Open[0] == Open[1]) pair = true;
                else cover = true;
            }

            if (Online && player1turn)
            {
                int row = (int)btn.GetValue(Grid.RowProperty);
                int column = (int)btn.GetValue(Grid.ColumnProperty);

                await SendRowCol(row, column);
            }

            if (pair) CardPair();
            else if(cover) await CoverCards();
        }

        private void GetOnline()
        {
            var msg = MessageBox.Show("Möchten Sie online spielen?", "Memory", MessageBoxButton.YesNo, MessageBoxImage.Question);
            Online = msg == MessageBoxResult.Yes;
        }

        private async void Reset()
        {
            _client?.Close();

            player1turn = true;
            Player1.Rect.Fill = Brushes.DeepSkyBlue;
            Player2.Rect.Fill = Brushes.LightGray;

            Player1.Score.Content = "0";
            Player2.Score.Content = "0";

            cardPath = Directory.GetCurrentDirectory() + @"\bilder\";
            Cards = [];
            Open = [];

            foreach (var child in Grid.Children.OfType<Button>().ToList())
            {
                Grid.Children.Remove(child);
            }

            GetOnline();

            await StartClient(ServerIp, GamePort);

            SetCards();

            LoadCards();

            await SetName();

            await DecideStart();

            await Shuffle();

            PlaceCards();

            await WaitForOpponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Reset();
        }
    }
}