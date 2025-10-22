using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class SoundRequestTrackProgress : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "CurrentTrackDuration",
            table: "SoundRequestPlayerState",
            newName: "CurrentTrackProgress"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "CurrentTrackProgress",
            table: "SoundRequestPlayerState",
            newName: "CurrentTrackDuration"
        );
    }
}
