using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations.LoggerMigrations;

/// <inheritdoc />
public partial class FixLogsTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(name: "PK_Logs", schema: "logs", table: "Logs");

        migrationBuilder.RenameTable(
            name: "Logs",
            schema: "logs",
            newName: "Errors",
            newSchema: "logs"
        );

        migrationBuilder.AlterColumn<string>(
            name: "StackTrace",
            schema: "logs",
            table: "Errors",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(8000)",
            oldMaxLength: 8000,
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "Message",
            schema: "logs",
            table: "Errors",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(4000)",
            oldMaxLength: 4000
        );

        migrationBuilder.AddPrimaryKey(
            name: "PK_Errors",
            schema: "logs",
            table: "Errors",
            column: "Id"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(name: "PK_Errors", schema: "logs", table: "Errors");

        migrationBuilder.RenameTable(
            name: "Errors",
            schema: "logs",
            newName: "Logs",
            newSchema: "logs"
        );

        migrationBuilder.AlterColumn<string>(
            name: "StackTrace",
            schema: "logs",
            table: "Logs",
            type: "character varying(8000)",
            maxLength: 8000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "Message",
            schema: "logs",
            table: "Logs",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AddPrimaryKey(
            name: "PK_Logs",
            schema: "logs",
            table: "Logs",
            column: "Id"
        );
    }
}
