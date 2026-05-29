namespace CryptoArbitrage.Api.Core.Enums;

/// <summary>
/// Describes the current lifecycle state of a market-data websocket connection.
/// </summary>
public enum MarketDataConnectionStatus
{
    /// <summary>
    /// The client has been created but has not begun connecting yet.
    /// </summary>
    Starting,

    /// <summary>
    /// The websocket is actively attempting to establish a connection.
    /// </summary>
    Connecting,

    /// <summary>
    /// Market data is flowing and the connection is healthy.
    /// </summary>
    Connected,

    /// <summary>
    /// The client is reconnecting after a disconnect or stale condition.
    /// </summary>
    Recovering,

    /// <summary>
    /// The connection is still present, but updates have stopped arriving in time.
    /// </summary>
    Stale,

    /// <summary>
    /// The connection has failed and the client is not currently healthy.
    /// </summary>
    Failed,

    /// <summary>
    /// The client has been stopped intentionally and is no longer active.
    /// </summary>
    Stopped
}
