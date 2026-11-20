using Microsoft.AspNetCore.Mvc;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OptionsController : ControllerBase
{
    private readonly IOptionsIntelligenceService _optionsIntelligence;
    private readonly IOptionChainSnapshotEngine _snapshotEngine;
    private readonly ILogger<OptionsController> _logger;

    public OptionsController(
        IOptionsIntelligenceService optionsIntelligence,
        IOptionChainSnapshotEngine snapshotEngine,
        ILogger<OptionsController> logger)
    {
        _optionsIntelligence = optionsIntelligence;
        _snapshotEngine = snapshotEngine;
        _logger = logger;
    }

    [HttpGet("intelligence")]
    public async Task<IActionResult> GetOptionsIntelligence(
        [FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50",
        [FromQuery] string? expiryDate = null,
        CancellationToken ct = default)
    {
        var snapshot = await _snapshotEngine.GetLatestSnapshotAsync(underlyingKey, expiryDate, ct)
                       ?? await _snapshotEngine.CaptureAndPersistSnapshotAsync(underlyingKey, expiryDate, ct: ct);

        if (snapshot == null)
        {
            return NotFound(new { message = $"No option chain snapshot available for {underlyingKey}" });
        }

        var report = _optionsIntelligence.AnalyzeSnapshot(snapshot);
        return Ok(report);
    }

    [HttpGet("select-contract")]
    public async Task<IActionResult> SelectContract(
        [FromQuery] OptionType optionType = OptionType.CE,
        [FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50",
        [FromQuery] decimal minLiquidityScore = 40m,
        CancellationToken ct = default)
    {
        var snapshot = await _snapshotEngine.GetLatestSnapshotAsync(underlyingKey, ct)
                       ?? await _snapshotEngine.CaptureAndPersistSnapshotAsync(underlyingKey, ct: ct);

        if (snapshot == null || snapshot.Quotes.Count == 0)
        {
            return NotFound(new { message = $"No option quotes available for {underlyingKey}" });
        }

        var candidate = _optionsIntelligence.SelectBestContract(
            snapshot.Quotes, optionType, snapshot.UnderlyingPrice, minLiquidityScore);

        if (candidate == null)
        {
            return NotFound(new { message = $"No suitable {optionType} contract found meeting liquidity criteria." });
        }

        return Ok(candidate);
    }

    [HttpGet("pcr")]
    public async Task<IActionResult> GetPcrAnalysis(
        [FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50",
        CancellationToken ct = default)
    {
        var snapshot = await _snapshotEngine.GetLatestSnapshotAsync(underlyingKey, ct)
                       ?? await _snapshotEngine.CaptureAndPersistSnapshotAsync(underlyingKey, ct: ct);

        if (snapshot == null)
        {
            return NotFound(new { message = $"No snapshot available for {underlyingKey}" });
        }

        var report = _optionsIntelligence.AnalyzeSnapshot(snapshot);
        return Ok(report.PcrAnalysis);
    }

    [HttpGet("levels")]
    public async Task<IActionResult> GetKeyLevels(
        [FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50",
        CancellationToken ct = default)
    {
        var snapshot = await _snapshotEngine.GetLatestSnapshotAsync(underlyingKey, ct)
                       ?? await _snapshotEngine.CaptureAndPersistSnapshotAsync(underlyingKey, ct: ct);

        if (snapshot == null)
        {
            return NotFound(new { message = $"No snapshot available for {underlyingKey}" });
        }

        var report = _optionsIntelligence.AnalyzeSnapshot(snapshot);
        return Ok(report.KeyLevels);
    }
}
