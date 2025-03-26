using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class MediaUrlOrLocalFile : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "FileInfo_IsLocal",
            table: "Alerts",
            type: "boolean",
            nullable: false,
            defaultValue: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FileInfo_IsLocal", table: "Alerts");
    }
}
