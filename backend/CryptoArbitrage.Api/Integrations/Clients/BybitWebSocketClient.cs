using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using CryptoArbitrage.Api.Configuration;
using CryptoArbitrage.Api.Core.Models;
using CryptoArbitrage.Api.Engine;
using CryptoArbitrage.Api.Infrastructure.Cache;
using CryptoArbitrage.Api.Integrations.Dtos;
using Microsoft.Extensions.Options;

namespace CryptoArbitrage.Api.Integrations.Clients;

// This client connects to Bybit and handles snapshot and delta order book updates.
public class BybitWebSocketClient(
    ILogger<BybitWebSocketClient> logger,
    IRedisCacheService redisCache,
    IMarketDataHealthService health,
    Channel<NormalizedOrderBook> marketDataChannel,
    IOptions<ArbitrageConfig> config,
    IOrderBookManager bookManager) 
    : BaseCryptoWebSocketClient(logger, redisCache, health, marketDataChannel)
{
    private static readonly List<List<string>> EmptyLevels = [];

    private readonly List<string> _symbols = config.Value.TradingPairs;
    private readonly HashSet<string> _snapshotReadySymbols = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _lastUpdateIds = new(StringComparer.Ordinal);
    public override IReadOnlyCollection<string> Symbols => _symbols;

    protected override Uri GetWebSocketUrl() => new Uri("wss://stream.bybit.com/v5/public/spot");

    protected override async Task SubscribeAsync(ClientWebSocket ws, CancellationToken ct)
    {
        if (_symbols.Count == 0) return;

        _snapshotReadySymbols.Clear();
        _lastUpdateIds.Clear();

        var allArgs = _symbols.Select(s => $"orderbook.50.{s}").ToArray();
        foreach (var chunk in allArgs.Chunk(10))
        {
            await SendSubscriptionAsync(ws, new { op = "subscribe", args = chunk }, ct).ConfigureAwait(false);
            await Task.Delay(50, ct).ConfigureAwait(false);
        }

        _ = Task.Run(() => SendPingLoopAsync(ws, ct), ct);
    }

    private async Task SendPingLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), ct);
                if (ws.State == WebSocketState.Open)
                {
                    await SendSubscriptionAsync(ws, new { req_id = Guid.NewGuid().ToString(), op = "ping" }, ct);
                }
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex) 
            { 
                logger.LogWarning("[Bybit] Ping loop interrupted: {Msg}", ex.Message); 
                break; 
            }
        }
    }

    protected override void ProcessMessage(ReadOnlySpan<byte> jsonBytes)
    {
        try
        {
            if (jsonBytes.IndexOf("\"op\":\"ping\""u8) >= 0 || jsonBytes.IndexOf("\"ret_msg\":\"pong\""u8) >= 0)
            {
                return;
            }

            if (jsonBytes.IndexOf("\"success\":false"u8) >= 0)
            {
                logger.LogError("[Bybit] API Error / Subscription Rejected: {Json}", System.Text.Encoding.UTF8.GetString(jsonBytes));
                return;
            }

            var response = JsonSerializer.Deserialize<BybitDepthResponse>(jsonBytes);
            var data = response?.Data;

            if (data != null && !string.IsNullOrEmpty(data.Symbol))
            {
                var symbol = data.Symbol.ToUpperInvariant();
                var isSnapshot = response?.Type == "snapshot";

                if (!isSnapshot && !_snapshotReadySymbols.Contains(symbol))
                {
                    MarkSymbolRecovering(symbol, "Delta received before snapshot.");
                    return;
                }

                if (!isSnapshot &&
                    data.UpdateId > 0 &&
                    _lastUpdateIds.TryGetValue(symbol, out var lastUpdateId) &&
                    data.UpdateId <= lastUpdateId)
                {
                    return;
                }

                var bids = data.Bids ?? EmptyLevels;
                var asks = data.Asks ?? EmptyLevels;

                bookManager.UpdateOrderBook("Bybit", symbol, bids, asks, isSnapshot);
                _snapshotReadySymbols.Add(symbol);
                if (data.UpdateId > 0) _lastUpdateIds[symbol] = data.UpdateId;

                var normalized = bookManager.GetNormalized("Bybit", symbol);
                
                if (normalized != null)
                {
                    PublishOrderBookIfChanged("Bybit", normalized);
                }
            }
        }
        catch (Exception ex) 
        { 
            logger.LogError(ex, "[Bybit] Error parsing message: {Json}", System.Text.Encoding.UTF8.GetString(jsonBytes));
        }
    }
}