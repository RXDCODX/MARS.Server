using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations.LoggerMigrations;

/// <inheritdoc />
public partial class Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "logs");

        migrationBuilder.CreateTable(
            name: "Logs",
            schema: "logs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WhenLogged = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                Message = table.Column<string>(type: "text", nullable: false),
                StackTrace = table.Column<string>(type: "text", nullable: true),
                LogLevel = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Logs", x => x.Id);
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Logs", schema: "logs");
    }
}
