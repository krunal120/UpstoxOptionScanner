using NiftyQuant.Application.Common.Models.Risk;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Application.Common.Interfaces;

public interface IRiskManagementService
{
    Task<PreTradeRiskValidationResult> ValidatePreTradeAsync(StrategySignal signal, CancellationToken ct = default);

    Task<DailyRiskStatusDto> GetDailyRiskStatusAsync(CancellationToken ct = default);

    Task<PaperAccount> GetOrCreateDefaultAccountAsync(CancellationToken ct = default);

    Task RecordTradeResultAsync(PaperTrade trade, CancellationToken ct = default);

    Task ResetDailyLimitsAsync(CancellationToken ct = default);

    PositionSizingResult CalculatePositionSize(decimal entryPremium, decimal stopLossPremium, decimal availableBalance);
}
