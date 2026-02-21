using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddTelegramDiscordBridge : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TelegramDiscordChannelBindings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TelegramChannelId = table.Column<long>(type: "bigint", nullable: false),
                DiscordChannelId = table.Column<string>(type: "character varying(64)", nullable: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TelegramDiscordChannelBindings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TelegramDiscordChannelStates",
            columns: table => new
            {
                TelegramChannelId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                LastProcessedMessageId = table.Column<int>(type: "integer", nullable: false),
                LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TelegramDiscordChannelStates", x => x.TelegramChannelId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TelegramDiscordChannelBindings_TelegramChannelId_DiscordCha~",
            table: "TelegramDiscordChannelBindings",
            columns: new[] { "TelegramChannelId", "DiscordChannelId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TelegramDiscordChannelBindings");

        migrationBuilder.DropTable(
            name: "TelegramDiscordChannelStates");
    }
}
