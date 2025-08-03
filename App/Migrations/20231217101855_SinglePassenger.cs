using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
  /// <inheritdoc />
  public partial class SinglePassenger : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(name: "Passengers", table: "Bookings");

      migrationBuilder.AddColumn<string>(
        name: "Passenger_FullName",
        table: "Bookings",
        type: "text",
        nullable: false,
        defaultValue: ""
      );

      migrationBuilder.AddColumn<byte>(
        name: "Passenger_Gender",
        table: "Bookings",
        type: "smallint",
        nullable: false,
        defaultValue: (byte)0
      );

      migrationBuilder.AddColumn<DateOnly>(
        name: "Passenger_PassportExpiry",
        table: "Bookings",
        type: "date",
        nullable: false,
        defaultValue: new DateOnly(1, 1, 1)
      );

      migrationBuilder.AddColumn<string>(
        name: "Passenger_PassportNumber",
        table: "Bookings",
        type: "text",
        nullable: false,
        defaultValue: ""
      );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(name: "Passenger_FullName", table: "Bookings");

      migrationBuilder.DropColumn(name: "Passenger_Gender", table: "Bookings");

      migrationBuilder.DropColumn(name: "Passenger_PassportExpiry", table: "Bookings");

      migrationBuilder.DropColumn(name: "Passenger_PassportNumber", table: "Bookings");

      migrationBuilder.AddColumn<string>(
        name: "Passengers",
        table: "Bookings",
        type: "jsonb",
        nullable: true
      );
    }
  }
}
