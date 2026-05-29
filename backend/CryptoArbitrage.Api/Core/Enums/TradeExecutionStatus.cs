namespace CryptoArbitrage.Api.Core.Enums;

/// <summary>
/// Tracks the outcome of a simulated trading execution.
/// </summary>
public enum TradeExecutionStatus
{
    /// <summary>
    /// The execution is waiting to be evaluated or applied.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The paper trade completed successfully.
    /// </summary>
    Success = 1,

    /// <summary>
    /// The execution failed because the price moved beyond the acceptable slippage threshold.
    /// </summary>
    Failed_Slippage = 2,

    /// <summary>
    /// The execution failed because the virtual wallet did not have enough balance.
    /// </summary>
    Failed_InsufficientFunds = 3
}