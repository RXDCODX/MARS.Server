using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class RefactorPlayerStateToEnum : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DisplayName", table: "TwitchLeaderboardUsers");

        migrationBuilder.DropColumn(name: "IsPaused", table: "SoundRequestPlayerState");

        migrationBuilder.DropColumn(name: "IsStoped", table: "SoundRequestPlayerState");

        migrationBuilder.DropColumn(
            name: "RequestedByDisplayName",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropColumn(name: "Name", table: "Hosts");

        migrationBuilder.DropColumn(name: "Name", table: "HelloVideosUsers");

        migrationBuilder.DropColumn(name: "DisplayName", table: "FumoUsers");

        migrationBuilder.DropColumn(name: "ChatColor", table: "FollowersEntitys");

        migrationBuilder.DropColumn(name: "DisplayName", table: "FollowersEntitys");

        migrationBuilder.DropColumn(name: "FollowedAt", table: "FollowersEntitys");

        migrationBuilder.DropColumn(name: "IsModerator", table: "FollowersEntitys");

        migrationBuilder.DropColumn(name: "IsVip", table: "FollowersEntitys");

        migrationBuilder.DropColumn(name: "LastUpdated", table: "FollowersEntitys");

        migrationBuilder.DropColumn(name: "ProfileImageUrl", table: "FollowersEntitys");

        migrationBuilder.DropColumn(name: "UserLogin", table: "FollowersEntitys");

        migrationBuilder.DropColumn(name: "UserName", table: "FollowersEntitys");

        migrationBuilder.DropColumn(name: "AddedBy", table: "CinemaQueue");

        migrationBuilder.DropColumn(name: "TwitchUsername", table: "CinemaQueue");

        migrationBuilder.AddColumn<Guid>(
            name: "CurrentTrackId1",
            table: "SoundRequestPlayerState",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.AddColumn<Guid>(
            name: "NextTrackId1",
            table: "SoundRequestPlayerState",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.AddColumn<int>(
            name: "State",
            table: "SoundRequestPlayerState",
            type: "integer",
            nullable: false,
            defaultValue: 0
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackId1",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackId1"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_NextTrackId1",
            table: "SoundRequestPlayerState",
            column: "NextTrackId1"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_Current~1",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackId1",
            principalTable: "SoundRequestBaseTrackInfos",
            principalColumn: "Id"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_NextTra~1",
            table: "SoundRequestPlayerState",
            column: "NextTrackId1",
            principalTable: "SoundRequestBaseTrackInfos",
            principalColumn: "Id"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_Current~1",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_NextTra~1",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackId1",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_NextTrackId1",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropColumn(name: "CurrentTrackId1", table: "SoundRequestPlayerState");

        migrationBuilder.DropColumn(name: "NextTrackId1", table: "SoundRequestPlayerState");

        migrationBuilder.DropColumn(name: "State", table: "SoundRequestPlayerState");

        migrationBuilder.AddColumn<string>(
            name: "DisplayName",
            table: "TwitchLeaderboardUsers",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.AddColumn<bool>(
            name: "IsPaused",
            table: "SoundRequestPlayerState",
            type: "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<bool>(
            name: "IsStoped",
            table: "SoundRequestPlayerState",
            type: "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<string>(
            name: "RequestedByDisplayName",
            table: "SoundRequestBaseTrackInfos",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "Hosts",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "HelloVideosUsers",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.AddColumn<string>(
            name: "DisplayName",
            table: "FumoUsers",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "ChatColor",
            table: "FollowersEntitys",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "DisplayName",
            table: "FollowersEntitys",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true
        );

        migrationBuilder.AddColumn<DateTime>(
            name: "FollowedAt",
            table: "FollowersEntitys",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
        );

        migrationBuilder.AddColumn<bool>(
            name: "IsModerator",
            table: "FollowersEntitys",
            type: "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<bool>(
            name: "IsVip",
            table: "FollowersEntitys",
            type: "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<DateTime>(
            name: "LastUpdated",
            table: "FollowersEntitys",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
        );

        migrationBuilder.AddColumn<string>(
            name: "ProfileImageUrl",
            table: "FollowersEntitys",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "UserLogin",
            table: "FollowersEntitys",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.AddColumn<string>(
            name: "UserName",
            table: "FollowersEntitys",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: ""
        );

        migrationBuilder.AddColumn<string>(
            name: "AddedBy",
            table: "CinemaQueue",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "TwitchUsername",
            table: "CinemaQueue",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true
        );
    }
}
