using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure.Persistence;

namespace NiftyQuant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketController : ControllerBase
{
    private readonly IUpstoxClient _upstoxClient;
    private readonly NiftyQuantDbContext _dbContext;
    private readonly ILogger<MarketController> _logger;

    public MarketController(
        IUpstoxClient upstoxClient,
        NiftyQuantDbContext dbContext,
        ILogger<MarketController> logger)
    {
        _upstoxClient = upstoxClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetMarketOverview(CancellationToken ct)
    {
        var quote = await _upstoxClient.GetMarketQuoteAsync("NSE_INDEX|Nifty 50", ct);
        var vixQuote = await _upstoxClient.GetMarketQuoteAsync("NSE_INDEX|India VIX", ct);

        return Ok(new
        {
            underlying = "NIFTY 50",
            instrumentKey = "NSE_INDEX|Nifty 50",
            lastPrice = quote?.LastPrice ?? 0m,
            netChange = quote?.NetChange ?? 0m,
            ohlc = quote?.Ohlc,
            indiaVix = vixQuote?.LastPrice ?? 0m,
            vixChange = vixQuote?.NetChange ?? 0m,
            timestamp = quote?.Timestamp,
            status = quote != null ? "Live" : "Unavailable"
        });
    }

    [HttpGet("quote")]
    public async Task<IActionResult> GetQuote([FromQuery] string instrumentKey = "NSE_INDEX|Nifty 50", CancellationToken ct = default)
    {
        var quote = await _upstoxClient.GetMarketQuoteAsync(instrumentKey, ct);
        if (quote == null) return NotFound(new { message = $"No quote found for {instrumentKey}" });
        return Ok(quote);
    }

    [HttpGet("quotes")]
    public async Task<IActionResult> GetQuotes([FromQuery] string keys = "NSE_INDEX|Nifty 50,NSE_INDEX|India VIX", CancellationToken ct = default)
    {
        var keyList = keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var quotes = await _upstoxClient.GetMarketQuotesAsync(keyList, ct);
        return Ok(quotes);
    }

    [HttpGet("expiries")]
    public async Task<IActionResult> GetExpiries([FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50", CancellationToken ct = default)
    {
        var contracts = await _upstoxClient.GetOptionContractsAsync(underlyingKey, ct);
        var expiries = contracts
            .Select(c => c.Expiry)
            .Distinct()
            .OrderBy(e => e)
            .ToList();

        return Ok(expiries);
    }

    [HttpGet("option-chain")]
    public async Task<IActionResult> GetOptionChain(
        [FromQuery] string? expiryDate = null,
        [FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50",
        CancellationToken ct = default)
    {
        // If expiryDate is omitted, pick the earliest available expiry
        if (string.IsNullOrWhiteSpace(expiryDate))
        {
            var contracts = await _upstoxClient.GetOptionContractsAsync(underlyingKey, ct);
            expiryDate = contracts.Select(c => c.Expiry).Distinct().OrderBy(e => e).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(expiryDate))
            {
                return BadRequest(new { message = "No active option expiries found for " + underlyingKey });
            }
        }

        var chain = await _upstoxClient.GetOptionChainAsync(underlyingKey, expiryDate, ct);
        return Ok(new
        {
            underlyingKey,
            expiryDate,
            strikeCount = chain.Count,
            chain
        });
    }

    [HttpGet("candles")]
    public async Task<IActionResult> GetCandles(
        [FromQuery] string instrumentKey = "NSE_INDEX|Nifty 50",
        [FromQuery] string unit = "minutes",
        [FromQuery] int interval = 1,
        [FromQuery] int days = 2,
        CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var toDate = now.ToString("yyyy-MM-dd");
        var fromDate = now.AddDays(-days).ToString("yyyy-MM-dd");

        var candles = await _upstoxClient.GetHistoricalCandlesAsync(instrumentKey, unit, interval, toDate, fromDate, ct);
        return Ok(new
        {
            instrumentKey,
            unit,
            interval,
            fromDate,
            toDate,
            count = candles.Count,
            candles
        });
    }

    [HttpPost("sync-contracts")]
    public async Task<IActionResult> SyncOptionContracts([FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50", CancellationToken ct = default)
    {
        _logger.LogInformation("Starting option contracts synchronization for {UnderlyingKey}", underlyingKey);
        var remoteContracts = await _upstoxClient.GetOptionContractsAsync(underlyingKey, ct);

        if (remoteContracts.Count == 0)
        {
            return BadRequest(new { message = "No contracts returned by Upstox API." });
        }

        int synced = 0;
        foreach (var c in remoteContracts)
        {
            if (!DateTime.TryParse(c.Expiry, out var expDate)) continue;

            var existing = await _dbContext.OptionContracts.FindAsync(new object[] { c.InstrumentKey }, ct);
            if (existing == null)
            {
                _dbContext.OptionContracts.Add(new OptionContract
                {
                    InstrumentKey = c.InstrumentKey,
                    UnderlyingKey = c.UnderlyingKey,
                    StrikePrice = c.StrikePrice,
                    OptionType = string.Equals(c.InstrumentType, "CE", StringComparison.OrdinalIgnoreCase) ? OptionType.CE : OptionType.PE,
                    ExpiryDate = expDate,
                    LotSize = c.LotSize,
                    IsActive = true
                });
            }
            else
            {
                existing.StrikePrice = c.StrikePrice;
                existing.LotSize = c.LotSize;
                existing.ExpiryDate = expDate;
                existing.IsActive = true;
            }
            synced++;
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Synchronized {Count} option contracts into database", synced);

        return Ok(new
        {
            underlyingKey,
            contractsProcessed = remoteContracts.Count,
            contractsSaved = synced,
            timestampUtc = DateTime.UtcNow
        });
    }
}
