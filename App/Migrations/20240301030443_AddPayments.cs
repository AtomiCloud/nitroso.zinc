using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
  /// <inheritdoc />
  public partial class AddPayments : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DeleteData(
          table: "Costs",
          keyColumn: "Id",
          keyValue: new Guid("2038d689-2741-44a6-ba79-66376940e0de"));

      migrationBuilder.AddColumn<Guid>(
          name: "PaymentId",
          table: "Transactions",
          type: "uuid",
          nullable: true);

      migrationBuilder.CreateTable(
          name: "Payments",
          columns: table => new
          {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            ExternalReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            Gateway = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            Amount = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
            CapturedAmount = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
            Currency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            Statuses = table.Column<Dictionary<string, DateTime>>(type: "jsonb", nullable: false),
            AdditionalData = table.Column<JsonDocument>(type: "jsonb", nullable: false),
            WalletId = table.Column<Guid>(type: "uuid", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Payments", x => x.Id);
            table.ForeignKey(
                      name: "FK_Payments_Wallets_WalletId",
                      column: x => x.WalletId,
                      principalTable: "Wallets",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.InsertData(
          table: "Costs",
          columns: new[] { "Id", "Cost", "CreatedAt" },
          values: new object[] { new Guid("7faf2b28-a953-4fe1-8d3f-408299e04c33"), 14m, new DateTime(2024, 3, 1, 3, 4, 43, 598, DateTimeKind.Utc).AddTicks(3460) });

      migrationBuilder.CreateIndex(
          name: "IX_Transactions_PaymentId",
          table: "Transactions",
          column: "PaymentId",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_Payments_ExternalReference",
          table: "Payments",
          column: "ExternalReference");

      migrationBuilder.CreateIndex(
          name: "IX_Payments_WalletId",
          table: "Payments",
          column: "WalletId");

      migrationBuilder.AddForeignKey(
          name: "FK_Transactions_Payments_PaymentId",
          table: "Transactions",
          column: "PaymentId",
          principalTable: "Payments",
          principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropForeignKey(
          name: "FK_Transactions_Payments_PaymentId",
          table: "Transactions");

      migrationBuilder.DropTable(
          name: "Payments");

      migrationBuilder.DropIndex(
          name: "IX_Transactions_PaymentId",
          table: "Transactions");

      migrationBuilder.DeleteData(
          table: "Costs",
          keyColumn: "Id",
          keyValue: new Guid("7faf2b28-a953-4fe1-8d3f-408299e04c33"));

      migrationBuilder.DropColumn(
          name: "PaymentId",
          table: "Transactions");

      migrationBuilder.InsertData(
          table: "Costs",
          columns: new[] { "Id", "Cost", "CreatedAt" },
          values: new object[] { new Guid("2038d689-2741-44a6-ba79-66376940e0de"), 14m, new DateTime(2024, 2, 28, 16, 27, 44, 182, DateTimeKind.Utc).AddTicks(4320) });
    }
  }
}
