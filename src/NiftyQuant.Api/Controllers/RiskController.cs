using Microsoft.AspNetCore.Mvc;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Risk;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RiskController : ControllerBase
{
    private readonly IRiskManagementService _riskService;
    private readonly IStrategyEngine _strategyEngine;
    private readonly ILogger<RiskController> _logger;

    public RiskController(
        IRiskManagementService riskService,
        IStrategyEngine strategyEngine,
        ILogger<RiskController> logger)
    {
        _riskService = riskService;
        _strategyEngine = strategyEngine;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetDailyRiskStatus(CancellationToken ct = default)
    {
        var status = await _riskService.GetDailyRiskStatusAsync(ct);
        return Ok(status);
    }

    [HttpGet("account")]
    public async Task<IActionResult> GetAccount(CancellationToken ct = default)
    {
        var account = await _riskService.GetOrCreateDefaultAccountAsync(ct);
        return Ok(account);
    }

    [HttpPost("validate-signal")]
    public async Task<IActionResult> ValidatePreTrade(
        [FromBody] StrategySignal? signal = null,
        [FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50",
        CancellationToken ct = default)
    {
        signal ??= await _strategyEngine.GetLatestSignalAsync(underlyingKey, ct);
        if (signal == null)
        {
            return BadRequest(new { message = $"No signal provided and no recorded signal found for {underlyingKey} to validate." });
        }

        var validationResult = await _riskService.ValidatePreTradeAsync(signal, ct);
        return Ok(validationResult);
    }

    [HttpPost("reset-daily-limits")]
    public async Task<IActionResult> ResetDailyLimits(CancellationToken ct = default)
    {
        await _riskService.ResetDailyLimitsAsync(ct);
        var status = await _riskService.GetDailyRiskStatusAsync(ct);
        return Ok(new
        {
            message = "Daily limits and circuit breakers reset successfully.",
            status
        });
    }

    [HttpGet("position-size")]
    public async Task<IActionResult> CalculatePositionSize(
        [FromQuery] decimal entryPremium,
        [FromQuery] decimal stopLossPremium,
        [FromQuery] decimal? availableBalance = null,
        CancellationToken ct = default)
    {
        if (entryPremium <= 0 || stopLossPremium <= 0)
        {
            return BadRequest(new { message = "Entry and stop loss premiums must be greater than zero." });
        }

        decimal balance = availableBalance ?? (await _riskService.GetOrCreateDefaultAccountAsync(ct)).AvailableBalance;
        var sizing = _riskService.CalculatePositionSize(entryPremium, stopLossPremium, balance);

        return Ok(sizing);
    }
}
