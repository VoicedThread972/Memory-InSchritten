using System.Collections.ObjectModel;
using System.Windows.Threading;
using MemoryInSchritten.Client.Cards;
using MemoryInSchritten.Client.Game;
using MemoryInSchritten.Client.Protocol;

namespace MemoryInSchritten.Client.ViewModels;

/// <summary>Projects the session's event stream onto bindable state. Must be used on the UI thread.</summary>
public sealed class GameViewModel : ObservableObject
{
    private static readonly TimeSpan MismatchDisplay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(2);

    private readonly DispatcherTimer coverTimer = new() { Interval = MismatchDisplay };
    private readonly DispatcherTimer flashTimer = new() { Interval = FlashDuration };
    private IGameSession? session;
    private CardDeck? deck;
    private int?[] pairIds = [];
    private int seat;
    private int turn;
    private int? open;
    private (int First, int Second)? pendingCover;
    private bool flipPending;
    private bool reconnecting;
    private string status = "";

    public GameViewModel()
    {
        coverTimer.Tick += (_, _) => CoverPending();
        flashTimer.Tick += (_, _) =>
        {
            flashTimer.Stop();
            UpdateStatus();
        };
    }

    public PlayerViewModel Left { get; } = new();
    public PlayerViewModel Right { get; } = new();
    public ObservableCollection<CardViewModel> Cards { get; } = [];

    public string Status
    {
        get => status;
        private set => Set(ref status, value);
    }

    /// <summary>Final result text once the game is over.</summary>
    public string? Outcome { get; private set; }

    public void Start(IGameSession newSession, CardDeck newDeck)
    {
        session = newSession;
        deck = newDeck;
        Cards.Clear();
        pairIds = [];
        open = null;
        pendingCover = null;
        flipPending = false;
        reconnecting = false;
        Outcome = null;
        coverTimer.Stop();
        Left.Reset();
        Right.Reset();
        Status = "Verbindung wird aufgebaut …";
    }

    public void Apply(ServerMessage message)
    {
        switch (message)
        {
            case WelcomeMessage welcome:
                Left.Name = welcome.Name;
                Left.Rating = welcome.Rating;
                break;
            case QueuedMessage:
                Status = "Warte auf einen Gegner …";
                break;
            case SnapshotMessage snapshot:
                ApplySnapshot(snapshot);
                break;
            case FlippedMessage flipped:
                ApplyFlip(flipped);
                break;
            case PresenceMessage presence:
                PlayerAt(presence.Seat).IsConnected = presence.Connected;
                UpdateStatus();
                break;
            case GameOverMessage gameOver:
                ApplyGameOver(gameOver);
                break;
            case ErrorMessage error:
                flipPending = false;
                ApplyError(error.Code);
                break;
            case ReconnectingMessage:
                reconnecting = true;
                Status = "Verbindung unterbrochen – verbinde neu …";
                break;
        }
    }

    public async Task FlipAsync(CardViewModel card)
    {
        if (session is null || Outcome is not null || flipPending || reconnecting || !session.ControlsSeat(turn)) return;

        // Clicking while a mismatch is still shown skips the rest of the display time.
        CoverPending();
        if (card.IsFaceUp) return;

        flipPending = true;
        if (!await session.FlipAsync(card.Index)) flipPending = false;
    }

    private void ApplySnapshot(SnapshotMessage snapshot)
    {
        seat = snapshot.Seat;
        turn = snapshot.Turn;
        reconnecting = false;
        flipPending = false;
        pendingCover = null;
        coverTimer.Stop();

        if (Cards.Count != snapshot.CardCount)
        {
            Cards.Clear();
            for (var index = 0; index < snapshot.CardCount; index++) Cards.Add(new CardViewModel(index));
        }
        pairIds = new int?[snapshot.CardCount];
        foreach (var card in Cards)
        {
            card.Cover();
            card.IsMatched = false;
        }
        foreach (var matched in snapshot.Matched)
        {
            Reveal(matched.Index, matched.PairId);
            Cards[matched.Index].IsMatched = true;
        }
        open = snapshot.Open?.Index;
        if (snapshot.Open is { } openCard) Reveal(openCard.Index, openCard.PairId);

        for (var index = 0; index < snapshot.Players.Count; index++)
        {
            var view = snapshot.Players[index];
            var player = PlayerAt(index);
            player.Name = view.Name;
            player.Rating = session!.IsRated ? view.Rating : null;
            player.Score = view.Score;
            player.IsConnected = view.Connected;
        }
        UpdateStatus();
    }

    private void ApplyFlip(FlippedMessage flipped)
    {
        CoverPending();
        Reveal(flipped.Index, flipped.PairId);

        if (open is not { } first)
        {
            open = flipped.Index;
        }
        else
        {
            open = null;
            if (pairIds[first] == flipped.PairId)
            {
                Cards[first].IsMatched = Cards[flipped.Index].IsMatched = true;
                Flash("Paar gefunden!");
            }
            else
            {
                pendingCover = (first, flipped.Index);
                coverTimer.Start();
            }
        }

        PlayerAt(0).Score = flipped.Scores[0];
        PlayerAt(1).Score = flipped.Scores[1];
        turn = flipped.Turn;
        flipPending = false;
        UpdateStatus();
    }

    private void ApplyGameOver(GameOverMessage gameOver)
    {
        CoverPending();
        Left.IsActive = Right.IsActive = false;

        var winner = gameOver.Winner is { } winnerSeat ? PlayerAt(winnerSeat).Name : null;
        var text = gameOver.Reason switch
        {
            GameOverReason.Completed when winner is null => "Unentschieden!",
            GameOverReason.Completed => $"{winner} gewinnt!",
            GameOverReason.Forfeit => $"{winner} gewinnt – der Gegner hat aufgegeben.",
            _ => "Spiel abgebrochen – beide Spieler waren zu lange getrennt.",
        };

        if (session!.IsRated)
        {
            var me = PlayerAt(seat);
            var before = me.Rating ?? gameOver.Ratings[seat];
            var after = gameOver.Ratings[seat];
            text += $"{Environment.NewLine}Deine Elo: {after} ({after - before:+0;-0;±0})";
            for (var index = 0; index < gameOver.Ratings.Count; index++) PlayerAt(index).Rating = gameOver.Ratings[index];
        }

        PlayerAt(0).Score = gameOver.Scores[0];
        PlayerAt(1).Score = gameOver.Scores[1];
        Outcome = text;
        flashTimer.Stop();
        Status = text;
    }

    private void ApplyError(ErrorCode code)
    {
        switch (code)
        {
            case ErrorCode.GameNotFound:
                Outcome = "Das Spiel existiert nicht mehr – die Verbindung war zu lange unterbrochen.";
                Status = Outcome;
                break;
            case ErrorCode.UnsupportedProtocol:
                Outcome = "Diese Version ist veraltet. Bitte aktualisiere das Spiel.";
                Status = Outcome;
                break;
            default:
                Flash("Dieser Zug ist nicht möglich.");
                break;
        }
    }

    private void Reveal(int index, int pairId)
    {
        pairIds[index] = pairId;
        Cards[index].Reveal(deck!.Face(pairId));
    }

    private void CoverPending()
    {
        coverTimer.Stop();
        if (pendingCover is not { } pair) return;
        Cards[pair.First].Cover();
        Cards[pair.Second].Cover();
        pendingCover = null;
    }

    private PlayerViewModel PlayerAt(int playerSeat) => playerSeat == seat ? Left : Right;

    private void Flash(string text)
    {
        Status = text;
        flashTimer.Stop();
        flashTimer.Start();
    }

    private void UpdateStatus()
    {
        if (Outcome is not null || session is null || pairIds.Length == 0) return;

        var active = PlayerAt(turn);
        Left.IsActive = active == Left;
        Right.IsActive = active == Right;
        if (flashTimer.IsEnabled) return;

        Status = !Right.IsConnected && session.IsRated ? "Gegner getrennt – warte auf Wiederverbindung …"
            : session.IsRated && session.ControlsSeat(turn) ? "Du bist dran."
            : $"{active.Name} ist dran.";
    }
}
