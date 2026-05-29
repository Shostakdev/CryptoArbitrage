using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoArbitrage.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "spread_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BuyExchange = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SellExchange = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AvgBuyPrice = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    AvgSellPrice = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    TradeVolumeUsdt = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    GrossSpreadPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    BuyFeeUsdt = table.Column<decimal>(type: "numeric", nullable: false),
                    SellFeeUsdt = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalFeesUsdt = table.Column<decimal>(type: "numeric", nullable: false),
                    CoinsTraded = table.Column<decimal>(type: "numeric", nullable: false),
                    GrossProfit = table.Column<decimal>(type: "numeric", nullable: false),
                    NetProfit = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    NetProfitPercent = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    SettlementMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spread_history", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "spread_history");
        }
    }
}
