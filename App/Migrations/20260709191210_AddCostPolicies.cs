using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddCostPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Priority",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PriorityFee",
                table: "Bookings",
                type: "numeric(16,8)",
                precision: 16,
                scale: 8,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CostPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    MatchDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MatchTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    MatchDayOfWeek = table.Column<byte>(type: "smallint", nullable: true),
                    MatchDirection = table.Column<int>(type: "integer", nullable: true),
                    LeadTimeUnderHours = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
                    IsPercentage = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriorityAccesses",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriorityAccesses", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "PrioritySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
                    AllowAll = table.Column<bool>(type: "boolean", nullable: false),
                    WindowStartSgt = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    WindowEndSgt = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrioritySettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostPolicies");

            migrationBuilder.DropTable(
                name: "PriorityAccesses");

            migrationBuilder.DropTable(
                name: "PrioritySettings");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PriorityFee",
                table: "Bookings");
        }
    }
}
