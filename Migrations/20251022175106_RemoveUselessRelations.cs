using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class RemoveUselessRelations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_CinemaQueue_TwitchUsers_TwitchUserId",
            table: "CinemaQueue"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_FumoUsers_TwitchUsers_TwitchId",
            table: "FumoUsers"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_HelloVideosUsers_TwitchUsers_TwitchId",
            table: "HelloVideosUsers"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_HonkaiMarkupUser_TwitchUsers_TwitchId",
            table: "HonkaiMarkupUser"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestBaseTrackInfos_TwitchUsers_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_TwitchUsers_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_TwitchLeaderboardUsers_TwitchUsers_TwitchId",
            table: "TwitchLeaderboardUsers"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_WaifuRollGuarantees_TwitchUsers_TwitchId",
            table: "WaifuRollGuarantees"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_CinemaQueue_TwitchUsers_TwitchUserId",
            table: "CinemaQueue",
            column: "TwitchUserId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_FumoUsers_TwitchUsers_TwitchId",
            table: "FumoUsers",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Cascade
        );

        migrationBuilder.AddForeignKey(
            name: "FK_HelloVideosUsers_TwitchUsers_TwitchId",
            table: "HelloVideosUsers",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Cascade
        );

        migrationBuilder.AddForeignKey(
            name: "FK_HonkaiMarkupUser_TwitchUsers_TwitchId",
            table: "HonkaiMarkupUser",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestBaseTrackInfos_TwitchUsers_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos",
            column: "RequestedByTwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestPlayerState_TwitchUsers_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackRequestedBy",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_TwitchLeaderboardUsers_TwitchUsers_TwitchId",
            table: "TwitchLeaderboardUsers",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Cascade
        );

        migrationBuilder.AddForeignKey(
            name: "FK_WaifuRollGuarantees_TwitchUsers_TwitchId",
            table: "WaifuRollGuarantees",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Cascade
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_CinemaQueue_TwitchUsers_TwitchUserId",
            table: "CinemaQueue"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_FumoUsers_TwitchUsers_TwitchId",
            table: "FumoUsers"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_HelloVideosUsers_TwitchUsers_TwitchId",
            table: "HelloVideosUsers"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_HonkaiMarkupUser_TwitchUsers_TwitchId",
            table: "HonkaiMarkupUser"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestBaseTrackInfos_TwitchUsers_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_TwitchUsers_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_TwitchLeaderboardUsers_TwitchUsers_TwitchId",
            table: "TwitchLeaderboardUsers"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_WaifuRollGuarantees_TwitchUsers_TwitchId",
            table: "WaifuRollGuarantees"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_CinemaQueue_TwitchUsers_TwitchUserId",
            table: "CinemaQueue",
            column: "TwitchUserId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_FumoUsers_TwitchUsers_TwitchId",
            table: "FumoUsers",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_HelloVideosUsers_TwitchUsers_TwitchId",
            table: "HelloVideosUsers",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_HonkaiMarkupUser_TwitchUsers_TwitchId",
            table: "HonkaiMarkupUser",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestBaseTrackInfos_TwitchUsers_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos",
            column: "RequestedByTwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestPlayerState_TwitchUsers_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackRequestedBy",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_TwitchLeaderboardUsers_TwitchUsers_TwitchId",
            table: "TwitchLeaderboardUsers",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_WaifuRollGuarantees_TwitchUsers_TwitchId",
            table: "WaifuRollGuarantees",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.Restrict
        );
    }
}
