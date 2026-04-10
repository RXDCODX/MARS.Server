using MARS.Server.Services.Telegram.PrivateChannelsResender.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class AddChannelProcessingState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ChannelProcessingStates",
            columns: table => new
            {
                ChannelId = table.Column<long>(type: "bigint", nullable: false),
                OffsetId = table.Column<int>(type: "integer", nullable: false),
                MessagesHash = table.Column<long>(type: "bigint", nullable: true),
                LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChannelProcessingStates", x => x.ChannelId);
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_ChannelProcessingStates_ChannelId",
            table: "ChannelProcessingStates",
            column: "ChannelId",
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ChannelProcessingStates");
    }
}
