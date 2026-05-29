using System.Text.Json.Serialization;

namespace CryptoArbitrage.Api.Integrations.Dtos;

/// <summary>
/// Binance websocket payload containing the stream identifier and the latest depth snapshot.
/// </summary>
public class BinanceDepthResponse
{
    /// <summary>The websocket stream name that produced this payload.</summary>
    [JsonPropertyName("stream")]
    public string Stream { get; init; } = string.Empty;

    /// <summary>The actual depth data for the stream.</summary>
    [JsonPropertyName("data")]
    public BinancePartialDepth? Data { get; init; }
}

/// <summary>
/// Binance partial depth update containing bids, asks, and an update identifier.
/// </summary>
public class BinancePartialDepth
{
    /// <summary>Exchange-specific update identifier used to track book revisions.</summary>
    [JsonPropertyName("lastUpdateId")]
    public long LastUpdateId { get; init; }

    /// <summary>Best bid levels received from Binance.</summary>
    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; init; } = new();

    /// <summary>Best ask levels received from Binance.</summary>
    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; init; } = new();
}