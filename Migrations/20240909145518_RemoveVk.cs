using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class RemoveVk : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VkUsers");

        migrationBuilder.DropColumn(name: "VkMessage", table: "ResendLinks");

        migrationBuilder.DropColumn(name: "VkMessage", table: "Replies");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "VkMessage",
            table: "ResendLinks",
            type: "bigint",
            nullable: true
        );

        migrationBuilder.AddColumn<long>(
            name: "VkMessage",
            table: "Replies",
            type: "bigint",
            nullable: true
        );

        migrationBuilder.CreateTable(
            name: "VkUsers",
            columns: table => new
            {
                UserId = table
                    .Column<long>(type: "bigint", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                FirstName = table.Column<string>(type: "text", nullable: false),
                LastName = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VkUsers", x => x.UserId);
            }
        );
    }
}
