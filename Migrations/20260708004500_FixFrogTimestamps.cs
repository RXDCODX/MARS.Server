using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class FixFrogTimestamps : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE \"Frogs\" SET \"LastOrder\" = '2000-01-01 00:00:00+00', \"WhenAdded\" = '2000-01-01 00:00:00+00'");
        migrationBuilder.Sql("UPDATE \"Frogs\" SET \"RussianName\" = 'Песчаная бородавница' WHERE \"Pid\" = 1");
        migrationBuilder.Sql("UPDATE \"Frogs\" SET \"RussianName\" = 'Элчоская бородавница' WHERE \"Pid\" = 141");
        migrationBuilder.Sql("UPDATE \"Frogs\" SET \"RussianName\" = 'Галивинкуская бородавница' WHERE \"Pid\" = 142");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No reverse - old data was corrupt anyway
    }
}
