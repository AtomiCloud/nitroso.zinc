using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoicePartners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Suffix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RoundingPreference = table.Column<byte>(type: "smallint", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePartners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MarketingSharePct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    Infrastructure = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
                    RecoveryPerBoost = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
                    RecoveryPerTicket = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePartners_Suffix_EffectiveAt",
                table: "InvoicePartners",
                columns: new[] { "Suffix", "EffectiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSettings_EffectiveAt",
                table: "InvoiceSettings",
                column: "EffectiveAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoicePartners");

            migrationBuilder.DropTable(
                name: "InvoiceSettings");
        }
    }
}
