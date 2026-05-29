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

// This client connects to Binance and converts depth updates into normalized order books.
public class BinanceWebSocketClient(
    ILogger<BinanceWebSocketClient> logger,
    IRedisCacheService redisCache,
    IMarketDataHealthService health,
    Channel<NormalizedOrderBook> marketDataChannel,
    IOptions<ArbitrageConfig> config, 
    IOrderBookManager bookManager)
    : BaseCryptoWebSocketClient(logger, redisCache, health, marketDataChannel)
{
    private readonly List<string> _symbols = config.Value.TradingPairs;
    public override IReadOnlyCollection<string> Symbols => _symbols;

    protected override Uri GetWebSocketUrl()
    {
        var streams = string.Join("/", _symbols.Select(static s => $"{s.ToLowerInvariant()}@depth20@100ms"));
        return new Uri($"wss://stream.binance.com:9443/stream?streams={streams}");
    }

    protected override Task SubscribeAsync(ClientWebSocket ws, CancellationToken ct) => Task.CompletedTask;

    protected override void ProcessMessage(ReadOnlySpan<byte> jsonBytes)
    {
        try
        {
            var response = JsonSerializer.Deserialize<BinanceDepthResponse>(jsonBytes); 
            
            if (response?.Data != null && !string.IsNullOrEmpty(response.Stream))
            {
                var separatorIndex = response.Stream.IndexOf('@');
                if (separatorIndex <= 0) return;

                var symbol = response.Stream[..separatorIndex].ToUpperInvariant();
                
                if (response.Data.LastUpdateId <= 0)
                {
                    MarkSymbolRecovering(symbol, "Binance depth snapshot has invalid lastUpdateId.");
                    return;
                }

                bookManager.UpdateOrderBook("Binance", symbol, response.Data.Bids, response.Data.Asks, isSnapshot: true);
                var normalized = bookManager.GetNormalized("Binance", symbol);
                
                if (normalized != null)
                {
                    PublishOrderBookIfChanged("Binance", normalized);
                }
            }
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "[Binance] Failed to parse message.");
        }
    }
}
