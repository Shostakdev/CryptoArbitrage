using CryptoArbitrage.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoArbitrage.Api.Infrastructure.Persistence;

// This database context stores arbitrage history, trade executions, and wallet state.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ArbitrageEventEntity> SpreadHistory { get; set; }
    public DbSet<VirtualWalletEntity> VirtualWallets { get; set; }
    public DbSet<TradeExecutionEntity> TradeExecutions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ArbitrageEventEntity>(entity =>
        {
            entity.Property(e => e.AvgBuyPrice).HasColumnType("numeric(24,8)");
            entity.Property(e => e.AvgSellPrice).HasColumnType("numeric(24,8)");
            entity.Property(e => e.TradeVolumeUsdt).HasColumnType("numeric(24,8)");
            entity.Property(e => e.NetProfit).HasColumnType("numeric(24,8)");
            entity.Property(e => e.NetProfitPercent).HasColumnType("numeric(24,8)");
            
            entity.Property(e => e.GrossSpreadPercent).HasColumnType("numeric(24,8)");
            entity.Property(e => e.BuyFeeUsdt).HasColumnType("numeric(24,8)");
            entity.Property(e => e.SellFeeUsdt).HasColumnType("numeric(24,8)");
            entity.Property(e => e.TotalFeesUsdt).HasColumnType("numeric(24,8)");
            entity.Property(e => e.CoinsTraded).HasColumnType("numeric(24,8)");
            entity.Property(e => e.GrossProfit).HasColumnType("numeric(24,8)");
        });

        modelBuilder.Entity<VirtualWalletEntity>(entity =>
        {
            entity.Property(e => e.Balance).HasColumnType("numeric(24,8)");
            
            entity.HasData(new VirtualWalletEntity 
            { 
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), 
                Asset = "USDT", 
                Balance = 1000m, 
                UpdatedAtUtc = DateTime.UtcNow 
            });
        });

        modelBuilder.Entity<TradeExecutionEntity>(entity =>
        {
            entity.Property(e => e.InvestedUsdt).HasColumnType("numeric(24,8)");
            entity.Property(e => e.NetProfitUsdt).HasColumnType("numeric(24,8)");
            
            entity.HasOne(e => e.ArbitrageEvent)
                  .WithMany()
                  .HasForeignKey(e => e.ArbitrageEventId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}