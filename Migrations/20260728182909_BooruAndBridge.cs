using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class BooruAndBridge : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NSFWBooruAutoPostConfigs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DiscordChannelId = table.Column<string>(type: "character varying(64)", nullable: false),
                Tags = table.Column<string>(type: "text", nullable: false),
                CronExpression = table.Column<string>(type: "text", nullable: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                LastExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NSFWBooruAutoPostConfigs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PostedImageRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Source = table.Column<string>(type: "text", nullable: false),
                ImageId = table.Column<int>(type: "integer", nullable: false),
                DiscordChannelId = table.Column<string>(type: "character varying(64)", nullable: false),
                PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PostedImageRecords", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PostedImageRecords_Source_ImageId_DiscordChannelId",
            table: "PostedImageRecords",
            columns: new[] { "Source", "ImageId", "DiscordChannelId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "NSFWBooruAutoPostConfigs");

        migrationBuilder.DropTable(
            name: "PostedImageRecords");
    }
}
