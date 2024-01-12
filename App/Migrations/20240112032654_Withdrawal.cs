using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
  /// <inheritdoc />
  public partial class Withdrawal : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AlterColumn<short>(
          name: "TransactionType",
          table: "Transactions",
          type: "smallint",
          nullable: false,
          oldClrType: typeof(int),
          oldType: "integer");

      migrationBuilder.CreateTable(
          name: "Withdrawals",
          columns: table => new
          {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            Status = table.Column<byte>(type: "smallint", nullable: false),
            Amount = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
            PayNowNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            Note = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
            Receipt = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
            CompleterId = table.Column<string>(type: "character varying(128)", nullable: true),
            WalletId = table.Column<Guid>(type: "uuid", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Withdrawals", x => x.Id);
            table.ForeignKey(
                      name: "FK_Withdrawals_Users_CompleterId",
                      column: x => x.CompleterId,
                      principalTable: "Users",
                      principalColumn: "Id");
            table.ForeignKey(
                      name: "FK_Withdrawals_Wallets_WalletId",
                      column: x => x.WalletId,
                      principalTable: "Wallets",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateIndex(
          name: "IX_Withdrawals_CompleterId",
          table: "Withdrawals",
          column: "CompleterId");

      migrationBuilder.CreateIndex(
          name: "IX_Withdrawals_WalletId",
          table: "Withdrawals",
          column: "WalletId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "Withdrawals");

      migrationBuilder.AlterColumn<int>(
          name: "TransactionType",
          table: "Transactions",
          type: "integer",
          nullable: false,
          oldClrType: typeof(short),
          oldType: "smallint");
    }
  }
}
