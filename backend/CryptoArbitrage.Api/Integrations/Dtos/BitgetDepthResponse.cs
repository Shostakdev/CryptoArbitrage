using System.Text.Json.Serialization;

namespace CryptoArbitrage.Api.Integrations.Dtos;

/// <summary>
/// Bitget websocket envelope containing the instrument argument and depth snapshots.
/// </summary>
public class BitgetDepthResponse
{
    /// <summary>Metadata describing the instrument being streamed.</summary>
    [JsonPropertyName("arg")]
    public BitgetArg? Arg { get; init; }

    /// <summary>The depth data emitted for the instrument.</summary>
    [JsonPropertyName("data")]
    public List<BitgetData>? Data { get; init; }
}

/// <summary>
/// Bitget websocket argument containing the instrument id.
/// </summary>
public class BitgetArg
{
    /// <summary>The instrument id string used by Bitget.</summary>
    [JsonPropertyName("instId")]
    public string InstId { get; init; } = string.Empty;
}

/// <summary>
/// A Bitget depth snapshot containing bid and ask levels.
/// </summary>
public class BitgetData
{
    /// <summary>Bid levels provided by Bitget.</summary>
    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; init; } = new();

    /// <summary>Ask levels provided by Bitget.</summary>
    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; init; } = new();
}