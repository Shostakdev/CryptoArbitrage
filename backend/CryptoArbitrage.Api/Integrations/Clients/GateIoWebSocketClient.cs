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

// This client handles Gate.io order book subscriptions and publishes normalized updates.
public class GateIoWebSocketClient(
    ILogger<GateIoWebSocketClient> logger,
    IRedisCacheService redisCache,
    IMarketDataHealthService health,
    Channel<NormalizedOrderBook> marketDataChannel,
    IOptions<ArbitrageConfig> config,
    IOrderBookManager bookManager)
    : BaseCryptoWebSocketClient(logger, redisCache, health, marketDataChannel)
{
    private readonly List<string> _symbols = config.Value.TradingPairs;
    public override IReadOnlyCollection<string> Symbols => _symbols;
    
    protected override Uri GetWebSocketUrl() => new Uri("wss://api.gateio.ws/ws/v4/");

    protected override TimeSpan PingInterval => TimeSpan.FromSeconds(20);
    protected override object GetPingPayload() => new { time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), channel = "spot.ping", @event = "ping" };

    protected override async Task SubscribeAsync(ClientWebSocket ws, CancellationToken ct)
    {
        foreach (var sym in _symbols)
        {
            var payload = new { time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), channel = "spot.order_book", @event = "subscribe", payload = new[] { sym.Replace("USDT", "_USDT"), "5", "100ms" } };
            await SendSubscriptionAsync(ws, payload, ct).ConfigureAwait(false);
            await Task.Delay(50, ct).ConfigureAwait(false);
        }
    }

    protected override void ProcessMessage(ReadOnlySpan<byte> jsonBytes)
    {
        if (jsonBytes.IndexOf("\"event\":\"pong\""u8) >= 0) return;

        if (jsonBytes.IndexOf("\"error\":"u8) >= 0 && jsonBytes.IndexOf("\"error\":null"u8) < 0)
        {
            logger.LogError("[GateIo] API Error: {Json}", System.Text.Encoding.UTF8.GetString(jsonBytes));
            return;
        }

        try
        {
            var response = JsonSerializer.Deserialize<GateIoDepthResponse>(jsonBytes);
            if (response?.Result != null && response.Event == "update")
            {
                string symbol = response.Result.Symbol.Replace("_", "").ToUpperInvariant();
                bookManager.UpdateOrderBook("GateIo", symbol, response.Result.Bids, response.Result.Asks, true);
                var normalized = bookManager.GetNormalized("GateIo", symbol);
                if (normalized != null) PublishOrderBookIfChanged("GateIo", normalized);
            }
        }
        catch (JsonException ex)
        {
            logger.LogDebug("[GateIo] Failed to parse message: {Error}", ex.Message);
        }
    }
}