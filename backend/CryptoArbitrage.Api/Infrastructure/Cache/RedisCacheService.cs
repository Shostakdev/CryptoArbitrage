using System.Text.Json;
using CryptoArbitrage.Api.Configuration;
using CryptoArbitrage.Api.Core.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CryptoArbitrage.Api.Infrastructure.Cache;

// This cache service stores recent normalized order books and reuses them for quick recovery.
public class RedisCacheService : IRedisCacheService
{
    private static readonly TimeSpan OrderBookTtl = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxOrderBookAge = TimeSpan.FromMilliseconds(1500);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDatabase _db;
    private readonly RedisKey[] _activeOrderBookKeys;

    // This caches the latest normalized order books so the engine can recover quickly after reconnects.
    public RedisCacheService(IConnectionMultiplexer redis, IOptions<ArbitrageConfig> config)
    {
        _db = redis.GetDatabase();

        var exchanges = config.Value.Exchanges;
        var symbols = config.Value.TradingPairs;
        _activeOrderBookKeys = new RedisKey[exchanges.Count * symbols.Count];

        var index = 0;
        foreach (var exchange in exchanges)
        foreach (var symbol in symbols)
        {
            _activeOrderBookKeys[index++] = BuildKey(exchange, symbol);
        }
    }

    public Task SetOrderBookAsync(NormalizedOrderBook orderBook)
    {
        var key = BuildKey(orderBook.Exchange, orderBook.Symbol);
        var json = JsonSerializer.SerializeToUtf8Bytes(orderBook, JsonOptions);
        return _db.StringSetAsync(key, json, OrderBookTtl);
    }

    public async Task<List<NormalizedOrderBook>> GetAllActiveOrderBooksAsync()
    {
        if (_activeOrderBookKeys.Length == 0) return [];

        var values = await _db.StringGetAsync(_activeOrderBookKeys).ConfigureAwait(false);
        var result = new List<NormalizedOrderBook>(values.Length);
        var utcNow = DateTime.UtcNow;

        foreach (var value in values)
        {
            if (value.IsNullOrEmpty) continue;

            NormalizedOrderBook? orderBook;
            try
            {
                orderBook = JsonSerializer.Deserialize<NormalizedOrderBook>((byte[])value!, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (orderBook == null) continue;
            if (orderBook.TopBids.Length == 0 || orderBook.TopAsks.Length == 0) continue;
            if (utcNow - orderBook.Timestamp > MaxOrderBookAge) continue;

            result.Add(orderBook);
        }

        return result;
    }

    private static RedisKey BuildKey(string exchange, string symbol) => $"OrderBook:{exchange}:{symbol}";
}