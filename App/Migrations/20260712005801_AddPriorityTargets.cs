using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddPriorityTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessTarget",
                table: "PrioritySettings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreeTarget",
                table: "PrioritySettings",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessTarget",
                table: "PrioritySettings");

            migrationBuilder.DropColumn(
                name: "FreeTarget",
                table: "PrioritySettings");
        }
    }
}
