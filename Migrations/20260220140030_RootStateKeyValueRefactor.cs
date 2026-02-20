using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class RootStateKeyValueRefactor : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RootState",
            columns: table => new
            {
                Name = table.Column<string>(type: "text", nullable: false),
                Value = table.Column<string>(type: "text", nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                TypeDescription = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RootState", x => x.Name);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO "RootState" ("Name", "Value", "Description", "TypeDescription")
            SELECT
                'RandomMemeOnlineIsStop',
                CASE WHEN "RandomMemeOnlineIsStop" THEN 'True' ELSE 'False' END,
                'Флаг остановки сервиса RandomMemeOnline',
                'bool'
            FROM "ApplicationState"
            ORDER BY "Id"
            LIMIT 1;

            INSERT INTO "RootState" ("Name", "Value", "Description", "TypeDescription")
            SELECT
                'PuntoSwitcherFilterEnabled',
                CASE WHEN "PuntoSwitcherFilterEnabled" THEN 'True' ELSE 'False' END,
                'Флаг включения фильтра PuntoSwitcher',
                'bool'
            FROM "ApplicationState"
            ORDER BY "Id"
            LIMIT 1;

            INSERT INTO "RootState" ("Name", "Value", "Description", "TypeDescription")
            SELECT
                'WaifuRollCooldownMinutes',
                CAST("WaifuRollCooldownMinutes" AS text),
                'Кулдаун ролла вайфу в минутах',
                'long'
            FROM "ApplicationState"
            ORDER BY "Id"
            LIMIT 1;
            """
        );

        migrationBuilder.Sql(
            """
            INSERT INTO "RootState" ("Name", "Value", "Description", "TypeDescription")
            SELECT 'RandomMemeOnlineIsStop', 'False', 'Флаг остановки сервиса RandomMemeOnline', 'bool'
            WHERE NOT EXISTS (SELECT 1 FROM "RootState" WHERE "Name" = 'RandomMemeOnlineIsStop');

            INSERT INTO "RootState" ("Name", "Value", "Description", "TypeDescription")
            SELECT 'PuntoSwitcherFilterEnabled', 'True', 'Флаг включения фильтра PuntoSwitcher', 'bool'
            WHERE NOT EXISTS (SELECT 1 FROM "RootState" WHERE "Name" = 'PuntoSwitcherFilterEnabled');

            INSERT INTO "RootState" ("Name", "Value", "Description", "TypeDescription")
            SELECT 'WaifuRollCooldownMinutes', '20', 'Кулдаун ролла вайфу в минутах', 'long'
            WHERE NOT EXISTS (SELECT 1 FROM "RootState" WHERE "Name" = 'WaifuRollCooldownMinutes');
            """
        );

        migrationBuilder.DropTable(name: "ApplicationState");

        migrationBuilder.CreateIndex(
            name: "IX_RootState_Name",
            table: "RootState",
            column: "Name",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_RootState_Name",
            table: "RootState");

        migrationBuilder.CreateTable(
            name: "ApplicationState",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                RandomMemeOnlineIsStop = table.Column<bool>(
                    type: "boolean",
                    nullable: false
                ),
                PuntoSwitcherFilterEnabled = table.Column<bool>(
                    type: "boolean",
                    nullable: false
                ),
                WaifuRollCooldownMinutes = table.Column<long>(type: "bigint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApplicationState", x => x.Id);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO "ApplicationState" ("Id", "RandomMemeOnlineIsStop", "PuntoSwitcherFilterEnabled", "WaifuRollCooldownMinutes")
            VALUES
            (
                1,
                COALESCE((SELECT CAST("Value" AS boolean) FROM "RootState" WHERE "Name" = 'RandomMemeOnlineIsStop' LIMIT 1), false),
                COALESCE((SELECT CAST("Value" AS boolean) FROM "RootState" WHERE "Name" = 'PuntoSwitcherFilterEnabled' LIMIT 1), true),
                COALESCE((SELECT CAST("Value" AS bigint) FROM "RootState" WHERE "Name" = 'WaifuRollCooldownMinutes' LIMIT 1), 20)
            );
            """
        );

        migrationBuilder.DropTable(name: "RootState");

    }
}
