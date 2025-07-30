using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class AddScoreboardLayout : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ScoreboardLayouts",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                HeaderTop = table.Column<int>(type: "integer", nullable: false),
                HeaderLeft = table.Column<int>(type: "integer", nullable: false),
                PlayersTop = table.Column<int>(type: "integer", nullable: false),
                PlayersLeft = table.Column<int>(type: "integer", nullable: false),
                PlayersRight = table.Column<int>(type: "integer", nullable: false),
                HeaderHeight = table.Column<int>(type: "integer", nullable: false),
                HeaderWidth = table.Column<int>(type: "integer", nullable: false),
                PlayerBarHeight = table.Column<int>(type: "integer", nullable: false),
                PlayerBarWidth = table.Column<int>(type: "integer", nullable: false),
                ScoreSize = table.Column<int>(type: "integer", nullable: false),
                FlagSize = table.Column<int>(type: "integer", nullable: false),
                Spacing = table.Column<int>(type: "integer", nullable: false),
                Padding = table.Column<int>(type: "integer", nullable: false),
                ShowHeader = table.Column<bool>(type: "boolean", nullable: false),
                ShowFlags = table.Column<bool>(type: "boolean", nullable: false),
                ShowSponsors = table.Column<bool>(type: "boolean", nullable: false),
                ShowTags = table.Column<bool>(type: "boolean", nullable: false),
                ScoreboardStateId = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScoreboardLayouts", x => x.Id);
                table.ForeignKey(
                    name: "FK_ScoreboardLayouts_ScoreboardStates_ScoreboardStateId",
                    column: x => x.ScoreboardStateId,
                    principalTable: "ScoreboardStates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_ScoreboardLayouts_ScoreboardStateId",
            table: "ScoreboardLayouts",
            column: "ScoreboardStateId",
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ScoreboardLayouts");
    }
}
