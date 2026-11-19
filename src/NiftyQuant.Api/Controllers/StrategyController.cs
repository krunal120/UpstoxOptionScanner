using Microsoft.AspNetCore.Mvc;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Indicators;
using NiftyQuant.Application.Common.Models.Options;
using NiftyQuant.Application.Common.Models.Strategy;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StrategyController : ControllerBase
{
    private readonly IStrategyEngine _strategyEngine;
    private readonly IMarketRegimeClassifier _regimeClassifier;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly IOptionChainSnapshotEngine _snapshotEngine;
    private readonly IOptionsIntelligenceService _optionsIntelligence;
    private readonly ICandleAggregationEngine _candleEngine;
    private readonly IUpstoxClient _upstoxClient;
    private readonly ILogger<StrategyController> _logger;

    public StrategyController(
        IStrategyEngine strategyEngine,
        IMarketRegimeClassifier regimeClassifier,
        ITechnicalIndicatorService indicatorService,
        IOptionChainSnapshotEngine snapshotEngine,
        IOptionsIntelligenceService optionsIntelligence,
        ICandleAggregationEngine candleEngine,
        IUpstoxClient upstoxClient,
        ILogger<StrategyController> logger)
    {
        _strategyEngine = strategyEngine;
        _regimeClassifier = regimeClassifier;
        _indicatorService = indicatorService;
        _snapshotEngine = snapshotEngine;
        _optionsIntelligence = optionsIntelligence;
        _candleEngine = candleEngine;
        _upstoxClient = upstoxClient;
        _logger = logger;
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate(
        [FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50",
        CancellationToken ct = default)
    {
        var context = await BuildContextAsync(underlyingKey, ct);
        if (context == null)
        {
            return BadRequest(new { message = $"Unable to build market context for {underlyingKey}. Ensure market candles and option chain data are accessible." });
        }

        var signal = await _strategyEngine.EvaluateStrategiesAsync(context, ct);
        return Ok(signal);
    }

    [HttpGet("latest-signal")]
    public async Task<IActionResult> GetLatestSignal(
        [FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50",
        CancellationToken ct = default)
    {
        var signal = await _strategyEngine.GetLatestSignalAsync(underlyingKey, ct);
        if (signal == null)
        {
            return NotFound(new { message = $"No signal recorded for {underlyingKey}." });
        }

        return Ok(signal);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetSignalHistory(
        [FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50",
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var signals = await _strategyEngine.GetSignalHistoryAsync(underlyingKey, limit, ct);
        return Ok(signals);
    }

    [HttpGet("regime")]
    public async Task<IActionResult> GetMarketRegime(
        [FromQuery] string underlyingKey = "NSE_INDEX|Nifty 50",
        CancellationToken ct = default)
    {
        var context = await BuildContextAsync(underlyingKey, ct);
        if (context == null)
        {
            return BadRequest(new { message = $"Unable to construct analysis context for {underlyingKey}" });
        }

        var regime = _regimeClassifier.ClassifyRegime(context);
        return Ok(regime);
    }

    private async Task<StrategyAnalysisContext?> BuildContextAsync(
        string underlyingKey,
        CancellationToken ct)
    {
        try
        {
            var candles1m = await GetEffectiveCandlesAsync(underlyingKey, CandleTimeframe.OneMinute, 3, ct);
            var candles5m = await GetEffectiveCandlesAsync(underlyingKey, CandleTimeframe.FiveMinute, 3, ct);
            var candles15m = await GetEffectiveCandlesAsync(underlyingKey, CandleTimeframe.FifteenMinute, 3, ct);

            if (candles1m.Count == 0 && candles5m.Count == 0 && candles15m.Count == 0)
            {
                return null;
            }

            var (dailyCpr, prevHigh, prevLow, prevClose) = await GetDailyLevelsAsync(underlyingKey, ct);

            var mtfState = _indicatorService.CalculateMultiTimeframeState(
                underlyingKey, candles1m, candles5m, candles15m, dailyCpr);

            // Get option chain snapshot & intelligence
            var snapshot = await _snapshotEngine.GetLatestSnapshotAsync(underlyingKey, ct)
                           ?? await _snapshotEngine.CaptureAndPersistSnapshotAsync(underlyingKey, ct: ct);

            var optionsReport = snapshot != null
                ? _optionsIntelligence.AnalyzeSnapshot(snapshot)
                : new OptionsIntelligenceReportDto();

            // Fetch India VIX quote (safe fallback)
            decimal? indiaVix = null;
            try
            {
                var vixQuote = await _upstoxClient.GetMarketQuoteAsync("NSE_INDEX|India VIX", ct);
                if (vixQuote != null && vixQuote.LastPrice > 0m)
                {
                    indiaVix = vixQuote.LastPrice;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch India VIX quote. Falling back to default.");
            }

            decimal currentSpot = snapshot?.UnderlyingPrice > 0m
                ? snapshot.UnderlyingPrice
                : (candles1m.Count > 0 ? candles1m[^1].Close : (candles5m.Count > 0 ? candles5m[^1].Close : 0m));

            // Today's intraday high and low
            DateTime todayUtc = DateTime.UtcNow.Date;
            var todayCandles = candles1m.Where(c => c.CandleTimeUtc.Date == todayUtc).ToList();
            decimal intradayHigh = todayCandles.Count > 0 ? todayCandles.Max(c => c.High) : currentSpot;
            decimal intradayLow = todayCandles.Count > 0 ? todayCandles.Min(c => c.Low) : currentSpot;

            return new StrategyAnalysisContext
            {
                UnderlyingKey = underlyingKey,
                CurrentPrice = currentSpot,
                IndiaVix = indiaVix,
                MultiTimeframeState = mtfState,
                OptionsReport = optionsReport,
                PreviousDayHigh = prevHigh,
                PreviousDayLow = prevLow,
                PreviousDayClose = prevClose,
                IntradayHigh = intradayHigh,
                IntradayLow = intradayLow,
                EvaluationTimeUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building strategy context for {UnderlyingKey}", underlyingKey);
            return null;
        }
    }

    private async Task<IReadOnlyList<MarketCandle>> GetEffectiveCandlesAsync(
        string instrumentKey,
        CandleTimeframe timeframe,
        int days,
        CancellationToken ct)
    {
        var liveCandles = _candleEngine.GetRecentCandles(instrumentKey, timeframe, 150);
        if (liveCandles.Count >= 50)
        {
            return liveCandles;
        }

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

        try
        {
            var historical = await _upstoxClient.GetHistoricalCandlesAsync(
                instrumentKey, unit, interval, toDate, fromDate, ct);

            return historical.Select(c => new MarketCandle
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
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch historical candles for {InstrumentKey}", instrumentKey);
            return liveCandles;
        }
    }

    private async Task<(CprResult? Cpr, decimal High, decimal Low, decimal Close)> GetDailyLevelsAsync(
        string instrumentKey,
        CancellationToken ct)
    {
        try
        {
            var now = DateTime.Now;
            var toDate = now.ToString("yyyy-MM-dd");
            var fromDate = now.AddDays(-7).ToString("yyyy-MM-dd");

            var dailyCandles = await _upstoxClient.GetHistoricalCandlesAsync(
                instrumentKey, "days", 1, toDate, fromDate, ct);

            if (dailyCandles.Count >= 2)
            {
                var prevDay = dailyCandles[^2];
                var cpr = _indicatorService.CalculateDailyCpr(prevDay.High, prevDay.Low, prevDay.Close);
                return (cpr, prevDay.High, prevDay.Low, prevDay.Close);
            }
            else if (dailyCandles.Count == 1)
            {
                var day = dailyCandles[0];
                var cpr = _indicatorService.CalculateDailyCpr(day.High, day.Low, day.Close);
                return (cpr, day.High, day.Low, day.Close);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute daily levels for {InstrumentKey}", instrumentKey);
        }

        return (null, 0m, 0m, 0m);
    }
}
