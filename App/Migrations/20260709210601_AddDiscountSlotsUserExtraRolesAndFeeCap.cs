using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountSlotsUserExtraRolesAndFeeCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "ExtraRoles",
                table: "Users",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<decimal>(
                name: "Cap",
                table: "Fees",
                type: "numeric(16,8)",
                precision: 16,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveAt",
                table: "Discounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Discounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeadTimeUnderHours",
                table: "Discounts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MatchDate",
                table: "Discounts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "MatchDayOfWeek",
                table: "Discounts",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchDirection",
                table: "Discounts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "MatchTime",
                table: "Discounts",
                type: "time without time zone",
                nullable: true);

            // Live-behavior parity seed: booking termination previously refunded
            // Domain:RefundPercentage (50%) of the amount; the fee queue must
            // open with an effective { Termination, 50%, 0 flat, no cap } row or
            // the zero-zero default would make termination FREE on deploy day.
            // FIXED id + FIXED past timestamps — a dynamic Guid.NewGuid() /
            // DateTime.UtcNow here would make the seed non-deterministic across
            // deploys (see the Costs seed comment in MainDbContext).
            migrationBuilder.InsertData(
                table: "Fees",
                columns: new[] { "Id", "CreatedAt", "EffectiveAt", "Type", "Percentage", "FlatAmount", "Cap" },
                values: new object?[]
                {
                    new Guid("8b6a1f5e-4c2d-4e8a-9f3b-7d5c1a2e6b90"),
                    new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    (byte)2, // FeeType.Termination
                    50m,
                    0m,
                    null,
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Fees",
                keyColumn: "Id",
                keyValue: new Guid("8b6a1f5e-4c2d-4e8a-9f3b-7d5c1a2e6b90"));

            migrationBuilder.DropColumn(
                name: "ExtraRoles",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Cap",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "EffectiveAt",
                table: "Discounts");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Discounts");

            migrationBuilder.DropColumn(
                name: "LeadTimeUnderHours",
                table: "Discounts");

            migrationBuilder.DropColumn(
                name: "MatchDate",
                table: "Discounts");

            migrationBuilder.DropColumn(
                name: "MatchDayOfWeek",
                table: "Discounts");

            migrationBuilder.DropColumn(
                name: "MatchDirection",
                table: "Discounts");

            migrationBuilder.DropColumn(
                name: "MatchTime",
                table: "Discounts");
        }
    }
}
