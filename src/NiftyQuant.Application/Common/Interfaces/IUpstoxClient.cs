using NiftyQuant.Application.Common.Models.Upstox;

namespace NiftyQuant.Application.Common.Interfaces;

public interface IUpstoxClient
{
    Task<MarketQuoteDto?> GetMarketQuoteAsync(string instrumentKey, CancellationToken ct = default);
    Task<Dictionary<string, MarketQuoteDto>> GetMarketQuotesAsync(IEnumerable<string> instrumentKeys, CancellationToken ct = default);
    Task<IReadOnlyList<OptionContractDto>> GetOptionContractsAsync(string underlyingKey = "NSE_INDEX|Nifty 50", CancellationToken ct = default);
    Task<IReadOnlyList<OptionChainItemDto>> GetOptionChainAsync(string underlyingKey, string expiryDate, CancellationToken ct = default);
    Task<IReadOnlyList<CandleDto>> GetHistoricalCandlesAsync(string instrumentKey, string unit, int interval, string toDate, string fromDate, CancellationToken ct = default);
    Task<IReadOnlyList<CandleDto>> GetIntradayCandlesAsync(string instrumentKey, string unit, int interval, CancellationToken ct = default);
    Task<IReadOnlyList<InstrumentMasterDto>> DownloadInstrumentMasterAsync(string segment = "NSE_FO", CancellationToken ct = default);
}
