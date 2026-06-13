using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class HusBandMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_CD_Husbands_HostId",
            table: "CD");

        migrationBuilder.DropTable(
            name: "AutoHello");

        migrationBuilder.DropPrimaryKey(
            name: "PK_CD",
            table: "CD");

        migrationBuilder.RenameTable(
            name: "CD",
            newName: "HusbandCoolDowns");

        migrationBuilder.RenameColumn(
            name: "HostId",
            table: "HusbandCoolDowns",
            newName: "HusbandId");

        migrationBuilder.RenameIndex(
            name: "IX_CD_HostId",
            table: "HusbandCoolDowns",
            newName: "IX_HusbandCoolDowns_HusbandId");

        migrationBuilder.AddPrimaryKey(
            name: "PK_HusbandCoolDowns",
            table: "HusbandCoolDowns",
            column: "Guid");

        migrationBuilder.CreateTable(
            name: "HusbandAutoHelloCooldowns",
            columns: table => new
            {
                Guid = table.Column<Guid>(type: "uuid", nullable: false),
                HusbandId = table.Column<string>(type: "character varying(50)", nullable: false),
                Time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HusbandAutoHelloCooldowns", x => x.Guid);
                table.ForeignKey(
                    name: "FK_HusbandAutoHelloCooldowns_Husbands_HusbandId",
                    column: x => x.HusbandId,
                    principalTable: "Husbands",
                    principalColumn: "TwitchId");
            });

        migrationBuilder.CreateIndex(
            name: "IX_HusbandAutoHelloCooldowns_HusbandId",
            table: "HusbandAutoHelloCooldowns",
            column: "HusbandId",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_HusbandCoolDowns_Husbands_HusbandId",
            table: "HusbandCoolDowns",
            column: "HusbandId",
            principalTable: "Husbands",
            principalColumn: "TwitchId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_HusbandCoolDowns_Husbands_HusbandId",
            table: "HusbandCoolDowns");

        migrationBuilder.DropTable(
            name: "HusbandAutoHelloCooldowns");

        migrationBuilder.DropPrimaryKey(
            name: "PK_HusbandCoolDowns",
            table: "HusbandCoolDowns");

        migrationBuilder.RenameTable(
            name: "HusbandCoolDowns",
            newName: "CD");

        migrationBuilder.RenameColumn(
            name: "HusbandId",
            table: "CD",
            newName: "HostId");

        migrationBuilder.RenameIndex(
            name: "IX_HusbandCoolDowns_HusbandId",
            table: "CD",
            newName: "IX_CD_HostId");

        migrationBuilder.AddPrimaryKey(
            name: "PK_CD",
            table: "CD",
            column: "Guid");

        migrationBuilder.CreateTable(
            name: "AutoHello",
            columns: table => new
            {
                Guid = table.Column<Guid>(type: "uuid", nullable: false),
                HostId = table.Column<string>(type: "character varying(50)", nullable: false),
                Time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutoHello", x => x.Guid);
                table.ForeignKey(
                    name: "FK_AutoHello_Husbands_HostId",
                    column: x => x.HostId,
                    principalTable: "Husbands",
                    principalColumn: "TwitchId");
            });

        migrationBuilder.CreateIndex(
            name: "IX_AutoHello_HostId",
            table: "AutoHello",
            column: "HostId",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_CD_Husbands_HostId",
            table: "CD",
            column: "HostId",
            principalTable: "Husbands",
            principalColumn: "TwitchId");
    }
}
