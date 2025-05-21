using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class NewMemeOrderType : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "MemeTypeId",
            table: "RandomMemeOrder",
            type: "integer",
            nullable: true
        );

        migrationBuilder.CreateTable(
            name: "RandomMemeType",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                Name = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: false
                ),
                FolderPath = table.Column<string>(
                    type: "text",
                    maxLength: 2147483647,
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RandomMemeType", x => x.Id);
            }
        );

        migrationBuilder.InsertData(
            table: "RandomMemeType",
            columns: ["Id", "FolderPath", "Name"],
            values: new object[,]
            {
                { 2, "Alerts\\random_meme", "Random Meme" },
                { 3, "Alerts\\zvik", "Random Sound" },
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_RandomMemeOrder_MemeTypeId",
            table: "RandomMemeOrder",
            column: "MemeTypeId"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_RandomMemeOrder_RandomMemeType_MemeTypeId",
            table: "RandomMemeOrder",
            column: "MemeTypeId",
            principalTable: "RandomMemeType",
            principalColumn: "Id"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_RandomMemeOrder_RandomMemeType_MemeTypeId",
            table: "RandomMemeOrder"
        );

        migrationBuilder.DropTable(name: "RandomMemeType");

        migrationBuilder.DropIndex(name: "IX_RandomMemeOrder_MemeTypeId", table: "RandomMemeOrder");

        migrationBuilder.DropColumn(name: "MemeTypeId", table: "RandomMemeOrder");
    }
}
