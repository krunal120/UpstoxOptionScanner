using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Application.Common.Interfaces;

public interface IUpstoxWebSocketClient
{
    bool IsConnected { get; }
    IReadOnlySet<string> SubscribedInstruments { get; }

    event Func<MarketTick, Task>? OnTickReceived;
    event Func<bool, string, Task>? OnConnectionStateChanged;

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task SubscribeAsync(IEnumerable<string> instrumentKeys, string mode = "full_d5", CancellationToken ct = default);
    Task UnsubscribeAsync(IEnumerable<string> instrumentKeys, CancellationToken ct = default);
}
