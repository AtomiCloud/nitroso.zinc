using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddWithdrawalPayoutAndBookingLastBuyingAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Costs",
                keyColumn: "Id",
                keyValue: new Guid("28c43132-a71a-464f-bb9c-31717c509a3a"));

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationNumber",
                table: "Withdrawals",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Fee",
                table: "Withdrawals",
                type: "numeric(16,8)",
                precision: 16,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayoutAttempt",
                table: "Withdrawals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastBuyingAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.InsertData(
                table: "Costs",
                columns: ["Id", "Cost", "CreatedAt"],
                values: [new Guid("5aa07550-fc00-48bd-bb0c-35eeaf8a1362"), 14m, new DateTime(2026, 7, 8, 6, 8, 2, 518, DateTimeKind.Utc).AddTicks(7650)]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Costs",
                keyColumn: "Id",
                keyValue: new Guid("5aa07550-fc00-48bd-bb0c-35eeaf8a1362"));

            migrationBuilder.DropColumn(
                name: "ConfirmationNumber",
                table: "Withdrawals");

            migrationBuilder.DropColumn(
                name: "Fee",
                table: "Withdrawals");

            migrationBuilder.DropColumn(
                name: "PayoutAttempt",
                table: "Withdrawals");

            migrationBuilder.DropColumn(
                name: "LastBuyingAt",
                table: "Bookings");

            migrationBuilder.InsertData(
                table: "Costs",
                columns: ["Id", "Cost", "CreatedAt"],
                values: [new Guid("28c43132-a71a-464f-bb9c-31717c509a3a"), 14m, new DateTime(2025, 7, 30, 2, 3, 58, 932, DateTimeKind.Utc).AddTicks(1630)]);
        }
    }
}
