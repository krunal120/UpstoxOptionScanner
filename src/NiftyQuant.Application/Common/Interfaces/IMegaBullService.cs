namespace NiftyQuant.Application.Common.Interfaces;

public interface IMegaBullService
{
    bool IsEnabled { get; }
    Task<string?> PlaceOrderAsync(string tradingSymbol, string optionType, decimal strikePrice, DateTime expiryDate, int quantity, string side = "BUY", CancellationToken ct = default);
    Task<string?> ClosePositionAsync(string tradingSymbol, string optionType, decimal strikePrice, DateTime expiryDate, int quantity, CancellationToken ct = default);
}
