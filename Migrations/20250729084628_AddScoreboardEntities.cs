using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddScoreboardEntities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ConfigurationJson", table: "ServiceStates");

        migrationBuilder.CreateTable(
            name: "ScoreboardStates",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                Title = table.Column<string>(type: "text", nullable: false),
                FightRule = table.Column<string>(type: "text", nullable: false),
                MainColor = table.Column<string>(type: "text", nullable: false),
                PlayerNamesColor = table.Column<string>(type: "text", nullable: false),
                TournamentTitleColor = table.Column<string>(type: "text", nullable: false),
                FightModeColor = table.Column<string>(type: "text", nullable: false),
                ScoreColor = table.Column<string>(type: "text", nullable: false),
                BackgroundColor = table.Column<string>(type: "text", nullable: false),
                BorderColor = table.Column<string>(type: "text", nullable: false),
                IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                AnimationDuration = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                UpdatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScoreboardStates", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "ScoreboardPlayers",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                Name = table.Column<string>(type: "text", nullable: false),
                Sponsor = table.Column<string>(type: "text", nullable: false),
                Score = table.Column<int>(type: "integer", nullable: false),
                Tag = table.Column<string>(type: "text", nullable: false),
                Flag = table.Column<string>(type: "text", nullable: false),
                Final = table.Column<string>(type: "text", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                ScoreboardStateId = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScoreboardPlayers", x => x.Id);
                table.ForeignKey(
                    name: "FK_ScoreboardPlayers_ScoreboardStates_ScoreboardStateId",
                    column: x => x.ScoreboardStateId,
                    principalTable: "ScoreboardStates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_ScoreboardPlayers_ScoreboardStateId_Position",
            table: "ScoreboardPlayers",
            columns: ["ScoreboardStateId", "Position"],
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ScoreboardPlayers");

        migrationBuilder.DropTable(name: "ScoreboardStates");

        migrationBuilder.AddColumn<string>(
            name: "ConfigurationJson",
            table: "ServiceStates",
            type: "text",
            nullable: true
        );
    }
}
