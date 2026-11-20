using Microsoft.AspNetCore.Mvc;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.PaperTrading;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaperTradingController : ControllerBase
{
    private readonly IPaperTradingEngine _paperEngine;
    private readonly IStrategyEngine _strategyEngine;
    private readonly IAutonomousTradingService _autoTradingService;
    private readonly ILogger<PaperTradingController> _logger;

    public PaperTradingController(
        IPaperTradingEngine paperEngine,
        IStrategyEngine strategyEngine,
        IAutonomousTradingService autoTradingService,
        ILogger<PaperTradingController> logger)
    {
        _paperEngine = paperEngine;
        _strategyEngine = strategyEngine;
        _autoTradingService = autoTradingService;
        _logger = logger;
    }

    [HttpGet("auto-trade/status")]
    public IActionResult GetAutoTradingStatus()
    {
        var status = _autoTradingService.GetStatus();
        return Ok(status);
    }

    [HttpPost("auto-trade/toggle")]
    public async Task<IActionResult> ToggleAutoTrading(
        [FromBody] ToggleAutoTradingRequest request,
        CancellationToken ct = default)
    {
        if (request.Enabled.HasValue)
        {
            await _autoTradingService.SetEnabledAsync(request.Enabled.Value, ct);
        }
        if (request.MinConfidence.HasValue)
        {
            _autoTradingService.MinConfidenceThreshold = request.MinConfidence.Value;
        }
        if (request.MarketHoursOnly.HasValue)
        {
            _autoTradingService.IsMarketHoursOnly = request.MarketHoursOnly.Value;
        }

        return Ok(_autoTradingService.GetStatus());
    }

    [HttpPost("auto-trade/test-execute")]
    public async Task<IActionResult> TestExecuteOrder(
        [FromBody] ForceSignalExecutionRequest request,
        CancellationToken ct = default)
    {
        var result = await _autoTradingService.ForceTriggerSignalAndExecuteAsync(request, ct);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("execute-signal")]
    public async Task<IActionResult> ExecuteSignal(
        [FromBody] StrategySignal? signal = null,
        [FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50",
        [FromQuery] bool simulateSlippage = true,
        CancellationToken ct = default)
    {
        signal ??= await _strategyEngine.GetLatestSignalAsync(underlyingKey, ct);
        if (signal == null)
        {
            return BadRequest(new { message = $"No signal provided and no recorded signal found for {underlyingKey} to execute." });
        }

        var result = await _paperEngine.ExecuteSignalAsync(signal, simulateSlippage, ct);
        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("positions")]
    public async Task<IActionResult> GetOpenPositions(CancellationToken ct = default)
    {
        var positions = await _paperEngine.GetOpenPositionsAsync(ct);
        return Ok(positions);
    }

    [HttpGet("trades")]
    public async Task<IActionResult> GetTradeHistory(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var trades = await _paperEngine.GetTradeHistoryAsync(limit, ct);
        return Ok(trades);
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrderHistory(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var orders = await _paperEngine.GetOrderHistoryAsync(limit, ct);
        return Ok(orders);
    }

    [HttpPost("close-position")]
    public async Task<IActionResult> ClosePosition(
        [FromBody] ClosePositionRequest request,
        CancellationToken ct = default)
    {
        var result = await _paperEngine.ClosePositionAsync(
            request.PositionId, request.ExitReason, request.ExitPrice, ct);

        if (!result.IsSuccess)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpPost("square-off-all")]
    public async Task<IActionResult> SquareOffAll(
        [FromQuery] string reason = "ManualSquareoff",
        CancellationToken ct = default)
    {
        var results = await _paperEngine.SquareOffAllPositionsAsync(reason, ct);
        return Ok(new
        {
            message = $"Squared off {results.Count} active positions.",
            results
        });
    }

    [HttpGet("portfolio")]
    public async Task<IActionResult> GetPortfolioSummary(CancellationToken ct = default)
    {
        var summary = await _paperEngine.GetPortfolioSummaryAsync(ct);
        return Ok(summary);
    }
}
