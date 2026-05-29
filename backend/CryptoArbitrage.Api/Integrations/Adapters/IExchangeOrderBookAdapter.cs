namespace CryptoArbitrage.Api.Integrations.Adapters;

/// <summary>
/// Describes the minimal contract that an exchange-specific order book adapter must implement.
/// </summary>
public interface IExchangeOrderBookAdapter
{
    /// <summary>The exchange identifier exposed by the adapter.</summary>
    string ExchangeName { get; }

    /// <summary>The symbols that the adapter currently subscribes to.</summary>
    IReadOnlyCollection<string> Symbols { get; }
}
