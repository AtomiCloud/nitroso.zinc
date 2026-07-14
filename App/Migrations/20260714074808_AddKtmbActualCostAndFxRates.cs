using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddKtmbActualCostAndFxRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "KtmbAmount",
                table: "Bookings",
                type: "numeric(16,8)",
                precision: 16,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KtmbCurrency",
                table: "Bookings",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KtmbFxRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KtmbFxRates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KtmbFxRates_EffectiveAt",
                table: "KtmbFxRates",
                column: "EffectiveAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KtmbFxRates");

            migrationBuilder.DropColumn(
                name: "KtmbAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "KtmbCurrency",
                table: "Bookings");
        }
    }
}
