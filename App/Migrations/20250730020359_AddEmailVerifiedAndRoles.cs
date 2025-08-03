using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerifiedAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Costs",
                keyColumn: "Id",
                keyValue: new Guid("416ee926-5e5a-429b-9667-d2c1746f8cef"));

            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "Users",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "Roles",
                table: "Users",
                type: "text[]",
                nullable: true);

            migrationBuilder.InsertData(
                table: "Costs",
                columns: ["Id", "Cost", "CreatedAt"],
                values: [new Guid("28c43132-a71a-464f-bb9c-31717c509a3a"), 14m, new DateTime(2025, 7, 30, 2, 3, 58, 932, DateTimeKind.Utc).AddTicks(1630)
                ]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Costs",
                keyColumn: "Id",
                keyValue: new Guid("28c43132-a71a-464f-bb9c-31717c509a3a"));

            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Roles",
                table: "Users");

            migrationBuilder.InsertData(
                table: "Costs",
                columns: ["Id", "Cost", "CreatedAt"],
                values: [new Guid("416ee926-5e5a-429b-9667-d2c1746f8cef"), 14m, new DateTime(2025, 7, 27, 16, 5, 50, 254, DateTimeKind.Utc).AddTicks(4970)
                ]);
        }
    }
}
