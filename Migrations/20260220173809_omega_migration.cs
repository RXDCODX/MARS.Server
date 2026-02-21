using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class omega_migration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "HonkaiMarkupUser");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HonkaiMarkupUser",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TwitchId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LTokenV2 = table.Column<string>(type: "text", nullable: false),
                LastAutoMarkup = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LtmidV2 = table.Column<string>(type: "text", nullable: false),
                LtuidV2 = table.Column<string>(type: "text", nullable: false),
                TelegramId = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HonkaiMarkupUser", x => x.Id);
                table.ForeignKey(
                    name: "FK_HonkaiMarkupUser_TwitchUsers_TwitchId",
                    column: x => x.TwitchId,
                    principalTable: "TwitchUsers",
                    principalColumn: "TwitchId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_HonkaiMarkupUser_TwitchId",
            table: "HonkaiMarkupUser",
            column: "TwitchId");
    }
}
