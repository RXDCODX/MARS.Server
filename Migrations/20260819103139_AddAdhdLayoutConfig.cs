using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MARS.Server.Migrations.MigrationsDb
{
    /// <inheritdoc />
    public partial class AddAdhdLayoutConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdhdLayoutConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShowRainEffect = table.Column<bool>(type: "boolean", nullable: false),
                    ShowDVDLogos = table.Column<bool>(type: "boolean", nullable: false),
                    ShowBreakingNews = table.Column<bool>(type: "boolean", nullable: false),
                    ShowStreamerVideo = table.Column<bool>(type: "boolean", nullable: false),
                    ShowFitnessVideo = table.Column<bool>(type: "boolean", nullable: false),
                    ShowGTAVideo = table.Column<bool>(type: "boolean", nullable: false),
                    ShowHydraulicMobileVideo = table.Column<bool>(type: "boolean", nullable: false),
                    ShowSlimeVideo = table.Column<bool>(type: "boolean", nullable: false),
                    ShowMukbangVideo = table.Column<bool>(type: "boolean", nullable: false),
                    ShowQuiz = table.Column<bool>(type: "boolean", nullable: false),
                    ShowSurfer = table.Column<bool>(type: "boolean", nullable: false),
                    ShowLOFIGirl = table.Column<bool>(type: "boolean", nullable: false),
                    ShowCatisa = table.Column<bool>(type: "boolean", nullable: false),
                    ShowNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdhdLayoutConfig", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdhdLayoutConfig");
        }
    }
}
