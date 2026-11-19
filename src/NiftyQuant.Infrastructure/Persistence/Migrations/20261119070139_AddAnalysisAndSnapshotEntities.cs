using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NiftyQuant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisAndSnapshotEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "IntradayOiChange",
                table: "OptionQuotes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IntradayOiChangePercent",
                table: "OptionQuotes",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "SnapshotId",
                table: "OptionQuotes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IndicatorSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstrumentKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Timeframe = table.Column<int>(type: "int", nullable: false),
                    CandleTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Ema9 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Ema21 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Ema50 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Ema200 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Vwap = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Supertrend = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SupertrendDirectionIsBullish = table.Column<bool>(type: "bit", nullable: true),
                    Rsi14 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Macd = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MacdSignal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MacdHistogram = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Adx14 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PlusDi = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MinusDi = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Atr14 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BollingerUpper = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BollingerMiddle = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BollingerLower = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    RelativeVolume = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndicatorSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketRegimes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegimeType = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AdxValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PriceAboveVwap = table.Column<bool>(type: "bit", nullable: false),
                    EmaBullishCross = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketRegimes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OptionQuotes_SnapshotId",
                table: "OptionQuotes",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_IndicatorSnapshots_InstrumentKey_Timeframe_CandleTimeUtc",
                table: "IndicatorSnapshots",
                columns: new[] { "InstrumentKey", "Timeframe", "CandleTimeUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketRegimes_TimestampUtc",
                table: "MarketRegimes",
                column: "TimestampUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_OptionQuotes_OptionChainSnapshots_SnapshotId",
                table: "OptionQuotes",
                column: "SnapshotId",
                principalTable: "OptionChainSnapshots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OptionQuotes_OptionChainSnapshots_SnapshotId",
                table: "OptionQuotes");

            migrationBuilder.DropTable(
                name: "IndicatorSnapshots");

            migrationBuilder.DropTable(
                name: "MarketRegimes");

            migrationBuilder.DropIndex(
                name: "IX_OptionQuotes_SnapshotId",
                table: "OptionQuotes");

            migrationBuilder.DropColumn(
                name: "IntradayOiChange",
                table: "OptionQuotes");

            migrationBuilder.DropColumn(
                name: "IntradayOiChangePercent",
                table: "OptionQuotes");

            migrationBuilder.DropColumn(
                name: "SnapshotId",
                table: "OptionQuotes");
        }
    }
}
