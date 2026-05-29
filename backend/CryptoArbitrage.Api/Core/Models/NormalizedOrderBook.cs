namespace CryptoArbitrage.Api.Core.Models;

/// <summary>
/// A normalized order book snapshot that has been converted into a common shape across exchanges.
/// </summary>
public class NormalizedOrderBook
{
    /// <summary>The exchange that produced the snapshot.</summary>
    public string Exchange { get; init; } = string.Empty;

    /// <summary>The trading pair represented by the snapshot.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>The best bid price available in the book.</summary>
    public decimal BestBidPrice { get; init; }

    /// <summary>The best ask price available in the book.</summary>
    public decimal BestAskPrice { get; init; }

    /// <summary>Top bid levels retained after normalization.</summary>
    public OrderBookLevel[] TopBids { get; init; } = [];

    /// <summary>Top ask levels retained after normalization.</summary>
    public OrderBookLevel[] TopAsks { get; init; } = [];

    /// <summary>The UTC timestamp of the normalized snapshot.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
