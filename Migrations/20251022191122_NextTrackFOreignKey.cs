using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class NextTrackFOreignKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
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
            name: "IX_SoundRequestPlayerState_CurrentTrackId",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackId1",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_NextTrackId",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_NextTrackId1",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropColumn(name: "CurrentTrackId1", table: "SoundRequestPlayerState");

        migrationBuilder.DropColumn(name: "NextTrackId1", table: "SoundRequestPlayerState");

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackId",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackId",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_NextTrackId",
            table: "SoundRequestPlayerState",
            column: "NextTrackId",
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackId",
            table: "SoundRequestPlayerState"
        );

        migrationBuilder.DropIndex(
            name: "IX_SoundRequestPlayerState_NextTrackId",
            table: "SoundRequestPlayerState"
        );

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

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackId",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_CurrentTrackId1",
            table: "SoundRequestPlayerState",
            column: "CurrentTrackId1"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SoundRequestPlayerState_NextTrackId",
            table: "SoundRequestPlayerState",
            column: "NextTrackId"
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
}
