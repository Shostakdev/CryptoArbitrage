using System.Collections.Concurrent;
using System.Globalization;
using CryptoArbitrage.Api.Core.Models;

namespace CryptoArbitrage.Api.Engine;

// This manager stores and normalizes live order books so the arbitrage engine can evaluate them consistently.
public interface IOrderBookManager
{
    void UpdateOrderBook(string exchange, string symbol, List<List<string>> bids, List<List<string>> asks, bool isSnapshot = false);
    NormalizedOrderBook? GetNormalized(string exchange, string symbol);
}

public class OrderBookManager : IOrderBookManager
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, BookState>> _books = new(StringComparer.Ordinal);

    // This updates the in-memory book for one exchange and symbol and keeps the book sorted by price.
    public void UpdateOrderBook(string exchange, string symbol, List<List<string>> bids, List<List<string>> asks, bool isSnapshot = false)
    {
        var parsedBids = ParseLevels(bids);
        var parsedAsks = ParseLevels(asks);

        if (parsedBids == null || parsedAsks == null) return;

        var exchangeBook = _books.GetOrAdd(exchange, static _ => new ConcurrentDictionary<string, BookState>(StringComparer.Ordinal));
        var symbolBook = exchangeBook.GetOrAdd(symbol, static _ => new BookState());

        lock (symbolBook.Sync)
        {
            if (isSnapshot)
            {
                symbolBook.Bids.Clear();
                symbolBook.Asks.Clear();
            }

            ApplyLevels(symbolBook.Bids, parsedBids, isBids: true);
            ApplyLevels(symbolBook.Asks, parsedAsks, isBids: false);
        }
    }

    // This converts raw exchange strings into normalized price and amount tuples.
    private static List<(decimal Price, decimal Amount)>? ParseLevels(List<List<string>> raw)
    {
        if (raw == null) return null;

        var result = new List<(decimal, decimal)>(raw.Count);
        foreach (var level in raw)
        {
            if (level.Count < 2) return null;
            if (!decimal.TryParse(level[0], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price)) return null;
            if (!decimal.TryParse(level[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)) return null;
            if (price <= 0m || amount < 0m) return null;

            result.Add((price, amount));
        }

        return result;
    }

    // This inserts, replaces, or removes levels while preserving sorted bid and ask books.
    private static void ApplyLevels(
        List<OrderBookLevel> levels,
        List<(decimal Price, decimal Amount)> updates,
        bool isBids)
    {
        foreach (var (price, amount) in updates)
        {
            int index = BinarySearch(levels, price, isBids);

            if (amount == 0m)
            {
                if (index >= 0)
                {
                    levels.RemoveAt(index);
                }
            }
            else
            {
                if (index >= 0)
                {
                    levels[index] = new OrderBookLevel(price, amount);
                }
                else
                {
                    levels.Insert(~index, new OrderBookLevel(price, amount));
                }
            }
        }
    }

    // This keeps book updates sorted for fast top-of-book access.
    private static int BinarySearch(List<OrderBookLevel> levels, decimal price, bool isBids)
    {
        int left = 0;
        int right = levels.Count - 1;

        while (left <= right)
        {
            int mid = left + ((right - left) >> 1);
            int cmp = price.CompareTo(levels[mid].Price);

            if (isBids)
            {
                if (cmp > 0) right = mid - 1;
                else if (cmp < 0) left = mid + 1;
                else return mid;
            }
            else
            {
                if (cmp > 0) left = mid + 1;
                else if (cmp < 0) right = mid - 1;
                else return mid;
            }
        }

        return ~left;
    }

    // This extracts the top levels for the engine while preserving the latest market snapshot.
    public NormalizedOrderBook? GetNormalized(string exchange, string symbol)
    {
        if (!_books.TryGetValue(exchange, out var exch) || !exch.TryGetValue(symbol, out var book)) return null;

        lock (book.Sync)
        {
            if (book.Bids.Count == 0 || book.Asks.Count == 0) return null;

            var topBids = CopyTopLevels(book.Bids, 10);
            var topAsks = CopyTopLevels(book.Asks, 10);

            if (topBids.Length == 0 || topAsks.Length == 0) return null;

            return new NormalizedOrderBook
            {
                Exchange = exchange,
                Symbol = symbol,
                BestBidPrice = topBids[0].Price,
                BestAskPrice = topAsks[0].Price,
                TopBids = topBids,
                TopAsks = topAsks,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    // This copies only the top levels needed by the arbitrage engine to reduce processing overhead.
    private static OrderBookLevel[] CopyTopLevels(List<OrderBookLevel> source, int limit)
    {
        var count = Math.Min(source.Count, limit);
        var result = new OrderBookLevel[count];

        for (int i = 0; i < count; i++)
        {
            result[i] = source[i];
        }

        return result;
    }

    private sealed class BookState
    {
        public object Sync { get; } = new();

        public List<OrderBookLevel> Bids { get; } = new(capacity: 1024);
        public List<OrderBookLevel> Asks { get; } = new(capacity: 1024);
    }
}