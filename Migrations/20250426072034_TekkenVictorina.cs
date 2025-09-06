using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class TekkenVictorina : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TwitchLeaderboardUsers",
            columns: table => new
            {
                TwitchId = table.Column<string>(type: "text", nullable: false),
                DisplayName = table.Column<string>(type: "text", nullable: false),
                TekkenVictorinaWins = table.Column<int>(type: "integer", nullable: false),
                TekkenVictorinaWinsWithWaifu = table.Column<int>(type: "integer", nullable: false),
                RussianRouletteWins = table.Column<int>(type: "integer", nullable: false),
                RussianRouletteWinsWithWaifu = table.Column<int>(type: "integer", nullable: false),
                TriviaWins = table.Column<int>(type: "integer", nullable: false),
                TriviaWinsWithWaifus = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TwitchLeaderboardUsers", x => x.TwitchId);
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TwitchLeaderboardUsers");
    }
}
