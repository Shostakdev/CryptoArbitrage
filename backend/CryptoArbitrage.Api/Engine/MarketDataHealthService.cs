using System.Collections.Concurrent;
using CryptoArbitrage.Api.Core.Enums;
using CryptoArbitrage.Api.Core.Models;

namespace CryptoArbitrage.Api.Engine;

// This service tracks connection health, message freshness, and stale market data for each exchange and symbol.
public interface IMarketDataHealthService
{
    void SetExchangeStatus(
        string exchange,
        MarketDataConnectionStatus status,
        string? error = null,
        int reconnectAttempts = 0,
        int consecutiveFailures = 0,
        TimeSpan? currentBackoff = null);

    void MarkMessage(string exchange);
    void MarkOrderBook(string exchange, string symbol);
    void SetSymbolStatus(string exchange, string symbol, MarketDataConnectionStatus status, string? error = null);
    IReadOnlyCollection<MarketDataHealthSnapshot> GetSnapshots();
}

public sealed class MarketDataHealthService : IMarketDataHealthService
{
    private const string ExchangeWideSymbol = "*";
    private static readonly TimeSpan SymbolFreshnessWindow = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan ExchangeMessageFreshnessWindow = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, MarketDataHealthSnapshot> _snapshots = new(StringComparer.Ordinal);

    // This updates the exchange-level status and preserves reconnect diagnostics.
    public void SetExchangeStatus(
        string exchange,
        MarketDataConnectionStatus status,
        string? error = null,
        int reconnectAttempts = 0,
        int consecutiveFailures = 0,
        TimeSpan? currentBackoff = null)
    {
        var now = DateTime.UtcNow;
        Upsert(
            BuildKey(exchange, null),
            exchange,
            null,
            current => current with
            {
                Status = status,
                UpdatedUtc = now,
                LastError = error,
                ReconnectAttempts = reconnectAttempts,
                ConsecutiveFailures = consecutiveFailures,
                CurrentBackoffMs = currentBackoff.HasValue ? (int)currentBackoff.Value.TotalMilliseconds : 0
            },
            status,
            now,
            error,
            reconnectAttempts,
            consecutiveFailures,
            currentBackoff);
    }

    // This marks a successful heartbeat from the exchange websocket.
    public void MarkMessage(string exchange)
    {
        var now = DateTime.UtcNow;
        Upsert(
            BuildKey(exchange, null),
            exchange,
            null,
            current => current with
            {
                Status = MarketDataConnectionStatus.Connected,
                UpdatedUtc = now,
                LastMessageUtc = now,
                LastError = null
            },
            MarketDataConnectionStatus.Connected,
            now,
            null,
            0,
            0,
            null,
            lastMessageUtc: now);
    }

    // This records that a symbol has a fresh normalized order book and is healthy.
    public void MarkOrderBook(string exchange, string symbol)
    {
        var now = DateTime.UtcNow;
        Upsert(
            BuildKey(exchange, symbol),
            exchange,
            symbol,
            current => current with
            {
                Status = MarketDataConnectionStatus.Connected,
                UpdatedUtc = now,
                LastMessageUtc = now,
                LastOrderBookUtc = now,
                LastError = null
            },
            MarketDataConnectionStatus.Connected,
            now,
            null,
            0,
            0,
            null,
            lastMessageUtc: now,
            lastOrderBookUtc: now);
    }

    // This updates the status for one symbol only, for example when a reconnect is needed.
    public void SetSymbolStatus(string exchange, string symbol, MarketDataConnectionStatus status, string? error = null)
    {
        var now = DateTime.UtcNow;
        Upsert(
            BuildKey(exchange, symbol),
            exchange,
            symbol,
            current => current with
            {
                Status = status,
                UpdatedUtc = now,
                LastError = error
            },
            status,
            now,
            error,
            0,
            0,
            null);
    }

    public IReadOnlyCollection<MarketDataHealthSnapshot> GetSnapshots()
    {
        var now = DateTime.UtcNow;

        return _snapshots.Values
            .Select(snapshot => ApplyStaleness(snapshot, now))
            .OrderBy(static snapshot => snapshot.Exchange, StringComparer.Ordinal)
            .ThenBy(static snapshot => snapshot.Symbol ?? ExchangeWideSymbol, StringComparer.Ordinal)
            .ToArray();
    }

    private void Upsert(
        string key,
        string exchange,
        string? symbol,
        Func<MarketDataHealthSnapshot, MarketDataHealthSnapshot> update,
        MarketDataConnectionStatus status,
        DateTime now,
        string? error,
        int reconnectAttempts,
        int consecutiveFailures,
        TimeSpan? currentBackoff,
        DateTime? lastMessageUtc = null,
        DateTime? lastOrderBookUtc = null)
    {
        _snapshots.AddOrUpdate(
            key,
            _ => new MarketDataHealthSnapshot(
                exchange,
                symbol,
                status,
                now,
                lastMessageUtc,
                lastOrderBookUtc,
                error,
                reconnectAttempts,
                consecutiveFailures,
                currentBackoff.HasValue ? (int)currentBackoff.Value.TotalMilliseconds : 0),
            (_, current) => update(current));
    }

    private static string BuildKey(string exchange, string? symbol) =>
        symbol is null ? $"{exchange}:{ExchangeWideSymbol}" : $"{exchange}:{symbol}";

    // This converts a healthy snapshot into a stale snapshot when the feed stops updating.
    private static MarketDataHealthSnapshot ApplyStaleness(MarketDataHealthSnapshot snapshot, DateTime now)
    {
        if (snapshot.Status is not MarketDataConnectionStatus.Connected)
        {
            return snapshot;
        }

        if (snapshot.Symbol is not null &&
            snapshot.LastOrderBookUtc.HasValue &&
            now - snapshot.LastOrderBookUtc.Value > SymbolFreshnessWindow)
        {
            return snapshot with
            {
                Status = MarketDataConnectionStatus.Stale,
                LastError = $"No order book update for {SymbolFreshnessWindow.TotalMilliseconds:0}ms."
            };
        }

        if (snapshot.Symbol is null &&
            snapshot.LastMessageUtc.HasValue &&
            now - snapshot.LastMessageUtc.Value > ExchangeMessageFreshnessWindow)
        {
            return snapshot with
            {
                Status = MarketDataConnectionStatus.Stale,
                LastError = $"No WebSocket message for {ExchangeMessageFreshnessWindow.TotalSeconds:0}s."
            };
        }

        return snapshot;
    }
}
