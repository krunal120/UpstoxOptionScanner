using Microsoft.EntityFrameworkCore;
using NiftyQuant.Domain.Entities;

namespace NiftyQuant.Infrastructure.Persistence;

public class NiftyQuantDbContext : DbContext
{
    public NiftyQuantDbContext(DbContextOptions<NiftyQuantDbContext> options) : base(options)
    {
    }

    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<MarketCandle> MarketCandles => Set<MarketCandle>();
    public DbSet<OptionContract> OptionContracts => Set<OptionContract>();
    public DbSet<OptionQuote> OptionQuotes => Set<OptionQuote>();
    public DbSet<OptionChainSnapshot> OptionChainSnapshots => Set<OptionChainSnapshot>();
    public DbSet<StrategySignal> StrategySignals => Set<StrategySignal>();
    public DbSet<SignalReason> SignalReasons => Set<SignalReason>();
    public DbSet<PaperAccount> PaperAccounts => Set<PaperAccount>();
    public DbSet<PaperOrder> PaperOrders => Set<PaperOrder>();
    public DbSet<PaperPosition> PaperPositions => Set<PaperPosition>();
    public DbSet<PaperTrade> PaperTrades => Set<PaperTrade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Instrument Configuration
        modelBuilder.Entity<Instrument>(entity =>
        {
            entity.HasKey(e => e.InstrumentKey);
            entity.Property(e => e.InstrumentKey).HasMaxLength(100);
            entity.Property(e => e.TradingSymbol).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Exchange).HasMaxLength(20);
            entity.Property(e => e.Segment).HasMaxLength(20);
            entity.Property(e => e.TickSize).HasPrecision(18, 4);
            entity.Property(e => e.StrikePrice).HasPrecision(18, 2);
        });

        // MarketCandle Configuration
        modelBuilder.Entity<MarketCandle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.InstrumentKey, e.Timeframe, e.CandleTimeUtc }).IsUnique();
            entity.Property(e => e.InstrumentKey).HasMaxLength(100);
            entity.Property(e => e.Open).HasPrecision(18, 4);
            entity.Property(e => e.High).HasPrecision(18, 4);
            entity.Property(e => e.Low).HasPrecision(18, 4);
            entity.Property(e => e.Close).HasPrecision(18, 4);
            entity.Property(e => e.OpenInterest).HasPrecision(18, 2);
        });

        // OptionContract Configuration
        modelBuilder.Entity<OptionContract>(entity =>
        {
            entity.HasKey(e => e.InstrumentKey);
            entity.Property(e => e.InstrumentKey).HasMaxLength(100);
            entity.Property(e => e.UnderlyingKey).HasMaxLength(100);
            entity.Property(e => e.StrikePrice).HasPrecision(18, 2);
            entity.HasIndex(e => new { e.UnderlyingKey, e.ExpiryDate, e.StrikePrice, e.OptionType }).IsUnique();
        });

        // OptionQuote Configuration
        modelBuilder.Entity<OptionQuote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InstrumentKey).HasMaxLength(100);
            entity.Property(e => e.StrikePrice).HasPrecision(18, 2);
            entity.Property(e => e.Ltp).HasPrecision(18, 4);
            entity.Property(e => e.BidPrice).HasPrecision(18, 4);
            entity.Property(e => e.AskPrice).HasPrecision(18, 4);
            entity.Property(e => e.OpenInterest).HasPrecision(18, 2);
            entity.Property(e => e.PreviousOpenInterest).HasPrecision(18, 2);
            entity.Property(e => e.ImpliedVolatility).HasPrecision(18, 4);
            entity.Property(e => e.Delta).HasPrecision(18, 4);
            entity.Property(e => e.Gamma).HasPrecision(18, 6);
            entity.Property(e => e.Theta).HasPrecision(18, 4);
            entity.Property(e => e.Vega).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.InstrumentKey, e.SnapshotTimeUtc });
        });

        // OptionChainSnapshot Configuration
        modelBuilder.Entity<OptionChainSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnderlyingKey).HasMaxLength(100);
            entity.Property(e => e.UnderlyingPrice).HasPrecision(18, 4);
            entity.Property(e => e.TotalCallOi).HasPrecision(18, 2);
            entity.Property(e => e.TotalPutOi).HasPrecision(18, 2);
            entity.Property(e => e.Pcr).HasPrecision(18, 4);
            entity.Property(e => e.MaxPainStrike).HasPrecision(18, 2);
            entity.Property(e => e.HighestCallOiStrike).HasPrecision(18, 2);
            entity.Property(e => e.HighestPutOiStrike).HasPrecision(18, 2);
            entity.HasIndex(e => new { e.UnderlyingKey, e.SnapshotTimeUtc });
        });

        // StrategySignal Configuration
        modelBuilder.Entity<StrategySignal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnderlyingKey).HasMaxLength(100);
            entity.Property(e => e.UnderlyingPrice).HasPrecision(18, 4);
            entity.Property(e => e.BullishScore).HasPrecision(18, 2);
            entity.Property(e => e.BearishScore).HasPrecision(18, 2);
            entity.Property(e => e.Confidence).HasPrecision(18, 2);
            entity.Property(e => e.SelectedInstrumentKey).HasMaxLength(100);
            entity.Property(e => e.SelectedStrike).HasPrecision(18, 2);
            entity.Property(e => e.EntryPremium).HasPrecision(18, 4);
            entity.Property(e => e.StopLossPremium).HasPrecision(18, 4);
            entity.Property(e => e.Target1Premium).HasPrecision(18, 4);
            entity.Property(e => e.Target2Premium).HasPrecision(18, 4);
            entity.Property(e => e.RiskAmount).HasPrecision(18, 2);
            entity.Property(e => e.RewardToRiskRatio).HasPrecision(18, 4);
            entity.HasIndex(e => e.SignalTimeUtc);

            entity.HasMany(e => e.Reasons)
                  .WithOne(r => r.Signal)
                  .HasForeignKey(r => r.SignalId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // SignalReason Configuration
        modelBuilder.Entity<SignalReason>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.ScoreContribution).HasPrecision(18, 4);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // PaperAccount Configuration
        modelBuilder.Entity<PaperAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InitialCapital).HasPrecision(18, 2);
            entity.Property(e => e.AvailableBalance).HasPrecision(18, 2);
            entity.Property(e => e.UtilizedMargin).HasPrecision(18, 2);
            entity.Property(e => e.RealizedPnL).HasPrecision(18, 2);
            entity.Property(e => e.UnrealizedPnL).HasPrecision(18, 2);
            entity.Property(e => e.DailyLossLimit).HasPrecision(18, 2);
            entity.Property(e => e.MaxRiskPerTrade).HasPrecision(18, 2);
        });

        // PaperOrder Configuration
        modelBuilder.Entity<PaperOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InstrumentKey).HasMaxLength(100);
            entity.Property(e => e.OrderedPrice).HasPrecision(18, 4);
            entity.Property(e => e.ExecutedPrice).HasPrecision(18, 4);
            entity.Property(e => e.Slippage).HasPrecision(18, 4);
            entity.Property(e => e.EstimatedCharges).HasPrecision(18, 2);
        });

        // PaperPosition Configuration
        modelBuilder.Entity<PaperPosition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InstrumentKey).HasMaxLength(100);
            entity.Property(e => e.StrikePrice).HasPrecision(18, 2);
            entity.Property(e => e.AverageBuyPrice).HasPrecision(18, 4);
            entity.Property(e => e.CurrentLtp).HasPrecision(18, 4);
            entity.Property(e => e.StopLossPrice).HasPrecision(18, 4);
            entity.Property(e => e.TargetPrice).HasPrecision(18, 4);
            entity.Property(e => e.UnrealizedPnL).HasPrecision(18, 2);
        });

        // PaperTrade Configuration
        modelBuilder.Entity<PaperTrade>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InstrumentKey).HasMaxLength(100);
            entity.Property(e => e.EntryPrice).HasPrecision(18, 4);
            entity.Property(e => e.ExitPrice).HasPrecision(18, 4);
            entity.Property(e => e.SlippagePaid).HasPrecision(18, 4);
            entity.Property(e => e.TotalCharges).HasPrecision(18, 2);
            entity.Property(e => e.GrossPnL).HasPrecision(18, 2);
            entity.Property(e => e.NetPnL).HasPrecision(18, 2);
        });
    }
}
