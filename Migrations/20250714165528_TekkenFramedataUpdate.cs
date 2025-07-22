using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class TekkenFramedataUpdate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "LinkToImage",
            table: "TekkenCharacters",
            type: "character varying(300)",
            maxLength: 300,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AddColumn<byte[]>(
            name: "Image",
            table: "TekkenCharacters",
            type: "bytea",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "ImageExtension",
            table: "TekkenCharacters",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "PageUrl",
            table: "TekkenCharacters",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: ""
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Image", table: "TekkenCharacters");

        migrationBuilder.DropColumn(name: "ImageExtension", table: "TekkenCharacters");

        migrationBuilder.DropColumn(name: "PageUrl", table: "TekkenCharacters");

        migrationBuilder.AlterColumn<string>(
            name: "LinkToImage",
            table: "TekkenCharacters",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(300)",
            oldMaxLength: 300,
            oldNullable: true
        );
    }
}
