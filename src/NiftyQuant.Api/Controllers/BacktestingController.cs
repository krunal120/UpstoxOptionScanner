using Microsoft.AspNetCore.Mvc;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Backtesting;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BacktestingController : ControllerBase
{
    private readonly IBacktestingEngine _backtestEngine;
    private readonly ILogger<BacktestingController> _logger;

    public BacktestingController(
        IBacktestingEngine backtestEngine,
        ILogger<BacktestingController> logger)
    {
        _backtestEngine = backtestEngine;
        _logger = logger;
    }

    [HttpPost("run")]
    public async Task<IActionResult> RunBacktest(
        [FromBody] BacktestRequest request,
        CancellationToken ct = default)
    {
        if (request.FromDate >= request.ToDate)
        {
            return BadRequest(new { message = "FromDate must be strictly earlier than ToDate." });
        }

        if (request.InitialCapital <= 0m)
        {
            return BadRequest(new { message = "InitialCapital must be greater than zero." });
        }

        try
        {
            var result = await _backtestEngine.RunBacktestAsync(request, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backtest execution failed.");
            return StatusCode(500, new { message = "An error occurred during backtest execution.", error = ex.Message });
        }
    }

    [HttpGet("strategies")]
    public IActionResult GetAvailableStrategies()
    {
        var strategies = Enum.GetValues<StrategyFamily>()
            .Where(s => s != StrategyFamily.None)
            .Select(s => new
            {
                Id = (int)s,
                Name = s.ToString(),
                Description = s switch
                {
                    StrategyFamily.TrendFollowing => "15m/5m EMA 9/21 trend alignment, VWAP, Supertrend & momentum",
                    StrategyFamily.Breakout => "Breakouts above PDH / Call Wall / Narrow CPR with RVol >= 1.3x",
                    StrategyFamily.Reversal => "Mean reversion at extreme Put/Call walls & oversold/overbought RSI in Sideways regimes",
                    _ => s.ToString()
                }
            });

        return Ok(strategies);
    }
}
