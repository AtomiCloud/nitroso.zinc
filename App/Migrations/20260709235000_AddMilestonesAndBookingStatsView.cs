using System;
using App.Modules.Bookings.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddMilestonesAndBookingStatsView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Milestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Milestones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Milestones_Date",
                table: "Milestones",
                column: "Date");

            // booking_stats materialized view + the plain-column unique index
            // REFRESH CONCURRENTLY requires. The SQL (including every bucket
            // CASE) is generated from the same ladder constants the C# helpers
            // use, so the two sides cannot drift. CREATE ... WITH DATA
            // populates the view here, so the first read after deploy can go
            // straight to REFRESH CONCURRENTLY.
            migrationBuilder.Sql(BookingStatsView.CreateSql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(BookingStatsView.DropSql());

            migrationBuilder.DropTable(
                name: "Milestones");
        }
    }
}
