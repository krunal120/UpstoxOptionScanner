using System.Text.Json;
using FluentAssertions;
using NiftyQuant.Application.Common.Models.Upstox;
using Xunit;

namespace NiftyQuant.UnitTests;

public class UpstoxDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    [Fact]
    public void MarketQuote_Deserialization_ShouldMapPropertiesCorrectly()
    {
        // Sample response recorded from live Upstox API
        var json = """
        {
            "status": "success",
            "data": {
                "NSE_INDEX:Nifty 50": {
                    "ohlc": {
                        "open": 25186.8,
                        "high": 25342.0,
                        "low": 25104.8,
                        "close": 25334.25
                    },
                    "timestamp": "2026-11-19T11:43:06.072+05:30",
                    "instrument_token": "NSE_INDEX|Nifty 50",
                    "last_price": 25334.25,
                    "net_change": 106.3
                }
            }
        }
        """;

        var response = JsonSerializer.Deserialize<UpstoxResponse<Dictionary<string, MarketQuoteDto>>>(json, JsonOptions);

        response.Should().NotBeNull();
        response!.Status.Should().Be("success");
        response.Data.Should().ContainKey("NSE_INDEX:Nifty 50");

        var quote = response.Data!["NSE_INDEX:Nifty 50"];
        quote.LastPrice.Should().Be(25334.25m);
        quote.NetChange.Should().Be(106.3m);
        quote.Ohlc.Should().NotBeNull();
        quote.Ohlc!.Open.Should().Be(25186.8m);
        quote.Ohlc.Close.Should().Be(25334.25m);
    }

    [Fact]
    public void OptionChain_Deserialization_ShouldMapGreeksAndMarketData()
    {
        var json = """
        {
            "status": "success",
            "data": [
                {
                    "expiry": "2026-11-23",
                    "pcr": 1.25,
                    "strike_price": 25300.0,
                    "underlying_key": "NSE_INDEX|Nifty 50",
                    "underlying_spot_price": 25340.1,
                    "call_options": {
                        "instrument_key": "NSE_FO|64116",
                        "market_data": {
                            "ltp": 142.5,
                            "volume": 52000,
                            "oi": 150000.0,
                            "bid_price": 142.0,
                            "ask_price": 143.0
                        },
                        "option_greeks": {
                            "delta": 0.52,
                            "gamma": 0.0012,
                            "theta": -15.4,
                            "vega": 8.5,
                            "iv": 14.2
                        }
                    },
                    "put_options": {
                        "instrument_key": "NSE_FO|64117",
                        "market_data": {
                            "ltp": 98.0,
                            "volume": 48000,
                            "oi": 180000.0,
                            "bid_price": 97.5,
                            "ask_price": 98.5
                        },
                        "option_greeks": {
                            "delta": -0.48,
                            "gamma": 0.0012,
                            "theta": -14.1,
                            "vega": 8.3,
                            "iv": 15.1
                        }
                    }
                }
            ]
        }
        """;

        var response = JsonSerializer.Deserialize<UpstoxResponse<List<OptionChainItemDto>>>(json, JsonOptions);

        response.Should().NotBeNull();
        response!.Data.Should().HaveCount(1);

        var item = response.Data![0];
        item.StrikePrice.Should().Be(25300.0m);
        item.UnderlyingSpotPrice.Should().Be(25340.1m);
        item.CallOptions.Should().NotBeNull();
        item.CallOptions!.MarketData!.Ltp.Should().Be(142.5m);
        item.CallOptions.OptionGreeks!.Delta.Should().Be(0.52m);
        item.PutOptions!.OptionGreeks!.Delta.Should().Be(-0.48m);
    }
}
