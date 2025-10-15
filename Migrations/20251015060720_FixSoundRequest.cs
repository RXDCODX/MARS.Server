using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class FixSoundRequest : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "CurrentTrackRequestedByDisplayName",
            table: "SoundRequestPlayerState",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropColumn(
            name: "CurrentTrackRequestedByDisplayName",
            table: "SoundRequestPlayerState"
        );
    }
}
