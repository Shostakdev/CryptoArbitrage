using CryptoArbitrage.Api.Core.Models;

namespace CryptoArbitrage.Api.Infrastructure.Cache;

// This interface exposes cache operations used by the order book pipeline.
public interface IRedisCacheService
{
    Task SetOrderBookAsync(NormalizedOrderBook orderBook);
    Task<List<NormalizedOrderBook>> GetAllActiveOrderBooksAsync();
}
