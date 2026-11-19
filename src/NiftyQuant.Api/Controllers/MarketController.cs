using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NiftyQuant.Infrastructure.Persistence;

namespace NiftyQuant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketController : ControllerBase
{
    private readonly NiftyQuantDbContext _dbContext;
    private readonly ILogger<MarketController> _logger;

    public MarketController(NiftyQuantDbContext dbContext, ILogger<MarketController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetMarketOverview(CancellationToken ct)
    {
        var latestCandle = await _dbContext.MarketCandles
            .Where(c => c.InstrumentKey == "NSE_INDEX|Nifty 50")
            .OrderByDescending(c => c.CandleTimeUtc)
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            underlying = "NIFTY 50",
            instrumentKey = "NSE_INDEX|Nifty 50",
            lastPrice = latestCandle?.Close ?? 0m,
            lastUpdatedUtc = latestCandle?.CandleTimeUtc,
            status = "Ready"
        });
    }

    [HttpGet("expiries")]
    public async Task<IActionResult> GetExpiries(CancellationToken ct)
    {
        var expiries = await _dbContext.OptionContracts
            .Where(c => c.IsActive)
            .Select(c => c.ExpiryDate.Date)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync(ct);

        return Ok(expiries);
    }
}
