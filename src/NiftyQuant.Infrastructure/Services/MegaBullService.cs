using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NiftyQuant.Application.Common.Interfaces;
using NiftyQuant.Application.Common.Options;

namespace NiftyQuant.Infrastructure.Services;

public class MegaBullService : IMegaBullService
{
    private readonly HttpClient _httpClient;
    private readonly MegaBullOptions _options;
    private readonly ILogger<MegaBullService> _logger;

    private readonly ConcurrentDictionary<string, string> _symbolToToken = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _keyToToken = new(StringComparer.OrdinalIgnoreCase);
    private bool _isLoaded = false;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public bool IsEnabled => _options.IsEnabled && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public MegaBullService(
        HttpClient httpClient,
        IOptions<MegaBullOptions> options,
        ILogger<MegaBullService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> PlaceOrderAsync(
        string tradingSymbol,
        string optionType,
        decimal strikePrice,
        DateTime expiryDate,
        int quantity,
        string side = "BUY",
        CancellationToken ct = default)
    {
        if (!IsEnabled) return null;

        try
        {
            await EnsureInstrumentsLoadedAsync(ct);

            var token = ResolveInstrumentToken(tradingSymbol, optionType, strikePrice, expiryDate);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("[MEGABULL] Could not resolve instrument token for {Symbol} Strike={Strike} Type={Type} Exp={Exp:dd-MMM-yyyy}",
                    tradingSymbol, strikePrice, optionType, expiryDate);
                return null;
            }

            var payload = new
            {
                instrumentToken = token,
                qty = quantity,
                type = side.ToUpperInvariant(),
                duration = "MIS",
                orderType = "MKT",
                price = 0
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/order/buysell");
            request.Headers.Add("api-key", _options.ApiKey);
            request.Content = jsonContent;

            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                var orderId = doc.RootElement.TryGetProperty("id", out var idElem) ? idElem.ToString() : "OK";
                var status = doc.RootElement.TryGetProperty("status", out var stElem) ? stElem.GetString() : "UNKNOWN";
                var execPrice = doc.RootElement.TryGetProperty("price", out var prElem) ? prElem.GetDecimal() : 0m;

                _logger.LogInformation("[MEGABULL ORDER PLACED] OrderId={OrderId} | Side={Side} | Qty={Qty} | Status={Status} | ExecPrice=₹{Price} | Token={Token}",
                    orderId, side, quantity, status, execPrice, token);

                return orderId;
            }
            else
            {
                _logger.LogError("[MEGABULL ORDER FAILED] Status={StatusCode} | Body={Body}", response.StatusCode, responseBody);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MEGABULL] Exception while placing {Side} order for {Symbol}", side, tradingSymbol);
            return null;
        }
    }

    public async Task<string?> ClosePositionAsync(
        string tradingSymbol,
        string optionType,
        decimal strikePrice,
        DateTime expiryDate,
        int quantity,
        CancellationToken ct = default)
    {
        // To close a BUY position, we place a SELL order
        return await PlaceOrderAsync(tradingSymbol, optionType, strikePrice, expiryDate, quantity, "SELL", ct);
    }

    private string? ResolveInstrumentToken(string tradingSymbol, string optionType, decimal strikePrice, DateTime expiryDate)
    {
        var opt = optionType.Trim().ToUpperInvariant();
        var strikeStr = strikePrice.ToString("F0");

        // 1. Direct symbol match
        if (!string.IsNullOrWhiteSpace(tradingSymbol))
        {
            if (_symbolToToken.TryGetValue(tradingSymbol, out var token)) return token;

            var cleanSymbol = tradingSymbol.Replace(" ", "").Replace("-", "").ToUpperInvariant();
            if (_symbolToToken.TryGetValue(cleanSymbol, out token)) return token;
        }

        // 2. Structured key match (e.g. "23NOV_25500_CE", "23Nov_25500_CE")
        var dateKey = $"{expiryDate:ddMMM}_{strikeStr}_{opt}";
        if (_keyToToken.TryGetValue(dateKey, out var t1)) return t1;

        var altDateKey = $"{expiryDate:yyyyMMdd}_{strikeStr}_{opt}";
        if (_keyToToken.TryGetValue(altDateKey, out var t2)) return t2;

        // 3. Exact InstrumentName format: "NIFTY 23 NOV 25500 CE"
        var formattedName = $"NIFTY {expiryDate:dd MMM} {strikeStr} {opt}".ToUpperInvariant();
        if (_symbolToToken.TryGetValue(formattedName, out var t3)) return t3;

        var formattedNameTrimmedDay = $"NIFTY {expiryDate:d MMM} {strikeStr} {opt}".ToUpperInvariant();
        if (_symbolToToken.TryGetValue(formattedNameTrimmedDay, out var t4)) return t4;

        // 4. Heuristic search in cached dictionary
        var monthStr = expiryDate.ToString("MMM").ToUpperInvariant();
        var dayStr = expiryDate.Day.ToString("00");

        var match = _symbolToToken.FirstOrDefault(kvp =>
            kvp.Key.StartsWith("NIFTY", StringComparison.OrdinalIgnoreCase) &&
            kvp.Key.EndsWith(opt, StringComparison.OrdinalIgnoreCase) &&
            kvp.Key.Contains(strikeStr) &&
            kvp.Key.Contains(monthStr) &&
            (kvp.Key.Contains(dayStr) || kvp.Key.Contains(expiryDate.Day.ToString())));

        if (!string.IsNullOrEmpty(match.Value))
        {
            _logger.LogInformation("[MEGABULL] Heuristically matched token {Token} for symbol key {Key}", match.Value, match.Key);
            return match.Value;
        }

        return null;
    }

    private async Task EnsureInstrumentsLoadedAsync(CancellationToken ct)
    {
        if (_isLoaded) return;

        await _loadLock.WaitAsync(ct);
        try
        {
            if (_isLoaded) return;

            string csvContent = string.Empty;
            var pathsToTry = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "megabull_instruments.csv"),
                Path.Combine(Directory.GetCurrentDirectory(), "megabull_instruments.csv"),
                Path.Combine(Directory.GetCurrentDirectory(), "src", "NiftyQuant.Api", "megabull_instruments.csv")
            };

            foreach (var path in pathsToTry)
            {
                if (File.Exists(path))
                {
                    _logger.LogInformation("[MEGABULL] Loading instruments from local CSV: {Path}", path);
                    csvContent = await File.ReadAllTextAsync(path, ct);
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(csvContent))
            {
                _logger.LogInformation("[MEGABULL] Local instruments CSV not found. Downloading from S3...");
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                csvContent = await client.GetStringAsync("https://megabull-open.s3.us-east-1.amazonaws.com/Instrument-default.csv", ct);
            }

            using var reader = new StringReader(csvContent);
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                if (parts.Length < 3) continue;

                var tradingSymbol = parts[0].Trim('"');
                var instrumentName = parts[1].Trim('"');
                var token = parts[2].Trim('"');

                _symbolToToken[tradingSymbol] = token;
                _symbolToToken[instrumentName] = token;
                _symbolToToken[tradingSymbol.Replace(" ", "")] = token;
                _symbolToToken[instrumentName.Replace(" ", "")] = token;

                // E.g. "NIFTY 23 NOV 25750 CE"
                var nameParts = instrumentName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (nameParts.Length >= 5 && nameParts[0].Equals("NIFTY", StringComparison.OrdinalIgnoreCase))
                {
                    // "23" "NOV" "25750" "CE"
                    var key = $"{nameParts[1]}{nameParts[2]}_{nameParts[3]}_{nameParts[4]}"; // "23NOV_25750_CE"
                    _keyToToken[key] = token;
                }
            }

            _isLoaded = true;
            _logger.LogInformation("[MEGABULL] Loaded {Count} instruments into lookup cache", _symbolToToken.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MEGABULL] Failed to load MegaBull instruments CSV");
        }
        finally
        {
            _loadLock.Release();
        }
    }
}
