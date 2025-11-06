using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class MikuMondayTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MikuTracks",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                Number = table.Column<int>(type: "integer", nullable: false),
                Artist = table.Column<string>(type: "text", nullable: false),
                Title = table.Column<string>(type: "text", nullable: false),
                Url = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MikuTracks", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "MikuMondayActivations",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                TwitchUserId = table.Column<string>(type: "text", nullable: false),
                DisplayName = table.Column<string>(type: "text", nullable: false),
                MikuTrackId = table.Column<int>(type: "integer", nullable: false),
                ActivatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                WeekOfYear = table.Column<int>(type: "integer", nullable: false),
                Year = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MikuMondayActivations", x => x.Id);
                table.ForeignKey(
                    name: "FK_MikuMondayActivations_MikuTracks_MikuTrackId",
                    column: x => x.MikuTrackId,
                    principalTable: "MikuTracks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_MikuMondayActivations_MikuTrackId",
            table: "MikuMondayActivations",
            column: "MikuTrackId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_MikuMondayActivations_TwitchUserId_Year_WeekOfYear",
            table: "MikuMondayActivations",
            columns: ["TwitchUserId", "Year", "WeekOfYear"],
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_MikuTracks_Number",
            table: "MikuTracks",
            column: "Number",
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MikuMondayActivations");

        migrationBuilder.DropTable(name: "MikuTracks");
    }
}
