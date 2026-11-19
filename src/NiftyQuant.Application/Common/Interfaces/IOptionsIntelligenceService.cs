using NiftyQuant.Application.Common.Models.Options;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;

namespace NiftyQuant.Application.Common.Interfaces;

public interface IOptionsIntelligenceService
{
    OptionsIntelligenceReportDto AnalyzeSnapshot(
        OptionChainSnapshot snapshot,
        OptionChainSnapshot? previousSnapshot = null);

    OiBuildupType ClassifyBuildup(decimal priceChange, decimal oiChange);

    PcrAnalysisDto AnalyzePcr(
        decimal totalPutOi,
        decimal totalCallOi,
        decimal totalPutVol,
        decimal totalCallVol,
        decimal? previousPcr = null);

    SupportResistanceLevelsDto IdentifySupportResistance(
        decimal spotPrice,
        decimal maxPain,
        IReadOnlyList<StrikeAnalysisDto> strikes);

    SelectedOptionContractDto? SelectBestContract(
        IReadOnlyList<OptionQuote> quotes,
        OptionType optionType,
        decimal spotPrice,
        decimal minLiquidityScore = 40m);

    int CalculateSentimentScore(
        PcrAnalysisDto pcr,
        SupportResistanceLevelsDto levels,
        IReadOnlyList<StrikeAnalysisDto> strikes,
        decimal spotPrice);
}
