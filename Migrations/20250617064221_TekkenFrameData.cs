using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class TekkenFrameData : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "TekkenCharacters",
            type: "text",
            nullable: true
        );

        migrationBuilder.AddColumn<string[]>(
            name: "Strengths",
            table: "TekkenCharacters",
            type: "text[]",
            nullable: true
        );

        migrationBuilder.AddColumn<string[]>(
            name: "Weaknesess",
            table: "TekkenCharacters",
            type: "text[]",
            nullable: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Description", table: "TekkenCharacters");

        migrationBuilder.DropColumn(name: "Strengths", table: "TekkenCharacters");

        migrationBuilder.DropColumn(name: "Weaknesess", table: "TekkenCharacters");
    }
}
