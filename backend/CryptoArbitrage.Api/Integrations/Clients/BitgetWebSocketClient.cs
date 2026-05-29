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

// This client reads Bitget book data and forwards clean order books into the shared pipeline.
public class BitgetWebSocketClient(
    ILogger<BitgetWebSocketClient> logger,
    IRedisCacheService redisCache,
    IMarketDataHealthService health,
    Channel<NormalizedOrderBook> marketDataChannel,
    IOptions<ArbitrageConfig> config,
    IOrderBookManager bookManager)
    : BaseCryptoWebSocketClient(logger, redisCache, health, marketDataChannel)
{
    private readonly List<string> _symbols = config.Value.TradingPairs;
    private bool _isFirstMessageLogged = false;
    public override IReadOnlyCollection<string> Symbols => _symbols;
    
    protected override Uri GetWebSocketUrl() => new Uri("wss://ws.bitget.com/v2/ws/public");
    
    protected override TimeSpan PingInterval => TimeSpan.FromSeconds(20);
    protected override string GetRawPingMessage() => "ping";

    protected override async Task SubscribeAsync(ClientWebSocket ws, CancellationToken ct)
    {
        if (_symbols.Count == 0) return;
        
        var allArgs = _symbols.Select(s => new { instType = "SPOT", channel = "books5", instId = s }).ToArray();
        _isFirstMessageLogged = false;
        
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
            logger.LogError("[Bitget] API Error / Subscription Rejected: {Json}", System.Text.Encoding.UTF8.GetString(jsonBytes));
            return;
        }

        try
        {
            var response = JsonSerializer.Deserialize<BitgetDepthResponse>(jsonBytes);
            
            if (response?.Data != null && response.Data.Count > 0 && response.Arg != null)
            {
                string symbol = response.Arg.InstId.ToUpperInvariant();

                if (!_isFirstMessageLogged)
                {
                    logger.LogWarning("[Bitget] 🟢 SUCCESS: First data parsed for {Symbol}", symbol);
                    _isFirstMessageLogged = true;
                }

                bookManager.UpdateOrderBook("Bitget", symbol, response.Data[0].Bids, response.Data[0].Asks, true);
                var normalized = bookManager.GetNormalized("Bitget", symbol);
                if (normalized != null) PublishOrderBookIfChanged("Bitget", normalized);
            }
        }
        catch (Exception ex) 
        { 
            var slice = jsonBytes.Length > 150 ? jsonBytes[..150] : jsonBytes;
            string shortJson = System.Text.Encoding.UTF8.GetString(slice) + (jsonBytes.Length > 150 ? "..." : "");
            logger.LogWarning("[Bitget] Parse error: {Message}, Json: {Json}", ex.Message, shortJson);
        }
    }
}