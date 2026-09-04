using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations
{
    /// <inheritdoc />
    public partial class BooruMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Migrate data from DanbooruAutoPostConfigs → BooruAutoPostConfigs ---
            migrationBuilder.Sql(@"
                INSERT INTO ""BooruAutoPostConfigs"" (
                    ""Id"", ""Source"", ""TargetPlatform"", ""DiscordChannelId"",
                    ""TelegramChannelId"", ""TargetPostCount"", ""SpecificPostId"",
                    ""Tags"", ""CronExpression"", ""PlanningHorizonDays"",
                    ""IsEnabled"", ""Message"", ""TelegramParseMode"",
                    ""LastExecutedAtUtc"", ""CreatedAtUtc"", ""UpdatedAtUtc""
                )
                SELECT
                    ""Id"",
                    0,
                    ""TargetPlatform"",
                    ""DiscordChannelId"",
                    ""TelegramChannelId"",
                    ""TargetPostCount"",
                    ""DanbooruPostId"",
                    ""Tags"",
                    ""CronExpression"",
                    ""PlanningHorizonDays"",
                    ""IsEnabled"",
                    ""Message"",
                    ""TelegramParseMode"",
                    ""LastExecutedAtUtc"",
                    ""CreatedAtUtc"",
                    ""UpdatedAtUtc""
                FROM ""DanbooruAutoPostConfigs""
            ");

            // --- Migrate data from NSFWBooruAutoPostConfigs → BooruAutoPostConfigs ---
            migrationBuilder.Sql(@"
                INSERT INTO ""BooruAutoPostConfigs"" (
                    ""Id"", ""Source"", ""TargetPlatform"", ""DiscordChannelId"",
                    ""TelegramChannelId"", ""TargetPostCount"", ""SpecificPostId"",
                    ""Tags"", ""CronExpression"", ""PlanningHorizonDays"",
                    ""IsEnabled"", ""Message"", ""TelegramParseMode"",
                    ""LastExecutedAtUtc"", ""CreatedAtUtc"", ""UpdatedAtUtc""
                )
                SELECT
                    ""Id"",
                    1,
                    0,
                    ""DiscordChannelId"",
                    NULL,
                    1,
                    NULL,
                    ""Tags"",
                    ""CronExpression"",
                    60,
                    ""IsEnabled"",
                    ""Message"",
                    0,
                    ""LastExecutedAtUtc"",
                    ""CreatedAtUtc"",
                    ""UpdatedAtUtc""
                FROM ""NSFWBooruAutoPostConfigs""
            ");

            // --- Migrate data from DanbooruScheduledPosts → BooruScheduledPosts ---
            migrationBuilder.Sql(@"
                INSERT INTO ""BooruScheduledPosts"" (
                    ""Id"", ""ConfigId"", ""Source"", ""ScheduledAtUtc"",
                    ""Status"", ""PostedAtUtc"", ""ErrorMessage"", ""CreatedAtUtc""
                )
                SELECT
                    ds.""Id"",
                    ds.""ConfigId"",
                    0,
                    ds.""ScheduledAtUtc"",
                    ds.""Status"",
                    ds.""PostedAtUtc"",
                    ds.""ErrorMessage"",
                    ds.""CreatedAtUtc""
                FROM ""DanbooruScheduledPosts"" ds
                INNER JOIN ""DanbooruAutoPostConfigs"" dc ON ds.""ConfigId"" = dc.""Id""
            ");

            // --- Drop old tables ---
            migrationBuilder.DropTable(
                name: "DanbooruScheduledPosts");

            migrationBuilder.DropTable(
                name: "NSFWBooruAutoPostConfigs");

            migrationBuilder.DropTable(
                name: "DanbooruAutoPostConfigs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DanbooruAutoPostConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CronExpression = table.Column<string>(type: "text", nullable: false),
                    DanbooruPostId = table.Column<int>(type: "integer", nullable: true),
                    DiscordChannelId = table.Column<string>(type: "character varying(64)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    PlanningHorizonDays = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: false),
                    TargetPlatform = table.Column<int>(type: "integer", nullable: false),
                    TargetPostCount = table.Column<int>(type: "integer", nullable: false),
                    TelegramChannelId = table.Column<long>(type: "bigint", nullable: true),
                    TelegramParseMode = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DanbooruAutoPostConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NSFWBooruAutoPostConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CronExpression = table.Column<string>(type: "text", nullable: false),
                    DiscordChannelId = table.Column<string>(type: "character varying(64)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NSFWBooruAutoPostConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DanbooruScheduledPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DanbooruScheduledPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DanbooruScheduledPosts_DanbooruAutoPostConfigs_ConfigId",
                        column: x => x.ConfigId,
                        principalTable: "DanbooruAutoPostConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DanbooruScheduledPosts_ConfigId",
                table: "DanbooruScheduledPosts",
                column: "ConfigId");
        }
    }
}
