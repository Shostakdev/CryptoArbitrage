using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CryptoArbitrage.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Persistent snapshot of a virtual wallet asset balance used by paper trading.
/// </summary>
[Table("virtual_wallets")]
public class VirtualWalletEntity
{
    /// <summary>Primary key for the wallet record.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>The asset symbol, such as USDT or BTC.</summary>
    [MaxLength(20)]
    public string Asset { get; set; } = null!;

    /// <summary>The current virtual balance for the asset.</summary>
    public decimal Balance { get; set; }

    /// <summary>The UTC timestamp of the last balance update.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}