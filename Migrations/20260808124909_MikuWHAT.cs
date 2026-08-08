using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class MikuWHAT : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            table: "RootState",
            columns: new[] { "Name", "Description", "TypeDescription", "Value" },
            values: new object[] { "SevenTvProxyUrl", "Прокси для 7TV API: http://user:pass@host:port или socks5://user:pass@host:port", "string", "" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            table: "RootState",
            keyColumn: "Name",
            keyValue: "SevenTvProxyUrl");
    }
}
