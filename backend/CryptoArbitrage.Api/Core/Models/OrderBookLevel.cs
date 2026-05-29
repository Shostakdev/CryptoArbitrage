namespace CryptoArbitrage.Api.Core.Models;

/// <summary>
/// A single price/quantity level in a normalized order book.
/// </summary>
public readonly record struct OrderBookLevel(
    /// <summary>The price for this level.</summary>
    decimal Price,
    /// <summary>The quantity available at this level.</summary>
    decimal Amount);
