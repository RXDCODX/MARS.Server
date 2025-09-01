using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations.LoggerMigrations;

/// <inheritdoc />
public partial class AddLogLevelField : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LogLevel",
            schema: "logs",
            table: "Errors",
            type: "text",
            nullable: false,
            defaultValue: ""
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LogLevel", schema: "logs", table: "Errors");
    }
}
