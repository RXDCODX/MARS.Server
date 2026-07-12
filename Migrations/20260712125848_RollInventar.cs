using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class RollInventar : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RollCooldowns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TwitchUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                RollType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                LastRollTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RollCooldowns", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UserFumoCollections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TwitchUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                FumoMfcId = table.Column<int>(type: "integer", nullable: false),
                Count = table.Column<int>(type: "integer", nullable: false),
                FirstObtained = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastObtained = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserFumoCollections", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UserMikuCollections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TwitchUserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                MikuPageId = table.Column<int>(type: "integer", nullable: false),
                Count = table.Column<int>(type: "integer", nullable: false),
                FirstObtained = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastObtained = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserMikuCollections", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RollCooldowns_TwitchUserId_RollType",
            table: "RollCooldowns",
            columns: new[] { "TwitchUserId", "RollType" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserFumoCollections_TwitchUserId_FumoMfcId",
            table: "UserFumoCollections",
            columns: new[] { "TwitchUserId", "FumoMfcId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserMikuCollections_TwitchUserId_MikuPageId",
            table: "UserMikuCollections",
            columns: new[] { "TwitchUserId", "MikuPageId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RollCooldowns");

        migrationBuilder.DropTable(
            name: "UserFumoCollections");

        migrationBuilder.DropTable(
            name: "UserMikuCollections");
    }
}
