using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryWindowAndKtmbChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstRecoveringAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true,
                comment: "When the booking first entered Recovering (first duplicate-passport rejection); start of the Recovery:MaxWindowMinutes retry window");

            migrationBuilder.CreateTable(
                name: "BookingKtmbChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    KtmbBookingNo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    KtmbTicketNo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClaimedByBookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Error = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingKtmbChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingKtmbChecks_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingKtmbChecks_BookingId_RequestedAt",
                table: "BookingKtmbChecks",
                columns: new[] { "BookingId", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingKtmbChecks");

            migrationBuilder.DropColumn(
                name: "FirstRecoveringAt",
                table: "Bookings");
        }
    }
}
