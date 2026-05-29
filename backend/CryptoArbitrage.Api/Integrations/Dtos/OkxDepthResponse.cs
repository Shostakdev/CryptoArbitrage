using System.Text.Json.Serialization;

namespace CryptoArbitrage.Api.Integrations.Dtos;

/// <summary>
/// OKX websocket envelope containing the instrument argument and depth books.
/// </summary>
public class OkxDepthResponse
{
    /// <summary>Metadata that identifies the instrument being streamed.</summary>
    [JsonPropertyName("arg")]
    public OkxArg? Arg { get; init; }

    /// <summary>The depth data emitted for the instrument.</summary>
    [JsonPropertyName("data")]
    public List<OkxData>? Data { get; init; }
}

/// <summary>
/// OKX websocket argument that contains the instrument identifier.
/// </summary>
public class OkxArg
{
    /// <summary>The instrument id, such as BTC-USDT-SWAP or BTC-USDT.</summary>
    [JsonPropertyName("instId")]
    public string InstId { get; init; } = string.Empty;
}

/// <summary>
/// A single OKX depth snapshot containing bid and ask levels.
/// </summary>
public class OkxData
{
    /// <summary>Bid levels provided by OKX.</summary>
    [JsonPropertyName("bids")]
    public List<List<string>> Bids { get; init; } = new();

    /// <summary>Ask levels provided by OKX.</summary>
    [JsonPropertyName("asks")]
    public List<List<string>> Asks { get; init; } = new();
}