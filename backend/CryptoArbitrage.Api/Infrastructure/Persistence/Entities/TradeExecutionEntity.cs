using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CryptoArbitrage.Api.Core.Enums;

namespace CryptoArbitrage.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Stores the outcome of a paper-trading execution linked to an arbitrage event.
/// </summary>
[Table("trade_executions")]
public class TradeExecutionEntity
{
    /// <summary>Primary key for the execution record.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Foreign key pointing to the originating arbitrage opportunity.</summary>
    public Guid ArbitrageEventId { get; set; }

    /// <summary>The parent arbitrage event that produced this execution.</summary>
    [ForeignKey(nameof(ArbitrageEventId))]
    public ArbitrageEventEntity ArbitrageEvent { get; set; } = null!;

    /// <summary>The execution outcome reported by the engine.</summary>
    public TradeExecutionStatus Status { get; set; }

    /// <summary>The amount of capital allocated to the trade.</summary>
    public decimal InvestedUsdt { get; set; }

    /// <summary>The net profit or loss produced by the execution.</summary>
    public decimal NetProfitUsdt { get; set; }

    /// <summary>The UTC timestamp when the execution was recorded.</summary>
    public DateTime ExecutedAtUtc { get; set; }
}