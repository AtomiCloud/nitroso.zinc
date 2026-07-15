using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddKtmbRefundCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "KtmbRefundAmount",
                table: "Bookings",
                type: "numeric(16,8)",
                precision: 16,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KtmbRefundCurrency",
                table: "Bookings",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KtmbRefundAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "KtmbRefundCurrency",
                table: "Bookings");
        }
    }
}
