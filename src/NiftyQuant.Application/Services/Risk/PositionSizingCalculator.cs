using NiftyQuant.Application.Common.Models.Risk;

namespace NiftyQuant.Application.Services.Risk;

public class PositionSizingCalculator
{
    public const int DefaultLotSize = 65; // NIFTY 50 Lot Size

    public PositionSizingResult CalculatePositionSize(
        decimal entryPremium,
        decimal stopLossPremium,
        decimal availableBalance,
        decimal maxRiskAllowed,
        int lotSize = DefaultLotSize,
        int maxLots = 1)
    {
        var result = new PositionSizingResult();

        if (entryPremium <= 0m || stopLossPremium <= 0m)
        {
            result.RejectionReason = "Invalid premium values: entry and stop-loss premiums must be greater than zero.";
            return result;
        }

        if (stopLossPremium >= entryPremium)
        {
            result.RejectionReason = "Stop-loss premium must be strictly less than entry premium for option buying.";
            return result;
        }

        decimal riskPerShare = Math.Round(entryPremium - stopLossPremium, 2);
        decimal costPerLot = Math.Round(entryPremium * lotSize, 2);
        decimal riskPerLot = Math.Round(riskPerShare * lotSize, 2);

        result.RiskPerShare = riskPerShare;

        if (availableBalance < costPerLot)
        {
            result.IsAffordable = false;
            result.RejectionReason = $"Insufficient balance: Required ₹{costPerLot:N2} for 1 lot, but only ₹{availableBalance:N2} is available.";
            return result;
        }

        // For V1 capital (₹50,000), minimum unit is 1 lot
        int lots = 1;
        if (maxLots > 1)
        {
            // If multiple lots are allowed, scale by maxRiskAllowed / riskPerLot
            int affordableLots = (int)(availableBalance / costPerLot);
            int riskBasedLots = (int)(maxRiskAllowed / riskPerLot);
            lots = Math.Clamp(Math.Min(affordableLots, riskBasedLots), 1, maxLots);
        }

        result.AllocatedLots = lots;
        result.AllocatedQuantity = lots * lotSize;
        result.TotalCost = Math.Round(entryPremium * result.AllocatedQuantity, 2);
        result.TotalRiskAmount = Math.Round(riskPerShare * result.AllocatedQuantity, 2);
        result.IsAffordable = true;

        return result;
    }

    public (bool IsValid, decimal Ratio) ValidateRewardToRisk(
        decimal entryPremium,
        decimal stopLossPremium,
        decimal targetPremium,
        decimal minRatio = 2.0m)
    {
        decimal risk = entryPremium - stopLossPremium;
        decimal reward = targetPremium - entryPremium;

        if (risk <= 0m || reward <= 0m)
        {
            return (false, 0m);
        }

        decimal ratio = Math.Round(reward / risk, 2);
        return (ratio >= minRatio, ratio);
    }

    public decimal CalculateEstimatedRoundTripCharges(
        decimal entryPremium,
        decimal exitPremium,
        int quantity,
        decimal brokeragePerOrder = 20.0m)
    {
        decimal buyTurnover = entryPremium * quantity;
        decimal sellTurnover = exitPremium * quantity;
        decimal totalTurnover = buyTurnover + sellTurnover;

        // 1. Brokerage: ₹20 per executed order (buy + sell = ₹40)
        decimal brokerage = brokeragePerOrder * 2m;

        // 2. STT: 0.1% on sell turnover for options
        decimal stt = Math.Round(sellTurnover * 0.001m, 2);

        // 3. Exchange transaction charges: ~0.05% of total turnover
        decimal exchangeCharges = Math.Round(totalTurnover * 0.0005m, 2);

        // 4. GST: 18% on (brokerage + exchange charges)
        decimal gst = Math.Round((brokerage + exchangeCharges) * 0.18m, 2);

        // 5. Stamp duty: 0.003% on buy turnover
        decimal stampDuty = Math.Round(buyTurnover * 0.00003m, 2);

        // 6. SEBI turnover charges: ₹10 per crore (0.0001%)
        decimal sebiCharges = Math.Round(totalTurnover * 0.000001m, 2);

        return Math.Round(brokerage + stt + exchangeCharges + gst + stampDuty + sebiCharges, 2);
    }
}
