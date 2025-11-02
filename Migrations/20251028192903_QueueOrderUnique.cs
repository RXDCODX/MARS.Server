using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class QueueOrderUnique : Migration
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
            name: "FK_TwitchLeaderboardUsers_TwitchUsers_TwitchId",
            table: "TwitchLeaderboardUsers"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_WaifuRollGuarantees_TwitchUsers_TwitchId",
            table: "WaifuRollGuarantees"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestQueueItems_QueueOrder",
            table: "SoundRequestQueueItems"
        );

        migrationBuilder.AlterColumn<int>(
            name: "QueueOrder",
            table: "SoundRequestQueueItems",
            type: "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestQueueItems_QueueOrder",
            table: "SoundRequestQueueItems",
            column: "QueueOrder",
            unique: true,
            descending: Array.Empty<bool>()
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
            name: "FK_TwitchLeaderboardUsers_TwitchUsers_TwitchId",
            table: "TwitchLeaderboardUsers"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_WaifuRollGuarantees_TwitchUsers_TwitchId",
            table: "WaifuRollGuarantees"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestQueueItems_QueueOrder",
            table: "SoundRequestQueueItems"
        );

        migrationBuilder.AlterColumn<int>(
            name: "QueueOrder",
            table: "SoundRequestQueueItems",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestQueueItems_QueueOrder",
            table: "SoundRequestQueueItems",
            column: "QueueOrder"
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
}
