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

// This client subscribes to OKX order book updates and publishes normalized snapshots.
public class OkxWebSocketClient(
    ILogger<OkxWebSocketClient> logger,
    IRedisCacheService redisCache,
    IMarketDataHealthService health,
    Channel<NormalizedOrderBook> marketDataChannel,
    IOptions<ArbitrageConfig> config,
    IOrderBookManager bookManager)
    : BaseCryptoWebSocketClient(logger, redisCache, health, marketDataChannel)
{
    private readonly List<string> _symbols = config.Value.TradingPairs;
    public override IReadOnlyCollection<string> Symbols => _symbols;
    
    protected override Uri GetWebSocketUrl() => new Uri("wss://ws.okx.com:8443/ws/v5/public");

    protected override TimeSpan PingInterval => TimeSpan.FromSeconds(20);
    protected override string GetRawPingMessage() => "ping";

    protected override async Task SubscribeAsync(ClientWebSocket ws, CancellationToken ct)
    {
        if (_symbols.Count == 0) return;
        
        var allArgs = _symbols.Select(s => new { channel = "books5", instId = s.Replace("USDT", "-USDT") }).ToArray();
        
        foreach (var chunk in allArgs.Chunk(10))
        {
            await SendSubscriptionAsync(ws, new { op = "subscribe", args = chunk }, ct).ConfigureAwait(false);
            await Task.Delay(50, ct).ConfigureAwait(false);
        }
    }

    protected override void ProcessMessage(ReadOnlySpan<byte> jsonBytes)
    {
        if (jsonBytes.SequenceEqual("pong"u8) || jsonBytes.SequenceEqual("\"pong\""u8)) return;

        if (jsonBytes.IndexOf("\"event\":\"error\""u8) >= 0)
        {
            logger.LogError("[Okx] API Error / Subscription Rejected: {Json}", System.Text.Encoding.UTF8.GetString(jsonBytes));
            return;
        }

        try
        {
            var response = JsonSerializer.Deserialize<OkxDepthResponse>(jsonBytes);
            if (response?.Data != null && response.Data.Count > 0 && response.Arg != null)
            {
                string symbol = response.Arg.InstId.Replace("-", "").ToUpperInvariant();
                bookManager.UpdateOrderBook("Okx", symbol, response.Data[0].Bids, response.Data[0].Asks, true);
                var normalized = bookManager.GetNormalized("Okx", symbol);
                if (normalized != null) PublishOrderBookIfChanged("Okx", normalized);
            }
        }
        catch (JsonException ex)
        {
            logger.LogDebug("[Okx] Failed to parse message: {Error}. Payload: {Payload}", ex.Message, System.Text.Encoding.UTF8.GetString(jsonBytes));
        }
    }
}