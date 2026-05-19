using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class RemoveNextQueueItemIdFromSoundRequest : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestQueueItems_NextQueueItemId",
            table: "SoundRequestPlayerState");

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_NextQueueItemId",
            table: "SoundRequestPlayerState");

        migrationBuilder.DropColumn(
            name: "NextQueueItemId",
            table: "SoundRequestPlayerState");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "NextQueueItemId",
            table: "SoundRequestPlayerState",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_NextQueueItemId",
            table: "SoundRequestPlayerState",
            column: "NextQueueItemId");

        migrationBuilder.AddForeignKey(
            name: "FK_SoundRequestPlayerState_SoundRequestQueueItems_NextQueueItemId",
            table: "SoundRequestPlayerState",
            column: "NextQueueItemId",
            principalTable: "SoundRequestQueueItems",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
