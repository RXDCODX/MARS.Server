using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class SoundRequestQueueItem : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackId",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_NextTrackId",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestBaseTrackInfos_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropColumn(
            name: "CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.RenameColumn(
            name: "NextTrackId",
            table: "SoundRequestPlayerState",
            newName: "NextQueueItemId"
        );

        migrationBuilder.RenameColumn(
            name: "CurrentTrackId",
            table: "SoundRequestPlayerState",
            newName: "CurrentQueueItemId"
        );

        migrationBuilder.AlterColumn<string>(
            name: "TrackName",
            table: "SoundRequestBaseTrackInfos",
            type: "character varying(300)",
            maxLength: 300,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "SoundRequestBaseTrackInfos",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
        );

        migrationBuilder.AddColumn<string>(
            name: "RequestedByTwitchUserTwitchId",
            table: "SoundRequestBaseTrackInfos",
            type: "character varying(50)",
            nullable: true
        );

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "SoundRequestBaseTrackInfos",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
        );

        migrationBuilder.CreateTable(
            name: "SoundRequestQueueItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TrackId = table.Column<Guid>(type: "uuid", nullable: false),
                QueueOrder = table.Column<int>(type: "integer", nullable: true),
                RequestedByTwitchId = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: false
                ),
                RequestedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SoundRequestQueueItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_SoundRequestQueueItems_SoundRequestBaseTrackInfos_TrackId",
                    column: x => x.TrackId,
                    principalTable: "SoundRequestBaseTrackInfos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_SoundRequestQueueItems_TwitchUsers_RequestedByTwitchId",
                    column: x => x.RequestedByTwitchId,
                    principalTable: "TwitchUsers",
                    principalColumn: "TwitchId",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_CurrentQueueItemId",
            table: "SoundRequestPlayerState",
            column: "CurrentQueueItemId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_NextQueueItemId",
            table: "SoundRequestPlayerState",
            column: "NextQueueItemId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestBaseTrackInfos_RequestedByTwitchUserTwitchId",
            table: "SoundRequestBaseTrackInfos",
            column: "RequestedByTwitchUserTwitchId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestBaseTrackInfos_Url",
            table: "SoundRequestBaseTrackInfos",
            column: "Url",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestBaseTrackInfos_VideoId",
            table: "SoundRequestBaseTrackInfos",
            column: "VideoId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestQueueItems_QueueOrder",
            table: "SoundRequestQueueItems",
            column: "QueueOrder"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestQueueItems_RequestedByTwitchId",
            table: "SoundRequestQueueItems",
            column: "RequestedByTwitchId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestQueueItems_TrackId",
            table: "SoundRequestQueueItems",
            column: "TrackId"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestBaseTrackInfos_TwitchUsers_RequestedByTwitchUse~",
            table: "SoundRequestBaseTrackInfos",
            column: "RequestedByTwitchUserTwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestQueueItems_CurrentQueue~",
            table: "SoundRequestPlayerState",
            column: "CurrentQueueItemId",
            principalTable: "SoundRequestQueueItems",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestQueueItems_NextQueueIte~",
            table: "SoundRequestPlayerState",
            column: "NextQueueItemId",
            principalTable: "SoundRequestQueueItems",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestBaseTrackInfos_TwitchUsers_RequestedByTwitchUse~",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestQueueItems_CurrentQueue~",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestQueueItems_NextQueueIte~",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropTable(name: "SoundRequestQueueItems");

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_CurrentQueueItemId",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_NextQueueItemId",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestBaseTrackInfos_RequestedByTwitchUserTwitchId",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestBaseTrackInfos_Url",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestBaseTrackInfos_VideoId",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropColumn(name: "CreatedAt", table: "SoundRequestBaseTrackInfos");

        migrationBuilder.DropColumn(
            name: "RequestedByTwitchUserTwitchId",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropColumn(name: "UpdatedAt", table: "SoundRequestBaseTrackInfos");

        migrationBuilder.RenameColumn(
            name: "NextQueueItemId",
            table: "SoundRequestPlayerState",
            newName: "NextTrackId"
        );

        migrationBuilder.RenameColumn(
            name: "CurrentQueueItemId",
            table: "SoundRequestPlayerState",
            newName: "CurrentTrackId"
        );

        migrationBuilder.AddColumn<string>(
            name: "CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "TrackName",
            table: "SoundRequestBaseTrackInfos",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(300)",
            oldMaxLength: 300
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackId",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackId",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackRequestedBy",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackRequestedBy"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_NextTrackId",
            table: "SoundRequestPlayerState",
            column: "NextTrackId",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestBaseTrackInfos_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos",
            column: "RequestedByTwitchId"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestBaseTrackInfos_TwitchUsers_RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos",
            column: "RequestedByTwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId"
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
            principalColumn: "TwitchId"
        );
    }
}
