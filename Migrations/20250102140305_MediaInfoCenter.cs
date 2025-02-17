using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class MediaInfoCenter : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "PositionInfo_IsHorizontalCenter",
            table: "Alerts",
            type: "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<bool>(
            name: "PositionInfo_IsVerticallCenter",
            table: "Alerts",
            type: "boolean",
            nullable: false,
            defaultValue: false
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PositionInfo_IsHorizontalCenter", table: "Alerts");

        migrationBuilder.DropColumn(name: "PositionInfo_IsVerticallCenter", table: "Alerts");
    }
}
