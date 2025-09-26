using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MARS.Server.Migrations;

/// <inheritdoc />
public partial class ChannelRewardsManager : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ChannelRewards",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "text", nullable: false),
                Cost = table.Column<int>(type: "integer", nullable: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                Prompt = table.Column<string>(type: "text", nullable: true),
                BackgroundColor = table.Column<string>(type: "text", nullable: true),
                IsUserInputRequired = table.Column<bool>(type: "boolean", nullable: false),
                IsMaxPerStreamEnabled = table.Column<bool>(type: "boolean", nullable: false),
                MaxPerStream = table.Column<int>(type: "integer", nullable: true),
                IsMaxPerUserPerStreamEnabled = table.Column<bool>(type: "boolean", nullable: false),
                MaxPerUserPerStream = table.Column<int>(type: "integer", nullable: true),
                IsGlobalCooldownEnabled = table.Column<bool>(type: "boolean", nullable: false),
                GlobalCooldownSeconds = table.Column<int>(type: "integer", nullable: true),
                ShouldRedemptionsSkipRequestQueue = table.Column<bool>(
                    type: "boolean",
                    nullable: false
                ),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                TwitchRewardId = table.Column<string>(type: "text", nullable: true),
                MediaInfoId = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChannelRewards", x => x.Id);
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ChannelRewards");
    }
}
