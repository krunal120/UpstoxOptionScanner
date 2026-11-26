using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Upstox;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Infrastructure.WebSockets;

public class SubscriptionManager : ISubscriptionManager
{
    private const string NiftyIndexKey = "NSE_INDEX|Nifty 50";
    private const string IndiaVixKey = "NSE_INDEX|India VIX";
    private const int StrikesAroundAtm = 5; // ATM +- 5 strikes

    private readonly IUpstoxClient _upstoxClient;
    private readonly IUpstoxWebSocketClient _webSocketClient;
    private readonly ILogger<SubscriptionManager> _logger;

    private readonly ConcurrentDictionary<string, byte> _trackedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IndexSubState> _indexStates = new(StringComparer.OrdinalIgnoreCase);
    private string _currentExpiry = string.Empty;
    private decimal _currentAtmStrike = 0m;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public decimal CurrentAtmStrike => _currentAtmStrike;
    public string CurrentExpiry => _currentExpiry;
    public IReadOnlyCollection<string> TrackedKeys => _trackedKeys.Keys.ToList();

    public SubscriptionManager(
        IUpstoxClient upstoxClient,
        IUpstoxWebSocketClient webSocketClient,
        ILogger<SubscriptionManager> logger)
    {
        _upstoxClient = upstoxClient;
        _webSocketClient = webSocketClient;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _logger.LogInformation("Initializing Subscription Manager for Multi-Index options (NIFTY 50, Bank Nifty, Sensex)...");

            var spotKeysToSubscribe = new List<string> { IndiaVixKey };
            _trackedKeys[IndiaVixKey] = 1;

            var todayUtc = DateTime.UtcNow.Date;

            foreach (var profile in IndexRegistry.SupportedIndices.Where(p => p.IsEnabled))
            {
                spotKeysToSubscribe.Add(profile.UnderlyingKey);
                _trackedKeys[profile.UnderlyingKey] = 1;

                var state = new IndexSubState
                {
                    Profile = profile
                };

                try
                {
                    state.Contracts = await _upstoxClient.GetOptionContractsAsync(profile.UnderlyingKey, ct);
                    if (state.Contracts.Count > 0)
                    {
                        state.CurrentExpiry = state.Contracts
                            .Where(c => DateTime.TryParse(c.Expiry, out var dt) && dt.Date >= todayUtc)
                            .Select(c => c.Expiry)
                            .Distinct()
                            .OrderBy(e => DateTime.Parse(e))
                            .FirstOrDefault() ?? string.Empty;

                        _logger.LogInformation("[{Symbol}] Selected nearest active weekly expiry: {Expiry} ({Count} contracts available)",
                            profile.Symbol, state.CurrentExpiry, state.Contracts.Count);
                    }
                    else
                    {
                        _logger.LogWarning("[{Symbol}] No option contracts returned for {Underlying}", profile.Symbol, profile.UnderlyingKey);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[{Symbol}] Could not load option contracts for {Underlying}", profile.Symbol, profile.UnderlyingKey);
                }

                _indexStates[profile.UnderlyingKey] = state;

                if (profile.UnderlyingKey == NiftyIndexKey && !string.IsNullOrEmpty(state.CurrentExpiry))
                {
                    _currentExpiry = state.CurrentExpiry;
                }
            }

            // Subscribe to all Spot and VIX feeds via WebSocket
            await _webSocketClient.SubscribeAsync(spotKeysToSubscribe, "full_d5", ct);

            // Fetch initial spot price and initialize ATM strikes for each index
            foreach (var profile in IndexRegistry.SupportedIndices.Where(p => p.IsEnabled))
            {
                try
                {
                    var spotQuote = await _upstoxClient.GetMarketQuoteAsync(profile.UnderlyingKey, ct);
                    var spotPrice = spotQuote?.LastPrice ?? 0m;
                    if (spotPrice > 0m)
                    {
                        await UpdateAtmSubscriptionsInternalAsync(profile.UnderlyingKey, spotPrice, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Initial spot quote fetch failed for {Key}", profile.UnderlyingKey);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task OnSpotPriceUpdateAsync(decimal spotPrice, CancellationToken ct = default)
    {
        return OnSpotPriceUpdateAsync(NiftyIndexKey, spotPrice, ct);
    }

    public async Task OnSpotPriceUpdateAsync(string instrumentKey, decimal spotPrice, CancellationToken ct = default)
    {
        if (spotPrice <= 0m || string.IsNullOrWhiteSpace(instrumentKey)) return;

        var profile = IndexRegistry.GetProfile(instrumentKey);
        if (!_indexStates.TryGetValue(profile.UnderlyingKey, out var state)) return;

        var newAtm = CalculateAtmStrike(spotPrice, profile.StrikeInterval);
        if (newAtm == state.CurrentAtmStrike) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (newAtm != state.CurrentAtmStrike)
            {
                await UpdateAtmSubscriptionsInternalAsync(profile.UnderlyingKey, spotPrice, ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public string GetCurrentExpiry(string underlyingKey)
    {
        var profile = IndexRegistry.GetProfile(underlyingKey);
        if (_indexStates.TryGetValue(profile.UnderlyingKey, out var state) && !string.IsNullOrWhiteSpace(state.CurrentExpiry))
        {
            return state.CurrentExpiry;
        }

        return _currentExpiry;
    }

    private async Task UpdateAtmSubscriptionsInternalAsync(string underlyingKey, decimal spotPrice, CancellationToken ct)
    {
        var profile = IndexRegistry.GetProfile(underlyingKey);
        if (!_indexStates.TryGetValue(profile.UnderlyingKey, out var state)) return;

        var newAtm = CalculateAtmStrike(spotPrice, profile.StrikeInterval);
        _logger.LogInformation("[{Symbol}] Spot: {Spot}. Updating ATM strike to: {Atm} for expiry {Expiry}",
            profile.Symbol, spotPrice, newAtm, state.CurrentExpiry);

        var minStrike = newAtm - (StrikesAroundAtm * profile.StrikeInterval);
        var maxStrike = newAtm + (StrikesAroundAtm * profile.StrikeInterval);

        var expiryContracts = state.Contracts
            .Where(c => c.Expiry == state.CurrentExpiry && c.StrikePrice >= minStrike && c.StrikePrice <= maxStrike)
            .ToList();

        var desiredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            profile.UnderlyingKey,
            IndiaVixKey
        };

        foreach (var c in expiryContracts)
        {
            desiredKeys.Add(c.InstrumentKey);
        }

        // Keys to add
        var toSubscribe = desiredKeys.Where(k => !_trackedKeys.ContainsKey(k)).ToList();

        if (toSubscribe.Count > 0)
        {
            _logger.LogInformation("[{Symbol}] Subscribing to {Count} new option strike contracts around ATM {Atm}",
                profile.Symbol, toSubscribe.Count, newAtm);
            foreach (var k in toSubscribe) _trackedKeys[k] = 1;
            await _webSocketClient.SubscribeAsync(toSubscribe, "full_d5", ct);
        }

        state.CurrentAtmStrike = newAtm;
        if (profile.UnderlyingKey == NiftyIndexKey)
        {
            _currentAtmStrike = newAtm;
        }
    }

    public static decimal CalculateAtmStrike(decimal spotPrice, decimal strikeStep = 50m)
    {
        return Math.Round(spotPrice / strikeStep, MidpointRounding.AwayFromZero) * strikeStep;
    }

    private class IndexSubState
    {
        public IndexProfile Profile { get; set; } = null!;
        public string CurrentExpiry { get; set; } = string.Empty;
        public decimal CurrentAtmStrike { get; set; } = 0m;
        public IReadOnlyList<OptionContractDto> Contracts { get; set; } = new List<OptionContractDto>();
    }
}
