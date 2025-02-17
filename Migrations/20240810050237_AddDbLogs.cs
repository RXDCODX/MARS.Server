using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class AddDbLogs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Logs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WhenLogged = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                Message = table.Column<string>(type: "text", nullable: false),
                StackTrace = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_Logs", a => a.Id)
        );

        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION check_logs_count()
            RETURNS TRIGGER AS $$
            BEGIN
                IF (SELECT COUNT(*) FROM Logs) >= 5000 THEN
                    DELETE FROM Logs
                    WHERE Id IN (
                        SELECT Id FROM Logs
                        ORDER BY WhenLogged DESC
                        LIMIT 4000
                    );
                END IF;
                RETURN NEW;
            END;
            $$
             LANGUAGE plpgsql;
            """
        );

        migrationBuilder.Sql(
            """
            CREATE TRIGGER logs_after_insert
            AFTER INSERT ON public."Logs"
            FOR EACH ROW
            EXECUTE FUNCTION check_logs_count();
            """
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER logs_after_insert ON public."Logs";
            DROP FUNCTION check_logs_count();
            """
        );

        migrationBuilder.DropTable(name: "Logs");
    }
}
