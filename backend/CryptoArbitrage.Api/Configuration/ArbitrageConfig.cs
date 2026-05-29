namespace CryptoArbitrage.Api.Configuration;

/// <summary>
/// Global runtime settings that describe which exchanges are monitored, how trades are sized,
/// and which profit thresholds must be met before an opportunity is accepted.
/// </summary>
public class ArbitrageConfig
{
    /// <summary>
    /// Exchange identifiers that the engine should subscribe to for live market data.
    /// </summary>
    public List<string> Exchanges { get; init; } = new();

    /// <summary>
    /// Trading pairs that should be scanned for arbitrage opportunities.
    /// </summary>
    public List<string> TradingPairs { get; init; } = new();

    /// <summary>
    /// Fallback taker fee rate used when an exchange-specific fee is not configured.
    /// </summary>
    public decimal TakerFeeRate { get; init; } = 0.001m;

    /// <summary>
    /// Default notional value in USDT that the engine uses for each simulated trade.
    /// </summary>
    public decimal TradeAmountUsdt { get; init; } = 100m;

    /// <summary>
    /// The settlement mode that is reported alongside opportunities and trades.
    /// </summary>
    public string SettlementMode { get; init; } = "Inventory";

    /// <summary>
    /// Minimum net profit in USDT required for an opportunity to be considered executable.
    /// </summary>
    public decimal MinNetProfitUsdt { get; init; } = 0m;

    /// <summary>
    /// Minimum net profit percentage required for an opportunity to be considered executable.
    /// </summary>
    public decimal MinNetProfitPercent { get; init; } = 0m;

    /// <summary>
    /// Per-exchange fee settings used to calculate accurate execution costs.
    /// </summary>
    public Dictionary<string, ExchangeFeeConfig> ExchangeFees { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Exchange-specific trading rules that define minimum sizes and allowed price increments.
    /// </summary>
    public Dictionary<string, TradingRuleConfig> TradingRules { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Exchanges that are temporarily disabled and should be skipped during scanning.
    /// </summary>
    public List<string> DisabledExchanges { get; init; } = new();

    /// <summary>
    /// Trading pairs that are temporarily disabled and should be skipped during scanning.
    /// </summary>
    public List<string> DisabledPairs { get; init; } = new();
}

/// <summary>
/// Fee parameters that can differ per exchange.
/// </summary>
public sealed class ExchangeFeeConfig
{
    /// <summary>
    /// Taker fee rate charged when immediate execution is required.
    /// </summary>
    public decimal TakerFeeRate { get; init; } = 0.001m;

    /// <summary>
    /// Maker fee rate charged when resting liquidity is used.
    /// </summary>
    public decimal MakerFeeRate { get; init; } = 0.001m;
}

/// <summary>
/// Trading constraints that define whether a symbol can be traded at the current market depth.
/// </summary>
public sealed class TradingRuleConfig
{
    /// <summary>
    /// Minimum notional value required before a trade is valid.
    /// </summary>
    public decimal MinNotional { get; init; }

    /// <summary>
    /// Minimum quantity required for a valid order.
    /// </summary>
    public decimal MinQuantity { get; init; }

    /// <summary>
    /// Quantity precision step accepted by the exchange.
    /// </summary>
    public decimal QuantityStep { get; init; }

    /// <summary>
    /// Minimum price increment accepted by the exchange.
    /// </summary>
    public decimal PriceTick { get; init; }

    /// <summary>
    /// Minimum available depth in USDT needed before the opportunity is considered actionable.
    /// </summary>
    public decimal MinDepthUsdt { get; init; }
}
