using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class FrameDataChangesNewVersion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TekkenCharactersPending",
            columns: table => new
            {
                Name = table.Column<string>(type: "text", nullable: false),
                LinkToImage = table.Column<string>(
                    type: "character varying(300)",
                    maxLength: 300,
                    nullable: true
                ),
                PageUrl = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false
                ),
                Image = table.Column<byte[]>(type: "bytea", nullable: true),
                ImageExtension = table.Column<string>(
                    type: "character varying(20)",
                    maxLength: 20,
                    nullable: true
                ),
                AvatarImage = table.Column<byte[]>(type: "bytea", nullable: true),
                AvatarImageExtension = table.Column<string>(
                    type: "character varying(20)",
                    maxLength: 20,
                    nullable: true
                ),
                FullBodyImage = table.Column<byte[]>(type: "bytea", nullable: true),
                FullBodyImageExtension = table.Column<string>(
                    type: "character varying(20)",
                    maxLength: 20,
                    nullable: true
                ),
                LastUpdateTime = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                Description = table.Column<string>(type: "text", nullable: true),
                Strengths = table.Column<string[]>(type: "text[]", nullable: true),
                Weaknesess = table.Column<string[]>(type: "text[]", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TekkenCharactersPending", x => x.Name);
            }
        );

        migrationBuilder.CreateTable(
            name: "TekkenMovesPending",
            columns: table => new
            {
                CharacterName = table.Column<string>(type: "text", nullable: false),
                Command = table.Column<string>(type: "text", nullable: false),
                StanceCode = table.Column<string>(type: "text", nullable: false),
                StanceName = table.Column<string>(type: "text", nullable: true),
                HeatEngage = table.Column<bool>(type: "boolean", nullable: false),
                HeatSmash = table.Column<bool>(type: "boolean", nullable: false),
                PowerCrush = table.Column<bool>(type: "boolean", nullable: false),
                Throw = table.Column<bool>(type: "boolean", nullable: false),
                Homing = table.Column<bool>(type: "boolean", nullable: false),
                Tornado = table.Column<bool>(type: "boolean", nullable: false),
                HeatBurst = table.Column<bool>(type: "boolean", nullable: false),
                RequiresHeat = table.Column<bool>(type: "boolean", nullable: false),
                HitLevel = table.Column<string>(type: "text", nullable: true),
                Damage = table.Column<string>(type: "text", nullable: true),
                StartUpFrame = table.Column<string>(type: "text", nullable: true),
                BlockFrame = table.Column<string>(type: "text", nullable: true),
                HitFrame = table.Column<string>(type: "text", nullable: true),
                CounterHitFrame = table.Column<string>(type: "text", nullable: true),
                Notes = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TekkenMovesPending", x => new { x.CharacterName, x.Command });
                table.ForeignKey(
                    name: "FK_TekkenMovesPending_TekkenCharactersPending_CharacterName",
                    column: x => x.CharacterName,
                    principalTable: "TekkenCharactersPending",
                    principalColumn: "Name"
                );
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TekkenMovesPending");

        migrationBuilder.DropTable(name: "TekkenCharactersPending");
    }
}
