using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddTwitchUserTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "WaifuRollGuarantees",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "TwitchLeaderboardUsers",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "TwitchLeaderboardUsers",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Hosts",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "Hosts",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "HonkaiMarkupUser",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "HelloVideosUsers",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "HelloVideosUsers",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "FumoUsers",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "FumoUsers",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "UserName",
            table: "FollowersEntitys",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "UserLogin",
            table: "FollowersEntitys",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "ProfileImageUrl",
            table: "FollowersEntitys",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "FollowersEntitys",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "ChatColor",
            table: "FollowersEntitys",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "UserId",
            table: "FollowersEntitys",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "HostId",
            table: "CD",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AlterColumn<string>(
            name: "HostId",
            table: "AutoHello",
            type: "character varying(50)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.CreateTable(
            name: "TwitchUsers",
            columns: table => new
            {
                TwitchId = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: false
                ),
                UserLogin = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                DisplayName = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                ProfileImageUrl = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: true
                ),
                ChatColor = table.Column<string>(
                    type: "character varying(20)",
                    maxLength: 20,
                    nullable: true
                ),
                IsModerator = table.Column<bool>(type: "boolean", nullable: false),
                IsVip = table.Column<bool>(type: "boolean", nullable: false),
                FollowedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                LastUpdated = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                CreatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TwitchUsers", x => x.TwitchId);
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackRequestedBy"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestBaseTrackInfos_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos",
            column: "RequestedByTwitchId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_HonkaiMarkupUser_TwitchId",
            table: "HonkaiMarkupUser",
            column: "TwitchId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_HelloVideosUsers_TwitchId",
            table: "HelloVideosUsers",
            column: "TwitchId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_CinemaQueue_TwitchUserId",
            table: "CinemaQueue",
            column: "TwitchUserId"
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
            name: "FK_FollowersEntitys_TwitchUsers_UserId",
            table: "FollowersEntitys",
            column: "UserId",
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
            onDelete: ReferentialAction.SetNull
        );

        migrationBuilder.AddForeignKey(
            name: "FK_Hosts_TwitchUsers_TwitchId",
            table: "Hosts",
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

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_CinemaQueue_TwitchUsers_TwitchUserId",
            table: "CinemaQueue"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_FollowersEntitys_TwitchUsers_UserId",
            table: "FollowersEntitys"
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

        migrationBuilder.DropForeignKey(name: "FK_Hosts_TwitchUsers_TwitchId", table: "Hosts");

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

        migrationBuilder.DropTable(name: "TwitchUsers");

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestBaseTrackInfos_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropIndex(name: "IX_HonkaiMarkupUser_TwitchId", table: "HonkaiMarkupUser");

        migrationBuilder.DropIndex(name: "IX_HelloVideosUsers_TwitchId", table: "HelloVideosUsers");

        migrationBuilder.DropIndex(name: "IX_CinemaQueue_TwitchUserId", table: "CinemaQueue");

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "WaifuRollGuarantees",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "TwitchLeaderboardUsers",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "TwitchLeaderboardUsers",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Hosts",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "Hosts",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "HonkaiMarkupUser",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "HelloVideosUsers",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "HelloVideosUsers",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100
        );

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "FumoUsers",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "TwitchId",
            table: "FumoUsers",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "UserName",
            table: "FollowersEntitys",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100
        );

        migrationBuilder.AlterColumn<string>(
            name: "UserLogin",
            table: "FollowersEntitys",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100
        );

        migrationBuilder.AlterColumn<string>(
            name: "ProfileImageUrl",
            table: "FollowersEntitys",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(500)",
            oldMaxLength: 500,
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "FollowersEntitys",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "ChatColor",
            table: "FollowersEntitys",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(20)",
            oldMaxLength: 20,
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "UserId",
            table: "FollowersEntitys",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "HostId",
            table: "CD",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );

        migrationBuilder.AlterColumn<string>(
            name: "HostId",
            table: "AutoHello",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(50)"
        );
    }
}
