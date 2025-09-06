using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class RemovePendingForeignKeys : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_TekkenMovesPending_TekkenCharactersPending_CharacterName",
            table: "TekkenMovesPending"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddForeignKey(
            name: "FK_TekkenMovesPending_TekkenCharactersPending_CharacterName",
            table: "TekkenMovesPending",
            column: "CharacterName",
            principalTable: "TekkenCharactersPending",
            principalColumn: "Name"
        );
    }
}
