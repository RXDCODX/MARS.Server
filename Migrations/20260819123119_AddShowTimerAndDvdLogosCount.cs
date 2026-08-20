using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddShowTimerAndDvdLogosCount : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DvdLogosCount",
            table: "AdhdLayoutConfig",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "ShowTimer",
            table: "AdhdLayoutConfig",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DvdLogosCount",
            table: "AdhdLayoutConfig");

        migrationBuilder.DropColumn(
            name: "ShowTimer",
            table: "AdhdLayoutConfig");
    }
}
