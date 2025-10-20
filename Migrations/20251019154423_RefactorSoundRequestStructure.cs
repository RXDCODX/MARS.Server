using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class RefactorSoundRequestStructure : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SoundRequestUserQueue");

        migrationBuilder.DropColumn(name: "Order", table: "SoundRequestBaseTrackInfos");

        migrationBuilder.AddColumn<int>(
            name: "QueueOrder",
            table: "SoundRequestBaseTrackInfos",
            type: "integer",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "RequestedByDisplayName",
            table: "SoundRequestBaseTrackInfos",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true
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
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_CurrentT~",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_NextTrac~",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackId",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_NextTrackId",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropColumn(name: "QueueOrder", table: "SoundRequestBaseTrackInfos");

        migrationBuilder.DropColumn(
            name: "RequestedByDisplayName",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.DropColumn(
            name: "RequestedByTwitchId",
            table: "SoundRequestBaseTrackInfos"
        );

        migrationBuilder.AddColumn<int>(
            name: "Order",
            table: "SoundRequestBaseTrackInfos",
            type: "integer",
            nullable: false,
            defaultValue: 0
        );

        migrationBuilder.CreateTable(
            name: "SoundRequestUserQueue",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RequestedTrackId = table.Column<Guid>(type: "uuid", nullable: true),
                Order = table.Column<int>(type: "integer", nullable: false),
                TwitchDisplayName = table.Column<string>(type: "text", nullable: true),
                TwitchId = table.Column<string>(type: "text", nullable: false),
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
            name: "IX_SoundRequestUserQueue_RequestedTrackId",
            table: "SoundRequestUserQueue",
            column: "RequestedTrackId",
            unique: true
        );
    }
}
