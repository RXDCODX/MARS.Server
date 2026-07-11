using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddWaifuRollAudio : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "AudioId",
            table: "Waifus",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "WaifuRollAudios",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                AudioData = table.Column<byte[]>(type: "bytea", nullable: false),
                FileExtension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WaifuRollAudios", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Waifus_AudioId",
            table: "Waifus",
            column: "AudioId");

        migrationBuilder.AddForeignKey(
            name: "FK_Waifus_WaifuRollAudios_AudioId",
            table: "Waifus",
            column: "AudioId",
            principalTable: "WaifuRollAudios",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Waifus_WaifuRollAudios_AudioId",
            table: "Waifus");

        migrationBuilder.DropTable(
            name: "WaifuRollAudios");

        migrationBuilder.DropIndex(
            name: "IX_Waifus_AudioId",
            table: "Waifus");

        migrationBuilder.DropColumn(
            name: "AudioId",
            table: "Waifus");
    }
}
