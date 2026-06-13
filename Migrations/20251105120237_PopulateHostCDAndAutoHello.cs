using MARS.Server.Services.WaifuRoll.Entitys;
using Microsoft.EntityFrameworkCore.Migrations;
using Host = MARS.Server.Services.WaifuRoll.Entitys.Host;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class PopulateHostCDAndAutoHello : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Добавление записей в CD (HostCoolDown) для всех hosts, у которых их нет
        migrationBuilder.Sql(
            $@"
                INSERT INTO ""CD"" (""{nameof(HostCoolDown.Guid)}"", ""{nameof(HostCoolDown.HostId)}"", ""{nameof(HostCoolDown.Time)}"")
                SELECT 
                    gen_random_uuid(),
                    h.""{nameof(Host.TwitchId)}"",
                    NOW() AT TIME ZONE 'UTC'
                FROM ""Hosts"" h
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""CD"" cd WHERE cd.""{nameof(HostCoolDown.HostId)}"" = h.""{nameof(Host.TwitchId)}""
                );
            "
        );

        // Добавление записей в AutoHello (HostAutoHello) для всех hosts, у которых их нет
        migrationBuilder.Sql(
            $@"
                INSERT INTO ""AutoHello"" (""{nameof(HostAutoHello.Guid)}"", ""{nameof(HostAutoHello.HostId)}"", ""{nameof(HostAutoHello.Time)}"")
                SELECT 
                    gen_random_uuid(),
                    h.""{nameof(Host.TwitchId)}"",
                    NOW() AT TIME ZONE 'UTC'
                FROM ""Hosts"" h
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""AutoHello"" ah WHERE ah.""{nameof(HostAutoHello.HostId)}"" = h.""{nameof(Host.TwitchId)}""
                );
            "
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // В Down миграции мы не удаляем добавленные записи,
        // так как они могут быть важны для работы системы
        // и их удаление может привести к потере данных
    }
}
