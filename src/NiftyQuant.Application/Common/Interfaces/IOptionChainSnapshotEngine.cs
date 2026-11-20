using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Application.Common.Interfaces;

public interface IOptionChainSnapshotEngine
{
    event Func<OptionChainSnapshot, Task>? OnSnapshotCaptured;

    Task<OptionChainSnapshot?> CaptureAndPersistSnapshotAsync(
        string underlyingKey = "NSE_INDEX|Nifty 50",
        string? expiryDate = null,
        CancellationToken ct = default);

    Task<OptionChainSnapshot?> GetLatestSnapshotAsync(
        string underlyingKey = "NSE_INDEX|Nifty 50",
        CancellationToken ct = default);

    Task<OptionChainSnapshot?> GetLatestSnapshotAsync(
        string underlyingKey,
        string? expiryDate,
        CancellationToken ct = default);
}
