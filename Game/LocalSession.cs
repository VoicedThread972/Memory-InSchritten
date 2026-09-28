using System.Security.Cryptography;
using System.Threading.Channels;
using MemoryInSchritten.Client.Protocol;

namespace MemoryInSchritten.Client.Game;

/// <summary>Hot-seat game on one machine; applies the same rules as the server.</summary>
public sealed class LocalSession : IGameSession
{
    private readonly Channel<ServerMessage> events = Channel.CreateUnbounded<ServerMessage>(new UnboundedChannelOptions { SingleReader = true });
    private readonly int[] pairIds;
    private readonly bool[] matched;
    private readonly int[] scores = new int[2];
    private int? open;
    private int turn;
    private int matchedCount;

    public LocalSession(string firstName, string secondName, int pairCount)
    {
        pairIds = new int[pairCount * 2];
        for (var index = 0; index < pairIds.Length; index++) pairIds[index] = index / 2;
        RandomNumberGenerator.Shuffle(pairIds.AsSpan());
        matched = new bool[pairIds.Length];
        turn = RandomNumberGenerator.GetInt32(2);

        events.Writer.TryWrite(new SnapshotMessage(
            Seat: 0,
            [new PlayerView(firstName, 0, 0, true), new PlayerView(secondName, 0, 0, true)],
            pairIds.Length, turn, [], null));
    }

    public bool IsRated => false;
    public ChannelReader<ServerMessage> Events => events.Reader;

    public bool ControlsSeat(int seat) => true;

    public Task<bool> FlipAsync(int index)
    {
        if ((uint)index >= (uint)pairIds.Length || matched[index] || index == open)
        {
            events.Writer.TryWrite(new ErrorMessage(ErrorCode.InvalidCard));
            return Task.FromResult(true);
        }

        if (open is not { } first)
        {
            open = index;
        }
        else
        {
            open = null;
            if (pairIds[first] == pairIds[index])
            {
                matched[first] = matched[index] = true;
                matchedCount += 2;
                scores[turn]++;
            }
            else
            {
                turn = 1 - turn;
            }
        }

        events.Writer.TryWrite(new FlippedMessage(index, pairIds[index], turn, [.. scores]));
        if (matchedCount == pairIds.Length)
        {
            var winner = scores[0].CompareTo(scores[1]) switch { > 0 => 0, < 0 => 1, _ => (int?)null };
            events.Writer.TryWrite(new GameOverMessage(GameOverReason.Completed, winner, [.. scores], [0, 0]));
            events.Writer.TryComplete();
        }
        return Task.FromResult(true);
    }

    public ValueTask DisposeAsync()
    {
        events.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
