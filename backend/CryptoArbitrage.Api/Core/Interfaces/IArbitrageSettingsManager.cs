using CryptoArbitrage.Api.Configuration;

namespace CryptoArbitrage.Api.Core.Interfaces;

/// <summary>
/// Exposes the live runtime configuration that the arbitrage engine uses for filtering and execution.
/// </summary>
public interface IArbitrageSettingsManager
{
    /// <summary>
    /// Returns the current engine configuration that is shared across the backend services.
    /// </summary>
    ArbitrageConfig GetCurrentConfig();

    /// <summary>
    /// Updates the core risk and profit thresholds that control whether a spread is considered tradeable.
    /// </summary>
    void UpdateCoreSettings(decimal tradeAmountUsdt, decimal minNetProfitUsdt, decimal minNetProfitPercent, decimal takerFeeRate);
}