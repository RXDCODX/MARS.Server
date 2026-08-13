using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class DanbooruAutoPostAddColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "TargetPlatform",
            table: "DanbooruAutoPostConfigs",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<long>(
            name: "TelegramChannelId",
            table: "DanbooruAutoPostConfigs",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DanbooruPostId",
            table: "DanbooruAutoPostConfigs",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ScheduledAtUtc",
            table: "DanbooruAutoPostConfigs",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "TargetPostCount",
            table: "DanbooruAutoPostConfigs",
            type: "integer",
            nullable: false,
            defaultValue: 1);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "TargetPlatform",
            table: "DanbooruAutoPostConfigs");

        migrationBuilder.DropColumn(
            name: "TelegramChannelId",
            table: "DanbooruAutoPostConfigs");

        migrationBuilder.DropColumn(
            name: "DanbooruPostId",
            table: "DanbooruAutoPostConfigs");

        migrationBuilder.DropColumn(
            name: "ScheduledAtUtc",
            table: "DanbooruAutoPostConfigs");

        migrationBuilder.DropColumn(
            name: "TargetPostCount",
            table: "DanbooruAutoPostConfigs");
    }
}
