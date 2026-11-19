using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Infrastructure;
using Xunit;

namespace NiftyQuant.IntegrationTests;

public class WebSocketIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUpstoxWebSocketClient _webSocketClient;
    private readonly ISubscriptionManager _subscriptionManager;

    public WebSocketIntegrationTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets("niftyquant-api-secrets-2026")
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        _serviceProvider = services.BuildServiceProvider();
        _webSocketClient = _serviceProvider.GetRequiredService<IUpstoxWebSocketClient>();
        _subscriptionManager = _serviceProvider.GetRequiredService<ISubscriptionManager>();
    }

    [Fact]
    public async Task WebSocketClient_CanConnectAndDisconnect()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Connect
        await _webSocketClient.ConnectAsync(cts.Token);
        _webSocketClient.IsConnected.Should().BeTrue();

        // Subscribe to NIFTY 50 spot
        await _webSocketClient.SubscribeAsync(new[] { "NSE_INDEX|Nifty 50" }, "full_d5", cts.Token);
        _webSocketClient.SubscribedInstruments.Should().Contain("NSE_INDEX|Nifty 50");

        // Disconnect
        await _webSocketClient.DisconnectAsync(cts.Token);
        _webSocketClient.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task SubscriptionManager_Initialize_ShouldSelectAtmAndExpiry()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await _subscriptionManager.InitializeAsync(cts.Token);

        _subscriptionManager.CurrentAtmStrike.Should().BeGreaterThan(10000m);
        _subscriptionManager.CurrentExpiry.Should().NotBeNullOrEmpty();
        _subscriptionManager.TrackedKeys.Should().Contain("NSE_INDEX|Nifty 50");
        _subscriptionManager.TrackedKeys.Count.Should().BeGreaterThan(2); // Spot + VIX + ATM strikes
    }
}
