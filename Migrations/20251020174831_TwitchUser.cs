using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class TwitchUser : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CurrentTrackRequestedByDisplayName",
            table: "SoundRequestPlayerState"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CurrentTrackRequestedByDisplayName",
            table: "SoundRequestPlayerState",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true
        );
    }
}
