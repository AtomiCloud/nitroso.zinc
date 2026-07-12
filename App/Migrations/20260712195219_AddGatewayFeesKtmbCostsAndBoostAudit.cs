using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
  /// <inheritdoc />
  public partial class AddGatewayFeesKtmbCostsAndBoostAudit : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<string>(
          name: "PriceBreakdown",
          table: "Bookings",
          type: "jsonb",
          nullable: true);

      migrationBuilder.AddColumn<DateTime>(
          name: "PrioritizedAt",
          table: "Bookings",
          type: "timestamp with time zone",
          nullable: true);

      migrationBuilder.AddColumn<string>(
          name: "PrioritizedBy",
          table: "Bookings",
          type: "character varying(128)",
          maxLength: 128,
          nullable: true);

      migrationBuilder.CreateTable(
          name: "GatewayFees",
          columns: table => new
          {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            SourceId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            SourceType = table.Column<byte>(type: "smallint", nullable: false),
            FinancialTransactionId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            Amount = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
            Fee = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
            Net = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
            Currency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            TransactedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_GatewayFees", x => x.Id);
          });

      migrationBuilder.CreateTable(
          name: "KtmbCosts",
          columns: table => new
          {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            Direction = table.Column<int>(type: "integer", nullable: false),
            Cost = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_KtmbCosts", x => x.Id);
          });

      migrationBuilder.CreateIndex(
          name: "IX_GatewayFees_FinancialTransactionId",
          table: "GatewayFees",
          column: "FinancialTransactionId",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_GatewayFees_SourceId",
          table: "GatewayFees",
          column: "SourceId");

      migrationBuilder.CreateIndex(
          name: "IX_GatewayFees_TransactedAt",
          table: "GatewayFees",
          column: "TransactedAt");

      migrationBuilder.CreateIndex(
          name: "IX_KtmbCosts_Direction_EffectiveAt",
          table: "KtmbCosts",
          columns: new[] { "Direction", "EffectiveAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "GatewayFees");

      migrationBuilder.DropTable(
          name: "KtmbCosts");

      migrationBuilder.DropColumn(
          name: "PriceBreakdown",
          table: "Bookings");

      migrationBuilder.DropColumn(
          name: "PrioritizedAt",
          table: "Bookings");

      migrationBuilder.DropColumn(
          name: "PrioritizedBy",
          table: "Bookings");
    }
  }
}
