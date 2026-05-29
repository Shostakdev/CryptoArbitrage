using System.Threading.Channels;
using CryptoArbitrage.Api.Configuration;
using CryptoArbitrage.Api.Core.Interfaces;
using CryptoArbitrage.Api.Core.Models;
using CryptoArbitrage.Api.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.SignalR;
using CryptoArbitrage.Api.Api.Hubs;

namespace CryptoArbitrage.Api.Engine;

// This background service listens to normalized order books, computes
// arbitrage opportunities, and dispatches signals to the UI and executor.
public class ArbitrageService(
    ILogger<ArbitrageService> logger,
    Channel<NormalizedOrderBook> marketDataChannel,
    Channel<ArbitrageOpportunityEvent> alertChannel,
    Channel<ArbitrageEventEntity> dbChannel,
    IHubContext<ArbitrageHub> hubContext,
    IArbitrageExecutor executor,
    IArbitrageSettingsManager settingsManager) : BackgroundService
{
    private const string InventorySettlementMode = "Inventory";
    private static readonly TimeSpan MaxOrderBookAge = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan IdleCleanupInterval = TimeSpan.FromMilliseconds(250);

    private readonly Dictionary<string, (string BuyEx, string SellEx, decimal BuyPrice, decimal SellPrice, decimal NetProfit)> _lastAlerts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<NormalizedOrderBook>> _booksBySymbol = new(StringComparer.Ordinal);
    private readonly HashSet<string> _affectedSymbols = new(StringComparer.Ordinal);
    private readonly List<ArbitrageOpportunityEvent> _opportunityBuffer = new(capacity: 25);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Inventory-mode event-driven arbitrage engine with DYNAMIC settings started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var currentConfig = settingsManager.GetCurrentConfig();

                if (!StringComparer.OrdinalIgnoreCase.Equals(currentConfig.SettlementMode, InventorySettlementMode))
                {
                    logger.LogCritical("Arbitrage engine misconfigured: only Inventory settlement mode is supported.");
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                var firstBook = await ReadNextOrderBookAsync(stoppingToken).ConfigureAwait(false);
                var utcNow = DateTime.UtcNow;

                _affectedSymbols.Clear();
                ProcessOrderBook(firstBook, utcNow, _affectedSymbols, currentConfig);

                while (marketDataChannel.Reader.TryRead(out var nextBook))
                {
                    ProcessOrderBook(nextBook, utcNow, _affectedSymbols, currentConfig);
                }

                foreach (var symbol in _affectedSymbols)
                {
                    AnalyzeSymbolIfReady(symbol, utcNow, currentConfig);
                }
            }
            catch (TimeoutException)
            {
                var currentConfig = settingsManager.GetCurrentConfig();
                CleanupStaleBooks(DateTime.UtcNow, currentConfig);
            }
            catch (ChannelClosedException)
            {
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in inventory-mode arbitrage calculation loop.");
            }
        }
    }

    private async Task<NormalizedOrderBook> ReadNextOrderBookAsync(CancellationToken stoppingToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeoutCts.CancelAfter(IdleCleanupInterval);

        try
        {
            return await marketDataChannel.Reader.ReadAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }

    private void ProcessOrderBook(NormalizedOrderBook orderBook, DateTime utcNow, HashSet<string> affectedSymbols, ArbitrageConfig currentConfig)
    {
        if (currentConfig.DisabledExchanges != null && currentConfig.DisabledExchanges.Contains(orderBook.Exchange)) return;
        if (currentConfig.DisabledPairs != null && currentConfig.DisabledPairs.Contains(orderBook.Symbol)) return;
        
        if (orderBook.TopBids.Length == 0 || orderBook.TopAsks.Length == 0) return;
        if (utcNow - orderBook.Timestamp > MaxOrderBookAge) return;

        if (!_booksBySymbol.TryGetValue(orderBook.Symbol, out var booksForSymbol))
        {
            booksForSymbol = new List<NormalizedOrderBook>(capacity: 8);
            _booksBySymbol.Add(orderBook.Symbol, booksForSymbol);
        }

        var replaced = false;
        for (var i = 0; i < booksForSymbol.Count; i++)
        {
            if (!StringComparer.Ordinal.Equals(booksForSymbol[i].Exchange, orderBook.Exchange)) continue;

            booksForSymbol[i] = orderBook;
            replaced = true;
            break;
        }

        if (!replaced) booksForSymbol.Add(orderBook);

        RemoveStaleBooksForSymbol(orderBook.Symbol, utcNow);
        affectedSymbols.Add(orderBook.Symbol);
    }

    private void CleanupStaleBooks(DateTime utcNow, ArbitrageConfig currentConfig)
    {
        var symbols = _booksBySymbol.Keys.ToArray();
        foreach (var symbol in symbols)
        {
            RemoveStaleBooksForSymbol(symbol, utcNow);
            AnalyzeSymbolIfReady(symbol, utcNow, currentConfig);
        }
    }

    private void RemoveStaleBooksForSymbol(string symbol, DateTime utcNow)
    {
        if (!_booksBySymbol.TryGetValue(symbol, out var booksForSymbol)) return;

        for (var i = booksForSymbol.Count - 1; i >= 0; i--)
        {
            if (utcNow - booksForSymbol[i].Timestamp > MaxOrderBookAge)
            {
                booksForSymbol.RemoveAt(i);
            }
        }

        if (booksForSymbol.Count == 0) _booksBySymbol.Remove(symbol);
    }

    private void AnalyzeSymbolIfReady(string symbol, DateTime utcNow, ArbitrageConfig currentConfig)
    {
        if (!_booksBySymbol.TryGetValue(symbol, out var booksForSymbol) || booksForSymbol.Count < 2)
        {
            _lastAlerts.Remove(symbol);
            return;
        }

        AnalyzeSymbolArbitrage(symbol, booksForSymbol, utcNow, currentConfig);
    }

    private void AnalyzeSymbolArbitrage(string symbol, List<NormalizedOrderBook> books, DateTime utcNow, ArbitrageConfig currentConfig)
    {
        // Reset the candidate list before evaluating every visible market snapshot.
        _opportunityBuffer.Clear();

        foreach (var buyBook in books)
        {
            // Each exchange can have its own minimum notional, quantity step, and depth requirements.
            var buyRule = GetTradingRule(currentConfig, buyBook.Exchange, symbol);
            if (!PassesTradeAmountRules(currentConfig.TradeAmountUsdt, buyRule)) continue;

            // Simulate buying the base asset from the best ask levels until the configured size is filled.
            var buySim = SimulateBuy(buyBook.TopAsks, currentConfig.TradeAmountUsdt);
            if (!buySim.IsFullyFilled) continue;
            if (!PassesQuantityRules(buySim.CoinsAcquired, buyRule)) continue;
            if (!PassesDepthRule(buySim.TotalSpentUsdt, buyRule)) continue;

            // Apply the taker fee on the buy side and reduce the acquired quantity accordingly.
            var buyFeeRate = GetTakerFeeRate(currentConfig, buyBook.Exchange);
            var buyFeeUsdt = buySim.TotalSpentUsdt * buyFeeRate;
            var coinsAfterBuyFee = buySim.CoinsAcquired * (1m - buyFeeRate);

            // Respect the exchange-specific quantity step before checking the sell side.
            coinsAfterBuyFee = ApplyQuantityStep(coinsAfterBuyFee, buyRule);
            if (!PassesQuantityRules(coinsAfterBuyFee, buyRule)) continue;

            foreach (var sellBook in books)
            {
                if (StringComparer.Ordinal.Equals(buyBook.Exchange, sellBook.Exchange)) continue;

                var sellRule = GetTradingRule(currentConfig, sellBook.Exchange, symbol);
                var sellQuantity = ApplyQuantityStep(coinsAfterBuyFee, sellRule);
                if (!PassesQuantityRules(sellQuantity, sellRule)) continue;

                // Simulate selling the bought coins into the target exchange's bid ladder.
                var sellSim = SimulateSell(sellBook.TopBids, sellQuantity);
                if (!sellSim.IsFullyFilled) continue;
                if (!PassesNotionalRules(sellSim.TotalEarnedUsdt, sellRule)) continue;
                if (!PassesDepthRule(sellSim.TotalEarnedUsdt, sellRule)) continue;

                var sellFeeRate = GetTakerFeeRate(currentConfig, sellBook.Exchange);
                var sellFeeUsdt = sellSim.TotalEarnedUsdt * sellFeeRate;

                // Gross profit is the price spread before fees, while net profit reflects the actual edge after fees.
                var grossProfit = sellSim.TotalEarnedUsdt - buySim.TotalSpentUsdt;
                var totalFeesUsdt = buyFeeUsdt + sellFeeUsdt;
                var netProfit = grossProfit - sellFeeUsdt;
                var netProfitPercent = buySim.TotalSpentUsdt > 0m ? netProfit / buySim.TotalSpentUsdt * 100m : 0m;

                // Reject opportunities that do not satisfy the current dynamic profit thresholds.
                if (netProfit < currentConfig.MinNetProfitUsdt) continue;
                if (netProfitPercent < currentConfig.MinNetProfitPercent) continue;

                // The gross spread percentage is used to describe the visible edge between the two exchanges.
                var grossSpreadPercent = buySim.AvgBuyPrice > 0m ? (sellSim.AvgSellPrice - buySim.AvgBuyPrice) / buySim.AvgBuyPrice * 100m : 0m;

                _opportunityBuffer.Add(new ArbitrageOpportunityEvent(
                    symbol, buyBook.Exchange, buySim.AvgBuyPrice, sellBook.Exchange, sellSim.AvgSellPrice,
                    buySim.TotalSpentUsdt, grossSpreadPercent, buyFeeUsdt, sellFeeUsdt, totalFeesUsdt,
                    buySim.CoinsAcquired, coinsAfterBuyFee, sellQuantity, grossProfit, netProfit, netProfitPercent,
                    InventorySettlementMode, utcNow));
            }
        }

        if (_opportunityBuffer.Count == 0)
        {
            _lastAlerts.Remove(symbol);
            return;
        }

        var bestRoute = _opportunityBuffer[0];
        for (var i = 1; i < _opportunityBuffer.Count; i++)
        {
            if (_opportunityBuffer[i].NetProfit > bestRoute.NetProfit) bestRoute = _opportunityBuffer[i];
        }

        if (_lastAlerts.TryGetValue(symbol, out var lastAlert) &&
            lastAlert.BuyEx == bestRoute.BuyExchange && lastAlert.SellEx == bestRoute.SellExchange &&
            lastAlert.BuyPrice == bestRoute.BuyPrice && lastAlert.SellPrice == bestRoute.SellPrice)
        {
            return;
        }

        _lastAlerts[symbol] = (bestRoute.BuyExchange, bestRoute.SellExchange, bestRoute.BuyPrice, bestRoute.SellPrice, bestRoute.NetProfit);

        if (!alertChannel.Writer.TryWrite(bestRoute))
        {
            logger.LogWarning("Arbitrage alert channel is full, dropping signal for {Symbol}", symbol);
        }

        var dbEntity = new ArbitrageEventEntity
        {
            Id = Guid.NewGuid(),
            Symbol = bestRoute.Symbol,
            BuyExchange = bestRoute.BuyExchange,
            SellExchange = bestRoute.SellExchange,
            AvgBuyPrice = bestRoute.BuyPrice,
            AvgSellPrice = bestRoute.SellPrice,
            TradeVolumeUsdt = bestRoute.TradeAmountUsdt, 
            CoinsTraded = bestRoute.CoinsBought,         
            CreatedAtUtc = bestRoute.Timestamp,          
            GrossSpreadPercent = bestRoute.GrossSpreadPercent,
            BuyFeeUsdt = bestRoute.BuyFeeUsdt,
            SellFeeUsdt = bestRoute.SellFeeUsdt,
            TotalFeesUsdt = bestRoute.TotalFeesUsdt,
            GrossProfit = bestRoute.GrossProfit,
            NetProfit = bestRoute.NetProfit,
            NetProfitPercent = bestRoute.NetProfitPercent,
            SettlementMode = bestRoute.SettlementMode
        };

        if (!dbChannel.Writer.TryWrite(dbEntity))
        {
            logger.LogWarning("Database write channel is full, dropping log for {Symbol}", symbol);
        }
        
        _ = hubContext.Clients.All.SendAsync("ReceiveOpportunity", dbEntity);
        _ = executor.ExecuteAsync(bestRoute, dbEntity.Id);
    }

    private decimal GetTakerFeeRate(ArbitrageConfig config, string exchange)
    {
        if (config.ExchangeFees != null && config.ExchangeFees.TryGetValue(exchange, out var exchangeFee))
        {
            return IsValidFee(exchangeFee.TakerFeeRate) ? exchangeFee.TakerFeeRate : config.TakerFeeRate;
        }
        return config.TakerFeeRate;
    }

    private TradingRuleConfig? GetTradingRule(ArbitrageConfig config, string exchange, string symbol)
    {
        if (config.TradingRules == null) return null;
        if (config.TradingRules.TryGetValue($"{exchange}:{symbol}", out var exchangeSymbolRule)) return exchangeSymbolRule;
        if (config.TradingRules.TryGetValue(symbol, out var symbolRule)) return symbolRule;
        return null;
    }

    private static bool PassesTradeAmountRules(decimal tradeAmountUsdt, TradingRuleConfig? rule) =>
        tradeAmountUsdt > 0m && (rule == null || rule.MinNotional <= 0m || tradeAmountUsdt >= rule.MinNotional);

    private static bool PassesNotionalRules(decimal notionalUsdt, TradingRuleConfig? rule) =>
        notionalUsdt > 0m && (rule == null || rule.MinNotional <= 0m || notionalUsdt >= rule.MinNotional);

    private static bool PassesQuantityRules(decimal quantity, TradingRuleConfig? rule) =>
        quantity > 0m && (rule == null || rule.MinQuantity <= 0m || quantity >= rule.MinQuantity);

    private static bool PassesDepthRule(decimal filledNotionalUsdt, TradingRuleConfig? rule) =>
        rule == null || rule.MinDepthUsdt <= 0m || filledNotionalUsdt >= rule.MinDepthUsdt;

    // Quantity steps reduce the simulated position size to exchange-safe increments.
    private static decimal ApplyQuantityStep(decimal quantity, TradingRuleConfig? rule) =>
        rule?.QuantityStep is > 0m ? Math.Floor(quantity / rule.QuantityStep) * rule.QuantityStep : quantity;

    private static bool IsValidFee(decimal feeRate) => feeRate >= 0m && feeRate < 1m;

    // This simulates buying the quote asset from the order book using a fixed USDT budget.
    public static (bool IsFullyFilled, decimal CoinsAcquired, decimal TotalSpentUsdt, decimal AvgBuyPrice) SimulateBuy(OrderBookLevel[] asks, decimal targetUsdt)
    {
        if (targetUsdt <= 0m) return (false, 0m, 0m, 0m);

        var remainingUsdt = targetUsdt;
        var coins = 0m;
        var spent = 0m;

        foreach (var level in asks)
        {
            if (level.Price <= 0m || level.Amount <= 0m) continue;

            // The cost of consuming one price level is price × volume.
            var costForThisLevel = level.Price * level.Amount;

            if (remainingUsdt <= costForThisLevel)
            {
                // The final level is partially filled, so the acquired amount is computed from the remaining budget.
                coins += remainingUsdt / level.Price;
                spent += remainingUsdt;
                remainingUsdt = 0m;
                break;
            }

            // Full level consumption.
            coins += level.Amount;
            spent += costForThisLevel;
            remainingUsdt -= costForThisLevel;
        }

        return (remainingUsdt == 0m, coins, spent, coins > 0m ? spent / coins : 0m);
    }

    // This simulates selling the acquired base asset into the bid ladder of another exchange.
    public static (bool IsFullyFilled, decimal TotalEarnedUsdt, decimal AvgSellPrice) SimulateSell(OrderBookLevel[] bids, decimal targetCoins)
    {
        if (targetCoins <= 0m) return (false, 0m, 0m);

        var remainingCoins = targetCoins;
        var earned = 0m;

        foreach (var level in bids)
        {
            if (level.Price <= 0m || level.Amount <= 0m) continue;

            if (remainingCoins <= level.Amount)
            {
                // The last level is partially consumed, so earned value is computed from the remaining coin amount.
                earned += remainingCoins * level.Price;
                remainingCoins = 0m;
                break;
            }

            // Full level consumption.
            earned += level.Amount * level.Price;
            remainingCoins -= level.Amount;
        }

        var soldCoins = targetCoins - remainingCoins;
        return (remainingCoins == 0m, earned, soldCoins > 0m ? earned / soldCoins : 0m);
    }
}