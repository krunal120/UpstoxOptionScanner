using NiftyQuant.Application.Common.Models.PaperTrading;

namespace NiftyQuant.Application.Common.Interfaces;

public interface IAutonomousTradingService
{
    bool IsEnabled { get; set; }
    string ExecutionMode { get; set; }
    decimal MinConfidenceThreshold { get; set; }
    bool IsMarketHoursOnly { get; set; }

    AutonomousTradingStatusDto GetStatus();

    Task SetEnabledAsync(bool enabled, CancellationToken ct = default);

    Task<OrderExecutionResult> ForceTriggerSignalAndExecuteAsync(ForceSignalExecutionRequest request, CancellationToken ct = default);

    Task<int> AutoSquareOffAllAsync(string reason = "AutonomousSquareOff", CancellationToken ct = default);
}
