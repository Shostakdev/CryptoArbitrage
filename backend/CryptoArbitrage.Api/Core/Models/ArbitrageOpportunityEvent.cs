namespace CryptoArbitrage.Api.Core.Models;

/// <summary>
/// Represents a single detected arbitrage opportunity, including the buy/sell route,
/// size, fees, and the profit metrics produced by the engine.
/// </summary>
public record ArbitrageOpportunityEvent(
    /// <summary>The trading pair, such as BTC/USDT.</summary>
    string Symbol,
    /// <summary>The exchange where the asset should be bought.</summary>
    string BuyExchange,
    /// <summary>The execution price on the buy side.</summary>
    decimal BuyPrice,
    /// <summary>The exchange where the asset should be sold.</summary>
    string SellExchange,
    /// <summary>The execution price on the sell side.</summary>
    decimal SellPrice,
    /// <summary>The notional volume used when evaluating the spread.</summary>
    decimal TradeAmountUsdt,
    /// <summary>The gross spread percentage between the two sides.</summary>
    decimal GrossSpreadPercent,
    /// <summary>The fee paid on the buy leg.</summary>
    decimal BuyFeeUsdt,
    /// <summary>The fee paid on the sell leg.</summary>
    decimal SellFeeUsdt,
    /// <summary>The combined execution cost across both legs.</summary>
    decimal TotalFeesUsdt,
    /// <summary>The amount of base asset bought after the buy-side execution.</summary>
    decimal CoinsBought,
    /// <summary>The amount of base asset remaining after deducting the buy-side fee.</summary>
    decimal CoinsAfterBuyFee,
    /// <summary>The amount of base asset sold on the exit side.</summary>
    decimal CoinsSold,
    /// <summary>The spread profit before fees are deducted.</summary>
    decimal GrossProfit,
    /// <summary>The net profit after applying both legs' fees.</summary>
    decimal NetProfit,
    /// <summary>The net profit expressed as a percentage of the trade notional.</summary>
    decimal NetProfitPercent,
    /// <summary>The settlement mode attached to the opportunity.</summary>
    string SettlementMode,
    /// <summary>The UTC timestamp when the opportunity was discovered.</summary>
    DateTime Timestamp
);
