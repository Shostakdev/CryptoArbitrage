using CryptoArbitrage.Api.Configuration;
using CryptoArbitrage.Api.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace CryptoArbitrage.Api.Engine;

// This manager keeps the current runtime settings in one place and applies updates safely.
public class ArbitrageSettingsManager : IArbitrageSettingsManager
{
    private readonly object _lock = new();
    private ArbitrageConfig _currentConfig;

    public ArbitrageSettingsManager(IOptions<ArbitrageConfig> initialConfig)
    {
        _currentConfig = initialConfig.Value;
    }

    public ArbitrageConfig GetCurrentConfig()
    {
        lock (_lock)
        {
            return _currentConfig;
        }
    }

    // This rebuilds the runtime config so the engine can change thresholds without restarting.
    public void UpdateCoreSettings(decimal tradeAmountUsdt, decimal minNetProfitUsdt, decimal minNetProfitPercent, decimal takerFeeRate)
    {
        lock (_lock)
        {
            _currentConfig = new ArbitrageConfig
            {
                SettlementMode = _currentConfig.SettlementMode,
                TradingPairs = _currentConfig.TradingPairs,
                DisabledExchanges = _currentConfig.DisabledExchanges,
                DisabledPairs = _currentConfig.DisabledPairs,
                ExchangeFees = _currentConfig.ExchangeFees,
                TradingRules = _currentConfig.TradingRules,
                
                TradeAmountUsdt = tradeAmountUsdt,
                MinNetProfitUsdt = minNetProfitUsdt,
                MinNetProfitPercent = minNetProfitPercent,
                TakerFeeRate = takerFeeRate
            };
        }
    }
}