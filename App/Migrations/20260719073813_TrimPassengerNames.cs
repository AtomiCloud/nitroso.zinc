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
            // spaces around the stored value. Each UPDATE is guarded by
            // IS DISTINCT FROM so only rows that actually change are rewritten.

            // Passengers.FullName is not part of any unique key: trim freely.
            migrationBuilder.Sql(
                "UPDATE \"Passengers\" SET \"FullName\" = "
                    + "TRIM(BOTH E' \\t\\n\\r\\u00A0' FROM \"FullName\") "
                    + "WHERE \"FullName\" IS DISTINCT FROM TRIM(BOTH E' \\t\\n\\r\\u00A0' FROM \"FullName\");"
            );

            // Passengers has a UNIQUE index on (UserId, PassportNumber). Trimming
            // could collapse two distinct values onto the same key, so skip any
            // row whose trimmed passport would collide with another passport for
            // the same user. Those rare rows are left as-is for manual resolution
            // rather than aborting the whole migration on a unique violation.
            migrationBuilder.Sql(
                "UPDATE \"Passengers\" p SET \"PassportNumber\" = "
                    + "TRIM(BOTH E' \\t\\n\\r\\u00A0' FROM p.\"PassportNumber\") "
                    + "WHERE p.\"PassportNumber\" IS DISTINCT FROM "
                    + "TRIM(BOTH E' \\t\\n\\r\\u00A0' FROM p.\"PassportNumber\") "
                    + "AND NOT EXISTS (SELECT 1 FROM \"Passengers\" o "
                    + "WHERE o.\"UserId\" = p.\"UserId\" AND o.\"Id\" <> p.\"Id\" "
                    + "AND o.\"PassportNumber\" = TRIM(BOTH E' \\t\\n\\r\\u00A0' FROM p.\"PassportNumber\"));"
            );

            // Bookings store a snapshot of the passenger at booking time (no
            // uniqueness), so the same cleanup is applied to those columns.
            migrationBuilder.Sql(
                "UPDATE \"Bookings\" SET \"Passenger_FullName\" = "
                    + "TRIM(BOTH E' \\t\\n\\r\\u00A0' FROM \"Passenger_FullName\") "
                    + "WHERE \"Passenger_FullName\" IS DISTINCT FROM "
                    + "TRIM(BOTH E' \\t\\n\\r\\u00A0' FROM \"Passenger_FullName\");"
            );
            migrationBuilder.Sql(
                "UPDATE \"Bookings\" SET \"Passenger_PassportNumber\" = "
                    + "TRIM(BOTH E' \\t\\n\\r\\u00A0' FROM \"Passenger_PassportNumber\") "
                    + "WHERE \"Passenger_PassportNumber\" IS DISTINCT FROM "
                    + "TRIM(BOTH E' \\t\\n\\r\\u00A0' FROM \"Passenger_PassportNumber\");"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible: the original untrimmed whitespace is not recoverable.
        }
    }
}
