using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddFieldIsFileNotConvertable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsFileNotConvertable",
            table: "RandomMemeOrder",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "FileInfo_IsFileNotConvertable",
            table: "Alerts",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsFileNotConvertable",
            table: "RandomMemeOrder");

        migrationBuilder.DropColumn(
            name: "FileInfo_IsFileNotConvertable",
            table: "Alerts");
    }
}
