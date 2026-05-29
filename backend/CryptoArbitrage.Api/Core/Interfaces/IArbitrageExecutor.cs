using CryptoArbitrage.Api.Core.Models;

namespace CryptoArbitrage.Api.Core.Interfaces;

/// <summary>
/// Runs the execution pipeline for a discovered arbitrage opportunity.
/// </summary>
public interface IArbitrageExecutor
{
    /// <summary>
    /// Executes a paper-trading or live-trading flow for the provided opportunity.
    /// </summary>
    Task ExecuteAsync(ArbitrageOpportunityEvent opportunity, Guid arbitrageEventId);
}