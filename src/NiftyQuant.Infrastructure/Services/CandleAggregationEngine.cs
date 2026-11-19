using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure.Persistence;

namespace NiftyQuant.Infrastructure.Services;

public class CandleAggregationEngine : ICandleAggregationEngine
{
    private static readonly TimeZoneInfo IstZone = GetIstTimeZone();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CandleAggregationEngine> _logger;
    private readonly ConcurrentDictionary<string, InstrumentCandleState> _states = new(StringComparer.OrdinalIgnoreCase);

    public event Func<MarketCandle, Task>? OnCandleFinalized;

    public CandleAggregationEngine(
        IServiceScopeFactory scopeFactory,
        ILogger<CandleAggregationEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ProcessTickAsync(MarketTick tick, CancellationToken ct = default)
    {
        if (tick.Ltp <= 0m || string.IsNullOrWhiteSpace(tick.InstrumentKey)) return;

        var state = _states.GetOrAdd(tick.InstrumentKey, key => new InstrumentCandleState(key));
        List<MarketCandle> finalizedCandles;

        lock (state.SyncRoot)
        {
            finalizedCandles = state.UpdateTick(tick);
        }

        if (finalizedCandles.Count > 0)
        {
            await PersistAndPublishFinalizedCandlesAsync(finalizedCandles, ct);
        }
    }

    public MarketCandle? GetCurrentCandle(string instrumentKey, CandleTimeframe timeframe)
    {
        if (!_states.TryGetValue(instrumentKey, out var state)) return null;

        lock (state.SyncRoot)
        {
            return timeframe switch
            {
                CandleTimeframe.OneMinute => state.Current1m != null ? CloneCandle(state.Current1m) : null,
                CandleTimeframe.FiveMinute => state.Current5m != null ? CloneCandle(state.Current5m) : null,
                CandleTimeframe.FifteenMinute => state.Current15m != null ? CloneCandle(state.Current15m) : null,
                _ => null
            };
        }
    }

    public IReadOnlyList<MarketCandle> GetRecentCandles(string instrumentKey, CandleTimeframe timeframe, int count = 100)
    {
        if (!_states.TryGetValue(instrumentKey, out var state)) return Array.Empty<MarketCandle>();

        lock (state.SyncRoot)
        {
            return state.GetRecentCandles(timeframe, count);
        }
    }

    private async Task PersistAndPublishFinalizedCandlesAsync(List<MarketCandle> candles, CancellationToken ct)
    {
        try
        {
            if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService(typeof(NiftyQuantDbContext)) as NiftyQuantDbContext;
                if (db != null)
                {
                    foreach (var candle in candles)
                    {
                        var existing = await db.MarketCandles.FirstOrDefaultAsync(c =>
                            c.InstrumentKey == candle.InstrumentKey &&
                            c.Timeframe == candle.Timeframe &&
                            c.CandleTimeUtc == candle.CandleTimeUtc, ct);

                        if (existing == null)
                        {
                            db.MarketCandles.Add(candle);
                        }
                        else
                        {
                            existing.Open = candle.Open;
                            existing.High = candle.High;
                            existing.Low = candle.Low;
                            existing.Close = candle.Close;
                            existing.Volume = candle.Volume;
                            existing.OpenInterest = candle.OpenInterest;
                            existing.IsFinalized = true;
                        }
                    }

                    await db.SaveChangesAsync(ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist {Count} finalized candles to database", candles.Count);
        }

        // Notify subscribers (SignalR hub, Indicator calculators, Strategy engine)
        foreach (var candle in candles)
        {
            try
            {
                if (OnCandleFinalized != null)
                {
                    await OnCandleFinalized.Invoke(candle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispatching OnCandleFinalized for {Instrument} {Timeframe}",
                    candle.InstrumentKey, candle.Timeframe);
            }
        }
    }

    private static MarketCandle CloneCandle(MarketCandle c) => new()
    {
        Id = c.Id,
        InstrumentKey = c.InstrumentKey,
        Timeframe = c.Timeframe,
        CandleTimeUtc = c.CandleTimeUtc,
        Open = c.Open,
        High = c.High,
        Low = c.Low,
        Close = c.Close,
        Volume = c.Volume,
        OpenInterest = c.OpenInterest,
        IsFinalized = c.IsFinalized,
        CreatedAtUtc = c.CreatedAtUtc
    };

    public static TimeZoneInfo GetIstTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
    }

    public static DateTime Get1mBucketTimeUtc(DateTime timestampUtc)
    {
        var ist = TimeZoneInfo.ConvertTimeFromUtc(timestampUtc, IstZone);
        var bucketIst = new DateTime(ist.Year, ist.Month, ist.Day, ist.Hour, ist.Minute, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(bucketIst, IstZone);
    }

    public static DateTime Get5mBucketTimeUtc(DateTime timestampUtc)
    {
        var ist = TimeZoneInfo.ConvertTimeFromUtc(timestampUtc, IstZone);
        var roundedMinute = (ist.Minute / 5) * 5;
        var bucketIst = new DateTime(ist.Year, ist.Month, ist.Day, ist.Hour, roundedMinute, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(bucketIst, IstZone);
    }

    public static DateTime Get15mBucketTimeUtc(DateTime timestampUtc)
    {
        var ist = TimeZoneInfo.ConvertTimeFromUtc(timestampUtc, IstZone);
        var roundedMinute = (ist.Minute / 15) * 15;
        var bucketIst = new DateTime(ist.Year, ist.Month, ist.Day, ist.Hour, roundedMinute, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(bucketIst, IstZone);
    }
}

internal class InstrumentCandleState
{
    public string InstrumentKey { get; }
    public object SyncRoot { get; } = new();

    private MarketCandle? _current1m;
    private MarketCandle? _current5m;
    private MarketCandle? _current15m;

    public MarketCandle? Current1m => _current1m;
    public MarketCandle? Current5m => _current5m;
    public MarketCandle? Current15m => _current15m;

    private readonly List<MarketCandle> _finalized1m = new();
    private readonly List<MarketCandle> _finalized5m = new();
    private readonly List<MarketCandle> _finalized15m = new();

    private long _lastCumulativeVolume;

    public InstrumentCandleState(string instrumentKey)
    {
        InstrumentKey = instrumentKey;
    }

    public List<MarketCandle> UpdateTick(MarketTick tick)
    {
        var finalizedList = new List<MarketCandle>();
        var tickTimeUtc = tick.TimestampUtc;
        var bucket1mUtc = CandleAggregationEngine.Get1mBucketTimeUtc(tickTimeUtc);
        var bucket5mUtc = CandleAggregationEngine.Get5mBucketTimeUtc(tickTimeUtc);
        var bucket15mUtc = CandleAggregationEngine.Get15mBucketTimeUtc(tickTimeUtc);

        var incVolume = CalculateIncrementalVolume(tick);

        // 1-minute aggregation
        UpdateSingleTimeframe(ref _current1m, tick, CandleTimeframe.OneMinute, bucket1mUtc, incVolume, _finalized1m, finalizedList);

        // 5-minute aggregation
        UpdateSingleTimeframe(ref _current5m, tick, CandleTimeframe.FiveMinute, bucket5mUtc, incVolume, _finalized5m, finalizedList);

        // 15-minute aggregation
        UpdateSingleTimeframe(ref _current15m, tick, CandleTimeframe.FifteenMinute, bucket15mUtc, incVolume, _finalized15m, finalizedList);

        return finalizedList;
    }

    private long CalculateIncrementalVolume(MarketTick tick)
    {
        if (tick.LastTradedQuantity.HasValue && tick.LastTradedQuantity.Value > 0)
        {
            if (tick.Volume > 0) _lastCumulativeVolume = tick.Volume;
            return tick.LastTradedQuantity.Value;
        }

        if (tick.Volume > 0)
        {
            if (_lastCumulativeVolume > 0 && tick.Volume >= _lastCumulativeVolume)
            {
                var delta = tick.Volume - _lastCumulativeVolume;
                _lastCumulativeVolume = tick.Volume;
                return delta;
            }

            _lastCumulativeVolume = tick.Volume;
            return tick.Volume;
        }

        return 0;
    }

    private static void UpdateSingleTimeframe(
        ref MarketCandle? current,
        MarketTick tick,
        CandleTimeframe timeframe,
        DateTime bucketUtc,
        long incrementalVolume,
        List<MarketCandle> historyList,
        List<MarketCandle> finalizedList)
    {
        if (current == null)
        {
            current = new MarketCandle
            {
                InstrumentKey = tick.InstrumentKey,
                Timeframe = timeframe,
                CandleTimeUtc = bucketUtc,
                Open = tick.Ltp,
                High = tick.Ltp,
                Low = tick.Ltp,
                Close = tick.Ltp,
                Volume = incrementalVolume,
                OpenInterest = tick.OpenInterest,
                IsFinalized = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            return;
        }

        if (bucketUtc == current.CandleTimeUtc)
        {
            current.High = Math.Max(current.High, tick.Ltp);
            current.Low = Math.Min(current.Low, tick.Ltp);
            current.Close = tick.Ltp;
            current.Volume += incrementalVolume;
            if (tick.OpenInterest > 0m)
            {
                current.OpenInterest = tick.OpenInterest;
            }
            return;
        }

        if (bucketUtc > current.CandleTimeUtc)
        {
            current.IsFinalized = true;
            historyList.Add(current);
            if (historyList.Count > 300) historyList.RemoveAt(0);
            finalizedList.Add(current);

            // Start new candle for new bucket
            current = new MarketCandle
            {
                InstrumentKey = tick.InstrumentKey,
                Timeframe = timeframe,
                CandleTimeUtc = bucketUtc,
                Open = tick.Ltp,
                High = tick.Ltp,
                Low = tick.Ltp,
                Close = tick.Ltp,
                Volume = incrementalVolume,
                OpenInterest = tick.OpenInterest,
                IsFinalized = false,
                CreatedAtUtc = DateTime.UtcNow
            };
        }
    }

    public IReadOnlyList<MarketCandle> GetRecentCandles(CandleTimeframe timeframe, int count)
    {
        var list = timeframe switch
        {
            CandleTimeframe.OneMinute => _finalized1m,
            CandleTimeframe.FiveMinute => _finalized5m,
            CandleTimeframe.FifteenMinute => _finalized15m,
            _ => _finalized1m
        };

        return list.TakeLast(count).ToList();
    }
}
