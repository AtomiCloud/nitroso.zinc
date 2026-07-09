using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeFeeQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Costs",
                keyColumn: "Id",
                keyValue: new Guid("07beac74-bfcc-4bbd-b47c-4feb41d59bef"));

            migrationBuilder.RenameColumn(
                name: "WithdrawFeePercentage",
                table: "Fees",
                newName: "Percentage");

            migrationBuilder.AddColumn<decimal>(
                name: "FlatAmount",
                table: "Fees",
                type: "numeric(16,8)",
                precision: 16,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<byte>(
                name: "Type",
                table: "Fees",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.InsertData(
                table: "Costs",
                columns: new[] { "Id", "Cost", "CreatedAt" },
                values: new object[] { new Guid("cdb9da3c-e2b0-4e95-bc42-ce5fa1a2b883"), 14m, new DateTime(2026, 7, 9, 6, 30, 57, 934, DateTimeKind.Utc).AddTicks(2790) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Costs",
                keyColumn: "Id",
                keyValue: new Guid("cdb9da3c-e2b0-4e95-bc42-ce5fa1a2b883"));

            migrationBuilder.DropColumn(
                name: "FlatAmount",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Fees");

            migrationBuilder.RenameColumn(
                name: "Percentage",
                table: "Fees",
                newName: "WithdrawFeePercentage");

            migrationBuilder.InsertData(
                table: "Costs",
                columns: new[] { "Id", "Cost", "CreatedAt" },
                values: new object[] { new Guid("07beac74-bfcc-4bbd-b47c-4feb41d59bef"), 14m, new DateTime(2026, 7, 9, 1, 44, 40, 392, DateTimeKind.Utc).AddTicks(4820) });
        }
    }
}
