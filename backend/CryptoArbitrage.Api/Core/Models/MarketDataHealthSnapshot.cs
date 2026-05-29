using CryptoArbitrage.Api.Core.Enums;

namespace CryptoArbitrage.Api.Core.Models;

/// <summary>
/// Captures the current health and connectivity state of a websocket feed for a single exchange or symbol.
/// </summary>
public sealed record MarketDataHealthSnapshot(
    /// <summary>The exchange whose websocket is being monitored.</summary>
    string Exchange,
    /// <summary>The symbol being watched, if the snapshot is symbol-specific.</summary>
    string? Symbol,
    /// <summary>The current connection lifecycle state.</summary>
    MarketDataConnectionStatus Status,
    /// <summary>The UTC time when the snapshot was last refreshed.</summary>
    DateTime UpdatedUtc,
    /// <summary>The time of the most recent message received from the websocket.</summary>
    DateTime? LastMessageUtc,
    /// <summary>The time of the most recent order book update received.</summary>
    DateTime? LastOrderBookUtc,
    /// <summary>The latest error message, if the connection has failed or degraded.</summary>
    string? LastError,
    /// <summary>The number of reconnection attempts made for this feed.</summary>
    int ReconnectAttempts,
    /// <summary>The number of consecutive failures seen in a row.</summary>
    int ConsecutiveFailures,
    /// <summary>The current reconnect backoff in milliseconds.</summary>
    int CurrentBackoffMs);
