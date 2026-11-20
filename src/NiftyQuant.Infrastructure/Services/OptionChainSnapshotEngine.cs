using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Upstox;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure.Persistence;

namespace NiftyQuant.Infrastructure.Services;

public class OptionChainSnapshotEngine : IOptionChainSnapshotEngine
{
    private readonly IUpstoxClient _upstoxClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OptionChainSnapshotEngine> _logger;

    public event Func<OptionChainSnapshot, Task>? OnSnapshotCaptured;

    public OptionChainSnapshotEngine(
        IUpstoxClient upstoxClient,
        IServiceScopeFactory scopeFactory,
        ILogger<OptionChainSnapshotEngine> logger)
    {
        _upstoxClient = upstoxClient;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<OptionChainSnapshot?> CaptureAndPersistSnapshotAsync(
        string underlyingKey = "NSE_INDEX|Nifty 50",
        string? expiryDate = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(expiryDate))
            {
                var contracts = await _upstoxClient.GetOptionContractsAsync(underlyingKey, ct);
                expiryDate = contracts.Select(c => c.Expiry).Distinct().OrderBy(e => e).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(expiryDate))
                {
                    _logger.LogWarning("No active option expiries found for {Underlying}", underlyingKey);
                    return null;
                }
            }

            var chainItems = await _upstoxClient.GetOptionChainAsync(underlyingKey, expiryDate, ct);
            if (chainItems.Count == 0)
            {
                _logger.LogWarning("Empty option chain received for {Underlying} expiry {Expiry}", underlyingKey, expiryDate);
                return null;
            }

            if (!DateTime.TryParse(expiryDate, out var parsedExpiry))
            {
                parsedExpiry = DateTime.UtcNow.Date;
            }

            var snapshotTimeUtc = DateTime.UtcNow;
            var spotPrice = chainItems.FirstOrDefault()?.UnderlyingSpotPrice ?? 0m;

            // Fetch previous stored snapshot for delta calculation
            Dictionary<string, decimal> prevOiMap = new(StringComparer.OrdinalIgnoreCase);
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
                var lastSnapshot = await db.OptionChainSnapshots
                    .Include(s => s.Quotes)
                    .Where(s => s.UnderlyingKey == underlyingKey && s.ExpiryDate == parsedExpiry.Date)
                    .OrderByDescending(s => s.SnapshotTimeUtc)
                    .FirstOrDefaultAsync(ct);

                if (lastSnapshot?.Quotes != null)
                {
                    foreach (var q in lastSnapshot.Quotes)
                    {
                        prevOiMap[q.InstrumentKey] = q.OpenInterest;
                    }
                }
            }

            decimal totalCallOi = 0m;
            decimal totalPutOi = 0m;
            decimal highestCallOi = -1m;
            decimal highestPutOi = -1m;
            decimal highestCallStrike = 0m;
            decimal highestPutStrike = 0m;

            var quotes = new List<OptionQuote>();

            foreach (var item in chainItems)
            {
                // Call Option
                if (item.CallOptions?.MarketData != null)
                {
                    var callOi = item.CallOptions.MarketData.OpenInterest;
                    totalCallOi += callOi;

                    if (callOi > highestCallOi)
                    {
                        highestCallOi = callOi;
                        highestCallStrike = item.StrikePrice;
                    }

                    var key = item.CallOptions.InstrumentKey;
                    var prevOi = prevOiMap.TryGetValue(key, out var storedOi) ? storedOi : item.CallOptions.MarketData.PreviousOpenInterest;
                    var oiChange = callOi - prevOi;
                    var oiChangePercent = prevOi > 0 ? (oiChange / prevOi) * 100m : 0m;

                    quotes.Add(new OptionQuote
                    {
                        InstrumentKey = key,
                        StrikePrice = item.StrikePrice,
                        OptionType = OptionType.CE,
                        ExpiryDate = parsedExpiry.Date,
                        Ltp = item.CallOptions.MarketData.Ltp,
                        BidPrice = item.CallOptions.MarketData.BidPrice,
                        AskPrice = item.CallOptions.MarketData.AskPrice,
                        Volume = item.CallOptions.MarketData.Volume,
                        OpenInterest = callOi,
                        PreviousOpenInterest = prevOi,
                        IntradayOiChange = oiChange,
                        IntradayOiChangePercent = oiChangePercent,
                        ImpliedVolatility = item.CallOptions.OptionGreeks?.ImpliedVolatility,
                        Delta = item.CallOptions.OptionGreeks?.Delta,
                        Gamma = item.CallOptions.OptionGreeks?.Gamma,
                        Theta = item.CallOptions.OptionGreeks?.Theta,
                        Vega = item.CallOptions.OptionGreeks?.Vega,
                        SnapshotTimeUtc = snapshotTimeUtc
                    });
                }

                // Put Option
                if (item.PutOptions?.MarketData != null)
                {
                    var putOi = item.PutOptions.MarketData.OpenInterest;
                    totalPutOi += putOi;

                    if (putOi > highestPutOi)
                    {
                        highestPutOi = putOi;
                        highestPutStrike = item.StrikePrice;
                    }

                    var key = item.PutOptions.InstrumentKey;
                    var prevOi = prevOiMap.TryGetValue(key, out var storedOi) ? storedOi : item.PutOptions.MarketData.PreviousOpenInterest;
                    var oiChange = putOi - prevOi;
                    var oiChangePercent = prevOi > 0 ? (oiChange / prevOi) * 100m : 0m;

                    quotes.Add(new OptionQuote
                    {
                        InstrumentKey = key,
                        StrikePrice = item.StrikePrice,
                        OptionType = OptionType.PE,
                        ExpiryDate = parsedExpiry.Date,
                        Ltp = item.PutOptions.MarketData.Ltp,
                        BidPrice = item.PutOptions.MarketData.BidPrice,
                        AskPrice = item.PutOptions.MarketData.AskPrice,
                        Volume = item.PutOptions.MarketData.Volume,
                        OpenInterest = putOi,
                        PreviousOpenInterest = prevOi,
                        IntradayOiChange = oiChange,
                        IntradayOiChangePercent = oiChangePercent,
                        ImpliedVolatility = item.PutOptions.OptionGreeks?.ImpliedVolatility,
                        Delta = item.PutOptions.OptionGreeks?.Delta,
                        Gamma = item.PutOptions.OptionGreeks?.Gamma,
                        Theta = item.PutOptions.OptionGreeks?.Theta,
                        Vega = item.PutOptions.OptionGreeks?.Vega,
                        SnapshotTimeUtc = snapshotTimeUtc
                    });
                }
            }

            var pcr = totalCallOi > 0 ? Math.Round(totalPutOi / totalCallOi, 4) : 0m;
            var maxPain = CalculateMaxPain(chainItems);

            var snapshot = new OptionChainSnapshot
            {
                UnderlyingKey = underlyingKey,
                UnderlyingPrice = spotPrice,
                ExpiryDate = parsedExpiry.Date,
                SnapshotTimeUtc = snapshotTimeUtc,
                TotalCallOi = totalCallOi,
                TotalPutOi = totalPutOi,
                Pcr = pcr,
                MaxPainStrike = maxPain,
                HighestCallOiStrike = highestCallStrike,
                HighestPutOiStrike = highestPutStrike,
                Quotes = quotes
            };

            // Persist to database
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();
                db.OptionChainSnapshots.Add(snapshot);
                await db.SaveChangesAsync(ct);
            }

            _logger.LogInformation("Saved Option Chain Snapshot: PCR={Pcr}, MaxPain={MaxPain}, CallOI={CallOi}, PutOI={PutOi}, Quotes={Count}",
                pcr, maxPain, totalCallOi, totalPutOi, quotes.Count);

            // Notify subscribers
            if (OnSnapshotCaptured != null)
            {
                await OnSnapshotCaptured.Invoke(snapshot);
            }

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture option chain snapshot for {Underlying}", underlyingKey);
            return null;
        }
    }

    public Task<OptionChainSnapshot?> GetLatestSnapshotAsync(string underlyingKey = "NSE_INDEX|Nifty 50", CancellationToken ct = default)
    {
        return GetLatestSnapshotAsync(underlyingKey, null, ct);
    }

    public async Task<OptionChainSnapshot?> GetLatestSnapshotAsync(
        string underlyingKey,
        string? expiryDate,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NiftyQuantDbContext>();

        var query = db.OptionChainSnapshots
            .Include(s => s.Quotes)
            .Where(s => s.UnderlyingKey == underlyingKey);

        if (!string.IsNullOrWhiteSpace(expiryDate) && DateTime.TryParse(expiryDate, out var exp))
        {
            query = query.Where(s => s.ExpiryDate == exp.Date);
        }

        return await query
            .OrderByDescending(s => s.SnapshotTimeUtc)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);
    }

    public static decimal CalculateMaxPain(IReadOnlyList<OptionChainItemDto> chain)
    {
        if (chain.Count == 0) return 0m;

        var strikes = chain.Select(c => c.StrikePrice).Distinct().OrderBy(s => s).ToList();
        decimal minTotalLoss = decimal.MaxValue;
        decimal maxPainStrike = strikes[0];

        foreach (var hypotheticalExpiryPrice in strikes)
        {
            decimal totalLossAtPrice = 0m;

            foreach (var item in chain)
            {
                // Call Option Loss: max(0, Price - Strike) * Call OI
                if (item.CallOptions?.MarketData != null && hypotheticalExpiryPrice > item.StrikePrice)
                {
                    totalLossAtPrice += (hypotheticalExpiryPrice - item.StrikePrice) * item.CallOptions.MarketData.OpenInterest;
                }

                // Put Option Loss: max(0, Strike - Price) * Put OI
                if (item.PutOptions?.MarketData != null && hypotheticalExpiryPrice < item.StrikePrice)
                {
                    totalLossAtPrice += (item.StrikePrice - hypotheticalExpiryPrice) * item.PutOptions.MarketData.OpenInterest;
                }
            }

            if (totalLossAtPrice < minTotalLoss)
            {
                minTotalLoss = totalLossAtPrice;
                maxPainStrike = hypotheticalExpiryPrice;
            }
        }

        return maxPainStrike;
    }
}
