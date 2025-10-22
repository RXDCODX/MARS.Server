using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class ChangeVolumeToFloat : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<float>(
            name: "Volume",
            table: "SoundRequestPlayerState",
            type: "real",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "Volume",
            table: "SoundRequestPlayerState",
            type: "integer",
            nullable: false,
            oldClrType: typeof(float),
            oldType: "real"
        );
    }
}
