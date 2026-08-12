using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations.MigrationsDb;

/// <inheritdoc />
public partial class AddWaifuChatFact : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WaifuChatFacts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TwitchId = table.Column<string>(type: "text", nullable: false),
                Fact = table.Column<string>(type: "text", nullable: false),
                ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Importance = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WaifuChatFacts", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WaifuChatFacts_TwitchId",
            table: "WaifuChatFacts",
            column: "TwitchId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "WaifuChatFacts");
    }
}
