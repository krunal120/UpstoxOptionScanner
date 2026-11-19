using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure.Services;
using Xunit;

namespace NiftyQuant.UnitTests;

public class CandleAggregationEngineTests
{
    private static readonly TimeZoneInfo IstZone = CandleAggregationEngine.GetIstTimeZone();

    [Fact]
    public void Get1mBucketTimeUtc_ShouldAlignToMinuteStart()
    {
        // 09:15:32 IST is 03:45:32 UTC
        var tickUtc = new DateTime(2026, 11, 19, 3, 45, 32, DateTimeKind.Utc);
        var bucketUtc = CandleAggregationEngine.Get1mBucketTimeUtc(tickUtc);

        var bucketIst = TimeZoneInfo.ConvertTimeFromUtc(bucketUtc, IstZone);
        bucketIst.Hour.Should().Be(9);
        bucketIst.Minute.Should().Be(15);
        bucketIst.Second.Should().Be(0);
        bucketUtc.Should().Be(new DateTime(2026, 11, 19, 3, 45, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(9, 15, 10, 9, 15)]
    [InlineData(9, 18, 45, 9, 15)]
    [InlineData(9, 19, 59, 9, 15)]
    [InlineData(9, 20, 0, 9, 20)]
    [InlineData(9, 24, 30, 9, 20)]
    [InlineData(9, 25, 0, 9, 25)]
    public void Get5mBucketTimeUtc_ShouldAlignTo5MinuteBoundaryInIst(
        int inHour, int inMin, int inSec, int expHour, int expMin)
    {
        var istTime = new DateTime(2026, 11, 19, inHour, inMin, inSec, DateTimeKind.Unspecified);
        var utcTime = TimeZoneInfo.ConvertTimeToUtc(istTime, IstZone);

        var bucketUtc = CandleAggregationEngine.Get5mBucketTimeUtc(utcTime);
        var bucketIst = TimeZoneInfo.ConvertTimeFromUtc(bucketUtc, IstZone);

        bucketIst.Hour.Should().Be(expHour);
        bucketIst.Minute.Should().Be(expMin);
        bucketIst.Second.Should().Be(0);
    }

    [Theory]
    [InlineData(9, 15, 0, 9, 15)]
    [InlineData(9, 22, 30, 9, 15)]
    [InlineData(9, 29, 59, 9, 15)]
    [InlineData(9, 30, 0, 9, 30)]
    [InlineData(9, 44, 15, 9, 30)]
    [InlineData(9, 45, 0, 9, 45)]
    public void Get15mBucketTimeUtc_ShouldAlignTo15MinuteBoundaryInIst(
        int inHour, int inMin, int inSec, int expHour, int expMin)
    {
        var istTime = new DateTime(2026, 11, 19, inHour, inMin, inSec, DateTimeKind.Unspecified);
        var utcTime = TimeZoneInfo.ConvertTimeToUtc(istTime, IstZone);

        var bucketUtc = CandleAggregationEngine.Get15mBucketTimeUtc(utcTime);
        var bucketIst = TimeZoneInfo.ConvertTimeFromUtc(bucketUtc, IstZone);

        bucketIst.Hour.Should().Be(expHour);
        bucketIst.Minute.Should().Be(expMin);
        bucketIst.Second.Should().Be(0);
    }

    [Fact]
    public async Task ProcessTick_ShouldAggregateOHLCV_WithinSameMinute()
    {
        var engine = new CandleAggregationEngine(null!, NullLogger<CandleAggregationEngine>.Instance);
        const string key = "NSE_INDEX|Nifty 50";

        var baseTime = new DateTime(2026, 11, 19, 3, 45, 0, DateTimeKind.Utc); // 09:15:00 IST

        // Tick 1
        await engine.ProcessTickAsync(new MarketTick
        {
            InstrumentKey = key,
            Ltp = 25000.0m,
            LastTradedQuantity = 100,
            Volume = 100,
            TimestampUtc = baseTime.AddSeconds(10)
        });

        // Tick 2 (New High)
        await engine.ProcessTickAsync(new MarketTick
        {
            InstrumentKey = key,
            Ltp = 25050.0m,
            LastTradedQuantity = 200,
            Volume = 300,
            TimestampUtc = baseTime.AddSeconds(20)
        });

        // Tick 3 (New Low & Close)
        await engine.ProcessTickAsync(new MarketTick
        {
            InstrumentKey = key,
            Ltp = 24950.0m,
            LastTradedQuantity = 150,
            Volume = 450,
            TimestampUtc = baseTime.AddSeconds(40)
        });

        var candle1m = engine.GetCurrentCandle(key, CandleTimeframe.OneMinute);

        candle1m.Should().NotBeNull();
        candle1m!.Open.Should().Be(25000.0m);
        candle1m.High.Should().Be(25050.0m);
        candle1m.Low.Should().Be(24950.0m);
        candle1m.Close.Should().Be(24950.0m);
        candle1m.Volume.Should().Be(450);
        candle1m.IsFinalized.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessTick_CrossingMinuteBoundary_ShouldFinalizePrevious1mCandle()
    {
        var engine = new CandleAggregationEngine(null!, NullLogger<CandleAggregationEngine>.Instance);
        const string key = "NSE_INDEX|Nifty 50";

        var finalizedCandles = new List<MarketCandle>();
        engine.OnCandleFinalized += candle =>
        {
            finalizedCandles.Add(candle);
            return Task.CompletedTask;
        };

        var baseTime = new DateTime(2026, 11, 19, 3, 45, 0, DateTimeKind.Utc); // 09:15:00 IST

        // Ticks in 09:15
        await engine.ProcessTickAsync(new MarketTick
        {
            InstrumentKey = key,
            Ltp = 25000.0m,
            LastTradedQuantity = 100,
            Volume = 100,
            TimestampUtc = baseTime.AddSeconds(15)
        });
        await engine.ProcessTickAsync(new MarketTick
        {
            InstrumentKey = key,
            Ltp = 25020.0m,
            LastTradedQuantity = 50,
            Volume = 150,
            TimestampUtc = baseTime.AddSeconds(45)
        });

        // Tick crossing into 09:16
        await engine.ProcessTickAsync(new MarketTick
        {
            InstrumentKey = key,
            Ltp = 25030.0m,
            LastTradedQuantity = 80,
            Volume = 230,
            TimestampUtc = baseTime.AddMinutes(1).AddSeconds(5) // 09:16:05 IST
        });

        // Previous 1m candle must be finalized
        finalizedCandles.Should().ContainSingle(c => c.Timeframe == CandleTimeframe.OneMinute);
        var final1m = finalizedCandles.First(c => c.Timeframe == CandleTimeframe.OneMinute);
        final1m.IsFinalized.Should().BeTrue();
        final1m.Open.Should().Be(25000.0m);
        final1m.High.Should().Be(25020.0m);
        final1m.Low.Should().Be(25000.0m);
        final1m.Close.Should().Be(25020.0m);
        final1m.Volume.Should().Be(150);

        // Current 1m candle is now for 09:16:00
        var current1m = engine.GetCurrentCandle(key, CandleTimeframe.OneMinute);
        current1m.Should().NotBeNull();
        current1m!.Open.Should().Be(25030.0m);
        current1m.Volume.Should().Be(80);
    }

    [Fact]
    public async Task ProcessTick_MultiMinuteProgression_ShouldFinalize5mCandle()
    {
        var engine = new CandleAggregationEngine(null!, NullLogger<CandleAggregationEngine>.Instance);
        const string key = "NSE_INDEX|Nifty 50";

        var finalizedCandles = new List<MarketCandle>();
        engine.OnCandleFinalized += candle =>
        {
            finalizedCandles.Add(candle);
            return Task.CompletedTask;
        };

        var baseTime = new DateTime(2026, 11, 19, 3, 45, 0, DateTimeKind.Utc); // 09:15:00 IST

        // Minutes 15, 16, 17, 18, 19
        decimal[] prices = [25000m, 25050m, 25020m, 24980m, 25040m];
        for (int i = 0; i < 5; i++)
        {
            await engine.ProcessTickAsync(new MarketTick
            {
                InstrumentKey = key,
                Ltp = prices[i],
                LastTradedQuantity = 100,
                Volume = (i + 1) * 100,
                TimestampUtc = baseTime.AddMinutes(i).AddSeconds(30)
            });
        }

        // Tick in minute 20 (09:20:05 IST) triggers finalization of 09:19 1m AND 09:15 5m
        await engine.ProcessTickAsync(new MarketTick
        {
            InstrumentKey = key,
            Ltp = 25060m,
            LastTradedQuantity = 100,
            Volume = 600,
            TimestampUtc = baseTime.AddMinutes(5).AddSeconds(5) // 09:20:05 IST
        });

        // 5 1m candles + 1 5m candle finalized
        finalizedCandles.Where(c => c.Timeframe == CandleTimeframe.OneMinute).Should().HaveCount(5);
        finalizedCandles.Should().ContainSingle(c => c.Timeframe == CandleTimeframe.FiveMinute);

        var final5m = finalizedCandles.First(c => c.Timeframe == CandleTimeframe.FiveMinute);
        final5m.IsFinalized.Should().BeTrue();
        final5m.Open.Should().Be(25000m);
        final5m.High.Should().Be(25050m);
        final5m.Low.Should().Be(24980m);
        final5m.Close.Should().Be(25040m);
        final5m.Volume.Should().Be(500);
    }
}
