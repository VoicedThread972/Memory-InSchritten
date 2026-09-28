using System.Threading.Channels;
using MemoryInSchritten.Client.Protocol;

namespace MemoryInSchritten.Client.Game;

/// <summary>A running match. Online and local play produce the same server-shaped event stream.</summary>
public interface IGameSession : IAsyncDisposable
{
    bool IsRated { get; }

    /// <summary>Whether this client may flip cards when <paramref name="seat"/> has the turn.</summary>
    bool ControlsSeat(int seat);

    /// <summary>Completes after game over; faults if the session ended abnormally.</summary>
    ChannelReader<ServerMessage> Events { get; }

    /// <returns>False if the move could not be sent.</returns>
    Task<bool> FlipAsync(int index);
}

/// <summary>Client-local event: the connection dropped and is being re-established.</summary>
public sealed record ReconnectingMessage : ServerMessage;

public sealed class SessionEndedException(string message) : Exception(message);
