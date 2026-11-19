namespace NiftyQuant.Application.Common.Options;

public class UpstoxOptions
{
    public const string SectionName = "Upstox";

    public string BaseUrl { get; set; } = "https://api.upstox.com";
    public string WebSocketUrl { get; set; } = "wss://api.upstox.com/v3/feed/market-data-feed";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
}

public class RiskOptions
{
    public const string SectionName = "TradingRisk";

    public decimal InitialCapital { get; set; } = 50000.0m;
    public decimal RiskPerTradePercent { get; set; } = 0.5m; // 0.5% = Rs. 250
    public decimal MaxDailyLossPercent { get; set; } = 1.5m; // 1.5% = Rs. 750
    public decimal MinRewardRiskRatio { get; set; } = 2.0m; // 1:2
    public int MaxConcurrentPositions { get; set; } = 1;
    public int MaxDailyTrades { get; set; } = 6;
    public int MaxConsecutiveLosses { get; set; } = 2;
    public bool EnableTrailingStopLoss { get; set; } = true;
    public decimal SlippagePerOrderPercent { get; set; } = 0.2m; // 0.2% estimated slippage
    public decimal BrokeragePerOrder { get; set; } = 20.0m; // Rs 20 per order
}
