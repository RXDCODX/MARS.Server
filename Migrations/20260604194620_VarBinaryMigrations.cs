using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class VarBinaryMigrations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<byte[]>(
            name: "Image",
            table: "TekkenCharacters",
            type: "bytea",
            nullable: true,
            oldClrType: typeof(byte[]),
            oldType: "varbinary(max)",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<byte[]>(
            name: "Image",
            table: "TekkenCharacters",
            type: "varbinary(max)",
            nullable: true,
            oldClrType: typeof(byte[]),
            oldType: "bytea",
            oldNullable: true);
    }
}
