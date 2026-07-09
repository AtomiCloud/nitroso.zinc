using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeEffectiveAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Costs",
                keyColumn: "Id",
                keyValue: new Guid("849829b6-3815-439f-a0d3-1bd72b9a9eaa"));

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveAt",
                table: "Fees",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.InsertData(
                table: "Costs",
                columns: ["Id", "Cost", "CreatedAt"],
                values: [new Guid("07beac74-bfcc-4bbd-b47c-4feb41d59bef"), 14m, new DateTime(2026, 7, 9, 1, 44, 40, 392, DateTimeKind.Utc).AddTicks(4820)]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Costs",
                keyColumn: "Id",
                keyValue: new Guid("07beac74-bfcc-4bbd-b47c-4feb41d59bef"));

            migrationBuilder.DropColumn(
                name: "EffectiveAt",
                table: "Fees");

            migrationBuilder.InsertData(
                table: "Costs",
                columns: ["Id", "Cost", "CreatedAt"],
                values: [new Guid("849829b6-3815-439f-a0d3-1bd72b9a9eaa"), 14m, new DateTime(2026, 7, 9, 1, 38, 27, 898, DateTimeKind.Utc).AddTicks(4410)]);
        }
    }
}
