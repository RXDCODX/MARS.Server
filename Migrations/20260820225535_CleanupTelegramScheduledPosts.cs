using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations.MigrationsDb
{
    /// <inheritdoc />
    public partial class CleanupTelegramScheduledPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Отложенные посты Telegram больше не отслеживаются в БД — источник истины: Telegram API
            migrationBuilder.Sql(
                """
                DELETE FROM "DanbooruScheduledPosts" AS p
                USING "DanbooruAutoPostConfigs" AS c
                WHERE p."ConfigId" = c."Id" AND c."TargetPlatform" = 1;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
