using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using MemoryInSchritten.Client.Protocol;

namespace MemoryInSchritten.Client.Game;

/// <summary>
/// Server-backed match. Owns the socket lifecycle: reconnects with backoff and resumes the running game,
/// so callers only ever see the event stream.
/// </summary>
public sealed class OnlineSession : IGameSession
{
    private const int MaxMessageBytes = 64 * 1024;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan InitialGiveUp = TimeSpan.FromSeconds(15);
    // Slightly longer than the server's reconnect grace, so a returning player can still claim the game.
    private static readonly TimeSpan ReconnectGiveUp = TimeSpan.FromSeconds(75);

    private readonly Uri endpoint;
    private readonly string name;
    private readonly Channel<ServerMessage> events = Channel.CreateUnbounded<ServerMessage>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly Task run;
    private volatile ClientWebSocket? socket;
    private volatile bool leaving;
    private int disposed;
    private string? playerId;
    private bool inGame;
    private bool everConnected;
    private int attempt;
    private DateTime? lostSince;

    public OnlineSession(Uri endpoint, string? playerId, string name)
    {
        this.endpoint = endpoint;
        this.playerId = playerId;
        this.name = name;
        run = Task.Run(RunAsync);
    }

    public bool IsRated => true;
    public int Seat { get; private set; } = -1;
    public ChannelReader<ServerMessage> Events => events.Reader;

    public bool ControlsSeat(int seat) => seat == Seat;

    public async Task<bool> FlipAsync(int index)
    {
        try
        {
            return await SendAsync(new FlipMessage(index));
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        leaving = true;
        // A clean close tells the server the player left on purpose (immediate forfeit instead of reconnect grace).
        if (socket is { State: WebSocketState.Open } open && await sendGate.WaitAsync(CloseTimeout))
        {
            try
            {
                await open.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Leaving", CancellationToken.None);
            }
            catch (WebSocketException)
            {
            }
            finally
            {
                sendGate.Release();
            }

            try
            {
                await run.WaitAsync(CloseTimeout);
            }
            catch (TimeoutException)
            {
            }
        }

        stop.Cancel();
        await run;
        stop.Dispose();
        sendGate.Dispose();
    }

    private async Task RunAsync()
    {
        try
        {
            while (!await RunConnectionAsync() && !leaving)
            {
                lostSince ??= DateTime.UtcNow;
                if (DateTime.UtcNow - lostSince > (everConnected ? ReconnectGiveUp : InitialGiveUp))
                    throw new SessionEndedException("Der Server ist nicht erreichbar.");

                events.Writer.TryWrite(new ReconnectingMessage());
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(10, 1 << Math.Min(attempt++, 4))), stop.Token);
            }
            events.Writer.TryComplete();
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            events.Writer.TryComplete();
        }
        catch (Exception exception)
        {
            events.Writer.TryComplete(exception);
        }
    }

    /// <returns>True when the session is over (game finished or left); false when the connection was lost.</returns>
    private async Task<bool> RunConnectionAsync()
    {
        using var connection = new ClientWebSocket();
        connection.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        connection.Options.KeepAliveTimeout = TimeSpan.FromSeconds(15);
        var finished = false;

        try
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
            {
                timeout.CancelAfter(ConnectTimeout);
                await connection.ConnectAsync(endpoint, timeout.Token);
            }

            socket = connection;
            await SendAsync(new HelloMessage(ProtocolInfo.Version, playerId, name, Resume: inGame));

            var buffer = new ArrayBufferWriter<byte>(1024);
            while (await ReceiveAsync(connection, buffer) is { } message)
            {
                finished |= Track(message);
                events.Writer.TryWrite(message);
            }
        }
        catch (Exception exception) when (exception is WebSocketException
                                          || (exception is OperationCanceledException && !stop.IsCancellationRequested))
        {
            return finished;
        }
        finally
        {
            socket = null;
        }

        if (connection.State == WebSocketState.CloseReceived)
        {
            try
            {
                await connection.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch (WebSocketException)
            {
            }
        }

        if (finished || leaving) return true;
        if (connection.CloseStatus is WebSocketCloseStatus.PolicyViolation or WebSocketCloseStatus.InvalidPayloadData
            or WebSocketCloseStatus.MessageTooBig or WebSocketCloseStatus.InvalidMessageType)
        {
            throw new SessionEndedException(connection.CloseStatusDescription is { Length: > 0 } reason
                ? $"Verbindung vom Server beendet: {reason}"
                : "Verbindung vom Server beendet.");
        }
        return false;
    }

    /// <returns>True if the message ends the session.</returns>
    private bool Track(ServerMessage message)
    {
        switch (message)
        {
            case WelcomeMessage welcome:
                playerId = welcome.PlayerId;
                everConnected = true;
                attempt = 0;
                lostSince = null;
                return false;
            case SnapshotMessage snapshot:
                inGame = true;
                Seat = snapshot.Seat;
                return false;
            case GameOverMessage:
                inGame = false;
                return true;
            case ErrorMessage { Code: ErrorCode.GameNotFound or ErrorCode.UnsupportedProtocol }:
                return true;
            default:
                return false;
        }
    }

    private async Task<bool> SendAsync(ClientMessage message)
    {
        if (socket is not { State: WebSocketState.Open } current) return false;

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, ProtocolJson.Default.ClientMessage);
        await sendGate.WaitAsync(stop.Token);
        try
        {
            await current.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, stop.Token);
            return true;
        }
        finally
        {
            sendGate.Release();
        }
    }

    private async Task<ServerMessage?> ReceiveAsync(ClientWebSocket connection, ArrayBufferWriter<byte> buffer)
    {
        buffer.ResetWrittenCount();
        while (true)
        {
            var result = await connection.ReceiveAsync(buffer.GetMemory(4096), stop.Token);
            if (result.MessageType == WebSocketMessageType.Close) return null;

            buffer.Advance(result.Count);
            if (buffer.WrittenCount > MaxMessageBytes) throw new SessionEndedException("Nachricht vom Server ist zu groß.");
            if (result.EndOfMessage) break;
        }

        return JsonSerializer.Deserialize(buffer.WrittenSpan, ProtocolJson.Default.ServerMessage)
               ?? throw new SessionEndedException("Leere Nachricht vom Server.");
    }
}
