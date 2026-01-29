using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddWaifuRollCooldownMinutesToRootState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "WaifuRollCooldownMinutes",
            table: "ApplicationState",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.UpdateData(
            table: "ApplicationState",
            keyColumn: "Id",
            keyValue: 1,
            column: "WaifuRollCooldownMinutes",
            value: 20L);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "WaifuRollCooldownMinutes",
            table: "ApplicationState");
    }
}
