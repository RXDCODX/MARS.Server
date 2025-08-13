using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class AddTekkenNotes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("Notes", "TekkenMovesPending");
        migrationBuilder.AddColumn<string[]>(
            "Notes",
            "TekkenMovesPending",
            "text[]",
            nullable: true
        );

        migrationBuilder.DropColumn("Notes", "TekkenMoves");
        migrationBuilder.AddColumn<string[]>("Notes", "TekkenMoves", "text[]", nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("Notes", "TekkenMovesPending");
        migrationBuilder.AddColumn<string>("Notes", "TekkenMovesPending", "text", nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Notes",
            table: "TekkenMovesPending",
            type: "text",
            nullable: true,
            oldClrType: typeof(string[]),
            oldType: "text[]",
            oldNullable: true
        );

        migrationBuilder.DropColumn("Notes", "TekkenMoves");
        migrationBuilder.AddColumn<string>("Notes", "TekkenMoves", "text", nullable: true);
    }
}
