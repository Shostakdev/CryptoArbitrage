using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CryptoArbitrage.Api.Infrastructure.Persistence;

namespace CryptoArbitrage.Api.Api.Controllers;

// This controller exposes the latest arbitrage events for the dashboard.
[ApiController]
[Route("api/[controller]")]
public class HistoryController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public HistoryController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory()
    {
        var history = await _dbContext.SpreadHistory
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100) 
            .ToListAsync();

        return Ok(history);
    }
}