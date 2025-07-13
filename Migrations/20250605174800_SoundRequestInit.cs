using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class SoundRequestInit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SoundRequestBaseTrackInfos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TrackName = table.Column<string>(type: "text", nullable: false),
                Authors = table.Column<string[]>(type: "text[]", nullable: true),
                FeatAuthors = table.Column<string[]>(type: "text[]", nullable: true),
                Duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                Genre = table.Column<string[]>(type: "text[]", nullable: true),
                Url = table.Column<string>(type: "text", nullable: false),
                LastTimePlays = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SoundRequestBaseTrackInfos", x => x.Id);
            }
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

        migrationBuilder.CreateTable(
            name: "SoundRequestPlayerState",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CurrentTrackId = table.Column<Guid>(type: "uuid", nullable: true),
                NextTrackId = table.Column<Guid>(type: "uuid", nullable: true),
                CurrentTrackDuration = table.Column<TimeSpan>(type: "interval", nullable: true),
                IsPaused = table.Column<bool>(type: "boolean", nullable: false),
                IsMuted = table.Column<bool>(type: "boolean", nullable: false),
                IsStoped = table.Column<bool>(type: "boolean", nullable: false),
                Volume = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SoundRequestPlayerState", x => x.Id);
                table.ForeignKey(
                    name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_CurrentT~",
                    column: x => x.CurrentTrackId,
                    principalTable: "SoundRequestBaseTrackInfos",
                    principalColumn: "Id"
                );
                table.ForeignKey(
                    name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_NextTrac~",
                    column: x => x.NextTrackId,
                    principalTable: "SoundRequestBaseTrackInfos",
                    principalColumn: "Id"
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "SoundRequestUserQueue",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TwitchDisplayName = table.Column<string>(type: "text", nullable: true),
                TwitchId = table.Column<string>(type: "text", nullable: false),
                Order = table.Column<int>(type: "integer", nullable: false),
                RequestedTrackId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SoundRequestUserQueue", x => x.Id);
                table.ForeignKey(
                    name: "FK_SoundRequestUserQueue_SoundRequestBaseTrackInfos_RequestedT~",
                    column: x => x.RequestedTrackId,
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

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackId",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_NextTrackId",
            table: "SoundRequestPlayerState",
            column: "NextTrackId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestUserQueue_RequestedTrackId",
            table: "SoundRequestUserQueue",
            column: "RequestedTrackId",
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SoundRequestBackgroundTracks");

        migrationBuilder.DropTable(name: "SoundRequestPlayerState");

        migrationBuilder.DropTable(name: "SoundRequestUserQueue");

        migrationBuilder.DropTable(name: "SoundRequestBaseTrackInfos");
    }
}
