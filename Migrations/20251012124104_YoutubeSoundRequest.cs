using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class YoutubeSoundRequest : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SoundRequestBackgroundTracks");

        migrationBuilder.DropColumn(name: "Domain", table: "SoundRequestBaseTrackInfos");

        migrationBuilder.DropColumn(name: "FeatAuthors", table: "SoundRequestBaseTrackInfos");

        migrationBuilder.DropColumn(name: "Genre", table: "SoundRequestBaseTrackInfos");

        migrationBuilder.RenameColumn(
            name: "YandexInfo_ArtworkUrl",
            table: "SoundRequestBaseTrackInfos",
            newName: "ArtworkUrl"
        );

        migrationBuilder.RenameColumn(
            name: "YandexInfo_MP3Url",
            table: "SoundRequestBaseTrackInfos",
            newName: "VideoId"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "ArtworkUrl",
            table: "SoundRequestBaseTrackInfos",
            newName: "YandexInfo_ArtworkUrl"
        );

        migrationBuilder.RenameColumn(
            name: "VideoId",
            table: "SoundRequestBaseTrackInfos",
            newName: "YandexInfo_MP3Url"
        );

        migrationBuilder.AddColumn<int>(
            name: "Domain",
            table: "SoundRequestBaseTrackInfos",
            type: "integer",
            nullable: false,
            defaultValue: 0
        );

        migrationBuilder.AddColumn<string[]>(
            name: "FeatAuthors",
            table: "SoundRequestBaseTrackInfos",
            type: "text[]",
            nullable: true
        );

        migrationBuilder.AddColumn<string[]>(
            name: "Genre",
            table: "SoundRequestBaseTrackInfos",
            type: "text[]",
            nullable: true
        );

        migrationBuilder.CreateTable(
            name: "SoundRequestBackgroundTracks",
            columns: table => new { TrackId = table.Column<Guid>(type: "uuid", nullable: false) },
            constraints: table =>
            {
                table.ForeignKey(
                    name: "FK_SoundRequestBackgroundTracks_SoundRequestBaseTrackInfos_Tra~",
                    column: x => x.TrackId,
                    principalTable: "SoundRequestBaseTrackInfos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestBackgroundTracks_TrackId",
            table: "SoundRequestBackgroundTracks",
            column: "TrackId",
            unique: true
        );
    }
}
