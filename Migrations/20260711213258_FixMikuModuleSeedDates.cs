using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class FixMikuModuleSeedDates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE \"MikuModules\" SET \"WhenAdded\" = '2024-01-01 00:00:00+00', \"LastOrder\" = '2024-01-01 00:00:00+00' WHERE \"WhenAdded\" < '2000-01-01'"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {

    }
}
