using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class AddPriorityPoliciesAndSlotCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Policies",
                table: "PrioritySettings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlotCap",
                table: "PrioritySettings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Policies",
                table: "PrioritySettings");

            migrationBuilder.DropColumn(
                name: "SlotCap",
                table: "PrioritySettings");
        }
    }
}
