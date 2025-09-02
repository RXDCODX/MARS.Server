using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations.LoggerMigrations;

/// <inheritdoc />
public partial class TriggerForMaxLogs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Создаем функцию для проверки и ограничения количества логов
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION logs.check_logs_count()
            RETURNS TRIGGER AS $$
            BEGIN
                -- Проверяем количество записей в таблице логов
                IF (SELECT COUNT(*) FROM logs."Logs") > 2000 THEN
                    -- Удаляем самые старые записи, оставляя только 2000 самых новых
                    DELETE FROM logs."Logs"
                    WHERE "Id" IN (
                        SELECT "Id" FROM logs."Logs"
                        ORDER BY "WhenLogged" ASC
                        LIMIT (SELECT COUNT(*) FROM logs."Logs") - 2000
                    );
                END IF;
                RETURN NEW;
            END;
            $$
            LANGUAGE plpgsql;
            """
        );

        // Создаем триггер, который будет срабатывать после вставки новой записи
        migrationBuilder.Sql(
            """
            CREATE TRIGGER logs_after_insert_trigger
            AFTER INSERT ON logs."Logs"
            FOR EACH ROW
            EXECUTE FUNCTION logs.check_logs_count();
            """
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Удаляем триггер
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS logs_after_insert_trigger ON logs."Logs";
            """
        );

        // Удаляем функцию
        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS logs.check_logs_count();
            """
        );
    }
}
