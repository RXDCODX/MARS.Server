using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class IDK : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestBaseTrackInfos_TwitchUsers_RequestedByTwitchUse~",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestBaseTrackInfos_RequestedByTwitchUserTwitchId",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropColumn(name: "QueueOrder", table: "SoundRequestBaseTrackInfos");

        migrationBuilder.DropColumn(
            name: "RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropColumn(
            name: "RequestedByTwitchUserTwitchId",
            table: "SoundRequestBaseTrackInfos"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "QueueOrder",
            table: "SoundRequestBaseTrackInfos",
            type: "integer",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "RequestedByTwitchUserTwitchId",
            table: "SoundRequestBaseTrackInfos",
            type: "character varying(50)",
            nullable: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestBaseTrackInfos_RequestedByTwitchUserTwitchId",
            table: "SoundRequestBaseTrackInfos",
            column: "RequestedByTwitchUserTwitchId"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestBaseTrackInfos_TwitchUsers_RequestedByTwitchUse~",
            table: "SoundRequestBaseTrackInfos",
            column: "RequestedByTwitchUserTwitchId",
            principalTable: "TwitchUsers",
            principalColumn: "TwitchId"
        );
    }
}
