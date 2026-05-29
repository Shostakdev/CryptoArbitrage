using System.Text.Json.Serialization;

namespace CryptoArbitrage.Api.Integrations.Dtos;

/// <summary>
/// Bybit websocket envelope that wraps a depth update for a symbol.
/// </summary>
public class BybitDepthResponse
{
    /// <summary>The message type, such as snapshot or delta.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>The symbol-specific depth payload.</summary>
    [JsonPropertyName("data")]
    public BybitData? Data { get; init; }
}

/// <summary>
/// The symbol-specific data contained in a Bybit depth update.
/// </summary>
public class BybitData
{
    /// <summary>The trading symbol that this update belongs to.</summary>
    [JsonPropertyName("s")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>The update id that increments with each depth change.</summary>
    [JsonPropertyName("u")]
    public long UpdateId { get; init; }

    /// <summary>The sequence number for the message ordering.</summary>
    [JsonPropertyName("seq")]
    public long Sequence { get; init; }

    /// <summary>Bid levels in the Bybit payload.</summary>
    [JsonPropertyName("b")]
    public List<List<string>> Bids { get; init; } = null!;

    /// <summary>Ask levels in the Bybit payload.</summary>
    [JsonPropertyName("a")]
    public List<List<string>> Asks { get; init; } = null!;
}
