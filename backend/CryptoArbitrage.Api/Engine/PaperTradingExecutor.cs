using CryptoArbitrage.Api.Api.Hubs;
using CryptoArbitrage.Api.Core.Enums;
using CryptoArbitrage.Api.Core.Interfaces;
using CryptoArbitrage.Api.Core.Models;
using CryptoArbitrage.Api.Infrastructure.Persistence;
using CryptoArbitrage.Api.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CryptoArbitrage.Api.Engine;

// This executor simulates trade execution and updates the virtual wallet
// without sending real orders to any exchange.
public class PaperTradingExecutor(
    ILogger<PaperTradingExecutor> logger,
    IServiceScopeFactory scopeFactory,
    IHubContext<ArbitrageHub> hubContext) : IArbitrageExecutor
{
    private static readonly SemaphoreSlim _walletLock = new SemaphoreSlim(1, 1);

    public async Task ExecuteAsync(ArbitrageOpportunityEvent opportunity, Guid arbitrageEventId)
    {
        // This small delay mimics the round-trip time of a real execution flow.
        await Task.Delay(50);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var execution = new TradeExecutionEntity
        {
            Id = Guid.NewGuid(),
            ArbitrageEventId = arbitrageEventId,
            InvestedUsdt = opportunity.TradeAmountUsdt,
            ExecutedAtUtc = DateTime.UtcNow
        };

        decimal currentBalance = 0;

        // Only one paper trade can update the wallet at a time.
        await _walletLock.WaitAsync();
        try
        {
            var wallet = await db.VirtualWallets.FirstOrDefaultAsync(w => w.Asset == "USDT");
            if (wallet == null) return;

            if (wallet.Balance < opportunity.TradeAmountUsdt)
            {
                // The simulation rejects the trade when the virtual wallet cannot cover the position size.
                execution.Status = TradeExecutionStatus.Failed_InsufficientFunds;
                execution.NetProfitUsdt = 0;
            }
            else
            {
                // Slippage is randomly injected to mimic a realistic exchange execution risk.
                var isSlippage = Random.Shared.NextDouble() < 0.10;

                if (isSlippage)
                {
                    execution.Status = TradeExecutionStatus.Failed_Slippage;
                    execution.NetProfitUsdt = 0;
                }
                else
                {
                    execution.Status = TradeExecutionStatus.Success;
                    execution.NetProfitUsdt = opportunity.NetProfit;
                    wallet.Balance += opportunity.NetProfit;
                    wallet.UpdatedAtUtc = DateTime.UtcNow;
                }
            }

            currentBalance = wallet.Balance;
            db.TradeExecutions.Add(execution);
            await db.SaveChangesAsync();
        }
        finally
        {
            _walletLock.Release();
        }

        // Notify the UI about the new wallet balance and the execution result.
        await hubContext.Clients.All.SendAsync("WalletUpdated", currentBalance);
        await hubContext.Clients.All.SendAsync("ExecutionUpdated", new
        {
            Id = arbitrageEventId,
            Status = execution.Status.ToString()
        });
    }
}