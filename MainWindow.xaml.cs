using System.ComponentModel;
using System.Windows;
using MemoryInSchritten.Client.Cards;
using MemoryInSchritten.Client.Game;
using MemoryInSchritten.Client.Protocol;
using MemoryInSchritten.Client.Settings;
using MemoryInSchritten.Client.ViewModels;
using MemoryInSchritten.Client.Views;

namespace MemoryInSchritten.Client;

/// <summary>Drives the menu → game → result loop; all game state lives in <see cref="GameViewModel"/>.</summary>
public partial class MainWindow : Window
{
    private const string AppTitle = "Memory";
    private const int MaxLocalPairs = 10;

    private static readonly Uri GameEndpoint = new(
        Environment.GetEnvironmentVariable("MEMORY_GAME_URL") ?? "wss://memory-server.francesco.kuberneteslv.dmz.becksche.de/game");

    private readonly PlayerSettings settings = PlayerSettings.Load();
    private readonly GameViewModel game = new();
    private IGameSession? session;
    private bool closing;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = game;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await RunAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        if (!closing) Close();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        closing = true;
        if (session is not { } running) return;

        // Keep the window until the socket is closed cleanly, so the server registers a deliberate leave.
        e.Cancel = true;
        session = null;
        await running.DisposeAsync();
        Close();
    }

    private async void Card_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CardViewModel card }) await game.FlipAsync(card);
    }

    private async Task RunAsync()
    {
        while (!closing)
        {
            var mode = MessageBox.Show(this, "Möchten Sie online spielen?", AppTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (mode == MessageBoxResult.Cancel) return;

            var create = mode == MessageBoxResult.Yes ? PrepareOnline() : PrepareLocal();
            var deck = CardDeck.Load(CardSetDialog.Choose(this));
            var current = create(deck);
            session = current;
            game.Start(current, deck);

            var outcome = await PlayAsync(current);
            if (closing) return;

            var again = MessageBox.Show(this, $"{outcome}{Environment.NewLine}{Environment.NewLine}Noch eine Runde?", AppTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (again != MessageBoxResult.Yes) return;
        }
    }

    private Func<CardDeck, IGameSession> PrepareOnline()
    {
        settings.Name = NameDialog.Ask(this, "Dein Name", settings.Name);
        settings.TrySave();
        return _ => new OnlineSession(GameEndpoint, settings.PlayerId, settings.Name);
    }

    private Func<CardDeck, IGameSession> PrepareLocal()
    {
        var first = NameDialog.Ask(this, "Name von Spieler 1", "Spieler 1");
        var second = NameDialog.Ask(this, "Name von Spieler 2", "Spieler 2");
        return deck => new LocalSession(first, second, Math.Min(deck.PairCount, MaxLocalPairs));
    }

    private async Task<string> PlayAsync(IGameSession current)
    {
        try
        {
            await foreach (var message in current.Events.ReadAllAsync())
            {
                if (message is WelcomeMessage welcome)
                {
                    settings.PlayerId = welcome.PlayerId;
                    settings.Name = welcome.Name;
                    settings.TrySave();
                }
                game.Apply(message);
            }
            return game.Outcome ?? "Das Spiel wurde beendet.";
        }
        catch (SessionEndedException ended)
        {
            return ended.Message;
        }
        catch (Exception exception) when (!closing)
        {
            return $"Verbindungsfehler: {exception.Message}";
        }
        finally
        {
            if (session == current)
            {
                session = null;
                await current.DisposeAsync();
            }
        }
    }
}
