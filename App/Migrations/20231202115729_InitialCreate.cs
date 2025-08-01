using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace App.Migrations
{
  /// <inheritdoc />
  public partial class InitialCreate : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
        name: "Schedules",
        columns: table => new
        {
          Date = table.Column<DateOnly>(type: "date", nullable: false),
          Confirmed = table.Column<bool>(type: "boolean", nullable: false),
          JToWExcluded = table.Column<TimeOnly[]>(
            type: "time without time zone[]",
            nullable: false
          ),
          WToJExcluded = table.Column<TimeOnly[]>(
            type: "time without time zone[]",
            nullable: false
          ),
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_Schedules", x => x.Date);
        }
      );

      migrationBuilder.CreateTable(
        name: "Timings",
        columns: table => new
        {
          Direction = table
            .Column<int>(type: "integer", nullable: false)
            .Annotation(
              "Npgsql:ValueGenerationStrategy",
              NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
            ),
          Timings = table.Column<TimeOnly[]>(type: "time without time zone[]", nullable: false),
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_Timings", x => x.Direction);
        }
      );

      migrationBuilder.CreateTable(
        name: "Users",
        columns: table => new
        {
          Id = table.Column<string>(type: "text", nullable: false),
          Username = table.Column<string>(type: "text", nullable: false),
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_Users", x => x.Id);
        }
      );

      migrationBuilder.CreateTable(
        name: "Bookings",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false),
          CreatedAt = table.Column<DateTime>(
            type: "timestamp with time zone",
            nullable: false,
            defaultValueSql: "NOW()"
          ),
          Status = table.Column<byte>(type: "smallint", nullable: false, defaultValue: (byte)0),
          CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
          Date = table.Column<DateOnly>(type: "date", nullable: false),
          Time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
          UserId = table.Column<string>(type: "text", nullable: false),
          Passengers = table.Column<string>(type: "jsonb", nullable: true),
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_Bookings", x => x.Id);
          table.ForeignKey(
            name: "FK_Bookings_Users_UserId",
            column: x => x.UserId,
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade
          );
        }
      );

      migrationBuilder.CreateTable(
        name: "Passengers",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false),
          FullName = table.Column<string>(type: "text", nullable: false),
          Gender = table.Column<byte>(type: "smallint", nullable: false),
          PassportExpiry = table.Column<DateOnly>(type: "date", nullable: false),
          PassportNumber = table.Column<string>(type: "text", nullable: false),
          UserId = table.Column<string>(type: "text", nullable: false),
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_Passengers", x => x.Id);
          table.ForeignKey(
            name: "FK_Passengers_Users_UserId",
            column: x => x.UserId,
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade
          );
        }
      );

      migrationBuilder.InsertData(
        table: "Timings",
        columns: ["Direction", "Timings"],
        values: new object[,]
        {
          {
            1,
            new[]
            {
              new TimeOnly(5, 0, 0),
              new TimeOnly(5, 30, 0),
              new TimeOnly(6, 0, 0),
              new TimeOnly(6, 30, 0),
              new TimeOnly(7, 0, 0),
              new TimeOnly(7, 30, 0),
              new TimeOnly(8, 45, 0),
              new TimeOnly(10, 0, 0),
              new TimeOnly(11, 30, 0),
              new TimeOnly(12, 45, 0),
              new TimeOnly(14, 0, 0),
              new TimeOnly(15, 15, 0),
              new TimeOnly(16, 30, 0),
              new TimeOnly(17, 45, 0),
              new TimeOnly(19, 0, 0),
              new TimeOnly(20, 15, 0),
              new TimeOnly(21, 30, 0),
              new TimeOnly(22, 45, 0),
            },
          },
          {
            2,
            new[]
            {
              new TimeOnly(8, 30, 0),
              new TimeOnly(9, 45, 0),
              new TimeOnly(11, 0, 0),
              new TimeOnly(12, 30, 0),
              new TimeOnly(13, 45, 0),
              new TimeOnly(15, 0, 0),
              new TimeOnly(16, 15, 0),
              new TimeOnly(17, 30, 0),
              new TimeOnly(18, 45, 0),
              new TimeOnly(20, 0, 0),
              new TimeOnly(21, 15, 0),
              new TimeOnly(22, 30, 0),
              new TimeOnly(23, 45, 0),
            },
          },
        }
      );

      migrationBuilder.CreateIndex(name: "IX_Bookings_UserId", table: "Bookings", column: "UserId");

      migrationBuilder.CreateIndex(
        name: "IX_Passengers_UserId_PassportNumber",
        table: "Passengers",
        columns: ["UserId", "PassportNumber"],
        unique: true
      );

      migrationBuilder.CreateIndex(
        name: "IX_Users_Username",
        table: "Users",
        column: "Username",
        unique: true
      );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(name: "Bookings");

      migrationBuilder.DropTable(name: "Passengers");

      migrationBuilder.DropTable(name: "Schedules");

      migrationBuilder.DropTable(name: "Timings");

      migrationBuilder.DropTable(name: "Users");
    }
  }
}
