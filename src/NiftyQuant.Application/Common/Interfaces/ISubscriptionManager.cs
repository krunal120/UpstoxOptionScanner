namespace NiftyQuant.Application.Common.Interfaces;

public interface ISubscriptionManager
{
    decimal CurrentAtmStrike { get; }
    string CurrentExpiry { get; }
    IReadOnlyCollection<string> TrackedKeys { get; }

    Task InitializeAsync(CancellationToken ct = default);
    Task OnSpotPriceUpdateAsync(decimal spotPrice, CancellationToken ct = default);
    Task OnSpotPriceUpdateAsync(string instrumentKey, decimal spotPrice, CancellationToken ct = default);
    string GetCurrentExpiry(string underlyingKey);
}
