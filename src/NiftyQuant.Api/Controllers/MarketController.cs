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
    private readonly IUpstoxWebSocketClient _webSocketClient;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ICandleAggregationEngine _candleEngine;
    private readonly IOptionChainSnapshotEngine _snapshotEngine;
    private readonly NiftyQuantDbContext _dbContext;
    private readonly ILogger<MarketController> _logger;

    public MarketController(
        IUpstoxClient upstoxClient,
        IUpstoxWebSocketClient webSocketClient,
        ISubscriptionManager subscriptionManager,
        ICandleAggregationEngine candleEngine,
        IOptionChainSnapshotEngine snapshotEngine,
        NiftyQuantDbContext dbContext,
        ILogger<MarketController> logger)
    {
        _upstoxClient = upstoxClient;
        _webSocketClient = webSocketClient;
        _subscriptionManager = subscriptionManager;
        _candleEngine = candleEngine;
        _snapshotEngine = snapshotEngine;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("latest-snapshot")]
    public async Task<IActionResult> GetLatestSnapshot([FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50", CancellationToken ct = default)
    {
        var snapshot = await _snapshotEngine.GetLatestSnapshotAsync(underlyingKey, ct);
        if (snapshot == null) return NotFound(new { message = $"No stored snapshots found for {underlyingKey}" });
        return Ok(snapshot);
    }

    [HttpPost("capture-snapshot")]
    public async Task<IActionResult> CaptureSnapshot(
        [FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50",
        [FromQuery] string? expiryDate = null,
        CancellationToken ct = default)
    {
        var snapshot = await _snapshotEngine.CaptureAndPersistSnapshotAsync(underlyingKey, expiryDate, ct);
        if (snapshot == null) return StatusCode(500, new { message = "Failed to capture option chain snapshot" });
        return Ok(snapshot);
    }

    [HttpGet("live-candle")]
    public IActionResult GetLiveCandle(
        [FromQuery] string instrumentKey = "NSE_INDEX|Nifty 50",
        [FromQuery] int timeframeMinutes = 1)
    {
        var tf = timeframeMinutes switch
        {
            5 => CandleTimeframe.FiveMinute,
            15 => CandleTimeframe.FifteenMinute,
            _ => CandleTimeframe.OneMinute
        };

        var candle = _candleEngine.GetCurrentCandle(instrumentKey, tf);
        if (candle == null) return NotFound(new { message = $"No active {tf} candle forming for {instrumentKey}" });
        return Ok(candle);
    }

    [HttpGet("stream-status")]
    public IActionResult GetStreamStatus()
    {
        return Ok(new
        {
            isConnected = _webSocketClient.IsConnected,
            currentAtmStrike = _subscriptionManager.CurrentAtmStrike,
            currentExpiry = _subscriptionManager.CurrentExpiry,
            subscribedCount = _webSocketClient.SubscribedInstruments.Count,
            subscribedInstruments = _webSocketClient.SubscribedInstruments
        });
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetMarketOverview(CancellationToken ct)
    {
        var keys = new[] { "NSE_INDEX|Nifty 50", "NSE_INDEX|Nifty Bank", "BSE_INDEX|SENSEX", "NSE_INDEX|India VIX" };
        var quotes = await _upstoxClient.GetMarketQuotesAsync(keys, ct);

        quotes.TryGetValue("NSE_INDEX|Nifty 50", out var niftyQuote);
        quotes.TryGetValue("NSE_INDEX|Nifty Bank", out var bankQuote);
        quotes.TryGetValue("BSE_INDEX|SENSEX", out var sensexQuote);
        quotes.TryGetValue("NSE_INDEX|India VIX", out var vixQuote);

        var niftyClose = niftyQuote?.Ohlc?.Close ?? niftyQuote?.LastPrice ?? 0m;
        var niftyChange = niftyQuote != null && niftyClose > 0m ? (niftyQuote.LastPrice - niftyClose) : (niftyQuote?.NetChange ?? 0m);
        var niftyChangePct = niftyClose > 0m ? Math.Round((niftyChange / niftyClose) * 100m, 2) : 0m;

        var bankClose = bankQuote?.Ohlc?.Close ?? bankQuote?.LastPrice ?? 0m;
        var bankChange = bankQuote != null && bankClose > 0m ? (bankQuote.LastPrice - bankClose) : (bankQuote?.NetChange ?? 0m);
        var bankChangePct = bankClose > 0m ? Math.Round((bankChange / bankClose) * 100m, 2) : 0m;

        var sensexClose = sensexQuote?.Ohlc?.Close ?? sensexQuote?.LastPrice ?? 0m;
        var sensexChange = sensexQuote != null && sensexClose > 0m ? (sensexQuote.LastPrice - sensexClose) : (sensexQuote?.NetChange ?? 0m);
        var sensexChangePct = sensexClose > 0m ? Math.Round((sensexChange / sensexClose) * 100m, 2) : 0m;

        var vixClose = vixQuote?.Ohlc?.Close ?? vixQuote?.LastPrice ?? 0m;
        var vixChange = vixQuote != null && vixClose > 0m ? (vixQuote.LastPrice - vixClose) : (vixQuote?.NetChange ?? 0m);
        var vixChangePct = vixClose > 0m ? Math.Round((vixChange / vixClose) * 100m, 2) : 0m;

        return Ok(new
        {
            underlying = "NIFTY 50",
            instrumentKey = "NSE_INDEX|Nifty 50",
            lastPrice = niftyQuote?.LastPrice ?? 0m,
            netChange = niftyChange,
            ohlc = niftyQuote?.Ohlc,
            indiaVix = new
            {
                lastPrice = vixQuote?.LastPrice ?? 0m,
                change = vixChange,
                changePercent = vixChangePct
            },
            vixChange = vixChange,
            timestamp = niftyQuote?.Timestamp,
            timestampUtc = DateTime.UtcNow.ToString("o"),
            status = niftyQuote != null ? "Live" : "Unavailable",
            niftySpot = new
            {
                lastPrice = niftyQuote?.LastPrice ?? 0m,
                change = niftyChange,
                changePercent = niftyChangePct,
                open = niftyQuote?.Ohlc?.Open ?? 0m,
                high = niftyQuote?.Ohlc?.High ?? 0m,
                low = niftyQuote?.Ohlc?.Low ?? 0m,
                close = niftyClose,
                timestamp = niftyQuote?.Timestamp
            },
            bankNiftySpot = bankQuote != null ? new
            {
                lastPrice = bankQuote.LastPrice,
                change = bankChange,
                changePercent = bankChangePct,
                open = bankQuote.Ohlc?.Open ?? 0m,
                high = bankQuote.Ohlc?.High ?? 0m,
                low = bankQuote.Ohlc?.Low ?? 0m,
                close = bankClose,
                timestamp = bankQuote.Timestamp
            } : null,
            sensexSpot = sensexQuote != null ? new
            {
                lastPrice = sensexQuote.LastPrice,
                change = sensexChange,
                changePercent = sensexChangePct,
                open = sensexQuote.Ohlc?.Open ?? 0m,
                high = sensexQuote.Ohlc?.High ?? 0m,
                low = sensexQuote.Ohlc?.Low ?? 0m,
                close = sensexClose,
                timestamp = sensexQuote.Timestamp
            } : null
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
        var todayUtc = DateTime.UtcNow.Date;
        var expiries = contracts
            .Where(c => DateTime.TryParse(c.Expiry, out var dt) && dt.Date >= todayUtc)
            .Select(c => c.Expiry)
            .Distinct()
            .OrderBy(e => DateTime.Parse(e))
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
