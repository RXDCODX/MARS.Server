using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Telegramus.Migrations;

/// <inheritdoc />
public partial class LiveChannelRename : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LiveChannelState",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                State = table.Column<int>(type: "integer", nullable: false),
                LastStart = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                LastEnd = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                IsTelegramChannelRenamed = table.Column<bool>(type: "boolean", nullable: false),
                IsVkGroupRenamed = table.Column<bool>(type: "boolean", nullable: false),
                IsDiscordGuiildNameRenamed = table.Column<bool>(type: "boolean", nullable: false),
                IsDiscordChannelRenamed = table.Column<bool>(type: "boolean", nullable: false),
                ActualPrefix = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LiveChannelState", x => x.Id);
            }
        );

        migrationBuilder.InsertData(
            table: "LiveChannelState",
            columns: new[]
            {
                "Id",
                "ActualPrefix",
                "IsDiscordChannelRenamed",
                "IsDiscordGuiildNameRenamed",
                "IsTelegramChannelRenamed",
                "IsVkGroupRenamed",
                "LastEnd",
                "LastStart",
                "State",
            },
            values: new object[]
            {
                1,
                "[LIVE 🔴] ",
                false,
                false,
                false,
                false,
                new DateTimeOffset(
                    new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                    new TimeSpan(0, 0, 0, 0, 0)
                ),
                new DateTimeOffset(
                    new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                    new TimeSpan(0, 0, 0, 0, 0)
                ),
                0,
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LiveChannelState");
    }
}
