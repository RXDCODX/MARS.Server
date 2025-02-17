using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class ResendVkDatabase : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "TelegramMessage",
            table: "ResendLinks",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer"
        );

        migrationBuilder.AlterColumn<decimal>(
            name: "DiscordMessage",
            table: "ResendLinks",
            type: "numeric(20,0)",
            nullable: true,
            oldClrType: typeof(decimal),
            oldType: "numeric(20,0)"
        );

        migrationBuilder.AddColumn<long>(
            name: "VkMessage",
            table: "ResendLinks",
            type: "bigint",
            nullable: true
        );

        migrationBuilder.CreateTable(
            name: "Replies",
            columns: table => new
            {
                Guid = table.Column<Guid>(type: "uuid", nullable: false),
                SourceChat = table.Column<byte>(type: "smallint", nullable: false),
                TelegramMessages = table.Column<int>(type: "integer", nullable: true),
                DiscordMessage = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                VkMessage = table.Column<long>(type: "bigint", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Replies", x => x.Guid);
            }
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

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Replies");

        migrationBuilder.DropTable(name: "VkUsers");

        migrationBuilder.DropColumn(name: "VkMessage", table: "ResendLinks");

        migrationBuilder.AlterColumn<int>(
            name: "TelegramMessage",
            table: "ResendLinks",
            type: "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<decimal>(
            name: "DiscordMessage",
            table: "ResendLinks",
            type: "numeric(20,0)",
            nullable: false,
            defaultValue: 0m,
            oldClrType: typeof(decimal),
            oldType: "numeric(20,0)",
            oldNullable: true
        );
    }
}
