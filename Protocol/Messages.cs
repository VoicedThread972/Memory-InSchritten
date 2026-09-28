using System.Text.Json;
using System.Text.Json.Serialization;

namespace MemoryInSchritten.Client.Protocol;

// Wire contract; mirrors the server's Protocol/Messages.cs. One WebSocket text message per record.
public static class ProtocolInfo
{
    public const int Version = 1;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(HelloMessage), "hello")]
[JsonDerivedType(typeof(FlipMessage), "flip")]
public abstract record ClientMessage;

/// <param name="Resume">True when reconnecting to a running game; the server then never queues for a new match.</param>
public sealed record HelloMessage(int Protocol, string? PlayerId, string? Name, bool Resume) : ClientMessage;

public sealed record FlipMessage(int Index) : ClientMessage;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WelcomeMessage), "welcome")]
[JsonDerivedType(typeof(QueuedMessage), "queued")]
[JsonDerivedType(typeof(SnapshotMessage), "snapshot")]
[JsonDerivedType(typeof(FlippedMessage), "flipped")]
[JsonDerivedType(typeof(PresenceMessage), "presence")]
[JsonDerivedType(typeof(GameOverMessage), "gameOver")]
[JsonDerivedType(typeof(ErrorMessage), "error")]
public abstract record ServerMessage;

public sealed record WelcomeMessage(string PlayerId, string Name, int Rating) : ServerMessage;

public sealed record QueuedMessage : ServerMessage;

/// <summary>Complete game state for the receiving seat; sent on game start and on every reconnect.</summary>
public sealed record SnapshotMessage(
    int Seat,
    IReadOnlyList<PlayerView> Players,
    int CardCount,
    int Turn,
    IReadOnlyList<CardView> Matched,
    CardView? Open) : ServerMessage;

/// <summary>A card was revealed. Turn and scores are the authoritative state after this flip.</summary>
public sealed record FlippedMessage(int Index, int PairId, int Turn, IReadOnlyList<int> Scores) : ServerMessage;

public sealed record PresenceMessage(int Seat, bool Connected) : ServerMessage;

public sealed record GameOverMessage(
    GameOverReason Reason,
    int? Winner,
    IReadOnlyList<int> Scores,
    IReadOnlyList<int> Ratings) : ServerMessage;

public sealed record ErrorMessage(ErrorCode Code) : ServerMessage;

public sealed record PlayerView(string Name, int Rating, int Score, bool Connected);

public sealed record CardView(int Index, int PairId);

[JsonConverter(typeof(CamelCaseEnumConverter<GameOverReason>))]
public enum GameOverReason { Completed, Forfeit, Abandoned }

[JsonConverter(typeof(CamelCaseEnumConverter<ErrorCode>))]
public enum ErrorCode { UnsupportedProtocol, UnexpectedMessage, NotInGame, NotYourTurn, InvalidCard, GameNotFound }

public sealed class CamelCaseEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.CamelCase)
    where TEnum : struct, Enum;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowOutOfOrderMetadataProperties = true)]
[JsonSerializable(typeof(ClientMessage))]
[JsonSerializable(typeof(ServerMessage))]
public sealed partial class ProtocolJson : JsonSerializerContext;
