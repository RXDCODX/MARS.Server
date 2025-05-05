using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class TelegramOffset : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TelegramUpdateReceiverOffset",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Offset = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TelegramUpdateReceiverOffset", x => x.Id);
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TelegramUpdateReceiverOffset");
    }
}
