using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class HSREnergy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HonkaiMarkupUser",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TwitchId = table.Column<string>(type: "text", nullable: true),
                TelegramId = table.Column<long>(type: "bigint", nullable: true),
                CreatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                LtmidV2 = table.Column<string>(type: "text", nullable: false),
                LTokenV2 = table.Column<string>(type: "text", nullable: false),
                LtuidV2 = table.Column<string>(type: "text", nullable: false),
                LastAutoMarkup = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HonkaiMarkupUser", x => x.Id);
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "HonkaiMarkupUser");
    }
}
