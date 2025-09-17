using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddStreamArchiveConfigs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StreamArchiveConfigs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TelegramChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                FileNameFormat = table.Column<string>(type: "text", nullable: false),
                CheckSpan = table.Column<TimeSpan>(type: "interval", nullable: false),
                FolderPath = table.Column<string>(type: "text", nullable: false),
                IsConvertFile = table.Column<bool>(type: "boolean", nullable: false),
                FileConvertType = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StreamArchiveConfigs", x => x.Id);
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StreamArchiveConfigs");
    }
}
