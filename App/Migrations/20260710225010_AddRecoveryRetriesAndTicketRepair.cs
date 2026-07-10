using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryRetriesAndTicketRepair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecoveryRetries",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Times this booking was recycled from Recovering back to Pending (RecoverRevert); capped by Recovery:MaxRetries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecoveryRetries",
                table: "Bookings");
        }
    }
}
