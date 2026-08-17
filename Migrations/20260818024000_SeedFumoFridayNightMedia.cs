using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class SeedFumoFridayNightMedia : Migration
{
    /// <summary>
    /// Статичный Guid записи Fumo Friday Night в таблице Alerts.
    /// Используется наградой FumoFridayNight_TwitchReward для поиска медиа.
    /// </summary>
    internal static readonly Guid FumoFridayNightMediaId = new(
        "F7F80F55-0000-4655-4D4F-465249444159"
    );

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            UPDATE "Alerts"
            SET "Id" = '{FumoFridayNightMediaId}',
                "MetaInfo_TwitchPointsCost" = -3
            WHERE "MetaInfo_TwitchPointsCost" = 170
              AND "MetaInfo_DisplayName" = 'FumoFridayNight'
            """
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            UPDATE "Alerts"
            SET "MetaInfo_TwitchPointsCost" = 170
            WHERE "Id" = '{FumoFridayNightMediaId}'
            """
        );
    }
}
