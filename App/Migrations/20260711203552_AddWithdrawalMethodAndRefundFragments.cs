using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddWithdrawalMethodAndRefundFragments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "Method",
                table: "Withdrawals",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0,
                comment: "Payout rail: 0 = PayNow transfer, 1 = card refunds (Airwallex)");

            migrationBuilder.CreateTable(
                name: "WithdrawalRefunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
                    PaymentIntentId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AirwallexRefundId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SettledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WithdrawalId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithdrawalRefunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WithdrawalRefunds_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WithdrawalRefunds_Withdrawals_WithdrawalId",
                        column: x => x.WithdrawalId,
                        principalTable: "Withdrawals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRefunds_PaymentId",
                table: "WithdrawalRefunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRefunds_RequestId",
                table: "WithdrawalRefunds",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRefunds_WithdrawalId",
                table: "WithdrawalRefunds",
                column: "WithdrawalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WithdrawalRefunds");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "Withdrawals");
        }
    }
}
