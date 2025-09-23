using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class CleanupScoreboard : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Оставляем одну (последнюю) запись состояния и удаляем остальные.
        // Каскадные удаления очистят связанные записи игроков и лейаута.
        migrationBuilder.Sql(
            """
            WITH keep AS (
                SELECT "Id" FROM "ScoreboardStates"
                ORDER BY "CreatedAt" DESC, "Id" DESC
                LIMIT 1
            )
            DELETE FROM "ScoreboardStates"
            WHERE "Id" NOT IN (SELECT "Id" FROM keep);

            -- Гарантируем единственную строку в будущем: уникальный индекс по константному выражению
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_ScoreboardStates_OnlyOne" ON "ScoreboardStates" ((true));
            """
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Откатываем только ограничение (данные вернуть невозможно)
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""UX_ScoreboardStates_OnlyOne"";");
    }
}
