using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class MediaInfoVolume : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "MetaInfo_Volume",
            table: "Alerts",
            type: "integer",
            nullable: false,
            defaultValue: 100
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "MetaInfo_Volume", table: "Alerts");
    }
}
