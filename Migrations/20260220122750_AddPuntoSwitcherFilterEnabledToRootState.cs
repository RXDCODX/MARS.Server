using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddPuntoSwitcherFilterEnabledToRootState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "PuntoSwitcherFilterEnabled",
            table: "ApplicationState",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.UpdateData(
            table: "ApplicationState",
            keyColumn: "Id",
            keyValue: 1,
            column: "PuntoSwitcherFilterEnabled",
            value: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PuntoSwitcherFilterEnabled",
            table: "ApplicationState");
    }
}
