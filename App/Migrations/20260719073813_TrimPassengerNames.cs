using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Migrations
{
    /// <inheritdoc />
    public partial class TrimPassengerNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One-off cleanup of records written before trim-on-write existed.
            // Strips leading/trailing whitespace (space, tab, newline, carriage
            // return, non-breaking space) from stored passenger names and
            // passport numbers, mirroring the C# string.Trim() now applied on
            // write. Fixes customers unable to cancel/confirm because of hidden
            // spaces around the stored value.
            migrationBuilder.Sql(
                "UPDATE \"Passengers\" "
                    + "SET \"FullName\" = TRIM(BOTH E' \\t\\n\\r\\u00A0' FROM \"FullName\"), "
                    + "\"PassportNumber\" = TRIM(BOTH E' \\t\\n\\r\\u00A0' FROM \"PassportNumber\");"
            );

            // Bookings store a snapshot of the passenger at booking time, so the
            // same cleanup is applied to those columns.
            migrationBuilder.Sql(
                "UPDATE \"Bookings\" "
                    + "SET \"Passenger_FullName\" = TRIM(BOTH E' \\t\\n\\r\\u00A0' FROM \"Passenger_FullName\"), "
                    + "\"Passenger_PassportNumber\" = TRIM(BOTH E' \\t\\n\\r\\u00A0' FROM \"Passenger_PassportNumber\");"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible: the original untrimmed whitespace is not recoverable.
        }
    }
}
