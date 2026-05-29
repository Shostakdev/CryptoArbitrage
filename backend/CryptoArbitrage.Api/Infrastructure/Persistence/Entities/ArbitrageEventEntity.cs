using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CryptoArbitrage.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Persistent record of a discovered arbitrage opportunity stored in the spread history table.
/// </summary>
[Table("spread_history")]
public class ArbitrageEventEntity
{
    /// <summary>Primary key for the stored arbitrage event.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>The trading pair that was evaluated.</summary>
    [MaxLength(20)]
    public string Symbol { get; set; } = null!;

    /// <summary>The exchange where the asset was bought.</summary>
    [MaxLength(50)]
    public string BuyExchange { get; set; } = null!;

    /// <summary>The exchange where the asset was sold.</summary>
    [MaxLength(50)]
    public string SellExchange { get; set; } = null!;

    /// <summary>The average buy price observed on the entry side.</summary>
    public decimal AvgBuyPrice { get; set; }

    /// <summary>The average sell price observed on the exit side.</summary>
    public decimal AvgSellPrice { get; set; }

    /// <summary>The trade notional in USDT used for the opportunity.</summary>
    public decimal TradeVolumeUsdt { get; set; }

    /// <summary>The gross percentage spread before fees.</summary>
    public decimal GrossSpreadPercent { get; set; }

    /// <summary>The fee paid on the buy leg.</summary>
    public decimal BuyFeeUsdt { get; set; }

    /// <summary>The fee paid on the sell leg.</summary>
    public decimal SellFeeUsdt { get; set; }

    /// <summary>The combined total of both execution fees.</summary>
    public decimal TotalFeesUsdt { get; set; }

    /// <summary>The quantity of base asset traded through the route.</summary>
    public decimal CoinsTraded { get; set; }

    /// <summary>The gross profit before fees are deducted.</summary>
    public decimal GrossProfit { get; set; }

    /// <summary>The net profit after fees are deducted.</summary>
    public decimal NetProfit { get; set; }

    /// <summary>The net profit percentage after fees.</summary>
    public decimal NetProfitPercent { get; set; }

    /// <summary>The settlement mode stored with the event.</summary>
    [MaxLength(20)]
    public string SettlementMode { get; set; } = null!;

    /// <summary>The UTC timestamp when the opportunity was recorded.</summary>
    public DateTime CreatedAtUtc { get; set; }
}