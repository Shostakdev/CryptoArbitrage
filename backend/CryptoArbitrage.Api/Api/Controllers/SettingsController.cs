using CryptoArbitrage.Api.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CryptoArbitrage.Api.Api.Controllers;

// This controller reads and updates the live trading settings used by the engine.
[ApiController]
[Route("api/[controller]")]
public class SettingsController(IArbitrageSettingsManager settingsManager) : ControllerBase
{
    [HttpGet]
    public IActionResult GetSettings()
    {
        var config = settingsManager.GetCurrentConfig();
        return Ok(new
        {
            config.TradeAmountUsdt,
            config.MinNetProfitUsdt,
            config.MinNetProfitPercent,
            config.TakerFeeRate
        });
    }

    [HttpPost]
    public IActionResult UpdateSettings([FromBody] UpdateSettingsRequest request)
    {
        if (request == null)
        {
            return BadRequest("Payload is empty.");
        }

        if (request.TradeAmountUsdt <= 0 || request.TakerFeeRate < 0 || request.TakerFeeRate >= 1)
        {
            return BadRequest("Invalid settings parameters. Trade amount must be > 0 and fee rate must be [0, 1).");
        }

        settingsManager.UpdateCoreSettings(
            request.TradeAmountUsdt,
            request.MinNetProfitUsdt,
            request.MinNetProfitPercent,
            request.TakerFeeRate
        );

        return Ok(new { Message = "Settings updated successfully" });
    }
}

public class UpdateSettingsRequest
{
    public decimal TradeAmountUsdt { get; set; }
    public decimal MinNetProfitUsdt { get; set; }
    public decimal MinNetProfitPercent { get; set; }
    public decimal TakerFeeRate { get; set; }
}