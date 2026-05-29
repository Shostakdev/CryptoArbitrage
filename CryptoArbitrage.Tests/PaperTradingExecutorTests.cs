using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using CryptoArbitrage.Api.Engine;
using CryptoArbitrage.Api.Core.Models;
using CryptoArbitrage.Api.Core.Enums;
using CryptoArbitrage.Api.Infrastructure.Persistence;
using CryptoArbitrage.Api.Infrastructure.Persistence.Entities;
using CryptoArbitrage.Api.Api.Hubs;

namespace CryptoArbitrage.Tests;

public class PaperTradingExecutorTests
{
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public PaperTradingExecutorTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectTrade_WhenInsufficientFunds()
    {
        using var dbContext = new AppDbContext(_dbOptions);
        
        dbContext.VirtualWallets.Add(new VirtualWalletEntity { Id = Guid.NewGuid(), Asset = "USDT", Balance = 50m, UpdatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(x => x.GetService(typeof(AppDbContext))).Returns(dbContext);
        mockScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);

        var mockLogger = new Mock<ILogger<PaperTradingExecutor>>();
        var mockHubContext = new Mock<IHubContext<ArbitrageHub>>();
        var mockClients = new Mock<IHubClients>();
        mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        mockClients.Setup(x => x.All).Returns(new Mock<IClientProxy>().Object);

        var executor = new PaperTradingExecutor(mockLogger.Object, mockScopeFactory.Object, mockHubContext.Object);

        var opportunity = new ArbitrageOpportunityEvent(
            "BTC/USDT", "Binance", 50000, "Bybit", 50100, 
            TradeAmountUsdt: 100m,
            0, 0, 0, 0, 0, 0, 0, 0, NetProfit: 2m, 0, "Inventory", DateTime.UtcNow);
        var eventId = Guid.NewGuid();

        await executor.ExecuteAsync(opportunity, eventId);

        var executionLog = await dbContext.TradeExecutions.FirstOrDefaultAsync(e => e.ArbitrageEventId == eventId);
        var wallet = await dbContext.VirtualWallets.FirstAsync();

        Assert.NotNull(executionLog);
        Assert.Equal(TradeExecutionStatus.Failed_InsufficientFunds, executionLog.Status);
        Assert.Equal(50m, wallet.Balance);
    }
}