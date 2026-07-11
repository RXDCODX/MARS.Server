using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class SOmethingMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "WhenAdded",
            table: "Fumos",
            type: "text",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "timestamp with time zone");

        migrationBuilder.AlterColumn<string>(
            name: "LastOrder",
            table: "Fumos",
            type: "text",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "timestamp with time zone");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<DateTime>(
            name: "WhenAdded",
            table: "Fumos",
            type: "timestamp with time zone",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.AlterColumn<DateTime>(
            name: "LastOrder",
            table: "Fumos",
            type: "timestamp with time zone",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text");
    }
}
