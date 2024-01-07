using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
  /// <inheritdoc />
  public partial class TransactionAndWallets : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AlterColumn<string>(
          name: "Username",
          table: "Users",
          type: "character varying(256)",
          maxLength: 256,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "text");

      migrationBuilder.AlterColumn<string>(
          name: "Id",
          table: "Users",
          type: "character varying(128)",
          maxLength: 128,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "text");

      migrationBuilder.AlterColumn<string>(
          name: "UserId",
          table: "Passengers",
          type: "character varying(128)",
          maxLength: 128,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "text");

      migrationBuilder.AlterColumn<string>(
          name: "PassportNumber",
          table: "Passengers",
          type: "character varying(64)",
          maxLength: 64,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "text");

      migrationBuilder.AlterColumn<string>(
          name: "FullName",
          table: "Passengers",
          type: "character varying(512)",
          maxLength: 512,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "text");

      migrationBuilder.AlterColumn<string>(
          name: "UserId",
          table: "Bookings",
          type: "character varying(128)",
          maxLength: 128,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "text");

      migrationBuilder.AlterColumn<string>(
          name: "Ticket",
          table: "Bookings",
          type: "character varying(128)",
          maxLength: 128,
          nullable: true,
          oldClrType: typeof(string),
          oldType: "text",
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "Passenger_PassportNumber",
          table: "Bookings",
          type: "character varying(64)",
          maxLength: 64,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "text");

      migrationBuilder.AlterColumn<string>(
          name: "Passenger_FullName",
          table: "Bookings",
          type: "character varying(512)",
          maxLength: 512,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "text");

      migrationBuilder.AddColumn<Guid>(
          name: "TransactionId",
          table: "Bookings",
          type: "uuid",
          nullable: false,
          defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

      migrationBuilder.CreateTable(
          name: "Wallets",
          columns: table => new
          {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            Usable = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
            WithdrawReserve = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
            BookingReserve = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
            UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Wallets", x => x.Id);
            table.ForeignKey(
                      name: "FK_Wallets_Users_UserId",
                      column: x => x.UserId,
                      principalTable: "Users",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "Transactions",
          columns: table => new
          {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            Description = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
            TransactionType = table.Column<int>(type: "integer", nullable: false),
            Amount = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
            From = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            To = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            WalletId = table.Column<Guid>(type: "uuid", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Transactions", x => x.Id);
            table.ForeignKey(
                      name: "FK_Transactions_Wallets_WalletId",
                      column: x => x.WalletId,
                      principalTable: "Wallets",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateIndex(
          name: "IX_Bookings_TransactionId",
          table: "Bookings",
          column: "TransactionId");

      migrationBuilder.CreateIndex(
          name: "IX_Transactions_WalletId",
          table: "Transactions",
          column: "WalletId");

      migrationBuilder.CreateIndex(
          name: "IX_Wallets_UserId",
          table: "Wallets",
          column: "UserId",
          unique: true);

      migrationBuilder.AddForeignKey(
          name: "FK_Bookings_Transactions_TransactionId",
          table: "Bookings",
          column: "TransactionId",
          principalTable: "Transactions",
          principalColumn: "Id",
          onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropForeignKey(
          name: "FK_Bookings_Transactions_TransactionId",
          table: "Bookings");

      migrationBuilder.DropTable(
          name: "Transactions");

      migrationBuilder.DropTable(
          name: "Wallets");

      migrationBuilder.DropIndex(
          name: "IX_Bookings_TransactionId",
          table: "Bookings");

      migrationBuilder.DropColumn(
          name: "TransactionId",
          table: "Bookings");

      migrationBuilder.AlterColumn<string>(
          name: "Username",
          table: "Users",
          type: "text",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "character varying(256)",
          oldMaxLength: 256);

      migrationBuilder.AlterColumn<string>(
          name: "Id",
          table: "Users",
          type: "text",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "character varying(128)",
          oldMaxLength: 128);

      migrationBuilder.AlterColumn<string>(
          name: "UserId",
          table: "Passengers",
          type: "text",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "character varying(128)",
          oldMaxLength: 128);

      migrationBuilder.AlterColumn<string>(
          name: "PassportNumber",
          table: "Passengers",
          type: "text",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "character varying(64)",
          oldMaxLength: 64);

      migrationBuilder.AlterColumn<string>(
          name: "FullName",
          table: "Passengers",
          type: "text",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "character varying(512)",
          oldMaxLength: 512);

      migrationBuilder.AlterColumn<string>(
          name: "UserId",
          table: "Bookings",
          type: "text",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "character varying(128)",
          oldMaxLength: 128);

      migrationBuilder.AlterColumn<string>(
          name: "Ticket",
          table: "Bookings",
          type: "text",
          nullable: true,
          oldClrType: typeof(string),
          oldType: "character varying(128)",
          oldMaxLength: 128,
          oldNullable: true);

      migrationBuilder.AlterColumn<string>(
          name: "Passenger_PassportNumber",
          table: "Bookings",
          type: "text",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "character varying(64)",
          oldMaxLength: 64);

      migrationBuilder.AlterColumn<string>(
          name: "Passenger_FullName",
          table: "Bookings",
          type: "text",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "character varying(512)",
          oldMaxLength: 512);
    }
  }
}
