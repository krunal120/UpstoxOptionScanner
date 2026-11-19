using Microsoft.AspNetCore.SignalR;

namespace NiftyQuant.Api.Hubs;

public class MarketHub : Hub
{
    private readonly ILogger<MarketHub> _logger;

    public MarketHub(ILogger<MarketHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to MarketHub: {ConnectionId}", Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "MarketFeed");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from MarketHub: {ConnectionId}", Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "MarketFeed");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeUnderlying(string underlyingKey)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, underlyingKey);
        _logger.LogInformation("Client {ConnectionId} subscribed to {Key}", Context.ConnectionId, underlyingKey);
    }

    public async Task UnsubscribeUnderlying(string underlyingKey)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, underlyingKey);
        _logger.LogInformation("Client {ConnectionId} unsubscribed from {Key}", Context.ConnectionId, underlyingKey);
    }
}
