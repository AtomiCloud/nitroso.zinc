using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
  /// <inheritdoc />
  public partial class ModifyPayments : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DeleteData(
          table: "Costs",
          keyColumn: "Id",
          keyValue: new Guid("7faf2b28-a953-4fe1-8d3f-408299e04c33"));

      migrationBuilder.InsertData(
          table: "Costs",
          columns: new[] { "Id", "Cost", "CreatedAt" },
          values: new object[] { new Guid("0050bc96-4945-4baa-92ca-e10ee0404199"), 14m, new DateTime(2024, 3, 1, 5, 6, 58, 472, DateTimeKind.Utc).AddTicks(3050) });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DeleteData(
          table: "Costs",
          keyColumn: "Id",
          keyValue: new Guid("0050bc96-4945-4baa-92ca-e10ee0404199"));

      migrationBuilder.InsertData(
          table: "Costs",
          columns: new[] { "Id", "Cost", "CreatedAt" },
          values: new object[] { new Guid("7faf2b28-a953-4fe1-8d3f-408299e04c33"), 14m, new DateTime(2024, 3, 1, 3, 4, 43, 598, DateTimeKind.Utc).AddTicks(3460) });
    }
  }
}
