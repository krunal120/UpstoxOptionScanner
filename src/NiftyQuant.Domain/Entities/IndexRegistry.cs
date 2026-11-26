namespace NiftyQuant.Domain.Entities;

public class IndexProfile
{
    public string UnderlyingKey { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Exchange { get; set; } = "NSE";
    public string DerivativesSegment { get; set; } = "NSE_FO";
    public int LotSize { get; set; } = 65;
    public decimal StrikeInterval { get; set; } = 50m;
    
    // Strategy thresholds scaled for this specific index
    public decimal VwapPullbackPoints { get; set; } = 35m;
    public decimal VwapOverExtendedPoints { get; set; } = 55m;
    public decimal Ema21TolerancePoints { get; set; } = 20m;
    public decimal MinSlUnderlyingPoints { get; set; } = 20m;
    public decimal SlBaseDefaultPoints { get; set; } = 25m;
    public DayOfWeek ExpiryDayOfWeek { get; set; } = DayOfWeek.Thursday;
    public bool IsEnabled { get; set; } = true;
}

public static class IndexRegistry
{
    public const string Nifty50Key = "NSE_INDEX|Nifty 50";
    public const string BankNiftyKey = "NSE_INDEX|Nifty Bank";
    public const string SensexKey = "BSE_INDEX|SENSEX";
    public const string IndiaVixKey = "NSE_INDEX|India VIX";

    public static readonly IndexProfile Nifty50 = new()
    {
        UnderlyingKey = Nifty50Key,
        Symbol = "NIFTY",
        DisplayName = "NIFTY 50",
        Exchange = "NSE",
        DerivativesSegment = "NSE_FO",
        LotSize = 65,
        StrikeInterval = 50m,
        VwapPullbackPoints = 35m,
        VwapOverExtendedPoints = 55m,
        Ema21TolerancePoints = 20m,
        MinSlUnderlyingPoints = 20m,
        SlBaseDefaultPoints = 25m,
        ExpiryDayOfWeek = DayOfWeek.Thursday,
        IsEnabled = true
    };

    public static readonly IndexProfile BankNifty = new()
    {
        UnderlyingKey = BankNiftyKey,
        Symbol = "BANKNIFTY",
        DisplayName = "Bank Nifty",
        Exchange = "NSE",
        DerivativesSegment = "NSE_FO",
        LotSize = 30, // Revised Bank Nifty lot size
        StrikeInterval = 100m,
        VwapPullbackPoints = 85m,
        VwapOverExtendedPoints = 140m,
        Ema21TolerancePoints = 50m,
        MinSlUnderlyingPoints = 50m,
        SlBaseDefaultPoints = 60m,
        ExpiryDayOfWeek = DayOfWeek.Wednesday,
        IsEnabled = true
    };

    public static readonly IndexProfile Sensex = new()
    {
        UnderlyingKey = SensexKey,
        Symbol = "SENSEX",
        DisplayName = "BSE SENSEX",
        Exchange = "BSE",
        DerivativesSegment = "BSE_FO",
        LotSize = 20, // BSE Sensex lot size
        StrikeInterval = 100m,
        VwapPullbackPoints = 140m,
        VwapOverExtendedPoints = 220m,
        Ema21TolerancePoints = 80m,
        MinSlUnderlyingPoints = 80m,
        SlBaseDefaultPoints = 100m,
        ExpiryDayOfWeek = DayOfWeek.Friday,
        IsEnabled = true
    };

    private static readonly Dictionary<string, IndexProfile> ProfilesByKey = new(StringComparer.OrdinalIgnoreCase)
    {
        [Nifty50Key] = Nifty50,
        ["NSE_INDEX:Nifty 50"] = Nifty50,
        ["NIFTY"] = Nifty50,
        ["NIFTY 50"] = Nifty50,

        [BankNiftyKey] = BankNifty,
        ["NSE_INDEX:Nifty Bank"] = BankNifty,
        ["NSE_INDEX|NIFTY BANK"] = BankNifty,
        ["NSE_INDEX:NIFTY BANK"] = BankNifty,
        ["BANKNIFTY"] = BankNifty,
        ["NIFTY BANK"] = BankNifty,

        [SensexKey] = Sensex,
        ["BSE_INDEX:SENSEX"] = Sensex,
        ["BSE_INDEX|Sensex"] = Sensex,
        ["BSE_INDEX:Sensex"] = Sensex,
        ["SENSEX"] = Sensex
    };

    public static IReadOnlyList<IndexProfile> SupportedIndices => new[] { Nifty50, BankNifty, Sensex };

    public static IndexProfile GetProfile(string? underlyingKey)
    {
        if (string.IsNullOrWhiteSpace(underlyingKey))
            return Nifty50;

        var normalized = underlyingKey.Trim().Replace(':', '|');
        if (ProfilesByKey.TryGetValue(normalized, out var profile))
            return profile;

        if (normalized.Contains("bank", StringComparison.OrdinalIgnoreCase))
            return BankNifty;

        if (normalized.Contains("sensex", StringComparison.OrdinalIgnoreCase))
            return Sensex;

        return Nifty50;
    }

    public static bool IsSupportedIndex(string? instrumentKey)
    {
        if (string.IsNullOrWhiteSpace(instrumentKey))
            return false;

        var normalized = instrumentKey.Trim().Replace(':', '|');
        return ProfilesByKey.ContainsKey(normalized) ||
               normalized.Contains("nifty 50", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("bank", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("sensex", StringComparison.OrdinalIgnoreCase);
    }
}
