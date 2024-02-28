using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
  /// <inheritdoc />
  public partial class AddBookingNoAndTicketNo : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DeleteData(
          table: "Costs",
          keyColumn: "Id",
          keyValue: new Guid("4da18c91-cc5f-4b69-a85f-3249b5b14731"));

      migrationBuilder.AddColumn<string>(
          name: "BookingNo",
          table: "Bookings",
          type: "character varying(128)",
          maxLength: 128,
          nullable: true);

      migrationBuilder.AddColumn<string>(
          name: "TicketNo",
          table: "Bookings",
          type: "character varying(128)",
          maxLength: 128,
          nullable: true);

      migrationBuilder.InsertData(
          table: "Costs",
          columns: new[] { "Id", "Cost", "CreatedAt" },
          values: new object[] { new Guid("2038d689-2741-44a6-ba79-66376940e0de"), 14m, new DateTime(2024, 2, 28, 16, 27, 44, 182, DateTimeKind.Utc).AddTicks(4320) });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DeleteData(
          table: "Costs",
          keyColumn: "Id",
          keyValue: new Guid("2038d689-2741-44a6-ba79-66376940e0de"));

      migrationBuilder.DropColumn(
          name: "BookingNo",
          table: "Bookings");

      migrationBuilder.DropColumn(
          name: "TicketNo",
          table: "Bookings");

      migrationBuilder.InsertData(
          table: "Costs",
          columns: new[] { "Id", "Cost", "CreatedAt" },
          values: new object[] { new Guid("4da18c91-cc5f-4b69-a85f-3249b5b14731"), 14m, new DateTime(2024, 1, 21, 8, 57, 27, 424, DateTimeKind.Utc).AddTicks(4600) });
    }
  }
}
