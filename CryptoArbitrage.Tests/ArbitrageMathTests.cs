using Xunit;
using CryptoArbitrage.Api.Engine;
using CryptoArbitrage.Api.Core.Models;

namespace CryptoArbitrage.Tests;

public class ArbitrageMathTests
{
    [Fact]
    public void SimulateBuy_ShouldCalculateMultiLevelFill_Correctly()
    {
        var asks = new[]
        {
            new OrderBookLevel { Price = 100m, Amount = 5m },
            new OrderBookLevel { Price = 101m, Amount = 10m }
        };
        
        decimal targetUsdt = 1005m;

        var result = ArbitrageService.SimulateBuy(asks, targetUsdt);

        Assert.True(result.IsFullyFilled, "Ордер должен быть исполнен полностью");
        
        Assert.Equal(10m, result.CoinsAcquired);
        Assert.Equal(1005m, result.TotalSpentUsdt);
        
        Assert.Equal(100.5m, result.AvgBuyPrice);
    }

    [Fact]
    public void SimulateSell_ShouldFail_WhenNotEnoughLiquidity()
    {
        var bids = new[]
        {
            new OrderBookLevel { Price = 100m, Amount = 2m }
        };
        
        decimal targetCoins = 5m;

        var result = ArbitrageService.SimulateSell(bids, targetCoins);

        Assert.False(result.IsFullyFilled, "Алгоритм должен понять, что ликвидности не хватает");
    }
}