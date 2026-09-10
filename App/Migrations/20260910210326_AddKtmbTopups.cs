using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddKtmbTopups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KtmbTopups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuingTransactionId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AmountMyr = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
                    AmountSgd = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
                    MerchantName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Source = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KtmbTopups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KtmbTopups_IssuingTransactionId",
                table: "KtmbTopups",
                column: "IssuingTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KtmbTopups_PostedAt",
                table: "KtmbTopups",
                column: "PostedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KtmbTopups");
        }
    }
}
