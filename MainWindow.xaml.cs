﻿using Memory_InSchritten.UserControls;
using Microsoft.VisualBasic;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
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

        private bool isHost;

        private bool _allowMove;

        private bool player1turn = true;

        private List<string> Open = [];

        private const int GamePort = 5000;

        private TcpClient? _client;

        private TcpListener? _listener;

        private ImageBrush Covered = new(new BitmapImage(new Uri(Directory.GetCurrentDirectory() + @"\bilder\starsolid.gif")));

        private List<string> Cards = [];
        public MainWindow()
        {
            InitializeComponent();
        }

        private async Task Handshake()
        {
            await SendString("READY");
            var msg = await ReadString();
            if (!msg.Equals("READY"))
            {
                MessageBox.Show("Verbindung Fehlgeschlagen", "Memory", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new NotImplementedException();
            }
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
            //MessageBox.Show(Encoding.UTF8.GetString(dataBytes));
            return Encoding.UTF8.GetString(dataBytes);
        }

        private async Task SendInt(int n)
        {
            byte[] bytes = BitConverter.GetBytes(n);
            await SendBytes(bytes);
        }

        private async Task<int> ReadInt()
        {
            return BitConverter.ToInt32(await ReadBytes(sizeof(int)), 0);
        }

        private async Task SendBytes(byte[] Object)
        {
            try
            {
                NetworkStream stream = _client.GetStream();

                await stream.FlushAsync();
                await stream.WriteAsync(Object, 0, Object.Length);
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
            try
            {
                NetworkStream stream = _client.GetStream();
                byte[] buffer = new byte[expectedSize];
                int totalRead = 0;

                while (totalRead < expectedSize)
                {
                    int bytesRead = await stream.ReadAsync(buffer, totalRead, expectedSize - totalRead);
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

        static bool IsTcpPortInUse(int port)
        {
            bool isInUse = false;

            try
            {
                TcpListener tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();
                tcpListener.Stop();
            }
            catch (SocketException)
            {
                isInUse = true;
            }

            return isInUse;
        }

        static async Task<bool> IsPortOpen(string ip, int port)
        {
            using (TcpClient client = new TcpClient())
            {
                try
                {
                    var connectTask = client.ConnectAsync(ip, port);
                    if (await Task.WhenAny(connectTask, Task.Delay(500)) == connectTask)
                    {
                        return client.Connected;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }

        static string? GetLocalBaseIp()
        {
            string? localIp = GetLocalIp();
            if (string.IsNullOrEmpty(localIp)) return null;

            string[] parts = localIp.Split('.');
            return $"{parts[0]}.{parts[1]}.{parts[2]}";
        }
        static string? GetLocalIp()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 80);
                    return ((IPEndPoint?)socket.LocalEndPoint)?.Address.ToString();
                }
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> FindIp(int port)
        {
            string? baseIp = GetLocalBaseIp();
            if (baseIp == null)
            {
                return "127.0.0.1";
            }

            Console.WriteLine($"Scanning {baseIp}.X for devices with port {port} open...");

            while (true)
            {
                for (int i = 1; i < 255; i++)
                {
                    string ip = $"{baseIp}.{i}";
                    bool isOpen = await IsPortOpen(ip, port);

                    if (isOpen)
                    {
                       return ip;
                    }
                }
            }
        }

        private async Task SearchOpponent()
        {
            isHost = !IsTcpPortInUse(GamePort);

            if (isHost)
            {
                int timeout = 1000;
                var task = FindIp(GamePort);
                if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
                {
                    await StartClient(await task, GamePort);
                }
                else await StartServer(GamePort);
            }
            else
            {
                var opponentIp = "127.0.0.1";
                await StartClient(opponentIp, GamePort);
            }
        }


        private async Task StartServer(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            var searchingDialog = new SearchingDialog
            {
                Text =
                {
                    Text = "Suche nach Gegner im lokalen Netzwerk..."
                }
            };
            searchingDialog.Show();

            _client = await _listener.AcceptTcpClientAsync();
            searchingDialog.Text.Text = "Gegner verbunden!";
            await Task.Delay(1000);
            searchingDialog.Close();

            await SendString(Player1.PlayerName.Text);
            Player2.PlayerName.Text = await ReadString();

            await DecideStart();
        }


        private async Task StartClient(string serverIp, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(serverIp, port);

            var searchingDialog = new SearchingDialog
            {
                Text =
                {
                    Text = "Verbindung zum Gegner hergestellt!"
                }
            };
            searchingDialog.Show();
            await Task.Delay(1000);
            searchingDialog.Close();

            await SendString(Player1.PlayerName.Text);
            Player2.PlayerName.Text = await ReadString();

            await DecideStart();
        }

        private async Task DecideStart()
        {
            Random rand = new Random();

            int myNumber = rand.Next(1, 100);
            await SendInt(myNumber);

            int opponentNumber = await ReadInt();


            int sum = myNumber + opponentNumber;
            if ((sum % 2 == 1) != isHost)
            {
                player1turn = true;
                Player1.Rect.Fill = Brushes.DeepSkyBlue;
                Player2.Rect.Fill = Brushes.LightGray;
                MessageBox.Show($"{Player1.PlayerName.Text} fängt an!");
            }
            else
            {
                player1turn = false;
                Player1.Rect.Fill = Brushes.LightGray;
                Player2.Rect.Fill = Brushes.DeepSkyBlue;
                MessageBox.Show($"{Player2.PlayerName.Text} fängt an!");
            }
        }

        private async Task WaitForOpponent()
        {
            if (player1turn) return;

            await Handshake();
            int row = await ReadInt();
            int column = await ReadInt();
            _allowMove = true;

            Button btn = Grid.Children.OfType<Button>().FirstOrDefault(b => (int)b.GetValue(Grid.RowProperty) == row && (int)b.GetValue(Grid.ColumnProperty) == column)!;
            ShowCard(btn, new RoutedEventArgs());

            await WaitForOpponent();
        }

        private async Task SetNames()
        {
            var p1name = Interaction.InputBox("Spieler 1 Name", "Memory", "Player 1");
            while (p1name.Length is > 10 or 0)
            {
                MessageBox.Show("Ungültiger Name", "Memory", MessageBoxButton.OK, MessageBoxImage.Error);
                p1name = Interaction.InputBox("Spieler 1 Name", "Memory", "Player 1");
            }

            Player1.PlayerName.Text = p1name;

            if (Online) await SearchOpponent();
            else
            {
                var p2name = Interaction.InputBox("Spieler 2 Name", "Memory", "Player 2");
                while (p2name.Length is > 10 or 0)
                {
                    MessageBox.Show("Ungültiger Name", "Memory", MessageBoxButton.OK, MessageBoxImage.Error);
                    p2name = Interaction.InputBox("Spieler 2 Name", "Memory", "Player 1");
                }
                Player2.PlayerName.Text = p2name;
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
                var rnd = new Random();
                for (var i = 0; i < Cards.Count; i++)
                {
                    var index = rnd.Next(Cards.Count);
                    (Cards[i], Cards[index]) = (Cards[index], Cards[i]);
                }
            }
            else
            {
                await Handshake();
                if (isHost)
                {
                    List<string> clientCards = [];
                    for (int i = 0; i < 20; i++)
                    {
                        clientCards.Add(await ReadString());
                    }

                    var rnd = new Random();
                    for (var i = 0; i < Cards.Count; i++)
                    {
                        var index = rnd.Next(Cards.Count);
                        (Cards[i], Cards[index]) = (Cards[index], Cards[i]);
                        (clientCards[i], clientCards[index]) = (clientCards[index], clientCards[i]);
                    }

                    foreach (var card in clientCards)
                    {
                        await SendString(card);
                    }
                }
                else
                {
                    foreach (var card in Cards)
                    {
                        await SendString(card);
                    }

                    Cards = [];
                    for (int i = 0; i < 20; i++)
                    {
                        Cards.Add(await ReadString());
                    }
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
            Button? btn = sender as Button;
            if (btn is null) return;

            if (Online)
            {
                if (player1turn)
                {
                    int row = (int)btn.GetValue(Grid.RowProperty);
                    int column = (int)btn.GetValue(Grid.ColumnProperty);

                    await Handshake();
                    await SendInt(row);
                    await SendInt(column);
                }
                else if (!_allowMove) return;
            }

            _allowMove = false;
            Open.Add(btn.Content.ToString() ?? "");
            btn.Background = new ImageBrush(new BitmapImage(new Uri(btn.Content.ToString() ?? "")));
            btn.IsHitTestVisible = false;
            btn.Focusable = false;
            if (Open.Count >= 2)
            {
                if (Open[0] == Open[1]) CardPair();
                else await CoverCards();
            }
        }

        private void GetOnline()
        {
            var msg = MessageBox.Show("Möchten Sie online spielen?", "Memory", MessageBoxButton.YesNo, MessageBoxImage.Question);
            Online = msg == MessageBoxResult.Yes;
            if (!Online) throw new NotImplementedException();
        }

        private async void Reset()
        {
            _listener?.Stop();

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

            await SetNames();

            SetCards();

            LoadCards();

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