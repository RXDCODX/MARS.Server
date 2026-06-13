using MARS.Server.Services.WaifuRoll.Entitys;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class PopulateHostCDAndAutoHello : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Добавление записей в CD (HusbandCoolDown) для всех hosts, у которых их нет
        migrationBuilder.Sql(
            $@"
                INSERT INTO ""CD"" (""{nameof(HusbandCoolDown.Guid)}"", ""{nameof(HusbandCoolDown.HusbandId)}"", ""{nameof(HusbandCoolDown.Time)}"")
                SELECT 
                    gen_random_uuid(),
                    h.""{nameof(Husband.TwitchId)}"",
                    NOW() AT TIME ZONE 'UTC'
                FROM ""Hosts"" h
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""CD"" cd WHERE cd.""{nameof(HusbandCoolDown.HusbandId)}"" = h.""{nameof(Husband.TwitchId)}""
                );
            "
        );

        // Добавление записей в AutoHello (HostAutoHello) для всех hosts, у которых их нет
        migrationBuilder.Sql(
            $@"
                INSERT INTO ""AutoHello"" (""{nameof(HusbandAutoHello.Guid)}"", ""{nameof(HusbandAutoHello.HusbandId)}"", ""{nameof(HusbandAutoHello.Time)}"")
                SELECT 
                    gen_random_uuid(),
                    h.""{nameof(Husband.TwitchId)}"",
                    NOW() AT TIME ZONE 'UTC'
                FROM ""Hosts"" h
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""AutoHello"" ah WHERE ah.""{nameof(HusbandAutoHello.HusbandId)}"" = h.""{nameof(Husband.TwitchId)}""
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
