using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class TwitchUsersUpdate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_CinemaQueue_TwitchUsers_TwitchUserId",
            table: "CinemaQueue"
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
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_CurrentT~",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_NextTrac~",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_TwitchUsers_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState"
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
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_CurrentT~",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackId",
            principalTable: "SoundRequestBaseTrackInfos",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_NextTrac~",
            table: "SoundRequestPlayerState",
            column: "NextTrackId",
            principalTable: "SoundRequestBaseTrackInfos",
            principalColumn: "Id",
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
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_CinemaQueue_TwitchUsers_TwitchUserId",
            table: "CinemaQueue"
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
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_CurrentT~",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_NextTrac~",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_TwitchUsers_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_CinemaQueue_TwitchUsers_TwitchUserId",
            table: "CinemaQueue",
            column: "TwitchUserId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.SetNull
        );

        migrationBuilder.AddForeignKey(
            name: "FK_HonkaiMarkupUser_TwitchUsers_TwitchId",
            table: "HonkaiMarkupUser",
            column: "TwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.SetNull
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestBaseTrackInfos_TwitchUsers_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos",
            column: "RequestedByTwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.SetNull
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_CurrentT~",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackId",
            principalTable: "SoundRequestBaseTrackInfos",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_NextTrac~",
            table: "SoundRequestPlayerState",
            column: "NextTrackId",
            principalTable: "SoundRequestBaseTrackInfos",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestPlayerState_TwitchUsers_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackRequestedBy",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId",
            onDelete: ReferentialAction.SetNull
        );
    }
}
