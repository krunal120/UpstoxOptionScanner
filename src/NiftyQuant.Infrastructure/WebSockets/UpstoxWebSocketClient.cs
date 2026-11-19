using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Com.Upstox.Marketdatafeederv3Udapi.Rpc.Proto;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Upstox;
using NiftyQuant.Application.Common.Options;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Infrastructure.WebSockets;

public class UpstoxWebSocketClient : IUpstoxWebSocketClient, IAsyncDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpstoxOptions _options;
    private readonly ILogger<UpstoxWebSocketClient> _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<string, string> _subscribedInstruments = new(StringComparer.OrdinalIgnoreCase);

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _connectionCts;
    private Task? _receiveLoopTask;
    private bool _isDisposed;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;
    public IReadOnlySet<string> SubscribedInstruments => _subscribedInstruments.Keys.ToHashSet();

    public event Func<MarketTick, Task>? OnTickReceived;
    public event Func<bool, string, Task>? OnConnectionStateChanged;

    public UpstoxWebSocketClient(
        IHttpClientFactory httpClientFactory,
        IOptions<UpstoxOptions> options,
        ILogger<UpstoxWebSocketClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (IsConnected) return;

        _connectionCts?.Cancel();
        _connectionCts = new CancellationTokenSource();
        var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(ct, _connectionCts.Token).Token;

        try
        {
            _logger.LogInformation("Requesting authorized WebSocket URL from Upstox API...");
            var authUrl = await GetAuthorizedRedirectUriAsync(linkedToken);

            _webSocket?.Dispose();
            _webSocket = new ClientWebSocket();

            _logger.LogInformation("Connecting to Upstox Market Data Feed V3 WebSocket...");
            await _webSocket.ConnectAsync(new Uri(authUrl), linkedToken);

            _logger.LogInformation("Connected successfully to Upstox WebSocket Feed V3.");
            NotifyConnectionState(true, "Connected");

            // Resubscribe if restoring connection
            if (!_subscribedInstruments.IsEmpty)
            {
                await ResubscribeAllAsync(linkedToken);
            }

            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_connectionCts.Token), _connectionCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Upstox WebSocket feed");
            NotifyConnectionState(false, ex.Message);
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_webSocket == null) return;

        _logger.LogInformation("Disconnecting from Upstox WebSocket feed...");
        _connectionCts?.Cancel();

        if (_webSocket.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while closing WebSocket");
            }
        }

        _webSocket.Dispose();
        _webSocket = null;
        NotifyConnectionState(false, "Disconnected by client");
    }

    public async Task SubscribeAsync(IEnumerable<string> instrumentKeys, string mode = "full_d5", CancellationToken ct = default)
    {
        var keysList = instrumentKeys.Distinct().ToList();
        if (keysList.Count == 0) return;

        foreach (var key in keysList)
        {
            _subscribedInstruments[key] = mode;
        }

        if (IsConnected)
        {
            await SendSubscriptionMessageAsync("sub", mode, keysList, ct);
        }
    }

    public async Task UnsubscribeAsync(IEnumerable<string> instrumentKeys, CancellationToken ct = default)
    {
        var keysList = instrumentKeys.Distinct().ToList();
        if (keysList.Count == 0) return;

        foreach (var key in keysList)
        {
            _subscribedInstruments.TryRemove(key, out _);
        }

        if (IsConnected)
        {
            await SendUnsubscribeMessageAsync(keysList, ct);
        }
    }

    private async Task<string> GetAuthorizedRedirectUriAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("UpstoxAuthClient");
        var url = "/v3/feed/market-data-feed/authorize";
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("data", out var dataElem))
        {
            if (dataElem.TryGetProperty("authorizedRedirectUri", out var uriElem) ||
                dataElem.TryGetProperty("authorized_redirect_uri", out uriElem))
            {
                var uri = uriElem.GetString();
                if (!string.IsNullOrEmpty(uri)) return uri;
            }
        }

        throw new InvalidOperationException("Failed to obtain authorized WebSocket redirect URI from Upstox response: " + json);
    }

    private async Task SendSubscriptionMessageAsync(string method, string mode, List<string> keys, CancellationToken ct)
    {
        var payload = new
        {
            guid = Guid.NewGuid().ToString(),
            method = method,
            data = new
            {
                mode = mode,
                instrumentKeys = keys
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(ct);
        try
        {
            if (IsConnected && _webSocket != null)
            {
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
                _logger.LogInformation("Sent {Method} request for {Count} instruments in mode {Mode}", method, keys.Count, mode);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task SendUnsubscribeMessageAsync(List<string> keys, CancellationToken ct)
    {
        var payload = new
        {
            guid = Guid.NewGuid().ToString(),
            method = "unsub",
            data = new
            {
                instrumentKeys = keys
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(ct);
        try
        {
            if (IsConnected && _webSocket != null)
            {
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
                _logger.LogInformation("Sent unsub request for {Count} instruments", keys.Count);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ResubscribeAllAsync(CancellationToken ct)
    {
        var grouped = _subscribedInstruments.GroupBy(kvp => kvp.Value);
        foreach (var group in grouped)
        {
            var keys = group.Select(g => g.Key).ToList();
            await SendSubscriptionMessageAsync("sub", group.Key, keys, ct);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[65536];
        using var ms = new MemoryStream();

        while (!ct.IsCancellationRequested && IsConnected)
        {
            try
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;

                do
                {
                    result = await _webSocket!.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogWarning("WebSocket closed by remote server: {Status} {Description}", result.CloseStatus, result.CloseStatusDescription);
                        NotifyConnectionState(false, "Closed by server");
                        _ = TriggerReconnectAsync();
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Binary && ms.Length > 0)
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    DecodeProtobufMessage(ms);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WebSocket receive loop");
                NotifyConnectionState(false, ex.Message);
                _ = TriggerReconnectAsync();
                break;
            }
        }
    }

    private void DecodeProtobufMessage(Stream stream)
    {
        try
        {
            var feedResponse = FeedResponse.Parser.ParseFrom(stream);
            if (feedResponse?.Feeds == null || feedResponse.Feeds.Count == 0) return;

            foreach (var (instrumentKey, feed) in feedResponse.Feeds)
            {
                var tick = ParseTick(instrumentKey, feed);
                if (tick != null)
                {
                    _ = OnTickReceived?.Invoke(tick);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decoding Protobuf MarketDataFeed frame");
        }
    }

    public static MarketTick? ParseTick(string instrumentKey, Feed feed)
    {
        if (feed == null) return null;

        var tick = new MarketTick
        {
            InstrumentKey = instrumentKey,
            TimestampUtc = DateTime.UtcNow
        };

        if (feed.FeedUnionCase == Feed.FeedUnionOneofCase.Ltpc && feed.Ltpc != null)
        {
            tick.Ltp = (decimal)feed.Ltpc.Ltp;
            tick.LastTradedQuantity = feed.Ltpc.Ltq;
            tick.ClosePrice = (decimal)feed.Ltpc.Cp;
            if (feed.Ltpc.Ltt > 0)
            {
                tick.LastTradedTime = DateTimeOffset.FromUnixTimeMilliseconds(feed.Ltpc.Ltt).UtcDateTime;
            }
            return tick;
        }

        if (feed.FeedUnionCase == Feed.FeedUnionOneofCase.FullFeed && feed.FullFeed != null)
        {
            if (feed.FullFeed.FullFeedUnionCase == FullFeed.FullFeedUnionOneofCase.MarketFF && feed.FullFeed.MarketFF != null)
            {
                var mff = feed.FullFeed.MarketFF;
                if (mff.Ltpc != null)
                {
                    tick.Ltp = (decimal)mff.Ltpc.Ltp;
                    tick.LastTradedQuantity = mff.Ltpc.Ltq;
                    tick.ClosePrice = (decimal)mff.Ltpc.Cp;
                }

                tick.Volume = mff.Vtt;
                tick.OpenInterest = (decimal)mff.Oi;
                tick.ImpliedVolatility = (decimal)mff.Iv;

                if (mff.OptionGreeks != null)
                {
                    tick.Delta = (decimal)mff.OptionGreeks.Delta;
                    tick.Theta = (decimal)mff.OptionGreeks.Theta;
                    tick.Gamma = (decimal)mff.OptionGreeks.Gamma;
                    tick.Vega = (decimal)mff.OptionGreeks.Vega;
                }

                if (mff.MarketLevel?.BidAskQuote != null && mff.MarketLevel.BidAskQuote.Count > 0)
                {
                    var topQuote = mff.MarketLevel.BidAskQuote[0];
                    tick.BidPrice = (decimal)topQuote.BidP;
                    tick.BidQuantity = (int)topQuote.BidQ;
                    tick.AskPrice = (decimal)topQuote.AskP;
                    tick.AskQuantity = (int)topQuote.AskQ;
                }

                if (mff.MarketOHLC?.Ohlc != null && mff.MarketOHLC.Ohlc.Count > 0)
                {
                    var ohlc = mff.MarketOHLC.Ohlc[0];
                    tick.Open = (decimal)ohlc.Open;
                    tick.High = (decimal)ohlc.High;
                    tick.Low = (decimal)ohlc.Low;
                    tick.Close = (decimal)ohlc.Close;
                }

                return tick;
            }

            if (feed.FullFeed.FullFeedUnionCase == FullFeed.FullFeedUnionOneofCase.IndexFF && feed.FullFeed.IndexFF != null)
            {
                var iff = feed.FullFeed.IndexFF;
                tick.IsIndex = true;
                if (iff.Ltpc != null)
                {
                    tick.Ltp = (decimal)iff.Ltpc.Ltp;
                    tick.ClosePrice = (decimal)iff.Ltpc.Cp;
                }

                if (iff.MarketOHLC?.Ohlc != null && iff.MarketOHLC.Ohlc.Count > 0)
                {
                    var ohlc = iff.MarketOHLC.Ohlc[0];
                    tick.Open = (decimal)ohlc.Open;
                    tick.High = (decimal)ohlc.High;
                    tick.Low = (decimal)ohlc.Low;
                    tick.Close = (decimal)ohlc.Close;
                }

                return tick;
            }
        }

        if (feed.FeedUnionCase == Feed.FeedUnionOneofCase.FirstLevelWithGreeks && feed.FirstLevelWithGreeks != null)
        {
            var fl = feed.FirstLevelWithGreeks;
            if (fl.Ltpc != null)
            {
                tick.Ltp = (decimal)fl.Ltpc.Ltp;
                tick.ClosePrice = (decimal)fl.Ltpc.Cp;
            }

            tick.Volume = fl.Vtt;
            tick.OpenInterest = (decimal)fl.Oi;
            tick.ImpliedVolatility = (decimal)fl.Iv;

            if (fl.FirstDepth != null)
            {
                tick.BidPrice = (decimal)fl.FirstDepth.BidP;
                tick.BidQuantity = (int)fl.FirstDepth.BidQ;
                tick.AskPrice = (decimal)fl.FirstDepth.AskP;
                tick.AskQuantity = (int)fl.FirstDepth.AskQ;
            }

            if (fl.OptionGreeks != null)
            {
                tick.Delta = (decimal)fl.OptionGreeks.Delta;
                tick.Theta = (decimal)fl.OptionGreeks.Theta;
                tick.Gamma = (decimal)fl.OptionGreeks.Gamma;
                tick.Vega = (decimal)fl.OptionGreeks.Vega;
            }

            return tick;
        }

        return tick;
    }

    private async Task TriggerReconnectAsync()
    {
        if (_isDisposed) return;

        _logger.LogWarning("Triggering automatic reconnection in 3 seconds...");
        await Task.Delay(3000);

        try
        {
            await ConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reconnection attempt failed");
        }
    }

    private void NotifyConnectionState(bool connected, string message)
    {
        _ = OnConnectionStateChanged?.Invoke(connected, message);
    }

    public async ValueTask DisposeAsync()
    {
        _isDisposed = true;
        _connectionCts?.Cancel();
        _connectionCts?.Dispose();
        _webSocket?.Dispose();
        _sendLock.Dispose();
        await Task.CompletedTask;
    }
}
