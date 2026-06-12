using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFramedata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TekkenMoves");

            migrationBuilder.DropTable(
                name: "TekkenMovesPending");

            migrationBuilder.DropTable(
                name: "TekkenCharacters");

            migrationBuilder.DropTable(
                name: "TekkenCharactersPending");

            migrationBuilder.DropColumn(
                name: "TekkenVictorinaWins",
                table: "TwitchLeaderboardUsers");

            migrationBuilder.DropColumn(
                name: "TekkenVictorinaWinsWithWaifu",
                table: "TwitchLeaderboardUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TekkenVictorinaWins",
                table: "TwitchLeaderboardUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TekkenVictorinaWinsWithWaifu",
                table: "TwitchLeaderboardUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TekkenCharacters",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    AvatarImage = table.Column<byte[]>(type: "bytea", nullable: true),
                    AvatarImageExtension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    FullBodyImage = table.Column<byte[]>(type: "bytea", nullable: true),
                    FullBodyImageExtension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Image = table.Column<byte[]>(type: "bytea", nullable: true),
                    ImageExtension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LinkToImage = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PageUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Strengths = table.Column<string[]>(type: "text[]", nullable: true),
                    Weaknesess = table.Column<string[]>(type: "text[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TekkenCharacters", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "TekkenCharactersPending",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    AvatarImage = table.Column<byte[]>(type: "bytea", nullable: true),
                    AvatarImageExtension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    FullBodyImage = table.Column<byte[]>(type: "bytea", nullable: true),
                    FullBodyImageExtension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Image = table.Column<byte[]>(type: "bytea", nullable: true),
                    ImageExtension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LastUpdateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LinkToImage = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PageUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Strengths = table.Column<string[]>(type: "text[]", nullable: true),
                    Weaknesess = table.Column<string[]>(type: "text[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TekkenCharactersPending", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "TekkenMoves",
                columns: table => new
                {
                    CharacterName = table.Column<string>(type: "text", nullable: false),
                    Command = table.Column<string>(type: "text", nullable: false),
                    BlockFrame = table.Column<string>(type: "text", nullable: true),
                    CounterHitFrame = table.Column<string>(type: "text", nullable: true),
                    Damage = table.Column<string>(type: "text", nullable: true),
                    HeatBurst = table.Column<bool>(type: "boolean", nullable: false),
                    HeatEngage = table.Column<bool>(type: "boolean", nullable: false),
                    HeatSmash = table.Column<bool>(type: "boolean", nullable: false),
                    HitFrame = table.Column<string>(type: "text", nullable: true),
                    HitLevel = table.Column<string>(type: "text", nullable: true),
                    Homing = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string[]>(type: "text[]", nullable: true),
                    PowerCrush = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresHeat = table.Column<bool>(type: "boolean", nullable: false),
                    StanceCode = table.Column<string>(type: "text", nullable: false),
                    StanceName = table.Column<string>(type: "text", nullable: true),
                    StartUpFrame = table.Column<string>(type: "text", nullable: true),
                    Throw = table.Column<bool>(type: "boolean", nullable: false),
                    Tornado = table.Column<bool>(type: "boolean", nullable: false),
                    VideoUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TekkenMoves", x => new { x.CharacterName, x.Command });
                    table.ForeignKey(
                        name: "FK_TekkenMoves_TekkenCharacters_CharacterName",
                        column: x => x.CharacterName,
                        principalTable: "TekkenCharacters",
                        principalColumn: "Name");
                });

            migrationBuilder.CreateTable(
                name: "TekkenMovesPending",
                columns: table => new
                {
                    CharacterName = table.Column<string>(type: "text", nullable: false),
                    Command = table.Column<string>(type: "text", nullable: false),
                    BlockFrame = table.Column<string>(type: "text", nullable: true),
                    CounterHitFrame = table.Column<string>(type: "text", nullable: true),
                    Damage = table.Column<string>(type: "text", nullable: true),
                    HeatBurst = table.Column<bool>(type: "boolean", nullable: false),
                    HeatEngage = table.Column<bool>(type: "boolean", nullable: false),
                    HeatSmash = table.Column<bool>(type: "boolean", nullable: false),
                    HitFrame = table.Column<string>(type: "text", nullable: true),
                    HitLevel = table.Column<string>(type: "text", nullable: true),
                    Homing = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string[]>(type: "text[]", nullable: true),
                    PowerCrush = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresHeat = table.Column<bool>(type: "boolean", nullable: false),
                    StanceCode = table.Column<string>(type: "text", nullable: false),
                    StanceName = table.Column<string>(type: "text", nullable: true),
                    StartUpFrame = table.Column<string>(type: "text", nullable: true),
                    Throw = table.Column<bool>(type: "boolean", nullable: false),
                    Tornado = table.Column<bool>(type: "boolean", nullable: false),
                    VideoUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TekkenMovesPending", x => new { x.CharacterName, x.Command });
                    table.ForeignKey(
                        name: "FK_TekkenMovesPending_TekkenCharactersPending_CharacterName",
                        column: x => x.CharacterName,
                        principalTable: "TekkenCharactersPending",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
