using Microsoft.AspNetCore.Mvc;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IndicatorsController : ControllerBase
{
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly ICandleAggregationEngine _candleEngine;
    private readonly IUpstoxClient _upstoxClient;
    private readonly ILogger<IndicatorsController> _logger;

    public IndicatorsController(
        ITechnicalIndicatorService indicatorService,
        ICandleAggregationEngine candleEngine,
        IUpstoxClient upstoxClient,
        ILogger<IndicatorsController> logger)
    {
        _indicatorService = indicatorService;
        _candleEngine = candleEngine;
        _upstoxClient = upstoxClient;
        _logger = logger;
    }

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetIndicatorSnapshot(
        [FromQuery] string instrumentKey = "NSE_INDEX|Nifty 50",
        [FromQuery] CandleTimeframe timeframe = CandleTimeframe.OneMinute,
        [FromQuery] int days = 3,
        CancellationToken ct = default)
    {
        var candles = await GetEffectiveCandlesAsync(instrumentKey, timeframe, days, ct);
        if (candles.Count == 0)
        {
            return NotFound(new { message = $"No candle data available for {instrumentKey} on timeframe {timeframe}" });
        }

        var dailyCpr = await GetDailyCprAsync(instrumentKey, ct);
        var snapshot = _indicatorService.CalculateIndicators(candles, timeframe, dailyCpr);

        return Ok(snapshot);
    }

    [HttpGet("multi-timeframe")]
    public async Task<IActionResult> GetMultiTimeframeState(
        [FromQuery] string instrumentKey = "NSE_INDEX|Nifty 50",
        [FromQuery] int days = 3,
        CancellationToken ct = default)
    {
        var candles1m = await GetEffectiveCandlesAsync(instrumentKey, CandleTimeframe.OneMinute, days, ct);
        var candles5m = await GetEffectiveCandlesAsync(instrumentKey, CandleTimeframe.FiveMinute, days, ct);
        var candles15m = await GetEffectiveCandlesAsync(instrumentKey, CandleTimeframe.FifteenMinute, days, ct);

        if (candles1m.Count == 0 && candles5m.Count == 0 && candles15m.Count == 0)
        {
            return NotFound(new { message = $"No candle data available across timeframes for {instrumentKey}" });
        }

        var dailyCpr = await GetDailyCprAsync(instrumentKey, ct);
        var mtfState = _indicatorService.CalculateMultiTimeframeState(
            instrumentKey, candles1m, candles5m, candles15m, dailyCpr);

        return Ok(mtfState);
    }

    [HttpGet("cpr")]
    public async Task<IActionResult> GetCpr(
        [FromQuery] string instrumentKey = "NSE_INDEX|Nifty 50",
        CancellationToken ct = default)
    {
        var cpr = await GetDailyCprAsync(instrumentKey, ct);
        if (cpr == null)
        {
            return NotFound(new { message = $"Unable to compute CPR: previous day candle not found for {instrumentKey}" });
        }

        return Ok(new
        {
            instrumentKey,
            cpr
        });
    }

    private async Task<IReadOnlyList<MarketCandle>> GetEffectiveCandlesAsync(
        string instrumentKey,
        CandleTimeframe timeframe,
        int days,
        CancellationToken ct)
    {
        // 1. Check live in-memory candle engine first
        var liveCandles = _candleEngine.GetRecentCandles(instrumentKey, timeframe, 150);
        if (liveCandles.Count >= 50)
        {
            return liveCandles;
        }

        // 2. Fetch from Upstox Historical Candle API V3
        var (unit, interval) = timeframe switch
        {
            CandleTimeframe.OneMinute => ("minutes", 1),
            CandleTimeframe.FiveMinute => ("minutes", 5),
            CandleTimeframe.FifteenMinute => ("minutes", 15),
            _ => ("minutes", 1)
        };

        var now = DateTime.Now;
        var toDate = now.ToString("yyyy-MM-dd");
        var fromDate = now.AddDays(-days).ToString("yyyy-MM-dd");

        var allCandles = new Dictionary<DateTime, MarketCandle>();

        // 1. Fetch completed historical candles
        try
        {
            var historical = await _upstoxClient.GetHistoricalCandlesAsync(
                instrumentKey, unit, interval, toDate, fromDate, ct);

            foreach (var c in historical)
            {
                allCandles[c.Timestamp] = new MarketCandle
                {
                    InstrumentKey = instrumentKey,
                    Timeframe = timeframe,
                    CandleTimeUtc = c.Timestamp,
                    Open = c.Open,
                    High = c.High,
                    Low = c.Low,
                    Close = c.Close,
                    Volume = c.Volume,
                    OpenInterest = c.OpenInterest,
                    IsFinalized = true
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch historical candles for {InstrumentKey}", instrumentKey);
        }

        // 2. Fetch today's live intraday candles
        try
        {
            var intraday = await _upstoxClient.GetIntradayCandlesAsync(
                instrumentKey, unit, interval, ct);

            foreach (var c in intraday)
            {
                allCandles[c.Timestamp] = new MarketCandle
                {
                    InstrumentKey = instrumentKey,
                    Timeframe = timeframe,
                    CandleTimeUtc = c.Timestamp,
                    Open = c.Open,
                    High = c.High,
                    Low = c.Low,
                    Close = c.Close,
                    Volume = c.Volume,
                    OpenInterest = c.OpenInterest,
                    IsFinalized = true
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch intraday candles for {InstrumentKey}", instrumentKey);
        }

        // 3. Overlay live WebSocket candles
        foreach (var c in liveCandles)
        {
            allCandles[c.CandleTimeUtc] = c;
        }

        return allCandles.Values.OrderBy(c => c.CandleTimeUtc).ToList();
    }

    private async Task<CprResult?> GetDailyCprAsync(string instrumentKey, CancellationToken ct)
    {
        try
        {
            var now = DateTime.Now;
            var toDate = now.ToString("yyyy-MM-dd");
            var fromDate = now.AddDays(-7).ToString("yyyy-MM-dd");

            var dailyCandles = await _upstoxClient.GetHistoricalCandlesAsync(
                instrumentKey, "day", 1, toDate, fromDate, ct);

            if (dailyCandles.Count >= 2)
            {
                // Previous completed trading day is the second to last (or last completed) candle
                var prevDay = dailyCandles[^2];
                return _indicatorService.CalculateDailyCpr(prevDay.High, prevDay.Low, prevDay.Close);
            }
            else if (dailyCandles.Count == 1)
            {
                var day = dailyCandles[0];
                return _indicatorService.CalculateDailyCpr(day.High, day.Low, day.Close);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute daily CPR for {InstrumentKey}", instrumentKey);
        }

        return null;
    }
}
