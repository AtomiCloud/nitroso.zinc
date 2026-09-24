using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    Seq = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    TicketBasis = table.Column<byte>(type: "smallint", nullable: false),
                    EngineVersion = table.Column<int>(type: "integer", nullable: false),
                    InputsJson = table.Column<string>(type: "text", nullable: false),
                    ComputedJson = table.Column<string>(type: "text", nullable: false),
                    NetProfit = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
                    PoolTotal = table.Column<decimal>(type: "numeric(16,8)", precision: 16, scale: 8, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IssuedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocuments_CreatedAt",
                table: "InvoiceDocuments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocuments_PeriodMonth",
                table: "InvoiceDocuments",
                column: "PeriodMonth",
                unique: true,
                filter: "\"Status\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceDocuments");
        }
    }
}
