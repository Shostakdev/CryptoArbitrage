using CryptoArbitrage.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoArbitrage.Api.Api.Controllers;

// This controller returns the current balance of the virtual paper wallet.
[ApiController]
[Route("api/[controller]")]
public class WalletController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBalance()
    {
        var wallet = await db.VirtualWallets.AsNoTracking().FirstOrDefaultAsync(w => w.Asset == "USDT");
        return Ok(new { Balance = wallet?.Balance ?? 1000m });
    }
}