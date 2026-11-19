using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Models.Upstox;
using NiftyQuant.Application.Common.Options;

namespace NiftyQuant.Infrastructure.Services;

public class UpstoxClient : IUpstoxClient
{
    private readonly HttpClient _httpClient;
    private readonly UpstoxOptions _options;
    private readonly ILogger<UpstoxClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    public UpstoxClient(
        HttpClient httpClient,
        IOptions<UpstoxOptions> options,
        ILogger<UpstoxClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MarketQuoteDto?> GetMarketQuoteAsync(string instrumentKey, CancellationToken ct = default)
    {
        var quotes = await GetMarketQuotesAsync(new[] { instrumentKey }, ct);
        return quotes.Values.FirstOrDefault();
    }

    public async Task<Dictionary<string, MarketQuoteDto>> GetMarketQuotesAsync(IEnumerable<string> instrumentKeys, CancellationToken ct = default)
    {
        var keysList = instrumentKeys.ToList();
        if (keysList.Count == 0) return new Dictionary<string, MarketQuoteDto>();

        var joinedKeys = string.Join(",", keysList.Select(Uri.EscapeDataString));
        var url = $"/v2/market-quote/quotes?instrument_key={joinedKeys}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<UpstoxResponse<Dictionary<string, MarketQuoteDto>>>(json, JsonOptions);

            var result = new Dictionary<string, MarketQuoteDto>(StringComparer.OrdinalIgnoreCase);
            if (parsed?.Data != null)
            {
                foreach (var kvp in parsed.Data)
                {
                    // Upstox formats key as "NSE_INDEX:Nifty 50", normalize both : and |
                    var normalizedKey = kvp.Key.Replace(':', '|');
                    result[normalizedKey] = kvp.Value;
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch market quotes for {Count} instruments", keysList.Count);
            throw;
        }
    }

    public async Task<IReadOnlyList<OptionContractDto>> GetOptionContractsAsync(string underlyingKey = "NSE_INDEX|Nifty 50", CancellationToken ct = default)
    {
        var url = $"/v2/option/contract?instrument_key={Uri.EscapeDataString(underlyingKey)}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<UpstoxResponse<List<OptionContractDto>>>(json, JsonOptions);

            return parsed?.Data ?? new List<OptionContractDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch option contracts for {UnderlyingKey}", underlyingKey);
            throw;
        }
    }

    public async Task<IReadOnlyList<OptionChainItemDto>> GetOptionChainAsync(string underlyingKey, string expiryDate, CancellationToken ct = default)
    {
        var url = $"/v2/option/chain?instrument_key={Uri.EscapeDataString(underlyingKey)}&expiry_date={expiryDate}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<UpstoxResponse<List<OptionChainItemDto>>>(json, JsonOptions);

            return parsed?.Data ?? new List<OptionChainItemDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch option chain for {UnderlyingKey} expiry {ExpiryDate}", underlyingKey, expiryDate);
            throw;
        }
    }

    public async Task<IReadOnlyList<CandleDto>> GetHistoricalCandlesAsync(
        string instrumentKey,
        string unit,
        int interval,
        string toDate,
        string fromDate,
        CancellationToken ct = default)
    {
        // Upstox V3 expects 'minutes', 'hours', 'day', 'week', 'month'
        var normalizedUnit = unit.ToLowerInvariant();
        if (normalizedUnit == "minute" || normalizedUnit == "min") normalizedUnit = "minutes";
        if (normalizedUnit == "hour") normalizedUnit = "hours";

        var url = $"/v3/historical-candle/{Uri.EscapeDataString(instrumentKey)}/{normalizedUnit}/{interval}/{toDate}/{fromDate}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<UpstoxResponse<HistoricalCandlesDataDto>>(json, JsonOptions);

            return ParseCandles(parsed?.Data?.RawCandles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch historical candles for {InstrumentKey}", instrumentKey);
            throw;
        }
    }

    public async Task<IReadOnlyList<CandleDto>> GetIntradayCandlesAsync(
        string instrumentKey,
        string unit,
        int interval,
        CancellationToken ct = default)
    {
        var normalizedUnit = unit.ToLowerInvariant();
        if (normalizedUnit == "minute" || normalizedUnit == "min") normalizedUnit = "minutes";

        var url = $"/v3/historical-candle/intraday/{Uri.EscapeDataString(instrumentKey)}/{normalizedUnit}/{interval}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<UpstoxResponse<HistoricalCandlesDataDto>>(json, JsonOptions);

            return ParseCandles(parsed?.Data?.RawCandles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch intraday candles for {InstrumentKey}", instrumentKey);
            throw;
        }
    }

    public async Task<IReadOnlyList<InstrumentMasterDto>> DownloadInstrumentMasterAsync(string segment = "NSE_FO", CancellationToken ct = default)
    {
        // Upstox publishes compressed JSON instrument masters on CloudFront
        var url = "https://assets.upstox.com/market-quote/instruments/exchange/NSE.json.gz";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);
            var json = await reader.ReadToEndAsync(ct);

            using var doc = JsonDocument.Parse(json);
            var result = new List<InstrumentMasterDto>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var seg = element.TryGetProperty("segment", out var segProp) ? segProp.GetString() : null;
                if (!string.IsNullOrEmpty(segment) && !string.Equals(seg, segment, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var item = new InstrumentMasterDto
                {
                    InstrumentKey = element.GetProperty("instrument_key").GetString() ?? string.Empty,
                    TradingSymbol = element.GetProperty("trading_symbol").GetString() ?? string.Empty,
                    Name = element.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "",
                    Segment = seg ?? "",
                    Exchange = element.TryGetProperty("exchange", out var exProp) ? exProp.GetString() ?? "" : "",
                    LotSize = element.TryGetProperty("lot_size", out var lsProp) && lsProp.TryGetInt32(out var ls) ? ls : 1,
                    TickSize = element.TryGetProperty("tick_size", out var tsProp) && tsProp.TryGetDecimal(out var ts) ? ts : 0.05m
                };

                if (element.TryGetProperty("strike_price", out var spProp) && spProp.TryGetDecimal(out var sp))
                {
                    item.StrikePrice = sp;
                }

                if (element.TryGetProperty("instrument_type", out var itProp))
                {
                    item.InstrumentType = itProp.GetString();
                }

                if (element.TryGetProperty("expiry", out var expProp) && DateTime.TryParse(expProp.GetString(), out var exp))
                {
                    item.Expiry = exp;
                }

                result.Add(item);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download and parse instrument master for segment {Segment}", segment);
            throw;
        }
    }

    private static IReadOnlyList<CandleDto> ParseCandles(List<List<object>>? rawCandles)
    {
        var result = new List<CandleDto>();
        if (rawCandles == null) return result;

        foreach (var c in rawCandles)
        {
            if (c.Count < 5) continue;

            // Upstox candle array: [timestamp, open, high, low, close, volume, oi]
            var timestamp = ParseDateTime(c[0]);
            var open = ParseDecimal(c[1]);
            var high = ParseDecimal(c[2]);
            var low = ParseDecimal(c[3]);
            var close = ParseDecimal(c[4]);
            var volume = c.Count > 5 ? ParseLong(c[5]) : 0L;
            var oi = c.Count > 6 ? ParseDecimal(c[6]) : 0m;

            result.Add(new CandleDto
            {
                Timestamp = timestamp,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                OpenInterest = oi
            });
        }

        // Return candles sorted chronologically (oldest to newest)
        return result.OrderBy(c => c.Timestamp).ToList();
    }

    private static decimal ParseDecimal(object obj)
    {
        if (obj is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number && je.TryGetDecimal(out var d)) return d;
            if (je.ValueKind == JsonValueKind.String && decimal.TryParse(je.GetString(), out var s)) return s;
            return 0m;
        }
        return obj != null ? Convert.ToDecimal(obj) : 0m;
    }

    private static long ParseLong(object obj)
    {
        if (obj is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number && je.TryGetInt64(out var l)) return l;
            if (je.ValueKind == JsonValueKind.String && long.TryParse(je.GetString(), out var s)) return s;
            return 0L;
        }
        return obj != null ? Convert.ToInt64(obj) : 0L;
    }

    private static DateTime ParseDateTime(object obj)
    {
        if (obj is JsonElement je)
        {
            var str = je.GetString();
            if (!string.IsNullOrEmpty(str) && DateTime.TryParse(str, out var dt)) return dt;
        }
        return obj != null && DateTime.TryParse(obj.ToString(), out var parsed) ? parsed : DateTime.MinValue;
    }
}
