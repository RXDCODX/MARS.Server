using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class TwitchCommands : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TwitchMessageTemplates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                MessageTemplate = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: false
                ),
                Description = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: true
                ),
                TriggerWord = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: false
                ),
                AuthorColor = table.Column<string>(
                    type: "character varying(7)",
                    maxLength: 7,
                    nullable: true
                ),
                AuthorName = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: true
                ),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                Priority = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                UpdatedAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                UsageCount = table.Column<int>(type: "integer", nullable: false),
                RandomChance = table.Column<int>(type: "integer", nullable: false),
                CooldownSeconds = table.Column<int>(type: "integer", nullable: false),
                LastTriggeredAt = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TwitchMessageTemplates", x => x.Id);
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TwitchMessageTemplates");
    }
}
