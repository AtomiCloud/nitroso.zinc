using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Costs",
                keyColumn: "Id",
                keyValue: new Guid("6b21363e-fff1-493b-8eb3-b73669df4fad"));

            migrationBuilder.CreateTable(
                name: "Fees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WithdrawFeePercentage = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fees", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Costs",
                columns: ["Id", "Cost", "CreatedAt"],
                values: [new Guid("849829b6-3815-439f-a0d3-1bd72b9a9eaa"), 14m, new DateTime(2026, 7, 9, 1, 38, 27, 898, DateTimeKind.Utc).AddTicks(4410)]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Fees");

            migrationBuilder.DeleteData(
                table: "Costs",
                keyColumn: "Id",
                keyValue: new Guid("849829b6-3815-439f-a0d3-1bd72b9a9eaa"));

            migrationBuilder.InsertData(
                table: "Costs",
                columns: ["Id", "Cost", "CreatedAt"],
                values: [new Guid("6b21363e-fff1-493b-8eb3-b73669df4fad"), 14m, new DateTime(2026, 7, 8, 20, 20, 56, 407, DateTimeKind.Utc).AddTicks(8010)]);
        }
    }
}
