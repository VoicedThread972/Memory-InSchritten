using Memory_InSchritten.UserControls;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Buffers.Binary;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Formats.Asn1.AsnWriter;

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

        private bool _covering;

        private bool silent;

        private int cardCount;

        private string? _Id;

        private static readonly string IdPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memory-InSchritten", "id.txt");

        private bool player1turn = true;

        private int p1Score;

        private int p2Score;

        private int p1Elo;

        private int p2Elo;

        private List<string> Open = [];

        private static List<Dialog> OpenDialogs = [];

        private readonly List<(int,int)> Moves = [];

        private static readonly Uri GameEndpoint = new(
            Environment.GetEnvironmentVariable("MEMORY_GAME_URL") ?? "wss://memory-server.francesco.kuberneteslv.dmz.becksche.de/game");

        private ClientWebSocket? _client;

        private readonly ImageBrush Covered = new(new BitmapImage(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "bilder", "starsolid.gif"))));

        private List<string> Cards = [];
        public MainWindow()
        {
            if (File.Exists(IdPath))
            {
                _Id = File.ReadAllText(IdPath);
            }
            else if (File.Exists("id.txt") || File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "id.txt")))
            {
                var oldIdPath = File.Exists("id.txt") ? "id.txt" : System.IO.Path.Combine(AppContext.BaseDirectory, "id.txt");
                _Id = File.ReadAllText(oldIdPath);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(IdPath)!);
                File.WriteAllText(IdPath, _Id);
            }
            InitializeComponent();
        }

        private async Task SendString(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            if (bytes.Length > 256) throw new InvalidDataException("String exceeds 256 bytes.");

            await SendInt(bytes.Length);
            if (bytes.Length > 0) await SendBytes(bytes);
        }

        private async Task<string> ReadString(bool allowIdle = false)
        {
            var length = await ReadInt(allowIdle);

            if (length == 0) return "";
            if (length < 0 || length > 256) throw new InvalidDataException("Invalid string length.");

            byte[] dataBytes = await ReadBytes(length);
            return Encoding.UTF8.GetString(dataBytes);
        }

        private async Task SendInt(int n)
        {
            byte[] bytes = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, n);
            await SendBytes(bytes);
        }

        private async Task<int> ReadInt(bool allowIdle = false)
        {
            byte[] dataBytes = await ReadBytes(sizeof(int), allowIdle);
            return BinaryPrimitives.ReadInt32LittleEndian(dataBytes);
        }

        private async Task SendBytes(byte[] data)
        {
            await _client!.SendAsync(data.AsMemory(), WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        private async Task<byte[]> ReadBytes(int expectedSize, bool allowIdle = false)
        {
            if (expectedSize is <= 0 or > 256) throw new InvalidDataException("Invalid frame length.");
            byte[] buffer = new byte[expectedSize];
            int totalRead = 0;

            while (true)
            {
                using var timeout = new CancellationTokenSource();
                if (!allowIdle || totalRead > 0) timeout.CancelAfter(TimeSpan.FromSeconds(30));
                var result = await _client!.ReceiveAsync(buffer.AsMemory(totalRead), timeout.Token);
                if (result.MessageType != WebSocketMessageType.Binary || result.Count == 0)
                    throw new IOException("WebSocket closed or sent a non-binary message.");
                totalRead += result.Count;
                if (result.EndOfMessage)
                {
                    if (totalRead != expectedSize) throw new InvalidDataException("Unexpected message size.");
                    return buffer;
                }
                if (totalRead == expectedSize) throw new InvalidDataException("Message exceeds expected size.");
            }
        }

        private async Task SendRowCol((int, int) rowCol)
        {
            await SendInt(rowCol.Item1);
            await SendInt(rowCol.Item2);
        }

        private async Task<(int,int)> ReadRowCol()
        {
            var row = await ReadInt();
            var column = await ReadInt();

            return (row, column);
        }

        private async Task<bool> HandleRequests()
        {
            var command = await ReadString(true);
            switch (command)
            {
                case "Read":
                    await SendString("ACK");
                    await ReadCard();
                    await SendString("OK");
                    return true;
                case "Send":
                    await SendString("ACK");
                    await SendCard();
                    await SendString("OK");
                    return true;
                case "Sync":
                    await SendString("ACK");
                    await Resync();
                    await SendString("OK");
                    return true;
                case "End":
                    await SendString("ACK");
                    await SendString("OK");
                    return false;
                default:
                    throw new InvalidDataException($"Unknown server command: {command}");
            }
        }

        private async Task Resync()
        {
            Player1.PlayerName.Text = await ReadString();
            var nameSet = !string.IsNullOrEmpty(Player1.PlayerName.Text);
            await SetName(nameSet);

            await SetElo();

            await SetCards();

            await Turn();

            await SetCardCount();

            await Shuffle();

            var count = await ReadInt();
            if (count < 0) throw new InvalidDataException("Invalid move history length.");
            silent = true;
            try
            {
                for (var i = 0; i < count; i++) await SendCard();
            }
            finally
            {
                silent = false;
            }
            p1Elo -= p1Score;
            p2Elo -= p2Score;
            Player1.Elo.Content = "ELO: " + (p1Elo + p1Score);
            Player2.Elo.Content = "ELO: " + (p2Elo + p2Score);
            Player1.CalcFontSize();
            Player2.CalcFontSize();
        }

        private async Task SetElo()
        {
            Player1.Elo.Content = "ELO: " + (p1Elo = await ReadInt());
            Player2.Elo.Content = "ELO: " + (p2Elo = await ReadInt());
            Player1.CalcFontSize();
            Player2.CalcFontSize();
        }

        private async Task ReadCard()
        {
            _allowMove = true;
            var lastKeepalive = DateTime.UtcNow;
            while (Moves.Count == 0)
            {
                if (_client!.State != WebSocketState.Open)
                    throw new IOException("Connection lost while waiting for a move.");
                if (DateTime.UtcNow - lastKeepalive >= TimeSpan.FromSeconds(10))
                {
                    await SendInt(-1);
                    lastKeepalive = DateTime.UtcNow;
                }
                await Task.Delay(100);
            }
            await SendRowCol(Moves[0]);
            Moves.RemoveAt(0);
        }

        private async Task SendCard()
        {
            (var row, var column) = await ReadRowCol();

            Button btn = Grid.Children.OfType<Button>().FirstOrDefault(b => (int)b.GetValue(Grid.RowProperty) == row && (int)b.GetValue(Grid.ColumnProperty) == column)!;
            if (btn is null || !btn.IsHitTestVisible) throw new InvalidDataException("Invalid card in move history.");
            while (Open.Count > 1) await Task.Delay(10);
            await RevealCard(btn);
        }

        private async Task StartClient()
        {
            if (!Online) return;

            while (Online)
            {
                try
                {
                    _ = ShowDialog("Verbindung wird aufgebaut...", true);
                    var candidate = new ClientWebSocket();
                    candidate.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                    try
                    {
                        await candidate.ConnectAsync(GameEndpoint, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
                        _client = candidate;
                    }
                    catch
                    {
                        candidate.Dispose();
                        throw;
                    }

                    await SendString(_Id ?? "");
                    if (string.IsNullOrWhiteSpace(_Id))
                    {
                        _Id = await ReadString();
                        if (string.IsNullOrWhiteSpace(_Id)) throw new InvalidDataException("Server sent an empty player ID.");
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(IdPath)!);
                        File.WriteAllText(IdPath, _Id);
                    }

                    await ShowDialog("Verbindung zum Server hergestellt!");
                    while (Online && await HandleRequests()) { }
                    if (Online) await Reset(false, false);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[StartClient] {e}");
                    if (!Online) return;
                    await Reset(true, false);
                    _ = ShowDialog("Verbindung unterbrochen. Neuer Versuch...", true);
                    await Task.Delay(1000);
                }
                finally
                {
                    _client?.Dispose();
                    _client = null;
                }
            }
        }

        private async Task Turn()
        {
            if (Online)
            {
                player1turn = await ReadInt() == 0;
            }
            else
            {
                player1turn = new Random().Next(2) == 0;
            }

            if (player1turn)
            {
                Player1.Rect.Fill = Brushes.DeepSkyBlue;
                Player2.Rect.Fill = Brushes.LightGray;
                await ShowDialog($"{Player1.PlayerName.Text} fängt an!");
            }
            else
            {
                Player1.Rect.Fill = Brushes.LightGray;
                Player2.Rect.Fill = Brushes.DeepSkyBlue;
                await ShowDialog($"{Player2.PlayerName.Text} fängt an!");
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
            if (msg.IsVisible) msg.Close();
            msg.Dispose();
            return msg.InputText ?? "";
        }

        private static async Task ShowDialog(string text, bool infinite = false)
        {
            foreach (var old in OpenDialogs)
            {
                old.Close();
                old.Dispose();
            }
            OpenDialogs = [];

            var msg = new Dialog
            {
                Text =
                {
                    Text = text
                }
            };
            msg.Show();
            if (!infinite)
            {
                await Task.Delay(1000);
                msg.Close();
                msg.Dispose();
            }
            else
            {
                OpenDialogs.Add(msg);
            }
        }

        private async Task<string> GetDefaultName()
        {
            if (!Online) return "Player 1";
            return await ReadString();
        }

        private async Task SetName(bool nameSet = false)
        {
            var defName = await GetDefaultName();
            if (!nameSet)
            {
                var p1Name = await ShowInputDialog(defName, "Spieler 1 Name");
                while (p1Name.Length is > 32 or 0)
                {
                    await ShowDialog("Ungültiger Name");
                    p1Name = await ShowInputDialog(defName, "Spieler 1 Name");
                }
                Player1.PlayerName.Text = p1Name;
            }

            if (Online)
            {
                await SendString(Player1.PlayerName.Text);
                _ = ShowDialog("Suche nach Gegner...", true);
                Player2.PlayerName.Text = await ReadString(true);
                await ShowDialog("Gegner gefunden!");
            }
            else
            {
                var p2Name = await ShowInputDialog("Player 2", "Spieler 2 Name");
                while (p2Name.Length is > 32 or 0)
                {
                    await ShowDialog("Ungültiger Name");
                    p2Name = await ShowInputDialog("Player 2", "Spieler 2 Name");
                }
                Player2.PlayerName.Text = p2Name;
            }
        }

        private async Task SetCards()
        {
            int choice;
            if (!Online || (choice = await ReadInt()) == -1)
            {
                var msg = new CustomMessageBox();
                if (msg.ShowDialog() == true)
                {
                    cardPath += msg.Result switch
                    {
                        0 => "comics",
                        1 => "harrypotter",
                        2 => "popstars",
                        _ => "markus"
                    };
                }
                else
                {
                    cardPath += "markus";
                }
                if (Online) await SendInt(msg.Result);
            }
            else
            {
                cardPath += choice switch
                {
                    0 => "comics",
                    1 => "harrypotter",
                    2 => "popstars",
                    _ => "markus",
                };
                await SendInt(choice);
            }
            LoadCards();
        }

        private async Task SetCardCount()
        {
            if (Cards.Count < 4 || Cards.Count > 256 || Cards.Count % 4 != 0)
                throw new InvalidDataException("The selected card set must contain a multiple of four cards.");
            await SendInt(Cards.Count);
        }

        private async Task Shuffle()
        {
            if (Online)
            {
                cardCount = await ReadInt(true);
                if (cardCount < 4 || cardCount > Cards.Count || cardCount % 4 != 0)
                    throw new InvalidDataException("Invalid negotiated card count.");
                Cards.RemoveRange(cardCount, Cards.Count - cardCount);
                for (var i = 0; i < cardCount; i++)
                {
                    var index = await ReadInt();
                    if (index < i || index >= cardCount) throw new InvalidDataException("Invalid shuffle index.");
                    (Cards[i], Cards[index]) = (Cards[index], Cards[i]);
                }
            }
            else
            {
                for (var i = 0; i < Cards.Count; i++)
                {
                    var index = new Random().Next(Cards.Count);
                    (Cards[i], Cards[index]) = (Cards[index], Cards[i]);
                }
            }

            PlaceCards();
        }

        private void LoadCards()
        {
            var index = 0;
            foreach (var file in Directory.GetFiles(cardPath).OrderBy(System.IO.Path.GetFileName, StringComparer.Ordinal))
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
            _covering = true;
            var currentCards = Cards;
            if (!silent)
            {
                await Task.Delay(1000);
            }
            if (!ReferenceEquals(currentCards, Cards)) return;

            player1turn = !player1turn;
            Player1.Rect.Fill = player1turn ? Brushes.DeepSkyBlue : Brushes.LightGray;
            Player2.Rect.Fill = player1turn ? Brushes.LightGray : Brushes.DeepSkyBlue;

            foreach (var child in Grid.Children)
            {
                if (child is not Button btn || !Open.Contains(btn.Content.ToString() ?? "")) continue;
                btn.Background = Covered;
                btn.IsHitTestVisible = true;
                btn.Focusable = true;
            }

            Open = [];
            _covering = false;
        }

        private void CardPair()
        {
            Open = [];
            if (!silent) _ = ShowDialog("Paar gefunden");

            var end = (player1turn ? ++p1Score + p2Score : ++p2Score + p1Score) >= Cards.Count / 2;

            Player1.Score.Content = p1Score;
            Player2.Score.Content = p2Score;
            Player1.Elo.Content = "ELO: " + (p1Elo + p1Score);
            Player2.Elo.Content = "ELO: " + (p2Elo + p2Score);
            Player1.CalcFontSize();
            Player2.CalcFontSize();
            if (end && !silent)
            {
                var result = $"Spiel beendet!{Environment.NewLine}{(p1Score > p2Score ? Player1.PlayerName.Text : p1Score < p2Score ? Player2.PlayerName.Text : "Niemand")} gewinnt";
                if (Online) _ = ShowDialog(result);
                else MessageBox.Show(result, "Memory", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private async void ShowCard(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (_covering || (Online && (!player1turn || !_allowMove))) return;

            await RevealCard(btn);
        }

        private async Task RevealCard(Button btn)
        {
            if (!btn.IsHitTestVisible) return;

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

            if (Online && player1turn && !silent)
            {
                int row = (int)btn.GetValue(Grid.RowProperty);
                int column = (int)btn.GetValue(Grid.ColumnProperty);

                Moves.Add((row, column));
            }

            if (pair) CardPair();
            else if(cover) await CoverCards();
        }

        private void GetOnline()
        {
            var msg = MessageBox.Show("Möchten Sie online spielen?", "Memory", MessageBoxButton.YesNo, MessageBoxImage.Question);
            Online = msg == MessageBoxResult.Yes;
        }

        private async Task Reset(bool reconnect=false, bool startClient=true)
        {
            Moves.Clear();
            _allowMove = false;
            _covering = false;
            _client?.Abort();
            _client?.Dispose();

            player1turn = true;
            Player1.Rect.Fill = Brushes.DeepSkyBlue;
            Player2.Rect.Fill = Brushes.LightGray;

            p1Score = 0;
            p2Score = 0;

            Player1.Score.Content = "0";
            Player2.Score.Content = "0";

            Player2.Elo.Content = "ELO: 0";
            Player2.PlayerName.Text = "";

            cardPath = System.IO.Path.Combine(AppContext.BaseDirectory, "bilder") + System.IO.Path.DirectorySeparatorChar;
            Cards = [];
            Open = [];

            foreach (var child in Grid.Children.OfType<Button>().ToList())
            {
                Grid.Children.Remove(child);
            }

            if (!reconnect) GetOnline();

            if (Online)
            {
                if (startClient) await StartClient();
            }
            else
            {
                await SetName();

                await Turn();

                await SetCards();

                await Shuffle();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _ = Reset();
        }
    }
}