using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
  /// <inheritdoc />
  public partial class CostUtc : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
        name: "Costs",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false),
          CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
          Cost = table.Column<decimal>(
            type: "numeric(16,8)",
            precision: 16,
            scale: 8,
            nullable: false
          ),
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_Costs", x => x.Id);
        }
      );

      migrationBuilder.CreateTable(
        name: "Discounts",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false),
          Amount = table.Column<decimal>(
            type: "numeric(16,8)",
            precision: 16,
            scale: 8,
            nullable: false
          ),
          DiscountType = table.Column<string>(type: "text", nullable: false),
          Name = table.Column<string>(type: "text", nullable: false),
          Description = table.Column<string>(type: "text", nullable: false),
          Disabled = table.Column<bool>(type: "boolean", nullable: false),
          Target = table.Column<string>(type: "jsonb", nullable: false),
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_Discounts", x => x.Id);
        }
      );

      migrationBuilder.InsertData(
        table: "Costs",
        columns: ["Id", "Cost", "CreatedAt"],
        values: 
        [
          new Guid("4da18c91-cc5f-4b69-a85f-3249b5b14731"),
          14m,
          new DateTime(2024, 1, 21, 8, 57, 27, 424, DateTimeKind.Utc).AddTicks(4600),
        ]
      );

      migrationBuilder.CreateIndex(
        name: "IX_Costs_CreatedAt",
        table: "Costs",
        column: "CreatedAt",
        unique: true
      );

      migrationBuilder.CreateIndex(name: "IX_Discounts_Name", table: "Discounts", column: "Name");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(name: "Costs");

      migrationBuilder.DropTable(name: "Discounts");
    }
  }
}
