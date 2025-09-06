using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class YandexMusicInfo : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Domain",
            table: "SoundRequestBaseTrackInfos",
            type: "integer",
            nullable: false,
            defaultValue: 0
        );

        migrationBuilder.AddColumn<string>(
            name: "YandexInfo_ArtworkUrl",
            table: "SoundRequestBaseTrackInfos",
            type: "text",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "YandexInfo_MP3Url",
            table: "SoundRequestBaseTrackInfos",
            type: "text",
            nullable: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Domain", table: "SoundRequestBaseTrackInfos");

        migrationBuilder.DropColumn(
            name: "YandexInfo_ArtworkUrl",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropColumn(name: "YandexInfo_MP3Url", table: "SoundRequestBaseTrackInfos");
    }
}
