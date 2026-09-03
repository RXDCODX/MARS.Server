using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class BooruMigrationCOnfig : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BooruAutoPostConfigs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Source = table.Column<int>(type: "integer", nullable: false),
                TargetPlatform = table.Column<int>(type: "integer", nullable: false),
                DiscordChannelId = table.Column<string>(type: "character varying(64)", nullable: false),
                TelegramChannelId = table.Column<long>(type: "bigint", nullable: true),
                TargetPostCount = table.Column<int>(type: "integer", nullable: false),
                SpecificPostId = table.Column<int>(type: "integer", nullable: true),
                Tags = table.Column<string>(type: "text", nullable: false),
                CronExpression = table.Column<string>(type: "text", nullable: false),
                PlanningHorizonDays = table.Column<int>(type: "integer", nullable: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                Message = table.Column<string>(type: "text", nullable: false),
                TelegramParseMode = table.Column<int>(type: "integer", nullable: false),
                LastExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BooruAutoPostConfigs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "BooruScheduledPosts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                Source = table.Column<int>(type: "integer", nullable: false),
                ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ErrorMessage = table.Column<string>(type: "text", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BooruScheduledPosts", x => x.Id);
                table.ForeignKey(
                    name: "FK_BooruScheduledPosts_BooruAutoPostConfigs_ConfigId",
                    column: x => x.ConfigId,
                    principalTable: "BooruAutoPostConfigs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BooruScheduledPosts_ConfigId",
            table: "BooruScheduledPosts",
            column: "ConfigId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BooruScheduledPosts");

        migrationBuilder.DropTable(
            name: "BooruAutoPostConfigs");
    }
}
