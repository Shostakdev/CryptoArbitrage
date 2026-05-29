using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoArbitrage.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperTrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "TotalFeesUsdt",
                table: "spread_history",
                type: "numeric(24,8)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "SellFeeUsdt",
                table: "spread_history",
                type: "numeric(24,8)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossSpreadPercent",
                table: "spread_history",
                type: "numeric(24,8)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossProfit",
                table: "spread_history",
                type: "numeric(24,8)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "CoinsTraded",
                table: "spread_history",
                type: "numeric(24,8)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "BuyFeeUsdt",
                table: "spread_history",
                type: "numeric(24,8)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.CreateTable(
                name: "trade_executions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArbitrageEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InvestedUsdt = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    NetProfitUsdt = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trade_executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trade_executions_spread_history_ArbitrageEventId",
                        column: x => x.ArbitrageEventId,
                        principalTable: "spread_history",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "virtual_wallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Asset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(24,8)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_virtual_wallets", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "virtual_wallets",
                columns: new[] { "Id", "Asset", "Balance", "UpdatedAtUtc" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "USDT", 1000m, new DateTime(2026, 5, 23, 23, 36, 57, 532, DateTimeKind.Utc).AddTicks(8281) });

            migrationBuilder.CreateIndex(
                name: "IX_trade_executions_ArbitrageEventId",
                table: "trade_executions",
                column: "ArbitrageEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trade_executions");

            migrationBuilder.DropTable(
                name: "virtual_wallets");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalFeesUsdt",
                table: "spread_history",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)");

            migrationBuilder.AlterColumn<decimal>(
                name: "SellFeeUsdt",
                table: "spread_history",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)");

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossSpreadPercent",
                table: "spread_history",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)");

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossProfit",
                table: "spread_history",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)");

            migrationBuilder.AlterColumn<decimal>(
                name: "CoinsTraded",
                table: "spread_history",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)");

            migrationBuilder.AlterColumn<decimal>(
                name: "BuyFeeUsdt",
                table: "spread_history",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)");
        }
    }
}
