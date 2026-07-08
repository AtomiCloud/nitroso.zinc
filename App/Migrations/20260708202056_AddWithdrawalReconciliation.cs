using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddWithdrawalReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Costs",
                keyColumn: "Id",
                keyValue: new Guid("5aa07550-fc00-48bd-bb0c-35eeaf8a1362"));

            migrationBuilder.AddColumn<int>(
                name: "ReconcileAttempts",
                table: "Withdrawals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.InsertData(
                table: "Costs",
                columns: ["Id", "Cost", "CreatedAt"],
                values: [new Guid("6b21363e-fff1-493b-8eb3-b73669df4fad"), 14m, new DateTime(2026, 7, 8, 20, 20, 56, 407, DateTimeKind.Utc).AddTicks(8010)]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Costs",
                keyColumn: "Id",
                keyValue: new Guid("6b21363e-fff1-493b-8eb3-b73669df4fad"));

            migrationBuilder.DropColumn(
                name: "ReconcileAttempts",
                table: "Withdrawals");

            migrationBuilder.InsertData(
                table: "Costs",
                columns: ["Id", "Cost", "CreatedAt"],
                values: [new Guid("5aa07550-fc00-48bd-bb0c-35eeaf8a1362"), 14m, new DateTime(2026, 7, 8, 6, 8, 2, 518, DateTimeKind.Utc).AddTicks(7650)]);
        }
    }
}
