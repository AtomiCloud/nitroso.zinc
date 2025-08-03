using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
  /// <inheritdoc />
  public partial class AddUserEmail : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DeleteData(
        table: "Costs",
        keyColumn: "Id",
        keyValue: new Guid("0050bc96-4945-4baa-92ca-e10ee0404199")
      );

      migrationBuilder.AddColumn<string>(
        name: "Email",
        table: "Users",
        type: "character varying(256)",
        maxLength: 256,
        nullable: true
      );

      migrationBuilder.InsertData(
        table: "Costs",
        columns: ["Id", "Cost", "CreatedAt"],
        values: 
        [
          new Guid("416ee926-5e5a-429b-9667-d2c1746f8cef"),
          14m,
          new DateTime(2025, 7, 27, 16, 5, 50, 254, DateTimeKind.Utc).AddTicks(4970),
        ]
      );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DeleteData(
        table: "Costs",
        keyColumn: "Id",
        keyValue: new Guid("416ee926-5e5a-429b-9667-d2c1746f8cef")
      );

      migrationBuilder.DropColumn(name: "Email", table: "Users");

      migrationBuilder.InsertData(
        table: "Costs",
        columns: ["Id", "Cost", "CreatedAt"],
        values: [
          new Guid("0050bc96-4945-4baa-92ca-e10ee0404199"),
          14m,
          new DateTime(2024, 3, 1, 5, 6, 58, 472, DateTimeKind.Utc).AddTicks(3050),
        ]
      );
    }
  }
}
