using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class WaifuShikiIdPK : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(name: "PK_Waifus", table: "Waifus");

        migrationBuilder.DropColumn(name: "ID", table: "Waifus");

        migrationBuilder.AlterColumn<string>(
            name: "ShikiId",
            table: "Waifus",
            type: "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<string>(
            name: "WaifuRollId",
            table: "Hosts",
            type: "text",
            nullable: true,
            oldClrType: typeof(long),
            oldType: "bigint"
        );

        migrationBuilder.AddPrimaryKey(name: "PK_Waifus", table: "Waifus", column: "ShikiId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(name: "PK_Waifus", table: "Waifus");

        migrationBuilder.AlterColumn<string>(
            name: "ShikiId",
            table: "Waifus",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text"
        );

        migrationBuilder
            .AddColumn<long>(
                name: "ID",
                table: "Waifus",
                type: "bigint",
                nullable: false,
                defaultValue: 0L
            )
            .Annotation(
                "Npgsql:ValueGenerationStrategy",
                NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
            );

        migrationBuilder.AlterColumn<long>(
            name: "WaifuRollId",
            table: "Hosts",
            type: "bigint",
            nullable: false,
            defaultValue: 0L,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true
        );

        migrationBuilder.AddPrimaryKey(name: "PK_Waifus", table: "Waifus", column: "ID");
    }
}
