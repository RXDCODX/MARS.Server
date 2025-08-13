using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class ChangedAvatarTekkenCharacters : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "AvatarImage",
            table: "TekkenCharacters",
            type: "bytea",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "AvatarImageExtension",
            table: "TekkenCharacters",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true
        );

        migrationBuilder.AddColumn<byte[]>(
            name: "FullBodyImage",
            table: "TekkenCharacters",
            type: "bytea",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "FullBodyImageExtension",
            table: "TekkenCharacters",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AvatarImage", table: "TekkenCharacters");

        migrationBuilder.DropColumn(name: "AvatarImageExtension", table: "TekkenCharacters");

        migrationBuilder.DropColumn(name: "FullBodyImage", table: "TekkenCharacters");

        migrationBuilder.DropColumn(name: "FullBodyImageExtension", table: "TekkenCharacters");
    }
}
