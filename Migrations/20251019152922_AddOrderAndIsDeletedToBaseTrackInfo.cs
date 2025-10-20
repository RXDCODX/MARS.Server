using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderAndIsDeletedToBaseTrackInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_CurrentT~",
                table: "SoundRequestPlayerState");

            migrationBuilder.DropForeignKey(
                name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_NextTrac~",
                table: "SoundRequestPlayerState");

            migrationBuilder.DropIndex(
                name: "IX_SoundRequestPlayerState_CurrentTrackId",
                table: "SoundRequestPlayerState");

            migrationBuilder.DropIndex(
                name: "IX_SoundRequestPlayerState_NextTrackId",
                table: "SoundRequestPlayerState");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SoundRequestBaseTrackInfos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "SoundRequestBaseTrackInfos",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SoundRequestBaseTrackInfos");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "SoundRequestBaseTrackInfos");

            migrationBuilder.CreateIndex(
                name: "IX_SoundRequestPlayerState_CurrentTrackId",
                table: "SoundRequestPlayerState",
                column: "CurrentTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_SoundRequestPlayerState_NextTrackId",
                table: "SoundRequestPlayerState",
                column: "NextTrackId");

            migrationBuilder.AddForeignKey(
                name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_CurrentT~",
                table: "SoundRequestPlayerState",
                column: "CurrentTrackId",
                principalTable: "SoundRequestBaseTrackInfos",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SoundRequestPlayerState_SoundRequestBaseTrackInfos_NextTrac~",
                table: "SoundRequestPlayerState",
                column: "NextTrackId",
                principalTable: "SoundRequestBaseTrackInfos",
                principalColumn: "Id");
        }
    }
}
