using System.Text.Json.Serialization;

namespace CryptoArbitrage.Api.Integrations.Dtos;

/// <summary>
/// Gate.io websocket envelope containing the event name and depth result.
/// </summary>
public class GateIoDepthResponse
{
    /// <summary>The event name emitted by Gate.io.</summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    /// <summary>The depth result payload for the event.</summary>
    [JsonPropertyName("result")]
    public GateIoResult? Result { get; init; }
}

/// <summary>
/// The symbol-specific order book payload returned by Gate.io.
/// </summary>
public class GateIoResult
{
    /// <summary>The symbol that the book belongs to.</summary>
    [JsonPropertyName("s")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Bid levels provided by Gate.io.</summary>
    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; init; } = new();

    /// <summary>Ask levels provided by Gate.io.</summary>
    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; init; } = new();
}