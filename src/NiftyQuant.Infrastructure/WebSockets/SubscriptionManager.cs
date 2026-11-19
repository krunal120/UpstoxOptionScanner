using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Upstox;

namespace NiftyQuant.Infrastructure.WebSockets;

public class SubscriptionManager : ISubscriptionManager
{
    private const string NiftyIndexKey = "NSE_INDEX|Nifty 50";
    private const string IndiaVixKey = "NSE_INDEX|India VIX";
    private const decimal StrikeStep = 50m;
    private const int StrikesAroundAtm = 5; // ATM +- 5 strikes

    private readonly IUpstoxClient _upstoxClient;
    private readonly IUpstoxWebSocketClient _webSocketClient;
    private readonly ILogger<SubscriptionManager> _logger;

    private readonly ConcurrentDictionary<string, byte> _trackedKeys = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<OptionContractDto> _contracts = new List<OptionContractDto>();
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
            _logger.LogInformation("Initializing Subscription Manager for NIFTY 50 options...");

            // Subscribe to Spot and VIX
            _trackedKeys[NiftyIndexKey] = 1;
            _trackedKeys[IndiaVixKey] = 1;
            await _webSocketClient.SubscribeAsync(new[] { NiftyIndexKey, IndiaVixKey }, "full_d5", ct);

            // Fetch active contracts
            _contracts = await _upstoxClient.GetOptionContractsAsync(NiftyIndexKey, ct);
            if (_contracts.Count == 0)
            {
                _logger.LogWarning("No option contracts returned for {Underlying}", NiftyIndexKey);
                return;
            }

            // Pick nearest active expiry
            _currentExpiry = _contracts
                .Select(c => c.Expiry)
                .Distinct()
                .OrderBy(e => e)
                .FirstOrDefault() ?? string.Empty;

            _logger.LogInformation("Selected nearest option expiry: {Expiry}", _currentExpiry);

            // Get initial spot price
            var spotQuote = await _upstoxClient.GetMarketQuoteAsync(NiftyIndexKey, ct);
            var spotPrice = spotQuote?.LastPrice ?? 0m;

            if (spotPrice > 0m)
            {
                await UpdateAtmSubscriptionsInternalAsync(spotPrice, ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task OnSpotPriceUpdateAsync(decimal spotPrice, CancellationToken ct = default)
    {
        if (spotPrice <= 0m) return;

        var newAtm = CalculateAtmStrike(spotPrice);
        if (newAtm == _currentAtmStrike) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (newAtm != _currentAtmStrike)
            {
                await UpdateAtmSubscriptionsInternalAsync(spotPrice, ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task UpdateAtmSubscriptionsInternalAsync(decimal spotPrice, CancellationToken ct)
    {
        var newAtm = CalculateAtmStrike(spotPrice);
        _logger.LogInformation("NIFTY Spot: {Spot}. Updating ATM strike to: {Atm} for expiry {Expiry}", spotPrice, newAtm, _currentExpiry);

        var minStrike = newAtm - (StrikesAroundAtm * StrikeStep);
        var maxStrike = newAtm + (StrikesAroundAtm * StrikeStep);

        var expiryContracts = _contracts
            .Where(c => c.Expiry == _currentExpiry && c.StrikePrice >= minStrike && c.StrikePrice <= maxStrike)
            .ToList();

        var desiredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NiftyIndexKey,
            IndiaVixKey
        };

        foreach (var c in expiryContracts)
        {
            desiredKeys.Add(c.InstrumentKey);
        }

        // Keys to add
        var toSubscribe = desiredKeys.Where(k => !_trackedKeys.ContainsKey(k)).ToList();

        // Keys to drop (only option contracts, not spot or vix)
        var toUnsubscribe = _trackedKeys.Keys
            .Where(k => !desiredKeys.Contains(k) && k != NiftyIndexKey && k != IndiaVixKey)
            .ToList();

        if (toSubscribe.Count > 0)
        {
            _logger.LogInformation("Subscribing to {Count} new option strike contracts around ATM {Atm}", toSubscribe.Count, newAtm);
            foreach (var k in toSubscribe) _trackedKeys[k] = 1;
            await _webSocketClient.SubscribeAsync(toSubscribe, "full_d5", ct);
        }

        if (toUnsubscribe.Count > 0)
        {
            _logger.LogInformation("Unsubscribing from {Count} distant contracts for ATM {Atm}", toUnsubscribe.Count, newAtm);
            foreach (var k in toUnsubscribe) _trackedKeys.TryRemove(k, out _);
            await _webSocketClient.UnsubscribeAsync(toUnsubscribe, ct);
        }

        _currentAtmStrike = newAtm;
    }

    public static decimal CalculateAtmStrike(decimal spotPrice)
    {
        return Math.Round(spotPrice / StrikeStep, MidpointRounding.AwayFromZero) * StrikeStep;
    }
}
